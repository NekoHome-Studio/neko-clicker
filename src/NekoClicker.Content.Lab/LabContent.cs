using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 内容包 #3《猫娘实验室》的入口。<para>
/// 定位：<b>第一个同时用到 Era + Lore + Choice 三项引擎能力的包</b>。
/// 它同时是一份检验——如果它需要往核心加任何一行"针对实验室的分支"，
/// 那么"核心不认识内容"（A1/A2）这个主张就是假的。目前核心一行未改。
/// </para>
/// <para>
/// 转生语义是<b>开新批次</b>：实验推倒重来，残留的记忆留在档案里，货币叫「残留记忆」。
/// 第二资源是<b>伦理值</b>——它不来自规模，只来自"有谁在看"（见 <see cref="EthicsModule"/>）。
/// </para>
/// </summary>
public static class LabContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘实验室";

    /// <summary>第二资源「伦理值」的计数器键（公开出来供测试与后续内容引用）。</summary>
    public const string EthicsCounterKey = EthicsModule.CounterKey;

    /// <summary>每多少个「在场者」每秒产出 1 点伦理值（公开常量，供测试与内容对齐数值）。</summary>
    public const double WatchersPerPointPerSecond = EthicsModule.WatchersPerPointPerSecond;

    /// <summary>构建内容定义（含伦理值模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("数据", "📊", "记录")
            .WithPrestigeCurrency("残留记忆", "🧬")
            .WithBalance(BuildBalance())
            .Add(new EthicsModule())
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
    /// 平衡参数：体现"实验室"的手感差异——<b>点击近乎无用</b>（这里是仪器在干活，
    /// 不是你在干活），随机事故频繁且凶，离线收益低（样本需要人在场）。
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        // 你是研究员，不是猫。徒手能做的很有限。
        ClickBasePower = 1,
        ClickCpsRatio = 0.005,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 实验事故：5~15 分钟一次（与基准一致），但只停留 11 秒——错过就是错过。
        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 15,
        GoldenCookieLifetime = 11,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 样本需要人在场：离线上限压到 1 小时，且只按 70% 结算。
        OfflineCapSeconds = 3600,
        OfflineEfficiency = 0.7,
        MinimumOfflineSeconds = 60,

        // 转生除数按**本包的阶梯**标定，而不是照抄配方 1e12：
        // 目标是"最后一次结算落在 ~100 级"，让永久线的总价（≤ 80）
        // 在一次自然游玩里就买得起。照抄 1e12 的话，纪元包的可结算历史累计
        // 被阶梯卡在 1e8~1e11，等级恒为 0 —— 整条永久线**结构上打不开**。
        PrestigeDivisor = 6e3,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 设备拆解折价更狠：这台实验室不鼓励你反悔。
        DefaultSellRefundRate = 0.4,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
