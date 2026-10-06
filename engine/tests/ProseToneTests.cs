using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 叙事散文的<b>语气守卫</b>（棘轮）：把「AI 腔」量成三个可数的形状，并给每个包记一个上限。<para>
/// <b>为什么需要它</b>。2026-10-05 人报「这个文案过度中二导致很明显是 ai 的」，选中的范围是
/// <b>全游戏所有包的叙事文案</b>。先把语料量了一遍，结论与直觉相反：
/// <b>问题不在词汇上</b>——整份叙事语料里「深渊 / 宇宙 / 命运 / 永恒」这类词一共只有 7 处，
/// 而且全部落在<b>故意为之的戏仿</b>里（神明包的「诸神黄昏 / 问命运」、九命包神纪元）。
/// 真正的指纹是<b>修辞形状</b>：<c>不是 X，是 Y</c> 的格言式反转 <b>37 处</b>、
/// 正文结尾落在「」引语上的「收尾格言鼓点」<b>125 处</b>、
/// 旁白替读者解释意义的 <c>第一次觉得 / 意识到 / 明白</c> <b>9 处</b>——
/// 683 段正文里近四分之一在用同一套句法收尾。<b>单看每条都不差，连在一起就是机器的节奏。</b>
/// </para>
/// <para>
/// <b>为什么是棘轮而不是"零容忍"</b>。这三个数今天不是 0，而清干净要向<影响全部 11 个包的文案>
/// 动刀——那是十轮清扫，不是一次改动。<c>engine/docs/CONTENT_AUTHORING.md</c> §12.4 定义了目标语气，
/// 这里的表就是那条路线上的<b>账本</b>：<b>每个包的上限等于它的实测值</b>，
/// 扫完一个包就把那一行改小（试点：公司包 EndQuote 23 → 1、RevealDash 8 → 0）。
/// 于是<b>「往下走要记账，往上走就是红」</b>——清扫进度变成了数据变更，而不是一句承诺。
/// </para>
/// <para>
/// <b>刻意不收进判据的（都量过，都因为不判别而否掉）</b>：
/// <list type="bullet">
/// <item><b>禁用词表</b>：见上，叙事语料里那类词只有 7 处且全在戏仿里；
/// 一条禁用词守卫会去红最好的两个包，而 37 处格言反转一处不碰。<b>否掉。</b></item>
/// <item><b>「——」破折号结尾</b>：实测咖啡馆包（全仓语气最克制、被 §12.4 当成范本的那个）
/// 有 <b>11 处</b>，比试点前的公司包（8 处）还多。破折号本身分不出「补一句旁白」与
/// 「在这儿揭示意义」，<b>这个量不判别，所以不守</b>——试点里仍然按目标语气改了，
/// 只是不把它写进闸门。</item>
/// <item><b>句长 / 字数的离散度</b>：能算，但"每段 40~60 字"是可以靠凑长度骗过去的指标，
/// 而凑长度恰好会把语气改坏。写进 §12.4 当写作建议，不当判据。</item>
/// </list>
/// </para>
/// </summary>
public static class ProseToneTests
{
    /// <summary>正文结尾落在一条「」引语上——"每条都金句收尾"的鼓点。</summary>
    private static readonly Regex EndQuote = new(@"「[^」]*」[。！？]?$");

    /// <summary><c>不是 X，是 Y</c>：格言式反转。它对每一件事都成立，因此什么也没说。</summary>
    private static readonly Regex NegPivot = new(@"不是[^。！？]{1,30}[，,]\s*是");

    /// <summary>旁白替读者把意义说出来（<c>第一次觉得 / 意识到 / 明白</c>）。</summary>
    private static readonly Regex Significance = new(@"第一次(觉得|意识到|明白|看清|知道)");

    /// <summary>
    /// 一个包的语气上限：三项判据各自的<b>实测值</b>。<para>
    /// <b>这张表必须与实测逐项相等（不是"不超过"）</b>：往下走要在表里记账，
    /// 往上走就是回归。这是刻意的——把"清扫到哪一步了"变成一处必须维护的数据。
    /// </para>
    /// </summary>
    private sealed record Budget(string Pack, int EndQuote, int NegPivot, int Significance);

    /// <summary>
    /// 实测（2026-10-05）。<b>已清扫</b>：公司（试点）与 §12.4.8 顺序里的七个包
    /// （文明 / 神明 / 图书馆 / 赛博 / 实验室 / 梦境 / 九命）。还留着现状的只有两行：
    /// <b>末世</b>（清扫那一轮它在被另一个改动改同一个文件，刻意跳过，见 §12.4.8 的清扫记录）
    /// 与 <b>咖啡馆</b>（1.9%，全仓范本，不动）。每扫完一个包就把那一行改小。
    /// <c>Neko</c> 没有叙事分区，所以三项都是 0——
    /// 但仍然要占一行（"漏登记"必须是红的，见 <see cref="ToneBudgetTable_CoversExactlyThePacksThatHaveAnAtextFile"/>）。
    /// </summary>
    private static readonly Budget[] Budgets =
    [
        new("Apocalypse", 17, 5, 3),
        new("Cafe", 0, 1, 0),
        new("Civ", 1, 0, 0),
        new("Company", 1, 0, 0),
        new("Cyber", 0, 0, 0),
        new("Dream", 0, 0, 0),
        new("God", 1, 0, 0),
        new("Lab", 1, 0, 0),
        new("Library", 0, 0, 0),
        new("Neko", 0, 0, 0),
        new("NineLives", 0, 0, 0),
    ];

