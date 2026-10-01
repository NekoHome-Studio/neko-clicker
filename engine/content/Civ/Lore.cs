using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几个时代</b>走：每跨一个时代，故事往前推一段；四条线各自在很小的动作上开篇
/// （拍 1 / 25 / 100 / 600 次），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出四条"（阶段 2.6 的实测教训：G5 只允许 ≤3 条）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余七个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会看到倒挂。
///     这里用的是"纪元门槛 + 层内里程碑"，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前跨时代，这条永远读不到。
///     本包第 2~5 层的完成门槛是 1.5e7 / 3e8 / 6e9 / 1e11，所以层内里程碑最远只用到 2e9。</item>
/// </list>
/// </para>
/// <para>
/// <b>「文化」线是这个包唯一一条门槛指标跨时代不清零的线</b>（文化单调不减、且
/// <c>OnAscend</c> 有意不清零），所以它可以在同一个门槛上横跨很多层——
/// 而它读起来也正好是这么回事：<b>她记得住的东西，比任何一个时代都活得久。</b>
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Civ"));

    /// <summary>本包的散文文件（随包复制到 content/Civ/text.json）。取不到就抛，绝不回退成空白。</summary>
    private static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("cat", 12),
        Line("settle", 10),
        Line("age", 6),
        Line("memory", 12),
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
        .. Settle(),
        .. Age(),
        .. Memory(),
    ];

    private static IEnumerable<LoreEntry> Cat() =>
    [
        new()
        {
            Id = "cat_01",
            Title = Prose.Text("lore", "cat_01", "title"),
            StorylineId = "cat",
            Order = 1,
            Icon = "🪨",
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
            Icon = "🔥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(60),
                UnlockCondition.EarnedThisRunAtLeast(1.2e4)),
            Body = Prose.Text("lore", "cat_02", "body"),
        },
        new()
        {
            Id = "cat_03",
            Title = Prose.Text("lore", "cat_03", "title"),
            StorylineId = "cat",
            Order = 3,
            Icon = "🖐️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2.5e5)),
            Body = Prose.Text("lore", "cat_03", "body"),
        },
        new()
        {
            Id = "cat_04",
            Title = Prose.Text("lore", "cat_04", "title"),
            StorylineId = "cat",
            Order = 4,
            Icon = "🤥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(3.2e6)),
            Body = Prose.Text("lore", "cat_04", "body"),
        },
        new()
        {
            Id = "cat_05",
            Title = Prose.Text("lore", "cat_05", "title"),
            StorylineId = "cat",
            Order = 5,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.5e6)),
            Body = Prose.Text("lore", "cat_05", "body"),
        },
        new()
        {
            Id = "cat_06",
            Title = Prose.Text("lore", "cat_06", "title"),
            StorylineId = "cat",
            Order = 6,
            Icon = "🤝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5e7)),
            Body = Prose.Text("lore", "cat_06", "body"),
        },
        new()
        {
            Id = "cat_07",
            Title = Prose.Text("lore", "cat_07", "title"),
            StorylineId = "cat",
            Order = 7,
            Icon = "🏛️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(9e7)),
            Body = Prose.Text("lore", "cat_07", "body"),
        },
        new()
        {
            Id = "cat_08",
            Title = Prose.Text("lore", "cat_08", "title"),
            StorylineId = "cat",
            Order = 8,
            Icon = "📐",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.1e8)),
            Body = Prose.Text("lore", "cat_08", "body"),
        },
        new()
        {
            Id = "cat_09",
            Title = Prose.Text("lore", "cat_09", "title"),
            StorylineId = "cat",
            Order = 9,
            Icon = "🖌️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.ClicksAtLeast(120)),
            Body = Prose.Text("lore", "cat_09", "body"),
        },
        new()
        {
            Id = "cat_10",
            Title = Prose.Text("lore", "cat_10", "title"),
            StorylineId = "cat",
            Order = 10,
            Icon = "🌌",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e8)),
            Body = Prose.Text("lore", "cat_10", "body"),
        },
        new()
        {
            Id = "cat_11",
            Title = Prose.Text("lore", "cat_11", "title"),
            StorylineId = "cat",
            Order = 11,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9e8)),
            Body = Prose.Text("lore", "cat_11", "body"),
        },
        new()
        {
            Id = "cat_12",
            Title = Prose.Text("lore", "cat_12", "title"),
            StorylineId = "cat",
            Order = 12,
            Icon = "🌠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.6e10)),
            Body = Prose.Text("lore", "cat_12", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Settle() =>
    [
        new()
        {
            Id = "settle_01",
            Title = Prose.Text("lore", "settle_01", "title"),
            StorylineId = "settle",
            Order = 1,
            Icon = "🛤️",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = Prose.Text("lore", "settle_01", "body"),
        },
        new()
        {
            Id = "settle_02",
            Title = Prose.Text("lore", "settle_02", "title"),
            StorylineId = "settle",
            Order = 2,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.4e5)),
            Body = Prose.Text("lore", "settle_02", "body"),
        },
        new()
        {
            Id = "settle_03",
            Title = Prose.Text("lore", "settle_03", "title"),
            StorylineId = "settle",
            Order = 3,
            Icon = "🏪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.6e6)),
            Body = Prose.Text("lore", "settle_03", "body"),
        },
        new()
        {
            Id = "settle_04",
            Title = Prose.Text("lore", "settle_04", "title"),
            StorylineId = "settle",
            Order = 4,
            Icon = "🌙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9.5e6)),
            Body = Prose.Text("lore", "settle_04", "body"),
        },
        new()
        {
            Id = "settle_05",
            Title = Prose.Text("lore", "settle_05", "title"),
            StorylineId = "settle",
            Order = 5,
            Icon = "🚪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7.2e7)),
            Body = Prose.Text("lore", "settle_05", "body"),
        },
        new()
        {
            Id = "settle_06",
            Title = Prose.Text("lore", "settle_06", "title"),
            StorylineId = "settle",
            Order = 6,
            Icon = "🗿",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.5e6)),
            Body = Prose.Text("lore", "settle_06", "body"),
        },
        new()
        {
            Id = "settle_07",
            Title = Prose.Text("lore", "settle_07", "title"),
            StorylineId = "settle",
            Order = 7,
            Icon = "🏛️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.2e7)),
            Body = Prose.Text("lore", "settle_07", "body"),
        },
        new()
        {
            Id = "settle_08",
            Title = Prose.Text("lore", "settle_08", "title"),
            StorylineId = "settle",
            Order = 8,
            Icon = "❓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("academy", 12)),
            Body = Prose.Text("lore", "settle_08", "body"),
        },
        new()
        {
            Id = "settle_09",
            Title = Prose.Text("lore", "settle_09", "title"),
            StorylineId = "settle",
            Order = 9,
            Icon = "🚀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.75e9)),
            Body = Prose.Text("lore", "settle_09", "body"),
        },
        new()
        {
            Id = "settle_10",
            Title = Prose.Text("lore", "settle_10", "title"),
            StorylineId = "settle",
            Order = 10,
            Icon = "🪹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.8e9)),
            Body = Prose.Text("lore", "settle_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Age() =>
    [
        new()
        {
            Id = "age_01",
            Title = Prose.Text("lore", "age_01", "title"),
            StorylineId = "age",
            Order = 1,
            Icon = "⏳",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(600),
            Body = Prose.Text("lore", "age_01", "body"),
        },
        new()
        {
            Id = "age_02",
            Title = Prose.Text("lore", "age_02", "title"),
            StorylineId = "age",
            Order = 2,
            Icon = "🏘️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(7.5e5)),
            Body = Prose.Text("lore", "age_02", "body"),
        },
        new()
        {
            Id = "age_03",
            Title = Prose.Text("lore", "age_03", "title"),
            StorylineId = "age",
            Order = 3,
            Icon = "👵",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.15e7)),
            Body = Prose.Text("lore", "age_03", "body"),
        },
        new()
        {
            Id = "age_04",
            Title = Prose.Text("lore", "age_04", "title"),
            StorylineId = "age",
            Order = 4,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.6e8)),
            Body = Prose.Text("lore", "age_04", "body"),
        },
        new()
        {
            Id = "age_05",
            Title = Prose.Text("lore", "age_05", "title"),
            StorylineId = "age",
            Order = 5,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.1e9)),
            Body = Prose.Text("lore", "age_05", "body"),
        },
        new()
        {
            Id = "age_06",
            Title = Prose.Text("lore", "age_06", "title"),
            StorylineId = "age",
            Order = 6,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e9)),
            Body = Prose.Text("lore", "age_06", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Memory() =>
    [
        new()
        {
            Id = "memory_01",
            Title = Prose.Text("lore", "memory_01", "title"),
            StorylineId = "memory",
            Order = 1,
            Icon = "🗣️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = Prose.Text("lore", "memory_01", "body"),
        },
        new()
        {
            Id = "memory_02",
            Title = Prose.Text("lore", "memory_02", "title"),
            StorylineId = "memory",
            Order = 2,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(CultureModule.CounterKey, 40)),
            Body = Prose.Text("lore", "memory_02", "body"),
        },
        new()
        {
            Id = "memory_03",
            Title = Prose.Text("lore", "memory_03", "title"),
            StorylineId = "memory",
            Order = 3,
            Icon = "🔤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(CultureModule.CounterKey, 600)),
            Body = Prose.Text("lore", "memory_03", "body"),
        },
        new()
        {
            Id = "memory_04",
            Title = Prose.Text("lore", "memory_04", "title"),
            StorylineId = "memory",
            Order = 4,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(CultureModule.CounterKey, 2_000)),
            Body = Prose.Text("lore", "memory_04", "body"),
        },
        new()
        {
            Id = "memory_05",
            Title = Prose.Text("lore", "memory_05", "title"),
            StorylineId = "memory",
            Order = 5,
            Icon = "📑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(CultureModule.CounterKey, 6_000)),
            Body = Prose.Text("lore", "memory_05", "body"),
        },
        new()
        {
            Id = "memory_06",
            Title = Prose.Text("lore", "memory_06", "title"),
            StorylineId = "memory",
            Order = 6,
            Icon = "🗒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(CultureModule.CounterKey, 14_000)),
            Body = Prose.Text("lore", "memory_06", "body"),
        },
        new()
        {
            Id = "memory_07",
            Title = Prose.Text("lore", "memory_07", "title"),
            StorylineId = "memory",
            Order = 7,
            Icon = "🖋️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(CultureModule.CounterKey, 32_000)),
            Body = Prose.Text("lore", "memory_07", "body"),
        },
        new()
        {
            Id = "memory_08",
            Title = Prose.Text("lore", "memory_08", "title"),
            StorylineId = "memory",
            Order = 8,
            Icon = "🏚️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 80_000)),
            Body = Prose.Text("lore", "memory_08", "body"),
        },
        new()
        {
            Id = "memory_09",
            Title = Prose.Text("lore", "memory_09", "title"),
            StorylineId = "memory",
            Order = 9,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 180_000)),
            Body = Prose.Text("lore", "memory_09", "body"),
        },
        new()
        {
            Id = "memory_10",
            Title = Prose.Text("lore", "memory_10", "title"),
            StorylineId = "memory",
            Order = 10,
            Icon = "🕰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 420_000)),
            Body = Prose.Text("lore", "memory_10", "body"),
        },
        new()
        {
            Id = "memory_11",
            Title = Prose.Text("lore", "memory_11", "title"),
            StorylineId = "memory",
            Order = 11,
            Icon = "🌍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 900_000)),
            Body = Prose.Text("lore", "memory_11", "body"),
        },
        new()
        {
            Id = "memory_12",
            Title = Prose.Text("lore", "memory_12", "title"),
            StorylineId = "memory",
            Order = 12,
            Icon = "🌠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 3.5e6)),
            Body = Prose.Text("lore", "memory_12", "body"),
        },
    ];

    /// <summary>
    /// 文本文件里“有、但代码从不取用”的条目会在这里抛（孤儿文本）。
    /// 调用点是 <see cref="Prose"/> 那一套：包在 <c>Build()</c> 末尾调用它。
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

}
