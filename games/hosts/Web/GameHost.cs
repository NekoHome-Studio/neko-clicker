using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Persistence;
using NekoClicker.Core.Views;
using NekoClicker.Hosts;

namespace NekoClicker.Web;

/// <summary>一条命令执行完之后回给浏览器的东西。</summary>
/// <param name="Ok">是否成功。</param>
/// <param name="Message">给人看的一句话（失败原因 / 结算结果）。</param>
/// <param name="Seq">处理完这条命令之后的快照序号。</param>
/// <param name="Text">
/// 这条命令**带回来的文本**（今天只有 <c>export</c> 用它，别的命令一律 <c>null</c>）。<para>
/// <b>为什么不塞进 <see cref="Message"/></b>：<see cref="Message"/> 是一句给人看的话，
/// 要短、要能显示在提示条上；导出文本是一份 1.5~6 KB 的机器数据，它的去处是文本框与剪贴板。
/// 两者混进一个字段，界面上就只能二选一——而那正是 <c>SAVE_TRANSFER_PLAN</c> §4.1
/// 明确要避免的（导出必须是**可复制的文本**，同时状态还得说得出话）。
/// </para>
/// </param>
public sealed record CommandOutcome(bool Ok, string Message, long Seq, string? Text = null);

/// <summary>
/// 调试门跳层的结果（见 <c>engine/docs/WEB_DEBUG_GATE_PLAN.md</c>）。<para>
/// <see cref="Min"/> / <see cref="Max"/> 一并回报，是为了让 400 的正文里能带上**这个包的**
/// 合法层号范围——只说"越界了"等于让人去猜边界在哪。没有分层转生的包 <see cref="Max"/> 为 <c>0</c>。
/// </para>
/// </summary>
/// <param name="Applied">是否真的跳了。</param>
/// <param name="Message">给人看的一句话（成功说明或拒绝原因）。</param>
/// <param name="From">跳之前的层号（被拒时为当前层号）。</param>
/// <param name="To">跳到的层号（被拒时为 <c>0</c>）。</param>
/// <param name="Min">这个包的合法最小层号（没有层时为 <c>0</c>）。</param>
/// <param name="Max">这个包的合法最大层号（没有层时为 <c>0</c>）。</param>
public sealed record DebugEraJump(bool Applied, string Message, int From, int To, int Min, int Max);

/// <summary>
/// 一个内容包的游戏会话宿主。<para>
/// <b>这里是整个 Web 前端最要紧的一处设计：引擎只有一个主人。</b>
/// <see cref="GameEngine"/> 是单线程可变对象（<c>_dirty</c> / <c>_production</c> / <c>_modifiers</c>
/// 都是普通字段），而 ASP.NET Core 用线程池处理请求——两个并发请求同时碰它就会坏掉。
/// 浏览器并发是常态：连点两下就是两个 <c>POST /api/command</c>，EventSource 重连时还会更多。
/// </para>
/// <para>
/// 所以这个类里的**所有**引擎访问都发生在同一个后台线程上：
/// <list type="number">
///   <item>一条专用线程按真实时间调 <see cref="GameEngine.Update"/>（引擎内部是 30Hz 固定步长累加器）；</item>
///   <item>HTTP 命令被投进 <see cref="Channel{T}"/>，由这条线程取出并执行；</item>
///   <item>快照也只在这条线程上生成，生成完再交给订阅者——因此订阅者拿到的一定是自洽的一帧，
///         不会读到"建筑买了但产量还没重算"的中间态。</item>
/// </list>
/// </para>
/// <para>
/// 存档复用引擎自带的 <see cref="FileStorage"/>（它写盘时是"先写 <c>.tmp</c>、反解一遍确认
/// 读得回来、再原子替换，并把上一份留下"的）与
/// <see cref="SaveManager"/>，槽位与终端 Demo 同构，于是<b>同一份存档两个前端都能接着玩</b>。
/// </para>
/// </summary>
public sealed class GameHost : IAsyncDisposable
{
    /// <summary>推送节流：250ms（4 Hz）。与 30Hz 的模拟步长解耦——推得太快没有意义，</summary>
    /// <remarks>数字靠前端的 CSS 过渡补平滑就够了。</remarks>
    private const double PushIntervalSeconds = 0.25;

