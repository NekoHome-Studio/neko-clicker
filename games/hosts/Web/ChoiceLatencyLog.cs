using System.Diagnostics;
using System.Globalization;
using System.Text;
using NekoClicker.Core;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Web;

/// <summary>
/// 一条量到的作答延迟。<para>
/// <see cref="SimulatedSeconds"/> 与 <see cref="WallSeconds"/> <b>两个都记</b>：
/// 宽限期是模拟秒，所以定宽限期要用的是前者；而后者是这次等待真实的墙钟长度。
/// 两者不一致说明模拟与真实脱了节（长挂机、时钟被改、宿主被挂起），
/// 那种情况下把两个数合并成一个就是在掩盖事实。
/// </para>
/// </summary>
/// <param name="ChoiceId">表态 id。</param>
/// <param name="OptionId">玩家选了哪一项。</param>
/// <param name="SimulatedSeconds">从表态进入待答队列到作答，引擎 <c>PlayTimeSeconds</c> 之差。</param>
/// <param name="WallSeconds">同一段等待的墙钟秒数（单调时钟，不受系统时间调整影响）。</param>
/// <param name="AnsweredAt">作答时刻（UTC）。</param>
internal readonly record struct LatencySample(
    string ChoiceId,
    string OptionId,
    double SimulatedSeconds,
    double WallSeconds,
    DateTimeOffset AnsweredAt);

/// <summary>
/// 埋点文件的文本格式。<b>全部是纯函数</b>，因此格式本身可以在没有宿主、没有引擎的情况下用例钉住
/// ——"写出来的东西没人解析得动"属于典型的沉默失败：文件会安安静静地存在，而里面的数字没人能用。
/// </summary>
internal static class LatencyLogFormat
{
    /// <summary>列分隔符：制表符。表态 id / 选项 id 都是标识符，不会含制表符，于是不需要引号转义。</summary>
    public const char Separator = '\t';

    /// <summary>有样本的行。</summary>
    public const string SampleTag = "S";

    /// <summary>不可量、但要如实记账的行。</summary>
    public const string UnmeasuredTag = "U";

    /// <summary>表头的第一行；同时它就是"这个文件是本埋点写的"的判据（见 <see cref="LatencyLogFile.Ensure"/>）。</summary>
    public const string HeaderSignature = "# neko-clicker 作答延迟埋点（Web 宿主）";

    /// <summary>
    /// 文件里已经有**别人**写的内容时的分隔说明。<para>
    /// 这不是假想：<c>artifacts/latency.txt</c> 上真的躺着一份**旧埋点实现**留下的
    /// <c>heartbeat</c> 文件（2026-10-01）。覆盖它是毁掉别人的数据；什么都不说地混写，
    /// 则会让之后读这个文件的人把两种格式当成一种——而"这份文件是唯一的人类数据来源"，
    /// 混起来就等于没有数据。
    /// </para>
    /// </summary>
    public const string ForeignContentNote =
        "# --- 以上内容不是当前埋点实现写的（可能是旧实现或别的工具），列意未经验证；"
        + "以下从这段表头开始是当前实现，只追加。---";

    /// <summary>
    /// 表头（文件不存在时写一次）。<b>自述</b>是硬要求：这份文件的主要读者不是坐在终端前的人，
    /// 而是隔了很久才打开它的另一个人（或另一个会话）——没有表头，那些数字就只是一串浮点数。
    /// </summary>
    public const string Header = """
# neko-clicker 作答延迟埋点（Web 宿主）—— 只追加，不重写。
#
# 为什么要它：结局落定前留给玩家作答的宽限期（GameEngineOptions.EndingGraceSeconds，
# 默认 30 模拟秒）是全项目唯一一个没有人类数据支撑的玩法参数。缺的不是"玩家过去有多少窗口"
# （那个量过：0.40 秒 ~ 11 分钟），而是"玩家需要多久"——机器人在毫秒内作答，引擎里量不到。
# 所以这个文件里只会有真人的数据行。
#
# 为什么记模拟秒：宽限期本身也是模拟秒。挂机造成的长尾必须被如实记下来，
# 而不是被"当时窗口没开着"折算掉。真实秒另占一列：两者不一致就是模拟与真实脱节，
# 不许悄悄折算成一个数。
#
# 行格式（制表符分隔，数字一律用不变文化的小数点，可直接解析）：
#   S <utc> <包id> <表态id> <选项id> <模拟秒> <真实秒> <当时生效的宽限期秒> <本次会话第几条样本>
#   U <utc> <包id> <表态id> <选项id> <不可量的原因>
#   # ...  自述行（会话开始 / 会话结束汇总 / 写盘出错）
""";

