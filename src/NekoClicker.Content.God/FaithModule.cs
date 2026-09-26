using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 第二资源「信仰」+ 它的公开仪表「直播在线人数」。<para>
/// 与咖啡馆的幸福感、末世的记忆残片同构：用一个 <see cref="IGameModule"/> 承载，
/// 状态放 <c>GameState.Counters</c>，乘数走已有的
/// <c>Scaling(ScalingSource.CustomCounter)</c> 接缝，解锁条件走 <c>UnlockCondition.Counter</c>。
/// <b>核心零改动</b>——这正是阶段 5 要检验的那条架构不变量。
/// </para>
/// <para>
/// <b>三条设计约束</b>（都来自换皮手册 §4，每一条都对应一个真实踩过的坑）：
/// <list type="number">
///   <item><b>信仰必须单调不减</b>：它要进五层纪元里四层的完成条件，而完成条件会一直显示在
///     灰按钮上（ROADMAP R3）。所以信仰只涨不花——没有任何东西用信仰计价，
///     转生货币另有其人（神格）。</item>
///   <item><b>切换神话体系不清零</b>：那是图书馆包「被阅读度」的专利
///     （<c>ReadershipModule.OnAscend</c>），照抄过来会把这个包的核心指标变成非单调，
///     四层完成条件立刻全部作废。信徒是跑不掉的，换一套神话他们照样烧香。</item>
///   <item><b>产率随层变化</b>：设计文档 §3.4 给第 2 层定的规则是「信仰获取 ×1.5」，
///     而"产率"不是一个能用修饰符表达的东西（信仰不是建筑产量），所以它写在模块里，
///     按层号查一张小表。</item>
/// </list>
/// </para>
/// <para>
/// <b>在线人数为什么要单独一个计数器</b>：设计文档给第 5 层（克苏鲁猫）定的完成条件是
/// 「直播在线人数 ≥ 1e6」，而终局判定每个检查周期都会跑一次，所以这个指标必须单调不减。
/// <b>取历史峰值</b>就同时满足两边：峰值天然只涨，而且"最高同时在线"本来就是直播间
/// 唯一值得炫耀的数字——观众可以走，纪录不会掉。所以它记的是历史最高水位，不是实时人数。
/// （门槛本身照实测下调到了 2e5，那是"量包络"的结果，见 <c>Eras.FinalCompletion</c>。）
/// </para>
/// </summary>
internal sealed class FaithModule : IGameModule
{
    /// <summary>信仰的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "faith";

    /// <summary>「直播在线人数」的计数器键——记的是历史峰值，所以单调不减。</summary>
    public const string ViewerCounterKey = "viewers";

    /// <summary>产出信仰的建筑标签（神殿类：神龛 / 神殿 / 祭坛 / 雷霆殿 / 深渊大教堂）。</summary>
    public const string TempleTag = "temple";

    /// <summary>被计入"在线的直播间"的建筑标签。</summary>
    public const string StreamTag = "stream";

    /// <summary>每座神殿类建筑每秒带来的信仰。</summary>
    public const double FaithPerTemplePerSecond = 0.5;

    /// <summary>每多少座建筑（含非神殿类）每秒带来 1 点信仰——香火不只来自神殿，也来自人气。</summary>
    public const double BuildingsPerFaithPerSecond = 200.0;

    /// <summary>每多少点信仰折算 1 个"在线观众"。</summary>
    public const double FaithPerViewer = 40.0;

    /// <summary>每座直播间额外贡献的在线位（设备、机位、房管）。</summary>
    public const double ViewersPerStudio = 5.0;

    /// <summary>
    /// 触发一次产量重算所需的信仰增量。<para>
    /// <c>Step()</c> 的顺序是「先重算 → 再结算生产 → 最后才 module.OnTick」，模块在 tick 里改了
    /// 计数器<b>不会</b>让产量变脏；而每秒都 <c>MarkDirty()</c> 会把 <c>ModifierSet</c>
    /// "生命周期与状态变更绑定"的设计直接毁掉（30fps × 长跑 = 上千万次全量重算）。
    /// 折中是<b>跨过量子才重算</b>：200 点是信仰乘数阶梯（封顶 10 万）的 0.2%。
    /// </para>
    /// </summary>
    public const double DirtyQuantum = 200.0;

    /// <summary>
    /// 每点信仰带来的全局产量加成（0.01%）。<para>
    /// 写成常量是因为它同时出现在两处：<c>Eras.FaithScaling</c> 的 <c>PerUnit</c>
    /// 与包内用例的端点断言。两处分叉的话，用例就会对着一个过期的数字断言通过。
    /// </para>
    /// </summary>
    public const double FaithPercentPerPoint = 0.0001;

