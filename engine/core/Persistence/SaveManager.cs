using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core.Persistence;

/// <summary>
/// 自动存档与存档槽管理。<para>
/// 它通过订阅引擎的 <see cref="TickEvent"/> 来计时，因此引擎完全不需要知道"存档"这件事——
/// 想关掉自动存档，不创建 <see cref="SaveManager"/> 即可。存储介质由 <see cref="IStorage"/>
/// 决定：文件、内存、将来的浏览器 localStorage 都行。
/// </para>
/// </summary>
public sealed class SaveManager : IDisposable
{
    private readonly GameEngine _engine;
    private readonly IStorage _storage;
    private readonly IDisposable _tickSubscription;
    private double _secondsSinceSave;
    private bool _disposed;

    /// <summary>创建存档管理器并开始自动存档计时。</summary>
    /// <param name="engine">目标引擎。</param>
    /// <param name="storage">存储介质。</param>
    /// <param name="slot">存档键/文件名。</param>
    /// <param name="autoSaveIntervalSeconds">自动存档间隔；<c>null</c> 表示用 <see cref="GameBalance.AutoSaveInterval"/>。</param>
    public SaveManager(
        GameEngine engine,
        IStorage storage,
        string slot = "neko-save.json",
        double? autoSaveIntervalSeconds = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Slot = slot;
        AutoSaveInterval = autoSaveIntervalSeconds ?? engine.Balance.AutoSaveInterval;
        _tickSubscription = engine.Events.Subscribe<TickEvent>(OnTick);
    }

    /// <summary>存档键。</summary>
    public string Slot { get; }

    /// <summary>
    /// 这份存档属于哪个内容包（导入时用来拒绝别的包的存档）。<para>
    /// 由宿主设置：Web 是 <c>package.Id</c>，终端是 <c>options.Package.Id</c>。
    /// <b>为什么需要它</b>：十一个内容包写出来的存档形状<b>逐字段同构</b>，
    /// 内容里没有一处写着"我是哪个包的"（包只体现在文件名
    /// <c>saves/&lt;包 id&gt;.json</c> 上）。于是"把咖啡馆的存档导进末世包"会一声不响地成功，
    /// 而未知 id 又是<b>刻意保留</b>的合法状态（见 <c>ARCHITECTURE.md</c> 的「存档与迁移」），
    /// 没有别的地方拦得住。
    /// </para>
    /// <para>
    /// 为 <c>null</c> 时不做该项检查，<see cref="Import"/> 的结果消息里会明说
    /// "没有包标识、未做跨包检查"——不让这件事静悄悄过去。
    /// </para>
    /// <para>
    /// <b>不许从 <see cref="Slot"/> 反推</b>：终端的 <c>--save</c> 可以是任意路径，
    /// 从文件名猜身份在不改默认路径时"看起来总是对的"，改一次就错。
    /// </para>
    /// </summary>
    public string? PackId { get; set; }

    /// <summary>自动存档间隔（秒）；&lt;= 0 表示关闭。</summary>
    public double AutoSaveInterval { get; }

    /// <summary>是否启用自动存档。</summary>
    public bool AutoSaveEnabled { get; set; } = true;

    /// <summary>距离上次存档经过的秒数。</summary>
    public double SecondsSinceSave => _secondsSinceSave;

    /// <summary>上次成功存档的时刻。</summary>
    public DateTimeOffset? LastSaveAt { get; private set; }

    /// <summary>上次读档结算出的离线收益。</summary>
    public OfflineProgress? LastOfflineProgress { get; private set; }

    /// <summary>最近一次失败原因。</summary>
    public Exception? LastError { get; private set; }

    /// <summary>是否存在存档。</summary>
    public bool HasSave() => _storage.Exists(Slot);

    /// <summary>立刻存档。</summary>
    public bool Save()
    {
        try
        {
            _storage.Write(Slot, _engine.Save());
            _secondsSinceSave = 0;
            LastSaveAt = _engine.Clock.UtcNow;
            LastError = null;
            _engine.Events.Publish(new GameSavedEvent(Slot));
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex;
            _engine.Notify($"存档失败：{ex.Message}", NotificationKind.Warning, "!");
            return false;
        }
    }

