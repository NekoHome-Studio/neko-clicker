using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

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
/// </summary>
public static class EndingSystem
{
    /// <summary>扫描结局条件，达成第一个就记下。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次达成的结局；没有则返回 <c>null</c>。</returns>
    public static EndingDefinition? Check(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (content.Endings.Count == 0) return null;

        GameState state = engine.State;
        if (state.EndingsReached.Count > 0) return null; // 一份存档只有一个结局

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
