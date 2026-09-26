using NekoClicker.Core.Content;
using NekoClicker.Core;

namespace NekoClicker.Core.Tests;

/// <summary>转生公式与重置语义。</summary>
public static class PrestigeTests
{
    /// <summary>
    /// 纪元包的永久升级线必须**在一次自然游玩里买得起**。
    /// <para>
    /// 这条守卫来自一个真实缺陷：七个内容包原本都照抄 <c>PrestigeDivisor = 1e12</c>，
    /// 而纪元包的可结算历史累计被自己的阶梯卡在 1e8~1e11（等级只在舍命那一刻结算，
    /// 最后一次结算之后就再也不会换了）。实测五个纪元包各跑一遍，结算货币全是 <b>0</b>，
    /// 于是「前世技能 / 前世经验 / 余烬 / 批注」这几条线**结构上打不开**——
    /// 不是难，是永远拿不到，而运行期完全看不出来（它们只是"一直没亮"）。
    /// </para>
    /// <para>
    /// <b>为什么只守纪元包</b>：经典包（示例包、咖啡馆）的转生是**可重复的循环**，
    /// 一局买不完可以再转生几次，包络是开放的；纪元包的一局只有那几次结算，
    /// 所以必须一次买得起。两边的判据不同，不能用一个阈值糊过去。
    /// </para>
    /// </summary>
    [Test]
    public static void EraPacks_PermanentUpgradesAreAffordableWithinOneRun()
    {
        foreach ((string name, GameEngine engine) in EraPacks())
        {
            for (int round = 0; round < 60_000 && engine.ReachedEnding is null; round++)
            {
                for (int i = 0; i < 8; i++) engine.Click();
                TestGame.BuyGreedily(engine);   // 只买普通升级，转生升级留给这条断言去算

                for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                    engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

                if (engine.EraGate.CanAdvance) engine.Ascend();
                engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);
            }

            List<UpgradeDefinition> line =
            [
                .. engine.Content.Upgrades.Where(u => u.Persistence == UpgradePersistence.Permanent),
            ];
            double total = line.Sum(u => u.Price);
            double chips = engine.State.PrestigeChips;

            Console.WriteLine(
                $"      {name}：一次游玩结算 {chips:F0} 点转生货币，永久线总价 {total:F0}"
                + $"（{string.Join(" / ", line.Select(u => u.Price.ToString("F0")))}）");

            Check.True(line.Count > 0, $"{name} 没有任何永久升级。");
            Check.AtLeast(
                chips,
                total,
                $"{name} 一次自然游玩只结算出 {chips:F0} 点转生货币，"
                + $"而永久线总价 {total:F0}——这条线里有内容永远买不到。");
        }
    }

    private static (string Name, GameEngine Engine)[] EraPacks()
    {
        ManualClock _;
        return
        [
            ("#2 九命", TestGame.CreateNineLives(out _)),
            ("#3 实验室", TestGame.CreateLab(out _)),
            ("#10 公司", TestGame.CreateCompany(out _)),
            ("#6 末世", TestGame.CreateApocalypse(out _)),
            ("#9 图书馆", TestGame.CreateLibrary(out _)),
            ("#7 神明", TestGame.CreateGod(out _)),
        ];
    }

    [Test]
    public static void LevelFormula_MatchesCookieClicker()
    {
        GameBalance balance = new();

        Check.Equal(0, PrestigeSystem.LevelFor(0, balance));
        Check.Equal(0, PrestigeSystem.LevelFor(1, balance));
        Check.Equal(0, PrestigeSystem.LevelFor(1e12 - 1, balance));
        Check.Equal(1, PrestigeSystem.LevelFor(1e12, balance));
        Check.Equal(2, PrestigeSystem.LevelFor(8e12, balance));
        Check.Equal(3, PrestigeSystem.LevelFor(2.7e13, balance));
        Check.Equal(10, PrestigeSystem.LevelFor(1e15, balance));
        Check.Equal(100, PrestigeSystem.LevelFor(1e18, balance));
    }

    [Test]
    public static void LevelFormula_IsMonotonic()
    {
        GameBalance balance = new();
        int previous = 0;
        for (double earned = 1e12; earned < 1e30; earned *= 2.5)
        {
            int level = PrestigeSystem.LevelFor(earned, balance);
            Check.AtLeast(level, previous);
            previous = level;
        }
    }

    [Test]
    public static void CookiesForLevel_IsInverseOfLevelFor()
    {
        GameBalance balance = new();
        for (int level = 1; level <= 60; level++)
        {
            double required = PrestigeSystem.CookiesForLevel(level, balance);
            Check.Equal(level, PrestigeSystem.LevelFor(required, balance), $"等级 {level} 的反函数不自洽。");
            Check.Equal(level - 1, PrestigeSystem.LevelFor(required * 0.999, balance), $"等级 {level} 的边界有误。");
        }
    }

    [Test]
    public static void Preview_ReportsProgressTowardsNextLevel()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.CookiesEarnedAllTime = 4e12;
        engine.MarkDirty();

        PrestigePreview preview = PrestigeSystem.Preview(engine);

        Check.Equal(0, preview.CurrentLevel);
        Check.Equal(1, preview.NextLevel);
        Check.Close(1.0, preview.ChipsOnAscend, 1e-9);
        Check.True(preview.CanAscend);
        Check.Close(8e12, preview.CookiesForNextLevel, 1.0);
        Check.Close(3.0 / 7.0, preview.Progress, 1e-6);
    }

    [Test]
    public static void Ascend_FailsWithoutLevelGain()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.CookiesEarnedAllTime = 5e11; // 不足 1 兆
        engine.MarkDirty();

        AscensionResult result = engine.Ascend();

        Check.False(result.Success);
        Check.Equal(0, engine.State.Ascensions);
        Check.Equal(0, engine.State.PrestigeLevel);
    }

    [Test]
    public static void Ascend_ResetsRun_ButKeepsPermanentAndAchievements()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.State.CookiesEarnedAllTime = 1e12;
        engine.MarkDirty();

        for (int i = 0; i < 10; i++) engine.Click();
        engine.State.Cookies = 1e12;
        engine.MarkDirty();
        engine.BuyUpgrade("warmer_hands");
        engine.BuyBuilding("curled_cat", 10);

        engine.State.PrestigeChips = 100;
        engine.MarkDirty();
        Check.True(engine.BuyUpgrade("eternal_paw").Success, "天堂升级应可用猫薄荷购买。");

        int achievementsBefore = engine.State.Achievements.Count;
        Check.AtLeast(achievementsBefore, 1);

        AscensionResult result = engine.Ascend();

        Check.True(result.Success);
        Check.Equal(1, engine.State.PrestigeLevel);
        Check.Close(1.0, result.ChipsGained, 1e-9);
        Check.Equal(1, engine.State.Ascensions);

        // 本轮进度清零
        Check.Close(0.0, engine.State.Cookies);
        Check.Close(0.0, engine.State.CookiesEarnedThisRun);
        Check.Equal(0, engine.State.BuildingCount("curled_cat"));
        Check.Equal(0, engine.State.UpgradeCount("warmer_hands"));
        Check.Equal(0, engine.State.Buffs.Count);

        // 跨转生保留
        Check.Equal(1, engine.State.UpgradeCount("eternal_paw"));
        Check.Equal(achievementsBefore, engine.State.Achievements.Count);
        Check.AtLeast(engine.State.CookiesEarnedAllTime, 1e12, "历史累计赚取不应被转生清零。");
    }

    [Test]
    public static void Ascend_SecondTime_Onwards()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.CookiesEarnedAllTime = 1e12;
        engine.MarkDirty();
        Check.True(engine.Ascend().Success);

        engine.State.CookiesEarnedAllTime = 8e12;
        engine.MarkDirty();
        AscensionResult second = engine.Ascend();

        Check.True(second.Success);
        Check.Equal(2, engine.State.PrestigeLevel);
        Check.Close(1.0, second.ChipsGained, 1e-9);
        Check.Equal(2, engine.State.Ascensions);
        Check.Close(2.0, engine.State.PrestigeChips, 1e-9);
    }

    [Test]
    public static void HeavenlyUpgrade_SurvivesAscension_And_Applies()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.State.PrestigeChips = 50;
        engine.MarkDirty();
        engine.BuyUpgrade("time_lord_cat"); // 所有建筑 ×1.15

        engine.State.CookiesEarnedAllTime = 1e12;
        engine.MarkDirty();
        engine.Ascend();

        engine.State.Cookies = 1e6;
        engine.State.CookiesEarnedThisRun = 1e6;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);

        Check.CloseRelative(1.0 * 1.15, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void ResetRun_KeepsLifetimeCounters()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.Click();
        engine.State.CookiesEarnedAllTime = 1e12;
        engine.MarkDirty();
        double clicksBefore = engine.State.TotalClicks;

        engine.Ascend();

        Check.Close(clicksBefore, engine.State.TotalClicks);
    }
}
