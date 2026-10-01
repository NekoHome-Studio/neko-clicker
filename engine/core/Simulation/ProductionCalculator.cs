using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>单个建筑的产量明细。</summary>
/// <param name="Id">建筑 id。</param>
/// <param name="Count">持有数量。</param>
/// <param name="BaseCps">单个建筑的基础产量。</param>
/// <param name="Multiplier">该建筑自身受到的倍率（不含全局倍率）。</param>
/// <param name="Cps">该建筑当前的总产量（含全局倍率）。</param>
/// <param name="Share">占总产量的比例 [0,1]。</param>
public readonly record struct BuildingProduction(
    string Id,
    int Count,
    double BaseCps,
    double Multiplier,
    double Cps,
    double Share);

/// <summary>一次生产结算的完整明细，供 UI 展示与数值验证。</summary>
public sealed record ProductionBreakdown
{
    /// <summary>空明细。</summary>
    public static readonly ProductionBreakdown Empty = new();

    /// <summary>总每秒产量。</summary>
    public double CookiesPerSecond { get; init; }

    /// <summary>点击一次的收益。</summary>
    public double ClickPower { get; init; }

    /// <summary>点击收益中"未乘点击倍率"的基础部分。</summary>
    public double ClickBase { get; init; }

    /// <summary>全局产量倍率（含所有建筑通用加成）。</summary>
    public double GlobalMultiplier { get; init; } = 1.0;

    /// <summary>逐建筑明细。</summary>
    public IReadOnlyDictionary<string, BuildingProduction> Buildings { get; init; }
        = new Dictionary<string, BuildingProduction>(StringComparer.Ordinal);

    /// <summary>读取某建筑的明细；不存在时返回零值。</summary>
    public BuildingProduction For(string buildingId)
        => Buildings.TryGetValue(buildingId, out BuildingProduction p) ? p : default;
}

/// <summary>
/// 生产管线：把 <see cref="GameState"/> + <see cref="ModifierSet"/> 折算成每秒产量与点击收益。<para>
/// 计算顺序（与 Cookie Clicker 的 <c>getCPS</c> 对齐）：
/// <list type="number">
///   <item>单个建筑产量 = (基础产量 + 加法项) × 该建筑倍率 × 全局倍率</item>
///   <item>建筑总产量 = 单个建筑产量 × 持有数量</item>
///   <item>总 CPS = Σ 建筑总产量，再统一施加全局乘方</item>
///   <item>点击收益 = (固定基础 + 总 CPS × 比例 + 加法项) × 点击倍率</item>
/// </list>
/// 第 4 步故意用"含增益后的总 CPS"，这样狂热（Frenzy）期间点击收益同步上涨，
/// 与原版体感一致。
/// </para>
/// </summary>
public static class ProductionCalculator
{
    /// <summary>结算一次生产。</summary>
    public static ProductionBreakdown Compute(
        GameContent content,
        GameState state,
        ModifierSet modifiers,
        GameBalance balance)
    {
        ModifierTarget globalTarget = ModifierTarget.GlobalCps;
        double globalMultiplier = modifiers.Multiplier(globalTarget);
        double globalPower = modifiers.PowerOf(globalTarget);

        var perBuilding = new Dictionary<string, BuildingProduction>(StringComparer.Ordinal);

        double totalBeforePower = 0;
        // 先算出未施加全局乘方的分量，用于后面把全局乘方摊回每个建筑。
        var rawContributions = new List<(string Id, double Count, double BaseCps, double BuildingMultiplier, double Raw)>(content.Buildings.Count);

        foreach (BuildingDefinition def in content.Buildings)
        {
            int count = state.BuildingCount(def.Id);
            ModifierTarget target = ModifierTarget.BuildingCps(def.Id);

            double flat = modifiers.FlatOf(target);
            double buildingMultiplier = modifiers.Multiplier(target);
            double buildingPower = modifiers.PowerOf(target);

            double perUnit = (def.BaseCps + flat) * buildingMultiplier;
            if (buildingPower != 1.0) perUnit = Num.SafePow(perUnit, buildingPower);

            double raw = count <= 0 ? 0 : Num.SafeMul(perUnit, count);
            if (raw > 0) totalBeforePower += raw;

            rawContributions.Add((def.Id, count, def.BaseCps, buildingMultiplier, raw));
        }

        double totalAfterMultiplier = Num.SafeMul(totalBeforePower, globalMultiplier);

        // 全局乘方：先算最终总量，再折算成一个标量因子摊回每个建筑，
        // 这样"每个建筑的占比"在乘方后依然自洽。
        double total = totalAfterMultiplier;
        double powerFactor = 1.0;
        if (globalPower != 1.0)
        {
            total = Num.SafePow(totalAfterMultiplier, globalPower);
            if (totalAfterMultiplier > 0 && double.IsFinite(total))
                powerFactor = total / totalAfterMultiplier;
        }

        double effectiveGlobal = globalMultiplier * powerFactor;

        foreach ((string id, double count, double baseCps, double buildingMultiplier, double raw) in rawContributions)
        {
            double cps = Num.SafeMul(raw, effectiveGlobal);
            double share = total > 0 ? cps / total : 0;
            perBuilding[id] = new BuildingProduction(id, (int)count, baseCps, buildingMultiplier, cps, share);
        }

        double clickBase = balance.ClickBasePower + Num.SafeMul(total, balance.ClickCpsRatio);
        ModifierTarget clickTarget = ModifierTarget.ClickPower;
        double clickFlat = modifiers.FlatOf(clickTarget);
        double click = (clickBase + clickFlat) * modifiers.Multiplier(clickTarget);
        double clickPower = modifiers.PowerOf(clickTarget);

        return new ProductionBreakdown
        {
            CookiesPerSecond = total,
            ClickPower = clickPower != 1.0 ? Num.SafePow(click, clickPower) : click,
            ClickBase = clickBase,
            GlobalMultiplier = effectiveGlobal,
            Buildings = perBuilding,
        };
    }
}
