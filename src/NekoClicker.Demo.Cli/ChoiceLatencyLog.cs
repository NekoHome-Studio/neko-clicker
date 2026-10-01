using NekoClicker.Core;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 量「玩家从看到表态到作答」用了多久（<b>模拟</b>秒）。<para>
/// <b>它是个测量埋点，不是玩法。</b>存在的理由：结局判定的作答宽限期
/// （<see cref="EndingSystem.DefaultGraceSeconds"/>，可由
/// <see cref="GameEngineOptions.EndingGraceSeconds"/> 外部配置）该定多少秒，
/// 取决于真人需要多久，而那个数引擎里量不到——机器人在毫秒内作答，量了也是 0。
/// </para>
/// <para>
/// 记模拟秒而不是真实秒，是因为宽限期也用模拟秒：挂机造成的长尾必须被如实记下来，
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

    /// <summary>汇总成一行——这就是"宽限期该定多少秒"的输入。</summary>
    public string Summary()
    {
        // 生效值永远要报：宿主可能用 --grace 改过它，而"没有样本"并不意味着"参数没生效"。
        string grace = $"宽限期当前 {NumFormat.Duration(EndingSystem.Grace(_engine))}";

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
