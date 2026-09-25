using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Persistence;
using NekoClicker.Core.Randomness;
using NekoClicker.Core.Views;

namespace NekoClicker.Core;

/// <summary>引擎构造参数。</summary>
public sealed class GameEngineOptions
{
    /// <summary>时间源。测试传 <see cref="ManualClock"/> 即可完全控制时间。</summary>
    public IClock Clock { get; init; } = SystemClock.Instance;

    /// <summary>随机种子；<c>0</c> 表示按时间随机。</summary>
    public ulong Seed { get; init; }

    /// <summary>读档时是否结算离线收益。</summary>
    public bool GrantOfflineProgress { get; init; } = true;

    /// <summary>保留多少条通知。</summary>
    public int MaxNotifications { get; init; } = 64;

    /// <summary>是否自动在每个逻辑步检查成就。</summary>
    public bool AutoCheckAchievements { get; init; } = true;

    /// <summary>初始状态（测试可注入预设存档）。</summary>
    public GameState? InitialState { get; init; }
}

/// <summary>
/// 增量游戏引擎——整个框架的门面。<para>
/// 职责边界：
/// <list type="bullet">
///   <item><b>拥有</b>：状态、时间推进、内容求解、所有玩家动作的规则与校验、事件派发。</item>
///   <item><b>不知道</b>：怎么渲染、存档写到哪里、玩家按了什么键。</item>
/// </list>
/// 因此同一个引擎既能驱动终端 UI、也能驱动 Web 前端或 Unity 场景，还能在测试里
/// 以数千倍速跑完 24 小时的内容曲线。
/// </para>
/// <para>
/// 时间推进使用<b>固定步长累加器</b>（默认 30Hz，与原版一致）：真实帧率波动不会
/// 影响产量计算结果，同时通过 <see cref="Update"/> 的补算上限避免长时间卡顿后的
/// "死亡螺旋"。需要跳跃式模拟（测试、离线校验）请用 <see cref="Simulate"/>。
/// </para>
/// </summary>
public sealed class GameEngine
{
    private readonly GameMetrics _metrics;
    private readonly List<IGameModule> _modules = [];
    private readonly List<GameNotification> _notifications = [];
    private readonly int _maxNotifications;

    private DeterministicRandom _random;
    private ModifierSet _modifiers = new();
    private ProductionBreakdown _production = ProductionBreakdown.Empty;
    private bool _dirty = true;
    private bool _recomputing;
    private double _accumulator;
    private double _achievementTimer;

    /// <summary>创建引擎。</summary>
    /// <param name="content">内容定义（必须已通过 <see cref="GameContentBuilder"/> 校验）。</param>
    /// <param name="options">可选构造参数。</param>
    public GameEngine(GameContent content, GameEngineOptions? options = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Options = options ?? new GameEngineOptions();
        Clock = Options.Clock;
        Events = new GameEventBus();
        _metrics = new GameMetrics(this);
        _maxNotifications = Math.Max(1, Options.MaxNotifications);

        State = Options.InitialState ?? new GameState();
        State.CreatedAt = Options.InitialState is null ? Clock.UtcNow : State.CreatedAt;
        State.LastSavedAt = Clock.UtcNow;

        _random = CreateRandom();

        foreach (IGameModule module in Content.Modules)
        {
            _modules.Add(module);
            module.OnAttach(this);
        }

        GoldenCookieSystem.ResetSchedule(this);
        MarkDirty();
    }

    /// <summary>内容定义。</summary>
    public GameContent Content { get; }

    /// <summary>构造参数。</summary>
    public GameEngineOptions Options { get; }

    /// <summary>时间源。</summary>
    public IClock Clock { get; }

    /// <summary>事件总线。</summary>
    public GameEventBus Events { get; }

    /// <summary>当前状态。读档/重开会替换整个实例。</summary>
    public GameState State { get; private set; }

    /// <summary>只读状态视图（内容层与 UI 用）。</summary>
    public IGameMetrics Metrics => _metrics;

    /// <summary>平衡参数。</summary>
    public GameBalance Balance => Content.BalanceFor(State.Era);

    /// <summary>已挂载模块。</summary>
    public IReadOnlyList<IGameModule> Modules => _modules;

    /// <summary>最近的玩家可见消息（最多 <see cref="GameEngineOptions.MaxNotifications"/> 条）。</summary>
    public IReadOnlyList<GameNotification> Notifications => _notifications;

