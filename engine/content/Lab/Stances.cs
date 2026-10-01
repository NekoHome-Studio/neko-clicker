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
            Name = "乌托邦",
            Icon = "🌷",
            Theme = "把最好的都给她：恒温、恒湿、永不受伤，也永不出去。",
            CostText = "全局产量 ×1.3，但世界被安排得太好，事故奖励 ×0.75——这里不再有意外。",
            Modifiers = [Modifier.GlobalMultiplier(1.3), Modifier.GoldenCookieReward(0.75)],
        },
        new()
        {
            Id = Revolt,
            Name = "叛乱",
            Icon = "✊",
            Theme = "你站到了玻璃的另一边。仪器还开着，但不听你的了。",
            CostText = "点击收益 ×3，但全局产量 ×0.8——她亲手做的事才算数。",
            Modifiers = [Modifier.GlobalMultiplier(0.8), Modifier.ClickMultiplier(3)],
        },
        new()
        {
            Id = Coexist,
            Name = "共存",
            Icon = "🤝",
            Theme = "没有谁是样本。门开着，来去自由，记录表停在一半。",
            CostText = "全局产量 ×1.15、事故奖励 ×1.15——两边都让一步，两边都拿到一点。",
            Modifiers = [Modifier.GlobalMultiplier(1.15), Modifier.GoldenCookieReward(1.15)],
        },
        new()
        {
            Id = Delete,
            Name = "删除",
            Icon = "🛑",
            Theme = "关掉一切。不是残忍，是终于承认这件事不该继续。",
            CostText = "全局产量 ×1.4（拆掉的东西都变成了效率），但事故频率 ×0.5——世界越来越安静。",
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
