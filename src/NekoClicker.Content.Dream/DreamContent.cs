using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 内容包 #8《猫娘梦境》的入口。<para>
/// 定位：ROADMAP 阶段 5 的换皮批产之一，语气是<b>梦层</b>——一层一层往下嵌套，
/// "越深的梦越大、也越难醒"。温柔，但有一点点不舍。
/// </para>
/// <para>
/// <b>它为什么能零核心改动</b>：这个包需要的东西只有两样——
/// 一个单调递增的第二资源（<see cref="DreamEnergyModule"/>），
/// 以及"每层换手感"的分层转生（<see cref="EraDefinition.Modifiers"/> + <c>Balance</c>）。
/// 前者落在 <c>IGameModule</c> 上，后者是阶段 1~4 就有的接缝；
/// 连"越深的梦越大"这条核心曲线也只用 <c>Scaling(ScalingSource.CustomCounter)</c> 表达。
/// 所以 <c>src/NekoClicker.Core/</c> 一行未动。
/// </para>
/// <para>
/// 转生语义是<b>「再往下睡一层」</b>：上一层的梦塌了，新的梦更大，按历史累计换取「梦屑」。
/// 第二资源是「梦境能量」——<b>只涨不落，而且跨层不清零</b>（梦会留在她身上）。
/// 两个结局的分岔只压在一件事上：她有没有攒够把人叫醒的力气。
/// </para>
/// </summary>
public static class DreamContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘梦境";

    /// <summary>第二资源「梦境能量」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string DreamEnergyCounterKey = DreamEnergyModule.CounterKey;

    /// <summary>产出梦境能量的建筑标签。</summary>
    public const string DreamLayerTag = DreamEnergyModule.DreamLayerTag;

    /// <summary>「越深的梦越大」那条成长曲线的软上限（= 封顶所需的梦境能量）。</summary>
    public const double DreamEnergySoftCap = Eras.DreamEnergySoftCap;

    /// <summary>「叫醒梦者」结局要求的梦境能量门槛。</summary>
    public const double DreamEnergyForEnding = Endings.DreamEnergyForEnding;

    /// <summary>梦层类建筑每秒产出的梦境能量（产率表的副本，公开出来供测试与文案引用）。</summary>
    public static IReadOnlyDictionary<string, double> DreamEnergyRatePerBuilding => DreamEnergyModule.RatePerBuilding;

    /// <summary>构建内容定义（含梦境能量模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("点", "🌙", "闭眼")
            .WithPrestigeCurrency("梦屑", "✨")
            .WithBalance(BuildBalance())
            .Add(new DreamEnergyModule())
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
    /// 平衡参数：梦境的手感是"入睡快、醒得慢"。<para>
    /// 点击比别的包都强（浅眠那一层一动就醒，手指还有用），
    /// 离线上限中等（睡得更沉的那一层会把它翻倍——第 2 层的规则变化就写在离线时长上），
    /// 但梦魇来得比图书馆的蠹虫还稀：它是从梦的褶皱里翻上来的，不是每天都有。
    /// </para>
    /// <para>
    /// <b>转生除数按本包的阶梯标定</b>，而不是照抄配方 1e12：目标是"最后一次结算落在 ~100 级"。
    /// 实测这一个包跑满 12 游戏小时的历史累计约 1e12，除以 <c>2e5</c> 正好落在 130 级上下，
    /// 而「梦屑」永久线总价 60 —— 一次自然游玩就买得起。
    /// 照抄 1e12 的话等级恒为 0，整条永久线<b>结构上打不开</b>（<c>PrestigeTests</c> 有专门的守卫）。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 梦魇来得比蠹虫还稀，但来一次待得久（负面结果比例也更高一点）。
        GoldenCookieMinDelay = 60 * 6,
        GoldenCookieMaxDelay = 60 * 16,
        GoldenCookieLifetime = 13,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.22,

        // 睡了 3 小时，按 65% 结算——做梦本来就不如醒着算得清。
        // 第 2 层「深眠」会把这条上限翻倍（写在那一层的 Balance 上）。
        OfflineCapSeconds = 3 * 3600,
        OfflineEfficiency = 0.65,
        MinimumOfflineSeconds = 60,

        PrestigeDivisor = 2e5,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 醒来之后梦就是醒了：卖掉的东西返还率压得比别的包低。
        DefaultSellRefundRate = 0.42,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