    /// <summary>每满这么多个序号就强制推一次全量，兜住任何累积漂移。</summary>
    private const long FullResyncEvery = 120;

    private readonly WebPackage _package;
    private readonly GameEngine _engine;
    private readonly SaveManager _saves;
    private readonly ChoiceLatencyLog _latency;
    private readonly Channel<Func<Task>> _work = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly object _viewGate = new();
    private string _viewJson = "{}";
    private JsonObject _viewObject = new();
    private long _seq;
    private PurchaseMode _mode = PurchaseMode.Buy1;

    /// <summary>
    /// 这个会话被调试门跳层之后为 <c>true</c>，直到进程退出。<para>
    /// 它的唯一作用是**禁止一切落盘**：跳层是直接改数据，玩家的真实存档里不该出现一个他没玩过的层号。
    /// 用 <c>volatile</c> 是因为它由游戏线程写、由 HTTP 线程（拒绝存档命令）与关停线程（跳过退出存档）读。
    /// </para>
    /// </summary>
    private volatile bool _debugMode;

    private GameSnapshot _lastSnapshot;
    private readonly List<Channel<string>> _subscribers = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _thread;
    private readonly Stopwatch _wall = Stopwatch.StartNew();

    /// <summary><see cref="StopAsync"/> 只真正执行一次（否则第二次会在死掉的游戏线程上白等超时）。</summary>
    private int _stopped;
    private double _lastTick;
    private double _lastPush;
    private OfflineProgress? _offlineOnLoad;

