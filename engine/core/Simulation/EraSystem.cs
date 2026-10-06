using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 分层转生（纪元）系统：舍命按钮的规则、跨层重置、以及每层规则的切换。<para>
/// 设计约束（见 ROADMAP §3）：
/// <list type="bullet">
///   <item><b>R1 只有一个按钮</b>：舍命 = 结算情感能量 + 层号 +1 + 重置本轮。
///     不提供"随时重置变强"的第二条路，否则玩家能在同一层无限刷转生攒货币，
///     再从第 1 层平推到最后一层 —— 分层的意义会被彻底抹掉。</item>
///   <item><b>R2 不做逃生阀</b>：完成条件被强制单调（构建期白名单校验），
///     所以不存在"资源浪费后无法达成"的卡死状态，也就不需要逃生阀。</item>
///   <item><b>R5 允许给 0 情感能量</b>：故事推进绝不以数值增长为条件。</item>
///   <item><b>A2</b>：<c>Era</c> 只通过 BalanceFor / 修饰符来源 / 解锁指标 / 继承参数
///     四个接缝影响引擎，这里不出现任何具体层号的特判。</item>
/// </list>
/// </para>
/// </summary>
public static class EraSystem
{
    /// <summary>
    /// 峰值每秒产量的计数器键。<para>
    /// 完成条件不能用 <c>Cps</c>：增益到期后它会掉下来，灰按钮就会闪烁、进度会倒退。
    /// 引擎每个逻辑步维护这个单调不减的计数器，内容用
    /// <c>UnlockCondition.Counter(GameEngine.PeakCpsCounterKey, n)</c> 引用它。
    /// </para>
    /// </summary>
    public const string PeakCpsCounterKey = "peak_cps";

    /// <summary>求值舍命按钮的状态。UI 直接读它来决定按钮是否置灰。</summary>
    /// <param name="engine">宿主引擎。</param>
    public static EraGate CanAdvance(GameEngine engine)
    {
        GameContent content = engine.Content;
        GameState state = engine.State;

        if (!content.HasEras)
            return new EraGate(false, "本内容包没有分层转生。", 0, state.Era, null);

        if (!content.EraByIndex.TryGetValue(state.Era, out EraDefinition? current))
            return new EraGate(false, $"内容里没有定义第 {state.Era} 层。", 0, state.Era, null);

        bool hasNext = state.Era < content.MaxEraIndex
                       && content.EraByIndex.ContainsKey(state.Era + 1);

        if (!hasNext)
        {
            // 最后一层：不会再有下一层，按钮永远置灰——这不是错误，是设计。
            // 主线完成后给一句诚实的说明：有结局的包说"接下来是终局判定"，
            // 没有结局的包说"这里就是结尾"（别撒谎说"后续阶段接入"，终局判定早就接入了）。
            bool lastMet = current.Completion.IsMet(engine.Metrics, content);
            return new EraGate(
                false,
                lastMet
                    ? content.Endings.Count > 0
                        ? "这是最后一层。主线已完成，接下来是终局判定。"
                        : "这是最后一层。主线已完成，这里就是结尾。"
                    : BlockedReason(current, engine),
                Progress(current, engine),
                state.Era,
                null);
        }

        bool met = current.Completion.IsMet(engine.Metrics, content);
        return new EraGate(
            met,
            met ? null : BlockedReason(current, engine),
            Progress(current, engine),
            state.Era,
            state.Era + 1);
    }

    /// <summary>若现在舍命可得到的情感能量（可能为 0，见 R5）。</summary>
    /// <param name="engine">宿主引擎。</param>
    public static double ChipsOnAdvance(GameEngine engine)
    {
        if (!engine.Content.EraByIndex.TryGetValue(engine.State.Era, out EraDefinition? current)) return 0;

        int level = PrestigeSystem.LevelFor(engine.State.CookiesEarnedAllTime, engine.Balance);
        return Math.Max(0, level - engine.State.PrestigeLevel) * Math.Max(0, current.MetaRewardMultiplier);
    }