    /// <summary>当前生效的修饰符集合（访问时按需重算）。</summary>
    public ModifierSet Modifiers
    {
        get
        {
            RecomputeIfDirty();
            return _modifiers;
        }
    }

    /// <summary>当前生产明细（访问时按需重算）。</summary>
    public ProductionBreakdown Production
    {
        get
        {
            RecomputeIfDirty();
            return _production;
        }
    }

    /// <summary>不触发重算的最近一次生产明细（供求解过程中的回调读取，避免重入）。</summary>
    internal ProductionBreakdown LastProduction => _production;

    /// <summary>每秒产量。</summary>
    public double CookiesPerSecond => Production.CookiesPerSecond;

    /// <summary>单次点击收益。</summary>
    public double ClickPower => Production.ClickPower;

    /// <summary>累计模拟秒数。</summary>
    public double PlayTimeSeconds => State.PlayTimeSeconds;

    /// <summary>随机数源（模块与金猫系统使用）。</summary>
    public DeterministicRandom RandomSource => _random;

    // ---------------------------------------------------------------- 时间推进

    /// <summary>
    /// 按真实经过时间推进模拟。<para>
    /// 超出 <see cref="GameBalance.MaxCatchUpSeconds"/> 的部分会被丢弃——这是刻意的：
    /// 断点调试或系统休眠后，玩家不应该靠"卡帧"一次性白拿几小时产量（那正是离线收益
    /// 上限要处理的事）。
    /// </para>
    /// </summary>
    /// <returns>实际推进的模拟秒数。</returns>
    public double Update(double deltaSeconds)
    {
        if (deltaSeconds <= 0 || double.IsNaN(deltaSeconds)) return 0;

        double cap = Math.Max(Balance.FixedDeltaSeconds, Balance.MaxCatchUpSeconds);
        double dt = Math.Min(deltaSeconds, cap);
        double fixedDelta = Balance.FixedDeltaSeconds;

        _accumulator += dt;
        int maxSteps = (int)Math.Ceiling(cap / fixedDelta) + 1;
        int steps = 0;
        while (_accumulator >= fixedDelta && steps < maxSteps)
        {
            Step(fixedDelta);
            _accumulator -= fixedDelta;
            steps++;
        }
        if (steps >= maxSteps) _accumulator = 0;

        Events.Publish(new TickEvent(dt, State.PlayTimeSeconds));
        return dt;
    }

    /// <summary>直接推进任意时长（测试/离线校验用），不受补算上限约束。</summary>
    /// <param name="seconds">要推进的秒数。</param>
    public void Simulate(double seconds)
    {
        if (seconds <= 0) return;
        double fixedDelta = Balance.FixedDeltaSeconds;
        long steps = (long)Math.Ceiling(seconds / fixedDelta);
        for (long i = 0; i < steps; i++) Step(fixedDelta);
        Events.Publish(new TickEvent(seconds, State.PlayTimeSeconds));
    }

    private void Step(double deltaSeconds)
    {
        RecomputeIfDirty();

        double cps = _production.CookiesPerSecond;
        if (cps > 0)
        {
            double gain = Num.SafeMul(cps, deltaSeconds);
            State.Cookies = Num.SafeAdd(State.Cookies, gain);
            State.CookiesEarnedThisRun = Num.SafeAdd(State.CookiesEarnedThisRun, gain);
            State.CookiesEarnedAllTime = Num.SafeAdd(State.CookiesEarnedAllTime, gain);
        }

        // 峰值产量：单调不减，供分层转生的完成条件使用。
        // 直接用 Cps 会让灰按钮在增益到期时闪烁、进度倒退，所以必须单独记峰值。
        if (cps > State.GetCounter(EraSystem.PeakCpsCounterKey))
            State.Counters[EraSystem.PeakCpsCounterKey] = cps;

        State.PlayTimeSeconds += deltaSeconds;
        State.TickCount++;

        if (BuffSystem.Tick(State, deltaSeconds, Events)) MarkDirty();

        GoldenCookieSystem.Tick(this, deltaSeconds);

        for (int i = 0; i < _modules.Count; i++) _modules[i].OnTick(this, deltaSeconds);

        if (!Options.AutoCheckAchievements) return;

        _achievementTimer += deltaSeconds;
        if (_achievementTimer < Math.Max(0.05, Balance.AchievementCheckInterval)) return;
        _achievementTimer = 0;
        CheckAchievements();
        CheckLore();
        CheckChoices();
    }

    // ---------------------------------------------------------------- 玩家动作