    /// <summary>创建一个会话并开始推进。</summary>
    /// <param name="package">要玩的内容包。</param>
    /// <param name="saveRoot">存档根目录（键会映射成 <c>根目录/键</c>）。<c>null</c> 表示不落盘。</param>
    /// <param name="seed">随机种子；<c>0</c> 表示按时间随机。</param>
    /// <param name="latencyLogPath">作答延迟埋点文件路径（见 <see cref="ChoiceLatencyLog"/>）；<c>null</c> 表示不落盘。</param>
    public GameHost(WebPackage package, string? saveRoot = null, ulong seed = 0, string? latencyLogPath = null)
    {
        _package = package;
        _engine = new GameEngine(package.Build(), new GameEngineOptions
        {
            Clock = SystemClock.Instance,
            Seed = seed,
            GrantOfflineProgress = true,
            MaxNotifications = 64,
        });

        if (saveRoot is not null)
        {
            var storage = new FileStorage(saveRoot);
            // PackId 必须由宿主告诉 SaveManager（**不许从槽位名反推**，见 SaveManager.PackId）：
            // 十一个内容包写出来的存档逐字段同构，没有包标识就拦不住"把咖啡馆的存档导进末世包"。
            _saves = new SaveManager(_engine, storage, $"{package.Id}.json") { PackId = package.Id };
            if (_saves.HasSave() && _saves.Load()) _offlineOnLoad = _saves.LastOfflineProgress;
        }
        else
        {
            _saves = null!;
        }

        // 作答延迟埋点（理由见 ChoiceLatencyLog 的类注释）。**必须建在读档之后**：
        // 读档会把表态放进待答队列，而那些表态的"出现时刻"在这次会话之外，只能如实记成不可量。
        _latency = new ChoiceLatencyLog(_engine, package.Id, LatencyLogFormat.WebHost, latencyLogPath);
        _latency.Answered += sample => Console.WriteLine(
            $"[{package.Id}] ⏱ 表态「{sample.ChoiceId}」等了 {NumFormat.Duration(sample.SimulatedSeconds)}（模拟）"
            + $" / {sample.WallSeconds.ToString("0.#", CultureInfo.InvariantCulture)} 秒（真实）才作答。");
        _latency.Unmeasured += (choiceId, reason) =>
            Console.WriteLine($"[{package.Id}] ⏱ 表态「{choiceId}」量不出时长：{reason}。");

        _lastSnapshot = _engine.Snapshot(_mode);
        _viewJson = SnapshotProtocol.Serialize(_lastSnapshot);
        _viewObject = JsonNode.Parse(_viewJson) as JsonObject ?? new JsonObject();

        _thread = new Thread(TickerLoop)
        {
            Name = $"neko-game-{package.Id}",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    /// <summary>内容包。</summary>
    public WebPackage Package => _package;

    /// <summary>存档槽位名（不落盘时为 <c>null</c>）。</summary>
    public string? Slot => _saves is null ? null : _saves.Slot;

    /// <summary>读档时结算出的离线收益（没有则为 <c>null</c>）。</summary>
    public OfflineProgress? OfflineOnLoad => _offlineOnLoad;

    /// <summary>当前快照序号。</summary>
    public long Seq => Interlocked.Read(ref _seq);

    /// <summary>当前的购买模式（决定快照里建筑行的批量数量与总价）。</summary>
    public PurchaseMode Mode => _mode;

    /// <summary>
    /// 这个会话是否被调试门跳层过。<para>
    /// 一旦为 <c>true</c>，<see cref="SaveAsync"/> 与 <see cref="StopAsync"/> 都不再写存档
    /// ——调试不该留下痕迹（plan §4）。
    /// </para>
    /// </summary>
    public bool DebugMode => _debugMode;

    /// <summary>取当前快照的 JSON 文本（已序列化好，读取不需要碰引擎）。</summary>
    public string ViewJson
    {
        get
        {
            lock (_viewGate) return _viewJson;
        }
    }

    /// <summary>
    /// 取当前快照的 JSON 对象副本（已深拷贝，调用方随便改）。<para>
    /// 用在新客户端接入时：它必须先拿到一份全量，之后才吃得下增量帧。
    /// </para>
    /// </summary>
    public JsonObject ViewSnapshot()
    {
        lock (_viewGate) return (JsonObject)_viewObject.DeepClone();
    }

    /// <summary>
    /// 订阅推送。返回的 <see cref="Channel{T}"/> 容量为 1 且<b>丢旧留新</b>：
    /// 浏览器卡住时不该把服务端拖下水，而挂机游戏的下一帧本来就包含上一帧的全部真相。
    /// </summary>
    public ChannelReader<string> Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_viewGate)
        {
            // 第一帧必须是全量：客户端从它开始累积增量
            channel.Writer.TryWrite(SnapshotProtocol.FullEnvelope(Seq, _viewObject));
            _subscribers.Add(channel);
        }

        return channel.Reader;
    }

    /// <summary>退订并释放这个客户端。</summary>
    public void Unsubscribe(ChannelReader<string> reader)
    {
        lock (_viewGate)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_subscribers[i].Reader, reader))
                {
                    _subscribers[i].Writer.TryComplete();
                    _subscribers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>把一条命令交给游戏线程执行，并等它做完（带超时，避免请求永久挂住）。</summary>
    public Task<CommandOutcome> ExecuteAsync(Func<GameEngine, CommandOutcome> action, CancellationToken cancellationToken = default)
        => RunOnGameThreadAsync(
            action,
            reason => new CommandOutcome(false, reason, Seq),
            ex => new CommandOutcome(false, $"内部错误：{ex.Message}", Seq),
            cancellationToken);

    /// <summary>
    /// <see cref="ExecuteAsync"/> 的通用版：调试跳层要把"这个包的合法层号范围"回报给调用方，
    /// 而那不是 <see cref="CommandOutcome"/> 装得下的东西。<para>
    /// <b>"所有引擎访问都在同一条游戏线程上"这条不变式由这里统一守住</b>——
    /// 复用同一段排队/超时/异常兜底，而不是在跳层那里另写一套。
    /// </para>
    /// </summary>
    /// <param name="action">要在游戏线程上跑的动作。</param>
    /// <param name="onUnavailable">游戏线程已经不在时（关停中 / 不响应）用什么结果回答。</param>
    /// <param name="onError">动作抛异常时用什么结果回答。</param>
    /// <param name="cancellationToken">请求的取消令牌。</param>
    private async Task<T> RunOnGameThreadAsync<T>(
        Func<GameEngine, T> action,
        Func<string, T> onUnavailable,
        Func<Exception, T> onError,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_work.Writer.TryWrite(() =>
            {
                try
                {
                    completion.TrySetResult(action(_engine));
                }
                catch (Exception ex)
                {
                    completion.TrySetResult(onError(ex));
                }
                return Task.CompletedTask;
            }))
        {
            return onUnavailable("游戏正在关闭。");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return onUnavailable("游戏线程没有响应。");
        }
    }

