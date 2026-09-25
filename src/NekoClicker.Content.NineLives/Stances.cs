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
            Name = "神性",
            Icon = "👁️",
            Theme = "她想把自己拼回去，哪怕拼回来的那个不记得纸箱。",
            CostText = "产量 ×1.25，但她越来越不像是会撞见意外的生物——金猫出现频率 ×0.75。",
            Modifiers =
            [
                Modifier.GlobalMultiplier(1.25),
                Modifier.GoldenCookieFrequency(0.75),
            ],
        },
        new()
        {
            Id = Human,
            Name = "人形",
            Icon = "🧍",
            Theme = "她想学会用两条腿走路，把想说的话说完。",
            CostText = "点击收益 ×2，但自动化让步于亲手——全局产量 ×0.85。",
            Modifiers =
            [
                Modifier.ClickMultiplier(2),
                Modifier.GlobalMultiplier(0.85),
            ],
        },
        new()
        {
            Id = Cat,
            Name = "猫形",
            Icon = "🐾",
            Theme = "她只想晒太阳。九个世界来来回回，地板还是那一块。",
            CostText = "金猫奖励 ×1.3，但产量 ×0.9——她不太在意剩下的世界。",
            Modifiers =
            [
                Modifier.GoldenCookieReward(1.3),
                Modifier.GlobalMultiplier(0.9),
            ],
        },
        new()
        {
            Id = Sever,
            Name = "断绝",
            Icon = "🕯️",
            Theme = "她想把九节一起点亮，然后什么都不做。",
            CostText = "建筑价格 ×0.8（她开始拆东西），但金猫奖励 ×0.7。",
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
