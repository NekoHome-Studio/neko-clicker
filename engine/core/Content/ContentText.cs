using System.Text.Json;
using System.Text.Json.Nodes;

namespace NekoClicker.Core.Content;

/// <summary>
/// 剧情文本的外部化载体：把"给玩家读的散文"从 C# 字面量搬到随包发布的 JSON 文件。<para>
/// <b>内容包用它读自己那份文案</b>（1.2.0 起是公开 API）。包的用法是：在内容代码里
/// <c>ContentText.Load("&lt;包名&gt;")</c> 取到它，再用 <see cref="Text"/> 按 id 拿标题与正文；
/// 条件树、序号、权重这些<b>逻辑</b>仍然留在代码里，两边靠 id 关联。
/// </para>
/// <para>
/// <b>为什么要有这个类</b>：文本编译进 dll 时，<c>Id</c> 与 <c>Title</c>/<c>Body</c> 在同一个
/// 对象上，<b>编译器保证它们同生共死</b>。搬到文件之后，两者只剩"字符串相等"这层关系，
/// 于是冒出三种<b>不会让任何测试变红</b>的失败：id 打错（界面空白）、代码删了文件没删
/// （孤儿条目）、id 重复（静默覆盖）。本类的全部价值就是<b>把这三件事变成抛异常</b>。
/// </para>
/// <para>
/// <b>只搬散文，不搬逻辑。</b><c>Reveal</c>（条件树）、<c>Order</c>、<c>Weight</c>、<c>Modifiers</c>
/// 一律留在代码里——它们是逻辑，用数据表达需要一整套 DSL 与解析器，与"剧情可读"无关。
/// 两边靠 id 关联，id 的一致性由本类的守卫负责。
/// </para>
/// <para>
/// 文件布局：<c>content/&lt;包名&gt;/text.json</c>，随内容包一起复制到输出目录。
/// 一棵树按 kind 分区，目前的十一个分区是
/// <c>storylines</c> / <c>lore</c> / <c>buildings</c> / <c>eras</c> / <c>endings</c> /
/// <c>stances</c> / <c>choices</c> / <c>achievements</c> / <c>buffs</c> / <c>upgrades</c> /
/// <c>goldenCookies</c>，
/// 每个条目是 <c>id → { 字段: 文本 }</c>。选项那类是两层：<c>choices → id → options → 选项 id</c>。
/// <b>分区的清单以内容包的文件为准</b>（这里列的是 2026-10-03 第五轮之后的全部），
/// 本类不认识任何具体分区：新增一类文案不需要改这个文件，但这份注释列漏了一类就是文档在说谎。
/// </para>
/// <para>
/// <b>自由文本表</b>（<c>$tables</c> 清单）：根节点上还有一个保留键 <c>$tables</c>，
/// 它声明"哪些分区<b>没有人读</b>"。这样一类表可以自由增减而<b>不需要任何 C# 改动</b>：
/// 条目形状与别的分区一样（<c>id → { "text": "…" }</c>），所以将来真有人读它时，
/// 只要把声明从 <c>free</c> 改成 <c>consumed</c> 再用 <see cref="Text"/> 取——
/// <b>永远不需要新的公开 API</b>。
/// </para>
/// </summary>
public sealed class ContentText
{
    /// <summary>
    /// 根节点上唯一的保留键：自由文本表的声明清单（<c>表名 → { "kind": … }</c>）。<para>
    /// <b>为什么需要这个清单</b>：<see cref="EnsureNoOrphans"/> 会把"文件里有、代码从没取过"
    /// 的条目报成孤儿，而自由文本表的定义就是"代码还没读它"——两者直接冲突。
    /// 不区分它们就只有两个坏选择：关掉孤儿检查（typo 重新静默），或者自由表写不进去。
    /// 所以把"没人读"这句事实<b>写进文件</b>，由本类在 <see cref="Load"/>（结构与形状）
    /// 与 <see cref="EnsureNoOrphans"/>（声明的诚实性）两处核对。
    /// </para>
    /// <para>
    /// 没有出现在清单里的分区 = <c>consumed</c>（代码消费，孤儿检查照旧严格）——
    /// 这就是"十一类分区一个字节都不用改"的原因。
    /// </para>
    /// <para>
    /// <b>刻意是 private</b>：它是文件格式的键名，不是公开契约的一部分；
    /// 加一个公开常量就是一次 minor（见 engine/docs/VERSIONING.md §2），而本机制不需要它。
    /// </para>
    /// </summary>
    private const string ManifestKey = "$tables";