    /// <summary>
    /// 把引擎的动作结果翻成回给浏览器的结果。<para>
    /// 用引擎自己的 <c>Message</c> 而不是在这里重写一套错误码表：那句话已经是面向玩家的中文，
    /// 而且换一个内容包时它会跟着包的语气变（"钱不够" / "客人还没来"）。
    /// 宿主重造一套只会两边慢慢说不到一起去。
    /// </para>
    /// </summary>
    public static CommandOutcome FromResult(PurchaseResult result, long seq)
        => new(result.Success, result.Message, seq);

    /// <summary>同上，用于舍命/转生。</summary>
    public static CommandOutcome FromResult(AscensionResult result, long seq)
        => new(result.Success, result.Message, seq);

    /// <summary>同上，用于金猫。</summary>
    public static CommandOutcome FromResult(GoldenCookieResult result, long seq)
        => new(result.Success, result.Message, seq);

    /// <summary>点击结果。引擎不产出面向玩家的文案（点一下不该刷屏），这里自己拼一句。</summary>
    public static CommandOutcome FromResult(ClickResult result, long seq)
        => new(true, $"+{NumFormat.FormatLong(result.Gained)}", seq);

    /// <summary>切换购买模式（改变快照里建筑/升级行的批量数量与总价）。</summary>
    public async Task<CommandOutcome> SetModeAsync(PurchaseMode mode)
        => await ExecuteAsync(engine =>
        {
            _mode = mode;
            // 模式变了会让每个建筑行的 BatchAmount/BatchPrice 都变，直接推一次全量
            Publish(forceFull: true);
            return new CommandOutcome(true, $"批量：{Describe(mode)}", Seq);
        }).ConfigureAwait(false);

    /// <summary>立刻存一次盘（正常退出时也必须调它）。</summary>
    public async Task<CommandOutcome> SaveAsync()
        => await ExecuteAsync(_ =>
        {
            if (_saves is null) return new CommandOutcome(false, "这个会话不落盘。", Seq);

            // 调试跳层之后一个字节都不许写：玩家没玩过的层号不该出现在他的真实存档里（plan §4）
            if (_debugMode) return new CommandOutcome(false, "调试模式下不写存档（跳层只在内存里，退出即弃）。", Seq);

            // 存档是"报了成功就等于玩家信了"的动作：`SaveManager.Save()` 把失败翻成
            // `false` + `LastError`（它自己不抛），所以不看返回值就等于把"没存上"说成"已存档"。
            return _saves.Save()
                ? new CommandOutcome(true, "已存档。", Seq)
                : new CommandOutcome(false, $"存档失败：{_saves.LastError?.Message ?? "原因未记录"}", Seq);
        }).ConfigureAwait(false);