    /// <summary>手动点击一次。</summary>
    public ClickResult Click()
    {
        double power = ClickPower;

        State.Cookies = Num.SafeAdd(State.Cookies, power);
        State.CookiesEarnedThisRun = Num.SafeAdd(State.CookiesEarnedThisRun, power);
        State.CookiesEarnedAllTime = Num.SafeAdd(State.CookiesEarnedAllTime, power);
        State.HandMadeCookies = Num.SafeAdd(State.HandMadeCookies, power);
        State.TotalClicks += 1;

        // 点击可能立刻满足"点击 N 次"这类成就，必须即时反馈，不能等下一个检查周期。
        if (Options.AutoCheckAchievements) CheckAchievements();

        Events.Publish(new ClickedEvent(power, State.Cookies));
        return new ClickResult(power, State.Cookies, power);
    }

    /// <summary>购买建筑。<paramref name="amount"/> 传 <c>0</c> 表示"买到买不起为止"。</summary>
    public PurchaseResult BuyBuilding(string id, int amount = 1)
    {
        if (!Content.BuildingById.TryGetValue(id, out BuildingDefinition? definition))
            return PurchaseResult.Fail($"未知建筑：{id}", id);

        if (!definition.Unlock.IsMet(_metrics, Content))
            return PurchaseResult.Fail($"「{definition.Name}」尚未解锁（{definition.Unlock.Describe(Content)}）。", id);

        int owned = State.BuildingCount(id);
        double priceMultiplier = GetPriceMultiplier(id);
        int cap = Math.Max(1, Balance.MaxBulkBuy);

        amount = amount <= 0
            ? Pricing.MaxAffordable(definition, owned, State.Cookies, priceMultiplier, cap)
            : Math.Min(amount, cap);

        if (amount <= 0)
            return PurchaseResult.Fail($"买不起「{definition.Name}」。", id);

        double total = Pricing.BulkPrice(definition, owned, amount, priceMultiplier);

        if (total > State.Cookies)
        {
            // 指定数量买不起时退化为"能买多少买多少"——原版也是这个手感，
            // 玩家点"×10"时不会因为差一点点而完全无响应。
            int affordable = Pricing.MaxAffordable(definition, owned, State.Cookies, priceMultiplier, amount);
            if (affordable <= 0)
                return PurchaseResult.Fail(
                    $"买不起「{definition.Name}」：需要 {NumFormat.FormatLong(total)}。", id);

            amount = affordable;
            total = Pricing.BulkPrice(definition, owned, amount, priceMultiplier);
        }

        State.Cookies = Math.Max(0, State.Cookies - total);
        State.BuildingCounts[id] = owned + amount;
        MarkDirty();

        Events.Publish(new BuildingPurchasedEvent(id, amount, total / amount, total, owned + amount));
        if (Options.AutoCheckAchievements) CheckAchievements();

        return PurchaseResult.Ok(
            $"购买 {amount} 个「{definition.Name}」，花费 {NumFormat.FormatLong(total)}。",
            id, amount, total, owned + amount);
    }

    /// <summary>出售建筑。<paramref name="amount"/> 传 <c>0</c> 表示全部卖出。</summary>
    public PurchaseResult SellBuilding(string id, int amount = 1)
    {
        if (!Balance.AllowSelling)
            return PurchaseResult.Fail("本游戏不允许出售建筑。", id);

        if (!Content.BuildingById.TryGetValue(id, out BuildingDefinition? definition))
            return PurchaseResult.Fail($"未知建筑：{id}", id);

        int owned = State.BuildingCount(id);
        if (owned <= 0)
            return PurchaseResult.Fail($"没有可出售的「{definition.Name}」。", id);

        amount = amount <= 0 ? owned : Math.Min(amount, owned);

        double refundRate = definition.SellRefundRate ?? Balance.DefaultSellRefundRate;
        double refund = Pricing.SellValue(definition, owned, amount, refundRate);

        State.BuildingCounts[id] = owned - amount;
        State.Cookies = Num.SafeAdd(State.Cookies, refund);
        MarkDirty();

        Events.Publish(new BuildingSoldEvent(id, amount, refund, owned - amount));

        return PurchaseResult.Ok(
            $"出售 {amount} 个「{definition.Name}」，返还 {NumFormat.FormatLong(refund)}。",
            id, amount, refund, owned - amount);
    }

