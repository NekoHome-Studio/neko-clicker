using NekoClicker.Core.Content;
using NekoClicker.Core.Events;

namespace NekoClicker.Core;

/// <summary>
/// 终局判定：主线走完之后，按条件树挑出<b>一个</b>结局。<para>
/// 与成就 / 叙事 / 选择同频执行——还是同一件事，"把条件树定期扫一遍"。
/// </para>
/// <para>
/// 判定是<b>一次性</b>的：一旦记下结局就不再判。这既是"互斥"的实现方式，
/// 也避免了条件恒真导致的重复触发（立场权重、图鉴条数都是单调不减的，
/// 达成之后会一直成立）。
/// </para>
/// <para>
/// <b>落定前的等待</b>：结局条件成立时若还有未作答的表态，判定会先等一等——
/// 因为"最后几次表态"往往就发生在结局条件成立前的几秒里，而那时候玩家很可能还没反应过来。
/// <b>1.5.0 起这个等待是条件，不是定时；1.6.0 起那个条件从"看过"改成"答过"</b>：
/// 只要还有一条<b>答得上</b>的待答表态，结局就不落定——过多久都不落定。
/// 判据是"答没答"，不是"玩家看过没有"（<see cref="GameEngine.MarkPendingChoicesShown"/>
/// 那份展示记录现在<b>只作诊断</b>，见下）。
/// </para>
/// <para>
/// <b>代价（刻意接受的，不是遗漏）</b>：一个<b>从不作答</b>的玩家<b>永远</b>拿不到结局。
/// 1.5.0 之前存在一条"最多等 <see cref="DefaultGraceSeconds"/> 模拟秒"的兜底，那条兜底是
/// <b>刻意移除</b>的（<see cref="DefaultGraceSeconds"/> / <see cref="Grace"/> /
/// <see cref="GraceRemaining"/> 与 <see cref="GameEngineOptions.EndingGraceSeconds"/> 只为兼容
/// 保留，<b>不再参与判定</b>）。1.5.0 把条件放宽成"被展示过就算过"，1.6.0 收紧成"必须答过"——
/// 于是"不会永远悬着"这条性质不但没有回来，覆盖的范围还更大了。别再顺手把它改回去。
/// </para>
/// <para>
/// <b>不会卡死的那一半（1.6.0 明确处理的）</b>：只有<b>答得上</b>的待答表态拦结局。
/// 一条永远答不上的（内容里已经不存在 / 已经答过却又挂在队列里）不拦，而且引擎会
/// <b>发一条警告把它说出来</b>——静默忽略一批玩家看得见的待答项是不允许的。
/// 判据与理由见 <see cref="ChoiceSystem.AnswerablePendingCount"/>。
/// </para>
/// </summary>
public static class EndingSystem
{
    /// <summary>
    /// 结局条件成立后、<b>落定之前</b>留给玩家作答的宽限的<b>历史默认值</b>（模拟秒）。<para>
    /// <b>1.5.0 起它不再参与判定。</b>判定读的是"还有没有答得上的待答表态"
    /// （见 <see cref="Check"/>），不是"过了多久"。这个常量与
    /// <see cref="GameEngineOptions.EndingGraceSeconds"/> 一起保留，只为不破坏已经引用它们的
    /// 宿主与诊断输出（例如宿主那份作答延迟埋点会把它记进样本行）；
    /// 删掉公开成员要按 <c>engine/docs/VERSIONING.md</c> 升主版本，而这次是有意的 minor。
    /// </para>
    /// <para>
    /// 它当年是干什么的（留档）：《猫娘实验室》的末次表态门槛是 3.4e8，末层完成门槛是 1e9，
    /// 两者之间只隔几秒；终局判定一旦在这几秒里成立就永久锁死，于是"押了两次乌托邦、
    /// 第三次还在想"的玩家会拿到兜底结局。当时的修法是"给一段固定的等待"。
    /// 那段等待的窗口宽度实测在 0.40 秒到 11 分钟之间（同一个包能被一个恰好在场的增益
    /// 压缩 100 倍），所以 30 秒这个数从来没有人类数据支撑——这正是 1.5.0 用条件把它换掉的原因。
    /// </para>
    /// </summary>
    public const double DefaultGraceSeconds = 30.0;

