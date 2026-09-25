using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 内容包 #1《猫娘咖啡馆》的入口：把各表拼装成一个校验过的 <see cref="GameContent"/>。<para>
/// 定位：十个包里<b>唯一没有道德压力的那个</b>，也是"换内容包即换游戏"的第一个实物证据——
/// 除 <c>NumericMetric.Counter</c>（框架通用能力，已落地）外，没有为它动过一行核心代码。
/// </para>
/// <para>
/// 转生语义是<b>重装修</b>：店面推倒重来，但「常客的记忆」留下来——这正是
/// <see cref="UpgradePersistence.Permanent"/> 的叙事外壳，货币叫「常客的信」。
/// </para>
/// <para>
/// 规格来源：<c>docs/PACK_01_CAT_CAFE.md</c>。想基于它做 #2~#10，只需按该文档 §14 换表，
/// 引擎代码一行都不用改。
/// </para>
/// </summary>
public static class CafeContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘咖啡馆";

    /// <summary>
    /// 第二资源「幸福感」的计数器键（同时是存档键）。<para>
    /// 公开出来是为了让测试与后续内容包能在不改 <see cref="HappinessModule"/> 的前提下引用它。
    /// </para>
    /// </summary>
    public const string HappinessCounterKey = HappinessModule.CounterKey;

    /// <summary>构建内容定义（含幸福感模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("小鱼干", "🐟", "做咖啡")
            .WithPrestigeCurrency("常客的信", "💌")
            .WithBalance(BuildBalance())
            .Add(new HappinessModule())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .Build();

    /// <summary>
    /// 平衡参数：在框架默认值上做四处调整，体现"治愈系"手感——
    /// 点击更有存在感、随机好事更常发生、手忙脚乱的惩罚更小、卖出不惩罚试错。
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        // 点咖啡要有存在感：点一下 = 1 条 + 当前产量的 1.5%。
        ClickBasePower = 1,
        ClickCpsRatio = 0.015,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 金猫（走错门的客人）：4~12 分钟一次（基准 5~15），存活 15 秒（基准 13），
        // 开局第一只提前到 15% 间隔，降低新手的错过感。
        GoldenCookieMinDelay = 60 * 4,
        GoldenCookieMaxDelay = 60 * 12,
        GoldenCookieLifetime = 15,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.15,

        // 离线收益：最多补 3 小时，全额结算。
        OfflineCapSeconds = 3 * 3600,
        OfflineEfficiency = 1.0,
        MinimumOfflineSeconds = 60,

        // 转生：1 兆 = 1 级，与「常客的信」的定价（3~30 封）匹配。
        PrestigeDivisor = 1e12,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 治愈系不该惩罚试错：卖出返还 60%（基准 50%）。
        DefaultSellRefundRate = 0.6,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