    /// <summary>
    /// 导出一份可复制粘贴的存档文本（信封格式，见 <see cref="SaveManager.Export"/>）。<para>
    /// <b>它不写盘</b>——导出是一次读。文本走 <see cref="CommandOutcome.Text"/> 回给浏览器，
    /// 因为那正是一段要被人整份复制走的东西，不是一句给人看的话。
    /// </para>
    /// <para>
    /// <b>调试跳层的会话不导出</b>（与 <see cref="SaveAsync"/> 同一条规矩，理由见
    /// <see cref="DebugMode"/>）：跳层只在内存里。导出物却是**耐久且会离开这台机器**的
    /// ——它会被人贴进别的会话、写进别的存档，等于把一个玩家没玩过的层号发了出去。
    /// 这个会话不落盘时也没有可导的东西（没有槽位）。
    /// </para>
    /// </summary>
    /// <returns>成功时 <see cref="CommandOutcome.Text"/> 里是整份导出文本。</returns>
    public async Task<CommandOutcome> ExportAsync()
        => await ExecuteAsync(_ =>
        {
            if (_saves is null) return new CommandOutcome(false, "这个会话不落盘，没有可导出的存档。", Seq);

            // 与 SaveAsync 同一条：调试跳层留下的层号不该出现在任何耐久的产物里。
            if (_debugMode) return new CommandOutcome(false, "调试模式下不导出（跳层只在内存里，退出即弃）。", Seq);

            try
            {
                string text = _saves.Export();
                return new CommandOutcome(
                    true,
                    $"已生成导出文本（内容包「{_package.Id}」、{text.Length} 个字符）——整份复制走，"
                    + "就能在别处导入；也可以下载成 .json 文件。",
                    Seq,
                    text);
            }
            catch (Exception ex)
            {
                // Export() 只在"引擎里的状态压根不是一份存档"时才抛（那是代码 bug），
                // 但界面这一侧不该看见一个 500：照样翻成一句人话（与 FromResult 的约定同源）。
                return new CommandOutcome(false, $"导出失败：{ex.Message}", Seq);
            }
        }).ConfigureAwait(false);

    /// <summary>
    /// 导入一份 <see cref="ExportAsync"/> 出来的文本。<para>
    /// <b>这里一行校验都不写</b>：尺寸、信封、版本、校验和、归属、结构、有限数字七道闸
    /// 全在 <see cref="SaveManager.Import"/> 里，而且**全都在碰磁盘之前**——所以"坏输入"
    /// 这条路上磁盘与内存一动都没动。宿主重复实现一遍只会得到第二套语义。
    /// </para>
    /// <para>
    /// 失败时把引擎自己的 <see cref="SaveTransferResult.Message"/> <b>原样</b>回给浏览器
    /// （与 <see cref="FromResult"/> 同一条约定）：那句话里已经写着"是哪个字段、期望什么、
    /// 该怎么办"，宿主再翻译一遍只会两边慢慢说不到一起去。
    /// </para>
    /// </summary>
    /// <param name="text">粘贴进来的导出文本。</param>
    /// <returns>结果；<b>坏输入不抛异常</b>，而是带着精确原因返回。</returns>
    public async Task<CommandOutcome> ImportAsync(string text)
        => await ExecuteAsync(_ =>
        {
            if (_saves is null) return new CommandOutcome(false, "这个会话不落盘，没法导入。", Seq);

            // 调试跳层的会话不导入：导入会**写盘**（把刚粘进来的那份变成当前存档），
            // 而"调试不该留下痕迹"这条在 SaveAsync 上已经定过（plan §4）。
            if (_debugMode) return new CommandOutcome(false, "调试模式下不导入（跳层只在内存里，退出即弃）。", Seq);

            SaveTransferResult result = _saves.Import(text);
            if (!result.Ok) return new CommandOutcome(false, result.Message, Seq);

            // 导入换掉的是**整局状态**，所以直接推一份全量：增量协议算得出差量，
            // 但"这一下换了一整局"是个大事件，让这份起点视图（新客户端接入时拿的就是它）
            // 立刻正确，比省一帧值钱得多。顺带也让调用方**紧接着**取快照就一定是导入后的状态。
            Publish(forceFull: true);

            return new CommandOutcome(true, result.Message, Seq);
        }).ConfigureAwait(false);

