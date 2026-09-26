using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 内容包 #6《猫娘末世》的入口。<para>
/// 定位：<b>第一个用「跨转生继承」的包</b>。前面四个包的 <c>ResetRun</c> 语义都是"全清"，
/// 末世要的是"重启文明，但保留上纪元的猫娘"——<see cref="EraDefinition.InheritBuildingRatio"/>
/// 在阶段 1 就位，这里是它的第一次实物验证。
/// </para>
/// <para>
/// 它同时是<b>唯一没有立场轴的包</b>（设计矩阵里 #6 只需要 Era + Lore + 继承）：
/// 三个结局不是"你表过什么态"决定的，而是"你究竟记住了多少"决定的。
/// 同一套 <c>EndingDefinition</c> 既能表达立场驱动的结局、也能表达记忆驱动的结局，
/// 这本身就是"结局是条件树，不是特判"的一次检验。
/// </para>
/// <para>
/// 转生语义是<b>「重启」</b>：世界推倒重来，按历史累计换取「火种」；
/// 第二资源是<b>记忆残片</b>——它由上一次重启留下来的建筑产出（见 <see cref="ShardsModule"/>），
/// 所以"继承"在这里不是一个静态事实，而是下一轮产量的直接来源。
/// </para>
/// </summary>
public static class ApocalypseContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘末世";

    /// <summary>第二资源「记忆残片」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string ShardsCounterKey = ShardsModule.CounterKey;

    /// <summary>每多少个留下来的座位，每秒产出 1 片记忆残片。</summary>
    public const double SeatsPerShardPerSecond = ShardsModule.SeatsPerShardPerSecond;

    /// <summary>「记忆够得上一个结局」的门槛（两个结局共用；公开出来供测试与文档引用）。</summary>
    public const double ShardsForEnding = Endings.ShardsForEnding;

    /// <summary>「复活人类」额外要求的图鉴条数。</summary>
    public const double LoreForRevival = Endings.LoreForRevival;

    /// <summary>构建内容定义（含记忆残片模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("物资", "🥫", "翻找")
            .WithPrestigeCurrency("火种", "🔥")
            .WithBalance(BuildBalance())
            .Add(new ShardsModule())
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
    /// 平衡参数：末世的手感是"手要动、夜很长、东西拆了就没了"。<para>
    /// 点击比实验室有用（废墟里没有仪器，只有手），离线上限比公司长（没人催你上班，
    /// 但也没人替你看着），随机事件来得密——变异体不挑时间。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 变异体不挑时间：事件来得比别的包密，停留时间也更短。
        GoldenCookieMinDelay = 60 * 4,
        GoldenCookieMaxDelay = 60 * 12,
        GoldenCookieLifetime = 12,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 没有人上班，也没有人替你看着：离线上限 3 小时，按 65% 结算。
        OfflineCapSeconds = 10800,
        OfflineEfficiency = 0.65,
        MinimumOfflineSeconds = 60,

        PrestigeDivisor = 1e12,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 拆下来的东西卖不出价——「拆了就真的没有了」。
        DefaultSellRefundRate = 0.5,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
