namespace NekoClicker.Core.Content;

/// <summary>
/// 内容构建器。<para>
/// 负责收集定义 → 校验 → 产出不可变的 <see cref="GameContent"/>。校验在构建期完成，
/// 任何指向不存在 id 的解锁条件、重复 id、非法数值都会在这里抛出
/// <see cref="GameContentValidationException"/>，而不是在玩家点了某个按钮时才炸。
/// </para>
/// </summary>
public sealed class GameContentBuilder
{
    private readonly List<BuildingDefinition> _buildings = [];
    private readonly List<UpgradeDefinition> _upgrades = [];
    private readonly List<AchievementDefinition> _achievements = [];
    private readonly List<BuffDefinition> _buffs = [];
    private readonly List<GoldenCookieOutcome> _goldenCookieOutcomes = [];
    private readonly List<EraDefinition> _eras = [];
    private readonly List<LoreEntry> _loreEntries = [];
    private readonly List<StorylineDefinition> _storylines = [];
    private readonly List<StanceDefinition> _stances = [];
    private readonly List<ChoiceDefinition> _choices = [];
    private readonly List<EndingDefinition> _endings = [];
    private readonly List<IGameModule> _modules = [];

    /// <summary>创建构建器。</summary>
    /// <param name="title">游戏标题。</param>
    public GameContentBuilder(string title = "Incremental Game")
    {
        Title = title;
    }

    /// <summary>游戏标题。</summary>
    public string Title { get; private set; }

    /// <summary>主货币名称。</summary>
    public string CurrencyName { get; private set; } = "cookies";

    /// <summary>主货币图标。</summary>
    public string CurrencyIcon { get; private set; } = "🍪";

    /// <summary>点击动作名称。</summary>
    public string ClickActionName { get; private set; } = "Click";

    /// <summary>转生货币名称。</summary>
    public string PrestigeCurrencyName { get; private set; } = "prestige chips";

    /// <summary>转生货币图标。</summary>
    public string PrestigeCurrencyIcon { get; private set; } = "🌟";

    /// <summary>平衡参数。</summary>
    public GameBalance Balance { get; private set; } = new();

    /// <summary>已登记模块。</summary>
    public IReadOnlyList<IGameModule> Modules => _modules;

    /// <summary>设置标题。</summary>
    public GameContentBuilder WithTitle(string title)
    {
        Title = title;
        return this;
    }

    /// <summary>设置主货币文案。</summary>
    public GameContentBuilder WithCurrency(string name, string icon, string? clickActionName = null)
    {
        CurrencyName = name;
        CurrencyIcon = icon;
        if (clickActionName is not null) ClickActionName = clickActionName;
        return this;
    }

    /// <summary>设置转生货币文案。</summary>
    public GameContentBuilder WithPrestigeCurrency(string name, string icon)
    {
        PrestigeCurrencyName = name;
        PrestigeCurrencyIcon = icon;
        return this;
    }

    /// <summary>设置平衡参数。</summary>
    public GameContentBuilder WithBalance(GameBalance balance)
    {
        Balance = balance;
        return this;
    }

    /// <summary>注册模块（模块可在 <c>Configure</c> 中继续补充定义）。</summary>
    public GameContentBuilder Add(IGameModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules.Add(module);
        return this;
    }

    /// <summary>添加建筑。</summary>
    public GameContentBuilder Add(BuildingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _buildings.Add(definition);
        return this;
    }

    /// <summary>批量添加建筑。</summary>
    public GameContentBuilder AddBuildings(params BuildingDefinition[] definitions)
    {
        _buildings.AddRange(definitions);
        return this;
    }

    /// <summary>添加升级。</summary>
    public GameContentBuilder Add(UpgradeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _upgrades.Add(definition);
        return this;
    }

    /// <summary>批量添加升级。</summary>
    public GameContentBuilder AddUpgrades(params UpgradeDefinition[] definitions)
    {
        _upgrades.AddRange(definitions);
        return this;
    }

    /// <summary>添加成就。</summary>
    public GameContentBuilder Add(AchievementDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _achievements.Add(definition);
        return this;
    }

    /// <summary>批量添加成就。</summary>
    public GameContentBuilder AddAchievements(params AchievementDefinition[] definitions)
    {
        _achievements.AddRange(definitions);
        return this;
    }

    /// <summary>添加增益。</summary>
    public GameContentBuilder Add(BuffDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _buffs.Add(definition);
        return this;
    }

    /// <summary>批量添加增益。</summary>
    public GameContentBuilder AddBuffs(params BuffDefinition[] definitions)
    {
        _buffs.AddRange(definitions);
        return this;
    }

    /// <summary>添加金猫结果。</summary>
    public GameContentBuilder Add(GoldenCookieOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        _goldenCookieOutcomes.Add(outcome);
        return this;
    }

    /// <summary>批量添加金猫结果。</summary>
    public GameContentBuilder AddGoldenCookieOutcomes(params GoldenCookieOutcome[] outcomes)
    {
        _goldenCookieOutcomes.AddRange(outcomes);
        return this;
    }

