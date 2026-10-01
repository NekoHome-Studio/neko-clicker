using NekoClicker.Content.Cafe;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 长时间模拟与扩展点。<para>
/// 这一组用例的价值在于：单元测试能证明"公式是对的"，只有连续跑几小时的模拟才能证明
/// "曲线是活的"——玩家真的会买到新建筑、解锁成就、点中金猫，而且不会出现 NaN/∞。
/// </para>
/// </summary>
public static class SimulationTests
{
    [Test]
    public static void SixHourGreedyRun_IsStableAndProgresses()
    {
        GameEngine engine = TestGame.CreateNeko(out _, seed: 4242);

        RunGreedy(engine, rounds: 200); // 200 × 108 秒 ≈ 6 小时

        Check.Finite(engine.State.Cookies);
        Check.Finite(engine.CookiesPerSecond);
        Check.Finite(engine.State.CookiesEarnedThisRun);
        Check.Greater(engine.State.CookiesEarnedThisRun, 1e6, "6 小时后应已有可观产出。");
        Check.AtLeast(engine.State.Achievements.Count, 5);
        Check.AtLeast(engine.State.TotalBuildings(), 20);
        Check.AtLeast(engine.State.GoldenCookiesClicked, 1, "6 小时内至少应点中一只金猫。");
        Check.AtLeast(engine.State.BuildingCount("cat_bed"), 1, "应已解锁并购买「猫窝」。");
    }

    /// <summary>同一个机器人跑内容包 #1：验证咖啡馆的曲线也是活的，且幸福感模块全程工作。</summary>
    [Test]
    public static void CafeSixHourGreedyRun_IsStableAndProgresses()
    {
        GameEngine engine = TestGame.CreateCafe(out _, seed: 4242);

        RunGreedy(engine, rounds: 200); // 200 × 108 秒 ≈ 6 小时

        Check.Finite(engine.State.Cookies);
        Check.Finite(engine.CookiesPerSecond);
        Check.Finite(engine.State.CookiesEarnedThisRun);
        Check.Greater(engine.State.CookiesEarnedThisRun, 1e6, "6 小时后应已有可观产出。");
        Check.AtLeast(engine.State.Achievements.Count, 5);
        Check.AtLeast(engine.State.TotalBuildings(), 20);
        Check.AtLeast(engine.State.GoldenCookiesClicked, 1, "6 小时内至少应点中一位客人。");
        Check.AtLeast(engine.State.BuildingCount("cat_tree"), 1, "应已解锁并购买「猫爬架」。");
        Check.AtLeast(
            engine.State.GetCounter(CafeContent.HappinessCounterKey),
            1,
            "幸福感模块应随 6 小时模拟持续增长。");
    }

    [Test]
    public static void SameSeed_ProducesIdenticalRuns()
    {
        Check.Equal(RunScripted(2024), RunScripted(2024));
    }

    [Test]
    public static void DifferentSeeds_DivergeInRandomContent()
    {
        double FirstSpawnX(ulong seed)
        {
            GameEngine engine = TestGame.CreateNeko(out _, seed: seed);
            return engine.SpawnGoldenCookie().X;
        }

        Check.NotEqual(FirstSpawnX(2024), FirstSpawnX(2025));
    }

    [Test]
    public static void PermanentUpgrades_MakeSubsequentRunsFaster()
    {
        double RunOnce(bool heavenly)
        {
            GameEngine engine = TestGame.CreateNeko(out _, seed: 555);
            if (heavenly)
            {
                engine.State.PrestigeChips = 100;
                engine.State.PrestigeLevel = 10;
                engine.MarkDirty();
                Check.True(engine.BuyUpgrade("time_lord_cat").Success, "永久升级应可购买。");
            }

            RunGreedy(engine, rounds: 40);
            return engine.State.CookiesEarnedThisRun;
        }

        double plain = RunOnce(false);
        double boosted = RunOnce(true);

        Check.Greater(boosted, plain, "同样的时长下，买了永久倍率升级的一轮应赚得更多。");
    }

