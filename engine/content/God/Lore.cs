using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 叙事条目：40 条，分四条线（12 + 10 + 10 + 8）。<para>
/// 节奏跟着<b>换到第几套神话</b>走：每切一次体系，故事往前推一段；四条线各自在一个很小的
/// 动作上开篇（点击 1 / 25 / 100 / 500），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出三条以上"（阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余六个包相同，前两条由通用守卫守着，第三条由包内用例在真跑图里验）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。
///     这里 36 条层内条目用的是 36 个<b>互不相同</b>的累计赚取门槛（全是看着别扭的数字，
///     那是故意的：圆整数容易撞车）。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。用"纪元门槛 +
///     层内里程碑"定序：进层那一刻层内里程碑必然归零，所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前切走，
///     这条<b>结构上读不到</b>（<c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c>）。</item>
/// </list>
/// </para>
/// <para>
/// <b>互文是单向的</b>（<c>engine/docs/CONTENT_AUTHORING.md</c> §12.3）：这个包可以提起九命轮回的
/// 「神明纪元」（那一段的进层文本写的就是"第一炷香是纸箱味的"），也可以望向那家还没开张的店，
/// 但它<b>不要求玩家装过任何别的包</b>——四条线单独读也完整。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("God"));

    /// <summary>本包的散文文件（随包复制到 content/God/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("cat", 12),
        Line("myth", 10),
        Line("fans", 10),
        Line("meta", 8),
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
        .. Cat(),
        .. Myth(),
        .. Fans(),
        .. Meta(),
    ];

    private static IEnumerable<LoreEntry> Cat() =>
    [
        new()
        {
            Id = "cat_01",
            Title = Prose.Text("lore", "cat_01", "title"),
            StorylineId = "cat",
            Order = 1,
            Icon = "🏠",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = Prose.Text("lore", "cat_01", "body"),
        },
        new()
        {
            Id = "cat_02",
            Title = Prose.Text("lore", "cat_02", "title"),
            StorylineId = "cat",
            Order = 2,
            Icon = "📜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.2e6)),
            Body = Prose.Text("lore", "cat_02", "body"),
        },
        new()
        {
            Id = "cat_03",
            Title = Prose.Text("lore", "cat_03", "title"),
            StorylineId = "cat",
            Order = 3,
            Icon = "☀️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(8.5e6)),
            Body = Prose.Text("lore", "cat_03", "body"),
        },
        new()
        {
            Id = "cat_04",
            Title = Prose.Text("lore", "cat_04", "title"),
            StorylineId = "cat",
            Order = 4,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.4e7)),
            Body = Prose.Text("lore", "cat_04", "body"),
        },
        new()
        {
            Id = "cat_05",
            Title = Prose.Text("lore", "cat_05", "title"),
            StorylineId = "cat",
            Order = 5,
            Icon = "🗒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9e7)),
            Body = Prose.Text("lore", "cat_05", "body"),
        },
        new()
        {
            Id = "cat_06",
            Title = Prose.Text("lore", "cat_06", "title"),
            StorylineId = "cat",
            Order = 6,
            Icon = "🌌",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.4e8)),
            Body = Prose.Text("lore", "cat_06", "body"),
        },
        new()
        {
            Id = "cat_07",
            Title = Prose.Text("lore", "cat_07", "title"),
            StorylineId = "cat",
            Order = 7,
            Icon = "⚡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.2e8)),
            Body = Prose.Text("lore", "cat_07", "body"),
        },
        new()
        {
            Id = "cat_08",
            Title = Prose.Text("lore", "cat_08", "title"),
            StorylineId = "cat",
            Order = 8,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.7e9)),
            Body = Prose.Text("lore", "cat_08", "body"),
        },
        new()
        {
            Id = "cat_09",
            Title = Prose.Text("lore", "cat_09", "title"),
            StorylineId = "cat",
            Order = 9,
            Icon = "🎬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.6e9)),
            Body = Prose.Text("lore", "cat_09", "body"),
        },
        new()
        {
            Id = "cat_10",
            Title = Prose.Text("lore", "cat_10", "title"),
            StorylineId = "cat",
            Order = 10,
            Icon = "🐙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e10)),
            Body = Prose.Text("lore", "cat_10", "body"),
        },
        new()
        {
            Id = "cat_11",
            Title = Prose.Text("lore", "cat_11", "title"),
            StorylineId = "cat",
            Order = 11,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e10)),
            Body = Prose.Text("lore", "cat_11", "body"),
        },
        new()
        {
            Id = "cat_12",
            Title = Prose.Text("lore", "cat_12", "title"),
            StorylineId = "cat",
            Order = 12,
            Icon = "🌑",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.9e10)),
            Body = Prose.Text("lore", "cat_12", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Myth() =>
    [
        new()
        {
            Id = "myth_01",
            Title = Prose.Text("lore", "myth_01", "title"),
            StorylineId = "myth",
            Order = 1,
            Icon = "🎭",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = Prose.Text("lore", "myth_01", "body"),
        },
        new()
        {
            Id = "myth_02",
            Title = Prose.Text("lore", "myth_02", "title"),
            StorylineId = "myth",
            Order = 2,
            Icon = "🐈",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5.1e6)),
            Body = Prose.Text("lore", "myth_02", "body"),
        },
        new()
        {
            Id = "myth_03",
            Title = Prose.Text("lore", "myth_03", "title"),
            StorylineId = "myth",
            Order = 3,
            Icon = "🖋️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.25e7)),
            Body = Prose.Text("lore", "myth_03", "body"),
        },
        new()
        {
            Id = "myth_04",
            Title = Prose.Text("lore", "myth_04", "title"),
            StorylineId = "myth",
            Order = 4,
            Icon = "🏛️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4.4e7)),
            Body = Prose.Text("lore", "myth_04", "body"),
        },
        new()
        {
            Id = "myth_05",
            Title = Prose.Text("lore", "myth_05", "title"),
            StorylineId = "myth",
            Order = 5,
            Icon = "📜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.55e8)),
            Body = Prose.Text("lore", "myth_05", "body"),
        },
        new()
        {
            Id = "myth_06",
            Title = Prose.Text("lore", "myth_06", "title"),
            StorylineId = "myth",
            Order = 6,
            Icon = "📄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(6.1e8)),
            Body = Prose.Text("lore", "myth_06", "body"),
        },
        new()
        {
            Id = "myth_07",
            Title = Prose.Text("lore", "myth_07", "title"),
            StorylineId = "myth",
            Order = 7,
            Icon = "🎟️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.9e9)),
            Body = Prose.Text("lore", "myth_07", "body"),
        },
        new()
        {
            Id = "myth_08",
            Title = Prose.Text("lore", "myth_08", "title"),
            StorylineId = "myth",
            Order = 8,
            Icon = "🌀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.6e10)),
            Body = Prose.Text("lore", "myth_08", "body"),
        },
        new()
        {
            Id = "myth_09",
            Title = Prose.Text("lore", "myth_09", "title"),
            StorylineId = "myth",
            Order = 9,
            Icon = "💬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.4e10)),
            Body = Prose.Text("lore", "myth_09", "body"),
        },
        new()
        {
            Id = "myth_10",
            Title = Prose.Text("lore", "myth_10", "title"),
            StorylineId = "myth",
            Order = 10,
            Icon = "🐾",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.4e10)),
            Body = Prose.Text("lore", "myth_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Fans() =>
    [
        new()
        {
            Id = "fans_01",
            Title = Prose.Text("lore", "fans_01", "title"),
            StorylineId = "fans",
            Order = 1,
            Icon = "🐹",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = Prose.Text("lore", "fans_01", "body"),
        },
        new()
        {
            Id = "fans_02",
            Title = Prose.Text("lore", "fans_02", "title"),
            StorylineId = "fans",
            Order = 2,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(6.4e6)),
            Body = Prose.Text("lore", "fans_02", "body"),
        },
        new()
        {
            Id = "fans_03",
            Title = Prose.Text("lore", "fans_03", "title"),
            StorylineId = "fans",
            Order = 3,
            Icon = "🧾",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.4e7)),
            Body = Prose.Text("lore", "fans_03", "body"),
        },
        new()
        {
            Id = "fans_04",
            Title = Prose.Text("lore", "fans_04", "title"),
            StorylineId = "fans",
            Order = 4,
            Icon = "⚔️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.7e7)),
            Body = Prose.Text("lore", "fans_04", "body"),
        },
        new()
        {
            Id = "fans_05",
            Title = Prose.Text("lore", "fans_05", "title"),
            StorylineId = "fans",
            Order = 5,
            Icon = "📕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.9e8)),
            Body = Prose.Text("lore", "fans_05", "body"),
        },
        new()
        {
            Id = "fans_06",
            Title = Prose.Text("lore", "fans_06", "title"),
            StorylineId = "fans",
            Order = 6,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(8.3e8)),
            Body = Prose.Text("lore", "fans_06", "body"),
        },
        new()
        {
            Id = "fans_07",
            Title = Prose.Text("lore", "fans_07", "title"),
            StorylineId = "fans",
            Order = 7,
            Icon = "🔬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(3.6e9)),
            Body = Prose.Text("lore", "fans_07", "body"),
        },
        new()
        {
            Id = "fans_08",
            Title = Prose.Text("lore", "fans_08", "title"),
            StorylineId = "fans",
            Order = 8,
            Icon = "📹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.1e10)),
            Body = Prose.Text("lore", "fans_08", "body"),
        },
        new()
        {
            Id = "fans_09",
            Title = Prose.Text("lore", "fans_09", "title"),
            StorylineId = "fans",
            Order = 9,
            Icon = "🎁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(7.3e10)),
            Body = Prose.Text("lore", "fans_09", "body"),
        },
        new()
        {
            Id = "fans_10",
            Title = Prose.Text("lore", "fans_10", "title"),
            StorylineId = "fans",
            Order = 10,
            Icon = "🧸",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.7e10)),
            Body = Prose.Text("lore", "fans_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Meta() =>
    [
        new()
        {
            Id = "meta_01",
            Title = Prose.Text("lore", "meta_01", "title"),
            StorylineId = "meta",
            Order = 1,
            Icon = "🧾",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(500),
            Body = Prose.Text("lore", "meta_01", "body"),
        },
        new()
        {
            Id = "meta_02",
            Title = Prose.Text("lore", "meta_02", "title"),
            StorylineId = "meta",
            Order = 2,
            Icon = "📎",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(9.3e6)),
            Body = Prose.Text("lore", "meta_02", "body"),
        },
        new()
        {
            Id = "meta_03",
            Title = Prose.Text("lore", "meta_03", "title"),
            StorylineId = "meta",
            Order = 3,
            Icon = "⚠️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(3.7e7)),
            Body = Prose.Text("lore", "meta_03", "body"),
        },
        new()
        {
            Id = "meta_04",
            Title = Prose.Text("lore", "meta_04", "title"),
            StorylineId = "meta",
            Order = 4,
            Icon = "👕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.1e8)),
            Body = Prose.Text("lore", "meta_04", "body"),
        },
        new()
        {
            Id = "meta_05",
            Title = Prose.Text("lore", "meta_05", "title"),
            StorylineId = "meta",
            Order = 5,
            Icon = "⏰",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.2e9)),
            Body = Prose.Text("lore", "meta_05", "body"),
        },
        new()
        {
            Id = "meta_06",
            Title = Prose.Text("lore", "meta_06", "title"),
            StorylineId = "meta",
            Order = 6,
            Icon = "🎤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e10)),
            Body = Prose.Text("lore", "meta_06", "body"),
        },
        new()
        {
            Id = "meta_07",
            Title = Prose.Text("lore", "meta_07", "title"),
            StorylineId = "meta",
            Order = 7,
            Icon = "😹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.1e10)),
            Body = Prose.Text("lore", "meta_07", "body"),
        },
        new()
        {
            Id = "meta_08",
            Title = Prose.Text("lore", "meta_08", "title"),
            StorylineId = "meta",
            Order = 8,
            Icon = "📚",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.1e10)),
            Body = Prose.Text("lore", "meta_08", "body"),
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
