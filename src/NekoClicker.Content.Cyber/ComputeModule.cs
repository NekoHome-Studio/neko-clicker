using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 第二资源「算力」：<b>常驻的进程在替她算</b>。<para>
/// 规则一句话：<b>算力每秒增加的量，正比于"带算力标签的建筑"的规模</b>：
/// <code>
/// dC/dt = ComputeBaseRate × Σ(该建筑数量 × 该建筑的算力权重)
/// </code>
/// 权重是<b>平方阶梯</b>（第 n 座建筑的权重 = n²）：进程跑完就退出，权重 0；
/// 守护进程 1、容器 4、虚拟机 9……弃用进程池 81。于是"往上一层，算力快一大截"，
/// 而玩家能一眼看出是谁在养它——这正是 BRIEF 要的"真实的产出语义"。
/// </para>
/// <para>
/// <b>它为什么是单调不减的</b>：<see cref="OnTick"/> 只做加法，
/// <see cref="OnAscend"/> <b>不清零</b>。理由不是"省事"，而是这个包的设定：
/// <b>迁服务器带走的是算力，不是机器</b>——她换了一台新机器，但她学会的算法、
/// 攒下的调度经验、以及那些愿意跟着她走的常驻进程，全都还在。
/// 清零的应该是"建筑"，而那件事引擎已经在 <c>PrestigeSystem.ResetRun</c> 里做了。
/// </para>
/// <para>
/// <b>为什么速度用平方权重而不是复用产量的成长曲线</b>：如果算力的产率挂在
/// <c>Scaling(ScalingSource.CustomCounter, …)</c> 上（"每点算力让算力长得更快"），
/// 那就是一个自反馈回路，计数器会变成双重指数；而这个包必须把算力放进纪元完成条件，
/// 一旦它可以爆炸，五层的门槛就全部失去意义。所以：
/// <b>算力的产率只由建筑数量决定</b>，算力本身只驱动<b>产量</b>（见
/// <c>Upgrades.ComputeUpgrades</c> 与末层规则），两个方向刻意分开。
/// </para>
/// <para>
/// <b>它怎么影响产量</b>（这部分才是"真的有用"）：内容侧在升级与末层规则里写
/// <c>GlobalPercent(0, new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 20_000_000, ComputeCounterKey))</c>，
/// 于是 <b>每 2 万点算力换来 +100% 全局产量</b>。注意 <c>Cap</c> 限的是<b>原始计数值</b>
/// （作者手册 §8 的坑）：<c>PerUnit 0.00005 × Cap 2e7 = +1000%</c>（×11 上限）。
/// </para>
/// <para>
/// <b>单例依赖</b>：本模块不持有任何实例状态，所有数据都在 <c>GameState.Counters</c> 里，
/// 所以存档、离线补算、转生都不需要核心做任何额外的事。
/// </para>
/// </summary>
internal sealed class ComputeModule : IGameModule
{
    /// <summary>算力的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "compute";

    /// <summary>产出算力的建筑标签。</summary>
    public const string ComputeTag = "compute";

    /// <summary>
    /// 算力的基础产率：整个机群的权重和 × 这个数 = 每秒算力。<para>
    /// 0.05 是按<b>实测包络</b>定的（BRIEF 的调参顺序：先量再设值）：
    /// 开局只有一座守护进程时约 0.05/s，走完五层时约 4000/s——
    /// 与五层的完成门槛（1e3 → 9e6）落在同一个量级里，玩家不用攒到天荒地老。
    /// </para>
    /// </summary>
    public const double ComputeBaseRate = 0.05;

    /// <summary>
    /// 触发一次产量重算所需的算力增量。<para>
    /// <c>Step()</c> 的顺序是「先重算，再结算生产，最后才 module.OnTick」——模块改了计数器
    /// 并不会让产量变脏。但每 tick 都 <c>MarkDirty</c> 又会把 <c>ModifierSet</c> 的
    /// "生命周期与状态变更绑定"直接毁掉（30fps × 长跑 = 上千万次全量重算）。
    /// 折中是<b>跨过量子才重算</b>：2000 点在这个包的阶梯上是乘数曲线的 10%，
    /// 量化误差远小于一次升级带来的跳变。
    /// </para>
    /// </summary>
    public const double DirtyQuantum = 2_000;

    /// <summary>
    /// 每座建筑的算力权重，按"往上爬一层"递增（第 n 座 = n²）。
    /// <para>
    /// 用数组下标 + 1 而不是字典：这张表与 <see cref="Buildings.All"/> 的顺序
    /// 是同一份阶梯的两种写法，写成字典反而会让人以为它们可以各自改。
    /// </para>
    /// </summary>
    public static readonly string[] ComputeBuildings =
    [
        "daemon",        // 1²  = 1
        "container",     // 2²  = 4
        "cluster",       // 3²  = 9
        "datacenter",    // 4²  = 16
        "root_server",   // 5²  = 25
        "orphan_pool",   // 6²  = 36
    ];

    /// <summary>
    /// 登记这个计数器的<b>玩家可见名</b>。
    /// <para>
    /// 计数器键是内部标识（<c>compute</c>），而它会出现在解锁提示、升级效果与纪元规则里。
    /// 不登记的话玩家看到的是「每点「compute」」这种半成品文案。
    /// </para>
    /// </summary>
    /// <param name="builder">内容构建器。</param>
    public void Configure(GameContentBuilder builder)
        => builder.AddCounterName(CounterKey, "算力");

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "compute";

    /// <summary>每个固定步长结算一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Advance(engine, deltaSeconds);

    /// <summary>
    /// 离线结算后补算——离线期间不经过 <see cref="OnTick"/>。<para>
    /// 这个模块的产率<b>不随时间衰减</b>（常驻进程不会因为你不在就停），所以补算就是一次乘法。
    /// 用 <c>CreditedSeconds</c> 而不是真实离线时长，是为了和补发收益的上限保持一致：
    /// 收益只按上限发，资源就该只按上限算，否则离线越久越划算。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Advance(engine, progress.CreditedSeconds);

    /// <summary>
    /// 迁服务器时<b>不清零</b>：换掉的是机器，不是她学会的东西。<para>
    /// 这里刻意留空而不是"写一行注释说不需要"——一个空实现 + 这条说明，
    /// 比一个把计数器写回去的实现更难被误改。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
    }

    /// <summary>当前的每秒算力（诊断、成就与测试用；与 <see cref="OnTick"/> 用的是同一个公式）。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>该机群规模下的每秒算力。</returns>
    public static double RatePerSecond(GameEngine engine) => RatePerSecondFor(engine.State);

    /// <summary>当前的每秒算力，直接从状态读（供不想依赖引擎的诊断路径使用）。</summary>
    /// <param name="state">游戏状态。</param>
    /// <returns>该机群规模下的每秒算力。</returns>
    public static double RatePerSecondFor(GameState state)
    {
        double weight = 0;
        for (int i = 0; i < ComputeBuildings.Length; i++)
        {
            double tier = i + 1;
            weight += state.BuildingCount(ComputeBuildings[i]) * tier * tier;
        }

        return ComputeBaseRate * weight;
    }

    private static void Advance(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double gain = RatePerSecond(engine) * seconds;
        if (gain <= 0) return;

        double current = engine.State.GetCounter(CounterKey);
        double next = current + gain;

        engine.State.SetCounter(CounterKey, next);
        if ((long)(current / DirtyQuantum) != (long)(next / DirtyQuantum)) engine.MarkDirty();
    }
}
