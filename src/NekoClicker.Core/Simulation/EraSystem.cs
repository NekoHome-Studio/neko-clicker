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
            ? $"，获得 {NumFormat.FormatLong(chips)} {content.PrestigeCurrencyName}"
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
            return $"{NumFormat.FormatLong(Math.Min(currentValue, target))} / {NumFormat.FormatLong(target)}" +
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

    private static string BlockedReason(EraDefinition era, GameEngine engine)
    {
        if (era.CompletionHint.Length > 0) return era.CompletionHint;
        return era.Completion.Describe(engine.Content);
    }
}
