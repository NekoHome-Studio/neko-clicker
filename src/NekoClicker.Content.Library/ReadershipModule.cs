using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 第二资源「被阅读度」：<b>会自己掉下去的计数器</b>。<para>
/// 这是全项目唯一一个非单调的第二资源，也是 ROADMAP 阶段 4B 要交付的能力「虚无化」。
/// 规则只有一条：
/// <code>
/// dR/dt = 读者类建筑数 × 1  −  R / 240
/// </code>
/// 也就是<b>按比例衰减</b>：注意力不会每秒固定掉一截，而是按"还剩多少"掉。
/// 均衡点是 <c>R* = 240 × 读者类建筑数</c>——一个玩家可以直接算出来的数。
/// </para>
/// <para>
/// <b>为什么按比例而不是线性、也不是阈值悬崖</b>（ROADMAP §6.5 的三条设计决定）：
/// <list type="number">
///   <item>按比例衰减的语义是"越没人读，掉得越慢但永远掉不干净"，这是注意力真正的行为。</item>
///   <item>均衡点可观测：玩家看到的是"再建一座阅览室，稳定在哪儿"，而不是"一直在慢慢掉"。</item>
///   <item>一步到位的闭式解让 tick 与离线共用同一个公式，没有积分漂移——
///     离线 3 小时算一次和在线算三十二万次的结果是同一个数（有用例守着）。</item>
/// </list>
/// 阈值悬崖被明确否决：乘数一旦是阶跃的，玩家就会体验到"差 1 点被砍一半"，
/// 而那正是 R6 拒绝模态弹窗的同一个理由。
/// </para>
/// <para>
/// <b>它怎么影响产量</b>：内容侧在每一层的 <c>Modifiers</c> 里写
/// <c>GlobalMultiplier(0.5)</c> + <c>GlobalPercent(Scaling(CustomCounter, 0.005, Cap: 300, readership))</c>，
/// 于是乘数 = <c>0.5 × (1 + min(0.5% × R, 300%))</c>：
/// 没有读者时 ×0.5（书还在，但等于没写）、2 万时回到 ×1.0、6 万时 ×2.0 封顶。
/// <b>下限 0.5 而不是 0</b> 是阶段 4B 的验收 ②：压力是真的，死锁是没有的。
/// </para>
/// <para>
/// <b>每次重启清零</b>（<see cref="OnAscend"/>）：上一本书的读者不会自动读新书。
/// 这正是"写新书开新世界观"这个转生语义的落点，也是这个包与其余五个包手感上最不一样的地方。
/// 代价是被阅读度不单调，<b>不能进纪元完成条件</b>（构建期只拦得住 Cps 那类明令禁止的指标，
/// 计数器靠内容自觉）——#9 的完成条件只用累计赚取 / 成就数 / peak_cps。
/// </para>
/// </summary>
internal sealed class ReadershipModule : IGameModule
{
    /// <summary>被阅读度的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "readership";

    /// <summary>产出被阅读度的建筑标签。</summary>
    public const string ReaderTag = "reader";

    /// <summary>每座读者类建筑每秒带来的被阅读度。</summary>
    public const double ReadersPerPointPerSecond = 1.0;

    /// <summary>
    /// 衰减时间常数（秒）。<c>R/240</c> 表示"什么都不做的话，240 秒后剩约 37%"，
    /// 也意味着均衡点 = 240 × 读者类建筑数。
    /// </summary>
    public const double DecaySeconds = 240.0;

    /// <summary>
    /// 触发一次产量重算所需的被阅读度增量。<para>
    /// <c>Step()</c> 的顺序是「先重算，再结算生产，最后才 module.OnTick」——模块改了计数器
    /// 并不会让产量变脏。每秒都 <c>MarkDirty</c> 又会把 <c>ModifierSet</c> 的
    /// "生命周期与状态变更绑定"直接毁掉（30fps × 长跑 = 上千万次全量重算）。
    /// 折中是<b>跨过量子才重算</b>：200 点是乘数阶梯的 1%，量化误差不到量程的 0.2%。
    /// </para>
    /// </summary>
    public const double DirtyQuantum = 200.0;

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "readership";

    /// <summary>每个固定步长结算一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Advance(engine, deltaSeconds);

    /// <summary>
    /// 离线结算后补算——离线期间不经过 <see cref="OnTick"/>。<para>
    /// 用闭式解一次算完，而不是按 tick 步进：离线 3 小时是 32 万步，
    /// 步进既慢又会积累浮点误差，而"衰减少多少、补回多少"本来就有解析解。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Advance(engine, progress.CreditedSeconds);

    /// <summary>
    /// 重启（写新书）时清零：<b>上一本书的读者不会自动读新书。</b>
    /// 引擎保证本方法在 <c>ResetRun</c> 之后调用，所以这里只要把计数器置零即可。
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
        if (engine.State.GetCounter(CounterKey) == 0) return;
        engine.State.SetCounter(CounterKey, 0);
        engine.MarkDirty();
    }

    /// <summary>被阅读度的均衡值（诊断与测试用）：读者类建筑数 × 衰减时间常数。</summary>
    /// <param name="readerBuildings">读者类建筑总数。</param>
    /// <returns>该规模下的稳定被阅读度。</returns>
    public static double Equilibrium(double readerBuildings)
        => readerBuildings * ReadersPerPointPerSecond * DecaySeconds;

    private static void Advance(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double readers = engine.Metrics.TaggedBuildingCount(ReaderTag);
        double gain = readers * ReadersPerPointPerSecond;
        double rate = 1.0 / DecaySeconds;

        double current = engine.State.GetCounter(CounterKey);
        double equilibrium = gain / rate;

        // 闭式解：R ← R* + (R − R*)·e^(−rate·dt)。
        // dt 很大时指数项趋近 0（离线一整天也只是一次乘法），dt 很小时不会掉精度。
        double next = equilibrium + (current - equilibrium) * Math.Exp(-rate * seconds);
        if (next < 0 || double.IsNaN(next)) next = 0;

        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);
        if (next == current) return;

        engine.State.SetCounter(CounterKey, next);
        if (crossedQuantum) engine.MarkDirty();
    }
}
