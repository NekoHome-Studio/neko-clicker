using NekoClicker.Core.Numbers;

namespace NekoClicker.Core.Content;

/// <summary>修饰符的叠加方式。</summary>
public enum ModifierOperation
{
    /// <summary>加法：<c>v = base + Σ value</c>。用于"+1 点击收益"。</summary>
    Flat,

    /// <summary>加法百分比：<c>v = base × (1 + Σ value)</c>。用于"+50% 产量"。</summary>
    AdditivePercent,

    /// <summary>乘法：<c>v = base × Π value</c>。用于"产量翻倍"（value = 2）。</summary>
    Multiplicative,

    /// <summary>乘方：<c>v = base ^ Π value</c>。用于指数级稀有升级。</summary>
    Power,
}

/// <summary>成长来源：让一个修饰符的数值随某项统计动态变化。</summary>
public enum ScalingSource
{
    /// <summary>不成长。</summary>
    None,

    /// <summary>某个建筑的持有数量（<see cref="Scaling.Id"/> = 建筑 id）。</summary>
    BuildingCount,

    /// <summary>所有建筑总数。</summary>
    TotalBuildings,

    /// <summary>已解锁成就数。Cookie Clicker 的"牛奶"就靠它。</summary>
    AchievementCount,

    /// <summary>转生等级。</summary>
    PrestigeLevel,

    /// <summary>累计点击数。</summary>
    TotalClicks,

    /// <summary>本轮累计赚取量。</summary>
    CookiesEarnedThisRun,

    /// <summary>历史累计赚取量。</summary>
    CookiesEarnedAllTime,

    /// <summary>已购升级种类数。</summary>
    PurchasedUpgrades,

    /// <summary>带指定标签的升级已购总数（<see cref="Scaling.Id"/> = 标签）。</summary>
    TaggedUpgradeCount,

    /// <summary>累计点击金猫次数。</summary>
    GoldenCookiesClicked,

    /// <summary>游玩小时数。</summary>
    PlayTimeHours,

    /// <summary>自定义计数器（<see cref="Scaling.Id"/> = 计数器键）。</summary>
    CustomCounter,
}

/// <summary>
/// 成长描述：把修饰符的基础值变成 <c>基础值 + 每单位增量 × 当前成长量</c>。<para>
/// 例："每个成就让所有产量 +1%" = <c>PerUnit = 0.01, Source = AchievementCount</c>。
/// </para>
/// </summary>
/// <param name="Source">成长来源。</param>
/// <param name="PerUnit">每单位来源带来的增量。</param>
/// <param name="Cap">成长量的上限（超过部分不再计入）。</param>
/// <param name="Id">来源所需的 id（建筑 id / 标签 / 计数器键）。</param>
public sealed record Scaling(
    ScalingSource Source,
    double PerUnit,
    double Cap = double.PositiveInfinity,
    string? Id = null)
{
    /// <summary>当前成长量（已按 <see cref="Cap"/> 截断）。</summary>
    public double Evaluate(IGameMetrics metrics)
    {
        double raw = Source switch
        {
            ScalingSource.None => 0,
            ScalingSource.BuildingCount => metrics.BuildingCount(Id ?? string.Empty),
            ScalingSource.TotalBuildings => metrics.TotalBuildings,
            ScalingSource.AchievementCount => metrics.AchievementCount,
            ScalingSource.PrestigeLevel => metrics.PrestigeLevel,
            ScalingSource.TotalClicks => metrics.TotalClicks,
            ScalingSource.CookiesEarnedThisRun => metrics.CookiesEarnedThisRun,
            ScalingSource.CookiesEarnedAllTime => metrics.CookiesEarnedAllTime,
            ScalingSource.PurchasedUpgrades => metrics.PurchasedUpgradeCount,
            ScalingSource.TaggedUpgradeCount => metrics.TaggedUpgradeCount(Id ?? string.Empty),
            ScalingSource.GoldenCookiesClicked => metrics.GoldenCookiesClicked,
            ScalingSource.PlayTimeHours => metrics.PlayTimeSeconds / 3600.0,
            ScalingSource.CustomCounter => metrics.GetCounter(Id ?? string.Empty),
            _ => 0,
        };
        return Math.Min(raw, Cap);
    }

    /// <summary>把基础值解析为当前实际值。</summary>
    public double Apply(double baseValue, IGameMetrics metrics) => baseValue + (PerUnit * Evaluate(metrics));

    /// <summary>人类可读的成长说明。</summary>
    public string Describe()
    {
        string unit = Source switch
        {
            ScalingSource.BuildingCount => $"每个「{Id}」",
            ScalingSource.TotalBuildings => "每座建筑",
            ScalingSource.AchievementCount => "每个成就",
            ScalingSource.PrestigeLevel => "每级转生",
            ScalingSource.TotalClicks => "每次点击",
            ScalingSource.CookiesEarnedThisRun => "本轮每赚取 1",
            ScalingSource.CookiesEarnedAllTime => "历史每赚取 1",
            ScalingSource.PurchasedUpgrades => "每个升级",
            ScalingSource.TaggedUpgradeCount => $"每个「{Id}」升级",
            ScalingSource.GoldenCookiesClicked => "每次金猫",
            ScalingSource.PlayTimeHours => "每小时游玩",
            ScalingSource.CustomCounter => $"每点「{Id}」",
            _ => "每单位",
        };

        string per = Source is ScalingSource.CookiesEarnedThisRun or ScalingSource.CookiesEarnedAllTime
            ? NumFormat.Format(PerUnit, NumberStyle.Short)
            : NumFormat.Percent(PerUnit, 2);

        string suffix = double.IsPositiveInfinity(Cap) ? string.Empty : $"（上限 {NumFormat.Format(Cap, NumberStyle.Plain)}）";
        return $"{unit} +{per}{suffix}";
    }
}

