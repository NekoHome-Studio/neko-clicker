using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>引擎的玩家动作：点击、购买、出售、行动结果与通知。</summary>
public static class EngineTests
{
    [Test]
    public static void Click_WithoutBuildings_GivesBasePower()
    {
        GameEngine engine = TestGame.CreateNeko(out _);

        ClickResult result = engine.Click();

        Check.Close(1.0, result.Gained);
        Check.Close(1.0, engine.State.Cookies);
        Check.Close(1.0, engine.State.TotalClicks);
        Check.Close(1.0, engine.State.HandMadeCookies);
        Check.Close(1.0, engine.State.CookiesEarnedAllTime);
    }

    [Test]
    public static void Click_ScalesWithCps()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("curled_cat", 10).Success);

        double cps = engine.CookiesPerSecond;
        Check.Close(1.0, cps, 1e-9); // 10 × 0.1

        ClickResult result = engine.Click();
        Check.Close(1.0 + (cps * 0.01), result.Gained, 1e-9);
    }

    [Test]
    public static void BuyBuilding_DeductsPrice_AndRaisesCps()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        PurchaseResult result = engine.BuyBuilding("curled_cat", 1);

        Check.True(result.Success);
        Check.Close(1_000 - 15, engine.State.Cookies, 1e-9);
        Check.Equal(1, engine.State.BuildingCount("curled_cat"));
        Check.Close(0.1, engine.CookiesPerSecond, 1e-9);
        Check.Equal(1, result.Amount);
        Check.Close(15, result.TotalPrice);
    }

    [Test]
    public static void BuyBuilding_FailsWhenUnaffordable()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1;
        engine.MarkDirty();

        PurchaseResult result = engine.BuyBuilding("curled_cat", 1);

        Check.False(result.Success);
        Check.Equal(0, engine.State.BuildingCount("curled_cat"));
        Check.Contains(result.Message, "买不起");
    }

    [Test]
    public static void BuyBuilding_DowngradesToAffordableAmount()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        // 15 + 17.25 = 32.25 买得起两个，第三个要 19.84 → 合计 52.09 > 40
        engine.State.Cookies = 40;
        engine.MarkDirty();

        PurchaseResult result = engine.BuyBuilding("curled_cat", 3);

        Check.True(result.Success);
        Check.Equal(2, result.Amount);
        Check.Equal(2, engine.State.BuildingCount("curled_cat"));
        Check.AtMost(engine.State.Cookies, 40);
    }

    [Test]
    public static void BuyBuilding_LockedContent_IsRejectedWithHint()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1e300;
        engine.MarkDirty();

        PurchaseResult result = engine.BuyBuilding("cat_universe", 1);

        Check.False(result.Success);
        Check.Contains(result.Message, "尚未解锁");
    }

    [Test]
    public static void SellBuilding_RefundsConfiguredRate()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);

        double before = engine.State.Cookies;
        PurchaseResult sold = engine.SellBuilding("curled_cat", 1);

        Check.True(sold.Success);
        Check.Equal(0, engine.State.BuildingCount("curled_cat"));
        Check.Close(before + (15 * 0.5), engine.State.Cookies, 1e-9);
    }

    [Test]
    public static void SellBuilding_FailsWhenNothingOwned()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        Check.False(engine.SellBuilding("curled_cat", 1).Success);
    }

    [Test]
    public static void BuyUpgrade_HonoursUnlock_And_IsSinglePurchase()
    {
        GameEngine engine = TestGame.CreateNeko(out _);

        // 尚未满足解锁条件（需要 100 次点击）。
        Check.False(engine.BuyUpgrade("plush_gloves").Success);

        for (int i = 0; i < 10; i++) engine.Click();
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        PurchaseResult bought = engine.BuyUpgrade("warmer_hands");

        Check.True(bought.Success);
        Check.Equal(1, engine.State.UpgradeCount("warmer_hands"));
        Check.Close(2.0, engine.ClickPower, 1e-9); // 基础 1 + 加法 1
        Check.False(engine.BuyUpgrade("warmer_hands").Success, "唯一升级不能重复购买。");
    }

    [Test]
    public static void BuyUpgrade_FailsWhenUnaffordable()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        for (int i = 0; i < 10; i++) engine.Click();

        PurchaseResult result = engine.BuyUpgrade("warmer_hands");

        Check.False(result.Success);
        Check.Contains(result.Message, "买不起");
    }

    [Test]
    public static void BuyUpgrade_UnknownId_IsRejected()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        Check.False(engine.BuyUpgrade("does_not_exist").Success);
        Check.False(engine.BuyBuilding("does_not_exist", 1).Success);
    }

    [Test]
    public static void Achievements_UnlockAtThresholds()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();

        Check.False(engine.State.Achievements.Contains("curled_cat_x25"));

        engine.BuyBuilding("curled_cat", 25);

        Check.True(engine.State.Achievements.Contains("curled_cat_x1"));
        Check.True(engine.State.Achievements.Contains("curled_cat_x25"));
        Check.False(engine.State.Achievements.Contains("curled_cat_x50"));
    }

    [Test]
    public static void Achievements_FireEvents_And_MarkNotifications()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        List<string> unlocked = [];
        using IDisposable subscription = engine.Events.Subscribe<Events.AchievementUnlockedEvent>(e => unlocked.Add(e.Id));

        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);

        Check.True(unlocked.Contains("curled_cat_x1"), "成就解锁应派发事件。");
        Check.True(engine.Notifications.Any(n => n.Message.Contains("成就解锁")), "成就解锁应产生通知。");
    }

    [Test]
    public static void BuyMax_HandlesEnormousBudgets()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1e40;
        engine.MarkDirty();

        PurchaseResult result = engine.BuyBuilding("curled_cat", 0); // 0 = 买到买不起为止

        Check.True(result.Success);
        // 1e40 预算下等比数列的解析解约 626 个；关键是结果有限且确实花了钱。
        Check.AtLeast(engine.State.BuildingCount("curled_cat"), 600);
        Check.Finite(engine.CookiesPerSecond);
        Check.AtMost(engine.State.Cookies, 1e40);
        Check.AtLeast(engine.State.Cookies, 0);
    }

    [Test]
    public static void Update_ClampsCatchUp()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);

        // 传一个远超补算上限的 delta：只应推进 MaxCatchUpSeconds 秒。
        double advanced = engine.Update(3_600);

        Check.Close(engine.Balance.MaxCatchUpSeconds, advanced, 1e-9);
        Check.Close(engine.Balance.MaxCatchUpSeconds, engine.PlayTimeSeconds, 1e-6);
    }

    [Test]
    public static void Update_AccumulatesFixedSteps()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);

        double cps = engine.CookiesPerSecond;
        double before = engine.State.Cookies;

        engine.Update(0.5);

        // 只应结算整步（0.5s / (1/30) = 15 步整）。
        Check.Close(before + (cps * 0.5), engine.State.Cookies, 1e-6);
    }

    [Test]
    public static void Notifications_AreCapped()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        for (int i = 0; i < 200; i++) engine.Notify($"msg {i}");

        Check.AtMost(engine.Notifications.Count, 32);
        Check.True(engine.Notifications[^1].Message.Contains("199"), "应保留最新的通知。");
    }
}
