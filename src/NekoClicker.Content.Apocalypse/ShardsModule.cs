using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 第二资源「记忆残片」：<b>只涨不花</b>，而且它只由<b>上一次重启留下来的东西</b>产出。<para>
/// 这是这个包唯一的原创机制，也是它相对其它四个包真正不同的地方：
/// 幸福感由整间店的规模驱动、伦理值由「有谁在看」驱动、士气由团队养也被加班吃，
/// 而记忆残片的产率 = <b>上一次重启时活下来的座位数 ÷ 5</b>（每秒）。
/// </para>
/// <para>
/// <b>为什么这么设计</b>：
/// <list type="bullet">
///   <item>它让「继承」这件事在数值上真的有用——阶段 4 要验证的核心能力不是"建筑不会被清空"
///     这样一个静态事实，而是"留下的东西会继续影响下一轮"。每次重启时继承了多少，
///     决定了下一轮的记忆产出速度。</item>
///   <item>它把"多攒点再重启"变成一个真实的取舍：重启前每多买一座建筑，下一轮的记忆就厚一点，
///     但晚重启本身要花时间。这个取舍不需要任何新机制。</item>
///   <item>它天然是单调不减的（只增不减），所以可以直接进解锁条件、成就与成长曲线，
///     不会像士气那样在灰按钮上倒退。</item>
/// </list>
/// </para>
/// <para>
/// <b>第一纪元产率恒为 0</b>——还没有任何东西被留下来。这是刻意的：
/// 「记忆」这个概念在第 1 轮还不存在，等到她第一次重启、发现有些东西没被忘掉，它才开始。
/// 因此本包的纪元完成条件一律不引用记忆残片（否则第 1 轮会永远走不出去），
/// 只把它用在解锁、成就与倍率上。
/// </para>
/// </summary>
internal sealed class ShardsModule : IGameModule
{
    /// <summary>记忆残片的计数器键（存档键，同时被解锁条件与成长曲线引用）。</summary>
    public const string CounterKey = "memory_shards";

    /// <summary>「上一次重启留下来的座位数」的计数器键——记忆产率的来源。</summary>
    public const string SeatsKey = "memory_seats";

    /// <summary>每多少个留下来的座位，每秒产出 1 片记忆残片。</summary>
    public const double SeatsPerShardPerSecond = 5.0;

    /// <summary>模块名（用于诊断）。</summary>
    public string Name => "memory_shards";

    /// <summary>每个固定步长结算一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <summary>离线结算后补算——离线期间不经过 <see cref="OnTick"/>。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细（用 <c>CreditedSeconds</c>，与补发收益的上限保持一致）。</param>
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    /// <summary>
    /// 重启之后记下"这一轮留下了多少"。<para>
    /// 引擎保证本方法在 <c>ResetRun</c> <b>之后</b>调用（<c>EraSystem.Advance</c> 与
    /// <c>PrestigeSystem.Ascend</c> 都是这个顺序），所以此刻的建筑总数就是继承下来的数量——
    /// 不需要再订阅事件、也不需要自己去算比例。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public void OnAscend(GameEngine engine)
    {
        int survived = 0;
        foreach (int count in engine.State.BuildingCounts.Values) survived += count;
        engine.State.SetCounter(SeatsKey, survived);
    }

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;

        double seats = engine.State.GetCounter(SeatsKey);
        if (seats <= 0) return;

        double current = engine.State.GetCounter(CounterKey);
        engine.State.SetCounter(CounterKey, current + seats / SeatsPerShardPerSecond * seconds);
    }
}
