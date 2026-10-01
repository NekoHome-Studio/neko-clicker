namespace NekoClicker.Core.Content;

/// <summary>
/// 全局平衡参数。<para>
/// 这些都是"引擎行为"级别的常量（原版写在 <c>Game</c> 里的那些魔法数字）。把它们集中成
/// 一个可覆盖的 record，好处是平台化调参：做一个"两倍速周末活动"只需要换一个
/// <see cref="GameBalance"/> 实例，而不用改任何内容定义。
/// </para>
/// </summary>
public sealed record GameBalance
{
    // ---------- 点击 ----------

    /// <summary>点击的基础收益。</summary>
    public double ClickBasePower { get; init; } = 1;

    /// <summary>点击额外获得的"当前 CPS 的比例"（原版 0.01，即 1% DPS）。</summary>
    public double ClickCpsRatio { get; init; } = 0.01;

    // ---------- 循环 ----------

    /// <summary>逻辑帧率（原版 30）。</summary>
    public double TickRate { get; init; } = 30;

    /// <summary>单次 <c>Update</c> 最多补算的真实秒数，防止卡顿/断点后一次算爆。</summary>
    public double MaxCatchUpSeconds { get; init; } = 5;

    /// <summary>成就条件的检查间隔（秒）。条件树评估比生产结算贵，降低频率。</summary>
    public double AchievementCheckInterval { get; init; } = 1;

    // ---------- 存档 ----------

    /// <summary>自动存档间隔（秒）；&lt;= 0 表示关闭。</summary>
    public double AutoSaveInterval { get; init; } = 60;

    // ---------- 金猫 ----------

    /// <summary>金猫出现的最小间隔（秒）。</summary>
    public double GoldenCookieMinDelay { get; init; } = 60 * 5;

    /// <summary>金猫出现的最大间隔（秒）。</summary>
    public double GoldenCookieMaxDelay { get; init; } = 60 * 15;

    /// <summary>金猫停留时间（秒）。</summary>
    public double GoldenCookieLifetime { get; init; } = 13;

    /// <summary>同时存在的金猫上限。</summary>
    public int MaxConcurrentGoldenCookies { get; init; } = 1;

    /// <summary>开局第一只金猫的间隔缩放（让新手更快见到这个机制）。</summary>
    public double FirstGoldenCookieDelayFactor { get; init; } = 0.25;

    /// <summary>是否启用金猫系统。</summary>
    public bool GoldenCookiesEnabled { get; init; } = true;

    // ---------- 离线收益 ----------

    /// <summary>离线收益的时间上限（秒）。</summary>
    public double OfflineCapSeconds { get; init; } = 3 * 3600;

    /// <summary>离线期间的产量效率（1 = 全额）。</summary>
    public double OfflineEfficiency { get; init; } = 1.0;

    /// <summary>低于该时长的离线不弹提示、不计收益。</summary>
    public double MinimumOfflineSeconds { get; init; } = 60;

    // ---------- 转生 ----------

    /// <summary>转生公式的除数（原版 1e12：赚到 1 兆得 1 点）。</summary>
    public double PrestigeDivisor { get; init; } = 1e12;

    /// <summary>转生公式的指数（原版 1/3）。</summary>
    public double PrestigeExponent { get; init; } = 1.0 / 3.0;

    /// <summary>每个转生等级折算的转生货币数量。</summary>
    public double PrestigeChipsPerLevel { get; init; } = 1;

    /// <summary>转生时是否保留成就（原版保留）。</summary>
    public bool KeepAchievementsOnAscend { get; init; } = true;

    // ---------- 买卖 ----------

    /// <summary>是否允许出售建筑。</summary>
    public bool AllowSelling { get; init; } = true;

    /// <summary>默认出售返还比例。</summary>
    public double DefaultSellRefundRate { get; init; } = 0.5;

    /// <summary><c>BuyMax</c> 单次最多计算的购买数量，防止极端数值下的求解抖动。</summary>
    public int MaxBulkBuy { get; init; } = 100_000;

    /// <summary>固定步长（秒）。</summary>
    public double FixedDeltaSeconds => 1.0 / Math.Max(1.0, TickRate);
}
