using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 内容包 #2《九命轮回》的入口。<para>
/// 这是第一个使用<b>分层转生</b>的内容包：九个纪元、每层换一条规则、
/// 舍命按钮必须完成本层主线才能按下。引擎侧只用到 <c>Era</c> 的四个接缝，
/// 没有任何针对"第几层"的特判。
/// </para>
/// </summary>
public static class NineLivesContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "九命轮回";

    /// <summary>构建内容定义。</summary>
    public static GameContent Build()
    {
        GameBalance balance = BuildBalance();

        return new GameContentBuilder(GameTitle)
            .WithCurrency("小鱼干", "🐟", "摸头")
            .WithPrestigeCurrency("情感能量", "💠")
            .WithBalance(balance)
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddEras(Eras.All(balance))
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .Add(new FaithModule())
            .Build();
    }

    /// <summary>基准平衡参数；各纪元用 <c>with</c> 在它之上做局部覆盖。</summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 15,
        GoldenCookieLifetime = 13,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        OfflineCapSeconds = 3 * 3600,
        OfflineEfficiency = 1.0,
        MinimumOfflineSeconds = 60,

        PrestigeDivisor = 1e12,
        PrestigeExponent = 1.0 / 3.0,

        AutoSaveInterval = 60,
    };
}
