using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>重启次数</b>走：每重启一次，故事往前推一段；四条线各自在很小的动作上开篇
/// （点击 1 / 25 / 100 / 400 次），免得图鉴一开就是一整墙 ???，也避免「开局十分钟放出六条」。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1~#3、#10 相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁，
///     阶段 2.6 实测九命 46 条里有 33 条撞车、进第 5 命一次放出 6 条，全是这么来的。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现「第 3 条还锁着，第 4 条已亮」。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前重启走人，这条永远读不到。
///     末世尤其要注意：<c>CookiesEarnedThisRun</c> 在重启时归零，所以门槛必须落在
///     <b>单层之内</b>够得到的量级上，不能拿「多次重启累计」当条件（那是 <c>EarnedAllTime</c> 的活）。</item>
/// </list>
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Apocalypse"));

    /// <summary>本包的散文文件（随包复制到 content/Apocalypse/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线 + 一条<b>纸条线</b>（纸条是"别人留下的东西"，与剧情分开成一条）。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("her", 12),
        Line("ruin", 10),
        Line("echo", 10),
        Line("seed", 8),
        Line("note", 2),
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
        .. Ruin(),
        .. Echo(),
        .. Seed(),
        .. Notes(),
    ];

    /// <summary>
    /// 纸条（<see cref="LoreChannel.Note"/>）：**在虚构里教机制**的那一类。<para>
    /// 与剧情分开的三个理由：① 它的读者是刚上手的人，不是追故事的人；
    /// ② 它的呈现是"捡到一张纸"，不是"游戏在跟你说话"（皮由通道决定）；
    /// ③ 它一张只讲一件事，所以门槛是"第一次遇到那个情况"，而不是剧情那种量级门槛。
    /// 方案与目录见 <c>engine/docs/FOUND_NOTES_PLAN.md</c> §4。
    /// </para>
    /// <para>
    /// <b>门槛一律挑"第一次成立"的那一个瞬间</b>：引擎每秒扫一次，成立就锁存
    /// （<c>LoreSystem.Check</c> 里 `LoreUnlocked` 那一句），所以"买得起"这种会退回去的
    /// 条件照样能用——它只需要在某一刻为真。
    /// </para>
    /// </summary>
    private static IEnumerable<LoreEntry> Notes() =>
    [
        new()
        {
            Id = "note_buy",
            Title = Prose.Text("lore", "note_buy", "title"),
            StorylineId = "note",
            Order = 1,
            Icon = "📝",
            Channel = LoreChannel.Note,
            // 第一次**赚够**最便宜那座的价钱——不是"买下了"，而是"买得起了"：
            // 纸条要在玩家还没动手之前就到手，讲的是"这些东西领回来会自己下矿"。
            //
            // 为什么用"本轮累计赚到"而不是"手上现有"：`Cookies` 会被花掉，
            // 而购买是**在买得起的那一刻立刻发生**的——"手上曾经到过这个数"在一秒一次的
            // 检查里很容易整段错过（`ApocalypseContentTests.Storylines_ReadInOrderDuringARealPlaythrough`
            // 第一次跑就是这么红的）。`CookiesEarnedThisRun` 单调递增，条件一旦成立就锁存，
            // 所以它既能对上"第一次买得起"这个语义，又不会被采样错过。
            Reveal = UnlockCondition.EarnedThisRunAtLeast(FirstBuildingPrice),
            Body = Prose.Text("lore", "note_buy", "body"),
        },
        new()
        {
            Id = "note_cat",
            Title = Prose.Text("lore", "note_cat", "title"),
            StorylineId = "note",
            Order = 2,
            Icon = "🪟",
            Channel = LoreChannel.Note,
            // 第一次**真的有金猫出现在场上**：这是这个机制唯一的可判定信号
            // （引擎刷出时自己写的计数器，见 GoldenCookieSystem.SerialCounterKey）。
            // 它成立的那一刻场上正站着一只限时的猫，所以界面上必须**让位给猫**——
            // 那条规则写在 Web 宿主的纸条 sheet 里（FOUND_NOTES_PLAN §5.4）。
            //
            // 为什么要**与第一张的条件取交**、而不是光看计数器：
            // 纸条是一条线，而图鉴里同一线的序号必须与玩家真正遇到的先后一致
            // （同包那条守卫 `Storylines_ReadInOrderDuringARealPlaythrough` 说的就是这件事）。
            // 金猫最早 48 秒就来了，而"赚够一座建筑的钱"可能来得更晚——
            // 两个条件各自成立时谁先谁后**取决于玩家**，那条守卫会在真实游玩里逮到倒挂。
            // 取交之后，"学会买东西"永远不晚于"知道金猫"，顺序由构造保证，不靠运气。
            Reveal = UnlockCondition.All(
                UnlockCondition.Counter(GoldenCookieSystem.SerialCounterKey, 1),
                UnlockCondition.EarnedThisRunAtLeast(FirstBuildingPrice)),
            Body = Prose.Text("lore", "note_cat", "body"),
        },
    ];

    /// <summary>
    /// 第一座建筑（<c>ruins</c>）的标价：两张入门纸条都用它当门槛——
    /// "第一次赚够这么多"就是"第一次买得起一座"。<para>
    /// 按 id 取而不是按下标：建筑表重排时下标会悄悄换一座，而 id 不会。
    /// </para>
    /// </summary>
    private static double FirstBuildingPrice
        => Buildings.All.First(building => string.Equals(building.Id, "ruins", StringComparison.Ordinal)).BasePrice;

    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = Prose.Text("lore", "her_01", "title"),
            StorylineId = "her",
            Order = 1,
            Icon = "🕯️",
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
            Icon = "💧",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(2e5)),
            Body = Prose.Text("lore", "her_02", "body"),
        },
        new()
        {
            Id = "her_03",
            Title = Prose.Text("lore", "her_03", "title"),
            StorylineId = "her",
            Order = 3,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(2e6)),
            Body = Prose.Text("lore", "her_03", "body"),
        },
        new()
        {
            Id = "her_04",
            Title = Prose.Text("lore", "her_04", "title"),
            StorylineId = "her",
            Order = 4,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(5e6)),
            Body = Prose.Text("lore", "her_04", "body"),
        },
        new()
        {
            Id = "her_05",
            Title = Prose.Text("lore", "her_05", "title"),
            StorylineId = "her",
            Order = 5,
            Icon = "📝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(4e7)),
            Body = Prose.Text("lore", "her_05", "body"),
        },
        new()
        {
            Id = "her_06",
            Title = Prose.Text("lore", "her_06", "title"),
            StorylineId = "her",
            Order = 6,
            Icon = "🔁",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(1e8)),
            Body = Prose.Text("lore", "her_06", "body"),
        },
        new()
        {
            Id = "her_07",
            Title = Prose.Text("lore", "her_07", "title"),
            StorylineId = "her",
            Order = 7,
            Icon = "🛖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(8e8)),
            Body = Prose.Text("lore", "her_07", "body"),
        },
        new()
        {
            Id = "her_08",
            Title = Prose.Text("lore", "her_08", "title"),
            StorylineId = "her",
            Order = 8,
            Icon = "🏕️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(6e9)),
            Body = Prose.Text("lore", "her_08", "body"),
        },
        new()
        {
            Id = "her_09",
            Title = Prose.Text("lore", "her_09", "title"),
            StorylineId = "her",
            Order = 9,
            Icon = "🧬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(2e10)),
            Body = Prose.Text("lore", "her_09", "body"),
        },
        new()
        {
            Id = "her_10",
            Title = Prose.Text("lore", "her_10", "title"),
            StorylineId = "her",
            Order = 10,
            Icon = "🕸️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(5e10)),
            Body = Prose.Text("lore", "her_10", "body"),
        },
        new()
        {
            Id = "her_11",
            Title = Prose.Text("lore", "her_11", "title"),
            StorylineId = "her",
            Order = 11,
            Icon = "🫥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1e11)),
            Body = Prose.Text("lore", "her_11", "body"),
        },
        new()
        {
            Id = "her_12",
            Title = Prose.Text("lore", "her_12", "title"),
            StorylineId = "her",
            Order = 12,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1.4e11)),
            Body = Prose.Text("lore", "her_12", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Ruin() =>
    [
        new()
        {
            Id = "ruin_01",
            Title = Prose.Text("lore", "ruin_01", "title"),
            StorylineId = "ruin",
            Order = 1,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedAllTimeAtLeast(30),
            Body = Prose.Text("lore", "ruin_01", "body"),
        },
        new()
        {
            Id = "ruin_02",
            Title = Prose.Text("lore", "ruin_02", "title"),
            StorylineId = "ruin",
            Order = 2,
            Icon = "📉",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(5e4)),
            Body = Prose.Text("lore", "ruin_02", "body"),
        },
        new()
        {
            Id = "ruin_03",
            Title = Prose.Text("lore", "ruin_03", "title"),
            StorylineId = "ruin",
            Order = 3,
            Icon = "📋",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(5e5)),
            Body = Prose.Text("lore", "ruin_03", "body"),
        },
        new()
        {
            Id = "ruin_04",
            Title = Prose.Text("lore", "ruin_04", "title"),
            StorylineId = "ruin",
            Order = 4,
            Icon = "💧",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(1e6)),
            Body = Prose.Text("lore", "ruin_04", "body"),
        },
        new()
        {
            Id = "ruin_05",
            Title = Prose.Text("lore", "ruin_05", "title"),
            StorylineId = "ruin",
            Order = 5,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(1e7)),
            Body = Prose.Text("lore", "ruin_05", "body"),
        },
        new()
        {
            Id = "ruin_06",
            Title = Prose.Text("lore", "ruin_06", "title"),
            StorylineId = "ruin",
            Order = 6,
            Icon = "🌱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(2e8)),
            Body = Prose.Text("lore", "ruin_06", "body"),
        },
        new()
        {
            Id = "ruin_07",
            Title = Prose.Text("lore", "ruin_07", "title"),
            StorylineId = "ruin",
            Order = 7,
            Icon = "✉️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(4e8)),
            Body = Prose.Text("lore", "ruin_07", "body"),
        },
        new()
        {
            Id = "ruin_08",
            Title = Prose.Text("lore", "ruin_08", "title"),
            StorylineId = "ruin",
            Order = 8,
            Icon = "⛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(2e9)),
            Body = Prose.Text("lore", "ruin_08", "body"),
        },
        new()
        {
            Id = "ruin_09",
            Title = Prose.Text("lore", "ruin_09", "title"),
            StorylineId = "ruin",
            Order = 9,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(3e10)),
            Body = Prose.Text("lore", "ruin_09", "body"),
        },
        new()
        {
            Id = "ruin_10",
            Title = Prose.Text("lore", "ruin_10", "title"),
            StorylineId = "ruin",
            Order = 10,
            Icon = "🛰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1.2e11)),
            Body = Prose.Text("lore", "ruin_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Echo() =>
    [
        new()
        {
            Id = "echo_01",
            Title = Prose.Text("lore", "echo_01", "title"),
            StorylineId = "echo",
            Order = 1,
            Icon = "🔁",
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
            Icon = "🤲",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.Counter(ShardsModule.CounterKey, 500)),
            Body = Prose.Text("lore", "echo_02", "body"),
        },
        new()
        {
            Id = "echo_03",
            Title = Prose.Text("lore", "echo_03", "title"),
            StorylineId = "echo",
            Order = 3,
            Icon = "💡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.Counter(ShardsModule.CounterKey, 3000)),
            Body = Prose.Text("lore", "echo_03", "body"),
        },
        new()
        {
            Id = "echo_04",
            Title = Prose.Text("lore", "echo_04", "title"),
            StorylineId = "echo",
            Order = 4,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.Counter(ShardsModule.CounterKey, 1e4)),
            Body = Prose.Text("lore", "echo_04", "body"),
        },
        new()
        {
            Id = "echo_05",
            Title = Prose.Text("lore", "echo_05", "title"),
            StorylineId = "echo",
            Order = 5,
            Icon = "🗄️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.Counter(ShardsModule.CounterKey, 3e4)),
            Body = Prose.Text("lore", "echo_05", "body"),
        },
        new()
        {
            Id = "echo_06",
            Title = Prose.Text("lore", "echo_06", "title"),
            StorylineId = "echo",
            Order = 6,
            Icon = "🏕️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 5e4)),
            Body = Prose.Text("lore", "echo_06", "body"),
        },
        new()
        {
            Id = "echo_07",
            Title = Prose.Text("lore", "echo_07", "title"),
            StorylineId = "echo",
            Order = 7,
            Icon = "⚖️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 8e4)),
            Body = Prose.Text("lore", "echo_07", "body"),
        },
        new()
        {
            Id = "echo_08",
            Title = Prose.Text("lore", "echo_08", "title"),
            StorylineId = "echo",
            Order = 8,
            Icon = "🕸️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 1.1e5)),
            Body = Prose.Text("lore", "echo_08", "body"),
        },
        new()
        {
            Id = "echo_09",
            Title = Prose.Text("lore", "echo_09", "title"),
            StorylineId = "echo",
            Order = 9,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.Counter(ShardsModule.CounterKey, 1.3e5)),
            Body = Prose.Text("lore", "echo_09", "body"),
        },
        new()
        {
            Id = "echo_10",
            Title = Prose.Text("lore", "echo_10", "title"),
            StorylineId = "echo",
            Order = 10,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.Counter(ShardsModule.CounterKey, 1.6e5)),
            Body = Prose.Text("lore", "echo_10", "body"),
        },
    ];

    private static IEnumerable<LoreEntry> Seed() =>
    [
        new()
        {
            Id = "seed_01",
            Title = Prose.Text("lore", "seed_01", "title"),
            StorylineId = "seed",
            Order = 1,
            Icon = "🌱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(400),
            Body = Prose.Text("lore", "seed_01", "body"),
        },
        new()
        {
            Id = "seed_02",
            Title = Prose.Text("lore", "seed_02", "title"),
            StorylineId = "seed",
            Order = 2,
            Icon = "🛖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(12),
            Body = Prose.Text("lore", "seed_02", "body"),
        },
        new()
        {
            Id = "seed_03",
            Title = Prose.Text("lore", "seed_03", "title"),
            StorylineId = "seed",
            Order = 3,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(16),
            Body = Prose.Text("lore", "seed_03", "body"),
        },
        new()
        {
            Id = "seed_04",
            Title = Prose.Text("lore", "seed_04", "title"),
            StorylineId = "seed",
            Order = 4,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.AchievementsAtLeast(20)),
            Body = Prose.Text("lore", "seed_04", "body"),
        },
        new()
        {
            Id = "seed_05",
            Title = Prose.Text("lore", "seed_05", "title"),
            StorylineId = "seed",
            Order = 5,
            Icon = "📄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.AchievementsAtLeast(24)),
            Body = Prose.Text("lore", "seed_05", "body"),
        },
        new()
        {
            Id = "seed_06",
            Title = Prose.Text("lore", "seed_06", "title"),
            StorylineId = "seed",
            Order = 6,
            Icon = "🕸️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.AchievementsAtLeast(28)),
            Body = Prose.Text("lore", "seed_06", "body"),
        },
        new()
        {
            Id = "seed_07",
            Title = Prose.Text("lore", "seed_07", "title"),
            StorylineId = "seed",
            Order = 7,
            Icon = "🗣️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.AchievementsAtLeast(32)),
            Body = Prose.Text("lore", "seed_07", "body"),
        },
        new()
        {
            Id = "seed_08",
            Title = Prose.Text("lore", "seed_08", "title"),
            StorylineId = "seed",
            Order = 8,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.AchievementsAtLeast(34)),
            Body = Prose.Text("lore", "seed_08", "body"),
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
