using NekoClicker.Core.Content;

namespace NekoClicker.Core.Views;

/// <summary>建筑在 UI 中的一行。</summary>
public sealed record BuildingView
{
    /// <summary>建筑 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>说明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>分组。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>持有数量。</summary>
    public int Owned { get; init; }

    /// <summary>是否已解锁。</summary>
    public bool IsUnlocked { get; init; }

    /// <summary>未解锁时是否应该隐藏（否则显示为灰色）。</summary>
    public bool HiddenUntilUnlocked { get; init; }

    /// <summary>解锁条件描述。</summary>
    public string UnlockHint { get; init; } = string.Empty;

    /// <summary>解锁进度 [0,1]；无法量化时为 0。</summary>
    public double UnlockProgress { get; init; }

    /// <summary>当前单价。</summary>
    public double UnitPrice { get; init; }

    /// <summary>本次批量操作涉及的数量（买 N / 卖 N）。</summary>
    public int BatchAmount { get; init; }

    /// <summary>本次批量操作的总价（出售时为返还额）。</summary>
    public double BatchPrice { get; init; }

    /// <summary>是否负担得起（出售模式下表示是否有货可卖）。</summary>
    public bool CanAfford { get; init; }

    /// <summary>单个建筑当前的实际产量。</summary>
    public double CpsEach { get; init; }

    /// <summary>该建筑当前贡献的总产量。</summary>
    public double CpsContribution { get; init; }

    /// <summary>占总产量的比例 [0,1]。</summary>
    public double CpsShare { get; init; }

    /// <summary>下一个由该建筑数量触发的里程碑数量；无则为 <c>null</c>。</summary>
    public int? NextMilestoneAt { get; init; }

    /// <summary>里程碑对应的升级名。</summary>
    public string? NextMilestoneName { get; init; }

    /// <summary>出售返还比例。</summary>
    public double SellRefundRate { get; init; }

    /// <summary>UI 是否应该显示这一行。</summary>
    public bool IsVisible => IsUnlocked || !HiddenUntilUnlocked;
}

/// <summary>升级在 UI 中的一行。</summary>
public sealed record UpgradeView
{
    /// <summary>升级 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>说明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>当前单价。</summary>
    public double Price { get; init; }

    /// <summary>计价货币。</summary>
    public UpgradeCurrency Currency { get; init; }

    /// <summary>已购次数。</summary>
    public int Owned { get; init; }

    /// <summary>可购次数上限。</summary>
    public int MaxPurchases { get; init; }

    /// <summary>是否已解锁。</summary>
    public bool IsUnlocked { get; init; }

    /// <summary>未解锁时是否隐藏。</summary>
    public bool HiddenUntilUnlocked { get; init; }

    /// <summary>解锁条件描述。</summary>
    public string UnlockHint { get; init; } = string.Empty;

    /// <summary>解锁进度 [0,1]。</summary>
    public double UnlockProgress { get; init; }

    /// <summary>是否买得起。</summary>
    public bool CanAfford { get; init; }

    /// <summary>效果摘要。</summary>
    public string EffectSummary { get; init; } = string.Empty;

    /// <summary>分组。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>层级。</summary>
    public int Tier { get; init; }

    /// <summary>是否转生后保留。</summary>
    public bool IsPermanent { get; init; }

    /// <summary>是否已经买满。</summary>
    public bool IsMaxed => Owned >= MaxPurchases;

    /// <summary>当前是否值得在"可购买列表"里展示。</summary>
    public bool IsAvailable => IsUnlocked && !IsMaxed;

    /// <summary>UI 是否应该显示这一行。</summary>
    public bool IsVisible => IsUnlocked || !HiddenUntilUnlocked;
}

/// <summary>成就在 UI 中的一行。</summary>
public sealed record AchievementView
{
    /// <summary>成就 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名（未解锁且隐藏时为 ???）。</summary>
    public required string Name { get; init; }

    /// <summary>图标（未解锁且隐藏时为 🔒）。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>说明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否已解锁。</summary>
    public bool Unlocked { get; init; }

    /// <summary>是否为隐藏成就。</summary>
    public bool Hidden { get; init; }

    /// <summary>分组。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>进度 [0,1]；不可量化时为 0。</summary>
    public double Progress { get; init; }

