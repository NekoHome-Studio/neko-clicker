using NekoClicker.Core.Content;
using NekoClicker.Content.Lab;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 实验室终局的回归测试（3C-1 补丁）。<para>
/// <b>背景</b>：<c>EndingSystem.Check</c> 每个检查周期都跑一次，而结局条件原本只写
/// <c>EraAtLeast(7)</c>——玩家一进入第 7 批，兜底结局「没有结论」就立刻成立。
/// 可乌托邦 / 共存这两条立场的第三次表态机会在第 7 批里（<c>choice_archive</c>），
/// 于是这两个结局在真实游玩中永远拿不到：终局判定先于最后一次表态发生。
/// </para>
/// <para>
/// <b>修法</b>与设计文档一致：所有结局都要求"第 7 批主线完成"（末层完成后才判定），
/// 而表态的层内门槛远低于完成门槛，所以最后一次表态一定先到手。
/// 两个用例一个守住"承诺过的路不被兜底抢走"，一个守住"不押任何一条路也有收场"。
/// </para>
/// <para>
/// <b>1.6.0 改了什么</b>："有收场"这条路以前是"<b>一次都不答</b> + 宿主报告看过"，
/// 现在必须"<b>每次都答，但每次都挑当前权重最低的立场</b>"——判据从"看过"换成了"作答"，
/// 所以不答的表态会把结局永远拦住。机器人下面那条 <c>AnswerWithoutCommitting</c> 就是这件事。
/// </para>
/// </summary>
public static class LabEndingTests
{
    [Test]
    public static void CommittedEndingSurvivesTheLastBatchChoice()
    {
        GameEngine engine = PlayToEnding(answerUtopia: true);

        Check.AtLeast(
            engine.State.StanceWeight(Stances.Utopia),
            Stances.EndingThreshold,
            "三次乌托邦表态应当都答上了。");
        Check.Equal(
            "end_utopia",
            engine.ReachedEnding?.Id,
            "押满乌托邦之后不该落到兜底结局——终局判定不能抢在最后一次表态之前。");
    }

    /// <summary>
    /// <b>不押任何一条立场也有收场</b>——但 1.6.0 起必须先<b>把六次表态都答掉</b>。<para>
    /// <b>旧名字：<c>FallbackEndingStillArrivesWhenNobodyAnswers</c></b>（1.5.0 的形态是
    /// "一次都不答，靠宿主报告看过解锁"）。现在"什么都不做"会永远等不到结局，
    /// 这条改成"回答但不承诺"：六次摊开，四条立场谁都没到 5 点。
    /// </para>
    /// </summary>
    [Test]
    public static void FallbackEndingStillArrivesForAPlayerWhoCommitsToNothing()
    {
        GameEngine engine = PlayToEnding(answerUtopia: false);

        Check.Equal(
            engine.Content.Choices.Count,
            engine.State.ChoiceAnswers.Count,
            "前提：六次表态都被答掉了（1.6.0 起不答就永远没有结局）。");
        foreach (StanceDefinition stance in engine.Content.Stances)
        {
            Check.AtMost(
                engine.State.StanceWeight(stance.Id),
                Stances.EndingThreshold - 1,
                $"「{stance.Name}」不该被押到门槛——这条测的就是「谁都没承诺」那条路。");
        }

        Check.Equal("end_open", engine.ReachedEnding?.Id, "谁都没承诺也必须有一个收场。");
    }

