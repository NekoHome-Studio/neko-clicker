namespace NekoClicker.Core.Content;

/// <summary>
/// 一座"建筑"（自动生产单位）。Cookie Clicker 的 Cursor / Grandma / Farm ... 对应这里。<para>
/// 价格按 <see cref="PriceGrowth"/> 指数增长（原版为 1.15），产量与持有数量线性相关，
/// 所有倍率来自 <see cref="Modifier"/>，因此新增建筑不需要改任何引擎代码。
/// </para>
/// </summary>
public sealed record BuildingDefinition
{
    /// <summary>唯一 id（存档键，改动会导致旧存档丢失该建筑）。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>说明文本。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>图标（emoji 或 ANSI 片段）。</summary>
    public string Icon { get; init; } = "🐾";

    /// <summary>第 0 个的价格。</summary>
    public double BasePrice { get; init; }

    /// <summary>单个建筑的基础每秒产量。</summary>
    public double BaseCps { get; init; }

    /// <summary>每购买一个，价格的乘数（原版 1.15）。</summary>
    public double PriceGrowth { get; init; } = 1.15;

    /// <summary>解锁条件。</summary>
    public UnlockCondition Unlock { get; init; } = UnlockCondition.Always;

    /// <summary>分组（UI 分页用）。</summary>
    public string Category { get; init; } = "core";

    /// <summary>标签，供 <see cref="NekoClicker.Core.IGameMetrics.TaggedBuildingCount"/> 之类的成长使用。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>未解锁时是否在 UI 中隐藏（否则显示为灰色 + 解锁提示）。</summary>
    public bool HiddenUntilUnlocked { get; init; } = true;

    /// <summary>出售返还比例（占当前单价的比例）；<c>null</c> 表示沿用 <see cref="GameBalance.DefaultSellRefundRate"/>。</summary>
    public double? SellRefundRate { get; init; }
}

/// <summary>升级的计价货币。</summary>
public enum UpgradeCurrency
{
    /// <summary>普通货币。</summary>
    Cookies,

    /// <summary>转生货币（猫薄荷）。</summary>
    PrestigeChips,
}

/// <summary>升级在转生后是否保留。</summary>
public enum UpgradePersistence
{
    /// <summary>转生时清空（普通升级）。</summary>
    Run,

    /// <summary>转生后保留（天堂升级）。</summary>
    Permanent,
}

/// <summary>升级：一次性（或有限次数）购买，提供一组修饰符。</summary>
public sealed record UpgradeDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>说明文本。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "⬆";

    /// <summary>价格。</summary>
    public double Price { get; init; }

    /// <summary>计价货币。</summary>
    public UpgradeCurrency Currency { get; init; } = UpgradeCurrency.Cookies;

    /// <summary>转生后是否保留。</summary>
    public UpgradePersistence Persistence { get; init; } = UpgradePersistence.Run;

    /// <summary>可购买次数上限（1 = 唯一升级）。</summary>
    public int MaxPurchases { get; init; } = 1;

    /// <summary>重复购买时的价格增长（1 = 每次同价）。仅当 <see cref="MaxPurchases"/> &gt; 1 时有意义。</summary>
    public double PriceGrowth { get; init; } = 1.0;

    /// <summary>解锁条件。</summary>
    public UnlockCondition Unlock { get; init; } = UnlockCondition.Always;

    /// <summary>提供的修饰符。</summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];

    /// <summary>标签（如 "kitten"、"grandma"），供成长曲线引用。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>分组（UI 用，例如 "building:catnap"）。</summary>
    public string Category { get; init; } = "general";

    /// <summary>层级，仅用于排序/展示。</summary>
    public int Tier { get; init; }

    /// <summary>未解锁时是否隐藏。</summary>
    public bool HiddenUntilUnlocked { get; init; } = true;
}

/// <summary>成就：条件达成后永久激活其修饰符，且通常跨转生保留。</summary>
public sealed record AchievementDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>说明文本。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "🏆";

    /// <summary>解锁条件。</summary>
    public UnlockCondition Unlock { get; init; } = UnlockCondition.Never;

    /// <summary>解锁后提供的修饰符（原版的"牛奶"就靠这个）。</summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];

    /// <summary>分组。</summary>
    public string Category { get; init; } = "general";

    /// <summary>未解锁时是否隐藏（隐藏成就只显示"???"）。</summary>
    public bool Hidden { get; init; }

    /// <summary>是否为"影子成就"：达成后自动解锁的连锁成就。</summary>
    public int Tier { get; init; }
}

/// <summary>增益的叠加规则。</summary>
public enum BuffStackMode
{
    /// <summary>重复施加时刷新剩余时间，取较长者。</summary>
    Refresh,

    /// <summary>重复施加时累加剩余时间。</summary>
    Extend,

    /// <summary>重复施加时叠加层数并重置时间。</summary>
    Stack,
}

/// <summary>限时增益（正面）或减益（负面）。</summary>
public sealed record BuffDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>说明文本。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "✨";

    /// <summary>默认持续时间（秒）。</summary>
    public double Duration { get; init; } = 30;

    /// <summary>可叠加的最大层数。</summary>
    public int MaxStacks { get; init; } = 1;

    /// <summary>叠加规则。</summary>
    public BuffStackMode StackMode { get; init; } = BuffStackMode.Refresh;

    /// <summary>提供的修饰符（层数会按乘数放大 <see cref="ModifierOperation.Multiplicative"/> 之外的部分）。</summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];

    /// <summary>是否为负面效果（UI 用红色显示）。</summary>
    public bool IsDebuff { get; init; }

    /// <summary>是否可被"驱散"。</summary>
    public bool Dispellable { get; init; } = true;
}

/// <summary>
/// 金猫结果表的一项。<para>
/// 字段刻意做成"配方"而非枚举：原版的 Lucky（银行 15% 与 CPS 900 秒取小，再加 13 秒产量）、
/// Frenzy、Click Frenzy、Ruin、Bloodlust 都能用同一组字段表达，新增结果不需要改引擎。
/// </para>
/// </summary>
public sealed record GoldenCookieOutcome
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>说明文本（支持 {amount}、{duration} 占位符）。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "🌟";

    /// <summary>抽取权重。</summary>
    public double Weight { get; init; } = 1;

    /// <summary>固定获得量。</summary>
    public double CookiesFlat { get; init; }

    /// <summary>按当前存量的比例获得（原版 Lucky 的 15%）。</summary>
    public double CookiesFromBankFraction { get; init; }

    /// <summary>存量比例收益的上限，以"CPS 秒数"表示（原版 Lucky 的 900）。</summary>
    public double CookiesFromBankFractionCapSecondsOfCps { get; init; } = double.PositiveInfinity;

    /// <summary>按"若干秒产量"获得（原版 Lucky 额外加 13 秒）。</summary>
    public double CookiesFromCpsSeconds { get; init; }

    /// <summary>按存量比例扣除（原版 Ruin 的 5%）。</summary>
    public double StealBankFraction { get; init; }

    /// <summary>附带增益 id。</summary>
    public string? BuffId { get; init; }

    /// <summary>附带增益时长。</summary>
    public double BuffSeconds { get; init; }

    /// <summary>连锁增益 id（原版 Chain Cookie）。</summary>
    public string? SecondaryBuffId { get; init; }

    /// <summary>连锁增益时长。</summary>
    public double SecondaryBuffSeconds { get; init; }

    /// <summary>是否为稀有结果（UI 高亮）。</summary>
    public bool IsRare { get; init; }
}
