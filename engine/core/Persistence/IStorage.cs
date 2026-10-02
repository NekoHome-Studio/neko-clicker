namespace NekoClicker.Core.Persistence;

/// <summary>
/// 键值存储抽象：存档最终写到哪里由宿主决定。<para>
/// 控制台/桌面用 <see cref="FileStorage"/>，测试用 <see cref="MemoryStorage"/>，
/// 将来接 Blazor/Unity 时换成 localStorage 或 PlayerPrefs 实现即可，
/// 引擎与 <see cref="SaveManager"/> 不需要任何改动。
/// </para>
/// </summary>
public interface IStorage
{
    /// <summary>指定键是否存在。</summary>
    bool Exists(string key);

    /// <summary>读取内容；键不存在时返回 <c>null</c>。</summary>
    string? Read(string key);

    /// <summary>写入（覆盖）内容。</summary>
    /// <remarks>
    /// 落盘型实现（<see cref="FileStorage"/>）在替换既有内容之前会先验证新内容读得回来，
    /// 读不回来就抛 <see cref="InvalidDataException"/> 并且<b>不动既有内容</b>。
    /// 这条约定只对"能把坏存档留在磁盘上"的实现有意义——纯内存实现没有下一局可读，
    /// 所以 <see cref="MemoryStorage"/> 不做这件事（它还得能存坏数据给读档失败路径当输入）。
    /// </remarks>
    void Write(string key, string content);

    /// <summary>删除键；键不存在时静默返回。</summary>
    void Delete(string key);

    /// <summary>列出全部键。</summary>
    IReadOnlyList<string> ListKeys();
}

/// <summary>
/// 基于文件系统的实现。键会被映射为 <c>根目录/键</c>。<para>
/// <b>写入是"先证明读得回来、再原子替换、并留下上一份"</b>（细节与顺序的理由见 <see cref="Write"/>）。
/// 存档是整个程序里唯一丢了就找不回来的东西，而它偏偏又是"写坏了当场看不出来"的东西：
/// 坏存档的全部代价都发生在下一局启动的那一刻，那时上一份好存档已经被覆盖掉了。
/// </para>
/// </summary>
public sealed class FileStorage : IStorage
{
    /// <summary>崩溃安全的临时文件后缀：内容先写这里，验证通过才替换真存档。</summary>
    private const string TempSuffix = ".tmp";

    /// <summary>上一份<b>验证过能读回来</b>的存档留在这个后缀的文件里（例如 <c>neko.json.bak</c>）。</summary>
    private const string BackupSuffix = ".bak";

    /// <summary>诊断信息里最多带出多少字符的原内容。</summary>
    private const int ExcerptLength = 160;

    private readonly string _root;

