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
/// 这两个用例一个守住"承诺过的路不被兜底抢走"，一个守住"一次都不答也有结局"。
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

    [Test]
    public static void FallbackEndingStillArrivesWhenNobodyAnswers()
    {
        GameEngine engine = PlayToEnding(answerUtopia: false);
        Check.Equal("end_open", engine.ReachedEnding?.Id, "一次都不表态也必须有一个收场。");
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
    /// 机器人从第 1 批跑到终局：每次有待答就作答（能选乌托邦就选），完成主线就舍命，
    /// 并且像宿主那样每轮报告一次「待答表态已经画给玩家看过了」。<para>
    /// <b>最后那一步是 1.5.0 起结局能否落定的唯一条件</b>（原来是 30 模拟秒的定时宽限）。
    /// 少了它，<c>FallbackEndingStillArrivesWhenNobodyAnswers</c> 那条「一次都不答也有收场」
    /// 会变成「永远等不到结局」——而那不是内容的问题，是这条用例忘了把宿主该做的事做掉。
    /// </para>
    /// <para>
    /// 最后一批仍用细步长：让「表态门槛与完成门槛落在同一拍」这个致命形状真的出现。
    /// （粗步长不会再吞掉最后一次表态了——1.5.0 之前靠定时宽限兜住的那件事，
    /// 现在由「没被展示过就不落定」兜住。）
    /// </para>
    /// </summary>
    private static GameEngine PlayToEnding(bool answerUtopia)
    {
        GameEngine engine = TestGame.CreateLab(out _, seed: 20240924);

        for (int round = 0; round < 40_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (answerUtopia) AnswerUtopiaWherePossible(engine);

            // 宿主把表态画出来了（这一轮里新挂上的那些）。
            engine.MarkPendingChoicesShown();

            if (engine.EraGate.CanAdvance) engine.Ascend();

            engine.Simulate(engine.State.Era >= 7 ? 0.25 : 30);

            if (answerUtopia) AnswerUtopiaWherePossible(engine);
        }

        Check.NotNull(engine.ReachedEnding, "机器人没能在预算内跑到终局。");
        return engine;
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