    /// <summary>
    /// 舍一命：结算情感能量，进入下一层，重置本轮。<para>
    /// 未完成本层时直接失败并返回原因 —— 调用方（UI）应当把按钮置灰，
    /// 所以正常流程下不会走到这里。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public static AscensionResult Advance(GameEngine engine)
    {
        GameContent content = engine.Content;
        GameState state = engine.State;

        if (!content.HasEras)
            return AscensionResult.Fail("本内容包没有分层转生。");

        EraGate gate = CanAdvance(engine);
        if (!gate.CanAdvance || gate.NextIndex is not { } nextIndex)
            return AscensionResult.Fail(gate.BlockedReason ?? "现在还无法舍命。");

        EraDefinition current = content.EraByIndex[state.Era];
        EraDefinition next = content.EraByIndex[nextIndex];

        // 情感能量：公式全局恒定（R4），只乘本层的奖励倍率；允许为 0（R5）。
        int previousLevel = state.PrestigeLevel;
        int level = PrestigeSystem.LevelFor(state.CookiesEarnedAllTime, engine.Balance);
        double chips = ChipsOnAdvance(engine);

        state.EraHistory[state.Era] = new EraRecord
        {
            Index = state.Era,
            PlayTimeSeconds = Math.Max(0, state.PlayTimeSeconds - state.EraEnteredPlayTimeSeconds),
            CookiesEarned = state.CookiesEarnedThisRun,
            ChipsGained = chips,
            CompletedAt = engine.Clock.UtcNow,
        };
        state.EraCompleted.Add(state.Era);

        state.PrestigeLevel = level;
        state.PrestigeChips = Num.SafeAdd(state.PrestigeChips, chips);
        state.Ascensions++;
        state.Era = nextIndex;
        state.EraEnteredPlayTimeSeconds = state.PlayTimeSeconds;

        int inherited = PrestigeSystem.ResetRun(engine, engine.Balance.KeepAchievementsOnAscend, next);

        engine.MarkDirty();
        engine.Events.Publish(new AscendedEvent(previousLevel, level, chips, state.Ascensions));
        engine.Events.Publish(new EraAdvancedEvent(current.Index, nextIndex, chips, inherited, next.Name));

        string gainedText = chips > 0
            ? $"，获得 {NumFormat.FormatShort(chips)} {content.PrestigeCurrencyName}"
            : "（本层还不足以提升等级，但故事继续）";
        engine.Notify($"舍去第 {current.Index} 命{gainedText}。", NotificationKind.Rare, next.Icon);

        if (next.EntryText.Length > 0)
            engine.Notify(next.EntryText, NotificationKind.Rare, next.Icon);

        foreach (IGameModule module in engine.Modules) module.OnAscend(engine);

        return new AscensionResult
        {
            Success = true,
            Message = $"{current.Name} 结束，进入 {next.Name}{gainedText}。",
            PreviousLevel = previousLevel,
            NewLevel = level,
            ChipsGained = chips,
            Ascensions = state.Ascensions,
            Era = nextIndex,
            EraAdvanced = true,
        };
    }

    /// <summary>
    /// 当前纪元的阶段状态（层内分段）。<para>
    /// <b>派生，不存储</b>：当前阶段 = "声明过的边界里有几条已经成立" + 1。因此
    /// 老存档天生带着自己的阶段进度（指标本来就在存档里），内容改版也不会让存档读不出来
    /// ——最坏情况是少播报或多播报一条提示（见 <see cref="CheckStage"/>）。
    /// </para>
    /// <para>
    /// 只数"成立了几条"而不逐条比较顺序，是因为阶段条件与纪元完成条件一样被强制单调
    /// （构建期白名单）：成立过的永远成立，所以这个计数只增不减。
    /// </para>
    /// </summary>
    public static EraStageGate Stage(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (!content.HasEras) return default;
        if (!content.EraByIndex.TryGetValue(engine.State.Era, out EraDefinition? era)) return default;

        IReadOnlyList<EraStage> stages = era.Stages;
        if (stages.Count == 0) return default;

        int met = 0;
        foreach (EraStage stage in stages)
            if (stage.At.IsMet(engine.Metrics, content)) met++;

        int index = met + 1;                                   // 第 1 阶段 = 刚进这一层
        EraStage? current = index >= 2 ? stages[index - 2] : null;
        EraStage? next = index <= stages.Count ? stages[index - 1] : null;

        // 进度条指向**下一条边界**：阶段是"还差多少进入下一段"，不是"这一层还差多少"。
        double progress = next is null ? 1 : Progress(next.At, engine);

        return new EraStageGate(index, stages.Count + 1, current, next, progress);
    }

    /// <summary>
    /// 阶段提示的计数器键前缀：<c>$era_stage_&lt;纪元 id&gt;</c> 存<b>本层已经播报到第几阶段</b>。<para>
    /// 与 <c>$choice_shown_&lt;id&gt;</c>、<c>$ending_ready_at_play_time</c> 同一个套路：
    /// 键前缀是公开约定，不为此新增存档字段。<b>按纪元 id 分键</b>是必须的——计数器跨层保留
    /// （<c>PrestigeSystem.ResetRun</c> 刻意不清空），只存一个"最高阶段"会让第 2 层一进层
    /// 就以为自己已经播报过第 4 阶段。
    /// </para>
    /// </summary>
    public const string StageCounterPrefix = "$era_stage_";

