using NekoClicker.Core.Content;
using NekoClicker.Content.NineLives;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 九命的终局内容（阶段 3B 的内容部分）。<para>
/// 验收 ① "四种结局各可达且互斥"的验证链条拆成三段，避免为四个结局跑四遍 33 小时：
/// <list type="number">
///   <item><b>六次表态在真实游玩里都会出现</b>——跑一次机器人到第八命，
///   断言六个选择全部进入待答队列。</item>
///   <item><b>四个结局各自可达</b>——把那次跑图存下来，分四份读档，
///   每份在每次表态里都押同一条立场，再把层号推到 9，看落到哪个结局。</item>
///   <item><b>兜底结局可达</b>——一次都不答也能走到。</item>
/// </list>
/// 合起来：触发链成立（1）+ 权重可攒够且条件/优先级正确（2）+ 不表态也有出路（3）。
/// </para>
/// </summary>
public static class NineLivesEndingTests
{
    // ---------------------------------------------------------------- ① 表态都会出现

    [Test]
    public static void EveryChoiceTriggersDuringARealPlaythrough()
    {
        GameEngine engine = LoadFrom(PlayedSave.Value);

        Check.Equal(
            engine.Content.Choices.Count,
            engine.State.PendingChoices.Count,
            $"只有 {engine.State.PendingChoices.Count} 次表态出现过，应当全部出现。");

        // 机器人从不作答，所以一个都不该被算作"已经历"。
        Check.Equal(0, engine.State.ChoiceAnswers.Count);
    }

    // ---------------------------------------------------------------- ② 四个结局

    [Test]
    public static void EachOfTheFourCommittedEndingsIsReachable()
    {
        Check.Equal("end_god", EndingAfterCommittingTo(PlayedSave.Value, Stances.Divine));
        Check.Equal("end_human", EndingAfterCommittingTo(PlayedSave.Value, Stances.Human));
        Check.Equal("end_cat", EndingAfterCommittingTo(PlayedSave.Value, Stances.Cat));
        Check.Equal("end_sever", EndingAfterCommittingTo(PlayedSave.Value, Stances.Sever));
    }

    [Test]
    public static void CommittedEndingsAreMutuallyExclusive()
    {
        // 每次表态都是在互斥的选项之间二选一，而每条立场只有三次机会、门槛要 5 点，
        // 所以四条路在构造上就不可能同时成立：押满一条，别的都够不到门槛。
        GameEngine engine = LoadFrom(PlayedSave.Value);
        AnswerAll(engine, Stances.Divine);

        Check.AtLeast(engine.State.StanceWeight(Stances.Divine), Stances.EndingThreshold);
        foreach (string other in new[] { Stances.Human, Stances.Cat, Stances.Sever })
        {
            Check.AtMost(
                engine.State.StanceWeight(other),
                Stances.EndingThreshold - 1,
                $"押满「{Stances.Divine}」时「{other}」不该也够到门槛，否则结局不互斥。");
        }
    }

    // ---------------------------------------------------------------- ③ 兜底