    /// <summary>UTC 时间戳（毫秒精度，固定宽度，便于对表）。</summary>
    public static string Timestamp(DateTimeOffset at)
        => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>数值一律走不变文化：埋点文件要能被另一个进程/另一种语言解析。</summary>
    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>一条有样本的行。</summary>
    public static string SampleLine(string packageId, LatencySample sample, double graceSeconds, int index)
        => string.Join(
            Separator,
            SampleTag,
            Timestamp(sample.AnsweredAt),
            packageId,
            sample.ChoiceId,
            sample.OptionId,
            Number(sample.SimulatedSeconds),
            Number(sample.WallSeconds),
            Number(graceSeconds),
            index.ToString(CultureInfo.InvariantCulture));

    /// <summary>一条"这次作答量不出来、原因是……"的行。<b>记账而不是丢掉</b>：丢掉的话，
    /// 最常见的 Web 情形（打开页面时表态已经挂着）会一条样本都不产出，
    /// 而"没有样本"会被读成"没人作答"——与事实正好相反。</summary>
    public static string UnmeasuredLine(
        string packageId,
        string choiceId,
        string optionId,
        string reason,
        DateTimeOffset at)
        => string.Join(Separator, UnmeasuredTag, Timestamp(at), packageId, choiceId, optionId, reason);

    /// <summary>会话开始的自述行（含当时生效的宽限期，便于日后核对参数到底是多少）。</summary>
    public static string SessionStartLine(string packageId, double graceSeconds, DateTimeOffset at)
        => $"# session start {Timestamp(at)} package={packageId} grace={Number(graceSeconds)}";

    /// <summary>会话结束的汇总行。<b>汇总用模拟秒的原始值</b>（不是给人看的"3 分 20 秒"）：
    /// 这一行是拿去做决策的输入，格式化了就没法再算。</summary>
    public static string SessionEndLine(
        string packageId,
        int samples,
        int unmeasured,
        double maxSeconds,
        double averageSeconds,
        double graceSeconds,
        DateTimeOffset at)
        => $"# session end {Timestamp(at)} package={packageId} samples={samples}"
           + $" unmeasured={unmeasured} max={Number(maxSeconds)} avg={Number(averageSeconds)}"
           + $" grace={Number(graceSeconds)}";

    /// <summary>写盘失败的自述行（失败必须留在文件里，而不是只在当时那一眼的控制台上）。</summary>
    public static string ErrorLine(string message, DateTimeOffset at)
        => $"# error {Timestamp(at)} {message.ReplaceLineEndings(" ")}";
}

/// <summary>
/// 只追加的埋点文件。<para>
/// <b>它的第一职责是"不抛"</b>：这些写入发生在引擎的事件回调里（也就是游戏线程上），
/// 异常会沿着事件总线打回调用方——最坏的一条路径是一次 HTTP 命令。
/// 埋点坏了不该让玩法坏掉。但"不抛"不等于"沉默"：失败会立刻写 stderr 一次、
/// 记进 <see cref="LastError"/> / <see cref="WriteFailures"/>，并（写不进去时除外）留在文件里。
/// </para>
/// </summary>
internal sealed class LatencyLogFile
{
    /// <summary>UTF-8，<b>不写 BOM</b>：追加式文件里 BOM 只会出现在开头，容易被误读成数据行的前缀。</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();

    /// <summary>创建一个指向 <paramref name="path"/> 的写入器（此时还不碰磁盘）。</summary>
    public LatencyLogFile(string path) => Path = System.IO.Path.GetFullPath(path);

