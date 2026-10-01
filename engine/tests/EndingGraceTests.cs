using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 终局判定的"作答宽限"（<see cref="EndingSystem.DefaultGraceSeconds"/>，可由
/// <see cref="GameEngineOptions.EndingGraceSeconds"/> 外部配置）。<para>
/// <b>它修的是什么</b>：《猫娘实验室》的末次表态门槛（3.4e8）与末层完成门槛（1e9）
/// 之间只隔几秒。终局判定一旦在那几秒里成立就永久锁死，于是"押了两次乌托邦、
/// 第三次还在读题"的玩家会拿到兜底结局——他什么都没做错，只是手没那么快。
/// </para>
/// <para>
/// 之前的处理是让测试机器人把末层步长降到 0.25 秒（见 <c>LabEndingTests</c> 的注释）。
/// 那是把缺陷挪出了测试视野，不是修掉它：真实玩家的反应时间不会因为测试写得细就变短。
/// 这两个用例守的是<b>慢一拍的玩家</b>。
/// </para>
/// </summary>
public static class EndingGraceTests
{
    /// <summary>
    /// 表态门槛<b>等于</b>末层完成门槛时，结局条件与表态机会在同一拍首次成立。<para>
    /// 这条内容是<b>合法的</b>——<c>ValidateChoiceFitsItsEra</c> 只拦"门槛高于完成门槛"，
    /// 相等放行；而结局只要和生产末层主线一样苛就通过校验。于是执行顺序成了唯一的区别：
    /// 终局判定若先跑，它看到待答队列还是空的，就会立刻落定，玩家再也没机会表态。
    /// </para>
    /// <para>
    /// 所以这条用例同时钉住两件事：宽限期存在，且表态触发排在终局判定之前。
    /// </para>
    /// </summary>
    [Test]
    public static void CommittedEndingWaitsForTheChoiceThatLandsOnTheSameTick()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        // 一次跨过末层完成门槛（也就是表态门槛）：表态进队列、结局条件成立，同一拍。
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.True(
            engine.State.PendingChoices.Contains("c"),
            "表态 c 应当已经触发——它的门槛就是这一刻的累计赚取。");
        Check.Null(
            engine.ReachedEnding,
            "还有表态没答，结局不该在这一拍就锁死（这正是重排 CheckChoices 的理由）。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "结局条件已经成立，应当处于就绪状态。");

        // 玩家慢了一拍才作答：仍然要拿到他承诺的那条结局。
        Check.True(engine.AnswerChoice("c", "a"), "待答的表态应当可以作答。");
        engine.Simulate(2);

        Check.Equal("e", engine.ReachedEnding?.Id, "作答之后应当落到他承诺的结局，而不是被兜底抢走。");
    }

    /// <summary>
    /// 什么都不答也要有收场：宽限期一到就照常落定。<para>
    /// 宽限是"推迟"，不是"阻止"——少了这条，一个永不答的表态就能让结局永远悬着，
    /// 那比原来的抢答更糟。
    /// </para>
    /// </summary>
    [Test]
    public static void GraceExpiresSoAnUnansweredChoiceCannotStallTheEndingForever()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Null(engine.ReachedEnding, "宽限期内不该落定。");
        Check.AtLeast(EndingSystem.GraceRemaining(engine), 1, "刚就绪时应当还剩几乎整个宽限期。");

        // 一直不答，只让时间走。宽限期一过就必须落定。
        engine.Simulate(EndingSystem.Grace(engine) + 1);

