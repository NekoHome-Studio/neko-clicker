using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 第二资源「收听率」＋ 本包的核心反转：<b>点击是干扰</b>。<para>
/// 这是创意池 C1《猫娘电台》的机制落点，也是"换包即换游戏"的第一份实物证据：
/// 别的包点击是生产线，这个包点击是<b>打断播音</b>。核心循环从"点"反转成"把节目排好、别去点"。
/// </para>
/// <para>
/// 收听率是一个会自己掉下去的计数器（照 <c>ReadershipModule</c> 的闭式解范式）：
/// <code>
/// dL/dt = 播音类建筑数 × 1  −  L / 240
/// </code>
/// 均衡点 <c>L* = 240 × 播音类建筑数</c>。它通过 <c>Scaling(ScalingSource.CustomCounter)</c>
/// 驱动产量乘数：没人收听时 ×0.5（电台还在，但等于没播），收听率越高乘数越大、封顶 ×2.0。
/// </para>
/// <para>
/// <b>「点击是干扰」怎么实现（核心零改动）</b>：引擎本就有 <see cref="ClickedEvent"/>，
/// 模块在 <see cref="OnAttach"/> 订阅它——每点一次，收听率按当前值掉一小截（打断播音）。
/// 而 <c>ClickBasePower</c> 被压到极小：点击给的那点信号远抵不上掉收听率损失的产量。
/// 于是"多点"在这个包裡是<b>亏本</b>的，最优策略是把播音建筑堆起来、然后管住手。
/// 这正是 C1 要的"反转核心动词"，且没有动引擎一行。
/// </para>
/// <para>
/// 收听率不单调（会衰减、点击会掉、转生清零），<b>不能进纪元完成条件</b>（ROADMAP R3）——
/// 完成条件只用累计赚取 / 成就数这类单调指标。这与图书馆的被阅读度同一条纪律。
/// </para>
/// </summary>
internal sealed class BroadcastModule : IGameModule
{
    /// <summary>收听率的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "listenership";

    /// <summary>产出收听率的建筑标签（播音类建筑）。</summary>
    public const string BroadcasterTag = "broadcaster";

    /// <summary>每座播音类建筑每秒带来的收听率。</summary>
    public const double ListenersPerPointPerSecond = 1.0;

    /// <summary>衰减时间常数（秒）。均衡点 = 240 × 播音类建筑数。</summary>
    public const double DecaySeconds = 240.0;

    /// <summary>
    /// 触发一次产量重算所需的收听率增量（照 ReadershipModule 的量子化，避免每 tick 全量重算）。
    /// </summary>
    public const double DirtyQuantum = 200.0;

    /// <summary>
    /// 每次点击打断播音掉的收听率比例。<b>刻意小</b>：干扰是真的，但不至于把一局点死——
    /// 无头机器人仍会盲点（它不知道这个包的反转），掉得太多会让"全程可达"验收过不去。
    /// 0.5%/次 让"管住手"明显更优，又给盲点留了活路。
    /// </summary>
    public const double ClickInterruptFraction = 0.005;

    /// <summary>登记计数器的玩家可见名（内部键 listenership 不许印在玩家文案上）。</summary>
    public void Configure(GameContentBuilder builder)
        => builder.AddCounterName(CounterKey, "收听率");

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "broadcast";

    /// <summary>订阅点击事件——这就是"点击是干扰"的接线，核心零改动。</summary>
    public void OnAttach(GameEngine engine)
        => engine.Events.Subscribe<ClickedEvent>(_ => Interrupt(engine));

    /// <summary>每个固定步长结算一次。</summary>
    public void OnTick(GameEngine engine, double deltaSeconds) => Advance(engine, deltaSeconds);

    /// <summary>离线结算后补算（闭式解一次算完，与在线同公式，无积分漂移）。</summary>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Advance(engine, progress.CreditedSeconds);

    /// <summary>转生（开新频段）时清零：上一频段的听众不会自动跟过来。</summary>
    public void OnAscend(GameEngine engine)
    {
        if (engine.State.GetCounter(CounterKey) == 0) return;
        engine.State.SetCounter(CounterKey, 0);
        engine.MarkDirty();
    }

    /// <summary>收听率的均衡值（诊断与测试用）：播音类建筑数 × 衰减时间常数。</summary>
    public static double Equilibrium(double broadcasterBuildings)
        => broadcasterBuildings * ListenersPerPointPerSecond * DecaySeconds;

    /// <summary>点击打断播音：收听率按当前值掉一小截。</summary>
    private static void Interrupt(GameEngine engine)
    {
        double current = engine.State.GetCounter(CounterKey);
        if (current <= 0) return;
        double next = current * (1.0 - ClickInterruptFraction);
        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);
        engine.State.SetCounter(CounterKey, next);
        if (crossedQuantum) engine.MarkDirty();
    }

    /// <summary>闭式解：L ← L* + (L − L*)·e^(−rate·dt)。tick 与离线共用。</summary>
    private static void Advance(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double broadcasters = engine.Metrics.TaggedBuildingCount(BroadcasterTag);
        double gain = broadcasters * ListenersPerPointPerSecond;
        double rate = 1.0 / DecaySeconds;

        double current = engine.State.GetCounter(CounterKey);
        double equilibrium = gain / rate;

        double next = equilibrium + (current - equilibrium) * Math.Exp(-rate * seconds);
        if (next < 0 || double.IsNaN(next)) next = 0;

        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);
        if (next == current) return;

        engine.State.SetCounter(CounterKey, next);
        if (crossedQuantum) engine.MarkDirty();
    }
}