    /// <summary>
    /// 信仰驱动产量时的封顶值（与 <c>Eras.FaithScaling</c> 的 <c>Cap</c> 必须一致）。<para>
    /// 超过它之后乘数已经封顶，再让产量变脏就是纯浪费——30 帧 × 十几小时不是小数。
    /// 取值照实测包络：这个包一次自然游玩结束时信仰约 8e6，
    /// 封顶定在 10 万意味着"前半程靠信仰涨产量、后半程靠体系规则"，
    /// 而不是一个开局几分钟就顶满、之后再无意义的旋钮。
    /// </para>
    /// </summary>
    public const double FaithScalingCap = 100_000.0;

    /// <summary>
    /// 每层的「信仰获取」倍率，按层号索引（第 1 层 = 1.0）。<para>
    /// 只有第 2 层（埃及猫神）非 1：神殿有组织、有账本、有排班的祭司，同一座神殿能收上来更多香火。
    /// 后面几层不叠，因为它们的规则变化落在别处（事件间隔 / 离线上限 / 全局倍率）——
    /// <b>每层只改一处规则</b>，五层才是五种手感，而不是层层加码的一根直线。
    /// </para>
    /// </summary>
    private static readonly double[] EraRateMultiplier = [1.0, 1.5, 1.5, 1.5, 1.5];

    /// <summary>
    /// 登记两个计数器的<b>玩家可见名</b>。<para>
    /// 计数器键是内部标识（英文、下划线），而它会出现在解锁提示、升级效果与「本层规则」里。
    /// 不登记的话玩家看到的是「每点「faith」」这种半成品文案——
    /// 通用守卫 <c>ContentTests.CounterNames_AreRegisteredForEveryReferencedCounter</c> 会真渲染一遍，
    /// 既要求含显示名、又要求不含内部键。
    /// </para>
    /// </summary>
    /// <param name="builder">内容构建器。</param>
    public void Configure(GameContentBuilder builder)
        => builder
            .AddCounterName(CounterKey, "信仰")
            .AddCounterName(ViewerCounterKey, "直播在线人数");

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "faith";

    /// <summary>每个固定步长结算一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>
    /// 离线结算后补算——离线期间不经过 <see cref="OnTick"/>。<para>
    /// 信仰是线性累加的，所以一次乘法就是闭式解，不存在积分漂移
    /// （图书馆包的指数衰减才需要认真写闭式解）。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    /// <summary>
    /// 切换神话体系时<b>什么都不做</b>。<para>
    /// 这是刻意的，而且与图书馆包正好相反：被阅读度在开新书时清零，因为"上一本书的读者不会
    /// 自动读新书"；而信仰是信徒对<b>她本人</b>的信仰，换一套神话只是换了个抬头，
    /// 香火照收。数值上的理由更硬：四层纪元的完成条件都挂在信仰上，
    /// 一旦清零，那四层的进度条会当场倒退到 0，灰按钮开始闪（ROADMAP R3 的禁区）。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
        // 刻意留空：信仰与在线人数都跨层保留。改动这里会同时破坏四层纪元的单调性。
    }

    /// <summary>当前信仰的每秒增量（诊断与用例用）。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本层规则下的每秒信仰。</returns>
    public static double FaithPerSecond(GameEngine engine)
    {
        double temples = engine.Metrics.TaggedBuildingCount(TempleTag);
        double total = engine.State.TotalBuildings();
        double rate = temples * FaithPerTemplePerSecond + total / BuildingsPerFaithPerSecond;
        return rate * RateMultiplierFor(engine.State.Era);
    }

    /// <summary>某层的信仰获取倍率。</summary>
    /// <param name="era">层号（第 1 层 = 1）。</param>
    /// <returns>该层的倍率；层号越界时按 1.0 处理。</returns>
    public static double RateMultiplierFor(int era)
        => era >= 1 && era <= EraRateMultiplier.Length ? EraRateMultiplier[era - 1] : 1.0;

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double current = engine.State.GetCounter(CounterKey);
        double next = current + (FaithPerSecond(engine) * seconds);
        if (next <= current) return;

        // 跨过量子才重算，而且只在乘数还没封顶的时候——封顶之后信仰再怎么涨，产量也不会动。
        bool worthRecomputing = next <= FaithScalingCap;
        bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);

        engine.State.SetCounter(CounterKey, next);

        // 在线人数记的是历史峰值：取 max，于是它天然单调不减，可以进完成条件。
        // 它只服务于解锁条件（不参与任何成长曲线），所以更新它不需要让产量变脏。
        double viewers = next / FaithPerViewer
                         + engine.Metrics.TaggedBuildingCount(StreamTag) * ViewersPerStudio;
        if (viewers > engine.State.GetCounter(ViewerCounterKey))
            engine.State.SetCounter(ViewerCounterKey, viewers);

        if (worthRecomputing && crossedQuantum) engine.MarkDirty();
    }
}
