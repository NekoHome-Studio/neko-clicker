using NekoClicker.Core.Content;

namespace NekoClicker.Core.Events;

/// <summary>一次逻辑推进完成（每个 <c>Update</c> 派发一次，不是每个固定步长）。</summary>
/// <param name="DeltaSeconds">本次推进的模拟秒数。</param>
/// <param name="TotalSeconds">累计模拟秒数。</param>
public sealed record TickEvent(double DeltaSeconds, double TotalSeconds) : IGameEvent;

/// <summary>手动点击了一次。</summary>
/// <param name="Gained">获得的货币。</param>
/// <param name="CookiesAfter">点击后的存量。</param>
public sealed record ClickedEvent(double Gained, double CookiesAfter) : IGameEvent;

/// <summary>购买了建筑。</summary>
public sealed record BuildingPurchasedEvent(string Id, int Amount, double UnitPrice, double TotalPrice, int OwnedAfter) : IGameEvent;

/// <summary>出售了建筑。</summary>
public sealed record BuildingSoldEvent(string Id, int Amount, double Refund, int OwnedAfter) : IGameEvent;

/// <summary>购买了升级。</summary>
public sealed record UpgradePurchasedEvent(string Id, string Name, double TotalPrice, UpgradeCurrency Currency, int OwnedAfter) : IGameEvent;

/// <summary>解锁了成就。</summary>
public sealed record AchievementUnlockedEvent(string Id, string Name, string Icon) : IGameEvent;

/// <summary>获得/刷新了增益。</summary>
public sealed record BuffAppliedEvent(string Id, string Name, double Seconds, int Stacks) : IGameEvent;

/// <summary>增益到期。</summary>
public sealed record BuffExpiredEvent(string Id) : IGameEvent;

/// <summary>金猫出现。</summary>
public sealed record GoldenCookieSpawnedEvent(string InstanceId, double X, double Y, double LifetimeSeconds) : IGameEvent;

/// <summary>金猫没被点就消失了。</summary>
public sealed record GoldenCookieExpiredEvent(string InstanceId) : IGameEvent;

/// <summary>点中金猫。</summary>
public sealed record GoldenCookieClickedEvent(string InstanceId, string OutcomeId, string OutcomeName, double CookiesGained, string? BuffId) : IGameEvent;

/// <summary>完成转生。</summary>
public sealed record AscendedEvent(int PreviousLevel, int NewLevel, double ChipsGained, int Ascensions) : IGameEvent;

/// <summary>产生了一条玩家可见消息。</summary>
/// <param name="Notification">消息内容。</param>
public sealed record NotificationEvent(GameNotification Notification) : IGameEvent;

/// <summary>读档完成。</summary>
/// <param name="OfflineSeconds">离线时长（秒）。</param>
/// <param name="OfflineCookies">离线补发的货币。</param>
public sealed record GameLoadedEvent(double OfflineSeconds, double OfflineCookies) : IGameEvent;

/// <summary>存档完成。</summary>
/// <param name="Key">存档键。</param>
public sealed record GameSavedEvent(string Key) : IGameEvent;

/// <summary>产量发生变化（购买、增益、成就导致的重算）。</summary>
public sealed record ProductionChangedEvent(double CookiesPerSecond, double ClickPower) : IGameEvent;

/// <summary>舍命进入下一纪元。</summary>
/// <param name="PreviousEra">离开的层号。</param>
/// <param name="NewEra">进入的层号。</param>
/// <param name="ChipsGained">本次结算到的情感能量（可能为 0）。</param>
/// <param name="BuildingsInherited">跨层保留下来的建筑总数。</param>
/// <param name="NewEraName">新层的显示名。</param>
public sealed record EraAdvancedEvent(
    int PreviousEra,
    int NewEra,
    double ChipsGained,
    int BuildingsInherited,
    string NewEraName) : IGameEvent;

/// <summary>释放了一条叙事条目。</summary>
/// <param name="Id">条目 id。</param>
/// <param name="Title">标题。</param>
/// <param name="Icon">图标。</param>
/// <param name="StorylineId">所属剧情线。</param>
/// <param name="Channel">投放通道（UI 据此决定是提示、弹窗还是静默进图鉴）。</param>
public sealed record LoreRevealedEvent(
    string Id,
    string Title,
    string Icon,
    string StorylineId,
    Content.LoreChannel Channel) : IGameEvent;