    /// <summary>购买升级（可重复购买的升级会按 <paramref name="amount"/> 连续购买）。</summary>
    public PurchaseResult BuyUpgrade(string id, int amount = 1)
    {
        if (!Content.UpgradeById.TryGetValue(id, out UpgradeDefinition? definition))
            return PurchaseResult.Fail($"未知升级：{id}", id);

        if (!definition.Unlock.IsMet(_metrics, Content))
            return PurchaseResult.Fail($"「{definition.Name}」尚未解锁（{definition.Unlock.Describe(Content)}）。", id);

        int owned = State.UpgradeCount(id);
        int remaining = definition.MaxPurchases - owned;
        if (remaining <= 0)
            return PurchaseResult.Fail($"「{definition.Name}」已经买过了。", id);

        amount = Math.Clamp(amount, 1, remaining);
        double total = Pricing.UpgradePrice(definition, owned, amount);

        bool usesChips = definition.Currency == UpgradeCurrency.PrestigeChips;
        double wallet = usesChips ? State.PrestigeChips : State.Cookies;
        string currencyName = usesChips ? Content.PrestigeCurrencyName : Content.CurrencyName;

        if (wallet < total)
            return PurchaseResult.Fail(
                $"买不起「{definition.Name}」：需要 {NumFormat.FormatLong(total)} {currencyName}。", id);

        if (usesChips)
        {
            State.PrestigeChips = Math.Max(0, State.PrestigeChips - total);
            State.PrestigeChipsSpent += total;
        }
        else
        {
            State.Cookies = Math.Max(0, State.Cookies - total);
        }

        State.UpgradeCounts[id] = owned + amount;
        MarkDirty();

        Events.Publish(new UpgradePurchasedEvent(id, definition.Name, total, definition.Currency, owned + amount));
        Notify($"已购买「{definition.Name}」。", NotificationKind.Success, definition.Icon);
        if (Options.AutoCheckAchievements) CheckAchievements();

        return PurchaseResult.Ok(
            $"购买「{definition.Name}」，花费 {NumFormat.FormatLong(total)} {currencyName}。",
            id, amount, total, owned + amount);
    }

    /// <summary>点中一只金猫。</summary>
    public GoldenCookieResult ClickGoldenCookie(string instanceId) => GoldenCookieSystem.Click(this, instanceId);

    /// <summary>立刻刷出一只金猫（调试/剧情）。</summary>
    public GoldenCookieSpawn SpawnGoldenCookie(string? forcedOutcomeId = null) => GoldenCookieSystem.Spawn(this, forcedOutcomeId);

    /// <summary>直接施加一个增益。</summary>
    public PurchaseResult ApplyBuff(string buffId, double? seconds = null)
    {
        if (!Content.BuffById.TryGetValue(buffId, out BuffDefinition? definition))
            return PurchaseResult.Fail($"未知增益：{buffId}", buffId);

        double duration = BuffSystem.ScaledDuration(Modifiers, buffId, seconds ?? definition.Duration);
        ActiveBuff buff = BuffSystem.Apply(State, Events, definition, duration);
        MarkDirty();

        Notify(
            $"{definition.Name} 生效，持续 {NumFormat.Duration(buff.RemainingSeconds)}。",
            definition.IsDebuff ? NotificationKind.Warning : NotificationKind.Success,
            definition.Icon);

        return PurchaseResult.Ok(
            $"{definition.Name}：{NumFormat.Duration(buff.RemainingSeconds)}",
            buffId, buff.Stacks, duration, buff.Stacks);
    }

    /// <summary>
    /// 转生 —— 整个游戏<b>唯一</b>的重置入口。<para>
    /// 内容包定义了纪元（<see cref="GameContent.HasEras"/>）时，它是"舍一命"：
    /// 必须完成本层主线才能调用，并且会推进层号；否则是经典的单轴转生（随时可用）。<para>
    /// 之所以不做成两个按钮：只要能随时重置换情感能量，玩家就能在同一层无限刷，
    /// 再从第 1 层平推到最后一层 —— 分层的意义会被抹掉（见 ROADMAP R1）。
    /// 调用前请先查 <see cref="EraGate"/> 决定按钮是否置灰。
    /// </para>
    /// </summary>
    public AscensionResult Ascend()
        => Content.HasEras ? EraSystem.Advance(this) : PrestigeSystem.Ascend(this);

    /// <summary>舍命按钮的状态（是否可以推进纪元、不能的原因、本层进度）。</summary>
    public EraGate EraGate => EraSystem.CanAdvance(this);