    /// <summary>
    /// 调试门用：把这一局<b>直接置到</b>第 <paramref name="requestedEra"/> 层。<para>
    /// <b>这是直接改数据，不是正常推进。</b>引擎没有"强行推进到第 N 层"的公开接口
    /// （<see cref="GameEngine.Ascend"/> 要过 <see cref="GameEngine.EraGate"/>，即必须完成本层主线），
    /// 所以这里只写 <see cref="GameState.Era"/>，于是<b>舍命该做的事被跳过了</b>：
    /// 跨层继承（比例与白名单）、层历史（<c>EraHistory</c> / <c>EraCompleted</c>）、
    /// 转生货币结算（<c>PrestigeLevel</c> / <c>PrestigeChips</c> / <c>Ascensions</c>）。
    /// 跳过的东西必须由调用方在响应正文里明确回报，不能让人以为这是正常推进（plan §3）。
    /// </para>
    /// <para>
    /// 同时<b>关掉这个会话的存档</b>：跳层只在内存里。否则自动存档（每 60 秒一次）会把这个
    /// 层号写进玩家的真实存档，而他自己不会知道（plan §4，选了推荐方案 a）。
    /// </para>
    /// </summary>
    /// <param name="requestedEra">
    /// 来自 URL 的原始文本。<b>刻意不收 <c>int</c></b>：解析与范围校验都放在游戏线程上做，
    /// 这样 400 的正文里一定能带上这个包**真实的**合法范围，而不是猜一个。
    /// </param>
    /// <returns>跳层结果；被拒时 <see cref="DebugEraJump.Message"/> 里带着原因与合法范围。</returns>
    public async Task<DebugEraJump> JumpToEraAsync(string requestedEra)
        => await RunOnGameThreadAsync(
            engine =>
            {
                GameContent content = engine.Content;
                GameState state = engine.State;

                if (!content.HasEras)
                {
                    return new DebugEraJump(
                        false,
                        $"内容包「{_package.Id}」没有分层转生，没有层可跳。",
                        state.Era, 0, 0, 0);
                }

                int max = content.MaxEraIndex;

                if (!int.TryParse(requestedEra, NumberStyles.Integer, CultureInfo.InvariantCulture, out int era))
                {
                    return new DebugEraJump(
                        false,
                        $"epoch「{requestedEra}」不是整数。合法范围：1..{max}。",
                        state.Era, 0, 1, max);
                }

                if (era < 1 || era > max)
                {
                    return new DebugEraJump(
                        false,
                        $"第 {era} 层越界（内容包「{_package.Id}」只有 {max} 层）。合法范围：1..{max}。",
                        state.Era, 0, 1, max);
                }

                int from = state.Era;
                state.Era = era;

                // 层耗时从"现在"起算：不写的话，之后正常舍命时这一层会被记成从旧层入场就开始的时长。
                state.EraEnteredPlayTimeSeconds = state.PlayTimeSeconds;

                _debugMode = true;
                if (_saves is not null) _saves.AutoSaveEnabled = false;

                engine.MarkDirty();
                Publish(forceFull: true); // 层号变了会让大部分数值都变，直接推一份全量

                return new DebugEraJump(
                    true,
                    $"已跳到第 {era} 层（原第 {from} 层）。跳过了跨层继承、层历史、转生货币结算；"
                    + "本次不自动存档，跳层只在内存里，退出即弃。",
                    from, era, 1, max);
            },
            reason => new DebugEraJump(false, reason, 0, 0, 0, 0),
            ex => new DebugEraJump(false, $"内部错误：{ex.Message}", 0, 0, 0, 0))
            .ConfigureAwait(false);

