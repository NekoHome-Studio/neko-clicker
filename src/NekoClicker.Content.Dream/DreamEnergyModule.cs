using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 第二资源「梦境能量」：<b>只涨不落的计数器</b>，只有梦层类建筑产得出它。<para>
/// 规则一条：
/// <code>
/// dE/dt = Σ(梦层类建筑数量 × 该建筑的每秒梦境能量)
/// </code>
/// 产率表写在 <see cref="RateByBuilding"/> 里，按建筑线逐档抬升（12 → 1200/s）——
/// <b>它不是又一个"幸福感"</b>：这里的产率贴着建筑线增长，所以"更深的那一层"
/// 本身就是"更浓的梦"，玩家把主力换成梦核的时候，梦境能量会跟着跳一个量级。
/// 数值是<b>先量包络再设值</b>定的（见 <c>docs/CONTENT_AUTHORING.md</c> §12.2）：
/// 常数倍率不影响任何相对节奏，只决定"结局门槛"落在哪一段真实游玩时长上。
/// </para>
/// <para>
/// <b>为什么是单调不减</b>：手册 §4 第 15 条把"会衰减的第二资源"划给了 #9 图书馆。
/// 这个包的语义也不需要衰减——梦会留在她身上，所以
/// <see cref="OnAscend"/> 里<b>不清零</b>：往下睡一层，梦不会变淡。
/// 这也让它可以安全地进解锁条件与成就门槛（有进度条可看）。
/// </para>
/// <para>
/// <b>它怎么影响产量</b>：内容侧在每一层的 <c>Modifiers</c> 里写
/// <c>GlobalPercent(Scaling(CustomCounter, …))</c>，于是"越深的梦越大"不是一句文案，
/// 而是一条真的曲线。给成长型修饰符的端点断言写在
/// <c>DreamContentTests.DeepDreamScaling_HitsTheEndpointsWrittenInTheText</c> 里——
/// 手册 §8 的坑：<c>Cap</c> 限的是原始计数值，不是加成结果。
/// </para>
/// </summary>
internal sealed class DreamEnergyModule : IGameModule
{
    /// <summary>梦境能量的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "dream_energy";

    /// <summary>产出梦境能量的建筑标签。</summary>
    public const string DreamLayerTag = "dream_layer";

    /// <summary>
    /// 触发一次产量重算所需的梦境能量增量。<para>
    /// <c>Step()</c> 的顺序是「先重算，再结算生产，最后才 module.OnTick」——模块在 tick 里
    /// 改了计数器并不会让产量变脏。每帧都 <c>MarkDirty</c> 又会把 <c>ModifierSet</c> 的
    /// "生命周期与状态变更绑定"直接毁掉（30fps × 长跑 = 上千万次全量重算）。
    /// 折中是<b>跨过量子才重算</b>：200 点是乘数阶梯的 0.5%，量化误差不到量程的 0.2%。
    /// </para>
    /// </summary>
    public const double DirtyQuantum = 200.0;

    /// <summary>
    /// 各建筑每秒产出的梦境能量。<para>
    /// 只有带 <see cref="DreamLayerTag"/> 标签的建筑出现在这张表里——「枕头」是现实里的东西，
    /// 它不产梦；表里没有的建筑一律按 0 计。产率与建筑线同阶，所以"换主力"这件事
    /// 在第二资源上立刻看得见。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, double> RateByBuilding = new(StringComparer.Ordinal)
    {
        ["dream_layer"] = 12,
        ["nightmare_nest"] = 30,
        ["lucid_zone"] = 75,
        ["dream_weaver"] = 190,
        ["nesting_tower"] = 480,
        ["dream_core"] = 1_200,
    };

    /// <summary>某座建筑每秒产出的梦境能量；不产梦的建筑返回 0。</summary>
    /// <param name="buildingId">建筑 id。</param>
    /// <returns>每秒梦境能量。</returns>
    public static double RateFor(string buildingId) => RateByBuilding.GetValueOrDefault(buildingId);

    /// <summary>整张产率表（公开给内容包的入口转出去，供测试与文案引用）。</summary>
    public static IReadOnlyDictionary<string, double> RatePerBuilding => RateByBuilding;

    /// <summary>
    /// 登记这个计数器的<b>玩家可见名</b>。<para>
    /// 计数器键是内部标识（英文、下划线），而它会出现在解锁提示、升级效果与纪元规则里。
    /// 不登记的话玩家看到的是「每点「dream_energy」」这种半成品文案。
    /// </para>
    /// </summary>
    /// <param name="builder">内容构建器。</param>
    public void Configure(GameContentBuilder builder)
        => builder.AddCounterName(CounterKey, "梦境能量");

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "dream_energy";

    /// <summary>每个固定步长结算一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Advance(engine, deltaSeconds);

    /// <summary>
    /// 离线结算后补算——离线期间不经过 <see cref="OnTick"/>。<para>
    /// 用 <c>CreditedSeconds</c> 而不是真实离线时长：与补发收益的上限保持一致，
    /// 否则"睡了三天"能换来的梦境能量会比同一段时间的收益还多。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Advance(engine, progress.CreditedSeconds);

    /// <summary>
    /// 往下睡一层时<b>不清零</b>——梦会留在她身上。<para>
    /// 这是与 #9 图书馆"被阅读度"最关键的一处不同，也是这个包能把它写进解锁条件的全部理由。
    /// 方法保留为空实现是为了让"想过这件事"留在代码里：清不清零是内容决定，不是默认行为。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
    }

    /// <summary>当前每秒梦境能量（诊断与测试用）。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>每秒梦境能量。</returns>
    public static double RatePerSecond(GameEngine engine)
    {
        double rate = 0;
        foreach (BuildingDefinition building in engine.Content.Buildings)
        {
            double perBuilding = RateFor(building.Id);
            if (perBuilding <= 0) continue;
            if (!building.Tags.Contains(DreamLayerTag, StringComparer.Ordinal)) continue;
            rate += engine.State.BuildingCount(building.Id) * perBuilding;
        }
        return rate;
    }

    private static void Advance(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double gain = RatePerSecond(engine) * seconds;
        if (gain <= 0) return;

        double current = engine.State.GetCounter(CounterKey);
        double next = current + gain;

        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);
        engine.State.SetCounter(CounterKey, next);
        if (crossedQuantum) engine.MarkDirty();
    }
}
