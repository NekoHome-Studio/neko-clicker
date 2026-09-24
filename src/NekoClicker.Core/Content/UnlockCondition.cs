using NekoClicker.Core.Numbers;

namespace NekoClicker.Core.Content;

/// <summary>可比较的数值型指标，供 <see cref="NumericCondition"/> 使用。</summary>
public enum NumericMetric
{
    /// <summary>当前存量。</summary>
    CurrentCookies,

    /// <summary>本轮累计赚取。</summary>
    CookiesEarnedThisRun,

    /// <summary>历史累计赚取。</summary>
    CookiesEarnedAllTime,

    /// <summary>每秒产量。</summary>
    Cps,

    /// <summary>累计点击次数。</summary>
    Clicks,

    /// <summary>指定建筑的数量。</summary>
    BuildingCount,

    /// <summary>所有建筑总数。</summary>
    TotalBuildings,

    /// <summary>转生等级。</summary>
    PrestigeLevel,

    /// <summary>转生货币存量。</summary>
    PrestigeChips,

    /// <summary>已解锁成就数。</summary>
    AchievementCount,

    /// <summary>累计点击金猫次数。</summary>
    GoldenCookiesClicked,

    /// <summary>已购升级种类数。</summary>
    PurchasedUpgrades,

    /// <summary>自定义计数器（<see cref="NumericCondition.Id"/> = 计数器键）。</summary>
    Counter,

    /// <summary>带指定标签的升级已购总次数（<see cref="NumericCondition.Id"/> = 标签）。</summary>
    TaggedUpgrades,

    /// <summary>累计游玩秒数。</summary>
    PlayTimeSeconds,
}

/// <summary>持有物类型。</summary>
public enum OwnedKind
{
    /// <summary>升级。</summary>
    Upgrade,

    /// <summary>成就。</summary>
    Achievement,
}

/// <summary>
/// 解锁条件树。<para>
/// 增量游戏的内容量最终会膨胀到几百个升级/成就，每个都有自己的解锁门槛。把这套门槛
/// 做成可组合的条件树（而不是散落在各处的 <c>if</c>），带来三个好处：
/// 一是内容纯数据、可校验；二是 UI 能反向生成"还差 3 只猫"的提示（<see cref="TryGetProgress"/>）；
/// 三是新增条件类型不需要改引擎。
/// </para>
/// </summary>
public abstract record UnlockCondition
{
    /// <summary>恒真：内容一开始就可见。</summary>
    public static readonly UnlockCondition Always = new ConstantCondition(true);

    /// <summary>恒假：用于占位或活动内容。</summary>
    public static readonly UnlockCondition Never = new ConstantCondition(false);

    /// <summary>判断条件是否满足。</summary>
    public abstract bool IsMet(IGameMetrics metrics, GameContent content);

    /// <summary>人类可读描述，例如"拥有 10 个 打盹的猫"。</summary>
    public virtual string Describe(GameContent? content = null) => "满足条件";

