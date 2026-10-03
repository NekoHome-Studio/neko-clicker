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
        // 一局的上限用「游戏小时」而不是轮数：成本才与内容规模挂钩，也才说得清"多久算走完"。
        // 实测九包走完主线在 1.4~11.8 游戏小时（每次运行都会打印），取 30 小时留足余量。
        //
        // 为什么必须去掉轮数上限：原来的 `round < 60_000` 在 0.25 秒细步下只覆盖
        // 约 4.2 模拟小时，而九命要 11.8 小时才到结局——循环被截断，ReachedEnding 是 null，
        // 断言却拿"跑到一半"的状态去算"一次游玩能结算多少转生货币"。
        // 它恰好仍然通过，于是没人发现：**代价变成了看不见，而不是红色**。
        const double deadlineHours = 30;

        foreach ((string name, GameEngine engine) in TestGame.AllEraPacks())
        {
            double deadlineSeconds = deadlineHours * 3600;

            // 「点击 × 建筑」的桥（门槛：持有 80 座建筑）必须在一局自然游玩里真的买到过。
            // 记的是"见过"而不是"结尾还持有"：普通升级会被舍命清掉，所以结尾查 UpgradeCount
            // 会把"买到过、后来又重开了一层"误判成"够不着"。形状与效果由 ClickBridgeTests 守，
            // 它们都不回答"够不够得着"这个问题——而够不着的内容不会报错，只会一直不亮。
            bool bridgeSeen = false;

            // 后期用 0.25 秒细步：末层表态门槛与完成门槛之间只隔几秒，细步长让那个形状真的出现。
            // （1.5.0 之前这里是"机器人来不及答最后一次表态"，靠 30 模拟秒的定时宽限兜住；
            //   1.5.0 到 1.6.0 之间靠"报告看过"；**1.6.0 起必须真的作答**——所以下面那行
            //   AnswerPending 才是关键，不再是"报告一下就行"。）
            while (engine.ReachedEnding is null && engine.State.PlayTimeSeconds < deadlineSeconds)
            {
                for (int i = 0; i < 8; i++) engine.Click();
                TestGame.BuyGreedily(engine);   // 只买普通升级，转生升级留给这条断言去算

                for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                    engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

                // 把待答的表态答掉（各取第一个选项）。1.6.0 起不答完就永远走不到结局，
                // 而这条用例要的正是"一次自然游玩"的完整包络。
                // 副作用照实记：机器人现在会拿到选项与主导立场的加成，产量比 1.5.0 那次高一点——
                // 这条断言问的是"买不买得起"，方向只会更宽松。
                AnswerPending(engine);

                if (engine.EraGate.CanAdvance) engine.Ascend();
                engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);

                if (!bridgeSeen) bridgeSeen = AnyClickBridgeOwned(engine);
            }

            // 到不了结局必须**明确失败**，而不是默默把预算烧完。
            // 这是这条用例最贵的一环：细步长下跑满预算要一万五千模拟秒，
            // 而原来的 `round < 60_000` 会让"永远走不到终局"表现为**变慢而不是变红**——
            // 代价从红色变成了时间，等于没有守卫。
            Check.NotNull(
                engine.ReachedEnding,
                $"{name} 在 {deadlineHours:F0} 游戏小时内没走到结局"
                + $"（当前 {engine.State.PlayTimeSeconds / 3600:F1} 小时）——"
                + $"要么它的终局条件不可达，要么末层表态始终没被答掉"
                + $"（1.6.0 起待答表态会一直拦着结局；当前待答 {engine.State.PendingChoices.Count} 项）。");

            List<UpgradeDefinition> line =
            [
                .. engine.Content.Upgrades.Where(u => u.Persistence == UpgradePersistence.Permanent),
            ];
            double total = line.Sum(u => u.Price);
            double chips = engine.State.PrestigeChips;

            Console.WriteLine(
                $"      {name}：{engine.State.PlayTimeSeconds / 3600:F1} 游戏小时走到结局，"
                + $"结算 {chips:F0} 点转生货币，永久线总价 {total:F0}"
                + $"（{string.Join(" / ", line.Select(u => u.Price.ToString("F0")))}），"
                + $"点击桥买到过：{(bridgeSeen ? "是" : "否")}");

            Check.True(line.Count > 0, $"{name} 没有任何永久升级。");
            Check.AtLeast(
                chips,
                total,
                $"{name} 一次自然游玩只结算出 {chips:F0} 点转生货币，"
                + $"而永久线总价 {total:F0}——这条线里有内容永远买不到。");
            Check.True(
                bridgeSeen,
                $"{name} 一局里从没买到过「点击 × 建筑」的桥（门槛是持有 80 座建筑）——"
                + $"它在自然游玩里够不着。这类缺陷不会报错，只会「一直不亮」；"
                + $"先按实测包络降门槛或降价，不要直接放宽这条断言。");
        }
    }

    // 纪元包的清单从 TestGame.AllEraPacks() 派生，不在这里硬编码。
    // 原因是一个真实踩过的坑：这份清单原本是手写的，而"通用守卫会自动覆盖新包"这个假设
    // 对它并不成立——阶段 5 的三个包各自独立发现"自己的永久线没人守"，然后各自补了一行。
    // 派生之后，新包只要进了 AllContentPacks() 就自动被这条守卫扫到。

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

    /// <summary>
    /// 把待答的表态答掉（各取第一个选项）。<para>
    /// 机器人此前从不应答表态，靠"宿主报告展示过"才走到结局（1.5.0 的判据）。
    /// 1.6.0 起判据是<b>作答</b>，所以这条替代那一行——不然这三个有表态的包会永远等不到结局，
    /// 而失败信息会指向"终局条件不可达"这个错误的诊断。
    /// </para>
    /// </summary>
    /// <param name="engine">跑图中的引擎。</param>
    private static void AnswerPending(GameEngine engine)
    {
        foreach (ChoiceDefinition choice in engine.Content.Choices)
        {
            if (!engine.State.PendingChoices.Contains(choice.Id)) continue;
            Check.True(engine.AnswerChoice(choice.Id, choice.Options[0].Id), $"作答 {choice.Id} 失败。");
        }
    }

    /// <summary>
    /// 本包是否已经买了「点击 × 建筑」的桥——即**唯一**那条"点击收益上带成长曲线"的升级。
    /// 形状与效果由 <see cref="ClickBridgeTests"/> 守；这里只问"这一局里够不够得着"。
    /// </summary>
    /// <param name="engine">跑图中的引擎。</param>
    private static bool AnyClickBridgeOwned(GameEngine engine)
    {
        foreach (UpgradeDefinition upgrade in engine.Content.Upgrades)
        {
            bool isBridge = upgrade.Modifiers.Any(
                m => m.Target.Kind == ModifierTargetKind.ClickPower && m.Scaling is not null);
            if (isBridge && engine.State.UpgradeCount(upgrade.Id) > 0) return true;
        }
        return false;
    }
}