    /// <summary>文件绝对路径。</summary>
    public string Path { get; }

    /// <summary>第一次写盘失败的原因；从未失败时为 <c>null</c>。</summary>
    public string? LastError { get; private set; }

    /// <summary>写盘失败的累计次数。</summary>
    public int WriteFailures { get; private set; }

    /// <summary>确保目录与表头就位（幂等：已有内容的文件绝不重写——它只追加）。</summary>
    /// <returns>文件可用时为 <c>true</c>。</returns>
    public bool Ensure()
    {
        lock (_gate)
        {
            try
            {
                string? directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string existing = File.Exists(Path) ? File.ReadAllText(Path, Utf8NoBom) : string.Empty;

                if (existing.Length == 0)
                {
                    File.AppendAllText(Path, LatencyLogFormat.Header + Environment.NewLine, Utf8NoBom);
                    return true;
                }

                if (existing.Contains(LatencyLogFormat.HeaderSignature, StringComparison.Ordinal)) return true;

                // 已有内容不是本埋点写的（实际遇到过：这个埋点的一份**旧实现**留下的 heartbeat 文件）。
                // **不覆盖**（那是别人的数据），也不让新行落在一个解释不了它们的表头下面：
                // 补一段分隔说明 + 完整表头，然后照常追加。同时喊一声——"格式混在一起"是最难查的一类坏账。
                Console.Error.WriteLine(
                    $"[作答延迟埋点] <{Path}> 里已有非本埋点写入的内容：不覆盖，补一段表头说明后继续追加。");
                File.AppendAllText(
                    Path,
                    LatencyLogFormat.ForeignContentNote + Environment.NewLine
                    + LatencyLogFormat.Header + Environment.NewLine,
                    Utf8NoBom);
                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                Report(ex);
                return false;
            }
        }
    }

    /// <summary>追加一段文本。<b>任何 IO 失败都在这里被吃掉并上报，绝不外抛。</b></summary>
    /// <returns>确实写进去了为 <c>true</c>。</returns>
    public bool Append(string text)
    {
        lock (_gate)
        {
            try
            {
                File.AppendAllText(Path, text + Environment.NewLine, Utf8NoBom);
                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                Report(ex);
                return false;
            }
        }
    }

    /// <summary>哪些异常算"写不进去"（其余异常照旧外抛：那才是真的程序错，不该被埋点吞掉）。</summary>
    private static bool IsIoFailure(Exception ex)
        => ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException;

    private void Report(Exception ex)
    {
        WriteFailures++;
        LastError = ex.Message;

        // 只在第一次刷屏：埋点每答一次就跑一遍，第二次以后重复打印只会把真正有用的日志淹掉。
        // 次数与最后一次的原因可以在会话结束的汇总行里看到。
        if (WriteFailures == 1)
        {
            Console.Error.WriteLine(
                $"[作答延迟埋点] 写不进 <{Path}>：{ex.Message}"
                + "（后续失败不再逐条刷屏；次数会在会话结束的汇总里给出。）");
        }
    }
}

/// <summary>
/// 量「玩家从看到表态到作答」用了多久（<b>模拟</b>秒）——Web 宿主版。<para>
/// <b>它是个测量埋点，不是玩法。</b>存在的理由：结局判定的作答宽限期
/// （<see cref="EndingSystem.DefaultGraceSeconds"/>，可由
/// <see cref="GameEngineOptions.EndingGraceSeconds"/> 外部配置）该定多少秒，取决于真人需要多久，
/// 而那个数引擎里量不到——机器人在毫秒内作答，量了也是 0。
/// </para>
/// <para>
/// 与终端宿主的 <c>Demo.Cli/ChoiceLatencyLog</c> 是<b>同构但不同程序集</b>的两份：Web 宿主
/// 不该为一个埋点去依赖 CLI 宿主（那会把两个宿主的发布节奏绑在一起）。共用的是
/// <see cref="LatencyLogFormat"/> 这层纯函数，于是"两边写出来的行格式一致"是用例能钉住的事实。
/// </para>
/// <para>
/// <b>线程约定</b>：事件回调由引擎发布，所以本类的所有可变状态都只被游戏线程碰。
/// HTTP 线程只读 <see cref="Samples"/> / <see cref="Summary"/> 是不安全的——
/// 宿主在游戏线程停下之后才调用它们（见 <c>GameHost.StopAsync</c>）。
/// </para>
/// </summary>
internal sealed class ChoiceLatencyLog : IDisposable
{
    private readonly GameEngine _engine;
    private readonly string _packageId;
    private readonly LatencyLogFile? _file;
    private readonly IDisposable _triggeredSubscription;
    private readonly IDisposable _madeSubscription;