    /// <summary>进度文本，例如 <c>12 / 50</c>；不可量化时为空。</summary>
    public string ProgressText { get; init; } = string.Empty;
}

/// <summary>增益在 UI 中的一行。</summary>
public sealed record BuffView
{
    /// <summary>增益 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>说明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>剩余秒数。</summary>
    public double RemainingSeconds { get; init; }

    /// <summary>总时长。</summary>
    public double TotalSeconds { get; init; }

    /// <summary>剩余比例 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>层数。</summary>
    public int Stacks { get; init; }

    /// <summary>是否负面效果。</summary>
    public bool IsDebuff { get; init; }
}

/// <summary>场上金猫的 UI 信息。</summary>
public sealed record GoldenCookieView
{
    /// <summary>实例 id。</summary>
    public required string InstanceId { get; init; }

    /// <summary>剩余停留秒数。</summary>
    public double RemainingSeconds { get; init; }

    /// <summary>总停留秒数。</summary>
    public double LifetimeSeconds { get; init; }

    /// <summary>剩余比例 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>归一化横坐标 [0,1]。</summary>
    public double X { get; init; }

    /// <summary>归一化纵坐标 [0,1]。</summary>
    public double Y { get; init; }
}

/// <summary>九层总览里的一行。</summary>
/// <param name="Index">层号。</param>
/// <param name="Name">显示名。</param>
/// <param name="Icon">图标。</param>
/// <param name="Theme">一句话主题。</param>
/// <param name="Completed">是否已经舍命离开过。</param>
/// <param name="Current">是否是当前所在层。</param>
public sealed record EraSummary(int Index, string Name, string Icon, string Theme, bool Completed, bool Current);

/// <summary>
/// 舍命面板的数据。<para>
/// UI 只需要读这个：<see cref="CanAdvance"/> 为真时按钮可用，否则置灰并把
/// <see cref="BlockedReason"/> 显示出来；<see cref="Progress"/> 直接驱动进度条。
/// 没有分层转生的内容包，<see cref="GameSnapshot.Era"/> 为 <c>null</c>，UI 自动隐藏该面板。
/// </para>
/// </summary>
public sealed record EraView
{
    /// <summary>当前层号。</summary>
    public int Index { get; init; }

    /// <summary>总层数。</summary>
    public int Total { get; init; }

    /// <summary>当前层的 id。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>当前层的显示名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>当前层的主题。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>当前层的图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>当前层的进入叙事。</summary>
    public string EntryText { get; init; } = string.Empty;

    /// <summary>舍命按钮是否可用。</summary>
    public bool CanAdvance { get; init; }

    /// <summary>不可用的原因（人类可读）；可用时为 <c>null</c>。</summary>
    public string? BlockedReason { get; init; }

    /// <summary>本层主线进度 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>本层主线进度的文本形式，例如 <c>1.2 million / 1 billion（1%）</c>。</summary>
    public string ProgressText { get; init; } = string.Empty;

    /// <summary>下一层的层号；已是最后一层时为 <c>null</c>。</summary>
    public int? NextIndex { get; init; }

    /// <summary>下一层的显示名。</summary>
    public string? NextName { get; init; }

    /// <summary>若现在舍命可得的情感能量（可能为 0）。</summary>
    public double ChipsOnAdvance { get; init; }

    /// <summary>当前层叠加的常驻规则摘要。</summary>
    public string ModifierSummary { get; init; } = string.Empty;

    /// <summary>是否是最后一层。</summary>
    public bool IsFinalEra { get; init; }

    /// <summary>全部层的总览。</summary>
    public IReadOnlyList<EraSummary> All { get; init; } = [];
}

/// <summary>
/// 一条叙事条目在图鉴里的样子。<para>
/// 未解锁时标题与正文都被隐藏（显示 ???），但<b>保留释放条件与进度</b>——
/// 于是图鉴同时是一张"还没读到什么"的清单，而不是一堵 ??? 墙。
/// </para>
/// </summary>
public sealed record LoreView
{
    /// <summary>条目 id。</summary>
    public required string Id { get; init; }

    /// <summary>标题（未解锁时为 ???）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>正文（未解锁时为空）。</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>图标（未解锁时为 🔒）。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>所属剧情线 id。</summary>
    public string StorylineId { get; init; } = string.Empty;