    /// <summary>检查并解锁所有满足条件的成就。</summary>
    public IReadOnlyList<AchievementDefinition> CheckAchievements()
    {
        List<AchievementDefinition> unlocked = AchievementSystem.Check(Content, State, _metrics, Events);
        if (unlocked.Count == 0) return unlocked;

        MarkDirty();
        foreach (AchievementDefinition definition in unlocked)
            Notify($"成就解锁：{definition.Name}", NotificationKind.Success, definition.Icon);

        return unlocked;
    }

    /// <summary>
    /// 检查并释放所有满足条件的叙事条目。<para>
    /// 与成就同频执行——两者都是"把条件树定期扫一遍"。被
    /// <see cref="GameEngineOptions.AutoCheckAchievements"/> 一并开关。
    /// </para>
    /// </summary>
    public IReadOnlyList<LoreEntry> CheckLore() => LoreSystem.Check(this);

    /// <summary>
    /// 检查并触发所有满足条件的选择。<para>
    /// 与成就 / 叙事同频执行。触发只进待答队列——<b>选择不阻塞</b>（R6），
    /// 玩家可以一直不答，在那之前它不产生任何效果。
    /// </para>
    /// </summary>
    public IReadOnlyList<ChoiceDefinition> CheckChoices() => ChoiceSystem.Check(this);

    /// <summary>作答一次选择。返回是否确实完成了这次作答（重复作答返回 <c>false</c>）。</summary>
    /// <param name="choiceId">选择 id。</param>
    /// <param name="optionId">选中的选项 id。</param>
    public bool AnswerChoice(string choiceId, string optionId) => ChoiceSystem.Answer(this, choiceId, optionId);

    /// <summary>当前主导立场 id；没有立场轴或全部权重为 0 时为 <c>null</c>。</summary>
    public string? DominantStance => ChoiceSystem.DominantStance(Content, State);

    /// <summary>点掉一个叙事弹窗。</summary>
    /// <param name="entryId">条目 id。</param>
    public bool DismissLorePopup(string entryId) => LoreSystem.DismissPopup(this, entryId);

    /// <summary>点掉全部叙事弹窗。</summary>
    public int DismissAllLorePopups() => LoreSystem.DismissAllPopups(this);

    // ---------------------------------------------------------------- 离线收益

    /// <summary>
    /// 结算离线收益。<para>
    /// 使用<b>不含增益</b>的产量计算：玩家下线期间狂热早就过期了，按带增益的秒产量
    /// 补发会显著高估（原版同样以基础产量结算）。
    /// </para>
    /// </summary>
    /// <param name="elapsed">离线时长。</param>
    /// <returns>结算明细；未达到最小离线时长时返回 <c>null</c>。</returns>
    public OfflineProgress? ApplyOfflineProgress(TimeSpan elapsed)
    {
        if (!Options.GrantOfflineProgress) return null;

        double seconds = elapsed.TotalSeconds;
        if (seconds < Balance.MinimumOfflineSeconds) return null;

        double credited = Math.Min(seconds, Math.Max(0, Balance.OfflineCapSeconds));

        RecomputeIfDirty();
        ModifierSet offlineModifiers = ModifierResolver.Build(Content, State, _metrics, includeBuffs: false);
        double cps = ProductionCalculator.Compute(Content, State, offlineModifiers, Balance).CookiesPerSecond;

        double efficiency = Math.Max(0, Balance.OfflineEfficiency)
                            * Math.Max(0, offlineModifiers.Multiplier(ModifierTarget.OfflineEfficiency));

        double gained = Num.SafeMul(Num.SafeMul(cps, credited), efficiency);

        if (gained > 0)
        {
            State.Cookies = Num.SafeAdd(State.Cookies, gained);
            State.CookiesEarnedThisRun = Num.SafeAdd(State.CookiesEarnedThisRun, gained);
            State.CookiesEarnedAllTime = Num.SafeAdd(State.CookiesEarnedAllTime, gained);
        }

        // 离线时间也要计入游玩时长，否则"游玩 N 小时"类成就会莫名落后于真实进度。
        State.PlayTimeSeconds += credited;
        State.LastSavedAt = Clock.UtcNow;

        var progress = new OfflineProgress(seconds, credited, gained, credited < seconds);

        // 模块自己维护的派生状态（计数器之类）不经过 Step()，必须显式通知它们补算。
        for (int i = 0; i < _modules.Count; i++) _modules[i].OnOffline(this, progress);

        return progress;
    }

    // ---------------------------------------------------------------- 存档

    /// <summary>序列化为 JSON 存档字符串（会先把 PRNG 状态同步进存档）。</summary>
    public string Save()
    {
        SyncRandomState();
        State.LastSavedAt = Clock.UtcNow;
        return SaveSerializer.Serialize(this);
    }

