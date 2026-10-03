using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 四条立场：她可以往哪几个方向长。<para>
/// 这是九命包<b>自己的</b>价值取向轴，不是引擎枚举——换成别的包（道德、劳资、信仰）
/// 只改内容，核心一行不动。这正是把立场做成 <see cref="StanceDefinition"/> 而不是
/// <c>enum</c> 的原因。
/// </para>
/// <para>
/// 四条立场分别对应四个结局，所以它们是"终局的四个方向"，不是四种玩法风格。
/// 每条都<b>有得有失</b>：一个方向上的加成必然在别处留代价（设计文档 §5.3 的"有代价"）。
/// 修饰符只在<b>主导</b>时生效，所以摇摆不定的玩家拿不到任何一条。
/// </para>
/// <para>
/// 声明为 <c>public</c>（本包其余内容都是 <c>internal</c>）：立场 id 是包对外暴露的词汇，
/// 测试与将来的 UI 都要按 id 引用，藏起来只会逼出散落的字符串字面量。
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

    /// <summary>神性 —— 拼回完整、接上那个名字。</summary>
    public const string Divine = "divine";

    /// <summary>人形 —— 学会用两条腿走路。</summary>
    public const string Human = "human";

    /// <summary>猫形 —— 拒绝改变，一直换下去。</summary>
    public const string Cat = "cat";

    /// <summary>断绝 —— 把九节一起点亮，然后停下。</summary>
    public const string Sever = "sever";

    /// <summary>四条立场。</summary>
    public static StanceDefinition[] All =>
    [
        new()
        {
            Id = Divine,
            Name = Prose.Text("stances", "divine", "name"),
            Icon = Prose.Text("stances", "divine", "icon"),
            Theme = Prose.Text("stances", "divine", "theme"),
            CostText = Prose.Text("stances", "divine", "costText"),
            Modifiers =
            [
                Modifier.GlobalMultiplier(1.25),
                Modifier.GoldenCookieFrequency(0.75),
            ],
        },
        new()
        {
            Id = Human,
            Name = Prose.Text("stances", "human", "name"),
            Icon = Prose.Text("stances", "human", "icon"),
            Theme = Prose.Text("stances", "human", "theme"),
            CostText = Prose.Text("stances", "human", "costText"),
            Modifiers =
            [
                Modifier.ClickMultiplier(2),
                Modifier.GlobalMultiplier(0.85),
            ],
        },
        new()
        {
            Id = Cat,
            Name = Prose.Text("stances", "cat", "name"),
            Icon = Prose.Text("stances", "cat", "icon"),
            Theme = Prose.Text("stances", "cat", "theme"),
            CostText = Prose.Text("stances", "cat", "costText"),
            Modifiers =
            [
                Modifier.GoldenCookieReward(1.3),
                Modifier.GlobalMultiplier(0.9),
            ],
        },
        new()
        {
            Id = Sever,
            Name = Prose.Text("stances", "sever", "name"),
            Icon = Prose.Text("stances", "sever", "icon"),
            Theme = Prose.Text("stances", "sever", "theme"),
            CostText = Prose.Text("stances", "sever", "costText"),
            Modifiers =
            [
                Modifier.PriceMultiplier(0.8),
                Modifier.GoldenCookieReward(0.7),
            ],
        },
    ];

    /// <summary>主导某条立场所需的权重门槛。</summary>
    /// <remarks>
    /// 每条立场一共只有 3 次表态机会，每次权重 2——所以要达到 5 就必须
    /// <b>每一次都选它</b>。这不是巧合：四个结局是"承诺"，不是"倾向"，
    /// 摇摆的玩家会走到兜底结局（她没有被塑造成任何形状）。
    /// </remarks>
    public const int EndingThreshold = 5;
}
