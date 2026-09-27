using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

namespace NekoClicker.Core;

/// <summary>
/// 选择与立场系统。<para>
/// 与成就 / 叙事同频执行（复用 <c>GameBalance.AchievementCheckInterval</c>），
/// 做的是同一件事——"把一堆条件树定期扫一遍，满足的就激活"。
/// </para>
/// <para>
/// <b>选择不阻塞</b>（ROADMAP R6）：触发后只进待答队列，玩家可以一直不答。
/// 未作答的选择<b>不产生任何效果</b>——不累加立场、选项修饰符也不生效。
/// 这是刻意的：把"回避表态"也做成一种合法玩法，而不是拿弹窗逼玩家点。
/// </para>
/// </summary>
public static class ChoiceSystem
{
    /// <summary>扫描尚未触发 / 尚未作答的选择，把满足条件者放进待答队列。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次新触发的选择。</returns>
    public static List<ChoiceDefinition> Check(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (content.Choices.Count == 0) return [];

        GameState state = engine.State;
        List<ChoiceDefinition>? triggered = null;

        foreach (ChoiceDefinition choice in content.Choices)
        {
            if (state.HasChoice(choice.Id)) continue;              // 已经答过
            if (state.PendingChoices.Contains(choice.Id)) continue; // 已经挂着等答

            // EraId 提供的是 Trigger 表达不了的能力："当前正处在 id 为 X 的那一层"。
            // NumericMetric.Era 只有层号，认不出"哪个世界"，而层号会随内容改版变化。
            if (choice.EraId.Length > 0)
            {
                if (!content.EraByIndex.TryGetValue(state.Era, out EraDefinition? current)) continue;
                if (!string.Equals(current.Id, choice.EraId, StringComparison.Ordinal)) continue;
            }

            if (!choice.Trigger.IsMet(engine.Metrics, content)) continue;

            state.PendingChoices.Add(choice.Id);
            (triggered ??= []).Add(choice);
            engine.Events.Publish(new ChoiceTriggeredEvent(choice.Id, choice.Speaker, choice.Prompt));
        }

        return triggered ?? [];
    }

    /// <summary>
    /// 作答一次选择。<para>
    /// 一次性语义：答过就不能再答（<see cref="GameState.ChoiceAnswers"/> 里已有记录时返回 <c>false</c>）。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="choiceId">选择 id。</param>
    /// <param name="optionId">选中的选项 id。</param>
    /// <returns>是否确实完成了这次作答。</returns>
    public static bool Answer(GameEngine engine, string choiceId, string optionId)
    {
        GameContent content = engine.Content;
        if (!content.ChoiceById.TryGetValue(choiceId, out ChoiceDefinition? choice)) return false;

        GameState state = engine.State;
        if (state.HasChoice(choiceId)) return false; // 一次性

        ChoiceOption? option = null;
        foreach (ChoiceOption candidate in choice.Options)
        {
            if (!string.Equals(candidate.Id, optionId, StringComparison.Ordinal)) continue;
            option = candidate;
            break;
        }
        if (option is null) return false; // 选项不属于这次选择

        string? previousDominant = DominantStance(content, state);

        state.PendingChoices.Remove(choiceId);
        state.ChoiceAnswers[choiceId] = option.Id;

        if (option.StanceId.Length > 0)
        {
            int weight = Math.Max(0, option.Weight);
            state.StanceWeights[option.StanceId] = state.StanceWeight(option.StanceId) + weight;
        }

        // 立场可能换了主导者、选项本身可能带修饰符 —— 两者都影响产量，必须重算。
        engine.MarkDirty();
        engine.Events.Publish(new ChoiceMadeEvent(choiceId, option.Id, option.StanceId, option.OutcomeText));

        string? currentDominant = DominantStance(content, state);
        if (!string.Equals(previousDominant, currentDominant, StringComparison.Ordinal))
            engine.Events.Publish(new DominantStanceChangedEvent(previousDominant, currentDominant));

        return true;
    }

    /// <summary>
    /// 当前的主导立场：权重最高者；全部为 0 时返回 <c>null</c>。<para>
    /// 权重相同时取<b>先声明</b>的那一个——必须是确定性的，否则同一个存档
    /// 两次读出的主导立场可能不同，产量就会莫名其妙地跳。
    /// </para>
    /// </summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static string? DominantStance(GameContent content, GameState state)
    {
        string? best = null;
        int bestWeight = 0;

        foreach (StanceDefinition stance in content.Stances)
        {
            int weight = state.StanceWeight(stance.Id);
            if (weight <= 0 || weight <= bestWeight) continue;
            bestWeight = weight;
            best = stance.Id;
        }

        return best;
    }

    /// <summary>尚未作答的选择（含已触发待答与尚未触发的）。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static List<ChoiceDefinition> Unanswered(GameContent content, GameState state)
    {
        List<ChoiceDefinition> result = [];
        foreach (ChoiceDefinition choice in content.Choices)
            if (!state.HasChoice(choice.Id)) result.Add(choice);
        return result;
    }

    /// <summary>某次选择当前选中的选项定义；未作答返回 <c>null</c>。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    /// <param name="choiceId">选择 id。</param>
    public static ChoiceOption? AnswerOf(GameContent content, GameState state, string choiceId)
    {
        string? optionId = state.AnswerOf(choiceId);
        if (optionId is null) return null;
        if (!content.ChoiceById.TryGetValue(choiceId, out ChoiceDefinition? choice)) return null;

        foreach (ChoiceOption option in choice.Options)
            if (string.Equals(option.Id, optionId, StringComparison.Ordinal)) return option;

        return null;
    }
}