    /// <summary>
    /// <b>反过来那半：一次都不答就永远没有结局</b>（1.6.0 刻意接受的代价）。<para>
    /// 这条在真实内容上钉住它，免得下一个人把"不答也有兜底"当成缺口顺手修回来。
    /// </para>
    /// </summary>
    [Test]
    public static void NeverAnsweringMeansNoEndingAtAllAndThatIsDeliberate()
    {
        GameEngine engine = PlayToEnding(answerUtopia: null);

        Check.Equal(0, engine.State.ChoiceAnswers.Count, "前提：一次都不答。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "兜底结局的条件已经成立——它在等作答。");
        Check.Null(
            engine.ReachedEnding,
            "一次都不答就永远没有结局：这是 1.6.0 有意移除「不会永远悬着」的代价，不是漏了兜底。");
    }

    [Test]
    public static void LastLayerGate_ReportsTheEndingInsteadOfAPromise()
    {
        GameEngine engine = PlayToEnding(answerUtopia: false);
        string reason = engine.EraGate.BlockedReason ?? string.Empty;

        Check.Contains(reason, "终局", "走完末层主线后，灰按钮应当说明接下来是终局判定。");
        Check.False(
            reason.Contains("后续阶段", StringComparison.Ordinal),
            "终局判定在阶段 3B 就接入了，按钮不能再显示「后续阶段」这种空头承诺。");
    }

    /// <summary>
    /// 机器人从第 1 批跑到终局：每次有待答就作答，完成主线就舍命。<para>
    /// <b>作答策略有三种：</b>
    /// <list type="bullet">
    ///   <item><c>true</c>：能选乌托邦就选（<c>CommittedEndingSurvivesTheLastBatchChoice</c>）。</item>
    ///   <item><c>false</c>：每次都挑<b>当前权重最低</b>的那条立场——"回答，但不承诺任何一条路"
    ///     （<c>FallbackEndingStillArrivesForAPlayerWhoCommitsToNothing</c>）。</item>
    ///   <item><c>null</c>：一次都不答，只让宿主报告"画出来了"——用来证明这条路
    ///     <b>拿不到结局</b>（<c>NeverAnsweringMeansNoEndingAtAllAndThatIsDeliberate</c>）。
    ///     它必须能跑到"结局就绪"，所以预算比另外两条大一些。</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>为什么 must 每轮都作答</b>：1.6.0 起"还有答得上的待答表态"就是落定的唯一障碍，
    /// 而表态是一条条触发的，所以机器人必须在整个跑图过程中反复答；少了它，
    /// 前两条会变成"永远等不到结局"——那不是内容的问题，是用例忘了把玩家该做的事做掉。
    /// </para>
    /// <para>
    /// 最后一批仍用细步长：让「表态门槛与完成门槛落在同一拍」这个致命形状真的出现。
    /// </para>
    /// </summary>
    /// <param name="answerUtopia">
    /// <c>true</c> 押乌托邦；<c>false</c> 只答但不承诺；<c>null</c> 一次都不答。
    /// </param>
    private static GameEngine PlayToEnding(bool? answerUtopia)
    {
        GameEngine engine = TestGame.CreateLab(out _, seed: 20240924);

        // 一次都不答的那条要跑到"结局就绪"（它永远不会落定），所以循环以就绪为终点。
        const int budget = 60_000;

        for (int round = 0; round < budget; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (answerUtopia == true) AnswerUtopiaWherePossible(engine);
            else if (answerUtopia == false) AnswerWithoutCommitting(engine);

            // 宿主把表态画出来了（这一轮里新挂上的那些）。1.6.0 起它只是**诊断**记录：
            // 结局等的是作答，不是"看过"——但宿主该报的照报，那份记录是"从没看过"
            // 与"看过没答"的唯一区分点。
            engine.MarkPendingChoicesShown();

            if (engine.EraGate.CanAdvance) engine.Ascend();

            engine.Simulate(engine.State.Era >= 7 ? 0.25 : 30);

            if (answerUtopia == true) AnswerUtopiaWherePossible(engine);
            else if (answerUtopia == false) AnswerWithoutCommitting(engine);

            if (engine.ReachedEnding is not null) return engine;

            // 一次都不答那条：结局一旦就绪就该收工（它永远等不到落定，再跑只是烧预算）。
            if (answerUtopia is null && EndingSystem.IsReady(engine.Content, engine.State)) return engine;
        }

        Check.Fail($"机器人在 {budget} 轮预算内没能跑到终局。");
        return engine;
    }

    /// <summary>
    /// 待答的就作答：每次都挑<b>当前权重最低</b>的那条立场（同权取先声明的那一项）——
    /// "回答，但不承诺任何一条路"。<para>
    /// 1.6.0 起这条是"回避表态"唯一还能走的路：待答表态不答完，结局永远不落定；
    /// 而六次摊开来（4/4/2/2，门槛 5）四条立场谁都到不了门槛，于是走兜底结局。
    /// </para>
    /// <para>
    /// <b>只能答待答队列里的</b>——<c>AnswerChoice</c> 本身不校验触发条件（它假设调用方
    /// 从视图里拿到的是待答选择），直接答未触发的选择就绕过了 <c>EraId</c> 硬门，
    /// 会造出一个真实玩家走不出来的假路径。
    /// </para>
    /// </summary>
    private static void AnswerWithoutCommitting(GameEngine engine)
    {
        foreach (ChoiceDefinition choice in engine.Content.Choices)
        {
            if (!engine.State.PendingChoices.Contains(choice.Id)) continue;

            ChoiceOption pick = choice.Options
                .OrderBy(o => engine.State.StanceWeight(o.StanceId))
                .First();

            engine.AnswerChoice(choice.Id, pick.Id);
        }
    }

    /// <summary>
    /// 待答的就作答：能押乌托邦就押，没有它的选项就取第一个（不留下未答的表态）。<para>
    /// <b>只能答待答队列里的</b>——<c>AnswerChoice</c> 本身不校验触发条件（它假设调用方
    /// 从视图里拿到的是待答选择），直接答未触发的选择就绕过了 <c>EraId</c> 硬门，
    /// 会造出一个真实玩家走不出来的假路径。
    /// </para>
    /// </summary>
    private static void AnswerUtopiaWherePossible(GameEngine engine)
    {
        foreach (ChoiceDefinition choice in engine.Content.Choices)
        {
            if (!engine.State.PendingChoices.Contains(choice.Id)) continue;

            ChoiceOption pick = choice.Options.FirstOrDefault(
                o => string.Equals(o.StanceId, Stances.Utopia, StringComparison.Ordinal)) ?? choice.Options[0];

            engine.AnswerChoice(choice.Id, pick.Id);
        }
    }
}
