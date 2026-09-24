using NekoClicker.Core.Content;
using NekoClicker.Content.Neko;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 框架完备性：内容校验的补漏、解锁可达性分析、条件指标对称性、模块离线生命周期。
/// <para>
/// 这一组用例的共同主题是<b>消灭静默失败</b>——每一条都对应"内容写错了但构建期放行、
/// 直到运行期才表现为数值不变或玩家卡死"的缺口。所以断言的重点不是"能构建"，
/// 而是"构建期必须报错"。
/// </para>
/// </summary>
public static class FrameworkTests
{
    // ---------------------------------------------------------------- 修饰符引用校验

    [Test]
    public static void Modifier_UnknownBuildingId_IsRejected()
    {
        // 修前：只检查 id 非空 → BuildingMultiplier("typo") 静默无效。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.BuildingMultiplier("typo", 2)],
                })
                .Build());

        Check.True(ex.Errors.Any(e => e.Contains("typo")), "错误信息应指出写错的建筑 id。");
    }

    [Test]
    public static void Modifier_UnknownBuffDurationId_IsRejected()
    {
        // 修前：BuffDuration 目标完全不校验。
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [new Modifier(ModifierTarget.BuffDuration("ghost"), ModifierOperation.Multiplicative, 2)],
                })
                .Build());
    }

    [Test]
    public static void Modifier_NonNullFiniteValue_IsRejected()
    {
        // 修前：NaN 会沿乘法链传播，把整条产量算成 NaN，且运行期极难定位。
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.GlobalPercent(double.NaN)],
                })
                .Build());

        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.GlobalMultiplier(double.PositiveInfinity)],
                })
                .Build());

        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.BuildingCount, double.NaN, Id: "b"))],
                })
                .Build());
    }

    [Test]
    public static void Modifier_TargetThatRejectsId_IsRejected()
    {
        // 全局目标带了 id，基本是把建筑 id 写错了地方。
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers =
                    [
                        new Modifier(
                            new ModifierTarget(ModifierTargetKind.GlobalCps, "b"),
                            ModifierOperation.Multiplicative,
                            2),
                    ],
                })
                .Build());
    }

    [Test]
    public static void Scaling_MissingOrUnknownBuildingId_IsRejected()
    {
        // 缺 id：metrics.BuildingCount("") 恒为 0，效果静默失效。
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.BuildingCount, 0.01))],
                })
                .Build());

        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.BuildingCount, 0.01, Id: "ghost"))],
                })
                .Build());
    }

    // ---------------------------------------------------------------- 解锁可达性

    [Test]
    public static void UnlockCycle_IsRejected()
    {
        // 建筑 b 要升级 u，升级 u 要建筑 b —— 谁也解不开，玩家永远卡死。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition
                {
                    Id = "b",
                    Name = "B",
                    BasePrice = 10,
                    BaseCps = 1,
                    Unlock = UnlockCondition.UpgradeOwned("u"),
                })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Unlock = UnlockCondition.BuildingsAtLeast("b", 1),
                })
                .Build());

        Check.True(ex.Errors.Any(e => e.Contains("永远无法解锁")), "应报告可达性问题。");
        Check.True(ex.Errors.Any(e => e.Contains("建筑「b」")), "应指出环路上的建筑。");
        Check.True(ex.Errors.Any(e => e.Contains("升级「u」")), "应指出环路上的升级。");
    }

    [Test]
    public static void UnlockOrphanChain_IsRejected()
    {
        // 不是环，而是一条同样解不开的链：成就恒假 → 升级依赖它。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new AchievementDefinition { Id = "a1", Name = "A", Unlock = UnlockCondition.Never })
                .Add(new UpgradeDefinition
                {
                    Id = "u1",
                    Name = "U1",
                    Price = 1,
                    Unlock = UnlockCondition.AchievementUnlocked("a1"),
                })
                .Build());

        Check.True(ex.Errors.Any(e => e.Contains("u1")), "应报告依赖链上的升级。");
    }

    [Test]
    public static void UnlockReachability_DoesNotFlagAnyWithReachableBranch()
    {
        // Any 有一条可达分支就必须判定为可达——不能把"部分分支不可达"误报成整体不可达。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b1", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new AchievementDefinition { Id = "aDead", Name = "A", Unlock = UnlockCondition.Never })
                .Add(new UpgradeDefinition
                {
                    Id = "uDead",
                    Name = "UD",
                    Price = 1,
                    Unlock = UnlockCondition.AchievementUnlocked("aDead"),
                })
                .Add(new UpgradeDefinition
                {
                    Id = "uOk",
                    Name = "UO",
                    Price = 1,
                    Unlock = UnlockCondition.Any(
                        UnlockCondition.BuildingsAtLeast("b1", 1),
                        UnlockCondition.AchievementUnlocked("aDead")),
                })
                .Build());

        Check.True(ex.Errors.Any(e => e.Contains("uDead")), "真正不可达的 uDead 应被报告。");
        Check.False(ex.Errors.Any(e => e.Contains("uOk")), "有可达分支的 uOk 不应被误报。");
    }

    [Test]
    public static void UnlockReachability_TreatsCustomConditionAsReachable()
    {
        // 自定义谓词无法静态分析：宁可漏报也不误报，否则内容作者会被迫绕过校验。
        GameContent content = new GameContentBuilder("X")
            .Add(new UpgradeDefinition
            {
                Id = "u",
                Name = "U",
                Price = 1,
                Unlock = UnlockCondition.Custom("由外部系统决定", _ => false),
            })
            .Build();

        Check.NotNull(content.UpgradeById["u"]);
    }

    [Test]
    public static void UnlockReachability_AcceptsChainedGating()
    {
        // 正常的链式门控不能被误伤：点击 → 升级 A → 升级 B，以及建筑门控。
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "b1", Name = "B1", BasePrice = 10, BaseCps = 1 })
            .Add(new BuildingDefinition
            {
                Id = "b2",
                Name = "B2",
                BasePrice = 100,
                BaseCps = 8,
                Unlock = UnlockCondition.BuildingsAtLeast("b1", 5),
            })
            .Add(new UpgradeDefinition
            {
                Id = "u1",
                Name = "U1",
                Price = 1,
                Unlock = UnlockCondition.ClicksAtLeast(10),
            })
            .Add(new UpgradeDefinition
            {
                Id = "u2",
                Name = "U2",
                Price = 1,
                Unlock = UnlockCondition.All(
                    UnlockCondition.UpgradeOwned("u1"),
                    UnlockCondition.BuildingsAtLeast("b2", 1)),
            })
            .Add(new AchievementDefinition
            {
                Id = "a1",
                Name = "A1",
                Unlock = UnlockCondition.All(
                    UnlockCondition.BuildingsAtLeast("b2", 1),
                    UnlockCondition.UpgradeOwned("u2")),
            })
            .Build();

        Check.Equal(2, content.Buildings.Count);
        Check.Equal(2, content.Upgrades.Count);
        Check.NotNull(content.AchievementById["a1"]);
    }

    [Test]
    public static void ExistingNekoContent_PassesTheNewValidator()
    {
        // 回归护栏：新增的可达性分析不能把现有内容包判死。
        GameContent content = NekoClicker.Content.Neko.NekoContent.Build();

        Check.AtLeast(content.Buildings.Count, 10);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
    }

    // ---------------------------------------------------------------- 条件指标对称性

    [Test]
    public static void CounterCondition_ReadsCounterAndReportsProgress()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Counters["happiness"] = 120;

        UnlockCondition condition = UnlockCondition.Counter("happiness", 500);

        Check.False(condition.IsMet(engine.Metrics, engine.Content));
        Check.True(condition.TryGetProgress(engine.Metrics, out double current, out double target),
            "计数器条件必须能给出进度，否则灰按钮/图鉴就没有进度条。");
        Check.Close(120, current);
        Check.Close(500, target);
        Check.Contains(condition.Describe(engine.Content), "happiness");

        engine.State.Counters["happiness"] = 500;
        Check.True(condition.IsMet(engine.Metrics, engine.Content));
    }

    [Test]
    public static void CounterCondition_MissingKey_IsRejected()
    {
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Unlock = new NumericCondition(NumericMetric.Counter, 10),
                })
                .Build());
    }

    [Test]
    public static void TaggedUpgradesCondition_ReadsTaggedCount()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        for (int i = 0; i < 10; i++) engine.Click();
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        Check.False(UnlockCondition.TaggedUpgradesAtLeast("click", 1).IsMet(engine.Metrics, engine.Content));

        Check.True(engine.BuyUpgrade("warmer_hands").Success); // Tags = ["click"]

        Check.True(UnlockCondition.TaggedUpgradesAtLeast("click", 1).IsMet(engine.Metrics, engine.Content));
        Check.False(UnlockCondition.TaggedUpgradesAtLeast("kitten", 1).IsMet(engine.Metrics, engine.Content));
    }

    [Test]
    public static void TaggedUpgradesCondition_MissingTag_IsRejected()
    {
        Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Unlock = new NumericCondition(NumericMetric.TaggedUpgrades, 1),
                })
                .Build());
    }

    // ---------------------------------------------------------------- 模块离线生命周期

    [Test]
    public static void Modules_ReceiveOnOffline()
    {
        var probe = new OfflineProbe();
        GameContent content = new GameContentBuilder("M")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .Add(probe)
            .Build();

        var clock = new ManualClock();
        var engine = new GameEngine(content, new GameEngineOptions { Clock = clock, Seed = 5 });
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("b", 10);

        string json = engine.Save();
        clock.Advance(2 * 3600);
        OfflineProgress? offline = engine.Load(json);

        Check.True(offline.HasValue);
        Check.Equal(1, probe.Calls, "模块应在离线结算后收到一次 OnOffline。");
        if (probe.Last is not { } got)
        {
            Check.Fail("模块未收到 OnOffline。");
            return;
        }
        Check.Close(2 * 3600, got.CreditedSeconds, 1e-6);
        Check.Greater(got.CookiesGained, 0);
    }

    [Test]
    public static void Modules_ReceiveOnTickBeforeAnyOffline()
    {
        var probe = new OfflineProbe();
        GameContent content = new GameContentBuilder("M")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .Add(probe)
            .Build();

        var engine = new GameEngine(content, new GameEngineOptions { Clock = new ManualClock(), Seed = 5 });
        engine.Simulate(1);

        Check.AtLeast(probe.Ticks, 30);
        Check.Equal(0, probe.Calls, "没有离线时长时不应触发 OnOffline。");
    }

    private sealed class OfflineProbe : IGameModule
    {
        public string Name => "offline-probe";

        public int Calls { get; private set; }

        public int Ticks { get; private set; }

        public OfflineProgress? Last { get; private set; }

        public void OnTick(GameEngine engine, double deltaSeconds) => Ticks++;

        public void OnOffline(GameEngine engine, OfflineProgress progress)
        {
            Calls++;
            Last = progress;
        }
    }
}
