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
///   <item>存在"什么都没选也有的结局"——回避表态的玩家不能卡在无结局状态。</item>
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
        // 三个分支各自可达：答 a → end_a，答 b → end_b，什么都不答 → 兜底结局。
        Check.Equal("end_a", ReachedBy("a"));
        Check.Equal("end_b", ReachedBy("b"));
        Check.Equal("end_default", ReachedBy(null));
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

        // 注意这里读的是 ReachedEnding 而不是 CheckEnding() 的返回值：
        // Step 里的自动检查（与成就/叙事/选择同频）早就判过了，显式再判是 no-op。
        EndingDefinition? reached = engine.ReachedEnding;
        Check.NotNull(reached, "回避表态的玩家也必须有结局，否则会卡在「主线走完但没有结局」。");
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

    // ---------------------------------------------------------------- 辅助

    private static string? ReachedBy(string? answer)
    {
        GameEngine engine = Create(Content());
        if (answer is not null) RevealAndAnswer(engine, answer);
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
                    // 兜底结局刻意不依赖任何选择或立场——回避表态也走得到这里。
                    Condition = UnlockCondition.Always,
                });

        return builder.Build();
    }
}
