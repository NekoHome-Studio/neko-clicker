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
/// 本类还负责另一件与"答不答"无关、但同样只属于引擎的事：<b>哪一批待答表态还拦着结局</b>
/// （<see cref="AnswerablePendingCount"/>）。判据是"这条表态此刻是否<b>答得上</b>"，
/// 不是"玩家看过它没有"——1.6.0 起，结局要等的是<b>作答</b>（见 <see cref="EndingSystem.Check"/>）。
/// </para>
/// <para>
/// 另有一份<b>只作诊断</b>的记录：<see cref="MarkShown"/>（由宿主的
/// <see cref="GameEngine.MarkPendingChoicesShown"/> 报告）记下"这条表态有没有被画给玩家看过"。
/// 它<b>不参与任何判定</b>，只回答"这个存档里的表态到底露过面没有"——因为
/// "结局一直没落定"在新规则下是一件真实可能的事，而"从没被展示过"与"看过但没答"
/// 是两种完全不同的原因，没有这份记录就分不出来。
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
    /// <para>
    /// <b>1.6.0 起它只是诊断信息</b>：判定读的是"答没答"（见 <see cref="AnswerablePendingCount"/>），
    /// 不是"看没看过"。宿主可以按 <c>$choice_shown_&lt;id&gt;</c> 读它来区分
    /// "玩家从没看到这条表态"与"看到了但没答"。
    /// </para>
    /// </remarks>
    internal const string ShownCounterPrefix = "$choice_shown_";

    /// <summary>某条表态"被展示过"的计数器键。</summary>
    internal static string ShownCounterKey(string choiceId) => ShownCounterPrefix + choiceId;

    /// <summary>某条表态是否已经被展示给玩家看过。<b>诊断用，不参与判定。</b></summary>
    /// <param name="state">游戏状态。</param>
    /// <param name="choiceId">选择 id。</param>
    internal static bool IsShown(GameState state, string choiceId)
        => state.GetCounter(ShownCounterKey(choiceId)) > 0;

    /// <summary>
    /// 此刻挂在待答队列里、而且玩家<b>仍然答得上</b>的表态条数（1.6.0 起这就是"拦不拦结局"的判据）。<para>
    /// <b>为什么不能只数 <see cref="GameState.PendingChoices"/> 的长度</b>：队列里的 id 有可能
    /// <b>永远答不上</b>，那样"必须答完"就会把这一局永久卡死（见
    /// <see cref="GameEngine.CheckEnding"/>）。两种情形都真实存在，而且都只在<b>读档</b>时才出现：
    /// </para>
    /// <list type="number">
    ///   <item><b>内容里已经没有这条表态了</b>（换内容包，或包改版时重命名 / 删掉了选择 id）。
    ///     存档格式里只存 id，没有包标识，<c>SaveSerializer.Apply</c> 也不做存在性过滤，
    ///     所以旧存档会带着一个当前内容不认识的 id 一直挂在待答队列里；
    ///     它既画不出来（<c>GameViewFactory.BuildPendingChoices</c> 的 <c>FindChoice</c> 会跳过），
    ///     也答不上（<see cref="Answer"/> 要在 <c>Content.ChoiceById</c> 里找得到它）。</item>
    ///   <item><b>这条 id 同时又出现在已答表里</b>（手改的存档、或有缺陷的迁移）。
    ///     <see cref="Answer"/> 对"已经答过"直接返回 <c>false</c>，于是它同样永远答不上。</item>
    /// </list>
    /// <para>
    /// 这两种都<b>不拦结局</b>，而且不拦这件事必须<b>说出来</b>（引擎会发一条警告通知）——
    /// 静默地忽略一批玩家看得见的待答项，正是本项目最反对的失败形态。
    /// 反过来，"挂在队列里、内容里也有、且还没答过"的表态一律拦着：这就是 1.6.0 的规则。
    /// </para>
    /// </summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    internal static int AnswerablePendingCount(GameContent content, GameState state)
    {
        int count = 0;

        foreach (string id in state.PendingChoices)
        {
            if (state.HasChoice(id)) continue;                 // 已经答过（重复出现在队列里）
            if (!content.ChoiceById.ContainsKey(id)) continue; // 内容里没有它 → 答不上

            count++;
        }

        return count;
    }

    /// <summary>
    /// 此刻挂在待答队列里、但玩家<b>已经答不上</b>的表态条数（只用于把这件事喊出来）。<para>
    /// 判据与 <see cref="AnswerablePendingCount"/> 互补；为什么要区分见那一条的注释。
    /// </para>
    /// </summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    internal static int UnanswerablePendingCount(GameContent content, GameState state)
    {
        int count = 0;

        foreach (string id in state.PendingChoices)
        {
            if (state.HasChoice(id)) { count++; continue; }
            if (!content.ChoiceById.ContainsKey(id)) count++;
        }

        return count;
    }

    /// <summary>
    /// 把此刻挂着的待答表态全部标记为"已展示"。<para>
    /// <b>只标此刻挂着的</b>：之后才触发的表态不会被这一发连坐——那正是"展示过"这句话
    /// 必须逐条记账、而不是存一个布尔值的原因。
    /// </para>
    /// <para>
    /// <b>1.6.0 起它不再影响任何判定</b>（结局等的是作答，不是"看过"）；它留下的是一份
    /// 可以事后回答"这条表态到底露过面没有"的诊断记录，随存档往返。
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
