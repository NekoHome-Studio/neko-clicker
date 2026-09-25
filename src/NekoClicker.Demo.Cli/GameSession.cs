using NekoClicker.Core;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Persistence;
using NekoClicker.Core.Views;

namespace NekoClicker.Demo.Cli;

/// <summary>当前获得键盘焦点的面板。</summary>
internal enum PanelFocus
{
    /// <summary>建筑面板。</summary>
    Buildings,

    /// <summary>升级面板。</summary>
    Upgrades,

    /// <summary>成就面板。</summary>
    Achievements,

    /// <summary>图鉴（叙事条目）面板；内容包没有叙事时该面板为空。</summary>
    Codex,
}

/// <summary>
/// 会话：把引擎、存档和"界面状态"绑在一起。<para>
/// 所有玩家输入最终都变成对 <see cref="GameEngine"/> 的一次调用，会话自己<b>不做任何规则判断</b>——
/// 买不起、没解锁、已买满这些都由引擎返回结果，会话只负责把消息显示出来。
/// 这正是框架分层的价值：换成 Web 前端时，这一层的逻辑可以整段复用。
/// </para>
/// </summary>
internal sealed class GameSession : IDisposable
{
    private const int MaxLogLines = 200;

    private static readonly string[] BuildingKeys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    private readonly List<GameNotification> _log = [];
    private readonly IDisposable _notificationSubscription;
    private GameSnapshot _snapshot = null!;
    private BuildingView[] _buildingCache = [];
    private UpgradeView[] _upgradeCache = [];
    private AchievementView[] _achievementCache = [];
    private LoreView[] _codexCache = [];

    /// <summary>创建会话。</summary>
    /// <param name="package">要玩的内容包（决定构建哪份 <c>GameContent</c>）。</param>
    /// <param name="savePath">存档路径；<c>null</c> 表示不落盘。</param>
    /// <param name="seed">随机种子。</param>
    public GameSession(ContentPackage package, string? savePath, ulong seed)
    {
        Package = package;

        Engine = new GameEngine(package.Build(), new GameEngineOptions
        {
            Clock = SystemClock.Instance,
            Seed = seed,
            GrantOfflineProgress = true,
            MaxNotifications = 64,
        });

        _notificationSubscription = Engine.Events.Subscribe<NotificationEvent>(OnNotification);

        if (savePath is not null)
        {
            Storage = new FileStorage(Path.GetDirectoryName(Path.GetFullPath(savePath)) ?? ".");
            Saves = new SaveManager(Engine, Storage, Path.GetFileName(savePath));
        }

        if (Saves is not null && Saves.HasSave() && Saves.Load())
        {
            Log($"已读取存档：{savePath}", "📂");
            if (Saves.LastOfflineProgress is { CookiesGained: > 0 } offline)
                Log($"离线 {NumFormat.Duration(offline.CreditedSeconds)}，店里替你赚了 {NumFormat.FormatLong(offline.CookiesGained)} 条{Engine.Content.CurrencyName}。", "🌙");
        }
        else
        {
            Log(package.Welcome, "🐱");
        }

        RefreshCache();
    }

    /// <summary>当前内容包。</summary>
    public ContentPackage Package { get; }

    /// <summary>引擎。</summary>
    public GameEngine Engine { get; }

    /// <summary>存储介质（未启用存档时为 <c>null</c>）。</summary>
    public IStorage? Storage { get; }

    /// <summary>存档管理器（未启用存档时为 <c>null</c>）。</summary>
    public SaveManager? Saves { get; }

    /// <summary>当前批量档位。</summary>
    public PurchaseMode Mode { get; private set; } = PurchaseMode.Buy1;

    /// <summary>当前焦点面板。</summary>
    public PanelFocus Focus { get; private set; } = PanelFocus.Buildings;

    /// <summary>当前选中行。</summary>
    public int Selected { get; private set; }

    /// <summary>待确认的转生 / 店休。</summary>
    public bool AwaitingAscendConfirm { get; private set; }

    /// <summary>是否显示帮助浮层。</summary>
    public bool ShowHelp { get; private set; }

    /// <summary>已退出标记。</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>最近一帧的快照（渲染与选择都基于它，避免每帧重复生成）。</summary>
    public GameSnapshot Snapshot => _snapshot;

    /// <summary>最近的消息（新的在后）。</summary>
    public IReadOnlyList<GameNotification> LogLines => _log;

    /// <summary>推进一帧。</summary>
    public void Update(double deltaSeconds)
    {
        Engine.Update(deltaSeconds);
        RefreshCache();
    }