    /// <summary>清单里认的第一个 kind：这张表<b>没有人读</b>（豁免孤儿检查）。</summary>
    private const string FreeKind = "free";

    /// <summary>清单里认的第二个 kind：这张表<b>由代码消费</b>（与不声明等价）。</summary>
    private const string ConsumedKind = "consumed";

    /// <summary>自由文本表里唯一允许的字段名：<c>id → { "text": "…" }</c>。</summary>
    private const string FreeTextField = "text";

    private readonly string _packId;
    private readonly string _path;
    private readonly JsonObject _root;

    /// <summary>
    /// 清单里声明过的表：表名 → kind。没出现在这张表里的根分区视为 <c>consumed</c>。
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _declared;

    /// <summary>
    /// "哪些 id 已经被取过"这张表——它是本类唯一的可变状态。
    /// <para>
    /// <b>必须并发安全</b>：一个内容包通常只持有<b>一份</b> <see cref="ContentText"/>（静态懒加载），
    /// 但 <c>Build()</c> 在同一个进程里可能有多个入口——测试里 <c>ArchitectureTests</c> 与
    /// <c>TestGame</c> 的缓存各建一次，Demo 的包目录也会直接建一次，而测试是并行跑的。
    /// 最初的实现直接改 <see cref="HashSet{T}"/>，实测能稳定复现
    /// 「集合在并发更新下损坏」（40 线程 × 30 轮 → 522 次失败），
    /// 所以这里所有读写都在 <c>_gate</c> 下进行。
    /// </para>
    /// </summary>
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    private readonly object _gate = new();

    private ContentText(string packId, string path, JsonObject root, IReadOnlyDictionary<string, string> declared)
    {
        _packId = packId;
        _path = path;
        _root = root;
        _declared = declared;
    }

    /// <summary>本包读到的文本文件路径（诊断用）。</summary>
    public string Path => _path;

