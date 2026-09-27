using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 内容包 #10《猫娘公司》的入口。<para>
/// 定位：第三个用 <c>Era</c> 的包、第二个用 <c>Choice</c> 的包，且<b>不引入任何新引擎能力</b>——
/// 它的立场轴是「劳资」（上市 / 工会 / 清算），与实验室的「道德」完全不是一回事，
/// 但共用同一套 <see cref="StanceDefinition"/>。这是 ROADMAP G3 要的第三套纪元叙事，
/// 也是"立场不是 enum"这条决定的第二次实物验证。
/// </para>
/// <para>
/// 转生语义是<b>重组</b>：公司推倒重来，按历史累计换取「期权」；三条路依次是
/// 车库创业 → A 轮 → 上市。第二资源是<b>士气</b>——它由「团队」建筑养起来，
/// 也会被加班类升级和 A 轮之后的全员加班吃掉（见 <see cref="MoraleModule"/>）。
/// </para>
/// </summary>
public static class CompanyContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘公司";

    /// <summary>第二资源「士气」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string MoraleCounterKey = MoraleModule.CounterKey;

    /// <summary>每多少个「团队」建筑每秒产出 1 点士气。</summary>
    public const double TeamPerPointPerSecond = MoraleModule.TeamPerPointPerSecond;

    /// <summary>构建内容定义（含士气模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("营收", "💰", "谈单")
            .WithPrestigeCurrency("期权", "🎫")
            .WithBalance(BuildBalance())
            .Add(new MoraleModule())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddEras(Eras.All(BuildBalance()))
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .AddStances(Stances.All)
            .AddChoices(Choices.All)
            .AddEndings(Endings.All)
            .Build();

    /// <summary>
    /// 平衡参数：公司的手感是"前期靠手，后期靠人"——点击比实验室有用（创业者自己也得干活），
    /// 离线收益中等（周末不上班），随机事件来得密（甲方不分工作日），但事件里也可能藏着融资。
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 甲方不分工作日：事件来得比别的包密，停留时间也更短（改需求的人不会等你）。
        GoldenCookieMinDelay = 60 * 4,
        GoldenCookieMaxDelay = 60 * 12,
        GoldenCookieLifetime = 12,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 周末不上班：离线上限 2 小时，按 60% 结算。
        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.6,
        MinimumOfflineSeconds = 60,

        // 转生除数按**本包的阶梯**标定，而不是照抄配方 1e12：
        // 目标是"最后一次结算落在 ~100 级"，让永久线的总价（≤ 80）
        // 在一次自然游玩里就买得起。照抄 1e12 的话，纪元包的可结算历史累计
        // 被阶梯卡在 1e8~1e11，等级恒为 0 —— 整条永久线**结构上打不开**。
        PrestigeDivisor = 35,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 二手设备卖不出价：这个包鼓励"想清楚再买"。
        DefaultSellRefundRate = 0.5,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
