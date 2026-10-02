using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 结局落定前的等待：<b>玩家被展示过那批待答表态</b>（1.5.0 起）。<para>
/// <b>它修的是什么</b>：《猫娘实验室》的末次表态门槛（3.4e8）与末层完成门槛（1e9）
/// 之间只隔几秒。终局判定一旦在那几秒里成立就永久锁死，于是「押了两次乌托邦、
/// 第三次还在读题」的玩家会拿到兜底结局——他什么都没做错，只是手没那么快。
/// </para>
/// <para>
/// <b>1.5.0 换掉了什么</b>：原来是「最多等 30 模拟秒」（<see cref="EndingSystem.DefaultGraceSeconds"/>，
/// 到期就当作放弃表态照常落定）。现在等的是一个<b>条件</b>：宿主在真的把表态画给玩家看时调
/// <see cref="GameEngine.MarkPendingChoicesShown"/>，在那之前无论过多久都不落定。
/// </para>
/// <para>
/// <b>代价（刻意接受的，不是遗漏）</b>：没被展示过的表态会让结局<b>永远</b>悬着——
/// 从不渲染表态面板的玩家永远拿不到结局。原来那条「不会永远悬着」由
/// <c>GraceExpiresSoAnUnansweredChoiceCannotStallTheEndingForever</c> 守着；
/// 那条性质是被<b>有意移除</b>的，所以守它的用例不能只改断言，只能整个反过来重写
/// （见 <see cref="NotShownChoicesStallTheEndingForeverAndThatIsDeliberate"/>）。
/// </para>
/// <para>
/// 文件名与类名保留 <c>EndingGraceTests</c>：不为「好看」改名——「那条被重写的用例」
/// 得留得下可以 grep 的痕迹（旧名字写在下面几条的注释与 CHANGELOG 里）。
/// </para>
/// </summary>
public static class EndingGraceTests
{
    /// <summary>
    /// 表态门槛<b>等于</b>末层完成门槛时，结局条件与表态机会在同一拍首次成立。<para>
    /// 这条内容是<b>合法的</b>——<c>ValidateChoiceFitsItsEra</c> 只拦「门槛高于完成门槛」，
    /// 相等放行；而结局只要和生产末层主线一样苛就通过校验。于是执行顺序成了唯一的区别：
    /// 终局判定若先跑，它看到待答队列还是空的，就会立刻落定，玩家再也没机会表态。
    /// </para>
    /// <para>
    /// 所以这条用例钉住的是：表态触发排在终局判定之前，而落定会一直等到那批表态被展示过
    /// （这里玩家干脆直接答了，所以等到的是「答完」而不是「看过」）。
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
            "还有表态没答、玩家也还没看到它们，结局不该在这一拍就锁死（这正是重排 CheckChoices 的理由）。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "结局条件已经成立，应当处于就绪状态。");

        // 玩家慢了一拍才作答：仍然要拿到他承诺的那条结局。
        Check.True(engine.AnswerChoice("c", "a"), "待答的表态应当可以作答。");
        engine.Simulate(2);

