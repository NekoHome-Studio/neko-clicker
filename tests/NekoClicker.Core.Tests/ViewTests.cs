using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>UI 只读视图层。</summary>
public static class ViewTests
{
    [Test]
    public static void Snapshot_ContainsAllContentRows()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.Buy10);

        Check.Equal(engine.Content.Buildings.Count, snapshot.Buildings.Count);
        Check.Equal(engine.Content.Upgrades.Count, snapshot.Upgrades.Count);
        Check.Equal(engine.Content.Achievements.Count, snapshot.Achievements.Count);
        Check.Equal("猫咖物语", snapshot.Title);
        Check.Equal("小鱼干", snapshot.CurrencyName);
        Check.Equal("撸猫", snapshot.ClickActionName);
        Check.Equal(PurchaseMode.Buy10, snapshot.Mode);
    }

    [Test]
    public static void Snapshot_FormatsNumbersForDisplay()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 2_500_000;
        engine.MarkDirty();

        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal("2.5 million", snapshot.CookiesText);
        Check.Equal("0", snapshot.CpsText);
        Check.Equal("1", snapshot.ClickPowerText);
    }

    [Test]
    public static void Snapshot_ReportsNextMilestone()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        GameSnapshot snapshot = engine.Snapshot();

        BuildingView cat = snapshot.Buildings.First(b => b.Id == "curled_cat");

        Check.Equal(0, cat.Owned);
        Check.True(cat.IsUnlocked);
        Check.Equal(1, cat.NextMilestoneAt, "0 个时应指向第一个里程碑（需要 1 个）。");
        Check.NotNull(cat.NextMilestoneName);
    }

    [Test]
    public static void Snapshot_MilestoneAdvancesWithOwnership()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);

        BuildingView cat = engine.Snapshot().Buildings.First(b => b.Id == "curled_cat");

        Check.Equal(25, cat.NextMilestoneAt, "已有 10 个时应指向 25 个的里程碑。");
    }

    [Test]
    public static void Snapshot_BuyMaxReportsAffordableBatch()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 100;
        engine.MarkDirty();

        BuildingView cat = engine.Snapshot(PurchaseMode.BuyMax).Buildings.First(b => b.Id == "curled_cat");

        Check.True(cat.CanAfford);
        Check.AtLeast(cat.BatchAmount, 1);
        Check.AtMost(cat.BatchPrice, 100 + 1e-9);
    }

    [Test]
    public static void Snapshot_SellModeReportsRefund()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 5);

        BuildingView cat = engine.Snapshot(PurchaseMode.Sell1).Buildings.First(b => b.Id == "curled_cat");

        Check.True(cat.CanAfford, "出售模式下 CanAfford 表示有货可卖。");
        Check.Equal(1, cat.BatchAmount);
        Check.Greater(cat.BatchPrice, 0);
        Check.CloseRelative(15 * Math.Pow(1.15, 4) * 0.5, cat.BatchPrice, 1e-9);
    }

    [Test]
    public static void Snapshot_UpgradeRowsExposeAffordabilityAndEffects()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        for (int i = 0; i < 10; i++) engine.Click();
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        UpgradeView warmer = engine.Snapshot().Upgrades.First(u => u.Id == "warmer_hands");

        Check.True(warmer.IsUnlocked);
        Check.True(warmer.CanAfford);
        Check.False(warmer.IsMaxed);
        Check.Contains(warmer.EffectSummary, "点击收益");
    }

    [Test]
    public static void Snapshot_BuildingSharesSumToOne()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);
        engine.BuyBuilding("scratching_post", 10);
        engine.BuyBuilding("cat_bed", 10);

        GameSnapshot snapshot = engine.Snapshot();
        double totalShare = snapshot.Buildings.Sum(b => b.CpsShare);

        Check.Close(1.0, totalShare, 1e-9);
    }

    [Test]
    public static void Snapshot_BuffRowsCarryProgress()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.ApplyBuff("frenzy", 100);
        engine.Simulate(25);

        BuffView buff = engine.Snapshot().Buffs.Single();

        Check.Equal("frenzy", buff.Id);
        Check.Close(75, buff.RemainingSeconds, 1e-6);
        Check.True(buff.Progress is > 0.7 and < 0.8, $"进度应约为 75%，实际 {buff.Progress}");
        Check.False(buff.IsDebuff);
    }

    [Test]
    public static void Snapshot_AchievementProgressIsQuantified()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 5);

        AchievementView next = engine.Snapshot().Achievements.First(a => a.Id == "curled_cat_x25");

        Check.False(next.Unlocked);
        Check.Close(0.2, next.Progress, 1e-6); // 5 / 25
        Check.Contains(next.ProgressText, "/ 25");
    }

    [Test]
    public static void Snapshot_PrestigePreviewIsPresent()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.CookiesEarnedAllTime = 4e12;
        engine.MarkDirty();

        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal(0, snapshot.Prestige.CurrentLevel);
        Check.Equal(1, snapshot.Prestige.NextLevel);
        Check.True(snapshot.Prestige.CanAscend);
    }

    [Test]
    public static void PurchaseModeHelpers_BehaveConsistently()
    {
        Check.True(PurchaseMode.Sell10.IsSell());
        Check.False(PurchaseMode.Buy10.IsSell());
        Check.Equal(0, PurchaseMode.BuyMax.RequestedAmount());
        Check.Equal(10, PurchaseMode.Buy10.RequestedAmount());
        Check.Equal(PurchaseMode.Buy100, PurchaseMode.Buy10.NextBuy());
        Check.Equal(PurchaseMode.Buy1, PurchaseMode.BuyMax.NextBuy());
        Check.Equal(PurchaseMode.Sell1, PurchaseMode.Buy1.ToggleSell());
        Check.Equal(PurchaseMode.Buy10, PurchaseMode.Sell10.ToggleSell());
    }
}
