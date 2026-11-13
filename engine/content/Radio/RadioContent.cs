using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 内容包 C1《猫娘电台》的入口（一层 MVP）。<para>
/// 定位：<b>"换包即换游戏"的第一份实物证据</b>——别的包点击是生产线，这个包<b>点击是干扰</b>。
/// 核心循环从"点"反转成"把节目排好、管住手别点"：点击会打断播音、掉收听率（见 <see cref="BroadcastModule"/>），
/// 而点击基值被压到极小，乱点是亏本的。引擎侧零改动——反转靠的是模块订阅 ClickedEvent ＋ 数值配方，
/// 不是给引擎加特判。
/// </para>
/// <para>
/// 第二资源是「收听率」——会自己掉下去（衰减＋点击打断＋转生清零），通过
/// <c>Scaling(ScalingSource.CustomCounter)</c> 驱动产量乘数。它不单调，<b>不能进纪元完成条件</b>（R3）。
/// </para>
/// <para>
/// 转生语义是<b>「开新频段」</b>：上一频段停播，新的频段开始，按历史累计换取「频段执照」。
/// </para>
/// </summary>
public static class RadioContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "猫娘电台";

    /// <summary>第二资源「收听率」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string ListenershipCounterKey = BroadcastModule.CounterKey;

    /// <summary>产出收听率的建筑标签。</summary>
    public const string BroadcasterTag = BroadcastModule.BroadcasterTag;

    /// <summary>衰减时间常数（秒）；均衡点 = 播音类建筑数 × 这个数。</summary>
    public const double DecaySeconds = BroadcastModule.DecaySeconds;

    /// <summary>产量乘数的下限——收听率归零时仍有 ×0.5，永远不会归零到死锁。</summary>
    public const double MultiplierFloor = 0.5;

    /// <summary>产量乘数的上限。</summary>
    public const double MultiplierCeiling = 2.0;

    /// <summary>「被听到最后」结局要求的收听率门槛。</summary>
    public const double ListenershipForEnding = Endings.ListenershipForEnding;

    /// <summary>构建内容定义（含收听率模块）。</summary>
    public static GameContent Build()
    {
        GameContent content = new GameContentBuilder(GameTitle)
            .WithCurrency("信号", "📶", "播音")
            .WithPrestigeCurrency("频段执照", "📻")
            .WithBalance(BuildBalance())
            .Add(new BroadcastModule())
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

        // 剧情线与全部图鉴条目到这里都已经从 text.json 读过一遍了，正是查孤儿的时候。
        Lore.VerifyAllTextUsed();

        return content;
    }

    /// <summary>
    /// 平衡参数：电台的手感是"播得稳、听得久"，而<b>点击被刻意压到极小</b>——
    /// 这个包点击是干扰，不是生产线。离线上限中等（停播之后没人听），事件来得比别的包稀。
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        // 点击是干扰：基值与 CPS 占比都压到极小，乱点亏本（掉收听率损失的产量远大于这点点击）。
        ClickBasePower = 0.1,
        ClickCpsRatio = 0.001,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 14,
        GoldenCookieLifetime = 14,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.6,
        MinimumOfflineSeconds = 60,

        // 转生除数按**本包的阶梯**标定（一层 MVP 完成门槛 1e9），不照抄 1e12：
        // 目标是"一次自然游玩结算落在 ~100 级"，让永久线总价（80）一局买得起。
        PrestigeDivisor = 1e3,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        DefaultSellRefundRate = 0.45,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