    [Test]
    public static void Modules_ReceiveConfigureAttachAndTick()
    {
        var module = new CountingModule();

        GameContent content = new GameContentBuilder("ModuleTest")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .Add(module)
            .Build();

        Check.True(module.Configured, "模块的 Configure 应在构建期调用。");
        Check.True(content.BuffById.ContainsKey("module_buff"), "模块应能向内容里补充定义。");

        var engine = new GameEngine(content, new GameEngineOptions { Clock = new ManualClock(), Seed = 1 });
        Check.True(module.Attached, "模块的 OnAttach 应在引擎创建时调用。");

        engine.Simulate(1);

        Check.AtLeast(module.Ticks, 30, "模块应随固定步长收到 OnTick。");
    }

    [Test]
    public static void EmptyContent_EngineRunsWithoutCrash()
    {
        var engine = new GameEngine(GameContent.Empty, new GameEngineOptions
        {
            Clock = new ManualClock(),
            Seed = 3,
        });

        engine.Simulate(120);
        engine.Click();

        Check.Close(0, engine.CookiesPerSecond);
        Check.Close(1, engine.State.Cookies);
        Check.Finite(engine.State.Cookies);
    }

    /// <summary>脚本化的一轮：固定种子 + 固定操作序列，用于验证可复现性。</summary>
    private static double RunScripted(ulong seed)
    {
        GameEngine engine = TestGame.CreateNeko(out _, seed: seed);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        for (int i = 0; i < 20; i++) engine.Click();
        engine.BuyBuilding("curled_cat", 10);
        engine.Simulate(900);
        foreach (GoldenCookieSpawn spawn in engine.State.GoldenCookies.ToList())
            engine.ClickGoldenCookie(spawn.InstanceId);
        return engine.State.Cookies;
    }

    /// <summary>一个贪心 AI：先买升级，再买最贵的买得起的建筑，并在每轮内盯金猫。</summary>
    private static void RunGreedy(GameEngine engine, int rounds, double secondsPerRound = 108, int clicksPerRound = 15)
    {
        for (int round = 0; round < rounds; round++)
        {
            for (int i = 0; i < clicksPerRound; i++) engine.Click();

            BuyGreedily(engine);

            double remaining = secondsPerRound;
            while (remaining > 0)
            {
                double slice = Math.Min(5, remaining);
                engine.Simulate(slice);
                remaining -= slice;

                // 金猫只活 13 秒，以 5 秒为粒度检查才不会错过。
                for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                    engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);
            }
        }
    }

    private static void BuyGreedily(GameEngine engine)
    {
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.BuyMax);

        // 贵的升级优先
        for (int i = snapshot.Upgrades.Count - 1; i >= 0; i--)
        {
            UpgradeView upgrade = snapshot.Upgrades[i];
            if (!upgrade.IsAvailable || !upgrade.CanAfford) continue;
            if (upgrade.Currency != UpgradeCurrency.Cookies) continue;
            engine.BuyUpgrade(upgrade.Id);
        }

        // 最贵的买得起的建筑优先（原版玩家的直觉策略）
        for (int i = snapshot.Buildings.Count - 1; i >= 0; i--)
        {
            BuildingView building = snapshot.Buildings[i];
            if (!building.IsUnlocked) continue;
            if (engine.BuyBuilding(building.Id, 0).Success) break;
        }
    }

    private sealed class CountingModule : IGameModule
    {
        public string Name => "counting";

        public bool Configured { get; private set; }

        public bool Attached { get; private set; }

        public int Ticks { get; private set; }

        public void Configure(GameContentBuilder builder)
        {
            Configured = true;
            builder.Add(new BuffDefinition { Id = "module_buff", Name = "模块增益", Duration = 5 });
        }

        public void OnAttach(GameEngine engine) => Attached = true;

        public void OnTick(GameEngine engine, double deltaSeconds) => Ticks++;
    }
}