    /// <summary>所属剧情线名。</summary>
    public string StorylineName { get; init; } = string.Empty;

    /// <summary>线内序号。</summary>
    public int Order { get; init; }

    /// <summary>是否已释放。</summary>
    public bool Unlocked { get; init; }

    /// <summary>投放通道。</summary>
    public LoreChannel Channel { get; init; }

    /// <summary>释放条件描述。</summary>
    public string RevealHint { get; init; } = string.Empty;

    /// <summary>释放条件进度 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>进度文本，例如 <c>3 / 25</c>。</summary>
    public string ProgressText { get; init; } = string.Empty;
}

/// <summary>一条剧情线在图鉴里的样子。</summary>
public sealed record StorylineView
{
    /// <summary>剧情线 id。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>显示名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>主题。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>已解锁条数。</summary>
    public int Unlocked { get; init; }

    /// <summary>声明总条数。</summary>
    public int Total { get; init; }

    /// <summary>进度 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>本线的条目（按序号升序）。</summary>
    public IReadOnlyList<LoreView> Entries { get; init; } = [];
}

/// <summary>图鉴：全部剧情线与叙事的只读视图。</summary>
public sealed record CodexView
{
    /// <summary>已释放总条数。</summary>
    public int TotalUnlocked { get; init; }

    /// <summary>总条数。</summary>
    public int TotalEntries { get; init; }

    /// <summary>总进度 [0,1]。</summary>
    public double Progress { get; init; }

    /// <summary>全部剧情线。</summary>
    public IReadOnlyList<StorylineView> Storylines { get; init; } = [];
}

/// <summary>某个立场当前的权重。UI 用它画立场轴。</summary>
public sealed record StanceView
{
    /// <summary>立场 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "⚖️";

    /// <summary>一句话主题。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>主导这一立场时的代价描述。</summary>
    public string CostText { get; init; } = string.Empty;

    /// <summary>累计权重。</summary>
    public int Weight { get; init; }

    /// <summary>是否是当前的主导立场。</summary>
    public bool IsDominant { get; init; }

    /// <summary>占总权重的比例 [0,1]；总权重为 0 时为 0。</summary>
    public double Share { get; init; }
}

/// <summary>
/// 一次待作答的选择。<para>
/// 刻意把选项一次性给全（而不是逐个解锁）——玩家在同一个画面上比较后表态，
/// 而不是被逐个选项牵着走。
/// </para>
/// </summary>
public sealed record ChoiceView
{
    /// <summary>选择 id。</summary>
    public required string Id { get; init; }

    /// <summary>谁在说话。</summary>
    public required string Speaker { get; init; }

    /// <summary>问题 / 情境。</summary>
    public required string Prompt { get; init; }

    /// <summary>可选项。</summary>
    public IReadOnlyList<ChoiceOptionView> Options { get; init; } = [];
}

/// <summary>选择里的一个选项。</summary>
public sealed record ChoiceOptionView
{
    /// <summary>选项 id（作答时回传）。</summary>
    public required string Id { get; init; }

    /// <summary>按钮文字。</summary>
    public required string Label { get; init; }

    /// <summary>所属立场显示名；不偏向任何立场时为空。</summary>
    public string StanceName { get; init; } = string.Empty;

    /// <summary>所属立场图标。</summary>
    public string StanceIcon { get; init; } = string.Empty;

    /// <summary>选择后给该立场累加的权重。</summary>
    public int Weight { get; init; }

    /// <summary>选项效果的摘要（人类可读）。</summary>
    public string EffectSummary { get; init; } = string.Empty;
}

/// <summary>
/// 一帧 UI 所需的全部数据。<para>
/// 这是引擎对前端的完整契约：前端只读它、只发命令，不接触 <see cref="GameState"/>。
/// 由于是普通 record，前端可以直接做差异比较来决定重绘哪些行。
/// </para>
/// </summary>
public sealed record GameSnapshot
{
    /// <summary>游戏标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主货币名。</summary>
    public string CurrencyName { get; init; } = string.Empty;

    /// <summary>主货币图标。</summary>
    public string CurrencyIcon { get; init; } = string.Empty;

    /// <summary>点击动作名。</summary>
    public string ClickActionName { get; init; } = string.Empty;

    /// <summary>转生货币名。</summary>
    public string PrestigeCurrencyName { get; init; } = string.Empty;

