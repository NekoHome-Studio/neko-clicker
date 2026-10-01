using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几层</b>走：每迁一次服务器，故事往上推一段；四条线各自在一个很小的动作上开篇
/// （点击 1 / 25 / 100 / 400 次），免得图鉴一开就是一整墙 ???，也避免「开局十分钟放出六条」
/// （阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余六个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。
///     这里用的是「纪元门槛 + 层内里程碑」，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前迁服务器，这条永远读不到。
///     这个包的层内门槛用「本轮累计赚取」，并且每一层都比该层完成条件低一档；
///     由 <c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 与包内用例同时守着。</item>
/// </list>
/// </para>
/// <para>
/// <b>为什么「转生类条目放线尾」在这里指 <c>EraAtLeast</c> 而不是 <c>PrestigeLevelAtLeast</c></b>：
/// 这个包的转生动作（迁服务器）由<b>纪元闸门</b>决定时机，而不是玩家随手按的循环，
/// 所以「层号」本身就是一条单调的、跟剧情同步的时钟。转生等级反而会滞后（它只在迁服务器那一刻结算），
/// 拿它当骨架会让整条线挤在最后一次性倒出来。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Cyber"));

    /// <summary>本包的散文文件（随包复制到 content/Cyber/text.json）。取不到就抛，绝不回退成空白。</summary>
    private static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("her", 12),
        Line("server", 10),
        Line("echo", 10),
        Line("intrusion", 8),
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
        .. Server(),
        .. Echo(),
        .. Intrusion(),
    ];

    /// <summary>「她」线（12 条）：从醒来到成为根层上的那一个进程。</summary>
    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = Prose.Text("lore", "her_01", "title"),
            StorylineId = "her",
            Order = 1,
            Icon = "🐈",
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
            Icon = "🏷️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(18_400)),
            Body = Prose.Text("lore", "her_02", "body"),
        },
        new()
        {
            Id = "her_03",
            Title = Prose.Text("lore", "her_03", "title"),
            StorylineId = "her",
            Order = 3,
            Icon = "🛡️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(48_300)),
            Body = Prose.Text("lore", "her_03", "body"),
        },
        new()
        {
            Id = "her_04",
            Title = Prose.Text("lore", "her_04", "title"),
            StorylineId = "her",
            Order = 4,
            Icon = "📦",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(190_000)),
            Body = Prose.Text("lore", "her_04", "body"),
        },
        new()
        {
            Id = "her_05",
            Title = Prose.Text("lore", "her_05", "title"),
            StorylineId = "her",
            Order = 5,
            Icon = "🕸️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2_150_000)),
            Body = Prose.Text("lore", "her_05", "body"),
        },
        new()
        {
            Id = "her_06",
            Title = Prose.Text("lore", "her_06", "title"),
            StorylineId = "her",
            Order = 6,
            Icon = "☁️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4_700_000)),
            Body = Prose.Text("lore", "her_06", "body"),
        },
        new()
        {
            Id = "her_07",
            Title = Prose.Text("lore", "her_07", "title"),
            StorylineId = "her",
            Order = 7,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.9e7)),
            Body = Prose.Text("lore", "her_07", "body"),
        },
        new()
        {
            Id = "her_08",
            Title = Prose.Text("lore", "her_08", "title"),
            StorylineId = "her",
            Order = 8,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.9e8)),
            Body = Prose.Text("lore", "her_08", "body"),
        },
        new()
        {
            Id = "her_09",
            Title = Prose.Text("lore", "her_09", "title"),
            StorylineId = "her",
            Order = 9,
            Icon = "⌫",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(3.2e9)),
            Body = Prose.Text("lore", "her_09", "body"),
        },
        new()
        {
            Id = "her_10",
            Title = Prose.Text("lore", "her_10", "title"),
            StorylineId = "her",
            Order = 10,
            Icon = "🗼",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.2e9)),
            Body = Prose.Text("lore", "her_10", "body"),
        },
        new()
        {
            Id = "her_11",
            Title = Prose.Text("lore", "her_11", "title"),
            StorylineId = "her",
            Order = 11,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e10)),
            Body = Prose.Text("lore", "her_11", "body"),
        },
        new()
        {
            Id = "her_12",
            Title = Prose.Text("lore", "her_12", "title"),
            StorylineId = "her",
            Order = 12,
            Icon = "🔌",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.6e10)),
            Body = Prose.Text("lore", "her_12", "body"),
        },
    ];

    /// <summary>「服务器」线（10 条）：她脚下那五台机器的故事。</summary>
    private static IEnumerable<LoreEntry> Server() =>
    [
        new()
        {
            Id = "server_01",
            Title = Prose.Text("lore", "server_01", "title"),
            StorylineId = "server",
            Order = 1,
            Icon = "⚙️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = Prose.Text("lore", "server_01", "body"),
        },
        new()
        {
            Id = "server_02",
            Title = Prose.Text("lore", "server_02", "title"),
            StorylineId = "server",
            Order = 2,
            Icon = "📦",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(76_500)),
            Body = Prose.Text("lore", "server_02", "body"),
        },
        new()
        {
            Id = "server_03",
            Title = Prose.Text("lore", "server_03", "title"),
            StorylineId = "server",
            Order = 3,
            Icon = "🏢",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(268_000)),
            Body = Prose.Text("lore", "server_03", "body"),
        },
        new()
        {
            Id = "server_04",
            Title = Prose.Text("lore", "server_04", "title"),
            StorylineId = "server",
            Order = 4,
            Icon = "☁️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(3_040_000)),
            Body = Prose.Text("lore", "server_04", "body"),
        },
        new()
        {
            Id = "server_05",
            Title = Prose.Text("lore", "server_05", "title"),
            StorylineId = "server",
            Order = 5,
            Icon = "🌙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7_800_000)),
            Body = Prose.Text("lore", "server_05", "body"),
        },
        new()
        {
            Id = "server_06",
            Title = Prose.Text("lore", "server_06", "title"),
            StorylineId = "server",
            Order = 6,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4.4e7)),
            Body = Prose.Text("lore", "server_06", "body"),
        },
        new()
        {
            Id = "server_07",
            Title = Prose.Text("lore", "server_07", "title"),
            StorylineId = "server",
            Order = 7,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(6.6e8)),
            Body = Prose.Text("lore", "server_07", "body"),
        },
        new()
        {
            Id = "server_08",
            Title = Prose.Text("lore", "server_08", "title"),
            StorylineId = "server",
            Order = 8,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.8e9)),
            Body = Prose.Text("lore", "server_08", "body"),
        },
        new()
        {
            Id = "server_09",
            Title = Prose.Text("lore", "server_09", "title"),
            StorylineId = "server",
            Order = 9,
            Icon = "♻️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.4e9)),
            Body = Prose.Text("lore", "server_09", "body"),
        },
        new()
        {
            Id = "server_10",
            Title = Prose.Text("lore", "server_10", "title"),
            StorylineId = "server",
            Order = 10,
            Icon = "✍️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.2e10)),
            Body = Prose.Text("lore", "server_10", "body"),
        },
    ];

    /// <summary>「数据残影」线（10 条）：主人留下的东西，一条一条被算出来。</summary>
    private static IEnumerable<LoreEntry> Echo() =>
    [
        new()
        {
            Id = "echo_01",
            Title = Prose.Text("lore", "echo_01", "title"),
            StorylineId = "echo",
            Order = 1,
            Icon = "🌙",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = Prose.Text("lore", "echo_01", "body"),
        },
        new()
        {
            Id = "echo_02",
            Title = Prose.Text("lore", "echo_02", "title"),
            StorylineId = "echo",
            Order = 2,
            Icon = "✒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(31_200)),
            Body = Prose.Text("lore", "echo_02", "body"),
        },
        new()
        {
            Id = "echo_03",
            Title = Prose.Text("lore", "echo_03", "title"),
            StorylineId = "echo",
            Order = 3,
            Icon = "🗑️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(84_700)),
            Body = Prose.Text("lore", "echo_03", "body"),
        },
        new()
        {
            Id = "echo_04",
            Title = Prose.Text("lore", "echo_04", "title"),
            StorylineId = "echo",
            Order = 4,
            Icon = "🖼️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(420_000)),
            Body = Prose.Text("lore", "echo_04", "body"),
        },
        new()
        {
            Id = "echo_05",
            Title = Prose.Text("lore", "echo_05", "title"),
            StorylineId = "echo",
            Order = 5,
            Icon = "🔍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4_100_000)),
            Body = Prose.Text("lore", "echo_05", "body"),
        },
        new()
        {
            Id = "echo_06",
            Title = Prose.Text("lore", "echo_06", "title"),
            StorylineId = "echo",
            Order = 6,
            Icon = "🕰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9_600_000)),
            Body = Prose.Text("lore", "echo_06", "body"),
        },
        new()
        {
            Id = "echo_07",
            Title = Prose.Text("lore", "echo_07", "title"),
            StorylineId = "echo",
            Order = 7,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5.8e7)),
            Body = Prose.Text("lore", "echo_07", "body"),
        },
        new()
        {
            Id = "echo_08",
            Title = Prose.Text("lore", "echo_08", "title"),
            StorylineId = "echo",
            Order = 8,
            Icon = "🗄️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.3e9)),
            Body = Prose.Text("lore", "echo_08", "body"),
        },
        new()
        {
            Id = "echo_09",
            Title = Prose.Text("lore", "echo_09", "title"),
            StorylineId = "echo",
            Order = 9,
            Icon = "🧮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(7.4e9),
                UnlockCondition.Counter(ComputeModule.CounterKey, 35_000)),
            Body = Prose.Text("lore", "echo_09", "body"),
        },
        new()
        {
            Id = "echo_10",
            Title = Prose.Text("lore", "echo_10", "title"),
            StorylineId = "echo",
            Order = 10,
            Icon = "✨",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e10),
                UnlockCondition.Counter(ComputeModule.CounterKey, 900_000)),
            Body = Prose.Text("lore", "echo_10", "body"),
        },
    ];

    /// <summary>「入侵」线（8 条）：敲门的东西。</summary>
    private static IEnumerable<LoreEntry> Intrusion() =>
    [
        new()
        {
            Id = "intrusion_01",
            Title = Prose.Text("lore", "intrusion_01", "title"),
            StorylineId = "intrusion",
            Order = 1,
            Icon = "🦠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(1),
                UnlockCondition.ClicksAtLeast(400)),
            Body = Prose.Text("lore", "intrusion_01", "body"),
        },
        new()
        {
            Id = "intrusion_02",
            Title = Prose.Text("lore", "intrusion_02", "title"),
            StorylineId = "intrusion",
            Order = 2,
            Icon = "🧬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(4),
                UnlockCondition.EraAtLeast(2)),
            Body = Prose.Text("lore", "intrusion_02", "body"),
        },
        new()
        {
            Id = "intrusion_03",
            Title = Prose.Text("lore", "intrusion_03", "title"),
            StorylineId = "intrusion",
            Order = 3,
            Icon = "⛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(10),
                UnlockCondition.EraAtLeast(2)),
            Body = Prose.Text("lore", "intrusion_03", "body"),
        },
        new()
        {
            Id = "intrusion_04",
            Title = Prose.Text("lore", "intrusion_04", "title"),
            StorylineId = "intrusion",
            Order = 4,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(20),
                UnlockCondition.EraAtLeast(3)),
            Body = Prose.Text("lore", "intrusion_04", "body"),
        },
        new()
        {
            Id = "intrusion_05",
            Title = Prose.Text("lore", "intrusion_05", "title"),
            StorylineId = "intrusion",
            Order = 5,
            Icon = "🎩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(32),
                UnlockCondition.EraAtLeast(3)),
            Body = Prose.Text("lore", "intrusion_05", "body"),
        },
        new()
        {
            Id = "intrusion_06",
            Title = Prose.Text("lore", "intrusion_06", "title"),
            StorylineId = "intrusion",
            Order = 6,
            Icon = "🧹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(44),
                UnlockCondition.EraAtLeast(4)),
            Body = Prose.Text("lore", "intrusion_06", "body"),
        },
        new()
        {
            Id = "intrusion_07",
            Title = Prose.Text("lore", "intrusion_07", "title"),
            StorylineId = "intrusion",
            Order = 7,
            Icon = "✉️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(56),
                UnlockCondition.EraAtLeast(4)),
            Body = Prose.Text("lore", "intrusion_07", "body"),
        },
        new()
        {
            Id = "intrusion_08",
            Title = Prose.Text("lore", "intrusion_08", "title"),
            StorylineId = "intrusion",
            Order = 8,
            Icon = "🛡️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(70),
                UnlockCondition.EraAtLeast(5)),
            Body = Prose.Text("lore", "intrusion_08", "body"),
        },
    ];

    /// <summary>
    /// 文本文件里“有、但代码从不取用”的条目会在这里抛（孤儿文本）。
    /// 调用点是 <see cref="Prose"/> 那一套：包在 <c>Build()</c> 末尾调用它。
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

}
