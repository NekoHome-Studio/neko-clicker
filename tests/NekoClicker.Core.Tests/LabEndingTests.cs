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

    /// <summary>
    /// 机器人从第 1 批跑到终局：每次有待答就作答（能选乌托邦就选），完成主线就舍命。<para>
    /// 最后一批改用细步长推进——表态门槛（3.4e8）与完成门槛（1e9）之间的窗口只有几秒，
    /// 一步 30 秒会直接跨过去，让机器人"来不及答"最后一个选择；
    /// 真实玩家的手速不会这样，但测试必须把那个窗口留出来，否则测的是步长而不是内容。
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
