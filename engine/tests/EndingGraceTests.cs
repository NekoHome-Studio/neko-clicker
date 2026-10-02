using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 结局落定前的等待：<b>玩家把待答表态答完</b>（1.6.0 起）。<para>
/// <b>它修的是什么</b>：《猫娘实验室》的末次表态门槛（3.4e8）与末层完成门槛（1e9）
/// 之间只隔几秒。终局判定一旦在那几秒里成立就永久锁死，于是「押了两次乌托邦、
/// 第三次还在读题」的玩家会拿到兜底结局——他什么都没做错，只是手没那么快。
/// </para>
/// <para>
/// <b>三代的判据（这条机制改过两次，历史留着是为了别再绕回来）</b>：
/// <list type="number">
///   <item><b>1.1.0</b>：最多等 30 模拟秒（<see cref="EndingSystem.DefaultGraceSeconds"/>），
///     到期就当作放弃表态照常落定。</item>
///   <item><b>1.5.0</b>：换成条件——宿主在真的把表态画给玩家看时调
///     <see cref="GameEngine.MarkPendingChoicesShown"/>，<b>被展示过</b>就允许落定。</item>
///   <item><b>1.6.0（现在）</b>：还是条件，但换成<b>作答</b>——只要还有一条<b>答得上</b>的
///     待答表态，结局就不落定。理由是"看过"只证明面板画出来了，证明不了玩家不再需要它；
///     "答完"才是玩家自己说"这一项我处理完了"。</item>
/// </list>
/// </para>
/// <para>
/// <b>代价（刻意接受的，不是遗漏）</b>：<b>从不作答的玩家永远拿不到结局。</b>
/// 1.5.0 之前那条"最多等 30 模拟秒"的兜底是<b>刻意移除</b>的；1.5.0 把条件放宽成
/// "被展示过就算过"，1.6.0 又收紧成"必须答过"——"不会永远悬着"这条性质不但没回来，
/// 覆盖面还更大了。它原来由
/// <c>GraceExpiresSoAnUnansweredChoiceCannotStallTheEndingForever</c> 守着；
/// 那条性质是被<b>有意移除</b>的，所以守它的用例只能整个反过来重写，而不是改个断言：
/// 它<b>先后</b>变成了
/// <see cref="ShownButUnansweredChoicesStallTheEndingForeverAndThatIsDeliberate"/>
/// （1.5.0 的"没看过就不落定"），现在这条守着的是"<b>看过也一样</b>，答完才落定"。
/// </para>
/// <para>
/// <b>唯一不拦的是"答不上"</b>：队列里的 id 在当前内容里不存在（换包读档 / 内容改版删掉了它），
/// 或它同时出现在已答表里。那两种如果也拦，这一局就永久卡死了；引擎会发一条警告说出来。
/// 证据见 <see cref="UnanswerablePendingChoicesDoNotStallTheRunAndTheEngineSaysSo"/>；
/// "跨层 / 转生不会让待答表态变成答不上"的证据见
/// <see cref="AdvancingTheEraKeepsPendingChoicesAnswerableSoTheNewRuleCannotHang"/> 与
/// <see cref="PrestigeKeepsPendingChoicesAnswerable"/>。
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
    /// 所以这条用例钉住的是：表态触发排在终局判定之前，而落定会一直等到玩家把这条表态
    /// <b>答掉</b>。1.6.0 起"报告看过"这一步<b>已经不能</b>解除等待了——下面专门验了这一点。
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

        // 1.6.0：宿主报告"画出来了"**不再**解除等待（1.5.0 那会儿它会）。
        engine.MarkPendingChoicesShown();
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "「画出来了」不等于「答完了」——结局仍然要等作答。");

        // 玩家慢了一拍才作答：仍然要拿到他承诺的那条结局。
        Check.True(engine.AnswerChoice("c", "a"), "待答的表态应当可以作答。");
        engine.Simulate(2);

        Check.Equal("e", engine.ReachedEnding?.Id, "作答之后应当落到他承诺的结局，而不是被兜底抢走。");
        Check.False(EndingSystem.IsReady(engine.Content, engine.State), "落定之后不该还停在就绪状态。");
    }

    /// <summary>
    /// <b>看过也一样：没作答就不落定，过多久都不落定。</b><para>
    /// <b>这条取代了 <c>GraceExpiresSoAnUnansweredChoiceCannotStallTheEndingForever</c>
    /// （1.1.0 的定时兜底），也取代了它 1.5.0 的形态
    /// <c>NotShownChoicesStallTheEndingForeverAndThatIsDeliberate</c>。</b>
    /// 那两条守的分别是「宽限一到就照常落定」与「被展示过就允许落定」；1.5.0 移除了定时兜底，
    /// 1.6.0 又把"看过"升级成"答过"，所以这里守的东西是同一件事的最新形态：
    /// <b>等不到"答完"，结局就永远不落定；而且时间与"看过"这两样都解决不了它。</b>
    /// <b>这不是漏了兜底，是决定不要兜底</b>——别顺手把它加回来。
    /// </para>
    /// <para>
    /// 判据是「时间不解决问题」：这里让它跑掉旧机制整整 120 倍（1 模拟小时），
    /// 并且像宿主那样报告"画出来了"，两者都不足以解锁。
    /// </para>
    /// </summary>
    [Test]
    public static void ShownButUnansweredChoicesStallTheEndingForeverAndThatIsDeliberate()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Null(engine.ReachedEnding, "刚就绪时不该落定——玩家还没答那些表态。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "结局条件已经成立，应当处于就绪状态。");
        Check.AtLeast(engine.State.PendingChoices.Count, 1, "前提：有一条表态挂着等答。");

        // 宿主报告「画出来了」：1.5.0 那会儿这一发就解锁了，现在不解锁。
        Check.Equal(1, engine.MarkPendingChoicesShown(), "前提：确实新记下了一条展示记录。");

        // 让时间走：旧机制下这里早就该落定了（默认宽限 30 模拟秒）。
        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);

        Check.Null(
            engine.ReachedEnding,
            "看过但没有作答的表态会让结局一直等下去——这是 1.6.0 刻意接受"
            + "「从不作答的玩家永远拿不到结局」的代价。别把定时兜底或「看过就算」加回来："
            + "那会把玩家没处理完的东西记成处理完了。");
        Check.True(EndingSystem.IsReady(engine.Content, engine.State), "它应当仍然停在就绪状态，而不是悄悄过期。");

        // 真正解除等待的只有一件事：作答。
        Check.True(engine.AnswerChoice("c", "a"), "待答的表态应当可以作答。");
        engine.Simulate(2);

        Check.Equal("e", engine.ReachedEnding?.Id, "答完之后，结局就该落定了。");
        Check.False(EndingSystem.IsReady(engine.Content, engine.State), "落定之后不该还停在就绪状态。");
    }

    /// <summary>
    /// 「报告展示过」现在是<b>纯诊断</b>：它不解锁结局，但仍然逐条记账、幂等。<para>
    /// <b>旧名字：<c>ShownChoicesLetTheEndingLockAndReportingTwiceIsHarmless</c></b>
    /// （1.5.0——那时报告一发就解锁；这条用例按新语义反过来重写，没有删除）。
    /// 留下这份记录的理由：新规则下"结局一直没落定"是真实可能的事，而
    /// <b>"玩家从没看到这条表态"与"看到了却一直没答"是两种完全不同的原因</b>，
    /// 只有这份记录分得开。它存在 <c>GameState.Counters</c> 的 <c>$choice_shown_&lt;id&gt;</c>，
    /// 所以宿主读得到，也随存档往返（下面那条单独验往返）。
    /// </para>
    /// </summary>
    [Test]
    public static void ShowingChoicesNoLongerLetsTheEndingLockAnsweringIsWhatDoes()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);

        Check.Equal(1, engine.MarkPendingChoicesShown(), "第一次报告应当新标记 1 条。");
        Check.Equal(0, engine.MarkPendingChoicesShown(), "重复报告不该反复记账（宿主每渲染一帧都会报）。");

        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "报告过展示不等于作答，结局不该落定。");
        Check.Equal(
            1,
            engine.State.GetCounter("$choice_shown_c"),
            "展示记录仍然要逐条记下来（它是诊断，也是「从没看过」与「看过没答」的唯一区分点）。");

        Check.True(engine.AnswerChoice("c", "a"), "作答。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "作答之后结局就可以落定了。");
    }

    /// <summary>
    /// 「展示过」是<b>逐条</b>记账，不是给存档开一个全局开关；而且它不预先批准之后才出现的表态。<para>
    /// <b>旧名字：<c>ShowingNothingDoesNotPreApproveAChoiceThatAppearsLater</c></b>
    /// （1.5.0 的写法是"先报一次，之后出现的表态仍算没看过"）。1.6.0 起这条的重心变了：
    /// 之后才出现的表态不止是"不算看过"，它还会<b>一直拦着结局</b>，直到被答掉。
    /// 若把"看过"存成一个布尔值，玩家连"之后那批要不要答"都会被这一发连坐地绕过去。
    /// </para>
    /// </summary>
    [Test]
    public static void ReportingBeforeAChoiceExistsDoesNotPreApproveItAndItStillBlocksUntilAnswered()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);

        // 此刻还没有任何表态挂着：报告一次，什么也不该被标记。
        Check.Equal(0, engine.MarkPendingChoicesShown(), "没有待答表态时不该凭空标记出点什么。");

        // 表态与结局条件在同一拍首次成立：这一条是「之后才出现的」。
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "之前那次报告不该顺带批准一条当时还不存在的表态。");

        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);
        Check.Null(engine.ReachedEnding, "它同样不会因为「过了很久」而消失——它还没被答掉。");

        Check.True(engine.AnswerChoice("c", "b"), "作答。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "只有把这条之后才出现的表态答掉，结局才会落定。");
    }

    /// <summary>
    /// <b>逐条作答：答完最后一条才落定。</b><para>
    /// 这条把「全部答完 ⇒ 可以落定」单独立出来（别的地方只验了"一条"）：
    /// 两条表态同时挂着，答掉第一条之后结局<b>仍然</b>等着，第二条答完才落定。
    /// 若判据写成"只要答过任意一条"或"只要队列长度变小过"，这条会红。
    /// </para>
    /// </summary>
    [Test]
    public static void AnsweringEveryPendingChoiceLetsTheEndingLock()
    {
        GameEngine engine = PlayToLastEraWithTwoChoices();

        Check.Equal(2, engine.State.PendingChoices.Count, "前提：两条表态同时挂着等答。");
        Check.Null(engine.ReachedEnding, "一条都没答，不该落定。");

        Check.True(engine.AnswerChoice("c1", "a"), "答掉第一条。");
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "还剩一条没答，结局必须继续等。");

        Check.True(engine.AnswerChoice("c2", "b"), "答掉第二条。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "全部答完，结局才允许落定。");
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
    /// 展示记录随存档往返（<b>诊断信息</b>），而它<b>不会</b>因为往返而变成"已作答"。<para>
    /// <b>旧名字：<c>ShownnessSurvivesASaveRoundTrip</c></b>——1.5.0 时"展示过"是判据本身，
    /// 所以那条守的是"跨会话仍然算看过"。1.6.0 起判据是作答，于是这条守两件事：
    /// ① 展示记录（诊断）照旧随存档走；② 读档不会把没答的当成答过。
    /// </para>
    /// </summary>
    [Test]
    public static void ShownnessSurvivesASaveTripButDoesNotBecomeAnAnswer()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Equal(1, engine.MarkPendingChoicesShown(), "前提：这条表态已经被展示过。");

        string save = engine.Save();

        // 换个引擎读档：展示记录必须跟着存档走，而"答没答"当然也必须跟着走。
        GameEngine reloaded = Create(BuildContent(choiceMilestone: 1e6));
        reloaded.Load(save);
        Check.Equal(1, reloaded.State.PendingChoices.Count, "读档后那条表态仍然挂着没答。");
        Check.Equal(
            1,
            reloaded.State.GetCounter("$choice_shown_c"),
            "展示记录是诊断信息，应当随存档往返（上一次会话看过就是看过）。");

        reloaded.Simulate(2);
        Check.Null(reloaded.ReachedEnding, "上一次会话「看过」不等于这一次「答过」：结局仍然要等作答。");

        Check.True(reloaded.AnswerChoice("c", "a"), "读档之后照样答得上——这条表态还在内容里。");
        reloaded.Simulate(2);
        Check.Equal("e", reloaded.ReachedEnding?.Id, "答完之后才落定。");
    }

    /// <summary>
    /// 反过来：读档<b>不会</b>凭空造出展示记录，也不会凭空造出作答。<para>
    /// <b>旧名字：<c>LoadingASaveDoesNotInventShownness</c></b>——它防的是一种很自然的、
    /// 但方向完全错误的「修法」：在读档/首次渲染时顺手补一个"已经展示过"（或现在更该防的：
    /// 补一个"已经答过"）。那等于把整条机制变成永远为真，也让「从不作答的玩家永远没有结局」
    /// 这条刻意接受的代价悄悄消失（消失得毫无痕迹：测试全绿）。
    /// </para>
    /// </summary>
    [Test]
    public static void LoadingASaveInventsNeitherShownnessNorAnswers()
    {
        GameEngine engine = PlayToLastEra(choiceMilestone: 1e6);
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "前提：还没被展示过、也没被答过，所以还没落定。");

        string save = engine.Save();

        GameEngine reloaded = Create(BuildContent(choiceMilestone: 1e6));
        reloaded.Load(save);
        reloaded.Simulate(EndingSystem.DefaultGraceSeconds * 120);

        Check.Equal(0, reloaded.State.GetCounter("$choice_shown_c"), "读档不许凭空造出展示记录。");
        Check.Equal(0, reloaded.State.ChoiceAnswers.Count, "读档不许凭空造出作答。");
        Check.Null(reloaded.ReachedEnding, "读档不许把「没答」变成「答过」。");
    }

    /// <summary>
    /// 「答不上」的待答表态<b>不拦结局</b>，而且引擎必须把这件事<b>说出来</b>。<para>
    /// <b>为什么这条是承重的</b>：新判据是"必须答完"，而队列里的 id 有可能<b>永远答不上</b>——
    /// 那样这一局就永久卡死了。两种形态都存在，而且都只在<b>读档</b>时才出现（见下）。
    /// 没有这条，静默地把玩家看得见的待答项忽略掉，正是本项目最反对的失败形态。
    /// </para>
    /// </summary>
    [Test]
    public static void UnanswerablePendingChoicesDoNotStallTheRunAndTheEngineSaysSo()
    {
        // 造一份"外来存档"：里面挂着一条 gone（当前内容里不存在），另有一条 c1 已经答过。
        // 两份形状都只在**读档**时才可能出现（见这个方法上面的说明与 ChoiceSystem 的注释）。
        GameEngine foreign = Create(BuildContent(
            choiceMilestone: 0,
            choiceTrigger: UnlockCondition.Always,
            choiceId: "gone"));
        Check.True(foreign.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        foreign.Ascend();
        foreign.CheckChoices();

        Check.True(foreign.State.PendingChoices.Contains("gone"), "前提：外来存档里挂着 gone。");
        foreign.State.PendingChoices.Add("c1");   // 形态 ②：已答又挂在待答队列里
        foreign.State.ChoiceAnswers["c1"] = "a";
        string save = foreign.Save();

        // 形态 ①：把这份存档读进"内容里有 c1 但没有 gone"的另一份内容里。
        GameEngine engine = PlayToLastEraWithTwoChoices();
        engine.Load(save);

        Check.True(engine.State.PendingChoices.Contains("gone"), "读档后那条不认识的表态仍然挂在队列里。");
        Check.False(engine.AnswerChoice("gone", "a"), "它答不上——内容里根本没有这条表态（这就是风险本体）。");
        Check.False(engine.AnswerChoice("c1", "a"), "已经答过的表态再答一次也会失败——它同样永远答不上。");

        // 结局条件成立：此刻唯一答得上的是 c2，所以结局该等；等答完它，还必须能落定。
        engine.State.CookiesEarnedThisRun = 2e6;
        engine.Simulate(2);

        Check.True(engine.State.PendingChoices.Contains("c2"), "c2 应当已经触发。");
        Check.Null(engine.ReachedEnding, "c2 还答得上，结局这时仍然该等。");

        Check.True(engine.AnswerChoice("c2", "b"), "答掉唯一还答得上的那一条。");
        engine.Simulate(2);

        Check.Equal(
            "e",
            engine.ReachedEnding?.Id,
            "答不上的待答表态不能拦结局——否则旧存档/换包读进来的这一局永远收不了场。");

        GameNotification? said = engine.Notifications.FirstOrDefault(
            n => n.Kind == NotificationKind.Warning
                 && n.Message.Contains("答不上", StringComparison.Ordinal));

        Check.NotNull(
            said,
            "「有表态答不上、所以结局不等它们」必须有一条警告说出来——静默忽略玩家看得见的待答项是不允许的。");
        Check.Contains(said!.Message, "2 项", "那条警告应当说清有几项答不上（这里是 gone 与重复出现的 c1）。");

        // 存档里的待答清单保持原样：这是诊断信息，不该被判定顺手改写。
        Check.Equal(2, engine.State.PendingChoices.Count, "判定不该改写存档里的待答清单（只剩那条答不上的 gone 与 c1）。");
    }

    /// <summary>
    /// <b>跨层不会把待答表态变成"答不上"</b>——所以"必须答完"不会因为舍命而卡死。<para>
    /// <b>结论（证据在断言里，不靠 UI 文案推）</b>：待答队列<b>不</b>随舍命清空
    /// （<c>PrestigeSystem.ResetRun</c> 清的是货币/建筑/增益/非永久升级，<b>不碰</b>
    /// <c>PendingChoices</c>），<see cref="GameEngine.AnswerChoice"/> 也<b>不</b>重判
    /// <c>EraId</c>（它只要求"在待答队列里"），两个宿主的表态面板也都是照着
    /// <c>PendingChoices</c> 全量渲染的。所以第 2 层的表态在第 3 层照样答得上，
    /// 结局只是在等它被答掉而已。
    /// </para>
    /// <para>
    /// UI 上那句「舍命之后就遇不到了」说的是<b>触发</b>（<c>EraId</c> 是硬门：没触发的表态
    /// 过了那层就再也不会出现），不是"已经挂在队列里的答不上"——两者不能混为一谈，
    /// 这条用例就是把它们分开。
    /// </para>
    /// </summary>
    [Test]
    public static void AdvancingTheEraKeepsPendingChoicesAnswerableSoTheNewRuleCannotHang()
    {
        GameEngine engine = Create(BuildEraAdvanceContent());

        Check.True(engine.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        engine.Ascend();
        Check.Equal(2, engine.State.Era);

        engine.CheckChoices();
        Check.True(engine.State.PendingChoices.Contains("c"), "前提：第 2 层的表态已经挂上。");

        // 舍命：离开绑定它的那一层，表态却必须原样留着。
        AscensionResult advanced = engine.Ascend();
        Check.True(advanced.Success, "第 2 层应当可以舍命。");
        Check.Equal(3, engine.State.Era);
        Check.True(
            engine.State.PendingChoices.Contains("c"),
            "舍命不该把待答表态丢掉——丢掉的话，「必须答完」就会变成「永远答不完」。");

        // 结局条件成立：它必须等这条表态被答掉。
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
        Check.Null(engine.ReachedEnding, "那条表态还答得上，结局就该等它。");
        engine.Simulate(EndingSystem.DefaultGraceSeconds * 120);
        Check.Null(engine.ReachedEnding, "时间不解决问题——它答得上，所以一直等。");

        // 跨了层照样答得上：这就是"不会卡死"的那一半。
        Check.True(engine.AnswerChoice("c", "a"), "跨层之后这条表态仍然答得上（EraId 不参与作答校验）。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "答完之后，结局在更后面的层里照常落定。");
    }

    /// <summary>
    /// 经典转生（没有分层的包）同样不会把待答表态变成"答不上"。<para>
    /// 走的是同一条 <c>PrestigeSystem.ResetRun</c>（<c>PrestigeSystem.Ascend</c> 调它），
    /// 所以结论与上一条相同；单独立一条是因为这是<b>另一条代码路径</b>（没有纪元时
    /// <c>GameEngine.Ascend</c> 走的是转生而不是舍命），而"重置本轮"这个名字最容易让人
    /// 以为它也会重置表态。
    /// </para>
    /// </summary>
    [Test]
    public static void PrestigeKeepsPendingChoicesAnswerable()
    {
        GameEngine engine = Create(BuildNoEraContent());
        engine.CheckChoices();
        Check.True(engine.State.PendingChoices.Contains("c"), "前提：表态已经挂上。");

        // 经典转生要求等级真的提升：把历史累计顶过第一级门槛。
        engine.State.CookiesEarnedAllTime = 1e12;
        AscensionResult ascended = engine.Ascend();
        Check.True(ascended.Success, "这次转生应当成功。");
        Check.True(
            engine.State.PendingChoices.Contains("c"),
            "转生重置的是货币/建筑/增益，不是待答表态——丢掉它会让这一局再也答不上。");
        Check.True(engine.AnswerChoice("c", "a"), "转生之后照样答得上。");
    }

    /// <summary>
    /// 就绪的那一刻必须<b>说出来</b>。<para>
    /// 一个「给了机会但不告诉玩家」的等待，比原来的抢答好不了多少——玩家只会觉得
    /// 「主线走完了，然后什么也没发生」。没接视图的宿主（无头模拟、纯引擎宿主）
    /// 也得能从消息流里看见这件事，所以通知发在引擎里而不是视图里。
    /// </para>
    /// <para>
    /// 顺带钉死措辞的两件事：① 它<b>不许再许诺时限</b>——1.1.0 时写的是「30 秒内还可以改」，
    /// 而那个期限早就没有了；② 它<b>必须说"答完"</b>——1.5.0 时写的是「结局会等你把这些表态看完」，
    /// 那句在 1.6.0 已经<b>是假的</b>（看过没用，答完才走）。留着旧措辞就是引擎在骗玩家。
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
        Check.False(
            waiting.Message.Contains("看完", StringComparison.Ordinal),
            $"这条消息不该再说「看完就行」——1.6.0 起结局等的是**答完**：<{waiting.Message}>");
        Check.Contains(
            waiting.Message,
            "答完",
            "它必须说清解除等待的唯一动作是作答。");
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

        Check.Null(engine.ReachedEnding, "设了宽限期不等于玩家答过表态：结局照样得等。");

        // 立刻落定的那条路也不受它影响：答没答是唯一判据，跟秒数无关。
        Check.True(engine.AnswerChoice("c", "a"), "前提：把这个表态答掉。");
        engine.Simulate(2);
        Check.Equal("e", engine.ReachedEnding?.Id, "设成 3600 秒也不该让「答完就能落定」多等一秒。");

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

    /// <summary>末层、两条表态同时挂上、结局条件已经成立的那一拍——用来验"逐条作答"。</summary>
    private static GameEngine PlayToLastEraWithTwoChoices()
    {
        GameEngine engine = Create(BuildContent(choiceMilestone: 1e6, choiceIds: ["c1", "c2"]));

        Check.True(engine.EraGate.CanAdvance, "第 1 层应当可以直接舍命。");
        engine.Ascend();

        engine.State.CookiesEarnedThisRun = 1e6;
        engine.Simulate(2);
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
    /// <param name="choiceId">表态 id；换一个就能造"存档里的表态在当前内容里不存在"那份外来存档。</param>
    /// <param name="choiceIds">要几条表态（都挂在末层、门槛相同，于是同时进队列）。</param>
    private static GameContent BuildContent(
        double choiceMilestone,
        UnlockCondition? choiceTrigger = null,
        string choiceId = "c",
        string[]? choiceIds = null)
    {
        GameContentBuilder builder = new GameContentBuilder("G")
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一", Completion = UnlockCondition.Always },
                new EraDefinition
                {
                    Index = 2,
                    Id = "e2",
                    Name = "二",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e6),
                });

        foreach (string id in choiceIds ?? [choiceId])
        {
            builder.Add(new ChoiceDefinition
            {
                Id = id,
                Speaker = "她",
                Prompt = "？",
                EraId = "e2",
                Trigger = choiceTrigger ?? UnlockCondition.All(
                    UnlockCondition.EraAtLeast(2),
                    UnlockCondition.EarnedThisRunAtLeast(choiceMilestone)),
                Options = [Option("a", stance: "s", weight: 1), Option("b")],
            });
        }

        return builder
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
    }

    /// <summary>
    /// 三层内容：表态绑在<b>中间</b>那一层，而那一层的完成条件是 <c>Always</c>——
    /// 于是"表态刚挂上就能舍命走人"这个形状可以精确地造出来。<para>
    /// 表态不绑末层，所以那条"结局不得抢在末层表态之前"的构建期规则不介入，
    /// 测的就只是跨层时待答队列的命运。
    /// </para>
    /// </summary>
    private static GameContent BuildEraAdvanceContent() =>
        new GameContentBuilder("G")
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一", Completion = UnlockCondition.Always },
                new EraDefinition { Index = 2, Id = "e2", Name = "二", Completion = UnlockCondition.Always },
                new EraDefinition
                {
                    Index = 3,
                    Id = "e3",
                    Name = "三",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e6),
                })
            .Add(new ChoiceDefinition
            {
                Id = "c",
                Speaker = "她",
                Prompt = "？",
                EraId = "e2",
                Trigger = UnlockCondition.Always,
                Options = [Option("a", stance: "s", weight: 1), Option("b")],
            })
            .AddEndings(new EndingDefinition
            {
                Id = "e",
                Name = "E",
                Text = "T",
                Condition = UnlockCondition.All(
                    UnlockCondition.EraAtLeast(3),
                    UnlockCondition.EarnedThisRunAtLeast(1e6)),
            })
            .Build();

    /// <summary>
    /// 没有分层转生的最小内容：<c>GameEngine.Ascend()</c> 于是走"经典转生"
    /// （<c>PrestigeSystem.Ascend</c>）而不是舍命，用来验另一条重置路径。
    /// 结局条件用 <c>Always</c>（它同时也是构建期要求的那个兜底结局形状）。
    /// </summary>
    private static GameContent BuildNoEraContent() =>
        new GameContentBuilder("G")
            .AddStances(new StanceDefinition { Id = "s", Name = "S" })
            .Add(new ChoiceDefinition
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
                Condition = UnlockCondition.Always,
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
