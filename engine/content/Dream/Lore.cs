using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几层梦</b>走：每往下睡一层，故事往前推一段；四条线各自在很小的动作上开篇，
/// 免得图鉴一开就是一整墙 ???。
/// </para>
/// <para>
/// <b>四条纪律</b>（与其余包相同，每一条都在这个包里踩过一次）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同一个条件必然在同一瞬间一起解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增。这个包的多指标混排最容易在这里翻车：
///     "本轮累计"在往下睡一层时会归零，所以纪元门槛与层内里程碑必须<b>按层分块</b>，
///     否则会出现"第 3 条还锁着，第 4 条已经亮了"。</item>
///   <item>挂了 <c>EraAtLeast(n)</c> 的条目，层内门槛必须 &lt; 第 n 层的完成门槛
///     （<c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 通用守卫）。</item>
///   <item>转生类条目只能放线的尾部——这个包<b>没有</b>转生类条目：往下睡一层的时机由玩家定。</item>
/// </list>
/// </para>
/// <para>
/// <b>「开局十分钟 ≤ 3 条」是怎么做到的</b>：四条线里只有三条早开，
/// 而且它们的开篇动作各不相同（点击 1 / 25 / 100）；第四条（梦层）晚一步，
/// 挂在"点到 80 下之后"上——它是这一包里唯一的"进得去才发现"的那条线。
/// 其余条目的门槛一律压在 <b>2e7 以上</b>，而开局十分钟的包络只有 2.7e7，
/// 所以它们在 G5 窗口里根本够不着。这条数值边界由
/// <c>DreamContentTests.G5_FirstTenMinutesRevealAtMostThreeEntries</c> 守着。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Dream"));

    /// <summary>本包的散文文件（随包复制到 content/Dream/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("her", 12),
        Line("layers", 10),
        Line("mare", 10),
        Line("waking", 8),
    ];

    /// <summary>剧情线的名称 / 主题 / 图标来自文本文件；条数留在代码里（它是纪律，不是散文）。</summary>
    private static StorylineDefinition Line(string id, int totalEntries) => new()
    {
        Id = id,
        Name = Prose.Text("storylines", id, "name"),
        Theme = Prose.Text("storylines", id, "theme"),
        Icon = Prose.Text("storylines", id, "icon"),
        TotalEntries = totalEntries,
    };

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Her(),
        .. Layers(),
        .. Mare(),
        .. Waking(),
    ];

    /// <summary>「她」线：开篇是"点一下"，此后按层逐段推进。</summary>
    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = Prose.Text("lore", "her_01", "title"),
            StorylineId = "her",
            Order = 1,
            Icon = "🛏️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = Prose.Text("lore", "her_01", "body"),
        },
        new()
        {
            Id = "her_02",
            Title = Prose.Text("lore", "her_02", "title"),
            StorylineId = "her",
            Order = 2,
            Icon = "⤵️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.75e7),
            Body = Prose.Text("lore", "her_02", "body"),
        },
        new()
        {
            Id = "her_03",
            Title = Prose.Text("lore", "her_03", "title"),
            StorylineId = "her",
            Order = 3,
            Icon = "😴",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.6e8)),
            Body = Prose.Text("lore", "her_03", "body"),
        },
        new()
        {
            Id = "her_04",
            Title = Prose.Text("lore", "her_04", "title"),
            StorylineId = "her",
            Order = 4,
            Icon = "🖐️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.5e9)),
            Body = Prose.Text("lore", "her_04", "body"),
        },
        new()
        {
            Id = "her_05",
            Title = Prose.Text("lore", "her_05", "title"),
            StorylineId = "her",
            Order = 5,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e10)),
            Body = Prose.Text("lore", "her_05", "body"),
        },
        new()
        {
            Id = "her_06",
            Title = Prose.Text("lore", "her_06", "title"),
            StorylineId = "her",
            Order = 6,
            Icon = "🔢",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.8e11)),
            Body = Prose.Text("lore", "her_06", "body"),
        },
        new()
        {
            Id = "her_07",
            Title = Prose.Text("lore", "her_07", "title"),
            StorylineId = "her",
            Order = 7,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.5e11)),
            Body = Prose.Text("lore", "her_07", "body"),
        },
        new()
        {
            Id = "her_08",
            Title = Prose.Text("lore", "her_08", "title"),
            StorylineId = "her",
            Order = 8,
            Icon = "🐈",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.5e11)),
            Body = Prose.Text("lore", "her_08", "body"),
        },
        new()
        {
            Id = "her_09",
            Title = Prose.Text("lore", "her_09", "title"),
            StorylineId = "her",
            Order = 9,
            Icon = "🔮",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.8e11)),
            Body = Prose.Text("lore", "her_09", "body"),
        },
        new()
        {
            Id = "her_10",
            Title = Prose.Text("lore", "her_10", "title"),
            StorylineId = "her",
            Order = 10,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.5e11)),
            Body = Prose.Text("lore", "her_10", "body"),
        },
        new()
        {
            Id = "her_11",
            Title = Prose.Text("lore", "her_11", "title"),
            StorylineId = "her",
            Order = 11,
            Icon = "🌀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.5e11)),
            Body = Prose.Text("lore", "her_11", "body"),
        },
        new()
        {
            Id = "her_12",
            Title = Prose.Text("lore", "her_12", "title"),
            StorylineId = "her",
            Order = 12,
            Icon = "🌄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.8e11)),
            Body = Prose.Text("lore", "her_12", "body"),
        },
    ];

    /// <summary>「梦层」线：四条线里唯一晚开的一条——它讲的是梦的地形本身。</summary>
    private static IEnumerable<LoreEntry> Layers() =>
    [
        new()
        {
            Id = "lay_01",
            Title = Prose.Text("lore", "lay_01", "title"),
            StorylineId = "layers",
            Order = 1,
            Icon = "🌙",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.9e7),
            Body = Prose.Text("lore", "lay_01", "body"),
        },
        new()
        {
            Id = "lay_02",
            Title = Prose.Text("lore", "lay_02", "title"),
            StorylineId = "layers",
            Order = 2,
            Icon = "🪞",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.45e8)),
            Body = Prose.Text("lore", "lay_02", "body"),
        },
        new()
        {
            Id = "lay_03",
            Title = Prose.Text("lore", "lay_03", "title"),
            StorylineId = "layers",
            Order = 3,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.5e8)),
            Body = Prose.Text("lore", "lay_03", "body"),
        },
        new()
        {
            Id = "lay_04",
            Title = Prose.Text("lore", "lay_04", "title"),
            StorylineId = "layers",
            Order = 4,
            Icon = "🚪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.4e9)),
            Body = Prose.Text("lore", "lay_04", "body"),
        },
        new()
        {
            Id = "lay_05",
            Title = Prose.Text("lore", "lay_05", "title"),
            StorylineId = "layers",
            Order = 5,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(8.5e9)),
            Body = Prose.Text("lore", "lay_05", "body"),
        },
        new()
        {
            Id = "lay_06",
            Title = Prose.Text("lore", "lay_06", "title"),
            StorylineId = "layers",
            Order = 6,
            Icon = "🧶",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.6e10)),
            Body = Prose.Text("lore", "lay_06", "body"),
        },
        new()
        {
            Id = "lay_07",
            Title = Prose.Text("lore", "lay_07", "title"),
            StorylineId = "layers",
            Order = 7,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.6e10)),
            Body = Prose.Text("lore", "lay_07", "body"),
        },
        new()
        {
            Id = "lay_08",
            Title = Prose.Text("lore", "lay_08", "title"),
            StorylineId = "layers",
            Order = 8,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e11)),
            Body = Prose.Text("lore", "lay_08", "body"),
        },
        new()
        {
            Id = "lay_09",
            Title = Prose.Text("lore", "lay_09", "title"),
            StorylineId = "layers",
            Order = 9,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.7e11)),
            Body = Prose.Text("lore", "lay_09", "body"),
        },
        new()
        {
            Id = "lay_10",
            Title = Prose.Text("lore", "lay_10", "title"),
            StorylineId = "layers",
            Order = 10,
            Icon = "🧩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.8e11)),
            Body = Prose.Text("lore", "lay_10", "body"),
        },
    ];

    /// <summary>「梦魇」线：开篇是"点 25 下"。</summary>
    private static IEnumerable<LoreEntry> Mare() =>
    [
        new()
        {
            Id = "mar_01",
            Title = Prose.Text("lore", "mar_01", "title"),
            StorylineId = "mare",
            Order = 1,
            Icon = "👁️",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = Prose.Text("lore", "mar_01", "body"),
        },
        new()
        {
            Id = "mar_02",
            Title = Prose.Text("lore", "mar_02", "title"),
            StorylineId = "mare",
            Order = 2,
            Icon = "🪨",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.95e7),
            Body = Prose.Text("lore", "mar_02", "body"),
        },
        new()
        {
            Id = "mar_03",
            Title = Prose.Text("lore", "mar_03", "title"),
            StorylineId = "mare",
            Order = 3,
            Icon = "🕷️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.7e8)),
            Body = Prose.Text("lore", "mar_03", "body"),
        },
        new()
        {
            Id = "mar_04",
            Title = Prose.Text("lore", "mar_04", "title"),
            StorylineId = "mare",
            Order = 4,
            Icon = "🪶",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.3e9)),
            Body = Prose.Text("lore", "mar_04", "body"),
        },
        new()
        {
            Id = "mar_05",
            Title = Prose.Text("lore", "mar_05", "title"),
            StorylineId = "mare",
            Order = 5,
            Icon = "🖤",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7.5e9)),
            Body = Prose.Text("lore", "mar_05", "body"),
        },
        new()
        {
            Id = "mar_06",
            Title = Prose.Text("lore", "mar_06", "title"),
            StorylineId = "mare",
            Order = 6,
            Icon = "🫥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(7.8e10)),
            Body = Prose.Text("lore", "mar_06", "body"),
        },
        new()
        {
            Id = "mar_07",
            Title = Prose.Text("lore", "mar_07", "title"),
            StorylineId = "mare",
            Order = 7,
            Icon = "🪞",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.15e11)),
            Body = Prose.Text("lore", "mar_07", "body"),
        },
        new()
        {
            Id = "mar_08",
            Title = Prose.Text("lore", "mar_08", "title"),
            StorylineId = "mare",
            Order = 8,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.2e11)),
            Body = Prose.Text("lore", "mar_08", "body"),
        },
        new()
        {
            Id = "mar_09",
            Title = Prose.Text("lore", "mar_09", "title"),
            StorylineId = "mare",
            Order = 9,
            Icon = "🤝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.9e11)),
            Body = Prose.Text("lore", "mar_09", "body"),
        },
        new()
        {
            Id = "mar_10",
            Title = Prose.Text("lore", "mar_10", "title"),
            StorylineId = "mare",
            Order = 10,
            Icon = "⚓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.4e11)),
            Body = Prose.Text("lore", "mar_10", "body"),
        },
    ];

    /// <summary>「清醒」线：开篇是"点 100 下"。</summary>
    private static IEnumerable<LoreEntry> Waking() =>
    [
        new()
        {
            Id = "wak_01",
            Title = Prose.Text("lore", "wak_01", "title"),
            StorylineId = "waking",
            Order = 1,
            Icon = "😴",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = Prose.Text("lore", "wak_01", "body"),
        },
        new()
        {
            Id = "wak_02",
            Title = Prose.Text("lore", "wak_02", "title"),
            StorylineId = "waking",
            Order = 2,
            Icon = "⏰",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(3.2e7),
            Body = Prose.Text("lore", "wak_02", "body"),
        },
        new()
        {
            Id = "wak_03",
            Title = Prose.Text("lore", "wak_03", "title"),
            StorylineId = "waking",
            Order = 3,
            Icon = "📣",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.9e8)),
            Body = Prose.Text("lore", "wak_03", "body"),
        },
        new()
        {
            Id = "wak_04",
            Title = Prose.Text("lore", "wak_04", "title"),
            StorylineId = "waking",
            Order = 4,
            Icon = "🛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.2e9)),
            Body = Prose.Text("lore", "wak_04", "body"),
        },
        new()
        {
            Id = "wak_05",
            Title = Prose.Text("lore", "wak_05", "title"),
            StorylineId = "waking",
            Order = 5,
            Icon = "🌅",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.5e9)),
            Body = Prose.Text("lore", "wak_05", "body"),
        },
        new()
        {
            Id = "wak_06",
            Title = Prose.Text("lore", "wak_06", "title"),
            StorylineId = "waking",
            Order = 6,
            Icon = "📝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(5.5e10)),
            Body = Prose.Text("lore", "wak_06", "body"),
        },
        new()
        {
            Id = "wak_07",
            Title = Prose.Text("lore", "wak_07", "title"),
            StorylineId = "waking",
            Order = 7,
            Icon = "🌬️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.1e11)),
            Body = Prose.Text("lore", "wak_07", "body"),
        },
        new()
        {
            Id = "wak_08",
            Title = Prose.Text("lore", "wak_08", "title"),
            StorylineId = "waking",
            Order = 8,
            Icon = "🌄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e11)),
            Body = Prose.Text("lore", "wak_08", "body"),
        },
    ];

    /// <summary>
    /// 文本文件里“有、但代码从不取用”的条目会在这里抛（孤儿文本）。<para>
    /// 覆盖<b>整份 <c>text.json</c></b>：剧情条目与 <see cref="Buildings"/> 的建筑文案都算——
    /// 两者共用同一份 <see cref="Prose"/> 实例，所以调用点必须在两者都被取过之后
    /// （现在只有包入口的 <c>Build()</c> 末尾一处）。
    /// </para>
    /// <para>
    /// 孤儿必须炸的理由见 <see cref="ContentText"/>：删了剧情却留着文本，
    /// 运行、断言、界面都不会有任何反应，于是那段死文本会一直躺在文件里等人改。
    /// </para>
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

}