    /// <summary>查询进度；返回 <c>false</c> 表示该条件无法量化。</summary>
    public virtual bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        current = 0;
        target = 0;
        return false;
    }

    /// <summary>展开条件树中的所有叶子节点（含自身）。</summary>
    public virtual IEnumerable<UnlockCondition> Flatten()
    {
        yield return this;
    }

    /// <summary>提取所有数值型叶子（用于"下一里程碑"提示与内容校验）。</summary>
    public IEnumerable<NumericCondition> NumericLeaves() => Flatten().OfType<NumericCondition>();

    /// <summary>提取所有持有型叶子。</summary>
    public IEnumerable<OwnedCondition> OwnedLeaves() => Flatten().OfType<OwnedCondition>();

    // ---------- 组合子 ----------

    /// <summary>全部满足。</summary>
    public static UnlockCondition All(params UnlockCondition[] conditions)
        => conditions.Length == 0 ? Always : conditions.Length == 1 ? conditions[0] : new AllCondition(conditions);

    /// <summary>任一满足。</summary>
    public static UnlockCondition Any(params UnlockCondition[] conditions)
        => conditions.Length == 0 ? Never : conditions.Length == 1 ? conditions[0] : new AnyCondition(conditions);

    /// <summary>取反。</summary>
    public static UnlockCondition Not(UnlockCondition condition) => new NotCondition(condition);

    // ---------- 常用条件快捷构造 ----------

    /// <summary>当前存量 ≥ amount。</summary>
    public static UnlockCondition CookiesAtLeast(double amount) => new NumericCondition(NumericMetric.CurrentCookies, amount);

    /// <summary>本轮累计赚取 ≥ amount。</summary>
    public static UnlockCondition EarnedThisRunAtLeast(double amount) => new NumericCondition(NumericMetric.CookiesEarnedThisRun, amount);

    /// <summary>历史累计赚取 ≥ amount。</summary>
    public static UnlockCondition EarnedAllTimeAtLeast(double amount) => new NumericCondition(NumericMetric.CookiesEarnedAllTime, amount);

    /// <summary>每秒产量 ≥ amount。</summary>
    public static UnlockCondition CpsAtLeast(double amount) => new NumericCondition(NumericMetric.Cps, amount);

    /// <summary>累计点击 ≥ count 次。</summary>
    public static UnlockCondition ClicksAtLeast(double count) => new NumericCondition(NumericMetric.Clicks, count);

    /// <summary>某建筑数量 ≥ count。</summary>
    public static UnlockCondition BuildingsAtLeast(string buildingId, double count) => new NumericCondition(NumericMetric.BuildingCount, count, buildingId);

    /// <summary>建筑总数 ≥ count。</summary>
    public static UnlockCondition TotalBuildingsAtLeast(double count) => new NumericCondition(NumericMetric.TotalBuildings, count);

    /// <summary>已购买某升级。</summary>
    public static UnlockCondition UpgradeOwned(string upgradeId) => new OwnedCondition(OwnedKind.Upgrade, upgradeId);

    /// <summary>已解锁某成就。</summary>
    public static UnlockCondition AchievementUnlocked(string achievementId) => new OwnedCondition(OwnedKind.Achievement, achievementId);

    /// <summary>转生等级 ≥ level。</summary>
    public static UnlockCondition PrestigeLevelAtLeast(double level) => new NumericCondition(NumericMetric.PrestigeLevel, level);

    /// <summary>转生货币 ≥ amount。</summary>
    public static UnlockCondition PrestigeChipsAtLeast(double amount) => new NumericCondition(NumericMetric.PrestigeChips, amount);

    /// <summary>成就数 ≥ count。</summary>
    public static UnlockCondition AchievementsAtLeast(double count) => new NumericCondition(NumericMetric.AchievementCount, count);

    /// <summary>累计点击金猫 ≥ count 次。</summary>
    public static UnlockCondition GoldenCookiesAtLeast(double count) => new NumericCondition(NumericMetric.GoldenCookiesClicked, count);

    /// <summary>已购升级数 ≥ count。</summary>
    public static UnlockCondition UpgradesAtLeast(double count) => new NumericCondition(NumericMetric.PurchasedUpgrades, count);

    /// <summary>自定义计数器 ≥ target（用于第二资源，如"幸福感 ≥ 500"）。</summary>
    /// <param name="counterKey">计数器键。</param>
    /// <param name="target">阈值。</param>
    public static UnlockCondition Counter(string counterKey, double target)
        => new NumericCondition(NumericMetric.Counter, target, counterKey);

    /// <summary>带指定标签的升级已购次数 ≥ count。</summary>
    /// <param name="tag">升级标签。</param>
    /// <param name="count">阈值。</param>
    public static UnlockCondition TaggedUpgradesAtLeast(string tag, double count)
        => new NumericCondition(NumericMetric.TaggedUpgrades, count, tag);

    /// <summary>游玩时长 ≥ seconds 秒。</summary>
    public static UnlockCondition PlayTimeAtLeast(double seconds) => new NumericCondition(NumericMetric.PlayTimeSeconds, seconds);

    /// <summary>自定义条件（用于引擎无关的派生逻辑）。</summary>
    public static UnlockCondition Custom(string description, Func<IGameMetrics, bool> predicate)
        => new CustomCondition(description, predicate);
}

/// <summary>恒真/恒假条件。</summary>
/// <param name="Value">条件值。</param>
public sealed record ConstantCondition(bool Value) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content) => Value;

    /// <inheritdoc />
    public override string Describe(GameContent? content = null) => Value ? "无条件" : "永不解锁";

    /// <inheritdoc />
    public override bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        current = Value ? 1 : 0;
        target = 1;
        return true;
    }
}

