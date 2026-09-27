using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>修饰符管线与增益系统。</summary>
public static class ModifierTests
{
    [Test]
    public static void AdditivePercents_AddUp()
    {
        GameEngine engine = TestGame.CreateMini(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("b", 1);
        Check.Close(1.0, engine.CookiesPerSecond, 1e-9);

        engine.BuyUpgrade("pct1");
        Check.Close(1.5, engine.CookiesPerSecond, 1e-9);

        engine.BuyUpgrade("pct2");
        // 两个 +50% 应是 +100%（而不是 ×2.25）
        Check.Close(2.0, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void MultiplicativeBonuses_Multiply()
    {
        GameEngine engine = TestGame.CreateMini(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("b", 1);

        engine.BuyUpgrade("mul1");
        engine.BuyUpgrade("mul2");

        Check.Close(4.0, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void MixedOperations_FollowDocumentedOrder()
    {
        GameEngine engine = TestGame.CreateMini(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("b", 1);

        engine.BuyUpgrade("pct1"); // +50%
        engine.BuyUpgrade("mul1"); // ×2

        // 1 × (1 + 0.5) × 2 = 3
        Check.Close(3.0, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void ClickFlat_AppliesBeforeMultiplier()
    {
        GameEngine engine = TestGame.CreateMini(out _);
        engine.State.Cookies = 10;
        engine.MarkDirty();

        Check.True(engine.BuyUpgrade("click_flat").Success); // +5

        Check.Close(6.0, engine.ClickPower, 1e-9); // (1 + 0) + 5
    }

    [Test]
    public static void Buff_MultipliesProduction_AndExpires()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);
        double baseCps = engine.CookiesPerSecond;
        Check.Greater(baseCps, 0);

        Check.True(engine.ApplyBuff("frenzy").Success);
        Check.CloseRelative(baseCps * 7, engine.CookiesPerSecond, 1e-9);

        engine.Simulate(78); // 狂热 77 秒

        Check.Equal(0, engine.State.Buffs.Count);
        Check.CloseRelative(baseCps, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void Buff_RefreshMode_DoesNotStack()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.ApplyBuff("frenzy", 10);
        engine.ApplyBuff("frenzy", 20);

        Check.Equal(1, engine.State.Buffs.Count);
        ActiveBuff buff = engine.State.Buffs[0];
        Check.Equal(1, buff.Stacks);
        Check.Close(20, buff.RemainingSeconds, 1e-9, "Refresh 模式应取较长剩余时间。");
    }

    [Test]
    public static void Buff_ExtendMode_AddsDuration()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.ApplyBuff("cotton_bed_frenzy", 10);
        engine.ApplyBuff("cotton_bed_frenzy", 15);

        Check.Close(25, engine.State.Buffs[0].RemainingSeconds, 1e-9);
    }

    [Test]
    public static void BuildingSpecificBuff_OnlyAffectsThatBuilding()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);
        engine.BuyBuilding("cat_bed", 5);

        double curlsBefore = engine.Production.For("curled_cat").Cps;
        double bedsBefore = engine.Production.For("cat_bed").Cps;
        Check.Greater(bedsBefore, 0);

        engine.ApplyBuff("cotton_bed_frenzy", 30);

        Check.CloseRelative(curlsBefore, engine.Production.For("curled_cat").Cps, 1e-9);
        Check.CloseRelative(bedsBefore * 30, engine.Production.For("cat_bed").Cps, 1e-9);
    }

    [Test]
    public static void Debuff_ReducesProduction()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);
        double baseCps = engine.CookiesPerSecond;

        engine.ApplyBuff("clumsy_paws");

        Check.CloseRelative(baseCps * 0.5, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void AchievementScaling_MakesMilkUpgradesGrow()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 25);
        engine.BuyBuilding("scratching_post", 25);
        engine.BuyBuilding("cat_bed", 50);

        int achievements = engine.State.Achievements.Count;
        Check.AtLeast(achievements, 5);

        Check.True(engine.BuyUpgrade("kitten_helpers").Success, "每个成就 +1% 的升级应可购买。");

        // 只有 kitten_helpers 一个修饰符生效。
        double baseCps = (25 * 0.1) + (25 * 1) + (50 * 8);
        Check.CloseRelative(baseCps * (1 + (achievements * 0.01)), engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void RepeatableUpgrade_AppliesPerPurchase()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);
        double baseCps = engine.CookiesPerSecond;

        Check.True(engine.BuyUpgrade("bulk_kibble_coupon", 3).Success);

        Check.Equal(3, engine.State.UpgradeCount("bulk_kibble_coupon"));
        Check.CloseRelative(baseCps * Math.Pow(1.5, 3), engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void PriceDiscount_ReducesUnitPrice()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        Check.True(engine.BuyUpgrade("wholesale_haggling").Success, "砍价升级应可购买。");

        Check.CloseRelative(15 * 0.95, engine.GetPriceMultiplier("curled_cat") * 15, 1e-9);
    }

    [Test]
    public static void ModifierSet_AccumulatesIndependentlyPerTarget()
    {
        var set = new ModifierSet();
        var metrics = new StubMetrics();

        set.Add(Modifier.GlobalPercent(0.5), metrics);
        set.Add(Modifier.GlobalPercent(0.5), metrics);
        set.Add(Modifier.GlobalMultiplier(3), metrics);
        set.Add(Modifier.BuildingPercent("x", 1.0), metrics);

        Check.Close(2.0 * 3.0, set.Multiplier(ModifierTarget.GlobalCps), 1e-9);
        Check.Close(2.0, set.Multiplier(ModifierTarget.BuildingCps("x")), 1e-9);
        Check.Close(1.0, set.Multiplier(ModifierTarget.BuildingCps("y")), 1e-9);
        Check.Close(0.0, set.FlatOf(ModifierTarget.GlobalCps), 1e-9);
    }

    [Test]
    public static void Scaling_ClampsAtCap()
    {
        var scaling = new Scaling(ScalingSource.BuildingCount, 0.1, Cap: 5, Id: "b");
        var metrics = new StubMetrics { BuildingCountValue = 100 };

        Check.Close(5, scaling.Evaluate(metrics), 1e-9);
        Check.Close(1.5, scaling.Apply(1.0, metrics), 1e-9);
    }

    private sealed class StubMetrics : IGameMetrics
    {
        public int BuildingCountValue { get; init; }

        public double Cookies => 0;
        public double CookiesPerSecond => 0;
        public double ClickPower => 0;
        public double CookiesEarnedThisRun => 0;
        public double CookiesEarnedAllTime => 0;
        public double TotalClicks => 0;
        public double HandMadeCookies => 0;
        public double GoldenCookiesClicked => 0;
        public int PrestigeLevel => 0;
        public double PrestigeChips => 0;
        public int Ascensions => 0;
        public double PlayTimeSeconds => 0;
        public int BuildingCount(string buildingId) => BuildingCountValue;
        public double TotalBuildings => BuildingCountValue;
        public int UpgradeCount(string upgradeId) => 0;
        public bool HasUpgrade(string upgradeId) => false;
        public int PurchasedUpgradeCount => 0;
        public bool HasAchievement(string achievementId) => false;
        public int AchievementCount => 0;
        public int TaggedUpgradeCount(string tag) => 0;
        public int TaggedBuildingCount(string tag) => 0;
        public double GetCounter(string key) => 0;
        public int Era => 1;
        public int LoreCount => 0;
        public bool HasChoice(string choiceId) => false;
        public int StanceWeight(string stanceId) => 0;
        public bool HasEnding(string endingId) => false;
    }
}
