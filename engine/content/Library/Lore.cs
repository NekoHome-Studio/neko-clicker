using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几本书</b>走：每开一本新书，故事往前推一段；四条线各自在很小的动作上开篇
/// （点击 1 / 30 页 / 100 次 / 400 次），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出六条"（阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余五个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。
///     这里用的是"纪元门槛 + 层内里程碑"，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前开新书，这条永远读不到。</item>
/// </list>
/// </para>
/// <para>
/// <b>「读者」线是这个包独有的麻烦：它的门槛指标会掉。</b>被阅读度会衰减，每次开新书还会清零，
/// 所以那几条的达成时刻取决于"读者正养着的时候"，而不是"曾经达到过"。
/// 这没有违反纪律 2——它们在真实游玩里的解锁顺序仍然单调，用例是在真跑图里验的
/// （<c>LibraryContentTests.Storylines_ReadInOrderDuringARealPlaythrough</c>），
/// 而不是靠静态推理。</para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Library"));

    /// <summary>本包的散文文件（随包复制到 content/Library/text.json）。取不到就抛，绝不回退成空白。</summary>
    private static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("she", 12),
        Line("shelf", 10),
        Line("reader", 10),
        Line("blank", 8),
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
        .. She(),
        .. Shelf(),
        .. Reader(),
        .. Blank(),
    ];

    private static IEnumerable<LoreEntry> She() =>
    [
        new()
        {
            Id = "she_01",
            Title = Prose.Text("lore", "she_01", "title"),
            StorylineId = "she",
            Order = 1,
            Icon = "📄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = Prose.Text("lore", "she_01", "body"),
        },
        new()
        {
            Id = "she_02",
            Title = Prose.Text("lore", "she_02", "title"),
            StorylineId = "she",
            Order = 2,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2e5)),
            Body = Prose.Text("lore", "she_02", "body"),
        },
        new()
        {
            Id = "she_03",
            Title = Prose.Text("lore", "she_03", "title"),
            StorylineId = "she",
            Order = 3,
            Icon = "🗺️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2e6)),
            Body = Prose.Text("lore", "she_03", "body"),
        },
        new()
        {
            Id = "she_04",
            Title = Prose.Text("lore", "she_04", "title"),
            StorylineId = "she",
            Order = 4,
            Icon = "🖍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5e6)),
            Body = Prose.Text("lore", "she_04", "body"),
        },
        new()
        {
            Id = "she_05",
            Title = Prose.Text("lore", "she_05", "title"),
            StorylineId = "she",
            Order = 5,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4e7)),
            Body = Prose.Text("lore", "she_05", "body"),
        },
        new()
        {
            Id = "she_06",
            Title = Prose.Text("lore", "she_06", "title"),
            StorylineId = "she",
            Order = 6,
            Icon = "📖",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1e8)),
            Body = Prose.Text("lore", "she_06", "body"),
        },
        new()
        {
            Id = "she_07",
            Title = Prose.Text("lore", "she_07", "title"),
            StorylineId = "she",
            Order = 7,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(8e8)),
            Body = Prose.Text("lore", "she_07", "body"),
        },
        new()
        {
            Id = "she_08",
            Title = Prose.Text("lore", "she_08", "title"),
            StorylineId = "she",
            Order = 8,
            Icon = "👤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(5e9)),
            Body = Prose.Text("lore", "she_08", "body"),
        },
        new()
        {
            Id = "she_09",
            Title = Prose.Text("lore", "she_09", "title"),
            StorylineId = "she",
            Order = 9,
            Icon = "🔖",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2e10)),
            Body = Prose.Text("lore", "she_09", "body"),
        },
        new()
        {
            Id = "she_10",
            Title = Prose.Text("lore", "she_10", "title"),
            StorylineId = "she",
            Order = 10,
            Icon = "❓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4e10)),
            Body = Prose.Text("lore", "she_10", "body"),
        },
        new()
        {
            Id = "she_11",
            Title = Prose.Text("lore", "she_11", "title"),
            StorylineId = "she",
            Order = 11,
            Icon = "✒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8e10)),
            Body = Prose.Text("lore", "she_11", "body"),
        },
        new()
        {
            Id = "she_12",
            Title = Prose.Text("lore", "she_12", "title"),
            StorylineId = "she",
            Order = 12,
            Icon = "📕",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.5e10)),
            Body = Prose.Text("lore", "she_12", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Shelf() =>
    [
        new()
        {
            Id = "shelf_01",
            Title = Prose.Text("lore", "shelf_01", "title"),
            StorylineId = "shelf",
            Order = 1,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedAllTimeAtLeast(30),
            Body = Prose.Text("lore", "shelf_01", "body"),
        },
        new()
        {
            Id = "shelf_02",
            Title = Prose.Text("lore", "shelf_02", "title"),
            StorylineId = "shelf",
            Order = 2,
            Icon = "🪜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5e4)),
            Body = Prose.Text("lore", "shelf_02", "body"),
        },
        new()
        {
            Id = "shelf_03",
            Title = Prose.Text("lore", "shelf_03", "title"),
            StorylineId = "shelf",
            Order = 3,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5e5)),
            Body = Prose.Text("lore", "shelf_03", "body"),
        },
        new()
        {
            Id = "shelf_04",
            Title = Prose.Text("lore", "shelf_04", "title"),
            StorylineId = "shelf",
            Order = 4,
            Icon = "📠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e6)),
            Body = Prose.Text("lore", "shelf_04", "body"),
        },
        new()
        {
            Id = "shelf_05",
            Title = Prose.Text("lore", "shelf_05", "title"),
            StorylineId = "shelf",
            Order = 5,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e7)),
            Body = Prose.Text("lore", "shelf_05", "body"),
        },
        new()
        {
            Id = "shelf_06",
            Title = Prose.Text("lore", "shelf_06", "title"),
            StorylineId = "shelf",
            Order = 6,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2e8)),
            Body = Prose.Text("lore", "shelf_06", "body"),
        },
        new()
        {
            Id = "shelf_07",
            Title = Prose.Text("lore", "shelf_07", "title"),
            StorylineId = "shelf",
            Order = 7,
            Icon = "🖨️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4e8)),
            Body = Prose.Text("lore", "shelf_07", "body"),
        },
        new()
        {
            Id = "shelf_08",
            Title = Prose.Text("lore", "shelf_08", "title"),
            StorylineId = "shelf",
            Order = 8,
            Icon = "🧩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2e9)),
            Body = Prose.Text("lore", "shelf_08", "body"),
        },
        new()
        {
            Id = "shelf_09",
            Title = Prose.Text("lore", "shelf_09", "title"),
            StorylineId = "shelf",
            Order = 9,
            Icon = "♾️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.5e10)),
            Body = Prose.Text("lore", "shelf_09", "body"),
        },
        new()
        {
            Id = "shelf_10",
            Title = Prose.Text("lore", "shelf_10", "title"),
            StorylineId = "shelf",
            Order = 10,
            Icon = "⬆️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9e10)),
            Body = Prose.Text("lore", "shelf_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Reader() =>
    [
        new()
        {
            Id = "reader_01",
            Title = Prose.Text("lore", "reader_01", "title"),
            StorylineId = "reader",
            Order = 1,
            Icon = "👤",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = Prose.Text("lore", "reader_01", "body"),
        },
        new()
        {
            Id = "reader_02",
            Title = Prose.Text("lore", "reader_02", "title"),
            StorylineId = "reader",
            Order = 2,
            Icon = "🗂️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 1_500)),
            Body = Prose.Text("lore", "reader_02", "body"),
        },
        new()
        {
            Id = "reader_03",
            Title = Prose.Text("lore", "reader_03", "title"),
            StorylineId = "reader",
            Order = 3,
            Icon = "🔍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 5_000)),
            Body = Prose.Text("lore", "reader_03", "body"),
        },
        new()
        {
            Id = "reader_04",
            Title = Prose.Text("lore", "reader_04", "title"),
            StorylineId = "reader",
            Order = 4,
            Icon = "💬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 12_000)),
            Body = Prose.Text("lore", "reader_04", "body"),
        },
        new()
        {
            Id = "reader_05",
            Title = Prose.Text("lore", "reader_05", "title"),
            StorylineId = "reader",
            Order = 5,
            Icon = "✉️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 30_000)),
            Body = Prose.Text("lore", "reader_05", "body"),
        },
        new()
        {
            Id = "reader_06",
            Title = Prose.Text("lore", "reader_06", "title"),
            StorylineId = "reader",
            Order = 6,
            Icon = "📕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 45_000)),
            Body = Prose.Text("lore", "reader_06", "body"),
        },
        new()
        {
            Id = "reader_07",
            Title = Prose.Text("lore", "reader_07", "title"),
            StorylineId = "reader",
            Order = 7,
            Icon = "🏁",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 58_000)),
            Body = Prose.Text("lore", "reader_07", "body"),
        },
        new()
        {
            Id = "reader_08",
            Title = Prose.Text("lore", "reader_08", "title"),
            StorylineId = "reader",
            Order = 8,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 62_000)),
            Body = Prose.Text("lore", "reader_08", "body"),
        },
        new()
        {
            Id = "reader_09",
            Title = Prose.Text("lore", "reader_09", "title"),
            StorylineId = "reader",
            Order = 9,
            Icon = "👣",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 67_000)),
            Body = Prose.Text("lore", "reader_09", "body"),
        },
        new()
        {
            Id = "reader_10",
            Title = Prose.Text("lore", "reader_10", "title"),
            StorylineId = "reader",
            Order = 10,
            Icon = "🕯️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 70_000)),
            Body = Prose.Text("lore", "reader_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Blank() =>
    [
        new()
        {
            Id = "blank_01",
            Title = Prose.Text("lore", "blank_01", "title"),
            StorylineId = "blank",
            Order = 1,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(400),
            Body = Prose.Text("lore", "blank_01", "body"),
        },
        new()
        {
            Id = "blank_02",
            Title = Prose.Text("lore", "blank_02", "title"),
            StorylineId = "blank",
            Order = 2,
            Icon = "🗃️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(12),
            Body = Prose.Text("lore", "blank_02", "body"),
        },
        new()
        {
            Id = "blank_03",
            Title = Prose.Text("lore", "blank_03", "title"),
            StorylineId = "blank",
            Order = 3,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(16),
            Body = Prose.Text("lore", "blank_03", "body"),
        },
        new()
        {
            Id = "blank_04",
            Title = Prose.Text("lore", "blank_04", "title"),
            StorylineId = "blank",
            Order = 4,
            Icon = "✒️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.AchievementsAtLeast(20)),
            Body = Prose.Text("lore", "blank_04", "body"),
        },
        new()
        {
            Id = "blank_05",
            Title = Prose.Text("lore", "blank_05", "title"),
            StorylineId = "blank",
            Order = 5,
            Icon = "📖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.AchievementsAtLeast(24)),
            Body = Prose.Text("lore", "blank_05", "body"),
        },
        new()
        {
            Id = "blank_06",
            Title = Prose.Text("lore", "blank_06", "title"),
            StorylineId = "blank",
            Order = 6,
            Icon = "👁️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.AchievementsAtLeast(28)),
            Body = Prose.Text("lore", "blank_06", "body"),
        },
        new()
        {
            Id = "blank_07",
            Title = Prose.Text("lore", "blank_07", "title"),
            StorylineId = "blank",
            Order = 7,
            Icon = "🌑",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.AchievementsAtLeast(32)),
            Body = Prose.Text("lore", "blank_07", "body"),
        },
        new()
        {
            Id = "blank_08",
            Title = Prose.Text("lore", "blank_08", "title"),
            StorylineId = "blank",
            Order = 8,
            Icon = "📄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.AchievementsAtLeast(34)),
            Body = Prose.Text("lore", "blank_08", "body"),
        },
    ];

    /// <summary>
    /// 文本文件里“有、但代码从不取用”的条目会在这里抛（孤儿文本）。
    /// 调用点是 <see cref="Prose"/> 那一套：包在 <c>Build()</c> 末尾调用它。
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

}
