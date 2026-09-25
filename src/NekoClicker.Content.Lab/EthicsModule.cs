using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 第二资源「伦理值」：不可消费的评分（与幸福感的定位一致——消费型第二资源会逼玩家
/// 纠结"该不该花"，而这个包想让你纠结的是"该不该做"）。<para>
/// <b>关键设计：伦理值不来自规模，来自「有谁在看」。</b>
/// 它只由带 <c>awake</c> 标签的建筑产出——觉醒区、伦理委员会——
/// 所以把样本农场铺到一千座也换不来一点伦理值。反过来，规模越大人均越薄，
/// "效率"和"克制"在这个包里的张力就是这么长出来的。
/// </para>
/// <para>
/// 用 <see cref="IGameModule"/> 实现，<b>核心引擎零改动</b>（与内容包 #1 的幸福感同构）：
/// <list type="bullet">
///   <item><c>GameState.Counters</c> 转生时不清空 → 伦理值的"账"跨批次保留。</item>
///   <item><c>UnlockCondition.Counter("ethics", n)</c> → 解锁条件自带进度条。</item>
///   <item><c>Scaling(ScalingSource.CustomCounter, ..., Id: "ethics")</c> → 可以驱动修饰符。</item>
///   <item>离线期间不经过 <see cref="OnTick"/>，必须实现 <see cref="OnOffline"/> 补算。</item>
/// </list>
/// </para>
/// </summary>
internal sealed class EthicsModule : IGameModule
{
    /// <summary>伦理值的计数器键（存档键，同时被解锁条件与纪元门槛引用）。</summary>
    public const string CounterKey = "ethics";

    /// <summary>
    /// 伦理值的加成计数器（升级可以往这里写"每小时伦理值 +X%"）。<para>
    /// 本包内容不写它，因此当前恒为 0——但契约留在代码里，后续加升级不必改模块。
    /// </para>
    /// </summary>
    public const string BonusCounterKey = "ethics_bonus";

    /// <summary>每多少个「在场者」每秒产出 1 点伦理值。</summary>
    public const double WatchersPerPointPerSecond = 10.0;

    /// <summary>产出伦理值的建筑标签。</summary>
    public const string WatcherTag = "awake";

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "ethics";

    /// <summary>每个固定步长累加伦理值。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>离线结算后补算伦理值——离线期间不经过 <see cref="OnTick"/>。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double watchers = engine.Metrics.TaggedBuildingCount(WatcherTag);
        if (watchers <= 0) return;

        double bonus = 1 + engine.Metrics.GetCounter(BonusCounterKey);
        engine.State.AddCounter(CounterKey, watchers / WatchersPerPointPerSecond * bonus * seconds);
    }
}