    /// <summary>
    /// 每个包的叙事语料不许超出它那一行记的数。<para>
    /// 失败时逐包逐项点名（包 + 判据 + 实测 + 上限），因为"哪一个包、哪一项、差多少"
    /// 是这条用例唯一有用的输出。
    /// </para>
    /// </summary>
    [Test]
    public static void NarrativeProse_StaysWithinItsRecordedBudget()
    {
        string root = RepoRoot();

        // 假绿防线：一个包都读不到，说明是这条用例自己走错了路径，而不是"大家都合规"。
        Check.Equal(11, Budgets.Length, "上限表被削短了——那不是这条用例该做的事。");

        int total = 0;
        List<string> over = [];

        foreach (Budget budget in Budgets)
        {
            List<string> bodies = Bodies(Read(root, budget.Pack));
            Check.AtLeast(
                bodies.Count,
                budget.Pack == "Neko" ? 0 : 20,
                $"{budget.Pack}：只读到 {bodies.Count} 段叙事——语料抽取坏了，这条守卫会变成假绿。");

            total += bodies.Count;
            foreach ((string name, Regex rule, int ceiling) in new[]
                     {
                         ("EndQuote", EndQuote, budget.EndQuote),
                         ("NegPivot", NegPivot, budget.NegPivot),
                         ("Significance", Significance, budget.Significance),
                     })
            {
                int actual = Count(rule, bodies);
                if (actual == ceiling) continue;
                over.Add(actual > ceiling
                    ? $"{budget.Pack}.{name}：实测 {actual}，上限 {ceiling}（**超支**，这是回归）"
                    : $"{budget.Pack}.{name}：实测 {actual}，表里记着 {ceiling}（**变好了**，把表改成 {actual}）");
            }
        }

        // 语料量本身也要有下界：683 段是这个守卫立起来那天的实测值。
        Check.AtLeast(total, 600, $"全仓叙事语料只剩 {total} 段——抽取逻辑漏了分区。");

        Check.Equal(
            0,
            over.Count,
            "叙事语气与 §12.4 记录的上限不一致（这张表是**棘轮**）：" + Environment.NewLine +
            string.Join(Environment.NewLine, over.Select(line => "    " + line)) + Environment.NewLine +
            "怎么修：**超支**就按 CONTENT_AUTHORING.md §12.4 改写那几段（具体是哪几段，把同一个正则" +
            "在 text.json 上跑一遍就知道）；**变好了**就把表里的数改成实测值——" +
            "清扫进度必须记账，否则这张表会在下一轮悄悄失去意义。");
    }