    /// <summary>读档（含离线收益结算）。返回是否成功。</summary>
    public bool Load()
    {
        string? json = _storage.Read(Slot);
        if (json is null)
        {
            _engine.Notify("没有找到存档。", NotificationKind.Warning, "!");
            return false;
        }

        try
        {
            LastOfflineProgress = _engine.Load(json);
            _secondsSinceSave = 0;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            // 存档损坏时保留玩家当前进度，不要因为读档失败而把游戏搞崩。
            LastError = ex;
            _engine.Notify($"读档失败：{ex.Message}", NotificationKind.Warning, "!");
            return false;
        }
    }

    /// <summary>删除存档。</summary>
    public void Delete()
    {
        _storage.Delete(Slot);
        LastOfflineProgress = null;
    }

    /// <summary>读取存档原始文本（用于备份/分享）。</summary>
    public string? ReadRaw() => _storage.Read(Slot);

    /// <summary>写入存档原始文本（用于导入）。</summary>
    public bool WriteRaw(string json)
    {
        try
        {
            _storage.Write(Slot, json);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex;
            return false;
        }
    }

    /// <summary>导出 base64 分享码。</summary>
    public string ExportShareCode() => SaveSerializer.SerializeToShareCode(_engine);

    /// <summary>
    /// 导出成一份可复制粘贴的存档文本（信封格式，见 <see cref="SaveTransfer"/>）。<para>
    /// <b>它不写盘</b>：导出是一次读。序列化走的是与自动存档同一条路
    /// （<see cref="GameEngine.Save"/>），所以对状态的唯一影响也与一次自动存档相同——
    /// 把 PRNG 状态同步进 <see cref="GameState"/>，并刷新 <c>LastSavedAt</c>。
    /// 这是必须的：不同步 PRNG，"导出的存档"与"存档文件里那份"就不是同一个东西了。
    /// </para>
    /// <para>
    /// 界面上的导出用这个，不要用 <see cref="ExportShareCode"/>：分享码是裸存档的 base64，
    /// 没有信封、没有包标识、没有校验和，导入端无从判断它属于谁，也无从发现它被截断了。
    /// </para>
    /// </summary>
    /// <returns>信封文本（外壳缩进，存档本体紧凑）。</returns>
    public string Export()
        => SaveTransfer.Wrap(_engine.Save(), PackId, ApiVersion.Current, _engine.Clock.UtcNow);

