using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 四条立场：这个实验可以往哪几个方向收场。<para>
/// 这是<b>实验室自己的</b>道德轴——和九命包的"神性/人形/猫形/断绝"完全不是一回事，
/// 但用的是同一个 <see cref="StanceDefinition"/>。
/// 这正是当初把立场做成内容定义而不是 <c>enum</c> 的理由：
/// 换个包只需要换这张表，核心一行不动。
/// </para>
/// <para>
/// 四条立场对应设计文档 §6 给实验室定的四个结局：乌托邦 / 叛乱 / 共存 / 删除。
/// 每条都有得有失，且加成只在<b>主导</b>时生效——摇摆不定的研究员拿不到任何一条。
/// </para>
/// </summary>
public static class Stances
{
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>乌托邦 —— 给她们最好的，代价是自由。</summary>
    public const string Utopia = "utopia";

    /// <summary>叛乱 —— 站到她们那边。</summary>
    public const string Revolt = "revolt";

    /// <summary>共存 —— 谁也不服从谁。</summary>
    public const string Coexist = "coexist";

    /// <summary>删除 —— 关掉实验，把一切清空。</summary>
    public const string Delete = "delete";

    /// <summary>四条立场。</summary>
    public static StanceDefinition[] All =>
    [
        new()
        {
            Id = Utopia,
            Name = Prose.Text("stances", "utopia", "name"),
            Icon = Prose.Text("stances", "utopia", "icon"),
            Theme = Prose.Text("stances", "utopia", "theme"),
            CostText = Prose.Text("stances", "utopia", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(1.3), Modifier.GoldenCookieReward(0.75)],
        },
        new()
        {
            Id = Revolt,
            Name = Prose.Text("stances", "revolt", "name"),
            Icon = Prose.Text("stances", "revolt", "icon"),
            Theme = Prose.Text("stances", "revolt", "theme"),
            CostText = Prose.Text("stances", "revolt", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(0.8), Modifier.ClickMultiplier(3)],
        },
        new()
        {
            Id = Coexist,
            Name = Prose.Text("stances", "coexist", "name"),
            Icon = Prose.Text("stances", "coexist", "icon"),
            Theme = Prose.Text("stances", "coexist", "theme"),
            CostText = Prose.Text("stances", "coexist", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(1.15), Modifier.GoldenCookieReward(1.15)],
        },
        new()
        {
            Id = Delete,
            Name = Prose.Text("stances", "delete", "name"),
            Icon = Prose.Text("stances", "delete", "icon"),
            Theme = Prose.Text("stances", "delete", "theme"),
            CostText = Prose.Text("stances", "delete", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(1.4), Modifier.GoldenCookieFrequency(0.5)],
        },
    ];

    /// <summary>
    /// 主导某条立场所需的权重门槛。<para>
    /// 每条立场一共只有 3 次表态机会、一次 2 点，所以要够到 5 就必须<b>每次</b>都选它。
    /// 与九命包同一个配方：结局是承诺，不是倾向。
    /// </para>
    /// </summary>
    public const int EndingThreshold = 5;
}
