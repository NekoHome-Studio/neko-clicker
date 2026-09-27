using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 三条立场：这家公司最后是谁的。<para>
/// 这是<b>公司自己的</b>劳资轴——与九命的"神性 / 人形 / 猫形 / 断绝"、
/// 实验室的"乌托邦 / 叛乱 / 共存 / 删除"完全不是一回事，
/// 但用的是同一个 <see cref="StanceDefinition"/>。第三套词表也换得动，
/// 说明"立场不是 enum"这个决定经得起第二次检验。
/// </para>
/// <para>
/// 三条立场对应设计文档 §6 给公司定的三个结局：上市 / 工会胜利 / 破产清算。
/// 每条都有得有失，且加成只在<b>主导</b>时生效——摇摆不定的创始人拿不到任何一条。
/// </para>
/// </summary>
public static class Stances
{
    /// <summary>上市派 —— 把公司做大，敲钟之后再说别的。</summary>
    public const string Ipo = "ipo";

    /// <summary>工会派 —— 员工不是资源，先把人当人。</summary>
    public const string Union = "union";

    /// <summary>清算派 —— 与其被拖死，不如自己按下停止键。</summary>
    public const string Liquidate = "liquidate";

    /// <summary>三条立场。</summary>
    public static StanceDefinition[] All =>
    [
        new()
        {
            Id = Ipo,
            Name = "上市派",
            Icon = "🔔",
            Theme = "把公司做大，敲钟，然后再说别的。",
            CostText = "全局产量 ×1.3，但事故奖励 ×0.85——增长要花钱，也要花人。",
            Modifiers = [Modifier.GlobalMultiplier(1.3), Modifier.GoldenCookieReward(0.85)],
        },
        new()
        {
            Id = Union,
            Name = "工会派",
            Icon = "✊",
            Theme = "员工不是资源。先把人当人，再谈增长。",
            CostText = "全局产量 ×0.85，但点击收益 ×2——慢一点，但每一步都是自己走的。",
            Modifiers = [Modifier.GlobalMultiplier(0.85), Modifier.ClickMultiplier(2)],
        },
        new()
        {
            Id = Liquidate,
            Name = "清算派",
            Icon = "🧳",
            Theme = "见好就收：卖掉、清算、每人分一笔，然后散伙。",
            CostText = "设备价格 ×0.75、事故奖励 ×1.25，但全局产量 ×0.9——你在准备随时走人。",
            Modifiers =
            [
                Modifier.PriceMultiplier(0.75),
                Modifier.GoldenCookieReward(1.25),
                Modifier.GlobalMultiplier(0.9),
            ],
        },
    ];

    /// <summary>
    /// 主导某条立场所需的权重门槛。<para>
    /// 每条立场一共只有 4 次表态机会、一次 2 点，所以要够到 7 就必须<b>每次</b>都选它
    /// （3 次只有 6 点）。与九命 / 实验室同一个配方：结局是承诺，不是倾向。
    /// </para>
    /// </summary>
    public const int EndingThreshold = 7;
}