    /// <summary>
    /// 停止推进并存档。给 <c>ApplicationStopping</c> 用。<para>
    /// <b>这里的顺序是本质的。</b>1.2.0 声称"关停时经 <c>ApplicationStopping</c> 强制存档一次"，
    /// 而实测没有发生——原因就是顺序反了：<c>_stopping.Cancel()</c> 排在"把存盘任务排进队列"
    /// <b>之前</b>，ticker 循环当场退出，那条任务永远不会被排空。所以现在改为：
    /// <b>先把存盘任务排进去、等它真的执行完，再取消循环</b>；而且等待用的是这条任务自己的回执，
    /// <b>不绑在 <see cref="_stopping"/> 上</b>——绑上去的话一取消就立刻返回"游戏线程没有响应"，
    /// 等于把"到底写没写"换成了"我猜它写了"，那正是这个 bug 的另一半。
    /// </para>
    /// <para>
    /// 循环那一侧也补了一道：退出前会把队列最后排空一次（见 <see cref="TickerLoop"/> 的收尾），
    /// 于是"取消之前排进来的工作"不会被静默丢掉。
    /// </para>
    /// </summary>
    public async Task StopAsync()
    {
        // 幂等：第二次调用时游戏线程已经死了，再排一条存盘任务只会白等超时。
        if (Interlocked.Exchange(ref _stopped, 1) == 1) return;

        // ① 关停存档。必须在游戏线程上做（它要读引擎状态），而且必须在循环还活着的时候做。
        if (_saves is not null)
        {
            var written = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_work.Writer.TryWrite(() =>
                {
                    try
                    {
                        // 调试跳层过的会话不落盘：否则"退出时的那一次存档"会把调试状态写进玩家的真实存档。
                        // 这一步是兜底——正常退出时 AutosaveEnabled 早已被跳层关掉了（见 JumpToEraAsync）。
                        if (_debugMode)
                        {
                            Console.WriteLine($"[{_package.Id}] 调试模式：退出时不写存档。");
                        }
                        else if (_saves.Save())
                        {
                            Console.WriteLine($"[{_package.Id}] 退出前已存档：{_saves.Slot}");
                        }
                        else
                        {
                            // 退出存档失败以前是**静默**的：`Save()` 把异常翻成了 false + LastError，
                            // 而这里不读返回值，于是日志照样写"退出前已存档"。那正是"以为存上了、
                            // 其实没存"的形状——必须喊出来。
                            Console.Error.WriteLine(
                                $"[{_package.Id}] 退出存档失败：{_saves.LastError?.Message ?? "原因未记录"}"
                                + $"，{_saves.Slot} 没有更新，进程带着上一次的存档退出。");
                        }
                    }
                    catch (Exception ex)
                    {
                        // 关停路径上不再抛：存不上也不能让进程退不出去。**但必须喊出来**——
                        // "以为存上了、其实没存"正是 1.2.0 那句话造成的伤害。
                        Console.Error.WriteLine($"[{_package.Id}] 退出存档失败：{ex.Message}");
                    }
                    finally
                    {
                        written.TrySetResult(true);
                    }

                    return Task.CompletedTask;
                }))
            {
                try
                {
                    await written.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLine(
                        $"[{_package.Id}] 退出存档 5 秒内没有执行完，进程可能带着没落盘的进度退出。");
                }
            }
            else
            {
                Console.Error.WriteLine(
                    $"[{_package.Id}] 退出存档没能排进游戏线程（队列已封闭），本次关停没有写存档。");
            }
        }

        // ② 现在才让循环停下。它会先把手里的活干完再退出（TickerLoop 的收尾排空）。
        _stopping.Cancel();
        _work.Writer.TryComplete();
        _thread.Join(TimeSpan.FromSeconds(2));

        // ③ 游戏线程已经停了 —— 这时读埋点的累计值才是安全的（见 ChoiceLatencyLog 的线程约定）。
        //    汇总同时进控制台与埋点文件：否则"真人到底等了多久"只留在文件里，而控制台当场就关了。
        Console.WriteLine($"[{_package.Id}] {_latency.Summary()}");
        _latency.Dispose();

