using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 价格与批量购买求解。<para>
/// 原版的价格公式是 <c>base × 1.15^已持有数</c>，买 k 个就是等比数列求和。
/// 这里全部用闭式解而不是循环累加：一是 <c>BuyMax</c> 在后期要一次算出成百上千个，
/// 循环会卡；二是闭式解在浮点下更稳定，也方便反解出"最多买得起几个"。
/// </para>
/// </summary>
public static class Pricing
{
    /// <summary>第 <paramref name="owned"/> 个（0 基）建筑的单价。</summary>
    public static double UnitPrice(BuildingDefinition definition, int owned, double priceMultiplier)
    {
        double growth = definition.PriceGrowth;
        double scaled = Num.SafeMul(definition.BasePrice, SafePow(growth, owned));
        return Num.SafeMul(scaled, priceMultiplier);
    }

    /// <summary>连续购买 <paramref name="count"/> 个建筑的总价（等比数列求和）。</summary>
    public static double BulkPrice(BuildingDefinition definition, int owned, int count, double priceMultiplier)
    {
        if (count <= 0) return 0;
        if (count == 1) return UnitPrice(definition, owned, priceMultiplier);

        double growth = definition.PriceGrowth;
        double first = UnitPrice(definition, owned, priceMultiplier);

        // Σ_{i=0}^{count-1} first×growth^i = first × (growth^count - 1) / (growth - 1)
        double factor = SafePow(growth, count);
        double sum = Num.SafeMul(first, (factor - 1.0) / (growth - 1.0));
        return double.IsFinite(sum) ? sum : double.MaxValue;
    }

    /// <summary>
    /// 给定预算最多能买几个。<para>
    /// 反解等比不等式：<c>first × (r^k − 1)/(r − 1) ≤ budget</c> → <c>k ≤ log_r(1 + budget(r−1)/first)</c>。
    /// 得到解析解后再用实际总价回退校验，消除浮点误差导致的"差一点点买不起"。
    /// </para>
    /// </summary>
    public static int MaxAffordable(BuildingDefinition definition, int owned, double budget, double priceMultiplier, int cap)
    {
        if (budget <= 0 || cap <= 0) return 0;

        double first = UnitPrice(definition, owned, priceMultiplier);
        if (!double.IsFinite(first)) return 0;
        if (first <= 0) return cap;
        if (budget < first) return 0;

        double growth = definition.PriceGrowth;
        double x = 1.0 + (budget * (growth - 1.0) / first);
        if (!double.IsFinite(x)) return cap;

        double k = Math.Log(x) / Math.Log(growth);
        if (double.IsNaN(k)) return 0;

        int count = (int)Math.Min(Math.Floor(k), cap);

        // 浮点回退：解析解通常已经正确，最多回退几步。
        int guard = 0;
        while (count > 0 && BulkPrice(definition, owned, count, priceMultiplier) > budget && guard++ < 64)
            count--;

        return count;
    }

    /// <summary>出售 <paramref name="count"/> 个建筑可返还的金额（按最近买进的那几个的单价折算）。</summary>
    public static double SellValue(BuildingDefinition definition, int owned, int count, double refundRate)
    {
        if (count <= 0 || owned <= 0) return 0;
        count = Math.Min(count, owned);

        double growth = definition.PriceGrowth;
        // 被卖掉的这 count 个的单价之和：从 owned-1 往回数 count 个。
        double first = Num.SafeMul(definition.BasePrice, SafePow(growth, owned - count));
        double factor = SafePow(growth, count);
        double sum = Num.SafeMul(first, (factor - 1.0) / (growth - 1.0));
        return Num.SafeMul(sum, refundRate);
    }

    /// <summary>重复购买的升级总价（支持 <see cref="UpgradeDefinition.PriceGrowth"/>）。</summary>
    public static double UpgradePrice(UpgradeDefinition definition, int owned, int count)
    {
        if (count <= 0) return 0;
        double growth = Math.Max(1.0, definition.PriceGrowth);
        double first = Num.SafeMul(definition.Price, SafePow(growth, owned));
        if (count == 1) return first;
        if (growth <= 1.0) return Num.SafeMul(first, count);
        double factor = SafePow(growth, count);
        double sum = Num.SafeMul(first, (factor - 1.0) / (growth - 1.0));
        return double.IsFinite(sum) ? sum : double.MaxValue;
    }

    private static double SafePow(double baseValue, double exponent)
    {
        double r = Math.Pow(baseValue, exponent);
        return double.IsFinite(r) ? r : double.MaxValue;
    }
}