    /// <summary>转生货币图标。</summary>
    public string PrestigeCurrencyIcon { get; init; } = string.Empty;

    /// <summary>当前存量。</summary>
    public double Cookies { get; init; }

    /// <summary>当前每秒产量。</summary>
    public double CookiesPerSecond { get; init; }

    /// <summary>单次点击收益。</summary>
    public double ClickPower { get; init; }

    /// <summary>存量格式化文本。</summary>
    public string CookiesText { get; init; } = string.Empty;

    /// <summary>产量格式化文本。</summary>
    public string CpsText { get; init; } = string.Empty;

    /// <summary>点击收益格式化文本。</summary>
    public string ClickPowerText { get; init; } = string.Empty;

    /// <summary>本轮累计赚取。</summary>
    public double CookiesEarnedThisRun { get; init; }

    /// <summary>历史累计赚取。</summary>
    public double CookiesEarnedAllTime { get; init; }

    /// <summary>手动点击累计赚取。</summary>
    public double HandMadeCookies { get; init; }

    /// <summary>累计点击次数。</summary>
    public double TotalClicks { get; init; }

    /// <summary>累计点中金猫次数。</summary>
    public double GoldenCookiesClicked { get; init; }

    /// <summary>转生等级。</summary>
    public int PrestigeLevel { get; init; }

    /// <summary>持有转生货币。</summary>
    public double PrestigeChips { get; init; }

    /// <summary>转生次数。</summary>
    public int Ascensions { get; init; }

    /// <summary>累计游玩秒数。</summary>
    public double PlayTimeSeconds { get; init; }

    /// <summary>已解锁成就数。</summary>
    public int AchievementCount { get; init; }

    /// <summary>成就总数。</summary>
    public int AchievementTotal { get; init; }

    /// <summary>建筑总数。</summary>
    public double TotalBuildings { get; init; }

    /// <summary>已购升级种类数。</summary>
    public int PurchasedUpgrades { get; init; }

    /// <summary>距离下一次金猫出现的秒数。</summary>
    public double GoldenCookieCountdown { get; init; }

    /// <summary>当前 UI 的购买模式。</summary>
    public PurchaseMode Mode { get; init; }

    /// <summary>建筑行。</summary>
    public IReadOnlyList<BuildingView> Buildings { get; init; } = [];

    /// <summary>升级行。</summary>
    public IReadOnlyList<UpgradeView> Upgrades { get; init; } = [];

    /// <summary>成就行。</summary>
    public IReadOnlyList<AchievementView> Achievements { get; init; } = [];

    /// <summary>生效中的增益。</summary>
    public IReadOnlyList<BuffView> Buffs { get; init; } = [];

    /// <summary>场上的金猫。</summary>
    public IReadOnlyList<GoldenCookieView> GoldenCookies { get; init; } = [];

    /// <summary>最近的通知。</summary>
    public IReadOnlyList<GameNotification> Notifications { get; init; } = [];

    /// <summary>转生预览。</summary>
    public PrestigePreview Prestige { get; init; }

    /// <summary>舍命面板；内容包没有分层转生时为 <c>null</c>（UI 应隐藏该面板）。</summary>
    public EraView? Era { get; init; }

    /// <summary>图鉴；内容包没有叙事条目时为 <c>null</c>。</summary>
    public CodexView? Codex { get; init; }

    /// <summary>待玩家点掉的叙事弹窗（按释放顺序）。</summary>
    public IReadOnlyList<LoreView> PendingLore { get; init; } = [];

    /// <summary>待玩家作答的选择（按触发顺序）。</summary>
    public IReadOnlyList<ChoiceView> PendingChoices { get; init; } = [];

    /// <summary>
    /// 立场轴；内容包没有立场时为 <c>null</c>（UI 应隐藏该面板）。<para>
    /// 已作答的选择也会一并列出（<see cref="ChoiceView.Options"/> 为空、只有 id 与 prompt），
    /// 便于 UI 展示"你曾经怎么答的"。
    /// </para>
    /// </summary>
    public IReadOnlyList<StanceView>? Stances { get; init; }

    /// <summary>当前主导立场 id；没有立场轴或全部权重为 0 时为 <c>null</c>。</summary>
    public string? DominantStanceId { get; init; }
}