    /// <summary>
    /// 导入一份 <see cref="Export"/> 出来的文本。<para>
    /// <b>顺序是刻意的，也是这个方法的全部要点</b>：
    /// </para>
    /// <list type="number">
    ///   <item><b>尺寸</b>（1 MiB）→ <b>信封</b>（格式标签 / 格式版本 / 存档版本 / 校验和）；
    ///         这几道闸<b>一个字节都不碰磁盘</b>；</item>
    ///   <item><b>归属</b>：信封声明的包 id 与 <see cref="PackId"/> 不符就拒绝；</item>
    ///   <item><b>结构</b>：<see cref="SaveSerializer.Parse"/>（含版本迁移）；</item>
    ///   <item><b>一致性</b>：数字必须有限；未知 id <b>只报告不拦</b>（它们是合法状态）；</item>
    ///   <item><b>落盘</b>：<see cref="IStorage.Write"/>——它自己就是"先证明读得回来、再原子替换、
    ///         并把上一份留成 <c>.bak</c>"（见 <see cref="FileStorage.Write"/>）；</item>
    ///   <item><b>应用</b>：落盘<b>成功之后</b>才换掉当前会话的状态。</item>
    /// </list>
    /// <para>
    /// <b>为什么先落盘再应用</b>：存档文件是耐久的、引擎状态是易失的。两件事都可能出错，
    /// 而顺序决定了"崩在中间"剩下什么——先落盘 ⇒ 磁盘上是新的，重启即导入生效；
    /// 反过来 ⇒ 磁盘上还是旧的，玩家看到的是一次"什么都没发生"的导入。
    /// 先让耐久的那份正确。
    /// </para>
    /// <para>
    /// <b>不结算离线收益</b>：导入是"把这局恢复成那个样子"，不是"接上那段时间"。
    /// 若按导入存档的 <c>LastSavedAt</c> 补发产量，导入一份三天前的存档会凭空发三天收益。
    /// 想接上的话那是 <see cref="SaveSerializer.DeserializeInto"/> 那条路，一行之差。
    /// </para>
    /// <para>
    /// <b>诚实边界</b>：落盘成功、应用失败时（今天不可达——<see cref="SaveSerializer.Apply"/>
    /// 只做内存搬运、不碰 I/O），磁盘上是新存档而会话里是旧状态，本方法以
    /// <see cref="SaveTransferKind.ApplyFailed"/> 如实报告，不吞。另外包 id 是<b>声明</b>不是证明：
    /// 手改信封能把它改成任意值，导入无法证明"这份内容真是这个包的"——因为未知 id
    /// <b>是</b>合法状态，不能拿"每个 id 都存在"当判据。
    /// </para>
    /// <para>
    /// 线程约定与 <see cref="Load"/> 相同：在拥有引擎的那条线程上调用
    /// （两个宿主都是在游戏线程 / 主循环上存档的）。
    /// </para>
    /// </summary>
    /// <param name="text">粘贴进来的导出文本。</param>
    /// <returns>结果；<b>坏输入不抛异常</b>，而是带着精确原因返回（见 <see cref="SaveTransferResult"/>）。</returns>
    public SaveTransferResult Import(string text)
    {
        // ①②③④ 尺寸 / 信封 / 版本 / 校验和：全都在解析与落盘之前。
        if (!SaveTransfer.TryUnwrap(text, out SaveTransfer.Envelope? envelope, out SaveTransferResult? failure))
            return failure!;

        SaveTransfer.Envelope from = envelope!;

        // ⑤ 归属闸：声明不符就拒绝。这条闸能成立，全靠导出时把包 id 写进了信封。
        if (PackId is not null && from.PackId is not null
            && !string.Equals(PackId, from.PackId, StringComparison.OrdinalIgnoreCase))
        {
            return new SaveTransferResult(
                false,
                SaveTransferKind.ForeignPack,
                $"这份存档是内容包「{from.PackId}」的，当前会话是「{PackId}」——"
                + "两个包的建筑 / 升级 id 不一样，灌进来只会得到一份这个包答不上的存档，所以拒绝导入。"
                + "磁盘上原来的存档与备份都没有被动过。",
                from.PackId,
                from.SaveVersion,
                0,
                []);
        }

        // ⑥ 结构闸：存档本体读不回来就拒绝（含"合法 JSON 但结构与 SaveData 对不上"）。
        SaveData data;
        try
        {
            data = SaveSerializer.Parse(from.Save);
        }
        catch (InvalidDataException ex)
        {
            // 只接"坏存档"这一类。别的异常原样漏出去——那更可能是代码 bug，
            // 把它说成"存档坏了"正是本仓库最想避免的那种错误归因（与 FileStorage.VerifyLoadable 同一条取舍）。
            return new SaveTransferResult(
                false,
                SaveTransferKind.CorruptSave,
                $"存档读不回来：{ex.Message}",
                from.PackId,
                from.SaveVersion,
                0,
                []);
        }

        // ⑦ 一致性闸：NaN / 无穷是"解析成功但玩不了"，既有任何一道闸都拦不住它。
        string? nonFinite = SaveTransfer.FindNonFinite(data);
        if (nonFinite is not null)
        {
            return new SaveTransferResult(
                false,
                SaveTransferKind.NonFiniteNumbers,
                $"存档里的 {nonFinite} 不是有限数字（NaN / 无穷），这份存档玩不下去"
                + "（之后每一次结算都会变成 NaN）。磁盘上原来的存档与备份都没有被动过。",
                from.PackId,
                from.SaveVersion,
                0,
                []);
        }

        SaveTransfer.ProbeUnknownIds(data, _engine.Content, out int unknownCount, out IReadOnlyList<string> unknownSample);

        // ⑧ 落盘：到这里为止一个字节都还没写过。FileStorage.Write 自己会"先反解一遍证明读得回来，
        //    再原子替换，并把被换下来的那份留成 .bak"——失败时它保证旧存档与备份一个字节不动。
        try
        {
            _storage.Write(Slot, from.Save);
        }
        catch (Exception ex)
        {
            LastError = ex;
            return new SaveTransferResult(
                false,
                SaveTransferKind.WriteFailed,
                $"导入没有写进存档：{ex.Message}。磁盘上原来的存档与备份都还在，当前会话也没变。",
                from.PackId,
                from.SaveVersion,
                unknownCount,
                unknownSample);
        }

        // ⑨ 应用：只有落盘成功了才换掉当前会话。
        try
        {
            SaveSerializer.Apply(_engine, data);
        }
        catch (Exception ex)
        {
            LastError = ex;
            return new SaveTransferResult(
                false,
                SaveTransferKind.ApplyFailed,
                $"存档已经写入 {Slot}，但应用到当前会话时出错了：{ex.Message}。"
                + "当前会话还是导入前的状态——重启游戏就会读到刚写进去的那一份（导入算成功了一半）。"
                + "请把这条消息报出来。",
                from.PackId,
                from.SaveVersion,
                unknownCount,
                unknownSample);
        }

        _secondsSinceSave = 0;
        LastSaveAt = _engine.Clock.UtcNow;
        LastError = null;
        _engine.Events.Publish(new GameSavedEvent(Slot));

        return new SaveTransferResult(
            true,
            SaveTransferKind.Ok,
            DescribeImport(from, unknownCount, unknownSample),
            from.PackId,
            from.SaveVersion,
            unknownCount,
            unknownSample);
    }