    /// <summary>
    /// "结局已就绪"的累计游玩秒数；<c>0</c> 表示尚未就绪。<para>
    /// 存在 <see cref="GameState.Counters"/> 里而不是新增一个状态字段：这样它自动随存档往返、
    /// 不用改存档格式、也不用升存档版本号（<c>EraSystem.PeakCpsCounterKey</c> 是同一个套路）。
    /// </para>
    /// <para>
    /// 键以 <c>$</c> 开头，是为了跟 <see cref="CounterKey"/>（<c>ending_&lt;id&gt;</c>）划清界限：
    /// 任何结局 id 都拼不出这个键，不会撞名。
    /// </para>
    /// </summary>
    private const string ReadyAtCounterKey = "$ending_ready_at_play_time";

    /// <summary>扫描结局条件，达成第一个就记下。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>本次达成的结局；没有则返回 <c>null</c>。</returns>
    public static EndingDefinition? Check(GameEngine engine)
    {
        GameContent content = engine.Content;
        if (content.Endings.Count == 0) return null;

        GameState state = engine.State;
        if (state.EndingsReached.Count > 0) return null; // 一份存档只有一个结局

        // 就绪时刻 = 第一次"有结局条件成立"的那一拍。记下来就不再改：
        // 它现在只用于 IsReady / GraceRemaining 这两个诊断查询（判定本身不再看时间）。
        double readyAt = state.GetCounter(ReadyAtCounterKey);
        bool justBecameReady = readyAt <= 0;

        if (justBecameReady)
        {
            if (!AnyConditionMet(engine)) return null; // 还没够条件，没什么可等的

            readyAt = state.PlayTimeSeconds;
            state.SetCounter(ReadyAtCounterKey, readyAt);
        }

        // 还有**答得上**的表态挂着没答 → 先等玩家把它们答掉。
        //
        // 1.6.0 起这是唯一会推迟落定的机制，判据是"答没答"（不是 1.5.0 的"看过没有"），
        // 而且它是**条件**不是定时：只要还有一条答得上的待答表态，过多久都不落定
        // （代价见类注释——刻意接受）。
        //
        // 为什么是"答得上"而不是"队列非空"：队列里可能躺着永远答不上的 id
        // （内容改版删掉了它、换包读档、或它同时出现在已答表里），只数长度会把这一局永久卡死。
        // 见 ChoiceSystem.AnswerablePendingCount 的注释。
        int blocking = ChoiceSystem.AnswerablePendingCount(content, state);

        if (justBecameReady)
        {
            if (blocking > 0)
            {
                engine.Notify(
                    $"主线已经走完，但你还有 {blocking} 项表态没答。"
                    + "它们会决定你落到哪个结局——结局会等你把它们答完。",
                    NotificationKind.Warning,
                    "⏳");
            }

            // 答不上的那些必须**说出来**：它们既画不出来也点不动，玩家会以为界面坏了，
            // 而这一局其实是能收场的（结局不再等它们）。沉默地忽略是不允许的。
            int unanswerable = ChoiceSystem.UnanswerablePendingCount(content, state);
            if (unanswerable > 0)
            {
                engine.Notify(
                    $"有 {unanswerable} 项待答表态在当前内容里已经答不上了（旧存档跨了内容改版，"
                    + "或这条表态已经答过）——结局不再等它们。存档里的待答清单保持原样，不会被改写。",
                    NotificationKind.Warning,
                    "!");
            }
        }

        if (blocking > 0) return null;

        // 按 Priority 升序；同优先级按声明顺序（OrderBy 是稳定排序）。
        foreach (EndingDefinition ending in content.Endings.OrderBy(e => e.Priority))
        {
            if (!ending.Condition.IsMet(engine.Metrics, content)) continue;

            state.EndingsReached.Add(ending.Id);
            state.SetCounter(CounterKey(ending.Id), 1);

            engine.MarkDirty();
            engine.Events.Publish(new EndingReachedEvent(ending.Id, ending.Name, ending.Icon, ending.Text));
            return ending;
        }

        return null;
    }

