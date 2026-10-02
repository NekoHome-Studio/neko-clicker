using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 终局判定（阶段 3B）。<para>
/// 三条特有验收对应 ROADMAP 阶段 3B：
/// <list type="number">
///   <item>各结局可达且<b>互斥</b>。</item>
///   <item>结局<b>不重置存档</b>，只写 <c>Counters["ending_&lt;id&gt;"]</c>。</item>
///   <item>存在"不押任何一条立场也有的结局"——1.6.0 起它的形状是
///     "<b>把每次表态都答掉，但每次都挑当前权重最低的那条立场</b>"，
///     而不是原来的"一次都不答"。区别是判据从"看过"改成了"答过"（见
///     <c>EndingGraceTests</c>）：待答的表态<b>必须被答掉</b>结局才会落定，
///     所以"回避表态"这条路现在只能靠"回答但不承诺"来走。</item>
/// </list>
/// </para>
/// </summary>
public static class EndingTests
{
    // ---------------------------------------------------------------- ① 可达与互斥

    [Test]
    public static void Ending_IsChosenByPriority()
    {
        // end_a 与兜底结局会同时成立，取 Priority 小的那个——互斥就是靠这个实现的。
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "a");

        EndingDefinition? reached = engine.CheckEnding();
        Check.NotNull(reached);
        Check.Equal("end_a", reached!.Id);
    }

    [Test]
    public static void Ending_RecordsOnlyOne_AndNeverRepicks()
    {
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "a");
        Check.Equal("end_a", engine.CheckEnding()?.Id);

        // 之后把另一个结局的条件也推成真：不该改写已达成的结局。
        engine.State.StanceWeights["b"] = 99;
        Check.Null(engine.CheckEnding(), "一份存档只判一次。");
        Check.Equal("end_a", engine.ReachedEnding?.Id);
        Check.Equal(1, engine.State.EndingsReached.Count);
    }

    [Test]
    public static void Ending_EachBranchIsReachable()
    {
        // 三个分支各自可达：答 a → end_a，答 b → end_b，**谁都不押**（每次挑权重最低的立场）
        // → 兜底结局。第三条以前写成"什么都不答"，1.6.0 起那条路不再有结局（见下一条），
        // 因为待答的表态必须被答掉结局才会落定。
        Check.Equal("end_a", ReachedBy("a"));
        Check.Equal("end_b", ReachedBy("b"));
        Check.Equal("end_default", ReachedByWithoutCommitting(Content()));
    }

    // ---------------------------------------------------------------- ③ 回避表态也有结局

    [Test]
    public static void Ending_FallsBackWhenPlayerAvoidsEveryChoice()
    {
        GameEngine engine = Create(Content());

        // 一次都不作答，但主线该走完走完。
        engine.Simulate(600);
        engine.CheckChoices();   // 触发但故意不答
        Check.Equal(0, engine.State.ChoiceAnswers.Count);

        // 1.6.0：待答的表态会一直拦着结局（1.5.0 只要"被展示过"就放行）。
        Check.Null(engine.ReachedEnding, "还有表态没答，结局不该落定。");

        // 宿主把表态画出来（本用例不跑界面，直接报告这一点）；玩家仍然不作答。
        Check.Equal(1, engine.MarkPendingChoicesShown(), "应当恰好标记那条待答表态。");
        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);
        Check.Null(
            engine.ReachedEnding,
            "「画出来了」不再等于「处理完了」：1.6.0 起不答就没有结局——这是刻意接受的代价，"
            + "不再是 1.1.0 那种「等 30 秒也会给兜底」的待遇。");

        // 玩家最终答了，只是没有押任何一条立场：兜底结局照常到达。
        Check.True(engine.AnswerChoice("c", "n"), "把那条表态答掉，而且挑不承诺任何立场的那一项。");
        engine.Simulate(2);

        // 注意这里读的是 ReachedEnding 而不是 CheckEnding() 的返回值：
        // Step 里的自动检查（与成就/叙事/选择同频）早就判过了，显式再判是 no-op。
        EndingDefinition? reached = engine.ReachedEnding;
        Check.NotNull(reached, "不押任何立场的玩家也必须有结局，否则会卡在「主线走完但没有结局」。");
        Check.Equal("end_default", reached!.Id);
    }

    // ---------------------------------------------------------------- ② 不重置存档

    [Test]
    public static void Ending_DoesNotResetTheSave()
    {
        GameEngine engine = Create(Content());
        engine.State.Cookies = 5_000;
        engine.MarkDirty();
        engine.BuyBuilding("b", 3);
        double cookies = engine.State.Cookies;
        double buildings = engine.State.TotalBuildings();

        RevealAndAnswer(engine, "a");
        engine.CheckEnding();

        Check.Close(cookies, engine.State.Cookies);
        Check.Close(buildings, engine.State.TotalBuildings());
        Check.Close(1, engine.State.GetCounter("ending_end_a")); // 只写一个计数器
    }

    [Test]
    public static void Ending_PublishesEventAndUnlocksAchievementGatedOnIt()
    {
        GameEngine engine = Create(Content());
        List<string> events = [];
        using IDisposable sub = engine.Events.Subscribe<EndingReachedEvent>(e => events.Add(e.Id));

        RevealAndAnswer(engine, "a");
        engine.CheckEnding();

        Check.Equal(1, events.Count);
        Check.Equal("end_a", events[0]);

        // 成就自己声明 Unlock = EndingReached("end_a")，走常规的成就检查路径解锁——
        // 结局不需要知道"谁是它的成就"，也不需要引擎特判。
        Check.False(engine.State.Achievements.Contains("ach_a"), "还没检查成就。");
        engine.CheckAchievements();
        Check.True(engine.State.Achievements.Contains("ach_a"));
    }

    // ---------------------------------------------------------------- 存档与视图

    [Test]
    public static void SaveRoundTrip_PreservesEnding()
    {
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "b");
        engine.CheckEnding();

        string json = engine.Save();

        GameEngine restored = Create(Content());
        restored.Load(json);

        Check.Equal("end_b", restored.ReachedEnding?.Id);
        Check.Equal(1, restored.State.GetCounter("ending_end_b"));
    }

    [Test]
    public static void Snapshot_ExposesEndingAndNullForPacksWithoutEndings()
    {
        GameEngine engine = Create(Content());
        Check.Null(engine.Snapshot().Ending, "还没达成时应当为 null。");

        RevealAndAnswer(engine, "a");
        engine.CheckEnding();

        EndingView? view = engine.Snapshot().Ending;
        Check.NotNull(view);
        Check.Equal("end_a", view!.Id);
        Check.True(view.Text.Length > 0);

        Check.Null(TestGame.CreateNineLives(out _).Snapshot().Ending, "九命还没写结局。");
    }

    // ---------------------------------------------------------------- 构建期校验

    [Test]
    public static void Validator_RequiresAFallbackEnding()
    {
        // 全部结局都依赖玩家的选择或立场 → 回避表态的玩家会卡住。这正是验收 ③ 的机器化。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .AddStances(new StanceDefinition { Id = "s", Name = "S" })
                .AddChoices(new ChoiceDefinition
                {
                    Id = "c",
                    Speaker = "她",
                    Prompt = "？",
                    Trigger = UnlockCondition.Always,
                    Options = [Option("a", stance: "s", weight: 1), Option("b")],
                })
                .AddEndings(new EndingDefinition
                {
                    Id = "e",
                    Name = "E",
                    Text = "T",
                    Condition = UnlockCondition.StanceWeight("s", 1),
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "缺少兜底结局");
    }

    [Test]
    public static void Validator_RejectsFallbackThatCanBecomeFalse()
    {
        // 只查"不含选择/立场"是不够的：Not(LoreAtLeast(n)) 对读得多的玩家反而是假，
        // 拿它冒充兜底，等于没有兜底。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .AddEndings(new EndingDefinition
                {
                    Id = "e",
                    Name = "E",
                    Text = "T",
                    Condition = UnlockCondition.Not(UnlockCondition.LoreAtLeast(40)),
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "缺少兜底结局");
    }

    [Test]
    public static void Validator_RejectsAchievementGatedOnUnknownEnding()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new AchievementDefinition
                {
                    Id = "a",
                    Name = "A",
                    Unlock = UnlockCondition.EndingReached("ghost"),
                })
                .AddEndings(new EndingDefinition { Id = "d", Name = "D", Text = "T", Condition = UnlockCondition.Always })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "不存在的 Ending");
    }

    [Test]
    public static void Validator_RejectsNeverEnding()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .AddEndings(
                    new EndingDefinition { Id = "e", Name = "E", Text = "T", Condition = UnlockCondition.Never },
                    new EndingDefinition { Id = "d", Name = "D", Text = "T", Condition = UnlockCondition.Always })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "永远达不成");
    }

    [Test]
    public static void Validator_RejectsUnreachableEnding()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Unlock = UnlockCondition.UpgradeOwned("missing"),
                })
                .AddEndings(
                    new EndingDefinition
                    {
                        Id = "e",
                        Name = "E",
                        Text = "T",
                        Condition = UnlockCondition.UpgradeOwned("u"),
                    },
                    new EndingDefinition { Id = "d", Name = "D", Text = "T", Condition = UnlockCondition.Always })
                .Build());

        // 可达性报错用的是 NodeKey 的格式（类别与 id 之间没有空格）。
        Check.Contains(string.Join("\n", ex.Errors), "结局「e」 永远无法解锁");
    }

    [Test]
    public static void Validator_RejectsEndingMissingText()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .AddEndings(new EndingDefinition { Id = "e", Name = "E", Text = "  " })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "缺少终局文本");
    }

    [Test]
    public static void Validator_RejectsEndingThatPreemptsLastEraChoices()
    {
        // 复刻《猫娘实验室》那个真实缺陷：末层绑定了表态机会，而结局只要求"进入末层"。
        // 运行期看不出来（结局只是"总是落到兜底那个"），必须在构建期拦下。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            BuildWithLastEraChoice(endingCondition: UnlockCondition.EraAtLeast(2)));

        string all = string.Join("\n", ex.Errors);
        Check.Contains(all, "表态机会", "应当指出它抢掉了末层的表态机会。");
        Check.Contains(all, "一样苛", "应当说明结局至少要和生产末层主线一样苛。");
    }

    [Test]
    public static void Validator_AcceptsEndingAsStrictAsTheLastEraMainline()
    {
        // 正确写法：直接复用末层完成条件（或更强）。
        // 这条同时保证上一条不是"永远都红"。
        GameContent content = BuildWithLastEraChoice(
            endingCondition: UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1e6)));

        Check.Equal(1, content.Endings.Count);
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 造一份"末层绑定了表态"的最小内容，用来验证那条构建期规则。<para>
    /// 第 2 层（末层）完成条件是累计赚取 1e6，而绑在该层的表态门槛只有 1e5——
    /// 于是"结局只要进入第 2 层"就会抢在那次表态之前成立。
    /// </para>
    /// </summary>
    private static GameContent BuildWithLastEraChoice(UnlockCondition endingCondition) =>
        new GameContentBuilder("X")
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .AddEras(
                new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "一",
                    Completion = UnlockCondition.Always,
                },
                new EraDefinition
                {
                    Index = 2,
                    Id = "e2",
                    Name = "二",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e6),
                })
            .Add(new ChoiceDefinition
            {
                Id = "c",
                Speaker = "她",
                Prompt = "？",
                EraId = "e2",
                Trigger = UnlockCondition.All(
                    UnlockCondition.EraAtLeast(2),
                    UnlockCondition.EarnedThisRunAtLeast(1e5)),
                Options = [Option("a", stance: "s", weight: 1), Option("b")],
            })
            .AddEndings(new EndingDefinition
            {
                Id = "e",
                Name = "E",
                Text = "T",
                Condition = endingCondition,
            })
            .Build();

    private static string? ReachedBy(string? answer)
    {
        GameEngine engine = Create(Content());
        if (answer is not null) RevealAndAnswer(engine, answer);
        return engine.CheckEnding()?.Id;
    }

    /// <summary>
    /// <b>"回答，但不承诺任何一条立场"</b>——1.6.0 起"回避表态"唯一还能走的路。<para>
    /// 待答的表态<b>必须被答掉</b>结局才会落定（见 <c>EndingGraceTests</c>），
    /// 所以玩家只能靠"每次都挑分量最轻的那一项"来避免押满任何一条路。
    /// 挑法有两条优先级：① 优先挑<b>不带立场</b>的选项（合成内容里那个 <c>n</c>）；
    /// ② 没有中立选项时挑<b>当前权重最低</b>的那条立场（真实内容用的就是这一条——
    /// 实验室 / 九命 / 公司每一条立场都只有 3~4 次机会，摊开来就谁也到不了门槛）。
    /// </para>
    /// </summary>
    private static string? ReachedByWithoutCommitting(GameContent content)
    {
        GameEngine engine = Create(content);
        engine.CheckChoices();

        foreach (ChoiceDefinition choice in content.Choices)
        {
            if (!engine.State.PendingChoices.Contains(choice.Id)) continue;

            ChoiceOption pick = choice.Options
                .OrderBy(o => o.StanceId.Length == 0 ? 0 : 1)
                .ThenBy(o => engine.State.StanceWeight(o.StanceId))
                .First();

            Check.True(engine.AnswerChoice(choice.Id, pick.Id), $"作答 {choice.Id}/{pick.Id} 应当成功。");
        }

        return engine.CheckEnding()?.Id;
    }

    private static GameEngine Create(GameContent content)
    {
        var clock = new ManualClock();
        return new GameEngine(content, new GameEngineOptions
        {
            Clock = clock,
            Seed = 11,
            GrantOfflineProgress = false,
        });
    }

    private static void RevealAndAnswer(GameEngine engine, string optionId)
    {
        engine.CheckChoices();
        Check.True(engine.State.PendingChoices.Contains("c"), "选择 c 应当已触发。");
        Check.True(engine.AnswerChoice("c", optionId), $"作答 {optionId} 应当成功。");
    }

    private static ChoiceOption Option(string id, string stance = "", int weight = 0) => new()
    {
        Id = id,
        Label = id,
        OutcomeText = id,
        StanceId = stance,
        Weight = weight,
    };

    /// <summary>
    /// 合成内容：一座建筑、两个立场、一次选择、三个结局。<para>
    /// 兜底结局用 <c>Always</c>，所以它与 <c>end_a</c> 会同时成立——
    /// 这正是验证"按 Priority 取第一个"的场合。
    /// </para>
    /// </summary>
    private static GameContent Content()
    {
        GameContentBuilder builder = new GameContentBuilder("Ending")
            .WithCurrency("单位", "u")
            .Add(new BuildingDefinition { Id = "b", Name = "工坊", BasePrice = 10, BaseCps = 1 })
            .Add(new AchievementDefinition
            {
                Id = "ach_a",
                Name = "成神",
                // 结局成就靠"结局已达成"这个条件解锁，而不是靠结局反过来去授予它。
                Unlock = UnlockCondition.EndingReached("end_a"),
            })
            .AddStances(
                new StanceDefinition { Id = "a", Name = "A" },
                new StanceDefinition { Id = "b", Name = "B" })
            .AddChoices(new ChoiceDefinition
            {
                Id = "c",
                Speaker = "她",
                Prompt = "？",
                Trigger = UnlockCondition.Always,
                Options =
                [
                    new ChoiceOption { Id = "a", Label = "A", OutcomeText = "A", StanceId = "a", Weight = 3 },
                    new ChoiceOption { Id = "b", Label = "B", OutcomeText = "B", StanceId = "b", Weight = 3 },
                    // 中立选项：不押任何一条立场。它在这里代表真实内容里那条"回答但不承诺"的路
                    // （真实内容没有中立选项，靠把答案摊在几条立场上来避免押满某一条；
                    // 合成内容只有一次表态，"摊开"无从谈起，所以用一个中立选项等价地表达它）。
                    // 1.6.0 起这一点是**承重**的：待答表态必须被答掉结局才会落定，
                    // 若每个选项都押立场，兜底结局在这份合成内容里就再也走不到了。
                    new ChoiceOption { Id = "n", Label = "N", OutcomeText = "N", Weight = 0 },
                ],
            })
            .AddEndings(
                new EndingDefinition
                {
                    Id = "end_a",
                    Name = "成神",
                    Text = "她把自己拼回了一份。",
                    Priority = 0,
                    Condition = UnlockCondition.StanceWeight("a", 3),
                },
                new EndingDefinition
                {
                    Id = "end_b",
                    Name = "变人",
                    Text = "她学会了用两条腿走路。",
                    Priority = 1,
                    Condition = UnlockCondition.StanceWeight("b", 3),
                },
                new EndingDefinition
                {
                    Id = "end_default",
                    Name = "永为猫",
                    Text = "她留下最后一节不点亮。",
                    Priority = 100,
                    // 兜底结局刻意不依赖任何选择或立场——"回答但不押任何一条路"也走得到这里
                    // （1.6.0 起这是唯一还能走到它的方式：待答的表态必须被答掉，见 EndingGraceTests）。
                    Condition = UnlockCondition.Always,
                });

        return builder.Build();
    }
}
