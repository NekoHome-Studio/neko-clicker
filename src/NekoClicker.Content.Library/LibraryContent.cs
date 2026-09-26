using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 内容包 #9《猫娘图书馆》的入口。<para>
/// 定位：<b>「虚无化」唯一的用武之地</b>，也是 ROADMAP 阶段 4B 的落点。
/// 设计矩阵里 #9 需要 Era + Lore + 虚无化，所以它和末世一样没有立场轴——
/// 而它的两个结局也确实不需要表态：一个由"合上书的时候还有没有人在读"决定，
/// 一个是没有读者时的兜底。
/// </para>
/// <para>
/// <b>「虚无化」是怎么实现的</b>（ROADMAP §6.5 有完整决策记录）：
/// 被阅读度是一个 <see cref="IGameModule"/> 维护的计数器，按比例衰减、每次开新书清零；
/// 它通过已有的 <c>Scaling(ScalingSource.CustomCounter)</c> 接缝驱动产量乘数。
/// <b>核心零改动</b>——原本预留给 S-D 的 <c>DecaySystem</c> 与"ModifierResolver 第 6 个来源"
/// 都不需要：前者是内容侧的动态，后者已经被"自定义计数器成长"泛化掉了。
/// </para>
/// <para>
/// 转生语义是<b>「开新书」</b>：上一本书合上，新的世界开始，按历史累计换取「书签」。
/// 第二资源是「被阅读度」——<b>全项目唯一一个会自己掉下去的第二资源</b>。
/// </para>
/// </summary>
public static class LibraryContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘图书馆";

    /// <summary>第二资源「被阅读度」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string ReadershipCounterKey = ReadershipModule.CounterKey;

    /// <summary>产出被阅读度的建筑标签。</summary>
    public const string ReaderTag = ReadershipModule.ReaderTag;

    /// <summary>衰减时间常数（秒）；均衡点 = 读者类建筑数 × 这个数。</summary>
    public const double DecaySeconds = ReadershipModule.DecaySeconds;

    /// <summary>产量乘数的下限——被阅读度归零时仍有 ×0.5，永远不会归零到死锁。</summary>
    public const double MultiplierFloor = 0.5;

    /// <summary>产量乘数的上限。</summary>
    public const double MultiplierCeiling = 2.0;

    /// <summary>「被读到最后」结局要求的被阅读度门槛。</summary>
    public const double ReadershipForEnding = Endings.ReadershipForEnding;

    /// <summary>构建内容定义（含被阅读度模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("页", "📄", "提笔")
            .WithPrestigeCurrency("书签", "🔖")
            .WithBalance(BuildBalance())
            .Add(new ReadershipModule())
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
    /// 平衡参数：图书馆的手感是"写得慢、读得久"。<para>
    /// 点击比实验室强（她自己就是生产线），离线上限中等（闭馆之后没人翻书），
    /// 但事件来得比别的包稀——蠹虫不是每天都有，查禁也不是。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 蠹虫不常来，但来一次待得久；查禁的年份更是几天才一次。
        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 14,
        GoldenCookieLifetime = 14,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 闭馆之后没人翻书：离线 2 小时，按 60% 结算。
        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.6,
        MinimumOfflineSeconds = 60,

        PrestigeDivisor = 1e12,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 卖掉的藏书就是真卖掉了：返还率压得比别的包低。
        DefaultSellRefundRate = 0.45,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