    /// <summary>
    /// 是否已有结局条件成立、但还没落定（也就是"结局在等玩家"）。<para>
    /// 1.6.0 起等的<b>不是</b>一段时间，也不只是"看过"，而是玩家把那批待答表态<b>答掉</b>——
    /// 所以这个值为真时，只要有一条一直在等的表态没被作答，结局就可能无限期地等下去
    /// （见 <see cref="Check"/>）。
    /// </para>
    /// </summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static bool IsReady(GameContent content, GameState state)
        => content.Endings.Count > 0
           && state.EndingsReached.Count == 0
           && state.GetCounter(ReadyAtCounterKey) > 0;

    /// <summary>
    /// 本存档配置的<b>历史</b>宽限期值（模拟秒）：取
    /// <see cref="GameEngineOptions.EndingGraceSeconds"/>，未配置则用
    /// <see cref="DefaultGraceSeconds"/>。<para>
    /// <b>1.5.0 起判定不再读它</b>（<see cref="Check"/> 用的是"还有没有答得上的待答表态"这个条件）。
    /// 保留这个成员是为了不破坏已经引用它的宿主与诊断输出——例如宿主那份作答延迟埋点会把
    /// 生效值记进每一行样本，好让"当时的参数是多少"在日后仍然对得上。
    /// </para>
    /// <para>
    /// <b>不是"生效值"了</b>：它现在只回答"宿主把那个已经退役的参数设成了多少"。
    /// 真正会拦住结局落定的是"玩家把那些表态答完了没有"，与这个数无关。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public static double Grace(GameEngine engine)
    {
        double? configured = engine.Options.EndingGraceSeconds;
        if (configured is not { } value) return DefaultGraceSeconds;

        // 负数与 NaN 都是配置错误。宁可按"没有宽限"处理，也不要让 NaN 的比较
        // 悄悄把结局卡在永远不就绪的状态里（那会让存档再也走不到终局）。
        return double.IsNaN(value) || value < 0 ? 0 : value;
    }

    /// <summary>
    /// 按<b>历史</b>宽限期算出的剩余值；未就绪时为 <c>0</c>。<para>
    /// <b>1.5.0 起判定不再读它</b>，也没有任何截止时间可画了：结局要等到玩家看过那批待答表态
    /// 才落定，那个等待没有上界。保留成员只为兼容（正在画倒计时的宿主应当把那处倒计时撤掉，
    /// 否则它会显示一个不存在的期限）。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    public static double GraceRemaining(GameEngine engine)
    {
        double readyAt = engine.State.GetCounter(ReadyAtCounterKey);
        if (readyAt <= 0) return 0;

        return Math.Max(0, Grace(engine) - (engine.State.PlayTimeSeconds - readyAt));
    }

    /// <summary>是否有任何一个结局的条件已经成立。</summary>
    private static bool AnyConditionMet(GameEngine engine)
    {
        foreach (EndingDefinition ending in engine.Content.Endings)
            if (ending.Condition.IsMet(engine.Metrics, engine.Content)) return true;

        return false;
    }

    /// <summary>当前存档已达成的结局；未达成返回 <c>null</c>。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    public static EndingDefinition? Reached(GameContent content, GameState state)
    {
        foreach (string id in state.EndingsReached)
            if (content.EndingById.TryGetValue(id, out EndingDefinition? ending)) return ending;

        return null;
    }

    /// <summary>结局计数器的键（约定：<c>ending_&lt;id&gt;</c>）。</summary>
    /// <param name="endingId">结局 id。</param>
    public static string CounterKey(string endingId) => "ending_" + endingId;
}
