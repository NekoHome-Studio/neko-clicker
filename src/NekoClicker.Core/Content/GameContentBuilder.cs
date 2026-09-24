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
        var errors = new List<string>();

        Index(_buildings, b => b.Id, buildingById, "建筑", errors);
        Index(_upgrades, u => u.Id, upgradeById, "升级", errors);
        Index(_achievements, a => a.Id, achievementById, "成就", errors);
        Index(_buffs, b => b.Id, buffById, "增益", errors);

        foreach (BuildingDefinition b in _buildings)
        {
            if (b.BasePrice < 0) errors.Add($"建筑 「{b.Id}」 的 BasePrice 不能为负。");
            if (b.BaseCps < 0) errors.Add($"建筑 「{b.Id}」 的 BaseCps 不能为负。");
            if (b.PriceGrowth <= 1) errors.Add($"建筑 「{b.Id}」 的 PriceGrowth 必须大于 1（否则价格不增长）。");
            if (b.SellRefundRate is < 0 or > 1) errors.Add($"建筑 「{b.Id}」 的 SellRefundRate 必须在 [0,1] 内。");
            ValidateCondition(b.Unlock, $"建筑 「{b.Id}」", buildingById, upgradeById, achievementById, errors);
        }

        foreach (UpgradeDefinition u in _upgrades)
        {
            if (u.Price < 0) errors.Add($"升级 「{u.Id}」 的 Price 不能为负。");
            if (u.MaxPurchases < 1) errors.Add($"升级 「{u.Id}」 的 MaxPurchases 至少为 1。");
            if (u.Persistence == UpgradePersistence.Permanent && u.Currency == UpgradeCurrency.Cookies)
                errors.Add($"升级 「{u.Id}」 是 Permanent 但用普通货币计价；永久升级通常应以转生货币购买（如非本意请改为 Run）。");
            ValidateCondition(u.Unlock, $"升级 「{u.Id}」", buildingById, upgradeById, achievementById, errors);
            ValidateModifiers(u.Modifiers, $"升级 「{u.Id}」", buffById, errors);
        }

        foreach (AchievementDefinition a in _achievements)
        {
            if (a.Unlock is ConstantCondition { Value: false })
                errors.Add($"成就 「{a.Id}」 的条件恒为假，永远无法解锁。");
            ValidateCondition(a.Unlock, $"成就 「{a.Id}」", buildingById, upgradeById, achievementById, errors);
            ValidateModifiers(a.Modifiers, $"成就 「{a.Id}」", buffById, errors);
        }

        foreach (BuffDefinition b in _buffs)
        {
            if (b.Duration <= 0) errors.Add($"增益 「{b.Id}」 的 Duration 必须为正。");
            if (b.MaxStacks < 1) errors.Add($"增益 「{b.Id}」 的 MaxStacks 至少为 1。");
            ValidateModifiers(b.Modifiers, $"增益 「{b.Id}」", buffById, errors);
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
        };
    }

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
        }

        foreach (OwnedCondition o in condition.OwnedLeaves())
        {
            bool ok = o.Kind == OwnedKind.Upgrade ? upgrades.ContainsKey(o.Id) : achievements.ContainsKey(o.Id);
            if (!ok) errors.Add($"{owner} 的解锁条件引用了不存在的 {o.Kind} 「{o.Id}」。");
        }
    }

    private static void ValidateModifiers(
        IReadOnlyList<Modifier> modifiers,
        string owner,
        Dictionary<string, BuffDefinition> buffs,
        List<string> errors)
    {
        foreach (Modifier m in modifiers)
        {
            if (m.Target.Kind is ModifierTargetKind.BuildingCps or ModifierTargetKind.BuildingPrice)
            {
                if (string.IsNullOrEmpty(m.Target.Id)) errors.Add($"{owner} 的修饰符缺少建筑 id（{m.Target.Kind}）。");
            }
            if (m.Operation == ModifierOperation.Multiplicative && m.Value < 0)
                errors.Add($"{owner} 的乘法修饰符数值为负（{m.Target.Kind} = {m.Value}）。");
            if (m.Scaling is { Source: ScalingSource.TaggedUpgradeCount, Id: null or "" })
                errors.Add($"{owner} 的 TaggedUpgradeCount 成长缺少标签 id。");
            if (m.Scaling is { Source: ScalingSource.CustomCounter, Id: null or "" })
                errors.Add($"{owner} 的 CustomCounter 成长缺少计数器键。");
        }
    }
}