    /// <summary>从 JSON 存档字符串恢复，并结算离线收益。</summary>
    /// <returns>离线收益明细；无收益时为 <c>null</c>。</returns>
    public OfflineProgress? Load(string json)
    {
        OfflineProgress? offline = SaveSerializer.DeserializeInto(this, json);
        Events.Publish(new GameLoadedEvent(offline?.CreditedSeconds ?? 0, offline?.CookiesGained ?? 0));
        return offline;
    }

    /// <summary>丢弃全部进度，回到全新开局（连转生记录一起清空）。</summary>
    public void HardReset()
    {
        _notifications.Clear();
        _accumulator = 0;
        _achievementTimer = 0;
        _production = ProductionBreakdown.Empty;
        _modifiers = new();
        _dirty = true;

        State = new GameState
        {
            CreatedAt = Clock.UtcNow,
            LastSavedAt = Clock.UtcNow,
        };
        _random = CreateRandom();
        GoldenCookieSystem.ResetSchedule(this);
    }

    /// <summary>替换状态（读档用）。</summary>
    internal void ReplaceState(GameState state)
    {
        State = state;
        _random = CreateRandom();
        _accumulator = 0;
        _achievementTimer = 0;
        MarkDirty();
    }

    // ---------------------------------------------------------------- 查询与视图

    /// <summary>某建筑的当前价格乘数（全局价格 × 该建筑价格）。</summary>
    public double GetPriceMultiplier(string buildingId)
        => Modifiers.CombinedMultiplier(ModifierTarget.GlobalPrice, ModifierTarget.BuildingPrice(buildingId));

    /// <summary>判断某个解锁条件是否已满足。</summary>
    public bool IsUnlocked(UnlockCondition condition) => condition.IsMet(_metrics, Content);

    /// <summary>生成一帧 UI 所需的完整只读快照。</summary>
    public GameSnapshot Snapshot(PurchaseMode mode = PurchaseMode.Buy1)
        => GameViewFactory.Create(this, mode);

    // ---------------------------------------------------------------- 内部

    /// <summary>标记产量需要重算。任何改变修饰符来源的操作都应调用。</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>添加一条玩家可见消息。</summary>
    public void Notify(string message, NotificationKind kind = NotificationKind.Info, string icon = "")
    {
        var notification = new GameNotification(message, icon, kind, State.PlayTimeSeconds);
        _notifications.Add(notification);
        while (_notifications.Count > _maxNotifications) _notifications.RemoveAt(0);
        Events.Publish(new NotificationEvent(notification));
    }

    /// <summary>清空通知。</summary>
    public void ClearNotifications() => _notifications.Clear();

    /// <summary>把 PRNG 状态写回存档字段。</summary>
    internal void SyncRandomState()
    {
        State.RandomState0 = _random.State0;
        State.RandomState1 = _random.State1;
    }

    private void RecomputeIfDirty()
    {
        if (!_dirty || _recomputing) return;

        _recomputing = true;
        try
        {
            ProductionBreakdown previous = _production;
            _modifiers = ModifierResolver.Build(Content, State, _metrics);
            _production = ProductionCalculator.Compute(Content, State, _modifiers, Balance);
            _dirty = false;

            bool changed = !Num.RelativeEquals(previous.CookiesPerSecond, _production.CookiesPerSecond)
                           || !Num.RelativeEquals(previous.ClickPower, _production.ClickPower);
            if (changed)
                Events.Publish(new ProductionChangedEvent(_production.CookiesPerSecond, _production.ClickPower));
        }
        finally
        {
            _recomputing = false;
        }
    }

    private DeterministicRandom CreateRandom()
    {
        // 已有存档的 PRNG 状态 → 恢复，保证同一存档的随机序列可复现。
        if (State.RandomState0 != 0 || State.RandomState1 != 0)
            return new DeterministicRandom(State.RandomState0, State.RandomState1);

        if (Options.Seed != 0)
        {
            var seeded = new DeterministicRandom(Options.Seed);
            State.RandomState0 = seeded.State0;
            State.RandomState1 = seeded.State1;
            return seeded;
        }

        ulong entropy = (ulong)Clock.UtcNow.ToUnixTimeMilliseconds()
                        ^ ((ulong)Environment.TickCount64 << 17)
                        ^ (ulong)(Guid.NewGuid().GetHashCode() & 0xFFFFFFFF);
        return new DeterministicRandom(entropy);
    }
}