    /// <summary>在指定目录下创建存储；目录不存在会自动创建。</summary>
    public FileStorage(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_root);
    }

    /// <summary>存储根目录。</summary>
    public string RootDirectory => _root;

    /// <inheritdoc />
    public bool Exists(string key) => File.Exists(PathFor(key));

    /// <inheritdoc />
    public string? Read(string key)
    {
        string path = PathFor(key);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 三道闸，顺序不能换：
    /// <list type="number">
    ///   <item>新内容先落到 <c>&lt;键&gt;.tmp</c>——崩在写一半也不会碰坏真存档（这条性质在本次改动前后都成立）；</item>
    ///   <item><b>证明它读得回来</b>：把 <c>.tmp</c> 里的字节读回来反解一遍（见 <see cref="SaveSerializer.Parse"/>）。
    ///         读不回来就抛，<b>真存档与备份一个字节都不动</b>；</item>
    ///   <item>把真存档原子替换成新内容，同时把<b>被换下来的那一份</b>留成 <c>&lt;键&gt;.bak</c>；
    ///         但只在被换下来的那份<b>本身也读得回来</b>时才留。</item>
    /// </list>
    /// 第 3 步用 <see cref="File.Replace(string, string, string, bool)"/>：它是 BCL 里唯一
    /// "原子替换 <b>且</b> 保留被替换文件"的原语。<c>ignoreMetadataErrors: true</c> 不是可选项——
    /// 实测（net8.0 / Windows）不传它时，连"同一进程刚在同一目录里建的两个普通文件"都会在
    /// 合并元数据那一步失败并抛 <see cref="UnauthorizedAccessException"/>，替换根本没发生。
    /// 它的前提是<b>目标必须已存在</b>：不存在时抛 <see cref="FileNotFoundException"/>，
    /// 所以第一次存档（以及既有那份读不回来时）走 <see cref="File.Move(string, string, bool)"/>。
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// <paramref name="content"/> 不是一份能读回来的存档：不是合法 JSON、根节点不是对象、
    /// 版本高于当前，或无法反序列化成 <see cref="SaveData"/>。此时既有存档与备份都保持原样。
    /// </exception>
    public void Write(string key, string content)
    {
        string path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // ① 先写临时文件再原子替换：避免存档写到一半崩溃导致进度彻底损坏。
        string temp = path + TempSuffix;
        File.WriteAllText(temp, content);

        try
        {
            // ② 提交之前先证明它读得回来。一份读不回来的存档一旦当上"当前存档"，
            //    玩家在下一局启动时只会看到一句"读档失败"，而那时上一份好存档已经没了。
            //    验的是**刚从 .tmp 里读回来的字节**，不是手里那个字符串：要被提交的是磁盘上那份，
            //    验"我打算写的"等于把"实际写下去的"排除在证据之外。
            VerifyLoadable(File.ReadAllText(temp));

            if (File.Exists(path) && IsLoadable(File.ReadAllText(path)))
            {
                // ③ 被换下来的那份读得回来，才配叫"上一份好存档"。
                File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
            }
            else
            {
                // 第一次存档（没有目标，File.Replace 用不了），或者既有那份根本读不回来
                // ——把它留成 .bak 会让"备份"变成一句谎话，所以直接覆盖它，
                // 已有的 .bak（上一次真正验证过的那份）原样留着。
                File.Move(temp, path, overwrite: true);
            }
        }
        catch
        {
            // 失败路径上不留半成品：这份内容没能成为当前存档，.tmp 就是垃圾。
            // 清理本身再失败也不覆盖原始异常——要喊的是上面那个错，不是"没扫干净"。
            TryDelete(temp);
            throw;
        }
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        string path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListKeys()
    {
        if (!Directory.Exists(_root)) return [];
        return Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private string PathFor(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("键不能为空。", nameof(key));

        string combined = Path.GetFullPath(Path.Combine(_root, key));
        // 防目录穿越：键里出现 ".." 时不允许逃出根目录。
        if (!combined.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"非法的存储键：{key}", nameof(key));
        return combined;
    }

    /// <summary>
    /// 证明即将提交的内容读得回来。这是"坏存档不许成为当前存档"的执行点。<para>
    /// 只把"读不回来"（<see cref="InvalidDataException"/>）翻成带上下文的失败；
    /// 其他异常原样抛——那更可能是代码 bug 而不是坏存档，把它说成"存档坏了"正好是本仓库
    /// 最想避免的那种错误归因（写入照样会被拒绝，因为异常一定在替换之前抛出）。
    /// </para>
    /// </summary>
    /// <param name="content">刚从 <c>.tmp</c> 读回来的内容（即将被提交的那些字节）。</param>
    /// <exception cref="InvalidDataException">内容读不回来；调用方不得替换既有存档。</exception>
    private static void VerifyLoadable(string content)
    {
        try
        {
            _ = SaveSerializer.Parse(content);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException(
                $"拒绝写入：这份内容写出来之后读不回来（{ex.Message}）。既有存档与备份未被改动。" +
                $"内容开头：{Excerpt(content)}",
                ex);
        }
    }

    /// <summary>判断一份内容是不是能读回来的存档（用于决定它配不配当"上一份好存档"）。</summary>
    /// <param name="content">候选内容。</param>
    /// <returns>能读回来为 <c>true</c>。</returns>
    private static bool IsLoadable(string content)
    {
        try
        {
            _ = SaveSerializer.Parse(content);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>删掉临时文件；删不掉也不抛（原始异常比"没扫干净"重要得多）。</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // 文件被别的进程占着：下一次 Write 会用 File.WriteAllText 覆盖它。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
        }
    }

    /// <summary>截一段原内容用于诊断（坏存档的第一现场通常在开头）。</summary>
    private static string Excerpt(string content)
        => content.Length <= ExcerptLength ? content : content[..ExcerptLength] + "…";
}

/// <summary>纯内存实现，供测试与"试玩不落盘"模式使用。</summary>
public sealed class MemoryStorage : IStorage
{
    private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Exists(string key) => _items.ContainsKey(key);

    /// <inheritdoc />
    public string? Read(string key) => _items.TryGetValue(key, out string? v) ? v : null;

    /// <inheritdoc />
    public void Write(string key, string content) => _items[key] = content;

    /// <inheritdoc />
    public void Delete(string key) => _items.Remove(key);

    /// <inheritdoc />
    public IReadOnlyList<string> ListKeys() => _items.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
}