/// <summary>逻辑与。</summary>
/// <param name="Conditions">子条件。</param>
public sealed record AllCondition(IReadOnlyList<UnlockCondition> Conditions) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content)
    {
        for (int i = 0; i < Conditions.Count; i++)
            if (!Conditions[i].IsMet(metrics, content)) return false;
        return true;
    }

    /// <inheritdoc />
    public override string Describe(GameContent? content = null)
        => string.Join("，且 ", Conditions.Select(c => c.Describe(content)));

    /// <inheritdoc />
    public override bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        double minRatio = double.PositiveInfinity;
        current = 0;
        target = 0;
        bool any = false;
        foreach (UnlockCondition c in Conditions)
        {
            if (!c.TryGetProgress(metrics, out double cur, out double tgt) || tgt <= 0) continue;
            any = true;
            double ratio = Math.Clamp(cur / tgt, 0, 1);
            if (ratio < minRatio)
            {
                minRatio = ratio;
                current = cur;
                target = tgt;
            }
        }
        return any;
    }

    /// <inheritdoc />
    public override IEnumerable<UnlockCondition> Flatten()
        => Conditions.SelectMany(c => c.Flatten());
}

/// <summary>逻辑或。</summary>
/// <param name="Conditions">子条件。</param>
public sealed record AnyCondition(IReadOnlyList<UnlockCondition> Conditions) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content)
    {
        for (int i = 0; i < Conditions.Count; i++)
            if (Conditions[i].IsMet(metrics, content)) return true;
        return false;
    }

    /// <inheritdoc />
    public override string Describe(GameContent? content = null)
        => string.Join("，或 ", Conditions.Select(c => c.Describe(content)));

    /// <inheritdoc />
    public override bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        double best = -1;
        current = 0;
        target = 0;
        foreach (UnlockCondition c in Conditions)
        {
            if (!c.TryGetProgress(metrics, out double cur, out double tgt) || tgt <= 0) continue;
            double ratio = Math.Clamp(cur / tgt, 0, 1);
            if (ratio > best)
            {
                best = ratio;
                current = cur;
                target = tgt;
            }
        }
        return best >= 0;
    }

    /// <inheritdoc />
    public override IEnumerable<UnlockCondition> Flatten()
        => Conditions.SelectMany(c => c.Flatten());
}

/// <summary>逻辑非。</summary>
/// <param name="Condition">被取反的条件。</param>
public sealed record NotCondition(UnlockCondition Condition) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content) => !Condition.IsMet(metrics, content);

    /// <inheritdoc />
    public override string Describe(GameContent? content = null) => $"未满足：{Condition.Describe(content)}";

    /// <inheritdoc />
    public override IEnumerable<UnlockCondition> Flatten() => Condition.Flatten();
}