    private readonly Dictionary<string, (double Simulated, double Wall)> _shownAt = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unmeasurable = new(StringComparer.Ordinal);
    private readonly List<LatencySample> _samples = [];
    private readonly List<string> _unmeasuredAnswers = [];
    private bool _disposed;

    /// <summary>
    /// 挂上事件订阅，并把"会话开始前就已经挂着的表态"记成<b>不可量</b>。
    /// </summary>
    /// <param name="engine">要观察的引擎。</param>
    /// <param name="packageId">内容包 id（写进每一行，用于区分不同包的数据）。</param>
    /// <param name="filePath">埋点文件路径；<c>null</c> 表示只在控制台报（不落盘）。</param>
    public ChoiceLatencyLog(GameEngine engine, string packageId, string? filePath = null)
    {
        _engine = engine;
        _packageId = packageId;

        if (filePath is not null)
        {
            _file = new LatencyLogFile(filePath);
            _file.Ensure();
            _file.Append(LatencyLogFormat.SessionStartLine(packageId, EndingSystem.Grace(engine), DateTimeOffset.UtcNow));
        }

        // 读档接手 / 宿主刚起来时，待答队列里可能已经挂着表态了。它的"出现时刻"发生在
        // 这次会话之外，真实等待时长不可知——**必须记账，不能丢**：丢掉的话，Web 宿主里
        // 最常见的那一类作答会一条样本都不产出，而"没有样本"会被读成"没人作答"。
        foreach (string id in engine.State.PendingChoices) _unmeasurable.Add(id);

        _triggeredSubscription = engine.Events.Subscribe<ChoiceTriggeredEvent>(OnTriggered);
        _madeSubscription = engine.Events.Subscribe<ChoiceMadeEvent>(OnMade);
    }

    /// <summary>每次量到样本后回调（宿主据此写控制台日志）。</summary>
    public event Action<LatencySample>? Answered;

    /// <summary>每次作答但量不出时长时回调（宿主据此说清"为什么这次没有数"）。</summary>
    public event Action<string, string>? Unmeasured;

    /// <summary>本次量到的样本（只在游戏线程上单调增长）。</summary>
    public IReadOnlyList<LatencySample> Samples => _samples;

    /// <summary>这次会话里"答了但量不出时长"的条数。</summary>
    public int UnmeasuredCount => _unmeasuredAnswers.Count;

    /// <summary>埋点文件路径（未落盘时为 <c>null</c>）。</summary>
    public string? FilePath => _file?.Path;

    /// <summary>写盘失败的原因（没失败过为 <c>null</c>）。</summary>
    public string? FileError => _file?.LastError;

    /// <summary>写盘失败的次数。</summary>
    public int FileWriteFailures => _file?.WriteFailures ?? 0;

    private void OnTriggered(ChoiceTriggeredEvent evt)
    {
        // 重新触发（例如读档后又被扫到）会把"不可量"改回"可量"：那一刻起它确实被观测到了。
        _unmeasurable.Remove(evt.Id);
        _shownAt[evt.Id] = (_engine.PlayTimeSeconds, WallSeconds());
    }

