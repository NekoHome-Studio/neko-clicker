using NekoClicker.Core;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 第二资源「幸福感」：不是货币，是<strong>评分</strong>——客人满意就涨，涨到阈值解锁内容，
/// 不可消费（消费型第二资源会让玩家纠结"该不该花"，与治愈系基调冲突）。<para>
/// 用 <see cref="IGameModule"/> 实现，<b>核心引擎零改动</b>：
/// <list type="bullet">
///   <item><c>GameState.Counters</c> 已存在且转生时不清空 → "常客的记忆"天然被保留。</item>
///   <item><c>Scaling(ScalingSource.CustomCounter, ..., Id: "happiness")</c> → 幸福感可以驱动修饰符。</item>
///   <item><c>UnlockCondition.Counter("happiness", n)</c> → 幸福感解锁自带进度条。</item>
///   <item>离线期间 <see cref="OnTick"/> 不会运行，所以必须实现 <see cref="OnOffline"/> 补算。</item>
/// </list>
/// </para>
/// <para>
/// 数值：每 50 座建筑每秒 +1 幸福感。它刻意与"整间店的活跃度"挂钩而不是只数猫娘，
/// 这样开局买到第二、三座建筑后幸福感就开始可见地增长，玩家会主动去问"涨满了会怎样"。
/// </para>
/// </summary>
internal sealed class HappinessModule : IGameModule
{
    /// <summary>幸福感的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "happiness";

    /// <summary>
    /// 幸福感的额外加成计数器（预留给后续内容：升级可以把每小时幸福感 +X% 写进这里）。
    /// 本包内容不写它，因此当前恒为 0。
    /// </summary>
    public const string BonusCounterKey = "happiness_bonus";

    /// <summary>每多少座建筑每秒产出 1 点幸福感。</summary>
    public const double BuildingsPerPointPerSecond = 50.0;

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "happiness";

    /// <summary>每个固定步长累加幸福感。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>离线结算后补算幸福感——离线期间不经过 <see cref="OnTick"/>。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double rate = engine.State.TotalBuildings() / BuildingsPerPointPerSecond;
        double bonus = 1 + engine.Metrics.GetCounter(BonusCounterKey);
        engine.State.AddCounter(CounterKey, rate * bonus * seconds);
    }
}