    /// <summary>
    /// 读取一个内容包的文本文件。<para>
    /// 默认在 <c>AppContext.BaseDirectory/content/&lt;包名&gt;/text.json</c> 找，
    /// 与 dll 同目录——这样 <c>dotnet run</c>、测试、发布产物三种情形走同一条路。
    /// </para>
    /// <para>
    /// 读的时候就把<b>结构</b>查完：<c>$tables</c> 清单合法、自由文本表非空且条目是
    /// <c>{ "text": "非空字符串" }</c>、全文没有重复键、没声明的分区里不许出现非对象条目。
    /// 声明与代码是否一致（"声明没人读，代码却读了"）要等到
    /// <see cref="EnsureNoOrphans"/> 才判得了——那需要"代码读过什么"。
    /// </para>
    /// </summary>
    /// <param name="packId">内容包名（目录名）。</param>
    /// <param name="directory">可选：指定 <c>content</c> 的父目录（测试用）。</param>
    /// <exception cref="InvalidOperationException">文件不存在、JSON 结构不对，或自由文本表不合规。</exception>
    public static ContentText Load(string packId, string? directory = null)
    {
        string root = directory ?? AppContext.BaseDirectory;
        string path = System.IO.Path.Combine(root, "content", packId, "text.json");

        if (!File.Exists(path))
        {
            // 丢文件不能静默降级成"没有剧情"——那正是这个类要消灭的失败形态。
            throw new InvalidOperationException(
                $"内容包「{packId}」的剧情文本文件不存在：{path}。" +
                "它应当随包发布（内容包项目的 csproj 里标 CopyToOutputDirectory）。");
        }

        string json = File.ReadAllText(path);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"剧情文本不是合法 JSON：{path}（{ex.Message}）", ex);
        }

        if (node is not JsonObject obj)
            throw new InvalidOperationException($"剧情文本的根必须是对象：{path}");

        // 重复键要在**其它一切校验之前**挡住：`JsonNode` / `JsonDocument` 都不把重复键当错
        // （实测：`{"a":1,"a":2}` 解析成功），于是"id 重复"就是本类注释第三条承诺里
        // 唯一一直没兑现的那条——重复的 id 会被静默覆盖（或按实现保留两份）。
        EnsureNoDuplicateKeys(path, json);

        IReadOnlyDictionary<string, string> declared = ParseDeclarations(path, obj);
        ValidateSections(path, obj, declared);

        return new ContentText(packId, path, obj, declared);
    }

    /// <summary>
    /// 全文扫一遍重复键（任意层级）。<para>
    /// 用 <c>JsonDocument</c> 而不是 <c>JsonNode</c> 来完成这件事：只有前者的枚举
    /// <b>会把重复的属性逐个列出来</b>（实测），所以这里才能点名是哪一层、哪个键。
    /// </para>
    /// </summary>
    private static void EnsureNoDuplicateKeys(string path, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        WalkForDuplicateKeys(path, document.RootElement, trail: string.Empty);
    }

    private static void WalkForDuplicateKeys(string path, JsonElement element, string trail)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new InvalidOperationException(
                    $"{path}：{(trail.Length == 0 ? "根对象" : $"「{trail}」")}里出现了重复的键「{property.Name}」。" +
                    "读的人只会看到其中一个——重复的 id 会静默覆盖，所以这里必须拦住。");
            }

            WalkForDuplicateKeys(
                path,
                property.Value,
                trail.Length == 0 ? property.Name : trail + "/" + property.Name);
        }
    }

    /// <summary>
    /// 解析 <c>$tables</c> 清单。缺清单是合法的（那时所有分区都按代码消费处理），
    /// 清单里写错任何一处都不合法。
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseDeclarations(string path, JsonObject root)
    {
        Dictionary<string, string> declared = new(StringComparer.Ordinal);

        if (!root.TryGetPropertyValue(ManifestKey, out JsonNode? node)) return declared;

        if (node is not JsonObject manifest)
        {
            throw new InvalidOperationException(
                $"{path}：{ManifestKey} 必须是对象（表名 → 声明），现在是{Describe(node)}。" +
                $"写法：{{ \"{ManifestKey}\": {{ \"<表名>\": {{ \"kind\": \"{FreeKind}\" }} }} }}。");
        }

        foreach ((string name, JsonNode? declaration) in manifest)
        {
            if (name.Length == 0)
                throw new InvalidOperationException($"{path}：{ManifestKey} 里有一个空表名。");

            if (name == ManifestKey)
                throw new InvalidOperationException($"{path}：{ManifestKey} 不能声明它自己。");

            if (declaration is not JsonObject fields)
            {
                throw new InvalidOperationException(
                    $"{path}：{ManifestKey} 里「{name}」的声明必须是对象，现在是{Describe(declaration)}。" +
                    $"写法：{{ \"kind\": \"{FreeKind}\" }}。");
            }

            foreach ((string field, JsonNode? _) in fields)
            {
                if (field != "kind")
                {
                    throw new InvalidOperationException(
                        $"{path}：{ManifestKey} 里「{name}」的声明多了一个字段「{field}」——" +
                        "这一版只认「kind」。");
                }
            }

            if (fields["kind"] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"{path}：{ManifestKey} 里「{name}」的声明缺少字符串字段「kind」" +
                    $"（只认「{FreeKind}」与「{ConsumedKind}」）。");
            }

            string kind = value.GetValue<string>();
            if (kind != FreeKind && kind != ConsumedKind)
            {
                throw new InvalidOperationException(
                    $"{path}：{ManifestKey} 里「{name}」的 kind 是「{kind}」——" +
                    $"这一版只认「{FreeKind}」（这张表没有人读）与「{ConsumedKind}」（代码消费它）。");
            }

            if (!root.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"{path}：{ManifestKey} 声明了「{name}」，但文件里没有这个分区——" +
                    "声明与分区必须成对（要么补上分区，要么删掉声明）。");
            }

            declared[name] = kind;
        }

        return declared;
    }

    /// <summary>
    /// 查根节点上的每个分区：保留键只有一个；分区必须是对象；<b>没声明为自由表的分区里，
    /// 每个条目都必须是对象</b>（否则它就是一个伪装的自由表——那正是本机制要堵的洞：
    /// 老实现遇到"值不是对象"会<b>静默跳过</b>，等于让 typo 从孤儿检查底下溜过去）。
    /// 声明过的表必须非空；自由表的条目恰好一个字段 <c>text</c>，且非空白。
    /// </summary>
    private static void ValidateSections(string path, JsonObject root, IReadOnlyDictionary<string, string> declared)
    {
        foreach ((string name, JsonNode? node) in root)
        {
            if (name == ManifestKey) continue;

            if (name.StartsWith('$'))
            {
                throw new InvalidOperationException(
                    $"{path}：根节点上的「{name}」是保留键，但这一版只认「{ManifestKey}」。");
            }

            if (node is not JsonObject section)
                throw new InvalidOperationException($"{path}：分区「{name}」必须是对象，现在是{Describe(node)}。");

            bool declaredHere = declared.TryGetValue(name, out string? kind);
            bool isFree = declaredHere && kind == FreeKind;

            if (declaredHere && section.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{path}：分区「{name}」在 {ManifestKey} 里声明了，却是空的——" +
                    "空表 = 作者以为写了什么，所以这里必须拦住。");
            }

            foreach ((string key, JsonNode? entry) in section)
            {
                if (entry is not JsonObject row)
                {
                    throw new InvalidOperationException(
                        $"{path}：分区「{name}」里的「{key}」不是对象，而是{Describe(entry)}——" +
                        $"没声明过的分区，每个条目都必须是 {{ 字段: 文本 }}。" +
                        $"想让它成为自由文本表（没有人读）就写进 {ManifestKey}：" +
                        $"{{ \"{name}\": {{ \"kind\": \"{FreeKind}\" }} }}，条目写成 {{ \"{FreeTextField}\": \"…\" }}。");
                }

                if (!isFree) continue;

                foreach ((string field, JsonNode? _) in row)
                {
                    if (field != FreeTextField)
                    {
                        throw new InvalidOperationException(
                            $"{path}：自由文本表「{name}」的条目「{key}」里多了一个字段「{field}」——" +
                            $"这一版自由表的条目恰好一个字段「{FreeTextField}」。");
                    }
                }

                if (row[FreeTextField] is not JsonValue text || text.GetValueKind() != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        $"{path}：自由文本表「{name}」的条目「{key}」缺少字符串字段「{FreeTextField}」" +
                        $"（或它不是字符串）——缺文本会在界面上表现为空白，所以这里必须拦住。");
                }

                if (string.IsNullOrWhiteSpace(text.GetValue<string>()))
                {
                    throw new InvalidOperationException(
                        $"{path}：自由文本表「{name}」的条目「{key}」的「{FreeTextField}」是全空白" +
                        "——空文本与缺文本一样，都表现为界面上什么都没有。");
                }
            }
        }
    }

    /// <summary>给错误消息用：节点是什么形状（而不是把整段内容打进消息里）。</summary>
    private static string Describe(JsonNode? node) => node?.GetValueKind() switch
    {
        null => "null",
        JsonValueKind.Object => "一个对象",
        JsonValueKind.Array => "一个数组",
        JsonValueKind.String => "一个字符串",
        JsonValueKind.Number => "一个数字",
        JsonValueKind.True or JsonValueKind.False => "一个布尔值",
        _ => "一个无法识别的值",
    };

    /// <summary>
    /// 取一条文本；没有就抛。<paramref name="id"/> 支持两级：
    /// <c>"choice_archive"</c> 或 <c>"choice_archive/utopia"</c>（选项那一层）。
    /// </summary>
    /// <param name="kind">分区：<c>storylines</c> / <c>lore</c> / <c>buildings</c> / <c>eras</c> /
    /// <c>endings</c> / <c>stances</c> / <c>choices</c> / <c>achievements</c> / <c>buffs</c> /
    /// <c>upgrades</c> / <c>goldenCookies</c>，或任何自由文本表的名字。
    /// <b>自由文本表要用它，得先把 <c>$tables</c> 里的声明从 <c>free</c> 改成 <c>consumed</c></b>：
    /// 声明说"没人读"而代码读了它，<see cref="EnsureNoOrphans"/> 会当场抛。</param>
    /// <param name="id">条目 id（选项用 <c>选择id/选项id</c>）。</param>
    /// <param name="field">字段名，例如 <c>title</c> / <c>body</c> / <c>text</c>
    /// （自由文本表只有一个字段 <c>text</c>）。</param>
    public string Text(string kind, string id, string field)
    {
        string key = kind + "\u0000" + id + "\u0000" + field;
        lock (_gate) _used.Add(key);

        JsonNode? section = _root[kind];
        if (section is not JsonObject sectionObj)
            throw new InvalidOperationException($"{_path}：没有分区「{kind}」。");

        int slash = id.IndexOf('/');
        JsonObject entry;

        if (slash < 0)
        {
            if (sectionObj[id] is not JsonObject single)
                throw new InvalidOperationException($"{_path}：{kind} 里没有 id「{id}」。");

            entry = single;
        }
        else
        {
            string choiceId = id[..slash];
            string optionId = id[(slash + 1)..];

            if (sectionObj[choiceId] is not JsonObject choice)
                throw new InvalidOperationException($"{_path}：{kind} 里没有 id「{choiceId}」。");
            if (choice["options"] is not JsonObject options)
                throw new InvalidOperationException($"{_path}：{kind}/{choiceId} 没有 options 分区。");
            if (options[optionId] is not JsonObject option)
                throw new InvalidOperationException($"{_path}：{kind}/{choiceId} 里没有选项「{optionId}」。");

            entry = option;
        }

        if (entry[field] is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"{_path}：{kind}「{id}」缺少字段「{field}」（或它不是字符串）。" +
                "缺文本会在界面上表现为空白，所以这里必须拦住。");
        }

        return value.GetValue<string>();
    }

    /// <summary>
    /// 取一条文本；没有则返回 <paramref name="fallback"/>（给"可选文本"用，例如图标）。
    /// 用了这个重载的字段<b>不</b>参与孤儿检查——它是可选的。
    /// </summary>
    /// <param name="kind">分区。</param>
    /// <param name="id">条目 id。</param>
    /// <param name="field">字段名。</param>
    /// <param name="fallback">缺失时的回退值。</param>
    public string TextOr(string kind, string id, string field, string fallback)
    {
        try
        {
            return Text(kind, id, field);
        }
        catch (InvalidOperationException)
        {
            lock (_gate) _used.Remove(kind + "\u0000" + id + "\u0000" + field);
            return fallback;
        }
    }

    /// <summary>
    /// 对本文件里每一条 id 调用一次 <paramref name="onEntry"/>，然后校验：
    /// <b>文件里有、代码里从没取过的条目</b>会被列出来抛异常（孤儿文本）。<para>
    /// 参数是 <c>kind</c> 与 <c>id</c>（选项那层是 <c>选择id/选项id</c>）。
    /// 传 <c>null</c> 表示"这条是给别处用的"，跳过。
    /// </para>
    /// <para>
    /// <b>为什么孤儿也要抛</b>：删了剧情却留着文本，永远不会有人发现——
    /// 它既不影响运行，也不影响任何断言。等到有人改了那段"死文本"却毫无效果时，
    /// 才第一次意识到它早就没用了。
    /// </para>
    /// <para>
    /// <b>自由文本表是唯一的例外，而且这个例外是被声明出来的</b>：清单
    /// <c>$tables</c> 里 <c>"kind": "free"</c> 的分区跳过孤儿检查（"没有人读它"
    /// 是一句写在文件里的事实）。反过来也查：声明为 <c>free</c> 的表<b>不许被代码取用</b>——
    /// 那说明声明过期了，必须当场抛（<c>free</c> → <c>consumed</c> 是改数据，不是改代码）。
    /// 这两条合起来保证"没人读"既不是沉默的，也不是把 typo 藏起来的借口。
    /// </para>
    /// </summary>
    /// <param name="onEntry">对每条 id 决定它是否已被使用；返回 <c>true</c> 表示"已用"。</param>
    public void EnsureNoOrphans(Func<string, string, bool>? onEntry = null)
    {
        List<string> orphans = [];

        foreach ((string kind, JsonNode? section) in Enumerate())
        {
            // 清单本身不是表。
            if (kind == ManifestKey) continue;

            if (IsDeclaredFree(kind))
            {
                string? taken = FirstUsedIdOf(kind);
                if (taken is not null)
                {
                    throw new InvalidOperationException(
                        $"{_path}：表「{kind}」在 {ManifestKey} 里声明为「{FreeKind}」（没有人读），" +
                        $"但代码取用了「{kind}/{taken}」——声明与代码必须一致：" +
                        $"要么把它改成「{ConsumedKind}」并让包真的把整张表读完，要么别读它。");
                }

                // 声明为"没有人读"：豁免孤儿检查。这一条就是本机制的全部豁免面。
                continue;
            }

            if (section is not JsonObject sectionObj) continue;

            foreach ((string id, JsonNode? node) in sectionObj)
            {
                if (node is not JsonObject entry) continue;

                if (kind == "choices" && entry["options"] is JsonObject options)
                {
                    foreach ((string optionId, JsonNode? _) in options)
                    {
                        string composite = id + "/" + optionId;
                        if (IsUsed(kind, composite) || (onEntry?.Invoke(kind, composite) ?? false)) continue;
                        orphans.Add($"{kind}/{composite}");
                    }
                }

                if (IsUsed(kind, id) || (onEntry?.Invoke(kind, id) ?? false)) continue;
                orphans.Add($"{kind}/{id}");
            }
        }

        if (orphans.Count == 0) return;

        throw new InvalidOperationException(
            $"内容包「{_packId}」的剧情文本里有 {orphans.Count} 条没人取用（孤儿条目）：" +
            $"{string.Join("、", orphans)}。它们要么是 id 与代码对不上，要么是代码里已经删掉了这段剧情。" +
            $"（自由文本表不走这条检查，但它必须在 {ManifestKey} 里声明为「{FreeKind}」。）");
    }

    private bool IsDeclaredFree(string kind)
        => _declared.TryGetValue(kind, out string? declaredKind) && declaredKind == FreeKind;

    /// <summary>这张表被代码取用过的第一个 id（没有就是 <c>null</c>）。给"声明过期"那条错误用。</summary>
    private string? FirstUsedIdOf(string kind)
    {
        string prefix = kind + "\u0000";
        lock (_gate)
        {
            foreach (string key in _used)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;

                string rest = key[prefix.Length..];
                int cut = rest.IndexOf('\u0000');
                return cut >= 0 ? rest[..cut] : rest;
            }
        }

        return null;
    }

    private bool IsUsed(string kind, string id)
    {
        string prefix = kind + "\u0000" + id + "\u0000";
        lock (_gate)
        {
            foreach (string key in _used)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private IEnumerable<(string Kind, JsonNode? Section)> Enumerate()
    {
        foreach ((string key, JsonNode? value) in _root) yield return (key, value);
    }
}
