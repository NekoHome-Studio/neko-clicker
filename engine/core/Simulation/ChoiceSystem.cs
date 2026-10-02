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
/// <para>
/// 本类还负责另一件与"答不答"无关、但同样只属于引擎的事：<b>待答表态有没有被展示给玩家看过</b>
/// （<see cref="MarkShown"/> / <see cref="PendingAreAllShown"/>）。它由宿主在真的渲染出表态时
/// 通过 <see cref="GameEngine.MarkPendingChoicesShown"/> 报告，是 1.5.0 起结局能否落定的
/// 唯一依据（见 <see cref="EndingSystem.Check"/>）。
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
    /// 三条前置条件缺一不可：选择存在、<b>已经触发过</b>（在待答队列里）、且尚未作答。
    /// </para>
    /// <para>
    /// <b>为什么要校验"已经触发过"</b>：触发条件里带着 <c>EraId</c> 这类硬门，而作答本身
    /// 不重新判条件。这里一旦放行，调用方就能答一个从未出现过的选择——等于绕过那道门，
    /// 造出一条真实玩家走不出来的路径。测试里图省事直接遍历 <c>Content.Choices</c> 作答时
    /// 最容易踩到，所以门要设在引擎这边，而不是指望每个调用方都自觉。
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
        if (state.HasChoice(choiceId)) return false;                // 一次性
        if (!state.PendingChoices.Contains(choiceId)) return false; // 还没触发过（含 EraId 硬门）

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

    /// <summary>"这条表态已经被展示给玩家看过"的计数器键前缀。</summary>
    /// <remarks>
    /// 存在 <see cref="GameState.Counters"/> 里（与 <c>EraSystem.PeakCpsCounterKey</c>、
    /// <c>EndingSystem</c> 的就绪时刻同一个套路）：这样它自动随存档往返，<b>不用改存档格式、
    /// 也不用升存档版本号</b>。键以 <c>$</c> 开头是为了跟内容自定义的计数器划清界限
    /// ——任何选择 id 都拼不出这个前缀。
    /// </remarks>
    internal const string ShownCounterPrefix = "$choice_shown_";

    /// <summary>某条表态"被展示过"的计数器键。</summary>
    internal static string ShownCounterKey(string choiceId) => ShownCounterPrefix + choiceId;

    /// <summary>某条表态是否已经被展示给玩家看过。</summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="choiceId">选择 id。</param>
    internal static bool IsShown(GameState state, string choiceId)
        => state.GetCounter(ShownCounterKey(choiceId)) > 0;

    /// <summary>
    /// 此刻仍挂在待答队列里的表态是否<b>全部</b>都被展示过。<para>
    /// 队列为空时返回 <c>true</c>——"没有要展示的东西"不等于"有东西没展示"。
    /// </para>
    /// </summary>
    /// <param name="state">游戏状态。</param>
    internal static bool PendingAreAllShown(GameState state)
    {
        foreach (string id in state.PendingChoices)
            if (!IsShown(state, id)) return false;

        return true;
    }

    /// <summary>
    /// 把此刻挂着的待答表态全部标记为"已展示"。<para>
    /// <b>只标此刻挂着的</b>：之后才触发的表态不会被这一发连坐——那正是"展示过"这句话
    /// 必须逐条记账、而不是存一个布尔值的原因。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次新标记的条数；都已标记过（或没有待答表态）时为 <c>0</c>。</returns>
    internal static int MarkShown(GameEngine engine)
    {
        GameState state = engine.State;
        int marked = 0;

        foreach (string id in state.PendingChoices)
        {
            if (IsShown(state, id)) continue;
            state.SetCounter(ShownCounterKey(id), 1);
            marked++;
        }

        return marked;
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