    private void OnMade(ChoiceMadeEvent evt)
    {
        // 这个回调经引擎的事件总线跑在游戏线程上（也跑在 HTTP 命令的调用路径上），
        // 所以它**绝不允许**扔异常出去。
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (_unmeasurable.Remove(evt.ChoiceId))
            {
                const string reason = "表态在这次 Web 会话开始前就已经挂着，真实等待时长不可知";
                _unmeasuredAnswers.Add(evt.ChoiceId);
                _file?.Append(LatencyLogFormat.UnmeasuredLine(_packageId, evt.ChoiceId, evt.OptionId, reason, now));
                Unmeasured?.Invoke(evt.ChoiceId, reason);
                return;
            }

            if (!_shownAt.Remove(evt.ChoiceId, out (double Simulated, double Wall) shown))
            {
                const string reason = "作答前没有观测到它进入待答队列（宿主在中途接手？）";
                _unmeasuredAnswers.Add(evt.ChoiceId);
                _file?.Append(LatencyLogFormat.UnmeasuredLine(_packageId, evt.ChoiceId, evt.OptionId, reason, now));
                Unmeasured?.Invoke(evt.ChoiceId, reason);
                return;
            }

            var sample = new LatencySample(
                evt.ChoiceId,
                evt.OptionId,
                Math.Max(0, _engine.PlayTimeSeconds - shown.Simulated),
                Math.Max(0, WallSeconds() - shown.Wall),
                now);

            _samples.Add(sample);
            _file?.Append(LatencyLogFormat.SampleLine(_packageId, sample, EndingSystem.Grace(_engine), _samples.Count));
            Answered?.Invoke(sample);
        }
        catch (Exception ex)
        {
            // 宁可少一条样本，也不能让玩法受影响——但要说出来。
            Console.Error.WriteLine($"[{_packageId}] 作答延迟埋点记录样本时出错：{ex.Message}");
        }
    }

    /// <summary>单调墙钟秒数（<see cref="Stopwatch"/> 不受系统时间调整影响）。</summary>
    private static double WallSeconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    /// <summary>
    /// 汇总成一句话——这就是"宽限期该定多少秒"的输入。
    /// <b>只能在游戏线程停下之后调用</b>（见类注释的线程约定）。
    /// </summary>
    public string Summary()
    {
        // 生效值永远要报：宿主可能改过它，而"没有样本"并不意味着"参数没生效"。
        string grace = $"宽限期当前 {NumFormat.Duration(EndingSystem.Grace(_engine))}";
        string unmeasured = _unmeasuredAnswers.Count == 0
            ? string.Empty
            : $"，另有 {_unmeasuredAnswers.Count} 条量不出（会话开始前就挂着的表态）";

        if (_samples.Count == 0) return $"作答延迟：这次没有量到样本{unmeasured}（{grace}）。";

        double max = _samples.Max(s => s.SimulatedSeconds);
        double average = _samples.Average(s => s.SimulatedSeconds);

        string writeError = _file?.LastError is { } error ? $"，埋点写盘失败 {_file.WriteFailures} 次（{error}）" : string.Empty;

        return $"作答延迟：样本 {_samples.Count} 条，最长 {NumFormat.Duration(max)}"
               + $"，平均 {NumFormat.Duration(average)}{unmeasured}{writeError}（{grace}）。";
    }

    /// <summary>退订，并把这次会话的汇总写进文件。<b>幂等</b>（宿主可能在关停与释放两条路径上都调）。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _triggeredSubscription.Dispose();
            _madeSubscription.Dispose();

            if (_file is not null)
            {
                double max = _samples.Count == 0 ? 0 : _samples.Max(s => s.SimulatedSeconds);
                double average = _samples.Count == 0 ? 0 : _samples.Average(s => s.SimulatedSeconds);
                _file.Append(LatencyLogFormat.SessionEndLine(
                    _packageId,
                    _samples.Count,
                    _unmeasuredAnswers.Count,
                    max,
                    average,
                    EndingSystem.Grace(_engine),
                    DateTimeOffset.UtcNow));

                if (_file.LastError is { } error)
                    _file.Append(LatencyLogFormat.ErrorLine($"埋点写盘失败累计 {_file.WriteFailures} 次：{error}", DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{_packageId}] 关闭作答延迟埋点时出错：{ex.Message}");
        }
    }
}