    /// <summary>
    /// <b>一次都不答 = 永远没有结局。</b>这是 1.6.0 刻意接受的代价，在真实内容上钉一遍。<para>
    /// 1.5.0 时这条路还有收场（宿主报告"画出来了"就行）；1.6.0 起判据是<b>作答</b>，
    /// 所以"回避表态"不再等于"什么都不做"，而是"回答但不承诺"（见下一条）。
    /// 时间与"画出来了"都不能替代作答——所以这条同时断言那条兜底结局<b>确实已经就绪</b>，
    /// 只是被一条一直没人答的表态拦着（否则它可能只是条件没成立，那就说明不了任何事）。
    /// </para>
    /// </summary>
    [Test]
    public static void NeverAnsweringMeansNoEndingAtAllAndThatIsDeliberate()
    {
        GameEngine engine = LoadFrom(PlayedSave.Value);
        Check.Equal(0, engine.State.ChoiceAnswers.Count, "前提：一次都不答。");

        engine.State.Era = 9;
        engine.MarkDirty();

        Check.AtLeast(engine.State.PendingChoices.Count, 1, "前提：这份存档里挂着没答的表态。");

        // 像宿主那样报告"画出来了"，再让时间跑掉旧机制整整 120 倍（1 模拟小时）。
        engine.MarkPendingChoicesShown();
        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);

        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "兜底结局的条件已经成立——它在等玩家作答。");
        Check.Null(
            engine.ReachedEnding,
            "一条都不答就永远没有结局：这是 1.6.0 有意移除「不会永远悬着」的代价，不是漏了兜底。");
    }

    /// <summary>
    /// <b>不押任何一条立场的玩家照样有兜底结局</b>——但前提是他<b>把每次表态都答掉</b>。<para>
    /// <b>旧名字：<c>AvoidingEveryChoiceYieldsTheFallbackEnding</c></b>（1.5.0 的写法是
    /// "一次都不答 + 宿主报告看过"）。1.6.0 起"回避表态"这条路只能这么走：
    /// 每次都挑<b>当前权重最低</b>的那条立场，六次摊下来谁也到不了门槛（4/4/2/2 &lt; 5），
    /// 于是走兜底。它同时是一条内容侧的可达性证据：兜底结局在真实内容里仍然走得到。
    /// </para>
    /// <para>
    /// <b>这里刻意不写 <c>IsReady</c> 断言</b>（初版写过，是错的）：就绪时刻是
    /// <c>EndingSystem.Check</c> 跑过之后才记下的诊断值，而待答队列空着时 Check 会<b>直接落定</b>
    /// ——落定之后 <c>IsReady</c> 按定义就是 <c>false</c>（<c>EndingsReached</c> 非空）。
    /// 所以这条直接问判定本身："全都答完了，它还等不等？"——答案必须是"不等"。
    /// "就绪但一直没落定"那半在 <see cref="NeverAnsweringMeansNoEndingAtAllAndThatIsDeliberate"/> 里。
    /// </para>
    /// </summary>
    [Test]
    public static void AvoidingEveryCommitmentYieldsTheFallbackEnding()
    {
        GameEngine engine = LoadFrom(PlayedSave.Value);
        AnswerWithoutCommitting(engine);
        Check.Equal(engine.Content.Choices.Count, engine.State.ChoiceAnswers.Count, "前提：六次表态都答了。");

        engine.State.Era = 9;
        engine.MarkDirty();

        Check.Equal(0, engine.State.PendingChoices.Count, "答完之后队列应当空了。");

        EndingDefinition? reached = engine.CheckEnding();
        Check.NotNull(
            reached,
            "六次表态都答完之后判定不该再等任何人（1.6.0 等的是作答，队列已经空了）。");
        Check.Equal("end_blank", reached!.Id, "不押任何一条立场也必须有一个收场。");
        Check.Equal("end_blank", engine.ReachedEnding?.Id, "落定的结局要能被读到。");
    }

    /// <summary>
    /// 待答的每次都作答应答，但每次都挑<b>当前权重最低</b>的那条立场（同权取先声明的那一项）——
    /// "回答，但不承诺任何一条路"。<para>
    /// 这是 1.6.0 下"回避表态"唯一还能走的路：不答就永远没有结局（见上一条）。
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

            Check.True(
                engine.AnswerChoice(choice.Id, pick.Id),
                $"作答 {choice.Id}/{pick.Id} 失败。");
        }
    }

    // ---------------------------------------------------------------- 选项修饰符真的生效

    [Test]
    public static void OptionModifiersTakeEffectImmediately()
    {
        // 设计文档"三条件"里的「有代价」：选择必须当场在数值上留痕，
        // 而不是只靠延迟结算的"主导立场"——否则前五次表态在数值上完全无感。
        //
        // 注意这里有两层效果叠加：选项自带的修饰符（立刻生效）+ 该选项所属立场
        // 一旦成为主导所带来的修饰符。两层都该看得见。
        GameEngine baseline = LoadFrom(PlayedSave.Value);
        double baseCps = baseline.Modifiers.Multiplier(ModifierTarget.GlobalCps);
        double baseReward = baseline.Modifiers.Multiplier(ModifierTarget.GoldenCookieReward);

        GameEngine wrote = LoadFrom(PlayedSave.Value);
        Check.True(wrote.AnswerChoice("choice_name", "name_write"));
        Check.True(
            wrote.Modifiers.Multiplier(ModifierTarget.GlobalCps) > baseCps,
            "选「写上去」应当立刻提高产量（选项 +5%，且神性成为主导 +25%）。");

        GameEngine blank = LoadFrom(PlayedSave.Value);
        Check.True(blank.AnswerChoice("choice_name", "name_blank"));
        Check.True(
            blank.Modifiers.Multiplier(ModifierTarget.GoldenCookieReward) > baseReward,
            "选「先空着」应当立刻提高金猫奖励（选项 +5%，且猫形成为主导 +30%）。");

        // 分叉必须留下不同的形状：两个存档在同一项上的乘数不该一样。
        Check.True(
            wrote.Modifiers.Multiplier(ModifierTarget.GlobalCps)
            != blank.Modifiers.Multiplier(ModifierTarget.GlobalCps),
            "同一个选择的两个选项必须导向不同的数值结果。");
    }

    // ---------------------------------------------------------------- 内容审查

    [Test]
    public static void EveryChoiceIsARealFork()
    {
        GameContent content = TestGame.NineLives;

        foreach (ChoiceDefinition choice in content.Choices)
        {
            Check.AtLeast(choice.Options.Count, 2, $"「{choice.Id}」选项太少。");

            // 两个选项必须指向不同的立场，否则不是"分叉"而是"同一个方向的强弱"。
            List<string> stances = [.. choice.Options.Select(o => o.StanceId)];
            Check.Equal(
                stances.Count,
                stances.Distinct(StringComparer.Ordinal).Count(),
                $"「{choice.Id}」的两个选项指向了同一条立场，不构成分叉。");

            foreach (ChoiceOption option in choice.Options)
            {
                Check.True(option.Modifiers.Count > 0, $"「{choice.Id}/{option.Id}」没有数值代价。");
                Check.True(option.Label.Length > 0);
                Check.True(option.OutcomeText.Length > 0);
            }
        }
    }

    [Test]
    public static void EveryStanceHasEnoughOpportunitiesToReachTheThreshold()
    {
        // 门槛 5 点、每次 2 点 → 每条立场至少要有 3 次机会，否则那个结局是死的。
        GameContent content = TestGame.NineLives;
        int needed = (Stances.EndingThreshold + 1) / 2;

        foreach (StanceDefinition stance in content.Stances)
        {
            int opportunities = content.Choices
                .SelectMany(c => c.Options)
                .Count(o => string.Equals(o.StanceId, stance.Id, StringComparison.Ordinal));

            Check.AtLeast(
                opportunities,
                needed,
                $"立场「{stance.Name}」只有 {opportunities} 次表态机会，攒不满 {Stances.EndingThreshold} 点。");
        }
    }

    [Test]
    public static void EndingAchievementsAreGatedOnTheirEndings()
    {
        GameContent content = TestGame.NineLives;

        foreach (EndingDefinition ending in content.Endings)
        {
            AchievementDefinition? achievement = content.Achievements
                .FirstOrDefault(a => a.Unlock is OwnedCondition { Kind: OwnedKind.Ending } owned
                                     && string.Equals(owned.Id, ending.Id, StringComparison.Ordinal));

            Check.NotNull(achievement, $"结局「{ending.Id}」没有对应的成就。");
        }

        // 反过来也要成立：不存在"门槛写着某个结局、但那个结局根本不存在"的成就。
        foreach (AchievementDefinition achievement in content.Achievements)
        {
            if (achievement.Unlock is not OwnedCondition { Kind: OwnedKind.Ending } owned) continue;
            Check.NotNull(content.FindEnding(owned.Id), $"成就「{achievement.Id}」指向了不存在的结局。");
        }
    }

    [Test]
    public static void StanceModifiersActuallyChangeProduction()
    {
        // 主导立场真的改产量（验收 ② 的内容侧）。
        GameEngine divine = LoadFrom(PlayedSave.Value);
        divine.State.StanceWeights[Stances.Divine] = Stances.EndingThreshold;
        divine.MarkDirty();

        GameEngine cat = LoadFrom(PlayedSave.Value);
        cat.State.StanceWeights[Stances.Cat] = Stances.EndingThreshold;
        cat.MarkDirty();

        Check.True(
            divine.Modifiers.Multiplier(ModifierTarget.GlobalCps)
            > cat.Modifiers.Multiplier(ModifierTarget.GlobalCps),
            "神性主导时产量应当高于猫形主导（1.25 vs 0.9）。");

        // 神性的代价：她越来越不像是会撞见意外的生物。
        Check.True(
            divine.Modifiers.Multiplier(ModifierTarget.GoldenCookieFrequency) < 1.0,
            "神性应当压低金猫出现频率——一个方向上的加成必然在别处留代价。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 跑一次真实游玩，直到六次表态全部进入待答队列（发生在第八命），把存档缓存起来。<para>
    /// <b>只跑一次</b>：上面六个用例都要用这份状态，各跑一遍就是六倍开销。
    /// 缓存的是不可变的存档字符串，各用例读档后互不影响。
    /// </para>
    /// </summary>
    private static readonly Lazy<string> PlayedSave = new(() => PlayUntilAllChoicesPending().Save());

    private static GameEngine PlayUntilAllChoicesPending()
    {
        GameEngine engine = TestGame.CreateNineLives(out _, seed: 20240924);
        int total = engine.Content.Choices.Count;

        for (int round = 0; round < 6000 && engine.State.PendingChoices.Count < total; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance && engine.State.Era < 9) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(
            total,
            engine.State.PendingChoices.Count,
            "机器人没能让六次表态全部出现——某个选择的门槛可能超过了它所在层的完成门槛。");
        return engine;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>每次表态都押同一条立场（没有该立场的选项就取第一个），然后推到第九命。</summary>
    private static string? EndingAfterCommittingTo(string save, string stanceId)
    {
        GameEngine engine = LoadFrom(save);
        AnswerAll(engine, stanceId);

        engine.State.Era = 9;
        engine.MarkDirty();
        return engine.CheckEnding()?.Id;
    }

    private static void AnswerAll(GameEngine engine, string stanceId)
    {
        foreach (ChoiceDefinition choice in engine.Content.Choices)
        {
            ChoiceOption pick = choice.Options.FirstOrDefault(
                o => string.Equals(o.StanceId, stanceId, StringComparison.Ordinal)) ?? choice.Options[0];

            Check.True(
                engine.AnswerChoice(choice.Id, pick.Id),
                $"作答 {choice.Id}/{pick.Id} 失败。");
        }
    }
}