    /// <summary>
    /// 有 <c>text.json</c> 的包必须每个都在上限表里，一个不多一个不少。<para>
    /// 与 <c>ContentTextFileTests</c> 那十张条数表同一条理由：新加一个包时"忘了登记"是<b>沉默的</b>。
    /// </para>
    /// </summary>
    [Test]
    public static void ToneBudgetTable_CoversExactlyThePacksThatHaveATextFile()
    {
        string contentRoot = Path.Combine(RepoRoot(), "engine", "content");
        string[] onDisk =
        [
            .. Directory.GetDirectories(contentRoot)
                .Where(dir => File.Exists(Path.Combine(dir, "text.json")))
                .Select(Path.GetFileName)
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        string[] inTable = [.. Budgets.Select(b => b.Pack).OrderBy(name => name, StringComparer.Ordinal)];

        Check.Equal(
            string.Join(",", onDisk),
            string.Join(",", inTable),
            "engine/content/*/text.json 与 ProseToneTests 的上限表不是同一张清单。" +
            "新包要把那一行补上（没有叙事分区就写 0/0/0）；删包要连那一行一起删。");
    }

    /// <summary>
    /// <b>判别力</b>：三个判据各自都真的会响，而且都不误伤——再证明棘轮比较本身会红。<para>
    /// 没有这一条，上面那条守卫最坏的失效形态是"正则写错了，于是永远绿"。
    /// </para>
    /// </summary>
    [Test]
    public static void ToneBudget_RejectsProseThatSpendsMoreThanTheRecordedValue()
    {
        // 一段把三种形状一次踩全的正文：先反转，再替读者解释意义，最后落在引语上。
        string[] offending = ["她第一次意识到，这不是结束，是开始。「那就这样吧。」"];

        Check.Equal(1, Count(EndQuote, offending), "EndQuote 判据没能认出「结尾落在引语上」。");
        Check.Equal(1, Count(NegPivot, offending), "NegPivot 判据没能认出「不是 X，是 Y」。");
        Check.Equal(1, Count(Significance, offending), "Significance 判据没能认出「第一次意识到」。");

        // 不误伤：合规的一段在三项上都必须安静。公司包试点后的账本正文就是这一句。
        string[] compliant = ["账本第一页记的是三把椅子和一台二手显示器，一共 1,840 块。"];

        Check.Equal(0, Count(EndQuote, compliant), "EndQuote 判据在合规正文上误伤了。");
        Check.Equal(0, Count(NegPivot, compliant), "NegPivot 判据在合规正文上误伤了。");
        Check.Equal(0, Count(Significance, compliant), "Significance 判据在合规正文上误伤了。");

        // 棘轮：把那段违规正文挂到试点包上，它必须当场超支（三项各自的余量都是 0 或 1）。
        Budget company = Budgets.Single(b => b.Pack == "Company");

        Check.True(
            Count(EndQuote, Bodies(Read(RepoRoot(), "Company"))) + Count(EndQuote, offending)
                > company.EndQuote,
            "公司包的 EndQuote 上限没有余量——棘轮在这条判据上不会响，这条证明就失去判别力。");
        Check.True(
            Count(NegPivot, Bodies(Read(RepoRoot(), "Company"))) + Count(NegPivot, offending)
                > company.NegPivot,
            "公司包的 NegPivot 上限没有余量——棘轮在这条判据上不会响。");
        Check.True(
            Count(Significance, Bodies(Read(RepoRoot(), "Company"))) + Count(Significance, offending)
                > company.Significance,
            "公司包的 Significance 上限没有余量——棘轮在这条判据上不会响。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 一个包的叙事语料：<b>只收散文正文</b>，不收名称、说明、数字。
    /// <list type="bullet">
    /// <item><c>storylines.&lt;id&gt;.theme</c></item>
    /// <item><c>lore.&lt;id&gt;.body</c></item>
    /// <item><c>eras.&lt;id&gt;</c> 的 <c>theme</c> / <c>entryText</c> / <c>exitText</c></item>
    /// <item><c>endings.&lt;id&gt;.text</c></item>
    /// <item><c>stances.&lt;id&gt;.theme</c></item>
    /// <item><c>choices.&lt;id&gt;.options.&lt;oid&gt;.outcomeText</c></item>
    /// </list>
    /// <b>刻意不收 <c>choices.&lt;id&gt;.prompt</c></b>：它的体裁就是"一个角色问玩家一句"，
    /// 末尾的「」是结构而不是修辞，收进来会让 EndQuote 判据在 6 个包上系统性误伤。
    /// <b>也不收</b><c>buildings</c> / <c>upgrades</c> / <c>achievements</c> / <c>buffs</c> /
    /// <c>goldenCookies</c>：它们是被模板与数字钉住的短标签，不是叙事句法。
    /// </summary>
    private static List<string> Bodies(JsonObject file)
    {
        List<string> bodies = [];

        if (file["storylines"] is JsonObject storylines)
            foreach ((string _, JsonNode? node) in storylines)
                Add(bodies, node, "theme");

        if (file["lore"] is JsonObject lore)
            foreach ((string _, JsonNode? node) in lore)
                Add(bodies, node, "body");

        if (file["eras"] is JsonObject eras)
            foreach ((string _, JsonNode? node) in eras)
                foreach (string field in new[] { "theme", "entryText", "exitText" })
                    Add(bodies, node, field);

        if (file["endings"] is JsonObject endings)
            foreach ((string _, JsonNode? node) in endings)
                Add(bodies, node, "text");

        if (file["stances"] is JsonObject stances)
            foreach ((string _, JsonNode? node) in stances)
                Add(bodies, node, "theme");

        if (file["choices"] is JsonObject choices)
            foreach ((string _, JsonNode? node) in choices)
                if (node?["options"] is JsonObject options)
                    foreach ((string _, JsonNode? option) in options)
                        Add(bodies, option, "outcomeText");

        return bodies;
    }

    private static void Add(List<string> bodies, JsonNode? node, string field)
    {
        if (node?[field]?.GetValue<string>() is { Length: > 0 } text) bodies.Add(text);
    }

    private static int Count(Regex rule, IEnumerable<string> bodies)
        => bodies.Count(body => rule.IsMatch(body.TrimEnd()));

    private static JsonObject Read(string root, string pack)
        => (JsonObject)JsonNode.Parse(File.ReadAllText(
            Path.Combine(root, "engine", "content", pack, "text.json")))!;

    private static string RepoRoot()
        => RepositoryPaths.Find()
           ?? throw new AssertionException("这条用例要在仓库里跑：它直接读 engine/content/*/text.json。");
}
