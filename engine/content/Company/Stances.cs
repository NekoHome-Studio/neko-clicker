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
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

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
            Name = Prose.Text("stances", "ipo", "name"),
            Icon = Prose.Text("stances", "ipo", "icon"),
            Theme = Prose.Text("stances", "ipo", "theme"),
            CostText = Prose.Text("stances", "ipo", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(1.3), Modifier.GoldenCookieReward(0.85)],
        },
        new()
        {
            Id = Union,
            Name = Prose.Text("stances", "union", "name"),
            Icon = Prose.Text("stances", "union", "icon"),
            Theme = Prose.Text("stances", "union", "theme"),
            CostText = Prose.Text("stances", "union", "costText"),
            Modifiers = [Modifier.GlobalMultiplier(0.85), Modifier.ClickMultiplier(2)],
        },
        new()
        {
            Id = Liquidate,
            Name = Prose.Text("stances", "liquidate", "name"),
            Icon = Prose.Text("stances", "liquidate", "icon"),
            Theme = Prose.Text("stances", "liquidate", "theme"),
            CostText = Prose.Text("stances", "liquidate", "costText"),
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