    /// <summary>
    /// 重新生成快照与列表缓存。<para>
    /// 直接操作引擎（例如无头模拟）之后必须调用它，否则界面读到的还是上一帧的缓存。
    /// </para>
    /// </summary>
    public void Refresh() => RefreshCache();

    // ---------------------------------------------------------------- 输入

    /// <summary>切换焦点面板。</summary>
    public void CycleFocus()
    {
        Focus = Focus switch
        {
            PanelFocus.Buildings => PanelFocus.Upgrades,
            PanelFocus.Upgrades => PanelFocus.Achievements,
            PanelFocus.Achievements => PanelFocus.Codex,
            _ => PanelFocus.Buildings,
        };
        Selected = 0;
        RefreshCache();
    }

    /// <summary>直接指定焦点面板（用于 <c>--frame --panel codex</c> 这类验证场景）。</summary>
    /// <param name="focus">目标面板。</param>
    public void SetFocus(PanelFocus focus)
    {
        Focus = focus;
        Selected = 0;
        RefreshCache();
    }

    /// <summary>移动选择。</summary>
    public void MoveSelection(int delta)
    {
        int count = RowCount;
        if (count == 0)
        {
            Selected = 0;
            return;
        }
        Selected = ((Selected + delta) % count + count) % count;
    }

    /// <summary>按序号直接选中（<c>-1</c> 表示无效）。</summary>
    public void SelectByKey(char key)
    {
        int index = Array.IndexOf(BuildingKeys, key.ToString());
        if (index < 0 || index >= RowCount) return;
        Selected = index;
    }

    /// <summary>手动点击（撸猫 / 做咖啡）。</summary>
    public void Click()
    {
        ClickResult result = Engine.Click();
        // 点击很频繁，不写日志；数字本身在顶栏跳动就够了。
        _ = result;
    }

    /// <summary>抓住第一只金猫。</summary>
    public void GrabGoldenCookie()
    {
        if (Engine.State.GoldenCookies.Count == 0)
        {
            double min = Engine.Content.Balance.GoldenCookieMinDelay;
            double max = Engine.Content.Balance.GoldenCookieMaxDelay;
            Log(
                $"现在没有{Package.GoldenCookieName}。它们每 {NumFormat.Duration(min)}~{NumFormat.Duration(max)} 出现一次，出现时顶部会有提示。",
                "🌟");
            return;
        }

        string id = Engine.State.GoldenCookies[0].InstanceId;
        GoldenCookieResult result = Engine.ClickGoldenCookie(id);
        Log(result.Message, result.Success ? result.Icon : "⚠");
    }

    /// <summary>切换批量档位。</summary>
    public void CycleMode()
    {
        if (Mode.IsSell())
        {
            Mode = Mode switch
            {
                PurchaseMode.Sell1 => PurchaseMode.Sell10,
                PurchaseMode.Sell10 => PurchaseMode.SellMax,
                _ => PurchaseMode.Sell1,
            };
        }
        else
        {
            Mode = Mode.NextBuy();
        }
        RefreshCache();
    }

    /// <summary>在买/卖之间切换。</summary>
    public void ToggleSell()
    {
        Mode = Mode.ToggleSell();
        RefreshCache();
    }

