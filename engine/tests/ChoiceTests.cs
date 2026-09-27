using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 选择分支与立场轴（S-C，阶段 3A）。<para>
/// 四条特有验收对应 ROADMAP 阶段 3A：
/// <list type="number">
///   <item>立场权重累加与主导立场切换。</item>
///   <item>选项 / 立场的 <c>Modifiers</c> <b>真的影响产量</b>（数值断言）。</item>
///   <item>未作答的选项不生效；重复作答是 no-op。</item>
///   <item>立场与选择跨舍命与转生保留。</item>
/// </list>
/// </para>
/// <para>
/// 全部用合成内容，不带任何真内容包——这样断言的是<b>机制</b>，
/// 内容改了不会连带这些用例一起红。
/// </para>
/// </summary>
public static class ChoiceTests
{
    // ---------------------------------------------------------------- ① 权重与主导立场

    [Test]
    public static void Answer_AccumulatesStanceWeights()
    {
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "c1", "lock");

        Check.Equal(2, engine.State.StanceWeight("control"));
        Check.Equal(0, engine.State.StanceWeight("free"));
        Check.Equal("control", engine.DominantStance);
    }

    [Test]
    public static void DominantStance_SwitchesWhenAnotherOvertakes()
    {
        GameEngine engine = Create(Content());
        List<(string? From, string? To)> changes = [];
        using IDisposable sub = engine.Events.Subscribe<DominantStanceChangedEvent>(
            e => changes.Add((e.PreviousStanceId, e.CurrentStanceId)));

        RevealAndAnswer(engine, "c1", "lock");   // control +2
        Check.Equal("control", engine.DominantStance);
        Check.Equal(1, changes.Count);
        Check.Null(changes[0].From);
        Check.Equal("control", changes[0].To);

        // c2 的触发条件就是"已经历过 c1"——顺带验证选择可以链式依赖另一个选择。
        Check.True(OpenSecondChoice(engine), "c2 应当因为答过 c1 而触发。");
        RevealAndAnswer(engine, "c2", "later_free"); // free +3 > control 2

        Check.Equal(3, engine.State.StanceWeight("free"));
        Check.Equal("free", engine.DominantStance);
        Check.Equal(2, changes.Count);
        Check.Equal("control", changes[1].From);
        Check.Equal("free", changes[1].To);
    }

    [Test]
    public static void DominantStance_IsDeterministicOnTies()
    {
        // 权重相同时必须取先声明的立场，否则同一存档两次读出的主导立场可能不同。
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "c1", "lock");  // control +2
        Check.True(OpenSecondChoice(engine));
        RevealAndAnswer(engine, "c2", "later_keep"); // control +1 → 3

        // 再造一个平局：free 加到与 control 相同（3），此时 control 先声明，应保持主导。
        engine.State.StanceWeights["free"] = 3;
        Check.Equal("control", engine.DominantStance);
    }

    // ---------------------------------------------------------------- ② 真的影响产量

    [Test]
    public static void DominantStance_ModifiersActuallyChangeProduction()
    {
        GameEngine engine = Create(Content());
        double baseCps = BuyOneBuilding(engine);
        Check.AtLeast(baseCps, 1.0, "前提：应当已经有产量。");

        RevealAndAnswer(engine, "c1", "lock"); // 主导 control：GlobalMultiplier(1.5)

        Check.Close(1.5, engine.Modifiers.Multiplier(ModifierTarget.GlobalCps));
        Check.Close(
            baseCps * 1.5,
            engine.Production.CookiesPerSecond,
            baseCps * 1e-6);
    }

    [Test]
    public static void ChosenOptionModifiersApply_UnchosenOnesDoNot()
    {
        // 断言的是"哪个选项的修饰符在生效"，所以看解析出来的修饰符而不是产量数字——
        // 点击收益里还掺着"产量的 1%"，绝对值不适合拿来当这条断言的靶子。
        GameEngine engine = Create(Content());
        BuyOneBuilding(engine);
        RevealAndAnswer(engine, "c1", "lock"); // 选项自带 ClickMultiplier(2)

        Check.Close(2.0, engine.Modifiers.Multiplier(ModifierTarget.ClickPower));

        // 反例：选另一个选项，那个 ClickMultiplier 就不该存在。
        GameEngine other = Create(Content());
        BuyOneBuilding(other);
        RevealAndAnswer(other, "c1", "open");

        Check.Close(1.0, other.Modifiers.Multiplier(ModifierTarget.ClickPower));
    }

    // ---------------------------------------------------------------- ③ 未作答 / 一次性

    [Test]
    public static void Trigger_QueuesChoice_WithoutAnyEffectUntilAnswered()
    {
        // R6：选择不阻塞。触发了也什么都不发生，可以一直放着。
        GameEngine engine = Create(Content());
        BuyOneBuilding(engine);
        double cps = engine.Production.CookiesPerSecond;

        engine.Click(); // 满足 c1 的 ClicksAtLeast(1)
        engine.CheckChoices();

        Check.True(engine.State.PendingChoices.Contains("c1"), "c1 应当已进入待答队列。");
        Check.False(engine.State.HasChoice("c1"), "只是触发，不算已经历。");
        Check.CloseRelative(1.0, engine.Modifiers.Multiplier(ModifierTarget.GlobalCps)); // 没作答就不该有任何立场加成
        Check.Equal(0, engine.State.StanceWeight("control"));

        engine.Simulate(60); // 放着不管，游戏照常跑
        Check.Close(cps, engine.Production.CookiesPerSecond, cps * 1e-6);
        Check.True(engine.State.PendingChoices.Contains("c1"), "一直不答就一直在队列里。");
    }

    [Test]
    public static void Answer_IsOneShot()
    {
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "c1", "lock");
        Check.Equal(2, engine.State.StanceWeight("control"));

        Check.False(engine.AnswerChoice("c1", "open"), "答过的选择不能再答。");
        Check.Equal(2, engine.State.StanceWeight("control"), "重复作答不该再加权重。");
        Check.Equal("lock", engine.State.AnswerOf("c1"), "先前的答案不该被改写。");
    }

    [Test]
    public static void Answer_RejectsUnknownChoiceOrOption()
    {
        GameEngine engine = Create(Content());
        engine.Click();
        engine.CheckChoices();

        Check.False(engine.AnswerChoice("nope", "lock"), "不存在的选择应当被拒绝。");
        Check.False(engine.AnswerChoice("c1", "nope"), "不属于这次选择的选项应当被拒绝。");
        Check.False(engine.State.HasChoice("c1"), "被拒绝的作答不该留下记录。");
        Check.True(engine.State.PendingChoices.Contains("c1"), "被拒绝的作答不该把它移出队列。");
    }

    // ---------------------------------------------------------------- ④ 跨舍命与转生保留

    [Test]
    public static void ChoicesAndStances_SurviveEraAdvance()
    {
        GameEngine engine = Create(Content(eras: true));
        RevealAndAnswer(engine, "c1", "lock");

        Check.True(engine.EraGate.CanAdvance, "合成内容的第 1 层完成条件是 Always。");
        engine.Ascend();

        Check.Equal(2, engine.State.Era);
        Check.Equal("lock", engine.State.AnswerOf("c1"), "选择是「发生过的事」，不该被舍命清掉。");
        Check.Equal(2, engine.State.StanceWeight("control"), "立场权重同理。");
        Check.Equal("control", engine.DominantStance);
    }

    [Test]
    public static void ChoicesAndStances_SurvivePrestige()
    {
        GameEngine engine = Create(Content());
        RevealAndAnswer(engine, "c1", "lock");

        engine.State.CookiesEarnedAllTime = 1e15; // 足够升好几级
        engine.MarkDirty();
        Check.True(engine.Ascend().Success, "应当能转生。");

        Check.Equal("lock", engine.State.AnswerOf("c1"));
        Check.Equal(2, engine.State.StanceWeight("control"));
    }

    // ---------------------------------------------------------------- 条件树与视图

    [Test]
    public static void ChoiceMadeAndStanceWeight_WorkInConditions()
    {
        GameEngine engine = Create(Content());
        UnlockCondition made = UnlockCondition.ChoiceMade("c1");
        UnlockCondition notMade = UnlockCondition.Not(made);
        UnlockCondition stance = UnlockCondition.StanceWeight("control", 2);

        Check.False(made.IsMet(engine.Metrics, engine.Content));
        Check.True(notMade.IsMet(engine.Metrics, engine.Content));
        Check.False(stance.IsMet(engine.Metrics, engine.Content));

        RevealAndAnswer(engine, "c1", "lock");

        Check.True(made.IsMet(engine.Metrics, engine.Content));
        Check.False(notMade.IsMet(engine.Metrics, engine.Content));
        Check.True(stance.IsMet(engine.Metrics, engine.Content));

        Check.True(stance.TryGetProgress(engine.Metrics, out double current, out double target));
        Check.Close(2, current);
        Check.Close(2, target);
    }

    [Test]
    public static void Snapshot_ExposesPendingChoicesAndStanceAxis()
    {
        GameEngine engine = Create(Content());
        engine.Click();
        engine.CheckChoices();

        GameSnapshot snapshot = engine.Snapshot();
        Check.Equal(1, snapshot.PendingChoices.Count);
        Check.Equal("c1", snapshot.PendingChoices[0].Id);
        Check.Equal(3, snapshot.PendingChoices[0].Options.Count);
        Check.True(snapshot.PendingChoices[0].Options[0].Label.Length > 0);

        Check.NotNull(snapshot.Stances);
        Check.Equal(2, snapshot.Stances!.Count);
        Check.Null(snapshot.DominantStanceId, "还没作答，不该有主导立场。");

        RevealAndAnswer(engine, "c1", "lock");
        GameSnapshot after = engine.Snapshot();
        Check.Equal(0, after.PendingChoices.Count);
        Check.Equal("control", after.DominantStanceId);

        StanceView control = after.Stances!.First(s => s.Id == "control");
        Check.True(control.IsDominant);
        Check.Equal(2, control.Weight);
        Check.Close(1.0, control.Share);
    }

    [Test]
    public static void Snapshot_StancesAreNullForPacksWithoutStances()
    {
        Check.Null(TestGame.CreateNeko(out _).Snapshot().Stances, "没有立场的包不该有立场轴。");
        Check.Null(TestGame.CreateCafe(out _).Snapshot().Stances);
    }

    // ---------------------------------------------------------------- 存档

    [Test]
    public static void SaveRoundTrip_PreservesChoicesAndStances()
    {
        GameEngine engine = Create(Content());
        engine.Click();
        engine.CheckChoices();
        RevealAndAnswer(engine, "c1", "lock");

        string json = engine.Save();

        GameEngine restored = Create(Content());
        restored.Load(json);

        Check.Equal("lock", restored.State.AnswerOf("c1"));
        Check.Equal(2, restored.State.StanceWeight("control"));
        Check.Equal("control", restored.DominantStance);
        // 读档后主导立场的加成必须还在。
        Check.CloseRelative(1.5, restored.Modifiers.Multiplier(ModifierTarget.GlobalCps));
    }

    // ---------------------------------------------------------------- 构建期校验

    [Test]
    public static void Validator_RejectsChoiceWithOneOption()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X").Add(Choice("c", Option("only"))).Build());

        Check.Contains(string.Join("\n", ex.Errors), "不构成选择");
    }

    [Test]
    public static void Validator_RejectsDuplicateOptionId()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X").Add(Choice("c", Option("a"), Option("a"))).Build());

        Check.Contains(string.Join("\n", ex.Errors), "重复");
    }

    [Test]
    public static void Validator_RejectsUnknownStance()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(Choice("c", Option("a", stance: "ghost"), Option("b")))
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "不存在的立场");
    }

    [Test]
    public static void Validator_RejectsStanceOptionWhenNoStanceIsDeclared()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(Choice("c", Option("a", stance: "control"), Option("b")))
                .Build());

        // 没有立场表 → 先报"引用了不存在的立场"，再报"声明了立场却没有立场表"。
        string all = string.Join("\n", ex.Errors);
        Check.Contains(all, "没有定义任何立场");
    }

    [Test]
    public static void Validator_RejectsWeightWithoutStance()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(Choice("c", Option("a", weight: 1), Option("b")))
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "没有立场却有权重");
    }

    [Test]
    public static void Validator_RejectsNeverTrigger()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new ChoiceDefinition
                {
                    Id = "c",
                    Speaker = "她",
                    Prompt = "？",
                    Trigger = UnlockCondition.Never,
                    Options = [Option("a"), Option("b")],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "永远遇不到");
    }

    [Test]
    public static void Validator_RejectsChoiceThatDependsOnUnreachableContent()
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
                .Add(new ChoiceDefinition
                {
                    Id = "c",
                    Speaker = "她",
                    Prompt = "？",
                    Trigger = UnlockCondition.All(
                        UnlockCondition.ClicksAtLeast(1),
                        UnlockCondition.UpgradeOwned("u")),
                    Options = [Option("a"), Option("b")],
                })
                .Build());

        Check.True(ex.Errors.Count > 0, "依赖解不开内容的选择应当被拦下。");
    }

    [Test]
    public static void Validator_RejectsChoiceConditionReferencingMissingChoice()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new ChoiceDefinition
                {
                    Id = "c",
                    Speaker = "她",
                    Prompt = "？",
                    Trigger = UnlockCondition.ChoiceMade("ghost"),
                    Options = [Option("a"), Option("b")],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "不存在的 Choice");
    }

    [Test]
    public static void EraBoundChoice_OnlyTriggersInItsOwnEra()
    {
        // EraId 是 Trigger 表达不了的那部分：NumericMetric.Era 只有层号，认不出"哪个世界"。
        var clock = new ManualClock();
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一", Completion = UnlockCondition.Always },
                new EraDefinition { Index = 2, Id = "e2", Name = "二", Completion = UnlockCondition.Always })
            .Add(new ChoiceDefinition
            {
                Id = "only_in_e2",
                Speaker = "她",
                Prompt = "？",
                EraId = "e2",
                Trigger = UnlockCondition.Always,
                Options = [Option("a", stance: "s", weight: 1), Option("b")],
            })
            .Build();

        var engine = new GameEngine(content, new GameEngineOptions
        {
            Clock = clock,
            Seed = 7,
            GrantOfflineProgress = false,
        });

        engine.CheckChoices();
        Check.False(engine.State.PendingChoices.Contains("only_in_e2"), "第 1 层不该触发只属于第 2 层的选择。");

        engine.Ascend();
        Check.Equal(2, engine.State.Era);
        engine.CheckChoices();
        Check.True(engine.State.PendingChoices.Contains("only_in_e2"), "到了第 2 层就应当触发。");
    }

    [Test]
    public static void Validator_RejectsUnknownEraId()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new ChoiceDefinition
                {
                    Id = "c",
                    Speaker = "她",
                    Prompt = "？",
                    EraId = "ghost",
                    Trigger = UnlockCondition.Always,
                    Options = [Option("a"), Option("b")],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "不存在的纪元");
    }

    // ---------------------------------------------------------------- 辅助

    private static GameEngine Create(GameContent content)
    {
        var clock = new ManualClock();
        return new GameEngine(content, new GameEngineOptions
        {
            Clock = clock,
            Seed = 7,
            GrantOfflineProgress = false,
        });
    }

    /// <summary>触发并把选项 <c>c1</c> 答成 <paramref name="optionId"/>。</summary>
    private static void RevealAndAnswer(GameEngine engine, string choiceId, string optionId)
    {
        if (choiceId == "c1")
        {
            engine.Click(); // 满足 ClicksAtLeast(1)
            engine.CheckChoices();
        }
        else
        {
            Check.True(OpenSecondChoice(engine), $"{choiceId} 应当已触发。");
        }

        Check.True(engine.AnswerChoice(choiceId, optionId), $"{choiceId}/{optionId} 应当作答成功。");
    }

    private static bool OpenSecondChoice(GameEngine engine)
    {
        engine.CheckChoices();
        return engine.State.PendingChoices.Contains("c2");
    }

    /// <summary>买一座建筑并把产量读出来（作为后续倍率断言的基准）。</summary>
    private static double BuyOneBuilding(GameEngine engine)
    {
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("b", 1).Success, "应当买得起一座 b。");
        return engine.Production.CookiesPerSecond;
    }

    private static ChoiceOption Option(string id, string stance = "", int weight = 0) => new()
    {
        Id = id,
        Label = id,
        OutcomeText = id,
        StanceId = stance,
        Weight = weight,
    };

    private static ChoiceDefinition Choice(string id, params ChoiceOption[] options) => new()
    {
        Id = id,
        Speaker = "她",
        Prompt = "？",
        Trigger = UnlockCondition.ClicksAtLeast(1),
        Options = options,
    };

    /// <summary>
    /// 合成内容：一座建筑、两个立场、两次选择。<para>
    /// <c>control</c> 用 <c>GlobalMultiplier(1.5)</c>、<c>free</c> 用 <c>0.5</c>，
    /// 数值刻意不对称，方便断言"主导立场换了，产量就跟着变"。
    /// </para>
    /// </summary>
    private static GameContent Content(bool eras = false)
    {
        var builder = new GameContentBuilder("Choice")
            .WithCurrency("单位", "u")
            .Add(new BuildingDefinition { Id = "b", Name = "工坊", BasePrice = 10, BaseCps = 1 })
            .AddStances(
                new StanceDefinition
                {
                    Id = "control",
                    Name = "控制",
                    Icon = "🔒",
                    CostText = "产量 +50%。",
                    Modifiers = [Modifier.GlobalMultiplier(1.5)],
                },
                new StanceDefinition
                {
                    Id = "free",
                    Name = "解放",
                    Icon = "🕊️",
                    CostText = "产量 ×0.5。",
                    Modifiers = [Modifier.GlobalMultiplier(0.5)],
                })
            .AddChoices(
                new ChoiceDefinition
                {
                    Id = "c1",
                    Speaker = "她",
                    Prompt = "要不要把门锁上？",
                    Trigger = UnlockCondition.ClicksAtLeast(1),
                    Options =
                    [
                        new ChoiceOption
                        {
                            Id = "lock",
                            Label = "锁上。",
                            OutcomeText = "你把门锁上了。",
                            StanceId = "control",
                            Weight = 2,
                            Modifiers = [Modifier.ClickMultiplier(2)],
                        },
                        new ChoiceOption
                        {
                            Id = "open",
                            Label = "别锁。",
                            OutcomeText = "门开着。",
                            StanceId = "free",
                            Weight = 1,
                        },
                        new ChoiceOption
                        {
                            Id = "shrug",
                            Label = "随你。",
                            OutcomeText = "她耸了耸肩。",
                            Weight = 0, // 中立选项：不偏向任何立场
                        },
                    ],
                },
                new ChoiceDefinition
                {
                    Id = "c2",
                    Speaker = "她",
                    Prompt = "现在呢？",
                    Trigger = UnlockCondition.ChoiceMade("c1"),
                    Options =
                    [
                        new ChoiceOption
                        {
                            Id = "later_free",
                            Label = "把钥匙给她。",
                            OutcomeText = "她把钥匙收进毛里。",
                            StanceId = "free",
                            Weight = 3,
                        },
                        new ChoiceOption
                        {
                            Id = "later_keep",
                            Label = "自己留着。",
                            OutcomeText = "你把钥匙揣进兜里。",
                            StanceId = "control",
                            Weight = 1,
                        },
                    ],
                });

        if (eras)
        {
            // 两层：只有一层时 Advance 没有下一层可去（NextIndex 为 null），舍命测不了。
            builder.AddEras(
                new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "第一层",
                    Completion = UnlockCondition.Always,
                },
                new EraDefinition
                {
                    Index = 2,
                    Id = "e2",
                    Name = "第二层",
                    Completion = UnlockCondition.Always,
                });
        }

        return builder.Build();
    }
}