    /// <summary>添加一个纪元（转生分层）。</summary>
    public GameContentBuilder Add(EraDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _eras.Add(definition);
        return this;
    }

    /// <summary>批量添加纪元。</summary>
    public GameContentBuilder AddEras(params EraDefinition[] definitions)
    {
        _eras.AddRange(definitions);
        return this;
    }

    /// <summary>添加一条叙事条目。</summary>
    public GameContentBuilder Add(LoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _loreEntries.Add(entry);
        return this;
    }

    /// <summary>批量添加叙事条目。</summary>
    public GameContentBuilder AddLore(params LoreEntry[] entries)
    {
        _loreEntries.AddRange(entries);
        return this;
    }

    /// <summary>添加一条剧情线。</summary>
    public GameContentBuilder Add(StorylineDefinition storyline)
    {
        ArgumentNullException.ThrowIfNull(storyline);
        _storylines.Add(storyline);
        return this;
    }

    /// <summary>批量添加剧情线。</summary>
    public GameContentBuilder AddStorylines(params StorylineDefinition[] storylines)
    {
        _storylines.AddRange(storylines);
        return this;
    }

    /// <summary>添加一条立场（价值取向）。</summary>
    public GameContentBuilder Add(StanceDefinition stance)
    {
        ArgumentNullException.ThrowIfNull(stance);
        _stances.Add(stance);
        return this;
    }

    /// <summary>批量添加立场。</summary>
    public GameContentBuilder AddStances(params StanceDefinition[] stances)
    {
        _stances.AddRange(stances);
        return this;
    }

    /// <summary>添加一次选择。</summary>
    public GameContentBuilder Add(ChoiceDefinition choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        _choices.Add(choice);
        return this;
    }

    /// <summary>批量添加选择。</summary>
    public GameContentBuilder AddChoices(params ChoiceDefinition[] choices)
    {
        _choices.AddRange(choices);
        return this;
    }

    /// <summary>添加一个结局。</summary>
    public GameContentBuilder Add(EndingDefinition ending)
    {
        ArgumentNullException.ThrowIfNull(ending);
        _endings.Add(ending);
        return this;
    }

    /// <summary>批量添加结局。</summary>
    public GameContentBuilder AddEndings(params EndingDefinition[] endings)
    {
        _endings.AddRange(endings);
        return this;
    }

