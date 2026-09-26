using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 第二资源「士气」：不可消费，只涨（也会掉）。<para>
/// <b>关键设计：士气不是靠规模堆出来的，是靠「团队」养出来的，而加班会吃它。</b>
/// 只有带 <c>team</c> 标签的建筑（工位 / 会议室 / 增长团队）产出士气；
/// 带 <c>crunch</c> 标签的升级（狼性文化那条线）每个每秒持续扣士气；
/// 进入 A 轮之后，全员默认加班，再额外扣一笔。于是"买加班升级换产量"这个动作
/// 有了持续代价，而不是一次性的价格——这是这个包唯一的原创机制，也刻意做在内容侧。
/// </para>
/// <para>
/// 与内容包 #1 的幸福感、#3 的伦理值同构，走 <see cref="IGameModule"/>，<b>核心引擎零改动</b>：
/// <list type="bullet">
///   <item><c>GameState.Counters</c> 转生时不清空 → 士气跨重组保留。</item>
///   <item><c>UnlockCondition.Counter("morale", n)</c> → 解锁条件自带进度条。</item>
///   <item><c>Scaling(ScalingSource.CustomCounter, ..., Id: "morale")</c> → 士气可以驱动修饰符。</item>
///   <item>离线期间不经过 <see cref="OnTick"/>，必须实现 <see cref="OnOffline"/> 补算。</item>
/// </list>
/// </para>
/// <para>
/// <b>为什么士气不参与纪元完成条件</b>：它会掉，而舍命按钮上的进度必须单调不减（ROADMAP R3）。
/// 士气只驱动解锁、成就与倍率，门槛交给累计赚取与成就数。
/// </para>
/// </summary>
internal sealed class MoraleModule : IGameModule
{
    /// <summary>士气的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "morale";

    /// <summary>产出士气的建筑标签。</summary>
    public const string TeamTag = "team";

    /// <summary>持续消耗士气的升级标签（加班类）。</summary>
    public const string CrunchTag = "crunch";

    /// <summary>每多少个「团队」成员每秒产出 1 点士气。</summary>
    public const double TeamPerPointPerSecond = 10.0;

    /// <summary>每个加班类升级每秒消耗的士气。</summary>
    public const double CrunchDrainPerSecond = 0.2;

    /// <summary>A 轮（第 2 批）起全员加班，每秒额外消耗的士气。</summary>
    public const double OvertimeDrainPerSecond = 1.0;

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "morale";

    /// <summary>每个固定步长结算一次士气。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>离线结算后补算士气——离线期间不经过 <see cref="OnTick"/>。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double team = engine.Metrics.TaggedBuildingCount(TeamTag);
        double gain = team / TeamPerPointPerSecond;

        double drain = engine.Metrics.TaggedUpgradeCount(CrunchTag) * CrunchDrainPerSecond;
        if (engine.Metrics.Era >= 2) drain += OvertimeDrainPerSecond;

        double delta = (gain - drain) * seconds;
        if (delta == 0) return;

        // 士气有下限 0：加班可以把人熬没，但不能熬成负数（负士气没有语义，
        // 而且会让"计数器"变成双向指标，给后续内容埋坑）。
        double current = engine.State.GetCounter(CounterKey);
        engine.State.SetCounter(CounterKey, Math.Max(0, current + delta));
    }
}