    /// <summary>
    /// 检查是否跨过了阶段边界，跨过就发一条通知。返回本次新播报的条数（0 或 1）。<para>
    /// 与成就 / 叙事同频执行（<c>GameEngine.Step</c> 的检查块），<b>但不参与任何判定</b>——
    /// 顺序对内容没有影响，所以它排在最后。
    /// </para>
    /// <para>
    /// <b>第一次看见某一层时只记基线、不播报</b>：读档（或内容刚加上阶段）时可能已经站得很靠后，
    /// 补发会让一次读档刷出一串通知。<b>代价是诚实的</b>：老存档读进来不会告诉你"你已经到第 5 阶段了"
    /// ——那件事在面板上写着（<c>EraView.StageIndex</c> 是派生的），只是不会响一声。
    /// 若一次检查跨过多条边界（检查周期内连跨），只播报最高的那一条。
    /// </para>
    /// </summary>
    public static int CheckStage(GameEngine engine)
    {
        GameContent content = engine.Content;
        EraStageGate gate = Stage(engine);
        if (!gate.HasStages) return 0;
        if (!content.EraByIndex.TryGetValue(engine.State.Era, out EraDefinition? era)) return 0;

        // 记基线这一步必须**在"开场阶段"那个提前返回之前**：进层时 Current 是 null（第 1 阶段
        // 没有边界名可报），若在那里就返回，基线永远记不上——于是玩家跨过第一条边界时
        // 引擎才第一次「看见」这一层，把它当成基线悄悄记下，**那一条边界就永远不会被播报**。
        // 这不是理论问题：本文档的作者就是这么写的第一版，而 EraStageTests 抓到了它。
        string key = StageCounterPrefix + era.Id;
        if (!engine.State.Counters.TryGetValue(key, out double announced))
        {
            engine.State.Counters[key] = gate.Index;
            return 0;
        }

        if (gate.Index <= announced) return 0;

        engine.State.Counters[key] = gate.Index;

        // 开场阶段没有名字可报——"这一层开始了"由 EntryText 负责，不该再多一条通知。
        if (gate.Current is not { } stage) return 0;

        engine.Notify(
            $"本层进入第 {gate.Index}/{gate.Count} 阶段：{stage.Name}",
            NotificationKind.Success,
            stage.Icon);
        return 1;
    }

    /// <summary>本层主线进度的显示文本（供 UI 在灰按钮旁展示）。</summary>
    public static string DescribeProgress(GameEngine engine)
    {
        EraGate gate = CanAdvance(engine);
        if (gate.CanAdvance) return "可以舍命";

        GameContent content = engine.Content;
        if (!content.EraByIndex.TryGetValue(engine.State.Era, out EraDefinition? current))
            return gate.BlockedReason ?? string.Empty;

        if (current.Completion.TryGetProgress(engine.Metrics, out double currentValue, out double target) && target > 0)
        {
            return $"{NumFormat.FormatShort(Math.Min(currentValue, target))} / {NumFormat.FormatShort(target)}" +
                   $"（{NumFormat.Percent(Math.Clamp(currentValue / target, 0, 1), 0)}）";
        }

        return gate.BlockedReason ?? string.Empty;
    }

    private static double Progress(EraDefinition era, GameEngine engine)
    {
        if (era.Completion.TryGetProgress(engine.Metrics, out double current, out double target) && target > 0)
            return Math.Clamp(current / target, 0, 1);
        return era.Completion.IsMet(engine.Metrics, engine.Content) ? 1 : 0;
    }

    /// <summary>任意条件的进度（阶段边界复用同一把尺子：与别处的进度条口径一致）。</summary>
    private static double Progress(UnlockCondition condition, GameEngine engine)
    {
        if (condition.TryGetProgress(engine.Metrics, out double current, out double target) && target > 0)
            return Math.Clamp(current / target, 0, 1);
        return condition.IsMet(engine.Metrics, engine.Content) ? 1 : 0;
    }

    private static string BlockedReason(EraDefinition era, GameEngine engine)
    {
        if (era.CompletionHint.Length > 0) return era.CompletionHint;
        return era.Completion.Describe(engine.Content);
    }
}
