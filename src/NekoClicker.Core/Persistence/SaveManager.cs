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
            _engine.Notify($"存档失败：{ex.Message}", NotificationKind.Warning, "⚠");
            return false;
        }
    }

    /// <summary>读档（含离线收益结算）。返回是否成功。</summary>
    public bool Load()
    {
        string? json = _storage.Read(Slot);
        if (json is null)
        {
            _engine.Notify("没有找到存档。", NotificationKind.Warning, "⚠");
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
            _engine.Notify($"读档失败：{ex.Message}", NotificationKind.Warning, "⚠");
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