/// <summary>
/// 一条修饰符：作用目标 + 叠加方式 + 数值。<para>
/// 内容作者只需要声明"什么对什么做了什么"，引擎负责把它们合成最终产量。
/// </para>
/// </summary>
/// <param name="Target">作用目标。</param>
/// <param name="Operation">叠加方式。</param>
/// <param name="Value">数值。</param>
/// <param name="Scaling">可选的成长曲线。</param>
public sealed record Modifier(
    ModifierTarget Target,
    ModifierOperation Operation,
    double Value,
    Scaling? Scaling = null)
{
    /// <summary>解析出当前实际数值（考虑成长）。</summary>
    public double Resolve(IGameMetrics metrics) => Scaling is null ? Value : Scaling.Apply(Value, metrics);

    /// <summary>人类可读说明，例如 <c>所有建筑产量 ×2</c>。</summary>
    public string Describe(GameContent? content = null)
    {
        string target = Target.Describe(content);
        string body = Operation switch
        {
            ModifierOperation.Flat => $"{target} +{NumFormat.Format(Value, NumberStyle.Plain)}",
            ModifierOperation.AdditivePercent => $"{target} +{NumFormat.Percent(Value, 1)}",
            ModifierOperation.Multiplicative => $"{target} ×{NumFormat.Format(Value, NumberStyle.Plain)}",
            ModifierOperation.Power => $"{target} 的 {Value} 次方",
            _ => target,
        };
        return Scaling is null ? body : $"{body}；{Scaling.Describe()}";
    }

    // ---- 内容作者用的快捷构造 ----

    /// <summary>所有建筑产量 +N%（可带成长）。</summary>
    public static Modifier GlobalPercent(double fraction, Scaling? scaling = null)
        => new(ModifierTarget.GlobalCps, ModifierOperation.AdditivePercent, fraction, scaling);

    /// <summary>所有建筑产量 ×N。</summary>
    public static Modifier GlobalMultiplier(double factor)
        => new(ModifierTarget.GlobalCps, ModifierOperation.Multiplicative, factor);

    /// <summary>指定建筑产量 +N%。</summary>
    public static Modifier BuildingPercent(string buildingId, double fraction, Scaling? scaling = null)
        => new(ModifierTarget.BuildingCps(buildingId), ModifierOperation.AdditivePercent, fraction, scaling);

    /// <summary>指定建筑产量 ×N。</summary>
    public static Modifier BuildingMultiplier(string buildingId, double factor)
        => new(ModifierTarget.BuildingCps(buildingId), ModifierOperation.Multiplicative, factor);

    /// <summary>点击收益 +N（加法）。</summary>
    public static Modifier ClickFlat(double amount)
        => new(ModifierTarget.ClickPower, ModifierOperation.Flat, amount);

    /// <summary>点击收益 +N%。</summary>
    public static Modifier ClickPercent(double fraction)
        => new(ModifierTarget.ClickPower, ModifierOperation.AdditivePercent, fraction);

    /// <summary>点击收益 ×N。</summary>
    public static Modifier ClickMultiplier(double factor)
        => new(ModifierTarget.ClickPower, ModifierOperation.Multiplicative, factor);

    /// <summary>所有建筑价格 ×N（小于 1 表示打折）。</summary>
    public static Modifier PriceMultiplier(double factor, string? buildingId = null)
        => new(buildingId is null ? ModifierTarget.GlobalPrice : ModifierTarget.BuildingPrice(buildingId),
               ModifierOperation.Multiplicative, factor);

    /// <summary>所有建筑价格 -N%。</summary>
    public static Modifier PriceDiscount(double fraction)
        => new(ModifierTarget.GlobalPrice, ModifierOperation.AdditivePercent, -fraction);

    /// <summary>金猫相关调整。</summary>
    public static Modifier GoldenCookieFrequency(double factor)
        => new(ModifierTarget.GoldenCookieFrequency, ModifierOperation.Multiplicative, factor);

    /// <summary>金猫奖励倍率。</summary>
    public static Modifier GoldenCookieReward(double factor)
        => new(ModifierTarget.GoldenCookieReward, ModifierOperation.Multiplicative, factor);
}
