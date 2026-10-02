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

    /// <summary>
    /// 结局条件成立后留给玩家作答的宽限（<b>模拟</b>秒）。<see langword="null"/> 表示用
    /// <see cref="EndingSystem.DefaultGraceSeconds"/>。<para>
    /// <b>已退役：1.5.0 起它不再参与判定。</b>当年它是"结局最多等多久"，
    /// 现在是"还有答得上的待答表态就不落定"（<see cref="GameEngine.CheckEnding"/>）——
    /// 那个等待没有上界，所以没有任何秒数能表达它。1.5.0 只是不再按时间兜底，
    /// 1.6.0 又把条件从"玩家看过那些表态"收紧成"玩家把它们答完"。
    /// </para>
    /// <para>
    /// <b>为什么留着而不是删掉</b>：删公开成员要按 <c>engine/docs/VERSIONING.md</c> 升主版本，
    /// 而这次是有意的 minor；另外引用它的宿主（<c>--grace</c>、作答延迟埋点）不会因此编不过。
    /// </para>
    /// <para>
    /// <b>设了它会喊一声</b>：构造时会发一条警告通知。一个"设置了却没有任何效果"的选项正是
    /// 本项目最反对的沉默失败——退役不等于可以悄悄忽略。
    /// </para>
    /// </summary>
    public double? EndingGraceSeconds { get; init; }
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

    private OfflineProgress? _pendingOffline;

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

        // 退役的参数必须喊一声（见 EndingGraceSeconds 的注释）：一个"设置了却没有任何效果"
        // 的选项正是本项目最反对的沉默失败。宿主可能照着旧文档把它设成 0/5/120，
        // 而 1.5.0 起判定根本不看它——不说出来的话，那个宿主会以为自己配好了一个期限。
        if (Options.EndingGraceSeconds is not null)
        {
            Notify(
                "EndingGraceSeconds 已不再生效：1.6.0 起结局改为「还有答得上的待答表态就不落定」，"
                + "要等玩家把它们答完，不按时间兜底（见 GameEngine.CheckEnding）。",
                NotificationKind.Warning,
                // 图标只用宽度确定的字符：⚠ 是 East Asian Ambiguous，在终端里按 1 列或 2 列渲染
                // 都可能，会把那行日志顶歪（FrameRenderTests.FramesAvoidFontFallbackProneGlyphs 守这条）。
                "!");
        }
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

    /// <summary>
    /// 已经结算、但**还没被界面播报过**的离线收益；没有则为 <c>null</c>。<para>
    /// 为什么这个状态属于引擎而不是宿主：离线补发是**事件**，事件只该被播报一次，
    /// 而"播报过没有"必须只有一个主人。放在宿主里的后果实测过——刷新一次页面就按各自的
    /// 记忆重复弹，而两次离线补发完全可能数值一模一样（同样离线到上限、产量也没变），
    /// 于是"拿数值当指纹去重"这条路本身就不成立。
    /// </para>
    /// <para>
    /// <b>收益为 0 时不记录</b>（离线时长不到门槛、或产量为 0）：没有东西可以播报。
    /// 播报完由宿主调 <see cref="DismissOfflineProgress"/>。
    /// </para>
    /// </summary>
    public OfflineProgress? PendingOfflineProgress => _pendingOffline;

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

        // 顺序有讲究，两条理由各不相同：
        //
        // ① 表态的触发排在终局判定<b>之前</b>。终局判定要读"还有没有**答得上的**待答表态"
        //    来决定要不要推迟落定（EndingSystem.Check）；若它先跑，同一拍里刚够条件的
        //    表态就还不在待答队列里，判定会误以为"没有东西在等玩家"而立刻落定。
        //    这不是杞人忧天：表态门槛允许<b>等于</b>本层完成门槛（见 ValidateChoiceFitsItsEra
        //    只拦 >），此时表态与结局条件会在同一拍首次成立，顺序就是唯一的区别。
        //    提前一拍对内容没有副作用——没有任何选择的触发条件依赖结局（只有成才会）。
        //
        // ② 终局判定排在成就 / 剧情<b>之前</b>。这样同一拍里"依赖结局"的内容
        //    （Unlock = EndingReached(...)）就能立刻结算，而不用再等一个检查周期。
        CheckChoices();
        CheckEnding();
        CheckAchievements();
        CheckLore();
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
    /// 必须完成本层主线才能调用，并且会推进层号；否则是经典的单轴转生（随时可用）。
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

    /// <summary>
    /// 作答一次选择。返回是否确实完成了这次作答（重复作答返回 <c>false</c>）。<para>
    /// <b>1.6.0 起这是唯一能让结局解除等待的动作</b>：只要还有答得上的待答表态，结局就不落定
    /// （见 <see cref="CheckEnding"/>）。返回 <c>false</c> 时表态仍然挂着——如果是"本来就没答上"
    /// （内容里没有这个 id、或已经答过），那一条不拦结局。
    /// </para>
    /// </summary>
    /// <param name="choiceId">选择 id。</param>
    /// <param name="optionId">选中的选项 id。</param>
    public bool AnswerChoice(string choiceId, string optionId) => ChoiceSystem.Answer(this, choiceId, optionId);

    /// <summary>
    /// 报告"当前挂着的待答表态已经被展示给玩家看过了"。<b>由宿主在真的渲染出表态时调用。</b><para>
    /// <b>1.6.0 起它不参与任何判定。</b>结局等的是玩家把这些表态<b>答掉</b>
    /// （见 <see cref="CheckEnding"/>），不是"看过"——所以这个调用<b>不会</b>让结局允许落定，
    /// 也不再是宿主的必做动作。留着它是因为它记下的东西本身有用：<b>每条表态到底露过面没有</b>。
    /// 在新规则下"结局一直没落定"是一件真实可能的事，而
    /// <b>"玩家从没看到这条表态"与"看到了却一直没答"是两种完全不同的原因</b>，
    /// 没有这份记录就分不出来。
    /// </para>
    /// <para>
    /// <b>记录在哪、怎么读</b>：逐条记在 <see cref="GameState.Counters"/> 的
    /// <c>$choice_shown_&lt;选择 id&gt;</c> 上（与 <c>EraSystem.PeakCpsCounterKey</c>、
    /// 就绪时刻 <c>$ending_ready_at_play_time</c> 同一个套路），因此<b>随存档往返</b>——
    /// 问的是"这个存档里的表态露过面没有"，而玩家是跨会话的。要读它就直接读
    /// <see cref="GameState.Counters"/>（键前缀是公开的约定，没有为此新增任何公开成员）。
    /// </para>
    /// <para>
    /// <b>逐条记账</b>：只标<b>此刻</b>挂在待答队列里的表态，之后才触发的不算"已经展示过"——
    /// 存一个布尔值会把"看过第一批"误当成"看过之后所有批次"。
    /// </para>
    /// <para>
    /// <b>1.5.0 到 1.6.0 之间它是什么</b>（留档，别按这段去改代码）：那段时间它是结局能否落定的
    /// <b>唯一</b>依据——"玩家被展示过那批待答表态"之后结局才允许落定。那条规则已被
    /// 1.6.0 换掉（看过 ⇒ 答过），因为"看过"只证明面板画出来了，证明不了玩家不再需要它。
    /// </para>
    /// </summary>
    /// <returns>本次新标记为"已展示"的表态条数；都已标记过（或当前没有待答表态）时为 <c>0</c>。</returns>
    public int MarkPendingChoicesShown() => ChoiceSystem.MarkShown(this);

    /// <summary>当前主导立场 id；没有立场轴或全部权重为 0 时为 <c>null</c>。</summary>
    public string? DominantStance => ChoiceSystem.DominantStance(Content, State);

    /// <summary>
    /// 检查终局判定。达成第一个满足条件的结局就记下，之后不再判（一份存档一个结局）。<para>
    /// 与其它检查同频执行。判定完全由条件树驱动，引擎不认识"哪一层是最后一层"——
    /// 想表达"走完主线"就在内容里写 <c>EraAtLeast(9)</c>。
    /// </para>
    /// <para>
    /// 条件成立时若还有<b>还没被答掉、而且答得上</b>的待答表态，落定会一直推迟，直到玩家把它们
    /// 答完。<b>1.6.0 起这是唯一会推迟落定的机制</b>，判据是作答而不是"看过"
    /// （<see cref="MarkPendingChoicesShown"/> 那份展示记录现在只作诊断），
    /// 而且它<b>没有时限</b>：只要还有一条在等作答，就永远不落定。
    /// （1.5.0 之前是"最多等 <see cref="GameEngineOptions.EndingGraceSeconds"/> 模拟秒"，
    /// 1.5.0 到 1.6.0 之间是"玩家被展示过就行"。）
    /// </para>
    /// <para>
    /// <b>刻意接受的代价</b>：一个从不作答的玩家，永远拿不到结局。这是<b>有意移除</b>
    /// "不会永远悬着"那条性质，不是疏漏——见 CHANGELOG 的 1.6.0。
    /// </para>
    /// <para>
    /// <b>唯一不拦的情形是"答不上"</b>：队列里的 id 在当前内容里不存在（换包读档 / 内容改版
    /// 删掉了它），或它已经出现在已答表里。那两种如果也算数，这一局就永久卡死了；
    /// 所以它们不拦，而且引擎会发一条警告把这件事说出来。判据见
    /// <see cref="EndingSystem.Check"/>。
    /// </para>
    /// </summary>
    public EndingDefinition? CheckEnding() => EndingSystem.Check(this);

    /// <summary>当前存档已达成的结局；未达成时为 <c>null</c>。</summary>
    public EndingDefinition? ReachedEnding => EndingSystem.Reached(Content, State);

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

        // 补发了多少是一件事、"有没有被界面播报过"是另一件：这里只登记，播报由宿主决定
        // （它拿到的是快照里的 `offline`），播报完再调 DismissOfflineProgress。
        if (progress.CookiesGained > 0) _pendingOffline = progress;

        return progress;
    }

    /// <summary>
    /// 宣告"这条离线收益已经播报过了"，之后 <see cref="PendingOfflineProgress"/> 归 <c>null</c>。
    /// </summary>
    /// <returns>
    /// 之前确实有待播报的收益时为 <c>true</c>；本来就没有（或已经播报过）时为 <c>false</c>。
    /// 重复调用是安全的——宿主不该为了幂等自己再存一份状态。
    /// </returns>
    public bool DismissOfflineProgress()
    {
        if (_pendingOffline is null) return false;
        _pendingOffline = null;
        return true;
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
        // 上一局的离线收益还没播报就先重开：那笔钱已经不属于这一局了，不该再弹。
        _pendingOffline = null;
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