        Check.Equal("e", engine.ReachedEnding?.Id, "作答之后应当落到他承诺的结局，而不是被兜底抢走。");
    }

    /// <summary>
    /// 没被展示过的表态会让结局<b>一直</b>等下去——过多久都不落定。<para>
    /// <b>这条取代了 <c>GraceExpiresSoAnUnansweredChoiceCannotStallTheEndingForever</c>。</b>
    /// 原来那条守的是「宽限一到就照常落定，所以不会永远悬着」；1.5.0 把那段定时兜底
    /// <b>刻意</b>移除了，所以这里守的东西整个反了过来：等不到「玩家看过」这个条件，
    /// 结局就永远不落定。<b>这不是漏了兜底，是决定不要兜底</b>——别顺手把它加回来。
    /// </para>
    /// <para>
    /// 判据是「时间不解决问题」：这里让它跑掉旧机制整整 120 倍（1 模拟小时）。
    /// </para>
    /// </summary>
    [Test]
    public static void NotShownChoicesStallTheEndingForeverAndThatIsDeliberate()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Null(engine.ReachedEnding, "刚就绪时不该落定——玩家还没看到那些表态。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "结局条件已经成立，应当处于就绪状态。");
        Check.AtLeast(engine.State.PendingChoices.Count, 1, "前提：有一条表态挂着等答。");

        // 让时间走：旧机制下这里早就该落定了（默认宽限 30 模拟秒），新机制下「过了多久」不是判据。
        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);

        Check.Null(
            engine.ReachedEnding,
            "没被展示过的表态会让结局一直等下去——这是 1.5.0 刻意移除「不会永远悬着」的代价。"
            + "别把定时兜底加回来：那会让「玩家没看到」重新变成「玩家看到过」。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "它应当仍然停在就绪状态，而不是悄悄过期。");

        // 宿主报告「画出来了」之后，结局才允许落定：不必等玩家作答。
        Check.Equal(1, engine.MarkPendingChoicesShown(), "应当恰好有 1 条表态刚被标记为已展示。");
        engine.Simulate(2);

        Check.Equal("e", engine.ReachedEnding?.Id, "玩家看过之后，结局就该落定了。");
        Check.False(EndingSystem.IsReady(engine.Content, engine.State), "落定之后不该还停在就绪状态。");
    }

    /// <summary>被展示过之后就可以落定，而且报告是<b>幂等</b>的（宿主每帧都会报一次）。</summary>
    [Test]
    public static void ShownChoicesLetTheEndingLockAndReportingTwiceIsHarmless()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Equal(1, engine.MarkPendingChoicesShown(), "第一次报告应当新标记 1 条。");
        Check.Equal(0, engine.MarkPendingChoicesShown(), "重复报告不该反复记账（宿主每渲染一帧都会报）。");

        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "玩家看过之后，结局就可以落定了。");
    }

    /// <summary>
    /// 「展示过」是<b>逐条</b>记账，不是给存档开一个全局开关。<para>
    /// 玩家看过第一批表态之后才出现的新表态，仍然算「没看过」——若存的是一个布尔值，
    /// 这一拍就会被当成已经看过而立刻锁死，玩家连读题的机会都没有（而那正是这条机制要防的事）。
    /// </para>
    /// </summary>
    [Test]
    public static void ShowingNothingDoesNotPreApproveAChoiceThatAppearsLater()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        // 此刻还没有任何表态挂着：报告一次，什么也不该被标记。
        Check.Equal(0, engine.MarkPendingChoicesShown(), "没有待答表态时不该凭空标记出点什么。");

        // 表态与结局条件在同一拍首次成立：这一条是「之后才出现的」，没被展示过。
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Null(
            engine.ReachedEnding,
            "之前那次报告不该顺带批准一条当时还不存在的表态——展示必须逐条记账。");
    }

    /// <summary>
    /// 没有待答表态时一切照旧：条件一成立就落定，不需要任何宿主信号。<para>
    /// 这条守的是「改机制没有顺手改掉别的」——六个没有表态的内容包走的就是这条路，
    /// 而已答完表态之后的任何时刻也走这条路。
    /// </para>
    /// </summary>
    [Test]
    public static void WithoutPendingChoicesTheEndingLocksExactlyAsBefore()
    {
        // 这条表态不设层内门槛（进了 e2 就能触发），所以能在结局条件成立之前就答掉。
        GameEngine engine = Create(BuildContent(
            choiceMilestone: 1e6,
            choiceTrigger: UnlockCondition.Always));

        Check.True(engine.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        engine.Ascend();

        engine.CheckChoices();
        Check.True(engine.State.PendingChoices.Contains("c"), "前提：表态已经触发。");
        Check.True(engine.AnswerChoice("c", "b"), "先把它答掉。");
        Check.Null(engine.ReachedEnding, "结局条件还没成立。");

        // 结局条件成立，而待答队列是空的：照旧立刻落定。
        engine.State.CookiesEarnedThisRun = 2e6;
        engine.Simulate(2);

        Check.Equal(0, engine.State.PendingChoices.Count, "前提：这一拍没有任何表态挂着。");
        Check.Equal("e", engine.ReachedEnding?.Id, "没有待答表态时，结局应当照旧立刻落定。");
    }

    /// <summary>
    /// 展示记录随存档往返：<b>上一次会话里看过的表态，读档回来仍然算看过</b>。<para>
    /// 理由：条件问的是「玩家有没有被展示过」，而玩家是跨会话的。若读档就把它抹掉，
    /// 「结局能不能落定」就会取决于哪个进程打开了这份存档——同一份存档在两个宿主里
    /// 会给出不同结局，而那是不可逆的。顺带一提，这也与它取代的那条定时宽限一致：
    /// 当年宽限走的是模拟时间，离线期间照样流逝（见 <c>ENDING_GRACE_PLAN</c> 规则 2）。
    /// </para>
    /// </summary>
    [Test]
    public static void ShownnessSurvivesASaveRoundTrip()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Equal(1, engine.MarkPendingChoicesShown(), "前提：这条表态已经被展示过。");

        string save = engine.Save();

        // 换个引擎读档：展示记录必须跟着存档走。
        GameEngine reloaded = Create(BuildContent(choiceMilestone: 1e6));
        reloaded.Load(save);
        Check.Equal(1, reloaded.State.PendingChoices.Count, "读档后那条表态仍然挂着没答。");

        reloaded.Simulate(2);
        Check.Equal("e", reloaded.ReachedEnding?.Id, "上一次会话展示过的表态，读档后不该重新变回「没看过」。");
    }

    /// <summary>
    /// 反过来：读档<b>不会</b>把「没看过」变成「看过」。<para>
    /// 这条防的是一种很自然的、但方向完全错误的「修法」——在读档/首次渲染时顺手补一个
    /// 「已经展示过」。那等于把整条机制变成永远为真，也让「从不看面板的玩家永远没有结局」
    /// 这条刻意接受的代价悄悄消失（消失得毫无痕迹：测试全绿）。
    /// </para>
    /// </summary>
    [Test]
    public static void LoadingASaveDoesNotInventShownness()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "前提：还没被展示过，所以还没落定。");

        string save = engine.Save();

        GameEngine reloaded = Create(BuildContent(choiceMilestone: 1e6));
        reloaded.Load(save);
        reloaded.Simulate(EndingSystem.DefaultGraceSeconds * 120);

        Check.Null(reloaded.ReachedEnding, "读档不许把「没看过」变成「看过」。");
    }

    /// <summary>
    /// 那个 30 秒的宽限参数<b>已退役</b>：它既不延长等待，也不缩短等待。<para>
    /// 逐条交代三件事：① 读取行为不变（老宿主与作答延迟埋点还靠它对齐当年的参数）；
    /// ② 判定完全不看它（设成 3600 秒也不会让结局多等，设成 0 也不会让它提前落定）；
    /// ③ <b>设了它会喊一声</b>——一个「设置了却没有任何效果」的选项正是本项目最反对的沉默失败，
    /// 而宿主完全可能照着旧文档把它设成一个数。
    /// </para>
    /// </summary>
    [Test]
    public static void TheRetiredGraceSettingNoLongerDelaysAnythingAndSaysSo()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6, graceSeconds: 3600);
        Check.Equal(3600, EndingSystem.Grace(engine), "历史宽限期的读取行为没变（诊断与埋点还靠它对账）。");

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Null(engine.ReachedEnding, "设了宽限期不等于玩家看过表态：结局照样得等。");

        // 立刻落定的那条路也不受它影响：看过就是看过，跟秒数无关。
        Check.Equal(1, engine.MarkPendingChoicesShown(), "前提：这条表态刚被标记为已展示。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "设成 3600 秒也不该让「看过之后就能落定」多等一秒。");

        Check.True(
            engine.Notifications.Any(
                n => n.Kind == NotificationKind.Warning
                     && n.Message.Contains("EndingGraceSeconds", StringComparison.Ordinal)),
            "设了已经退役的 EndingGraceSeconds 就必须有一条警告——沉默地忽略配置是不允许的。");

        // 非法值当年按「没有宽限」处理（NaN 的比较永远为假，会让结局永远落不了定）。
        // 判定不看它了，但这条读取行为保持不变，免得把老宿主/老埋点打歪。
        foreach (double bad in new[] { -1.0, double.NaN })
        {
            GameEngine withBad = PlayToLastEra(choiceMilestone: 1e6, graceSeconds: bad);
            Check.Equal(0, EndingSystem.Grace(withBad), $"非法值 {bad} 仍然按 0 报。");
        }
    }

    /// <summary>
    /// 就绪的那一刻必须<b>说出来</b>。<para>
    /// 一个「给了机会但不告诉玩家」的等待，比原来的抢答好不了多少——玩家只会觉得
    /// 「主线走完了，然后什么也没发生」。没接视图的宿主（无头模拟、纯引擎宿主）
    /// 也得能从消息流里看见这件事，所以通知发在引擎里而不是视图里。
    /// </para>
    /// <para>
    /// 顺带钉住：这条消息<b>不许再许诺时限</b>。1.5.0 之前它写的是「30 秒内还可以改」，
    /// 而那个期限已经不存在了——留着那句话就是引擎在骗玩家。
    /// </para>
    /// </summary>
    [Test]
    public static void BecomingReadyTellsThePlayerThatItIsWaitingOnThem()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        GameNotification? waiting = engine.Notifications.FirstOrDefault(
            n => n.Message.Contains("表态", StringComparison.Ordinal)
                 && n.Message.Contains("结局", StringComparison.Ordinal));

        Check.NotNull(waiting, "结局已就绪时必须有一条消息说明「还有表态没答、结局在等他」。");
        Check.Equal(
            NotificationKind.Warning,
            waiting!.Kind,
            "这条消息应当是警告级——它说的是「再不做点什么就会落到别的结局」。");
        Check.False(
            waiting.Message.Contains("秒", StringComparison.Ordinal),
            $"这条消息不该再许诺任何时限（定时兜底已退役）：<{waiting.Message}>");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 把机器人推进到末层、表态还没触发的那一刻。<para>
    /// 用引擎自己的入口（<see cref="GameEngine.Ascend"/>）进末层，而不是直接改
    /// <c>State.Era</c>——那会绕过 <c>EraSystem</c> 该做的记账。
    /// </para>
    /// </summary>
    /// <param name="choiceMilestone">末层表态的层内门槛。</param>
    /// <param name="graceSeconds">历史宽限期（1.5.0 起不参与判定，只在兼容用例里传）。</param>
    private static GameEngine PlayToLastEra(double choiceMilestone, double? graceSeconds = null)
    {
        GameEngine engine = Create(BuildContent(choiceMilestone), graceSeconds);

        // 第 1 层完成条件是 Always，所以一开始就能舍命。
        Check.True(engine.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        engine.Ascend();
        Check.Equal(2, engine.State.Era, "应当已经进入末层。");

        // 末层门槛之下：表态还没触发，结局条件也没成立。
        Check.False(
            engine.State.PendingChoices.Contains("c"),
            "门槛之外的断言有误：表态不该已经触发，否则下面的用例测不到那个窗口。");

        return engine;
    }

    /// <summary>
    /// 造一份「末层绑定了表态」的最小内容。<para>
    /// 末层完成条件 = 累计赚取 1e6；表态绑定在末层，门槛由 <paramref name="choiceMilestone"/>
    /// 给定（传 1e6 就是「与完成门槛相等」那个合法但致命的形状）。结局条件是末层主线完成，
    /// 也就是「和生产末层一样苛」的最小合法写法。
    /// </para>
    /// </summary>
    /// <param name="choiceMilestone">末层表态的层内门槛。</param>
    /// <param name="choiceTrigger">换掉表态的触发条件（默认是「进了末层且累计赚取过门槛」）；
    /// <c>Always</c> 用来造「结局条件成立之前就能把它答掉、于是待答队列为空」那条对照。</param>
    private static GameContent BuildContent(double choiceMilestone, UnlockCondition? choiceTrigger = null) =>
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
                Trigger = choiceTrigger ?? UnlockCondition.All(
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
