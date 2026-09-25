using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

namespace NekoClicker.Core;

/// <summary>
/// 叙事释放系统：按条件把剧情一条条放出来，并维护图鉴状态。<para>
/// 与成就系统同频执行（复用 <c>GameBalance.AchievementCheckInterval</c>），
/// 因为两者做的是同一件事——"把一堆条件树定期扫一遍，满足的就激活"。
/// </para>
/// </summary>
public static class LoreSystem
{
    /// <summary>
    /// 检查全部未释放的叙事条目。<para>
    /// 通道决定打扰方式：<see cref="LoreChannel.Log"/> 进通知栏、
    /// <see cref="LoreChannel.Popup"/> 进待处理队列等玩家点掉、
    /// <see cref="LoreChannel.Codex"/> 静默进图鉴。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次新释放的条目。</returns>
    public static List<LoreEntry> Check(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (content.LoreEntries.Count == 0) return [];

        GameState state = engine.State;
        List<LoreEntry>? revealed = null;

        foreach (LoreEntry entry in content.LoreEntries)
        {
            if (state.LoreUnlocked.Contains(entry.Id)) continue;
            if (!entry.Reveal.IsMet(engine.Metrics, content)) continue;

            state.LoreUnlocked.Add(entry.Id);
            (revealed ??= []).Add(entry);
            engine.Events.Publish(new LoreRevealedEvent(entry.Id, entry.Title, entry.Icon, entry.StorylineId, entry.Channel));

            switch (entry.Channel)
            {
                case LoreChannel.Log:
                    engine.Notify(entry.Body, NotificationKind.Info, entry.Icon);
                    break;

                case LoreChannel.Popup:
                    // 弹窗不写进通知栏——它是"待处理"的，由 UI 点掉后调用 DismissLorePopup。
                    state.PendingLorePopups.Add(entry.Id);
                    break;

                case LoreChannel.Codex:
                case LoreChannel.EraText:
                default:
                    break; // 静默进图鉴
            }
        }

        return revealed ?? [];
    }

    /// <summary>点掉一个弹窗。返回它是否确实处于待处理状态。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="entryId">条目 id。</param>
    public static bool DismissPopup(GameEngine engine, string entryId)
        => engine.State.PendingLorePopups.Remove(entryId);

    /// <summary>点掉当前全部弹窗。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>被点掉的数量。</returns>
    public static int DismissAllPopups(GameEngine engine)
    {
        int count = engine.State.PendingLorePopups.Count;
        engine.State.PendingLorePopups.Clear();
        return count;
    }

    /// <summary>某条剧情线已解锁的条数。</summary>
    public static int CountUnlocked(GameContent content, GameState state, string storylineId)
    {
        int count = 0;
        foreach (LoreEntry entry in content.LoreEntries)
        {
            if (!string.Equals(entry.StorylineId, storylineId, StringComparison.Ordinal)) continue;
            if (state.LoreUnlocked.Contains(entry.Id)) count++;
        }
        return count;
    }
}