        lock (_viewGate)
        {
            foreach (Channel<string> subscriber in _subscribers) subscriber.Writer.TryComplete();
            _subscribers.Clear();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    // ------------------------------------------------------------------ 内部

    /// <summary>
    /// 游戏线程主循环。这是**唯一**允许碰 <see cref="_engine"/> 的地方。
    /// </summary>
    private void TickerLoop()
    {
        _lastTick = _wall.Elapsed.TotalSeconds;

        while (!_stopping.IsCancellationRequested)
        {
            // ① 先执行排队的命令（它们也在这个线程上，所以和 tick 天然互斥）
            DrainPendingWork();

            // ② 按真实经过时间推进。引擎内部是 30Hz 固定步长累加器，
            //    所以这里调用得比 30Hz 更频繁也不会改变模拟结果。
            double now = _wall.Elapsed.TotalSeconds;
            double delta = now - _lastTick;
            _lastTick = now;
            _engine.Update(delta);

            // ③ 节流推送
            if (now - _lastPush >= PushIntervalSeconds)
            {
                _lastPush = now;
                Publish(forceFull: false);
            }

            Thread.Sleep(8);
        }

        // 收尾：取消之后也要把队列排空。**这是"关停时强制存档"能成立的另一半**——
        // 关停路径正是往这个队列里排一条存盘任务，而循环若在排空前就退出，
        // 那条任务就被静默丢掉了（1.2.0 的 bug 就是这个形态：任务排进去了，只是没人执行）。
        DrainPendingWork();
    }

    /// <summary>把队列里现有的工作全部执行掉。<b>只能在游戏线程上调用。</b></summary>
    private void DrainPendingWork()
    {
        while (_work.Reader.TryRead(out Func<Task>? job))
        {
            try
            {
                job();
            }
            catch (Exception)
            {
                // 单条命令出错不该杀死整个会话；结果已经由 ExecuteAsync 回给对方了
            }
        }
    }

    /// <summary>
    /// 生成新的一帧并交给所有订阅者。<b>必须在游戏线程上调用。</b>
    /// </summary>
    private void Publish(bool forceFull)
    {
        GameSnapshot current = _engine.Snapshot(_mode);
        SnapshotProtocol.Delta delta = SnapshotProtocol.Diff(_lastSnapshot, current);
        JsonObject changed = delta.Changed;
        _lastSnapshot = current;

        if (changed.Count == 0 && !forceFull) return;

        long seq = Interlocked.Increment(ref _seq);

        lock (_viewGate)
        {
            if (_subscribers.Count == 0)
            {
                // 没人在看：也要维护"当前视图"，因为新客户端接入时要拿它当全量的起点。
                // 这里必须用带量化的那个重载，否则这份视图与后续增量累积出来的对不上。
                SnapshotProtocol.Merge(_viewObject, delta);
                _viewJson = SnapshotProtocol.Serialize(_viewObject);
                return;
            }
        }

        // 每 FullResyncEvery 帧推一次全量：增量协议一旦有一帧丢了（连接抖动、客户端漏写），
        // 靠它对账恢复。代价是每 30 秒多几十 KB，换来"永远不会长期漂移"。
        bool full = forceFull || seq % FullResyncEvery == 0;

        lock (_viewGate)
        {
            SnapshotProtocol.Merge(_viewObject, delta);
            _viewJson = SnapshotProtocol.Serialize(_viewObject);

            string frame = full
                ? SnapshotProtocol.FullEnvelope(seq, _viewObject)
                : SnapshotProtocol.DeltaEnvelope(seq, changed);

            foreach (Channel<string> subscriber in _subscribers) subscriber.Writer.TryWrite(frame);
        }
    }

    /// <summary>给人看的模式名。</summary>
    public static string Describe(PurchaseMode mode) => mode switch
    {
        PurchaseMode.Buy1 => "买 1",
        PurchaseMode.Buy10 => "买 10",
        PurchaseMode.Buy100 => "买 100",
        PurchaseMode.BuyMax => "买满",
        PurchaseMode.Sell1 => "卖 1",
        PurchaseMode.Sell10 => "卖 10",
        PurchaseMode.SellMax => "全卖",
        _ => mode.ToString(),
    };

    /// <summary>解析 URL / 命令里的模式名。无法识别时返回 <c>null</c>。</summary>
    public static PurchaseMode? ParseMode(string? text) => text?.ToLowerInvariant() switch
    {
        "buy1" or "1" => PurchaseMode.Buy1,
        "buy10" or "10" => PurchaseMode.Buy10,
        "buy100" or "100" => PurchaseMode.Buy100,
        "buymax" or "max" => PurchaseMode.BuyMax,
        "sell1" => PurchaseMode.Sell1,
        "sell10" => PurchaseMode.Sell10,
        "sellmax" => PurchaseMode.SellMax,
        _ => null,
    };

    /// <summary>把快照里的一行摘要成日志（给"最近消息"用）。</summary>
    public static string Summarize(JsonObject snapshot)
    {
        double cookies = snapshot["cookies"]?.GetValue<double>() ?? 0;
        string text = snapshot["cookiesText"]?.GetValue<string>() ?? NumFormat.FormatLong(cookies);
        string currency = snapshot["currencyName"]?.GetValue<string>() ?? string.Empty;
        return $"{text} {currency}";
    }
}