        Check.Equal("e", engine.ReachedEnding?.Id, "宽限期一到就得落定，不能因为没人答就永远悬着。");
        Check.Close(0, EndingSystem.GraceRemaining(engine), 0.001, "落定之后宽限不该还剩时间。");
    }

    /// <summary>
    /// 宽限以<b>模拟秒</b>计：跨存档、跨离线都按世界时间走。<para>
    /// 这条守住设计文档里那个刻意的取舍——挂机跨过宽限期回来，结局已经自己落定了。
    /// 存档往返之后计时不能重置，否则"存一下读一下"就能把宽限无限续期。
    /// </para>
    /// </summary>
    [Test]
    public static void GraceIsSimulatedTimeAndSurvivesASaveRoundTrip()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "应当处于就绪状态。");

        double remainingBefore = EndingSystem.GraceRemaining(engine);
        string save = engine.Save();

        // 换个引擎读档：就绪状态与剩余宽限都必须跟着存档走。
        GameEngine reloaded = Create(BuildContent(choiceMilestone: 1e6));
        reloaded.Load(save);

        Check.True(EndingSystem.IsReady(reloaded.Content, reloaded.State), "读档后应当仍然是就绪状态。");
        Check.Close(
            remainingBefore,
            EndingSystem.GraceRemaining(reloaded),
            1e-6,
            "读档不该重置宽限期——否则反复存读就能无限续期。");
    }

    /// <summary>
    /// 宽限期是<b>外部参数</b>，不是写死的常量。<para>
    /// 它该定多少取决于"真人从看到表态到作答需要多久"，而那只能实测；写死在核心里
    /// 就意味着每次按数据调参都要改代码 + 重编 + 走一遍版本与快照流程——那会让
    /// "按数据调"变成不划算的事，于是参数就永远停在初始那个拍出来的数上。
    /// </para>
    /// <para>
    /// 这条用例钉住三个边界：设 0 等于退回旧行为、设更大就真的等更久、
    /// 非法值（负数 / NaN）按"没有宽限"处理而不是让结局卡死。
    /// </para>
    /// </summary>
    [Test]
    public static void GraceIsConfigurableFromOutsideAndHandlesBadValues()
    {
        // 设 0 → 条件一成立就落定，这就是修之前的行为。
        GameEngine none = PlayToLastEra(choiceMilestone: 1e6, graceSeconds: 0);
        Check.Equal(0, EndingSystem.Grace(none), "显式传 0 应当生效。");

        none.State.CookiesEarnedThisRun = 1e6;
        none.Simulate(2);

        Check.Equal("e", none.ReachedEnding?.Id, "宽限设为 0 时应当立刻落定（旧行为）。");
        Check.False(EndingSystem.IsReady(none.Content, none.State), "立刻落定之后不该停在就绪状态。");

        // 设 120 秒 → 走过默认的 30 秒还得继续等。
        GameEngine longer = PlayToLastEra(choiceMilestone: 1e6, graceSeconds: 120);
        longer.State.CookiesEarnedThisRun = 1e6;
        longer.Simulate(2);

        Check.Null(longer.ReachedEnding, "宽限期内不该落定。");
        longer.Simulate(EndingSystem.DefaultGraceSeconds + 5);
        Check.Null(longer.ReachedEnding, "把宽限设成 120 秒之后，走过默认的 30 秒不该就落定。");

        longer.Simulate(100);
        Check.Equal("e", longer.ReachedEnding?.Id, "过了 120 秒就该落定。");

        // 非法值：按"没有宽限"处理。NaN 尤其危险——比较永远为假会让结局永不落定。
        foreach (double bad in new[] { -1.0, double.NaN })
        {
            GameEngine engine = PlayToLastEra(choiceMilestone: 1e6, graceSeconds: bad);
            Check.Equal(0, EndingSystem.Grace(engine), $"非法值 {bad} 应当按没有宽限处理。");

            engine.State.CookiesEarnedThisRun = 1e6;
            engine.Simulate(2);
            Check.Equal("e", engine.ReachedEnding?.Id, $"非法值 {bad} 不该让结局永远落不了定。");
        }
    }

    /// <summary>
    /// 就绪的那一刻必须<b>说出来</b>。<para>
    /// 一个"给了机会但不告诉玩家"的宽限期，比原来的抢答好不了多少——玩家只会觉得
    /// "主线走完了，然后什么也没发生"。没接视图的宿主（无头模拟、纯引擎宿主）
    /// 也得能从消息流里看见这件事，所以通知发在引擎里而不是视图里。
    /// </para>
    /// </summary>
    [Test]
    public static void BecomingReadyTellsThePlayerThatItIsWaitingOnThem()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.True(
            engine.Notifications.Any(n => n.Message.Contains("表态", StringComparison.Ordinal)
                                          && n.Message.Contains("结局", StringComparison.Ordinal)),
            "结局已就绪时必须有一条消息说明「还有表态没答、结局在等他」。");
        Check.True(
            engine.Notifications.Any(n => n.Kind == NotificationKind.Warning),
            "这条消息应当是警告级——它说的是「再不做点什么就会落到别的结局」。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 造一份"末层绑定了表态"的最小内容，把机器人推进到末层、表态还没触发的那一刻。<para>
    /// 末层完成条件 = 累计赚取 1e6；表态绑定在末层，门槛由 <paramref name="choiceMilestone"/>
    /// 给定（传 1e6 就是"与完成门槛相等"那个合法但致命的形状）。结局条件是末层主线完成，
    /// 也就是"和生产末层一样苛"的最小合法写法。
    /// </para>
    /// </summary>
    /// <param name="choiceMilestone">末层表态的层内门槛。</param>
    private static GameEngine PlayToLastEra(double choiceMilestone, double? graceSeconds = null)
    {
        GameEngine engine = Create(BuildContent(choiceMilestone), graceSeconds);

        // 第 1 层完成条件是 Always，所以一开始就能舍命——用引擎自己的入口进末层，
        // 而不是直接改 State.Era（那会绕过 EraSystem 该做的记账）。
        Check.True(engine.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        engine.Ascend();
        Check.Equal(2, engine.State.Era, "应当已经进入末层。");

        // 末层门槛之下：表态还没触发，结局条件也没成立。
        Check.False(
            engine.State.PendingChoices.Contains("c"),
            "门槛之外的断言有误：表态不该已经触发，否则下面的用例测不到那个窗口。");

        return engine;
    }

    private static GameContent BuildContent(double choiceMilestone) =>
        new GameContentBuilder("G")
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一", Completion = UnlockCondition.Always },
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
                    UnlockCondition.EarnedThisRunAtLeast(choiceMilestone)),
                Options = [Option("a", stance: "s", weight: 1), Option("b")],
            })
            .AddEndings(new EndingDefinition
            {
                Id = "e",
                Name = "E",
                Text = "T",
                Condition = UnlockCondition.All(
                    UnlockCondition.EraAtLeast(2),
                    UnlockCondition.EarnedThisRunAtLeast(1e6)),
            })
            .Build();

    private static GameEngine Create(GameContent content, double? graceSeconds = null) => new(content, new GameEngineOptions
    {
        Clock = new ManualClock(),
        Seed = 11,
        GrantOfflineProgress = false,
        EndingGraceSeconds = graceSeconds,
    });

    private static ChoiceOption Option(string id, string stance = "", int weight = 0) => new()
    {
        Id = id,
        Label = id,
        OutcomeText = id,
        StanceId = stance,
        Weight = weight,
    };
}
