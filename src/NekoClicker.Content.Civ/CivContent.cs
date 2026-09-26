using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 内容包 #4《猫娘文明》的入口。<para>
/// 定位：<b>从一只猫窝走到星港</b>——一次"她一个人把文明重跑了一遍"的文明演进，
/// 也是 ROADMAP 阶段 5 的换皮批产之一。设计矩阵里 #4 需要 Era + Lore + 一个计数器，
/// 所以它和末世、图书馆一样<b>没有立场轴与表态</b>（<c>Stances</c> 与 <c>Choices</c> 都为空）：
/// 它的三个结局确实不需要玩家表过态——区分它们的是"你走到了哪里、又记住了多少"。
/// </para>
/// <para>
/// <b>这个包由什么构成</b>：9 座建筑（猫窝 → 村庄 → 城墙 → 集市 → 学院 → 神殿 → 星港 → 灵桥 → 深空中继）、
/// 5 层纪元（石堆 / 村庄 / 城墙 / 学院 / 星港，每层换一套 <see cref="GameBalance"/> 与常驻倍率）、
/// 「天灾」（金猫换皮：洪水、瘟疫、长冬、陨石，以及丰收年与技术突破）、
/// 第二资源「文化」（<see cref="CultureModule"/>），和三个结局。
/// </para>
/// <para>
/// <b>核心零改动</b>：文化是一个 <c>IGameModule</c> 维护的单调计数器，
/// 它对产量的影响走已有的 <c>Scaling(ScalingSource.CustomCounter)</c> 接缝，
/// 它对内容的门控走已有的 <c>UnlockCondition.Counter</c>。没有为这个包加过任何核心能力。
/// </para>
/// <para>
/// 转生语义是<b>「跨入下一个时代」</b>：上一段文明落幕，新的时代从头开始，按历史累计换取「火种」。
/// 建筑与货币清零，<b>只有文化不清零</b>——时代可以重来，记得住的东西不会。
/// </para>
/// </summary>
public static class CivContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘文明";

    /// <summary>第二资源「文化」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string CultureCounterKey = CultureModule.CounterKey;

    /// <summary>产出「文化」的建筑标签。</summary>
    public const string RecorderTag = CultureModule.RecorderTag;

    /// <summary>每座记录者建筑每秒的文化产率（按建筑 id；未列出的按兜底值 1）。</summary>
    public static IReadOnlyDictionary<string, double> CulturePerBuilding => CultureModule.CulturePerBuilding;

    /// <summary>文化对全局产量的每点加成（0.00002 = 每点 +0.002%）。</summary>
    public const double CulturePercentPerPoint = 0.00002;

    /// <summary>文化加成的量程：到 400,000 点封顶（+800%）。<c>Cap</c> 限的是原始计数值。</summary>
    public const double CultureScalingCap = 400_000;

    /// <summary>「星际文明」结局要求的文化门槛。</summary>
    public const double CultureForInterstellar = Endings.CultureForInterstellar;

    /// <summary>「自我毁灭」结局要求的成就数门槛。</summary>
    public const double AchievementsForSelfDestruction = Endings.AchievementsForSelfDestruction;

    /// <summary>构建内容定义（含文化模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("产能", "🪨", "拍")
            .WithPrestigeCurrency("火种", "🔥")
            .WithBalance(BuildBalance())
            .Add(new CultureModule())
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
    /// 平衡参数：文明的手感是"慢、厚、跨得过去"。<para>
    /// 这个包的基准档刻意靠近图书馆（点击中等、离线上限 2 小时、事件 5~14 分钟一次）——
    /// 真正换手感的不是这里，而是<b>每一层自己的 <c>Balance</c></b>：
    /// 第 1 层的点击是 2.5 倍强、第 5 层的离线上限是 3 倍。
    /// </para>
    /// <para>
    /// <b>转生除数按本包的阶梯标定</b>（<c>PrestigeDivisor = 1e5</c>），而不是照抄配方 1e12：
    /// 目标是"最后一次结算落在 ~100 级"，让遗产线的总价（21 点火种）
    /// 在一次自然游玩里就买得起。照抄 1e12 的话，纪元包的可结算历史累计被阶梯
    /// 卡在 1e8~1e11，等级恒为 0——整条遗产线<b>结构上打不开</b>（阶段 4C 的真实缺陷）。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1.5,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 天灾不是每天都有：洪水看年景，瘟疫看运气，陨石看天。
        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 14,
        GoldenCookieLifetime = 13,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 夜里没人干活：离线 2 小时，按 60% 结算；学院时代起上限翻倍、星港时代 ×3。
        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.6,
        MinimumOfflineSeconds = 60,

        // 见方法注释：除数必须与本包自己的阶梯一起标定。
        PrestigeDivisor = 1e5,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 拆掉自己盖的房子是亏的——她记得每一块石头是怎么搬上去的。
        DefaultSellRefundRate = 0.45,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
