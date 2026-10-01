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

namespace NekoClicker.Web;

/// <summary>一条命令执行完之后回给浏览器的东西。</summary>
/// <param name="Ok">是否成功。</param>
/// <param name="Message">给人看的一句话（失败原因 / 结算结果）。</param>
/// <param name="Seq">处理完这条命令之后的快照序号。</param>
public sealed record CommandOutcome(bool Ok, string Message, long Seq);

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
/// 存档复用引擎自带的 <see cref="FileStorage"/>（它已经是"先写 .tmp 再原子替换"的）与
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
    private double _lastTick;
    private double _lastPush;
    private OfflineProgress? _offlineOnLoad;

    /// <summary>创建一个会话并开始推进。</summary>
    /// <param name="package">要玩的内容包。</param>
    /// <param name="saveRoot">存档根目录（键会映射成 <c>根目录/键</c>）。<c>null</c> 表示不落盘。</param>
    /// <param name="seed">随机种子；<c>0</c> 表示按时间随机。</param>
    public GameHost(WebPackage package, string? saveRoot = null, ulong seed = 0)
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
            _saves = new SaveManager(_engine, storage, $"{package.Id}.json");
            if (_saves.HasSave() && _saves.Load()) _offlineOnLoad = _saves.LastOfflineProgress;
        }
        else
        {
            _saves = null!;
        }

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

            _saves.Save();
            return new CommandOutcome(true, "已存档。", Seq);
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

    /// <summary>停止推进并存档。给 <c>ApplicationStopping</c> 用。</summary>
    public async Task StopAsync()
    {
        _stopping.Cancel();

        // 存档必须在游戏线程上做（它要读引擎状态），所以先排一条工作再停线程
        try
        {
            await ExecuteAsync(_ =>
            {
                // 调试跳层过的会话不落盘：否则"退出时的那一次存档"会把调试状态写进玩家的真实存档。
                // 这一步是兜底——正常退出时 AutosaveEnabled 早已被跳层关掉了（见 JumpToEraAsync）。
                if (!_debugMode) _saves?.Save();
                return new CommandOutcome(true, _debugMode ? "调试模式：退出时不写存档。" : "已存档。", Seq);
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 关停路径上不再抛：存不上也不能让进程退不出去
        }

        _work.Writer.TryComplete();
        _thread.Join(TimeSpan.FromSeconds(2));

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