    /// <summary>构建并校验。</summary>
    /// <exception cref="GameContentValidationException">存在校验错误。</exception>
    public GameContent Build()
    {
        // 模块先补充定义，再一并校验。
        foreach (IGameModule module in _modules) module.Configure(this);

        var buildingById = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);
        var upgradeById = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);
        var achievementById = new Dictionary<string, AchievementDefinition>(StringComparer.Ordinal);
        var buffById = new Dictionary<string, BuffDefinition>(StringComparer.Ordinal);
        var eraByIndex = new Dictionary<int, EraDefinition>();
        var stanceById = new Dictionary<string, StanceDefinition>(StringComparer.Ordinal);
        var choiceById = new Dictionary<string, ChoiceDefinition>(StringComparer.Ordinal);
        var endingById = new Dictionary<string, EndingDefinition>(StringComparer.Ordinal);
        var errors = new List<string>();

        Index(_buildings, b => b.Id, buildingById, "建筑", errors);
        Index(_upgrades, u => u.Id, upgradeById, "升级", errors);
        Index(_achievements, a => a.Id, achievementById, "成就", errors);
        Index(_buffs, b => b.Id, buffById, "增益", errors);
        IndexEras(eraByIndex, errors);

        // 立场与选择的索引要在逐实体校验之前建好：建筑/升级/成就的条件里可以引用
        // ChoiceMade(id)，所以 ValidateCondition 需要 choiceById 才能判断引用是否存在。
        Index(_stances, s => s.Id, stanceById, "立场", errors);
        Index(_choices, c => c.Id, choiceById, "选择", errors);
        Index(_endings, e => e.Id, endingById, "结局", errors);

        foreach (BuildingDefinition b in _buildings)
        {
            if (b.BasePrice < 0) errors.Add($"建筑 「{b.Id}」 的 BasePrice 不能为负。");
            if (b.BaseCps < 0) errors.Add($"建筑 「{b.Id}」 的 BaseCps 不能为负。");
            if (b.PriceGrowth <= 1) errors.Add($"建筑 「{b.Id}」 的 PriceGrowth 必须大于 1（否则价格不增长）。");
            if (b.SellRefundRate is < 0 or > 1) errors.Add($"建筑 「{b.Id}」 的 SellRefundRate 必须在 [0,1] 内。");
            ValidateCondition(b.Unlock, $"建筑 「{b.Id}」", buildingById, upgradeById, achievementById, choiceById, errors);
        }

        foreach (UpgradeDefinition u in _upgrades)
        {
            if (u.Price < 0) errors.Add($"升级 「{u.Id}」 的 Price 不能为负。");
            if (u.MaxPurchases < 1) errors.Add($"升级 「{u.Id}」 的 MaxPurchases 至少为 1。");
            if (u.Persistence == UpgradePersistence.Permanent && u.Currency == UpgradeCurrency.Cookies)
                errors.Add($"升级 「{u.Id}」 是 Permanent 但用普通货币计价；永久升级通常应以转生货币购买（如非本意请改为 Run）。");
            ValidateCondition(u.Unlock, $"升级 「{u.Id}」", buildingById, upgradeById, achievementById, choiceById, errors);
            ValidateModifiers(u.Modifiers, $"升级 「{u.Id}」", buildingById, buffById, errors);
        }

        foreach (AchievementDefinition a in _achievements)
        {
            if (a.Unlock is ConstantCondition { Value: false })
                errors.Add($"成就 「{a.Id}」 的条件恒为假，永远无法解锁。");
            ValidateCondition(a.Unlock, $"成就 「{a.Id}」", buildingById, upgradeById, achievementById, choiceById, errors);
            ValidateModifiers(a.Modifiers, $"成就 「{a.Id}」", buildingById, buffById, errors);
        }

        foreach (BuffDefinition b in _buffs)
        {
            if (b.Duration <= 0) errors.Add($"增益 「{b.Id}」 的 Duration 必须为正。");
            if (b.MaxStacks < 1) errors.Add($"增益 「{b.Id}」 的 MaxStacks 至少为 1。");
            ValidateModifiers(b.Modifiers, $"增益 「{b.Id}」", buildingById, buffById, errors);
        }

        if (_goldenCookieOutcomes.Count > 0 && !Balance.GoldenCookiesEnabled)
            errors.Add("已定义金猫结果，但 Balance.GoldenCookiesEnabled 为 false。");

        foreach (GoldenCookieOutcome o in _goldenCookieOutcomes)
        {
            if (o.Weight <= 0) errors.Add($"金猫结果 「{o.Id}」 的 Weight 必须为正。");
            if (o.BuffId is not null && !buffById.ContainsKey(o.BuffId))
                errors.Add($"金猫结果 「{o.Id}」 引用了不存在的增益 「{o.BuffId}」。");
            if (o.SecondaryBuffId is not null && !buffById.ContainsKey(o.SecondaryBuffId))
                errors.Add($"金猫结果 「{o.Id}」 引用了不存在的增益 「{o.SecondaryBuffId}」。");
            if (o.BuffId is not null && o.BuffSeconds <= 0)
                errors.Add($"金猫结果 「{o.Id}」 声明了增益但没有设置 BuffSeconds。");
        }

        // 纪元的完成条件是灰按钮的数据源，必须单调——单独校验。
        foreach (EraDefinition era in _eras)
        {
            ValidateEra(era, buildingById, upgradeById, achievementById, buffById, choiceById, errors);
        }

        // 叙事：条数声明、序号、剧情线引用都要自洽。
        var loreById = new Dictionary<string, LoreEntry>(StringComparer.Ordinal);
        var storylineById = new Dictionary<string, StorylineDefinition>(StringComparer.Ordinal);
        Index(_loreEntries, l => l.Id, loreById, "叙事条目", errors);
        Index(_storylines, s => s.Id, storylineById, "剧情线", errors);
        ValidateLore(loreById, storylineById, buildingById, upgradeById, achievementById, choiceById, errors);
        ValidateChoices(choiceById, stanceById, eraByIndex, buildingById, upgradeById, achievementById, buffById, errors);
        ValidateEndings(_endings, buildingById, upgradeById, achievementById, choiceById, errors);

        // 最后做一次全局可达性分析：前两步只能发现"引用不存在"，
        // 发现不了"互相引用导致谁也解不开"。
        ValidateReachability(buildingById, upgradeById, achievementById, loreById, choiceById, endingById, errors);

        if (errors.Count > 0) throw new GameContentValidationException(errors);

        return new GameContent
        {
            Title = Title,
            CurrencyName = CurrencyName,
            CurrencyIcon = CurrencyIcon,
            ClickActionName = ClickActionName,
            PrestigeCurrencyName = PrestigeCurrencyName,
            PrestigeCurrencyIcon = PrestigeCurrencyIcon,
            Balance = Balance,
            Buildings = _buildings,
            BuildingById = buildingById,
            Upgrades = _upgrades,
            UpgradeById = upgradeById,
            Achievements = _achievements,
            AchievementById = achievementById,
            Buffs = _buffs,
            BuffById = buffById,
            GoldenCookieOutcomes = _goldenCookieOutcomes,
            GoldenCookieWeightTotal = _goldenCookieOutcomes.Sum(o => o.Weight),
            Modules = _modules,
            Eras = [.. _eras.OrderBy(e => e.Index)],
            EraByIndex = eraByIndex,
            MaxEraIndex = eraByIndex.Count == 0 ? 1 : eraByIndex.Keys.Max(),
            LoreEntries = [.. _loreEntries
                .OrderBy(l => l.StorylineId, StringComparer.Ordinal)
                .ThenBy(l => l.Order)],
            LoreById = loreById,
            Storylines = _storylines,
            StorylineById = storylineById,
            Stances = _stances,
            StanceById = stanceById,
            Choices = _choices,
            ChoiceById = choiceById,
            Endings = _endings,
            EndingById = endingById,
        };
    }

    /// <summary>校验叙事条目与剧情线的自洽性。</summary>
    private static void ValidateLore(
        Dictionary<string, LoreEntry> loreById,
        Dictionary<string, StorylineDefinition> storylineById,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, ChoiceDefinition> choices,
        List<string> errors)
    {
        // 剧情线声明条数必须与实际一致，否则图鉴的 "3 / 20" 会骗人。
        Dictionary<string, int> actual = new(StringComparer.Ordinal);
        HashSet<string> orders = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in loreById.Values)
        {
            string owner = $"叙事条目 「{entry.Id}」";

            if (string.IsNullOrWhiteSpace(entry.Title)) errors.Add($"{owner} 缺少标题。");
            if (string.IsNullOrWhiteSpace(entry.Body)) errors.Add($"{owner} 缺少正文。");

            if (!storylineById.ContainsKey(entry.StorylineId))
            {
                errors.Add($"{owner} 引用了不存在的剧情线 「{entry.StorylineId}」。");
            }
            else
            {
                actual[entry.StorylineId] = actual.GetValueOrDefault(entry.StorylineId) + 1;

                // 同一条线里序号重复 → 剧情顺序不确定。
                string orderKey = $"{entry.StorylineId}#{entry.Order}";
                if (!orders.Add(orderKey))
                    errors.Add($"{owner} 的序号与同剧情线的另一条重复（{entry.StorylineId} #{entry.Order}）。");
            }

            if (entry.Channel == LoreChannel.EraText)
                errors.Add($"{owner} 使用了 EraText 通道，但那一通道由纪元定义的进 / 出文本承载，条目本身不该用它。");

            if (entry.Reveal is ConstantCondition { Value: false })
                errors.Add($"{owner} 的释放条件恒为假，永远放不出来。");

            ValidateCondition(entry.Reveal, owner, buildings, upgrades, achievements, choices, errors);
        }

        foreach (StorylineDefinition storyline in storylineById.Values)
        {
            int count = actual.GetValueOrDefault(storyline.Id);
            if (count == 0)
                errors.Add($"剧情线 「{storyline.Id}」 没有任何叙事条目。");
            else if (storyline.TotalEntries != count)
                errors.Add(
                    $"剧情线 「{storyline.Id}」 声明了 {storyline.TotalEntries} 条，实际有 {count} 条" +
                    $"（图鉴会显示错误的总数，请同步）。");
        }
    }

    /// <summary>索引纪元并校验"从 1 开始连续"。</summary>
    private void IndexEras(Dictionary<int, EraDefinition> eraByIndex, List<string> errors)
    {
        foreach (EraDefinition era in _eras)
        {
            if (string.IsNullOrWhiteSpace(era.Id)) { errors.Add("纪元存在空 id。"); continue; }
            if (era.Index < 1) { errors.Add($"纪元 「{era.Id}」 的 Index 必须从 1 开始。"); continue; }
            if (!eraByIndex.TryAdd(era.Index, era))
                errors.Add($"纪元层号重复：{era.Index}（{era.Id}）。");
        }

        // 缺层会让"逐级推进"断链：第 3 层之后直接跳到第 5 层，玩家会卡在门后。
        for (int index = 1; index <= eraByIndex.Count; index++)
        {
            if (!eraByIndex.ContainsKey(index))
                errors.Add($"纪元层号不连续：缺少第 {index} 层（已定义 {eraByIndex.Count} 层）。");
        }
    }

    /// <summary>校验单个纪元定义。</summary>
    private static void ValidateEra(
        EraDefinition era,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, BuffDefinition> buffs,
        Dictionary<string, ChoiceDefinition> choices,
        List<string> errors)
    {
        string owner = $"纪元 「{era.Id}」";

        if (!double.IsFinite(era.MetaRewardMultiplier) || era.MetaRewardMultiplier < 0)
            errors.Add($"{owner} 的 MetaRewardMultiplier 必须是非负有限数（当前 {era.MetaRewardMultiplier}）。");

        if (era.InheritBuildingRatio is < 0 or > 1)
            errors.Add($"{owner} 的 InheritBuildingRatio 必须在 [0,1] 内（当前 {era.InheritBuildingRatio}）。");

        foreach (string id in era.InheritBuildings)
            if (!buildings.ContainsKey(id))
                errors.Add($"{owner} 的保留白名单引用了不存在的建筑 「{id}」。");

        foreach (string id in era.UnlocksBuildings)
            if (!buildings.ContainsKey(id))
                errors.Add($"{owner} 的 UnlocksBuildings 引用了不存在的建筑 「{id}」。");

        foreach (string id in era.UnlocksUpgrades)
            if (!upgrades.ContainsKey(id))
                errors.Add($"{owner} 的 UnlocksUpgrades 引用了不存在的升级 「{id}」。");

        ValidateModifiers(era.Modifiers, owner, buildings, buffs, errors);
        ValidateCondition(era.Completion, owner, buildings, upgrades, achievements, choices, errors);
        ValidateCompletionIsMonotonic(era, errors);
    }

    /// <summary>
    /// 完成条件必须<b>单调不减</b>。<para>
    /// 这个条件会持续显示在舍命按钮上；一旦它引用的指标可能下降（花掉的货币、卖掉的建筑、
    /// 到期后掉下来的产量、花掉的转生货币），玩家就会看到"进度倒退"、灰按钮闪烁，
    /// 而且"不可能卡死"这个性质也随之失效——那正是不需要逃生阀的全部理由（ROADMAP R2/R3）。
    /// </para>
    /// </summary>
    private static void ValidateCompletionIsMonotonic(EraDefinition era, List<string> errors)
    {
        foreach (NumericCondition condition in era.Completion.NumericLeaves())
        {
            if (ForbiddenInCompletion.Contains(condition.Metric))
            {
                errors.Add(
                    $"纪元 「{era.Id}」 的完成条件用了会下降的指标 {condition.Metric}：" +
                    $"灰按钮的进度会倒退，且可能让玩家卡在无法完成的状态。" +
                    $"请改用累计赚取 / 成就数 / 点击数 / 时长 / " +
                    $"{EraSystem.PeakCpsCounterKey} 计数器这类单调不减的指标。");
            }
            else if (!MonotonicMetrics.Contains(condition.Metric))
            {
                errors.Add(
                    $"纪元 「{era.Id}」 的完成条件用了未经白名单确认的指标 {condition.Metric}：" +
                    $"请先确认它单调不减，再把它加进 GameContentBuilder.MonotonicMetrics。");
            }
        }
    }

    /// <summary>允许出现在纪元完成条件里的指标（单调不减）。</summary>
    private static readonly NumericMetric[] MonotonicMetrics =
    [
        NumericMetric.CookiesEarnedThisRun,   // 每层归零，但层内只增
        NumericMetric.CookiesEarnedAllTime,
        NumericMetric.Clicks,
        NumericMetric.PrestigeLevel,
        NumericMetric.AchievementCount,
        NumericMetric.GoldenCookiesClicked,
        NumericMetric.PurchasedUpgrades,
        NumericMetric.PlayTimeSeconds,
        NumericMetric.Counter,                // 计数器是否单调由内容/模块自己保证
        NumericMetric.TaggedUpgrades,
        NumericMetric.LoreCount,
    ];

    /// <summary>明确<b>禁止</b>出现在纪元完成条件里的指标（会下降）。</summary>
    private static readonly NumericMetric[] ForbiddenInCompletion =
    [
        NumericMetric.CurrentCookies,   // 会被花掉
        NumericMetric.Cps,              // 增益到期会掉，灰按钮会闪
        NumericMetric.BuildingCount,    // 建筑可以卖
        NumericMetric.TotalBuildings,   // 同上
        NumericMetric.PrestigeChips,    // 会被花掉买永久升级
        NumericMetric.Era,              // 会自我指涉：本层的条件不该引用层号
    ];

    private static void Index<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        Dictionary<string, T> target,
        string kind,
        List<string> errors)
    {
        foreach (T item in items)
        {
            string id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{kind}存在空 id。");
                continue;
            }
            if (!target.TryAdd(id, item))
                errors.Add($"{kind} id 重复：{id}");
        }
    }

    private static void ValidateCondition(
        UnlockCondition condition,
        string owner,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, ChoiceDefinition> choices,
        List<string> errors)
    {
        foreach (NumericCondition n in condition.NumericLeaves())
        {
            if (n.Target <= 0) errors.Add($"{owner} 的解锁条件阈值必须为正（{n.Metric} = {n.Target}）。");
            if (n.Metric == NumericMetric.BuildingCount)
            {
                if (string.IsNullOrEmpty(n.Id)) errors.Add($"{owner} 的 BuildingCount 条件缺少建筑 id。");
                else if (!buildings.ContainsKey(n.Id)) errors.Add($"{owner} 的解锁条件引用了不存在的建筑 「{n.Id}」。");
            }
            else if (n.Metric == NumericMetric.Counter && string.IsNullOrEmpty(n.Id))
            {
                errors.Add($"{owner} 的 Counter 条件缺少计数器键。");
            }
            else if (n.Metric == NumericMetric.TaggedUpgrades && string.IsNullOrEmpty(n.Id))
            {
                errors.Add($"{owner} 的 TaggedUpgrades 条件缺少标签 id。");
            }
            else if (n.Metric == NumericMetric.StanceWeight && string.IsNullOrEmpty(n.Id))
            {
                errors.Add($"{owner} 的 StanceWeight 条件缺少立场 id。");
            }
        }

        foreach (OwnedCondition o in condition.OwnedLeaves())
        {
            bool ok = o.Kind switch
            {
                OwnedKind.Upgrade => upgrades.ContainsKey(o.Id),
                OwnedKind.Achievement => achievements.ContainsKey(o.Id),
                OwnedKind.Choice => choices.ContainsKey(o.Id),
                _ => false,
            };
            if (!ok) errors.Add($"{owner} 的解锁条件引用了不存在的 {o.Kind} 「{o.Id}」。");
        }
    }

    /// <summary>
    /// 校验选择与立场的自洽性。<para>
    /// 这些规则看似琐碎，但每一条都对应一种"运行起来才发现"的坏内容：
    /// 只有一个选项的选择不构成选择；选项 id 重复会让"选了哪个"变得不确定；
    /// 引用不存在的立场会让权重加到一个永远不会被读取的键上（静默失效）。
    /// </para>
    /// </summary>
    private static void ValidateChoices(
        Dictionary<string, ChoiceDefinition> choices,
        Dictionary<string, StanceDefinition> stances,
        Dictionary<int, EraDefinition> eras,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, BuffDefinition> buffs,
        List<string> errors)
    {
        bool anyStanceOption = false;

        // 层 id 集合：EraId 引用的必须是真实存在的层。
        HashSet<string> eraIds = new(StringComparer.Ordinal);
        foreach (EraDefinition era in eras.Values) eraIds.Add(era.Id);

        foreach (ChoiceDefinition choice in choices.Values)
        {
            string owner = $"选择 「{choice.Id}」";

            if (string.IsNullOrWhiteSpace(choice.Speaker)) errors.Add($"{owner} 缺少说话人。");
            if (string.IsNullOrWhiteSpace(choice.Prompt)) errors.Add($"{owner} 缺少问题描述。");

            if (choice.EraId.Length > 0 && !eraIds.Contains(choice.EraId))
                errors.Add($"{owner} 的 EraId 引用了不存在的纪元 「{choice.EraId}」。");

            if (choice.Trigger is ConstantCondition { Value: false })
                errors.Add($"{owner} 的触发条件恒为假，玩家永远遇不到它。");

            // 触发条件也要做引用校验：漏掉这一句的话，"引用了一个不存在的选择"
            // 就只能靠后面的可达性分析兜底，报出来的原因是"依赖…解不开"而不是"引用了不存在的东西"。
            ValidateCondition(choice.Trigger, owner, buildings, upgrades, achievements, choices, errors);

            if (choice.Options.Count < 2)
                errors.Add($"{owner} 只有 {choice.Options.Count} 个选项——不构成选择（至少 2 个）。");

            HashSet<string> optionIds = new(StringComparer.Ordinal);
            foreach (ChoiceOption option in choice.Options)
            {
                string optionOwner = $"{owner} 的选项 「{option.Id}」";

                if (string.IsNullOrWhiteSpace(option.Id)) errors.Add($"{owner} 有选项缺少 id。");
                else if (!optionIds.Add(option.Id)) errors.Add($"{owner} 的选项 id 重复：{option.Id}");

                if (string.IsNullOrWhiteSpace(option.Label)) errors.Add($"{optionOwner} 缺少按钮文字。");
                if (string.IsNullOrWhiteSpace(option.OutcomeText)) errors.Add($"{optionOwner} 缺少结果文本。");

                if (option.Weight < 0) errors.Add($"{optionOwner} 的权重不能为负（当前 {option.Weight}）。");

                if (option.StanceId.Length > 0)
                {
                    anyStanceOption = true;
                    if (!stances.ContainsKey(option.StanceId))
                        errors.Add($"{optionOwner} 引用了不存在的立场 「{option.StanceId}」。");
                }
                else if (option.Weight > 0)
                {
                    errors.Add($"{optionOwner} 没有立场却有权重 {option.Weight}——权重会加到一个不存在的轴上。");
                }

                ValidateModifiers(option.Modifiers, optionOwner, buildings, buffs, errors);
            }
        }

        if (anyStanceOption && stances.Count == 0)
            errors.Add("有选项声明了立场，但这个内容包没有定义任何立场——立场轴不会生效。");

        foreach (StanceDefinition stance in stances.Values)
        {
            string owner = $"立场 「{stance.Id}」";
            if (string.IsNullOrWhiteSpace(stance.Name)) errors.Add($"{owner} 缺少显示名。");
            ValidateModifiers(stance.Modifiers, owner, buildings, buffs, errors);
        }
    }

    /// <summary>
    /// 校验结局。<para>
    /// 其中"至少有一个结局不依赖玩家表态"这条是把 ROADMAP 阶段 3B 的验收 ③
    /// （存在"什么都没选也有的结局"）写成了构建期规则——否则一个回避所有选择的玩家
    /// 会卡在"主线走完了但没有任何结局成立"的状态里。
    /// </para>
    /// </summary>
    private static void ValidateEndings(
        List<EndingDefinition> endings,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, ChoiceDefinition> choices,
        List<string> errors)
    {
        if (endings.Count == 0) return;

        bool anyUnconditional = false;

        foreach (EndingDefinition ending in endings)
        {
            string owner = $"结局 「{ending.Id}」";

            if (string.IsNullOrWhiteSpace(ending.Name)) errors.Add($"{owner} 缺少显示名。");
            if (string.IsNullOrWhiteSpace(ending.Text)) errors.Add($"{owner} 缺少终局文本。");

            if (ending.Condition is ConstantCondition { Value: false })
                errors.Add($"{owner} 的条件恒为假，永远达不成。");

            if (ending.AchievementId is { Length: > 0 } achievementId && !achievements.ContainsKey(achievementId))
                errors.Add($"{owner} 引用了不存在的成就 「{achievementId}」。");

            ValidateCondition(ending.Condition, owner, buildings, upgrades, achievements, choices, errors);

            // 条件里既没有选择节点、也没有立场权重 → 不依赖玩家表过什么态。
            bool dependsOnStance = ending.Condition.OwnedLeaves().Any(o => o.Kind == OwnedKind.Choice)
                                   || ending.Condition.NumericLeaves().Any(n => n.Metric == NumericMetric.StanceWeight);
            if (!dependsOnStance) anyUnconditional = true;
        }

        if (!anyUnconditional)
            errors.Add("所有结局都依赖玩家的选择或立场——回避表态的玩家会走完主线却没有结局。至少留一个兜底结局。");
    }

    private static void ValidateModifiers(
        IReadOnlyList<Modifier> modifiers,
        string owner,
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, BuffDefinition> buffs,
        List<string> errors)
    {
        foreach (Modifier m in modifiers)
        {
            ModifierTarget target = m.Target;

            // 数值必须有限：NaN 会沿着乘法链一路传播，把整个产量算成 NaN，
            // 而这种错误在运行期极难定位，必须在构建期拦住。
            if (!double.IsFinite(m.Value))
                errors.Add($"{owner} 的修饰符数值不是有限数（{target.Kind} = {m.Value}）。");

            if (m.Scaling is { } scaling)
            {
                if (!double.IsFinite(scaling.PerUnit))
                    errors.Add($"{owner} 的成长增量不是有限数（{target.Kind}，PerUnit = {scaling.PerUnit}）。");
                if (double.IsNaN(scaling.Cap) || scaling.Cap <= 0)
                    errors.Add($"{owner} 的成长上限必须是正数或 +∞（{target.Kind}，Cap = {scaling.Cap}）。");
            }

            switch (target.Kind)
            {
                // 需要具体 id 的目标：id 必须非空且指向真实存在的内容
                case ModifierTargetKind.BuildingCps:
                case ModifierTargetKind.BuildingPrice:
                    if (string.IsNullOrEmpty(target.Id))
                        errors.Add($"{owner} 的修饰符缺少建筑 id（{target.Kind}）。");
                    else if (!buildings.ContainsKey(target.Id))
                        errors.Add($"{owner} 的修饰符引用了不存在的建筑 「{target.Id}」。");
                    break;

                case ModifierTargetKind.BuffDuration:
                    if (target.Id is not null && !buffs.ContainsKey(target.Id))
                        errors.Add($"{owner} 的修饰符引用了不存在的增益 「{target.Id}」。");
                    break;

                // 不接受 id 的目标：带了 id 基本都是手误（例如把建筑 id 写到了全局目标上）
                default:
                    if (target.Id is not null)
                        errors.Add($"{owner} 的目标 {target.Kind} 不接受 id，但传入了 「{target.Id}」。");
                    break;
            }

            if (m.Operation == ModifierOperation.Multiplicative && m.Value < 0)
                errors.Add($"{owner} 的乘法修饰符数值为负（{target.Kind} = {m.Value}）。");

            if (m.Scaling is not { } growing) continue;

            if (growing.Id is null or "")
            {
                switch (growing.Source)
                {
                    case ScalingSource.BuildingCount:
                        errors.Add($"{owner} 的 BuildingCount 成长缺少建筑 id（否则求值恒为 0）。");
                        break;
                    case ScalingSource.TaggedUpgradeCount:
                        errors.Add($"{owner} 的 TaggedUpgradeCount 成长缺少标签 id。");
                        break;
                    case ScalingSource.CustomCounter:
                        errors.Add($"{owner} 的 CustomCounter 成长缺少计数器键。");
                        break;
                }
            }
            else if (growing.Source == ScalingSource.BuildingCount && !buildings.ContainsKey(growing.Id))
            {
                errors.Add($"{owner} 的 BuildingCount 成长引用了不存在的建筑 「{growing.Id}」。");
            }
        }
    }

    /// <summary>
    /// 解锁可达性校验：确认每个建筑 / 升级 / 成就都存在一条从开局状态出发的解锁路径。<para>
    /// 能抓到两类致命内容错误——<b>环路依赖</b>（建筑 B 的解锁要升级 u，而 u 的解锁要 B）
    /// 和 <b>孤儿依赖</b>（依赖了一条同样解不开的链）。这两类错误在运行期的表现都是
    /// "玩家永远卡住"，而在构建期只需要一个不动点迭代就能发现。
    /// </para>
    /// </summary>
    private static void ValidateReachability(
        Dictionary<string, BuildingDefinition> buildings,
        Dictionary<string, UpgradeDefinition> upgrades,
        Dictionary<string, AchievementDefinition> achievements,
        Dictionary<string, LoreEntry> loreEntries,
        Dictionary<string, ChoiceDefinition> choices,
        Dictionary<string, EndingDefinition> endings,
        List<string> errors)
    {
        var conditionByNode = new Dictionary<string, UnlockCondition>(StringComparer.Ordinal);
        foreach (BuildingDefinition b in buildings.Values) conditionByNode[NodeKey("建筑", b.Id)] = b.Unlock;
        foreach (UpgradeDefinition u in upgrades.Values) conditionByNode[NodeKey("升级", u.Id)] = u.Unlock;
        foreach (AchievementDefinition a in achievements.Values) conditionByNode[NodeKey("成就", a.Id)] = a.Unlock;
        // 叙事条目也算节点：一段永远放不出来的剧情和一条解不开的升级一样，都是坏内容。
        foreach (LoreEntry l in loreEntries.Values) conditionByNode[NodeKey("叙事", l.Id)] = l.Reveal;
        // 选择同理：条件永远不成立的选择，等于一段永远不会发生的对话。
        foreach (ChoiceDefinition c in choices.Values) conditionByNode[NodeKey("选择", c.Id)] = c.Trigger;
        // 结局也一样：达不到的结局就是没写完的结局。
        foreach (EndingDefinition e in endings.Values) conditionByNode[NodeKey("结局", e.Id)] = e.Condition;

        // 不动点：从"只依赖进度型指标"的节点出发反复放宽，直到不再有新节点可达。
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach ((string node, UnlockCondition condition) in conditionByNode)
            {
                if (reachable.Contains(node)) continue;
                if (!IsSatisfiable(condition, reachable)) continue;
                reachable.Add(node);
                changed = true;
            }
        }

        foreach ((string node, UnlockCondition condition) in conditionByNode)
        {
            if (reachable.Contains(node)) continue;

            List<string> blocking = [.. ReferencedNodes(condition).Where(r => !reachable.Contains(r)).Distinct(StringComparer.Ordinal)];
            string reason = blocking.Count == 0
                ? "它的条件恒不成立"
                : $"它依赖 {string.Join("、", blocking)}，而这些内容同样解不开";

            errors.Add($"{node} 永远无法解锁：{reason}。");
        }
    }

    /// <summary>构造带类别前缀的节点键（不同类别的 id 可能重名，必须隔离）。</summary>
    private static string NodeKey(string kind, string id) => $"{kind}「{id}」";

    /// <summary>在"已可达集合"的假设下，判断条件是否可能成立。</summary>
    private static bool IsSatisfiable(UnlockCondition condition, HashSet<string> reachable) => condition switch
    {
        ConstantCondition constant => constant.Value,
        AllCondition all => all.Conditions.All(c => IsSatisfiable(c, reachable)),
        AnyCondition any => any.Conditions.Any(c => IsSatisfiable(c, reachable)),
        // "未拥有某物"默认就是成立的，因此取反条件不构成解锁障碍。
        NotCondition => true,
        OwnedCondition owned => reachable.Contains(NodeKey(
            owned.Kind switch
            {
                OwnedKind.Upgrade => "升级",
                OwnedKind.Achievement => "成就",
                _ => "选择",
            },
            owned.Id)),
        // 只有"指定建筑的数量"会被别的内容卡住；其余指标都会随游戏进程自然增长。
        NumericCondition numeric when numeric.Metric == NumericMetric.BuildingCount
            => reachable.Contains(NodeKey("建筑", numeric.Id ?? string.Empty)),
        NumericCondition => true,
        // 自定义谓词无法静态分析：保守判定为可达，宁可漏报也不误报。
        _ => true,
    };

    /// <summary>收集条件里引用的全部内容节点（仅用于生成人类可读的报错原因）。</summary>
    private static IEnumerable<string> ReferencedNodes(UnlockCondition condition)
    {
        foreach (NumericCondition n in condition.NumericLeaves())
            if (n.Metric == NumericMetric.BuildingCount && !string.IsNullOrEmpty(n.Id))
                yield return NodeKey("建筑", n.Id);

        foreach (OwnedCondition o in condition.OwnedLeaves())
        {
            yield return NodeKey(
                o.Kind switch
                {
                    OwnedKind.Upgrade => "升级",
                    OwnedKind.Achievement => "成就",
                    _ => "选择",
                },
                o.Id);
        }
    }
}
