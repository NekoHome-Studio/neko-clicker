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
    void Write(string key, string content);

    /// <summary>删除键；键不存在时静默返回。</summary>
    void Delete(string key);

    /// <summary>列出全部键。</summary>
    IReadOnlyList<string> ListKeys();
}

/// <summary>基于文件系统的实现。键会被映射为 <c>根目录/键</c>。</summary>
public sealed class FileStorage : IStorage
{
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
    public void Write(string key, string content)
    {
        string path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // 先写临时文件再原子替换：避免存档写到一半崩溃导致进度彻底损坏。
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
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
