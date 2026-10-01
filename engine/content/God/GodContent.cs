using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 内容包 #7《猫娘神明》的入口。<para>
/// 定位：<b>ROADMAP 阶段 5「换皮批产」的第一个包</b>，也是另外三个换皮包（#4 文明 / #5 赛博 /
/// #8 梦境）的形态样板。它只用了已经交付过的四样能力——<c>Era</c>（五套神话体系）、
/// <c>Lore</c>（四条线 40 条）、一个只涨不花的第二资源（信仰 / 在线人数）、三个结局——
/// <b>核心零改动</b>，这正是阶段 5 要检验的那条架构不变量（A3）。
/// </para>
/// <para>
/// 语气是<b>轻松搞笑的 meta</b>：神也要恰饭、开直播间、卖周边。
/// 转生语义是<b>「切换神话体系」</b>：家猫神 → 埃及猫神 → 希腊猫神 → 北欧猫神 → 克苏鲁猫，
/// 每换一套，世界重新揭示一遍（<c>InheritBuildingRatio</c> 全为 0，<b>不做继承</b>），
/// 按历史累计换取「神格」。
/// </para>
/// <para>
/// 第二资源是「信仰」（计数器键 <c>faith</c>）：<b>只涨不花、切换神话体系也不清零</b>，
/// 所以它可以进四层纪元的完成条件。它的量级被五层阶梯钉在一个包络里，
/// 所以本包的结局不挂在它上面，而是挂在"你读完了多少神话"上（见 <see cref="Endings"/>）。
/// </para>
/// </summary>
public static class GodContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘神明";

    /// <summary>第二资源「信仰」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string FaithCounterKey = FaithModule.CounterKey;

    /// <summary>「直播在线人数」的计数器键——记的是历史峰值，所以单调不减。</summary>
    public const string ViewerCounterKey = FaithModule.ViewerCounterKey;

    /// <summary>产出信仰的建筑标签（神殿类）。</summary>
    public const string TempleTag = FaithModule.TempleTag;

    /// <summary>贡献在线人数的建筑标签（直播间）。</summary>
    public const string StreamTag = FaithModule.StreamTag;

    /// <summary>信仰驱动产量时的封顶点数（每点 +0.01%，10 万点 = +1000%）。</summary>
    public const double FaithScalingCap = FaithModule.FaithScalingCap;

    /// <summary>每点信仰带来的全局产量加成（0.01%）。</summary>
    public const double FaithPercentPerPoint = FaithModule.FaithPercentPerPoint;

    /// <summary>每座神殿类建筑每秒带来的信仰。</summary>
    public const double FaithPerTemplePerSecond = FaithModule.FaithPerTemplePerSecond;

    /// <summary>每多少座建筑（含非神殿类）每秒带来 1 点信仰——香火不只来自神殿，也来自人气。</summary>
    public const double BuildingsPerFaithPerSecond = FaithModule.BuildingsPerFaithPerSecond;

    /// <summary>每多少点信仰折算 1 个"在线观众"。</summary>
    public const double FaithPerViewer = FaithModule.FaithPerViewer;

    /// <summary>每座直播间额外贡献的在线位。</summary>
    public const double ViewersPerStudio = FaithModule.ViewersPerStudio;

    /// <summary>「成为主神」结局要求的图鉴条数。</summary>
    public const double LoreForGod = Endings.LoreForGod;

    /// <summary>「变成 meme」结局要求的图鉴条数。</summary>
    public const double LoreForMeme = Endings.LoreForMeme;

    /// <summary>构建内容定义（含信仰模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("香火", "🕯️", "显灵")
            .WithPrestigeCurrency("神格", "👑")
            .WithBalance(BuildBalance())
            .Add(new FaithModule())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddEras(Eras.All(BuildBalance()))
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .AddEndings(Endings.All)
            .Build();

    /// <summary>
    /// 平衡参数：神明的手感是"事件来得勤、香火收得慢"。<para>
    /// 点击中等（显灵是现场演示），离线上限中等（神也得看着点），
    /// 但神迹来得比别的包密——五套体系都抢着显灵，谁不显灵谁掉粉。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 神迹来得勤：神也要冲业绩，五套体系的 KPI 是共享的。
        GoldenCookieMinDelay = 60 * 4,
        GoldenCookieMaxDelay = 60 * 12,
        GoldenCookieLifetime = 12,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 神也得看着点：离线 3 小时，按 65% 结算。
        // 第 4 层（北欧猫神）会把上限翻倍——英灵殿不打烊。
        OfflineCapSeconds = 10800,
        OfflineEfficiency = 0.65,
        MinimumOfflineSeconds = 60,

        // 转生除数按**本包的阶梯**标定，而不是照抄配方 1e12：
        // 目标是"最后一次结算落在 ~100 级"，让永久线（神格，总价 80）
        // 在一次自然游玩里就买得起。照抄 1e12 的话，纪元包的可结算历史累计
        // 被阶梯卡在 1e8~1e11，等级恒为 0 —— 整条神格线**结构上打不开**（ROADMAP 阶段 4C）。
        //
        // 这个数是量出来的，不是推出来的：机器人实测"切到第 5 套神话那一刻"的历史累计是
        // 5.49e15，反解 divisor = A / L³ 取 L ≈ 106 → 4.5e9（(5.49e15/4.5e9)^(1/3) = 106）。
        // 故意留出余量：永久线总价 80，而 106 ≥ 80 有三成富余，
        // 内容微调把包络压下去一点也不会让整条线突然变成死内容。
        PrestigeDivisor = 4.5e9,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 卖掉的供品就是真卖掉了：返还率压得比别的包低一点。
        DefaultSellRefundRate = 0.5,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
