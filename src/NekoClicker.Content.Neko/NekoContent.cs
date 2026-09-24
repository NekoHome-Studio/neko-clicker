using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 示例内容包的入口：把各表拼装成一个校验过的 <see cref="GameContent"/>。<para>
/// 想把它当成模板做自己的游戏，只需要改本文件的文案与 <c>Build()</c> 里的平衡参数，
/// 再替换 Buildings / Upgrades / Achievements 三张表即可——引擎代码一行都不用动。
/// </para>
/// </summary>
public static class NekoContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫咖物语";

    /// <summary>构建内容定义。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("小鱼干", "🐟", "撸猫")
            .WithPrestigeCurrency("猫薄荷", "🌿")
            .WithBalance(BuildBalance())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .Build();

    /// <summary>平衡参数。<see cref="GameBalance"/> 的默认值已经是原版手感，这里只覆盖需要调的几项。</summary>
    public static GameBalance BuildBalance() => new()
    {
        // 点击手感：1 条基础 + 1% 当前产量。
        ClickBasePower = 1,
        ClickCpsRatio = 0.01,

        // 30Hz 与原版一致；补算上限 5 秒避免卡顿后雪崩。
        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 金猫：5~15 分钟一只，存活 13 秒，开局第一只提前到 ~1/5 间隔。
        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 15,
        GoldenCookieLifetime = 13,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        // 离线收益：最多补 3 小时，全额结算（可通过天堂升级再加强）。
        OfflineCapSeconds = 3 * 3600,
        OfflineEfficiency = 1.0,
        MinimumOfflineSeconds = 60,

        // 转生：1 兆 = 1 级，与"猫薄荷"的定价（3~30 点）匹配。
        PrestigeDivisor = 1e12,
        PrestigeExponent = 1.0 / 3.0,

        AutoSaveInterval = 60,
    };
}