/// <summary>数值阈值条件。</summary>
/// <param name="Metric">比较的指标。</param>
/// <param name="Target">阈值。</param>
/// <param name="Id">指标所需的 id（如建筑 id）。</param>
public sealed record NumericCondition(NumericMetric Metric, double Target, string? Id = null) : UnlockCondition
{
    /// <summary>读取当前值。</summary>
    public double Read(IGameMetrics metrics) => Metric switch
    {
        NumericMetric.CurrentCookies => metrics.Cookies,
        NumericMetric.CookiesEarnedThisRun => metrics.CookiesEarnedThisRun,
        NumericMetric.CookiesEarnedAllTime => metrics.CookiesEarnedAllTime,
        NumericMetric.Cps => metrics.CookiesPerSecond,
        NumericMetric.Clicks => metrics.TotalClicks,
        NumericMetric.BuildingCount => metrics.BuildingCount(Id ?? string.Empty),
        NumericMetric.TotalBuildings => metrics.TotalBuildings,
        NumericMetric.PrestigeLevel => metrics.PrestigeLevel,
        NumericMetric.PrestigeChips => metrics.PrestigeChips,
        NumericMetric.AchievementCount => metrics.AchievementCount,
        NumericMetric.GoldenCookiesClicked => metrics.GoldenCookiesClicked,
        NumericMetric.PurchasedUpgrades => metrics.PurchasedUpgradeCount,
        NumericMetric.Counter => metrics.GetCounter(Id ?? string.Empty),
        NumericMetric.TaggedUpgrades => metrics.TaggedUpgradeCount(Id ?? string.Empty),
        NumericMetric.PlayTimeSeconds => metrics.PlayTimeSeconds,
        _ => 0,
    };

    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content) => Read(metrics) >= Target;

    /// <inheritdoc />
    public override bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        current = Read(metrics);
        target = Target;
        return true;
    }

    /// <inheritdoc />
    public override string Describe(GameContent? content = null)
    {
        string currency = content?.CurrencyName ?? "货币";
        string chips = content?.PrestigeCurrencyName ?? "转生货币";
        string amount = NumFormat.Format(Target, NumberStyle.Plain);
        string building = Id is not null && content is not null && content.BuildingById.TryGetValue(Id, out BuildingDefinition? b)
            ? b.Name
            : Id ?? "?";

        return Metric switch
        {
            NumericMetric.CurrentCookies => $"持有 {amount} {currency}",
            NumericMetric.CookiesEarnedThisRun => $"本轮累计赚取 {amount} {currency}",
            NumericMetric.CookiesEarnedAllTime => $"历史累计赚取 {amount} {currency}",
            NumericMetric.Cps => $"每秒产量达到 {NumFormat.Format(Target, NumberStyle.Short)}",
            NumericMetric.Clicks => $"点击 {amount} 次",
            NumericMetric.BuildingCount => $"拥有 {amount} 个「{building}」",
            NumericMetric.TotalBuildings => $"拥有 {amount} 座建筑",
            NumericMetric.PrestigeLevel => $"转生等级达到 {amount}",
            NumericMetric.PrestigeChips => $"持有 {amount} {chips}",
            NumericMetric.AchievementCount => $"解锁 {amount} 个成就",
            NumericMetric.GoldenCookiesClicked => $"点击 {amount} 次金猫",
            NumericMetric.PurchasedUpgrades => $"购买 {amount} 个升级",
            NumericMetric.Counter => $"「{Id}」达到 {amount}",
            NumericMetric.TaggedUpgrades => $"购买 {amount} 个「{Id}」类升级",
            NumericMetric.PlayTimeSeconds => $"游玩时长达到 {NumFormat.Duration(Target)}",
            _ => $"达成 {amount}",
        };
    }
}

/// <summary>持有型条件（已购升级 / 已解锁成就）。</summary>
/// <param name="Kind">持有物类型。</param>
/// <param name="Id">目标 id。</param>
public sealed record OwnedCondition(OwnedKind Kind, string Id) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content) => Kind switch
    {
        OwnedKind.Upgrade => metrics.HasUpgrade(Id),
        OwnedKind.Achievement => metrics.HasAchievement(Id),
        _ => false,
    };

    /// <inheritdoc />
    public override bool TryGetProgress(IGameMetrics metrics, out double current, out double target)
    {
        current = IsMetValue(metrics) ? 1 : 0;
        target = 1;
        return true;
    }

    /// <inheritdoc />
    public override string Describe(GameContent? content = null)
    {
        if (Kind == OwnedKind.Upgrade)
        {
            string name = content is not null && content.UpgradeById.TryGetValue(Id, out UpgradeDefinition? u) ? u.Name : Id;
            return $"已购买「{name}」";
        }
        string achievement = content is not null && content.AchievementById.TryGetValue(Id, out AchievementDefinition? a) ? a.Name : Id;
        return $"已解锁成就「{achievement}」";
    }

    private bool IsMetValue(IGameMetrics metrics) => Kind switch
    {
        OwnedKind.Upgrade => metrics.HasUpgrade(Id),
        OwnedKind.Achievement => metrics.HasAchievement(Id),
        _ => false,
    };
}

/// <summary>自定义谓词条件。</summary>
/// <param name="Description">描述文本。</param>
/// <param name="Predicate">判定逻辑。</param>
public sealed record CustomCondition(string Description, Func<IGameMetrics, bool> Predicate) : UnlockCondition
{
    /// <inheritdoc />
    public override bool IsMet(IGameMetrics metrics, GameContent content) => Predicate(metrics);

    /// <inheritdoc />
    public override string Describe(GameContent? content = null) => Description;
}
