using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 叙事条目：40 条，分四条线（C1《猫娘电台》，1 层 MVP）。<para>
/// 节奏跟着<b>累计信号</b>走：四条线各自在很小的动作上开篇，免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出六条"。因为这是 1 层包，门槛<b>不用 EraAtLeast</b>，
/// 全部用本轮累计 / 点击 / 建筑数这类单调或可达指标。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余包相同）：任何两条的 Reveal 不得相同；同一条线内门槛随 Order 单调递增；
/// 门槛一律 ≤ 本层完成门槛。「static」线讲的就是"为什么点击是干扰"——它是这个包反转核心动词的叙事落点。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Radio"));

    /// <summary>本包的散文文件（随包复制到 content/Radio/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("dj", 12),
        Line("signal", 10),
        Line("listener", 10),
        Line("static", 8),
    ];

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
        .. Dj(),
        .. Signal(),
        .. Listener(),
        .. Static(),
    ];

    /// <summary>条目工厂：标题/正文来自 text.json，门槛与序号在代码里（它们是纪律，不是散文）。</summary>
    private static LoreEntry Entry(string id, string line, int order, string icon, LoreChannel channel, UnlockCondition reveal)
        => new()
        {
            Id = id,
            Title = Prose.Text("lore", id, "title"),
            StorylineId = line,
            Order = order,
            Icon = icon,
            Channel = channel,
            Reveal = reveal,
            Body = Prose.Text("lore", id, "body"),
        };

    private static IEnumerable<LoreEntry> Dj() =>
    [
        Entry("dj_01", "dj", 1, "🎧", LoreChannel.Popup, UnlockCondition.ClicksAtLeast(1)),
        Entry("dj_02", "dj", 2, "🎙️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(200)),
        Entry("dj_03", "dj", 3, "📻", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(2_000)),
        Entry("dj_04", "dj", 4, "🌙", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(20_000)),
        Entry("dj_05", "dj", 5, "☕", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(150_000)),
        Entry("dj_06", "dj", 6, "🌆", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(1_000_000)),
        Entry("dj_07", "dj", 7, "🌃", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(8_000_000)),
        Entry("dj_08", "dj", 8, "🌌", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(50_000_000)),
        Entry("dj_09", "dj", 9, "🌠", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(300_000_000)),
        Entry("dj_10", "dj", 10, "🛰️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(2_000_000_000)),
        Entry("dj_11", "dj", 11, "🌟", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(12_000_000_000)),
        Entry("dj_12", "dj", 12, "👑", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(80_000_000_000)),
    ];

    private static IEnumerable<LoreEntry> Signal() =>
    [
        Entry("signal_01", "signal", 1, "📶", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(500)),
        Entry("signal_02", "signal", 2, "🔊", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(5_000)),
        Entry("signal_03", "signal", 3, "🎚️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(50_000)),
        Entry("signal_04", "signal", 4, "🎛️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(400_000)),
        Entry("signal_05", "signal", 5, "📡", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(3_000_000)),
        Entry("signal_06", "signal", 6, "🌐", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(25_000_000)),
        Entry("signal_07", "signal", 7, "🔆", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(180_000_000)),
        Entry("signal_08", "signal", 8, "⚡", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(1_200_000_000)),
        Entry("signal_09", "signal", 9, "🌈", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(9_000_000_000)),
        Entry("signal_10", "signal", 10, "✨", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(60_000_000_000)),
    ];

    private static IEnumerable<LoreEntry> Listener() =>
    [
        Entry("listener_01", "listener", 1, "👂", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(1_000)),
        Entry("listener_02", "listener", 2, "🐈", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(10_000)),
        Entry("listener_03", "listener", 3, "🏠", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(90_000)),
        Entry("listener_04", "listener", 4, "🌆", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(700_000)),
        Entry("listener_05", "listener", 5, "🚗", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(6_000_000)),
        Entry("listener_06", "listener", 6, "🌃", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(45_000_000)),
        Entry("listener_07", "listener", 7, "🌍", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(320_000_000)),
        Entry("listener_08", "listener", 8, "🌙", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(2_400_000_000)),
        Entry("listener_09", "listener", 9, "💫", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(16_000_000_000)),
        Entry("listener_10", "listener", 10, "🌌", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(95_000_000_000)),
    ];

    private static IEnumerable<LoreEntry> Static() =>
    [
        Entry("static_01", "static", 1, "📉", LoreChannel.Codex, UnlockCondition.ClicksAtLeast(30)),
        Entry("static_02", "static", 2, "🔇", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(30_000)),
        Entry("static_03", "static", 3, "🌫️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(250_000)),
        Entry("static_04", "static", 4, "🌪️", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(2_000_000)),
        Entry("static_05", "static", 5, "🌊", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(15_000_000)),
        Entry("static_06", "static", 6, "🌑", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(110_000_000)),
        Entry("static_07", "static", 7, "🌒", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(800_000_000)),
        Entry("static_08", "static", 8, "🌓", LoreChannel.Codex, UnlockCondition.EarnedThisRunAtLeast(40_000_000_000)),
    ];

    /// <summary>孤儿检查：text.json 里若有代码从不取用的条目，在这里当场抛。</summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();
}
