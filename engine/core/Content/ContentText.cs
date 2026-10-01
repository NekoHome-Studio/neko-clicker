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
/// 一棵树按 kind 分区（<c>lore</c> / <c>eras</c> / <c>choices</c> / <c>endings</c>），
/// 每个条目是 <c>id → { 字段: 文本 }</c>。选项那类是两层：<c>choices → id → options → 选项 id</c>。
/// </para>
/// </summary>
public sealed class ContentText
{
    private readonly string _packId;
    private readonly string _path;
    private readonly JsonObject _root;

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

    private ContentText(string packId, string path, JsonObject root)
    {
        _packId = packId;
        _path = path;
        _root = root;
    }

    /// <summary>本包读到的文本文件路径（诊断用）。</summary>
    public string Path => _path;

    /// <summary>
    /// 读取一个内容包的文本文件。<para>
    /// 默认在 <c>AppContext.BaseDirectory/content/&lt;包名&gt;/text.json</c> 找，
    /// 与 dll 同目录——这样 <c>dotnet run</c>、测试、发布产物三种情形走同一条路。
    /// </para>
    /// </summary>
    /// <param name="packId">内容包名（目录名）。</param>
    /// <param name="directory">可选：指定 <c>content</c> 的父目录（测试用）。</param>
    /// <exception cref="InvalidOperationException">文件不存在或 JSON 结构不对。</exception>
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

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"剧情文本不是合法 JSON：{path}（{ex.Message}）", ex);
        }

        if (node is not JsonObject obj)
            throw new InvalidOperationException($"剧情文本的根必须是对象：{path}");

        return new ContentText(packId, path, obj);
    }

    /// <summary>
    /// 取一条文本；没有就抛。<paramref name="id"/> 支持两级：
    /// <c>"choice_archive"</c> 或 <c>"choice_archive/utopia"</c>（选项那一层）。
    /// </summary>
    /// <param name="kind">分区：<c>lore</c> / <c>eras</c> / <c>choices</c> / <c>endings</c>。</param>
    /// <param name="id">条目 id（选项用 <c>选择id/选项id</c>）。</param>
    /// <param name="field">字段名，例如 <c>title</c> / <c>body</c> / <c>text</c>。</param>
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
    /// </summary>
    /// <param name="onEntry">对每条 id 决定它是否已被使用；返回 <c>true</c> 表示"已用"。</param>
    public void EnsureNoOrphans(Func<string, string, bool>? onEntry = null)
    {
        List<string> orphans = [];

        foreach ((string kind, JsonNode? section) in Enumerate())
        {
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
            $"{string.Join("、", orphans)}。它们要么是 id 与代码对不上，要么是代码里已经删掉了这段剧情。");
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
