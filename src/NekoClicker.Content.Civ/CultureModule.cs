using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 第二资源「文化」：<b>单调不减</b>的计数器，由"记录 / 传承 / 学府"类建筑产出。
/// <para>
/// 规则只有一条，而且是这个包全部数值的支点：
/// <code>
/// dCulture/dt = Σ 记录者类建筑数量 × 各自的产率
/// </code>
/// 也就是<b>文化只来自"有人把它记下来"这件事</b>，而不是来自规模。她可以有一百座城墙
/// 而文化纹丝不动——城墙挡得住洪水，挡不住遗忘。
/// </para>
/// <para>
/// <b>为什么每座建筑一个产率，而不是一个统一的常数</b>：文化是这个包的复利项
/// （每点 +0.002% 全局产量、还有一条跨时代的成长线挂在上面），所以它必须跟着
/// <b>时代</b>走——村口那面记事的墙和整颗星球的档案网不是一个量级。
/// 统一的常数会让文化在中盘变成一条压死其它内容的直线；
/// 按建筑给产率则让它与"她建了什么"绑定，而玩家能直接在城市列表里读出这件事。
/// </para>
/// <para>
/// <b>为什么不做衰减</b>：衰减（"无人读就消失"）是 #9 图书馆的专利，阶段 5 手册 §4 第 15 条
/// 明确不许再新增第二种会掉的机制。文化的语义本来就是"记得住的东西"——
/// 她在第 1 层的石堆上划的那道痕，到了第 5 层的星港还认得出。
/// 每一次「跨入下一个时代」<b>都不清零</b>（见 <see cref="OnAscend"/>）：
/// 建筑会消失、货币会归零，只有文化跟着她走。
/// </para>
/// <para>
/// <b>它怎么影响产量</b>：内容侧用
/// <c>GlobalPercent(Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 400_000, Id: "culture"))</c>
/// 之类的成长曲线驱动，另有 5 处解锁条件与 4 档成就挂在它上面——
/// 于是"哪一种建筑在养文化"是玩家能从界面上直接读出来的事，而不是一句设定。
/// </para>
/// </summary>
internal sealed class CultureModule : IGameModule
{
    /// <summary>文化的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "culture";

    /// <summary>产出文化的建筑标签。</summary>
    public const string RecorderTag = "culture";

    /// <summary>
    /// 每座记录者建筑每秒带来的文化（按建筑 id）。
    /// <para>
    /// 数字跟着时代阶梯走：村口记事的墙 1 点，星港时代的深空中继 24000 点。
    /// 没列进来的记录者建筑按 <see cref="DefaultCulturePerBuildingPerSecond"/> 计。
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> CulturePerBuilding = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["village"] = 1,
        ["market"] = 4,
        ["academy"] = 100,
        ["temple"] = 1_200,
        ["deep_space_relay"] = 24_000,
    };

    /// <summary>未在上面列出的记录者建筑每秒带来的文化（保守兜底，避免新建筑悄悄变成 0）。</summary>
    public const double DefaultCulturePerBuildingPerSecond = 1.0;

    /// <summary>
    /// 触发一次产量重算所需的文化增量。<para>
    /// <c>Step()</c> 的顺序是「先重算，再结算生产，最后才 module.OnTick」——模块在 tick 里
    /// 改了计数器并不会自动让产量变脏，而文化正是驱动全局乘数的那个数。
    /// 每秒都 <c>MarkDirty()</c> 会把 <c>ModifierSet</c> 的"生命周期与状态变更绑定"毁掉
    /// （30fps × 12 小时 = 上百万次全量重算）；一次都不重算则加成永远不生效。
    /// 折中是<b>跨过一个量子才重算</b>：1000 点是乘数阶梯（40 万点满）的 0.25%，
    /// 量化误差远小于玩家能感知的一档。
    /// </para>
    /// </summary>
    public const double DirtyQuantum = 1_000.0;

    /// <summary>
    /// 登记这个计数器的<b>玩家可见名</b>。
    /// <para>
    /// 计数器键是内部标识（英文、下划线），而它会出现在解锁提示、升级效果与纪元规则里。
    /// 不登记的话玩家看到的是「每点「culture」」这种半成品文案。
    /// </para>
    /// </summary>
    /// <param name="builder">内容构建器。</param>
    public void Configure(GameContentBuilder builder)
        => builder.AddCounterName(CounterKey, "文化");

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "culture";

    /// <summary>每个固定步长累加一次文化。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>
    /// 离线结算后补算——离线期间不经过 <see cref="OnTick"/>。<para>
    /// 这里不需要 #9 那种闭式解：文化是纯累加、没有衰减项，一次乘法就是精确值。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    /// <summary>
    /// 「跨入下一个时代」时<b>不清零</b>：建筑重新爬、货币归零，只有文化跟着她走。<para>
    /// 这是这个包与 #9 图书馆最不一样的地方，也是"文明"这个词的落点：
    /// 时代可以重来，记得住的东西不会。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
        // 有意留空：文化的单调性就是这个包的设计。写成空方法而不是删掉，
        // 是为了让"这里想过、并且决定不做"这件事在代码里看得见。
    }

    /// <summary>按当前建筑算出的每秒文化（诊断与测试用）。</summary>
    /// <param name="buildingCounts">各建筑的数量（键 = 建筑 id）。</param>
    /// <returns>该规模下的文化产率。</returns>
    public static double CulturePerSecond(IReadOnlyDictionary<string, int> buildingCounts)
    {
        double total = 0;
        foreach ((string id, double rate) in CulturePerBuilding)
            if (buildingCounts.TryGetValue(id, out int count))
                total += rate * count;
        return total;
    }

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double rate = CulturePerSecond(engine.State.BuildingCounts);
        if (rate <= 0) return;

        double current = engine.State.GetCounter(CounterKey);
        double next = current + (rate * seconds);

        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);

        engine.State.SetCounter(CounterKey, next);
        if (crossedQuantum) engine.MarkDirty();
    }
}
