using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

namespace NekoClicker.Core;

/// <summary>
/// 增益系统：限时修饰符的施加、叠加与到期。<para>
/// 叠加规则由内容声明（<see cref="BuffStackMode"/>），引擎不硬编码任何具体增益。
/// 到期只做一件事——从状态里移除并抛事件；产量重算由引擎的脏标记驱动。
/// </para>
/// </summary>
public static class BuffSystem
{
    /// <summary>查找某个增益的当前实例。</summary>
    public static ActiveBuff? Find(GameState state, string buffId)
    {
        for (int i = 0; i < state.Buffs.Count; i++)
            if (string.Equals(state.Buffs[i].Id, buffId, StringComparison.Ordinal))
                return state.Buffs[i];
        return null;
    }

    /// <summary>把基础时长按"增益持续时间"修饰符放大。</summary>
    public static double ScaledDuration(ModifierSet modifiers, string buffId, double baseSeconds)
    {
        double multiplier = modifiers.CombinedMultiplier(
            ModifierTarget.BuffDuration(buffId),
            ModifierTarget.BuffDuration(null));
        return Math.Max(0, baseSeconds) * multiplier;
    }

    /// <summary>施加一个增益，返回其当前实例。</summary>
    public static ActiveBuff Apply(GameState state, GameEventBus events, BuffDefinition definition, double durationSeconds)
    {
        double duration = Math.Max(0, durationSeconds);
        ActiveBuff? existing = Find(state, definition.Id);

        if (existing is null)
        {
            existing = new ActiveBuff
            {
                Id = definition.Id,
                RemainingSeconds = duration,
                TotalSeconds = duration,
                Stacks = 1,
            };
            state.Buffs.Add(existing);
        }
        else
        {
            switch (definition.StackMode)
            {
                case BuffStackMode.Extend:
                    existing.RemainingSeconds += duration;
                    existing.TotalSeconds += duration;
                    break;

                case BuffStackMode.Stack:
                    existing.Stacks = Math.Min(Math.Max(1, definition.MaxStacks), existing.Stacks + 1);
                    existing.RemainingSeconds = Math.Max(existing.RemainingSeconds, duration);
                    existing.TotalSeconds = Math.Max(existing.TotalSeconds, duration);
                    break;

                case BuffStackMode.Refresh:
                default:
                    existing.RemainingSeconds = Math.Max(existing.RemainingSeconds, duration);
                    existing.TotalSeconds = Math.Max(existing.TotalSeconds, duration);
                    break;
            }
        }

        events.Publish(new BuffAppliedEvent(definition.Id, definition.Name, existing.RemainingSeconds, existing.Stacks));
        return existing;
    }

    /// <summary>推进时间并移除到期的增益。返回是否有增益到期（调用方据此打脏标记）。</summary>
    public static bool Tick(GameState state, double deltaSeconds, GameEventBus events)
    {
        bool changed = false;
        for (int i = state.Buffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = state.Buffs[i];
            buff.RemainingSeconds -= deltaSeconds;
            if (buff.RemainingSeconds > 0) continue;
            state.Buffs.RemoveAt(i);
            events.Publish(new BuffExpiredEvent(buff.Id));
            changed = true;
        }
        return changed;
    }

    /// <summary>清空全部增益（转生/重开时用）。</summary>
    public static void Clear(GameState state) => state.Buffs.Clear();
}

/// <summary>成就系统：按条件树评估解锁。</summary>
public static class AchievementSystem
{
    /// <summary>
    /// 检查全部未解锁成就。返回本次新解锁的定义（调用方负责打脏标记）。<para>
    /// 由于指标是实时读取的，解锁"成就数 ≥ N"这类成就会在同一轮里连锁触发——
    /// 这正是原版行为（达成一个里程碑会连带弹出后续成就）。
    /// </para>
    /// </summary>
    public static List<AchievementDefinition> Check(
        GameContent content,
        GameState state,
        IGameMetrics metrics,
        GameEventBus events)
    {
        List<AchievementDefinition>? unlocked = null;

        foreach (AchievementDefinition definition in content.Achievements)
        {
            if (state.Achievements.Contains(definition.Id)) continue;
            if (!definition.Unlock.IsMet(metrics, content)) continue;

            state.Achievements.Add(definition.Id);
            (unlocked ??= []).Add(definition);
            events.Publish(new AchievementUnlockedEvent(definition.Id, definition.Name, definition.Icon));
        }

        return unlocked ?? [];
    }

    /// <summary>查询成就进度；不可量化时返回 (0,0)。</summary>
    public static (double Current, double Target) Progress(AchievementDefinition definition, IGameMetrics metrics)
        => definition.Unlock.TryGetProgress(metrics, out double current, out double target)
            ? (current, target)
            : (0, 0);
}
