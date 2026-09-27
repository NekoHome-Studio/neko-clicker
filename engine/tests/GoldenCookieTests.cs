using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>金猫系统：刷新、抽取、结算、过期。</summary>
public static class GoldenCookieTests
{
    [Test]
    public static void ForcedOutcome_AppliesBuff()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("frenzy");

        Check.Equal(1, engine.State.GoldenCookies.Count);

        GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);

        Check.True(result.Success);
        Check.Equal("frenzy", result.OutcomeId);
        Check.Equal(0, engine.State.GoldenCookies.Count);
        Check.Close(1.0, engine.State.GoldenCookiesClicked);
        Check.Greater(result.BuffSeconds, 0);
        Check.NotNull(BuffSystem.Find(engine.State, "frenzy"));
    }

    [Test]
    public static void Luck_MatchesCookieClickerFormula()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);

        double cps = engine.CookiesPerSecond;
        double bank = engine.State.Cookies;
        Check.Greater(cps, 0);

        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("lucky");
        GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);

        // min(存量 × 15%, 产量 × 900 秒) + 产量 × 13 秒
        double expected = Math.Min(bank * 0.15, cps * 900) + (cps * 13);
        Check.CloseRelative(expected, result.CookiesGained, 1e-9);
        Check.CloseRelative(bank + expected, engine.State.Cookies, 1e-9);
    }

    [Test]
    public static void Luck_IsCappedByCps()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        // 存量极大但产量很低：应被 cps × 900 秒的上限截断
        engine.State.Cookies = 1e15;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);

        double cps = engine.CookiesPerSecond;
        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("lucky");
        GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);

        Check.CloseRelative((cps * 900) + (cps * 13), result.CookiesGained, 1e-9);
    }

    [Test]
    public static void Ruin_StealsBankButNeverGoesNegative()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("ruin");
        GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);

        Check.True(result.Success);
        Check.Close(-50, result.CookiesGained, 1e-9);
        Check.Close(950, engine.State.Cookies, 1e-9);

        // 空钱包被抢也不会变成负数
        engine.State.Cookies = 0;
        engine.MarkDirty();
        GoldenCookieSpawn second = engine.SpawnGoldenCookie("ruin");
        engine.ClickGoldenCookie(second.InstanceId);
        Check.AtLeast(engine.State.Cookies, 0);
    }

    [Test]
    public static void ChainOutcome_AppliesBothBuffs()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("chain");
        engine.ClickGoldenCookie(spawn.InstanceId);

        Check.NotNull(BuffSystem.Find(engine.State, "frenzy"));
        Check.NotNull(BuffSystem.Find(engine.State, "click_frenzy"));
        Check.Equal(2, engine.State.Buffs.Count);
    }

    [Test]
    public static void RewardUpgrade_ScalesGain()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.State.GoldenCookiesClicked = 5;
        engine.MarkDirty();
        Check.True(engine.BuyUpgrade("lucky_charm").Success, "幸运猫爪应可购买。");

        engine.BuyBuilding("curled_cat", 10);
        double cps = engine.CookiesPerSecond;
        double bank = engine.State.Cookies;

        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("lucky");
        GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);

        double baseGain = Math.Min(bank * 0.15, cps * 900) + (cps * 13);
        Check.CloseRelative(baseGain * 1.25, result.CookiesGained, 1e-9);
    }

    [Test]
    public static void GoldenCookie_ExpiresAfterLifetime()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.SpawnGoldenCookie("lucky");
        Check.Equal(1, engine.State.GoldenCookies.Count);

        engine.Simulate(20); // 停留时间 13 秒

        Check.Equal(0, engine.State.GoldenCookies.Count);
    }

    [Test]
    public static void GoldenCookie_SpawnsAutomatically()
    {
        GameEngine engine = TestGame.CreateNeko(out _, seed: 777);
        Check.Equal(0, engine.State.GoldenCookies.Count);

        // 金猫只活 13 秒，所以逐秒推进、一旦出现就停下（否则它早就过期了）。
        int elapsed = 0;
        while (elapsed < 400 && engine.State.GoldenCookies.Count == 0)
        {
            engine.Simulate(1);
            elapsed++;
        }

        Check.Equal(1, engine.State.GoldenCookies.Count);
        Check.True(engine.State.GoldenCookieIntroduced);
        Check.Greater(engine.State.GoldenCookies[0].LifetimeSeconds, 0);
        // 开局第一只应被 FirstGoldenCookieDelayFactor 提前（正常间隔 5~15 分钟）。
        Check.AtMost(elapsed, 180, "第一只金猫应在 3 分钟内出现。");
    }

    [Test]
    public static void GoldenCookie_PositionIsNormalized()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie();

        Check.True(spawn.X is > 0 and < 1, $"X 应归一化到 (0,1)，实际 {spawn.X}");
        Check.True(spawn.Y is > 0 and < 1, $"Y 应归一化到 (0,1)，实际 {spawn.Y}");
    }

    [Test]
    public static void ClickingMissingCookie_Fails()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        Check.False(engine.ClickGoldenCookie("gc-999").Success);
    }

    [Test]
    public static void WeightedPick_NeverReturnsUnknownOutcome()
    {
        GameEngine engine = TestGame.CreateNeko(out _, seed: 31);
        HashSet<string> ids = [.. engine.Content.GoldenCookieOutcomes.Select(o => o.Id)];

        for (int i = 0; i < 300; i++)
        {
            GoldenCookieSpawn spawn = engine.SpawnGoldenCookie();
            GoldenCookieResult result = engine.ClickGoldenCookie(spawn.InstanceId);
            Check.True(result.Success);
            Check.True(ids.Contains(result.OutcomeId!), $"抽到了未定义的结果：{result.OutcomeId}");
        }
    }

    [Test]
    public static void GoldenCookieEvents_ArePublished()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        int spawned = 0;
        int clicked = 0;
        using IDisposable s1 = engine.Events.Subscribe<Events.GoldenCookieSpawnedEvent>(_ => spawned++);
        using IDisposable s2 = engine.Events.Subscribe<Events.GoldenCookieClickedEvent>(_ => clicked++);

        GoldenCookieSpawn spawn = engine.SpawnGoldenCookie("frenzy");
        engine.ClickGoldenCookie(spawn.InstanceId);

        Check.Equal(1, spawned);
        Check.Equal(1, clicked);
    }

    [Test]
    public static void GoldenCookieViews_ExposeCountdown()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.SpawnGoldenCookie();

        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal(1, snapshot.GoldenCookies.Count);
        Check.True(snapshot.GoldenCookies[0].Progress is > 0 and <= 1);
    }
}
