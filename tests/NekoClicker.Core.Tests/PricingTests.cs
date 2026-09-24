using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>价格、批量购买与出售的闭式解。</summary>
public static class PricingTests
{
    private static readonly BuildingDefinition Building = new()
    {
        Id = "b",
        Name = "B",
        BasePrice = 15,
        BaseCps = 0.1,
    };

    [Test]
    public static void UnitPrice_GrowsGeometrically()
    {
        Check.Close(15, Pricing.UnitPrice(Building, 0, 1));
        Check.Close(15 * 1.15, Pricing.UnitPrice(Building, 1, 1));
        Check.Close(15 * Math.Pow(1.15, 10), Pricing.UnitPrice(Building, 10, 1));
    }

    [Test]
    public static void UnitPrice_AppliesMultiplier()
    {
        Check.Close(15 * 0.9, Pricing.UnitPrice(Building, 0, 0.9));
    }

    [Test]
    public static void BulkPrice_EqualsSumOfUnitPrices()
    {
        Check.Close(SumUnitPrices(0, 1), Pricing.BulkPrice(Building, 0, 1, 1));
        Check.Close(SumUnitPrices(0, 10), Pricing.BulkPrice(Building, 0, 10, 1));
        Check.Close(SumUnitPrices(7, 25), Pricing.BulkPrice(Building, 7, 25, 1));
        Check.Close(SumUnitPrices(200, 100), Pricing.BulkPrice(Building, 200, 100, 1), 1e-3);
    }

    [Test]
    public static void MaxAffordable_IsTightBothWays()
    {
        const double budget = 100;
        int max = Pricing.MaxAffordable(Building, 0, budget, 1, 10_000);

        Check.Equal(4, max);
        Check.AtMost(Pricing.BulkPrice(Building, 0, max, 1), budget);
        Check.Greater(Pricing.BulkPrice(Building, 0, max + 1, 1), budget);
    }

    [Test]
    public static void MaxAffordable_HandlesEdgeCases()
    {
        Check.Equal(0, Pricing.MaxAffordable(Building, 0, 0, 1, 100));
        Check.Equal(0, Pricing.MaxAffordable(Building, 0, 14.99, 1, 100));
        Check.Equal(0, Pricing.MaxAffordable(Building, 0, 1e9, 1, 0), "cap 为 0 时应返回 0。");
        Check.Equal(5, Pricing.MaxAffordable(Building, 0, 1e9, 1, 5), "应被 cap 截断。");
    }

    [Test]
    public static void SellValue_RefundsLastPurchasedUnits()
    {
        Check.Close(15 * 0.5, Pricing.SellValue(Building, 1, 1, 0.5));
        Check.Close(15 * Math.Pow(1.15, 4) * 0.5, Pricing.SellValue(Building, 5, 1, 0.5));
        Check.Close(
            (15 * Math.Pow(1.15, 3) + 15 * Math.Pow(1.15, 4)) * 0.5,
            Pricing.SellValue(Building, 5, 2, 0.5));
    }

    [Test]
    public static void SellValue_NeverExceedsOwned()
    {
        Check.Close(Pricing.SellValue(Building, 2, 2, 0.5), Pricing.SellValue(Building, 2, 99, 0.5));
    }

    [Test]
    public static void UpgradePrice_SupportsRepeatables()
    {
        var repeatable = new UpgradeDefinition
        {
            Id = "u",
            Name = "U",
            Price = 100,
            MaxPurchases = 5,
            PriceGrowth = 2,
        };

        Check.Close(100, Pricing.UpgradePrice(repeatable, 0, 1));
        Check.Close(300, Pricing.UpgradePrice(repeatable, 0, 2));
        Check.Close(100 + 200 + 400, Pricing.UpgradePrice(repeatable, 0, 3));
        Check.Close(800, Pricing.UpgradePrice(repeatable, 3, 1));
    }

    [Test]
    public static void UpgradePrice_FixedPriceWhenNoGrowth()
    {
        var fixedPrice = new UpgradeDefinition { Id = "u", Name = "U", Price = 50, MaxPurchases = 3 };
        Check.Close(150, Pricing.UpgradePrice(fixedPrice, 0, 3));
    }

    [Test]
    public static void HugeOwnedCounts_DoNotProduceNaN()
    {
        double price = Pricing.UnitPrice(Building, 5_000, 1);
        Check.True(double.IsFinite(price) || price == double.MaxValue, "极端持有量下价格应是有限值或饱和值。");
        Check.True(Pricing.MaxAffordable(Building, 5_000, 1e300, 1, 1_000_000) >= 0);
    }

    private static double SumUnitPrices(int owned, int count)
    {
        double total = 0;
        for (int i = 0; i < count; i++) total += Pricing.UnitPrice(Building, owned + i, 1);
        return total;
    }
}
