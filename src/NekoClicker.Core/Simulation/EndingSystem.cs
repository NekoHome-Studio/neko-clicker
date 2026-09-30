using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 终局判定：主线走完之后，按条件树挑出<b>一个</b>结局。<para>
/// 与成就 / 叙事 / 选择同频执行——还是同一件事，"把条件树定期扫一遍"。
/// </para>
/// <para>
/// 判定是<b>一次性</b>的：一旦记下结局就不再判。这既是"互斥"的实现方式，
/// 也避免了条件恒真导致的重复触发（立场权重、图鉴条数都是单调不减的，
/// 达成之后会一直成立）。
/// </para>
/// <para>
/// <b>作答宽限</b>（<see cref="GraceSeconds"/>）：结局条件成立时若还有未作答的表态，
/// 判定会先等一等——因为"最后几次表态"往往就发生在结局条件成立前的几秒里，
/// 而那时候玩家很可能还没反应过来。<b>这是唯一会让落定推迟的机制</b>，
/// 它只会推迟、不会阻止：宽限期一到就照常落定（见 <see cref="Check"/>）。
/// </para>
/// </summary>
public static class EndingSystem
{
    /// <summary>
    /// 结局条件成立后、<b>落定之前</b>留给玩家作答的宽限（模拟秒）。<para>
    /// <b>为什么需要它</b>：《猫娘实验室》的末次表态门槛是 3.4e8，末层完成门槛是 1e9。
    /// 两者之间只隔几秒——终局判定一旦在这几秒里成立就永久锁死，于是"押了两次乌托邦、
    /// 第三次还在想"的玩家会拿到兜底结局，而他明明什么都没做错。这不是执行顺序问题
    /// （玩家的作答永远发生在触发它的那一拍之外），而是<b>缺一段等待</b>。
    /// </para>
    /// <para>
    /// <b>为什么用模拟秒</b>：这个框架的既有语义是"世界在你不看的时候也在前进"
    /// （离线收益、离线期间的计数器都按这个来），所以宽限也走模拟时间，是同一套世界观，
    /// 而不是新学一条规则。代价是挂机跨过宽限期回来会发现结局已经自己落定了——
    /// 这正是想要的行为。
    /// </para>
    /// </summary>
    public const double GraceSeconds = 30.0;

    /// <summary>
    /// "结局已就绪"的累计游玩秒数；<c>0</c> 表示尚未就绪。<para>
    /// 存在 <see cref="GameState.Counters"/> 里而不是新增一个状态字段：这样它自动随存档往返、
    /// 不用改存档格式、也不用升存档版本号（<c>EraSystem.PeakCpsCounterKey</c> 是同一个套路）。
    /// </para>
    /// <para>
    /// 键以 <c>$</c> 开头，是为了跟 <see cref="CounterKey"/>（<c>ending_&lt;id&gt;</c>）划清界限：
    /// 任何结局 id 都拼不出这个键，不会撞名。
    /// </para>
    /// </summary>
    private const string ReadyAtCounterKey = "$ending_ready_at_play_time";

    /// <summary>扫描结局条件，达成第一个就记下。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次达成的结局；没有则返回 <c>null</c>。</returns>
    public static EndingDefinition? Check(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (content.Endings.Count == 0) return null;

        GameState state = engine.State;
        if (state.EndingsReached.Count > 0) return null; // 一份存档只有一个结局

        // 就绪时刻 = 第一次"有结局条件成立"的那一拍。记下来就不再改：
        // 从那一刻起算宽限，条件即使中途抖动也不会把宽限重置（只会让它更早到点）。
        double readyAt = state.GetCounter(ReadyAtCounterKey);
        bool justBecameReady = readyAt <= 0;

        if (justBecameReady)
        {
            if (!AnyConditionMet(engine)) return null; // 还没够条件，没什么可等的

            readyAt = state.PlayTimeSeconds;
            state.SetCounter(ReadyAtCounterKey, readyAt);
        }

        // 还有表态挂着没答 → 先等玩家。等满宽限期就当"放弃表态"处理，照常落定。
        if (state.PendingChoices.Count > 0 && state.PlayTimeSeconds - readyAt < GraceSeconds)
        {
            if (justBecameReady)
            {
                engine.Notify(
                    $"主线已经走完，但你还有 {state.PendingChoices.Count} 项表态没答。"
                    + $"它们会决定你落到哪个结局——{NumFormat.Duration(GraceSeconds)}内还可以改。",
                    NotificationKind.Warning,
                    "⏳");
            }

            return null;
        }

        // 按 Priority 升序；同优先级按声明顺序（OrderBy 是稳定排序）。
        foreach (EndingDefinition ending in content.Endings.OrderBy(e => e.Priority))
        {
            if (!ending.Condition.IsMet(engine.Metrics, content)) continue;

            state.EndingsReached.Add(ending.Id);
            state.SetCounter(CounterKey(ending.Id), 1);

            engine.MarkDirty();
            engine.Events.Publish(new EndingReachedEvent(ending.Id, ending.Name, ending.Icon, ending.Text));
            return ending;
        }

        return null;
    }

    /// <summary>是否已有结局条件成立、但还没落定（即正处在作答宽限期里）。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static bool IsReady(GameContent content, GameState state)
        => content.Endings.Count > 0
           && state.EndingsReached.Count == 0
           && state.GetCounter(ReadyAtCounterKey) > 0;

    /// <summary>距宽限期结束还剩多少模拟秒；未就绪时为 <c>0</c>。</summary>
    /// <param name="state">游戏状态。</param>
    public static double GraceRemaining(GameState state)
    {
        double readyAt = state.GetCounter(ReadyAtCounterKey);
        if (readyAt <= 0) return 0;

        return Math.Max(0, GraceSeconds - (state.PlayTimeSeconds - readyAt));
    }

    /// <summary>是否有任何一个结局的条件已经成立。</summary>
    private static bool AnyConditionMet(GameEngine engine)
    {
        foreach (EndingDefinition ending in engine.Content.Endings)
            if (ending.Condition.IsMet(engine.Metrics, engine.Content)) return true;

        return false;
    }

    /// <summary>当前存档已达成的结局；未达成返回 <c>null</c>。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static EndingDefinition? Reached(GameContent content, GameState state)
    {
        foreach (string id in state.EndingsReached)
            if (content.EndingById.TryGetValue(id, out EndingDefinition? ending)) return ending;

        return null;
    }

    /// <summary>结局计数器的键（约定：<c>ending_&lt;id&gt;</c>）。</summary>
    /// <param name="endingId">结局 id。</param>
    public static string CounterKey(string endingId) => "ending_" + endingId;
}
