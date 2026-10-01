using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using NekoClicker.Core;
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

    private readonly Lock _viewGate = new();
    private string _viewJson = "{}";
    private JsonObject _viewObject = new();
    private long _seq;
    private PurchaseMode _mode = PurchaseMode.Buy1;
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
    public async Task<CommandOutcome> ExecuteAsync(Func<GameEngine, CommandOutcome> action, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<CommandOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_work.Writer.TryWrite(() =>
            {
                try
                {
                    completion.TrySetResult(action(_engine));
                }
                catch (Exception ex)
                {
                    completion.TrySetResult(new CommandOutcome(false, $"内部错误：{ex.Message}", Seq));
                }
                return Task.CompletedTask;
            }))
        {
            return new CommandOutcome(false, "游戏正在关闭。", Seq);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CommandOutcome(false, "游戏线程没有响应。", Seq);
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
            _saves.Save();
            return new CommandOutcome(true, "已存档。", Seq);
        }).ConfigureAwait(false);

    /// <summary>停止推进并存档。给 <c>ApplicationStopping</c> 用。</summary>
    public async Task StopAsync()
    {
        _stopping.Cancel();

        // 存档必须在游戏线程上做（它要读引擎状态），所以先排一条工作再停线程
        try
        {
            await ExecuteAsync(_ =>
            {
                _saves?.Save();
                return new CommandOutcome(true, "已存档。", Seq);
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
