using NekoClicker.Core;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 量「玩家从看到表态到作答」用了多久（<b>模拟</b>秒）。<para>
/// <b>它是个测量埋点，不是玩法。</b>当年它存在的理由是：结局判定的作答宽限期
/// （<see cref="EndingSystem.DefaultGraceSeconds"/>，可由
/// <see cref="GameEngineOptions.EndingGraceSeconds"/> 外部配置）该定多少秒，
/// 取决于真人需要多久，而那个数引擎里量不到——机器人在毫秒内作答，量了也是 0。
/// </para>
/// <para>
/// <b>那个参数在 1.5.0 退役了</b>（落定条件改成了"玩家被展示过待答表态"，
/// 见 <see cref="GameEngine.MarkPendingChoicesShown"/>），所以这份数据不再是"某个待调参数的输入"，
/// 而是"真人从看到表态到作答要多久"这个事实本身。埋点照做：**这个数只有这一个来源**。
/// 汇总里仍然报出那个参数的值——它已不参与判定，但样本行里的值要能对上当年设置的是什么。
/// </para>
/// <para>
/// 记模拟秒而不是真实秒，是因为它换掉的那条宽限也用模拟秒：挂机造成的长尾必须被如实记下来，
/// 而不是被"当时没开窗口"折算掉。
/// </para>
/// <para>
/// 两个宿主（TUI 会话与无头播放）共用这一份实现。这不是为了省几行——
/// 埋点如果在某一条路径上不生效，它就会安安静静地什么都不产出，
/// 而那正是"沉默失败"最典型的形态。
/// </para>
/// </summary>
internal sealed class ChoiceLatencyLog : IDisposable
{
    private readonly GameEngine _engine;
    private readonly IDisposable _shownSubscription;
    private readonly IDisposable _madeSubscription;
    private readonly Dictionary<string, double> _shownAt = new(StringComparer.Ordinal);
    private readonly List<(string ChoiceId, double Seconds)> _samples = [];

    /// <summary>挂上事件订阅。</summary>
    /// <param name="engine">要观察的引擎。</param>
    public ChoiceLatencyLog(GameEngine engine)
    {
        _engine = engine;
        _shownSubscription = engine.Events.Subscribe<ChoiceTriggeredEvent>(OnTriggered);
        _madeSubscription = engine.Events.Subscribe<ChoiceMadeEvent>(OnMade);
    }

    /// <summary>每次作答后回调（选择 id、耗时秒数），宿主据此写日志。</summary>
    public event Action<string, double>? Answered;

    /// <summary>本次量到的样本。</summary>
    public IReadOnlyList<(string ChoiceId, double Seconds)> Samples => _samples;

    private void OnTriggered(ChoiceTriggeredEvent evt) => _shownAt[evt.Id] = _engine.PlayTimeSeconds;

    private void OnMade(ChoiceMadeEvent evt)
    {
        if (!_shownAt.Remove(evt.ChoiceId, out double shownAt)) return;

        double latency = Math.Max(0, _engine.PlayTimeSeconds - shownAt);
        _samples.Add((evt.ChoiceId, latency));
        Answered?.Invoke(evt.ChoiceId, latency);
    }

    /// <summary>
    /// 汇总成一行——"真人从看到表态到作答要多久"就是这个问题的输入。<para>
    /// 顺带报出那个已退役的宽限期参数：它 1.5.0 起不参与判定，但"当时设的是多少"仍然要能被对上。
    /// </para>
    /// </summary>
    public string Summary()
    {
        // 生效值永远要报：宿主可能用 --grace 改过它，而"没有样本"并不意味着"参数没生效"。
        string grace = $"宽限期参数 {NumFormat.Duration(EndingSystem.Grace(_engine))}（1.5.0 起已退役，判定不再使用）";

        if (_samples.Count == 0) return $"作答延迟：本次没有作答过表态，没有样本（{grace}）。";

        double max = _samples.Max(s => s.Seconds);
        double avg = _samples.Average(s => s.Seconds);

        return $"作答延迟：样本 {_samples.Count} 条，最长 {NumFormat.Duration(max)}"
               + $"，平均 {NumFormat.Duration(avg)}（{grace}）。";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _shownSubscription.Dispose();
        _madeSubscription.Dispose();
    }
}