    /// <summary>拼导入成功那句话：先自证身份（包 / 版本 / 导出时刻），再把"答不上"的东西报出来。</summary>
    private string DescribeImport(
        SaveTransfer.Envelope envelope,
        int unknownCount,
        IReadOnlyList<string> unknownSample)
    {
        var parts = new List<string>
        {
            $"已导入：内容包「{envelope.PackId ?? "未声明"}」｜存档格式 {envelope.SaveVersion}"
            + $"｜框架 {envelope.FrameworkVersion ?? "未记录"}",
        };

        if (envelope.ExportedAt is not null) parts.Add($"导出于 {envelope.ExportedAt}");
        parts.Add($"当前会话与 {Slot} 都已换成这一份");

        // "没有包标识"与"有包标识"是两件事，必须说出来：前者意味着这次导入**没有**跨包检查。
        parts.Add(PackId is null
            ? "本会话没有包标识，所以这次没有做跨包检查"
            : envelope.PackId is null
                ? "这份输入没有包标识，所以没有做跨包检查"
                : $"已核对包标识（{PackId}）");

        parts.Add(unknownCount == 0
            ? "存档里的 id 这个包全都认识"
            : $"其中 {unknownCount} 个 id 这个包不认识（{string.Join("、", unknownSample)}"
              + (unknownCount > unknownSample.Count ? " 等" : string.Empty)
              + "）——它们被原样保留，不影响本包");

        return string.Join("｜", parts) + "。";
    }

    /// <summary>导出的分享码字符串长度概览，便于 UI 提示。</summary>
    public string DescribeStorage()
        => $"槽位 {Slot}｜间隔 {(AutoSaveInterval <= 0 ? "关闭" : NumFormat.Duration(AutoSaveInterval))}" +
           $"｜状态 {(HasSave() ? "已有存档" : "空")}";

    /// <summary>手动推进存档计时（不使用 TickEvent 的宿主可自行调用）。</summary>
    public void Tick(double deltaSeconds)
    {
        if (!AutoSaveEnabled || AutoSaveInterval <= 0) return;

        _secondsSinceSave += deltaSeconds;
        if (_secondsSinceSave < AutoSaveInterval) return;
        Save();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tickSubscription.Dispose();
    }

    private void OnTick(TickEvent evt) => Tick(evt.DeltaSeconds);
}