    /// <summary>执行当前选中项。</summary>
    public void Activate()
    {
        if (AwaitingAscendConfirm)
        {
            Ascend();
            return;
        }

        switch (Focus)
        {
            case PanelFocus.Buildings:
                ActivateBuilding(Selected);
                break;
            case PanelFocus.Upgrades:
                ActivateUpgrade(Selected);
                break;
            case PanelFocus.Codex:
                ActivateLore(Selected);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 在图鉴里"读"一条：如果是待处理的弹窗就点掉它，已解锁的就在日志里重读一遍。
    /// </summary>
    /// <param name="index">条目下标。</param>
    public void ActivateLore(int index)
    {
        if (index < 0 || index >= _codexCache.Length) return;

        LoreView entry = _codexCache[index];

        if (Engine.DismissLorePopup(entry.Id))
        {
            Log($"「{entry.Title}」", entry.Icon);
            RefreshCache();
            return;
        }

        if (entry.Unlocked) Log($"「{entry.Title}」{entry.Body}", entry.Icon);
        else Log($"还没读到这一段。条件：{entry.RevealHint}", "🔒");
    }

    /// <summary>执行指定序号的建筑（供数字快捷键使用）。</summary>
    /// <param name="index">建筑列表下标。</param>
    public void ActivateBuilding(int index)
    {
        if (index < 0 || index >= _buildingCache.Length) return;

        BuildingView view = _buildingCache[index];
        bool selling = Mode.IsSell();
        int amount = Mode.RequestedAmount();

        PurchaseResult result = selling
            ? Engine.SellBuilding(view.Id, amount)
            : Engine.BuyBuilding(view.Id, amount);

        Log(result.Message, result.Success ? (selling ? "💸" : "🛒") : "⚠");
        RefreshCache();
    }

    /// <summary>执行指定序号的升级。</summary>
    /// <param name="index">升级列表下标。</param>
    public void ActivateUpgrade(int index)
    {
        if (index < 0 || index >= _upgradeCache.Length) return;

        UpgradeView view = _upgradeCache[index];
        PurchaseResult result = Engine.BuyUpgrade(view.Id);
        Log(result.Message, result.Success ? "⬆" : "⚠");
        RefreshCache();
    }

    /// <summary>请求转生（首次调用进入确认态）。</summary>
    public void RequestAscend()
    {
        if (AwaitingAscendConfirm)
        {
            Ascend();
            return;
        }

        PrestigePreview preview = PrestigeSystem.Preview(Engine);
        if (!preview.CanAscend)
        {
            Log(
                $"还不能{Package.PrestigeActionName}：历史累计 {NumFormat.FormatLong(Engine.State.CookiesEarnedAllTime)} / " +
                $"{NumFormat.FormatLong(preview.CookiesForNextLevel)}。",
                "⚠");
            return;
        }

        AwaitingAscendConfirm = true;
        Log(
            $"确认{Package.PrestigeActionName}？将清空本轮进度，换取 {NumFormat.FormatLong(preview.ChipsOnAscend)} {Engine.Content.PrestigeCurrencyName}。按 Y 确认，其他键取消。",
            "🌿");
    }

    /// <summary>执行转生。</summary>
    public void Ascend()
    {
        AwaitingAscendConfirm = false;
        AscensionResult result = Engine.Ascend();
        Log(result.Message, result.Success ? "🌿" : "⚠");
        RefreshCache();
    }

    /// <summary>取消待确认状态。</summary>
    public void CancelPending() => AwaitingAscendConfirm = false;

    /// <summary>切换帮助浮层。</summary>
    public void ToggleHelp() => ShowHelp = !ShowHelp;

    /// <summary>手动存档。</summary>
    public void Save()
    {
        if (Saves is null)
        {
            Log("本次运行未启用存档（--no-save）。", "💾");
            return;
        }

        Log(Saves.Save() ? "已存档。" : $"存档失败：{Saves.LastError?.Message}", "💾");
    }

    /// <summary>请求退出。</summary>
    public void Quit()
    {
        if (Saves is not null) Saves.Save();
        QuitRequested = true;
    }

    // ---------------------------------------------------------------- 视图数据

    /// <summary>可见建筑（已解锁，或未解锁但不隐藏）。</summary>
    public IReadOnlyList<BuildingView> Buildings => _buildingCache;

    /// <summary>可购买升级（已解锁且未买满，按价格升序）。</summary>
    public IReadOnlyList<UpgradeView> Upgrades => _upgradeCache;

    /// <summary>可见成就。</summary>
    public IReadOnlyList<AchievementView> Achievements => _achievementCache;

    /// <summary>图鉴条目（按剧情线与序号排好）。</summary>
    public IReadOnlyList<LoreView> Codex => _codexCache;

    /// <summary>当前焦点面板的行数。</summary>
    public int RowCount => Focus switch
    {
        PanelFocus.Buildings => _buildingCache.Length,
        PanelFocus.Upgrades => _upgradeCache.Length,
        PanelFocus.Codex => _codexCache.Length,
        _ => _achievementCache.Length,
    };

    /// <summary>把引擎通知与会话日志写进统一的时间线。</summary>
    public void Log(string message, string icon = "·")
    {
        _log.Add(new GameNotification(message, icon, NotificationKind.Info, Engine.State.PlayTimeSeconds));
        while (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _notificationSubscription.Dispose();
        Saves?.Dispose();
    }

    private void OnNotification(NotificationEvent evt)
    {
        _log.Add(evt.Notification);
        while (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }

    private void RefreshCache()
    {
        _snapshot = Engine.Snapshot(Mode);

        _buildingCache = [.. _snapshot.Buildings.Where(b => b.IsVisible)];
        _upgradeCache = [.. _snapshot.Upgrades.Where(u => u.IsAvailable).OrderBy(u => u.Price)];
        _achievementCache = [.. _snapshot.Achievements.Where(a => a.Unlocked || !a.Hidden)];
        _codexCache = _snapshot.Codex is { } codex
            ? [.. codex.Storylines.SelectMany(s => s.Entries)]
            : [];

        int count = RowCount;
        if (Selected >= count) Selected = Math.Max(0, count - 1);
    }
}
