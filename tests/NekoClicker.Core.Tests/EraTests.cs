using NekoClicker.Content.NineLives;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 分层转生（Era）系统 —— 阶段 1 的核心验收。<para>
/// 四条特有验收对应 ROADMAP §9：
/// <list type="number">
///   <item>完成条件单调性：构建期白名单校验真的会拦下会下降的指标。</item>
///   <item>灰按钮：未完成时 <c>CanAdvance=false</c> 且原因非空、内容正确。</item>
///   <item>A2 架构不变量：核心内不出现层号特判（在 ArchitectureTests 里）。</item>
///   <item>G4 全程可达：机器人从第 1 层走到第 9 层，证明内容不会卡死。</item>
///   <item>R3 的运行期侧：真实游玩中**每层进度只进不退**、舍命按钮不闪
///     （构建期只守得住 <c>Cps</c> 那类明禁指标，计数器要靠这条兜底）。</item>
/// </list>
/// </para>
/// </summary>
public static class EraTests
{
    // ---------------------------------------------------------------- 内容包本身

    [Test]
    public static void NineLives_BuildsWithNineContiguousEras()
    {
        GameContent content = NineLivesContent.Build();

        Check.Equal(9, content.Eras.Count);
        Check.True(content.HasEras);
        Check.Equal(9, content.MaxEraIndex);

        // 层号必须从 1 连续到 9，否则"逐级推进"会断链。
        for (int index = 1; index <= 9; index++)
        {
            Check.NotNull(content.FindEra(index));
            Check.Equal(index, content.Eras[index - 1].Index);
        }

        Check.Equal(12, content.Buildings.Count);
        // 63 条常规成就 + 5 条结局成就（阶段 3B）。
        Check.Equal(68, content.Achievements.Count);
        Check.AtLeast(content.Upgrades.Count, 45);
    }

    [Test]
    public static void NineLives_EveryEraHasAMeaningfulRuleChange()
    {
        GameContent content = NineLivesContent.Build();

        // 每层都必须换掉至少一条规则，否则"推进纪元"就只是重复劳动。
        for (int index = 2; index <= 9; index++)
        {
            EraDefinition era = content.FindEra(index)!;
            bool changesRule = era.Balance is not null
                               || era.Modifiers.Count > 0
                               || era.MetaRewardMultiplier != 1.0;
            Check.True(changesRule, $"第 {index} 层没有改变任何规则（Balance / Modifiers / MetaRewardMultiplier 全为默认）。");
        }
    }

    [Test]
    public static void NineLives_CompletionConditionsUseOnlyMonotonicMetrics()
    {
        GameContent content = NineLivesContent.Build();

        NumericMetric[] forbidden =
        [
            NumericMetric.CurrentCookies, NumericMetric.Cps,
            NumericMetric.BuildingCount, NumericMetric.TotalBuildings,
            NumericMetric.PrestigeChips, NumericMetric.Era,
        ];

        foreach (EraDefinition era in content.Eras)
        {
            foreach (NumericCondition condition in era.Completion.NumericLeaves())
            {
                Check.False(
                    forbidden.Contains(condition.Metric),
                    $"第 {era.Index} 层的完成条件用了会下降的指标 {condition.Metric}。");
            }

            Check.True(era.CompletionHint.Length > 0, $"第 {era.Index} 层缺少 CompletionHint（灰按钮上没有提示可显示）。");
        }
    }

    // ---------------------------------------------------------------- 单调性校验

    [Test]
    public static void MonotonicValidator_RejectsSpendableMetrics()
    {
        // 当前存量会被花掉 → 进度会倒退 → 构建期必须拦住。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            BuildWithEra(UnlockCondition.CookiesAtLeast(1e6)));

        Check.Contains(string.Join("\n", ex.Errors), "CurrentCookies");
    }

    [Test]
    public static void MonotonicValidator_RejectsCps()
    {
        // Cps 会随增益到期掉下来，灰按钮会闪烁。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            BuildWithEra(UnlockCondition.CpsAtLeast(1e9)));

        Check.Contains(string.Join("\n", ex.Errors), "Cps");
    }

    [Test]
    public static void MonotonicValidator_RejectsBuildingCount()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            BuildWithEra(UnlockCondition.BuildingsAtLeast("b", 50)));

        Check.Contains(string.Join("\n", ex.Errors), "BuildingCount");
    }

    [Test]
    public static void MonotonicValidator_AcceptsEarningsAndCounters()
    {
        GameContent content = BuildWithEra(UnlockCondition.All(
            UnlockCondition.EarnedThisRunAtLeast(1e6),
            UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e3)));

        Check.Equal(1, content.Eras.Count);
    }

    [Test]
    public static void EraValidator_RejectsNonContiguousIndexes()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .AddEras(
                    new EraDefinition { Index = 1, Id = "e1", Name = "一" },
                    new EraDefinition { Index = 3, Id = "e3", Name = "三" })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "不连续");
    }

    [Test]
    public static void EraValidator_RejectsUnknownInheritBuilding()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .Add(new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "一",
                    InheritBuildings = ["ghost"],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "ghost");
    }

    // ---------------------------------------------------------------- 灰按钮

    [Test]
    public static void Gate_IsClosedAtStart_WithReasonAndZeroProgress()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);

        EraGate gate = engine.EraGate;

        Check.False(gate.CanAdvance, "开局不该能舍命。");
        Check.Equal(1, gate.CurrentIndex);
        Check.Equal(2, gate.NextIndex);
        Check.NotNull(gate.BlockedReason);
        Check.Contains(gate.BlockedReason!, "100,000");
        Check.Close(0, gate.Progress, 1e-9);
    }

    [Test]
    public static void Gate_OpensWhenCompletionIsMet()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        engine.State.CookiesEarnedThisRun = 2e5;
        engine.MarkDirty();

        EraGate gate = engine.EraGate;

        Check.True(gate.CanAdvance);
        Check.Null(gate.BlockedReason);
        Check.Close(1.0, gate.Progress, 1e-9);
    }

    [Test]
    public static void Gate_ProgressIsTheLeastAdvancedRequirement()
    {
        // 第 2 层要求「累计 1e8」且「8 个成就」。只满足一半时，
        // 灰按钮应当显示最落后的那一项——这靠 AllCondition 的进度实现。
        GameEngine engine = TestGame.CreateNineLives(out _);
        engine.State.CookiesEarnedThisRun = 1e5;
        engine.MarkDirty();
        Check.True(engine.Ascend().Success); // → 第 2 层

        engine.State.CookiesEarnedThisRun = 1e8; // 赚取达标，成就还差
        engine.MarkDirty();
        engine.CheckAchievements();

        EraGate gate = engine.EraGate;
        Check.False(gate.CanAdvance);
        Check.Contains(gate.BlockedReason!, "成就");
        Check.True(gate.Progress is > 0 and < 1, $"进度应当介于 0 和 1 之间，实际 {gate.Progress}");
    }

    [Test]
    public static void Advance_FailsWhileGateIsClosed_AndChangesNothing()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);

        AscensionResult result = engine.Ascend();

        Check.False(result.Success);
        Check.Equal(1, engine.State.Era);
        Check.Equal(0, engine.State.Ascensions);
        Check.Equal(0, engine.State.EraCompleted.Count);
    }

    // ---------------------------------------------------------------- 舍命

    [Test]
    public static void Advance_MovesToNextEra_AndResetsTheRun()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);
        engine.BuyBuilding("cardboard_box", 20);
        Check.AtLeast(engine.State.BuildingCount("cardboard_box"), 20);

        AscensionResult result = engine.Ascend();

        Check.True(result.Success);
        Check.True(result.EraAdvanced);
        Check.Equal(2, result.Era);
        Check.Equal(2, engine.State.Era);
        Check.Equal(1, engine.State.Ascensions);
        Check.True(engine.State.EraCompleted.Contains(1));
        Check.True(engine.State.EraHistory.ContainsKey(1));
        Check.Equal(0, engine.State.BuildingCount("cardboard_box"));
        Check.Close(0, engine.State.Cookies);
        Check.Close(0, engine.State.CookiesEarnedThisRun);

        EraRecord record = engine.State.EraHistory[1];
        Check.Equal(1, record.Index);
        Check.Greater(record.CookiesEarned, 0, "本层记录应当留下累计赚取。");
        Check.AtLeast(record.PlayTimeSeconds, 0);
    }

    [Test]
    public static void Advance_KeepsAchievementsAndCounters()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);
        engine.State.Counters["faith"] = 12345;
        int achievementsBefore = engine.State.Achievements.Count;

        AscensionResult result = engine.Ascend();

        Check.True(result.Success);
        Check.Close(12345, engine.State.Counters["faith"], 1e-9, "计数器必须跨命保留。");
        Check.AtLeast(engine.State.Achievements.Count, achievementsBefore);
    }

    [Test]
    public static void Advance_GrantsZeroChipsWhenLevelDoesNotRise()
    {
        // R5：情感能量可以给 0，故事照样推进——绝不用数值增长来锁剧情。
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);

        AscensionResult result = engine.Ascend();

        Check.True(result.Success);
        Check.Close(0, result.ChipsGained, 1e-9);
        Check.Equal(0, engine.State.PrestigeLevel, "2e5 的累计赚取还不足以抬升等级，所以等级仍是 0。");
        Check.Equal(2, engine.State.Era);
    }

    [Test]
    public static void Advance_AppliesNextEraModifiers()
    {
        // 第 3 层常驻「全局产量 ×1.5」。
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);
        engine.Ascend(); // → 2
        engine.State.CookiesEarnedThisRun = 1e9;
        engine.State.Achievements.Clear();
        for (int i = 0; i < 8; i++) engine.State.Achievements.Add($"dummy_{i}");
        engine.MarkDirty();
        Check.True(engine.Ascend().Success); // → 3
        Check.Equal(3, engine.State.Era);

        // 进入第 3 层后本轮累计归零，而建筑解锁条件依赖它，所以必须先补上。
        engine.State.Cookies = 1e9;
        engine.State.CookiesEarnedThisRun = 1e9;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("cardboard_box", 10).Success);

        // 10 × 0.1 = 1.0 基础产量，只有第 3 层的常驻「全局 ×1.5」生效。
        Check.CloseRelative(1.5, engine.CookiesPerSecond, 1e-9);
    }

    [Test]
    public static void Advance_AppliesNextEraBalance()
    {
        // 第 5 层把离线收益上限翻倍。
        GameEngine engine = TestGame.CreateNineLives(out _);
        double baseCap = engine.Balance.OfflineCapSeconds;

        GameContent content = engine.Content;
        Check.Close(baseCap * 2, content.BalanceFor(5).OfflineCapSeconds, 1e-9);
        Check.Close(baseCap, content.BalanceFor(1).OfflineCapSeconds, 1e-9);
        Check.Close(baseCap, content.BalanceFor(99).OfflineCapSeconds, 1e-9, "未定义的层应回退到基准。");
    }

    [Test]
    public static void Advance_AppliesMetaRewardMultiplier()
    {
        // 第 3 层有"道德税"：情感能量 ×0.8。
        GameContent content = TestGame.NineLives;
        Check.Close(0.8, content.FindEra(3)!.MetaRewardMultiplier, 1e-9);
    }

    [Test]
    public static void InheritBuildings_KeepsWhitelistedOnes()
    {
        // 用合成内容验证 ResetRun 的继承分支（真实包目前全是 0 继承）。
        // 语义：白名单写在"要进入的那一层"上——是这一层决定允许带进来什么。
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "kept", Name = "K", BasePrice = 10, BaseCps = 1 })
            .Add(new BuildingDefinition { Id = "lost", Name = "L", BasePrice = 10, BaseCps = 1 })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一" },
                new EraDefinition { Index = 2, Id = "e2", Name = "二", InheritBuildings = ["kept"] })
            .Build();

        var engine = new GameEngine(content, new GameEngineOptions { Clock = new ManualClock(), Seed = 3 });
        engine.State.Cookies = 1e6;
        engine.MarkDirty();
        engine.BuyBuilding("kept", 7);
        engine.BuyBuilding("lost", 5);

        Check.True(engine.Ascend().Success, "第 1 层默认无条件，应当可以舍命。");

        Check.Equal(7, engine.State.BuildingCount("kept"), "白名单里的建筑应当保留。");
        Check.Equal(0, engine.State.BuildingCount("lost"));
    }

    [Test]
    public static void InheritBuildingRatio_KeepsAProportion()
    {
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .AddEras(
                new EraDefinition { Index = 1, Id = "e1", Name = "一" },
                new EraDefinition { Index = 2, Id = "e2", Name = "二", InheritBuildingRatio = 0.5 })
            .Build();

        var engine = new GameEngine(content, new GameEngineOptions { Clock = new ManualClock(), Seed = 3 });
        engine.State.Cookies = 1e6;
        engine.MarkDirty();
        engine.BuyBuilding("b", 30);

        Check.True(engine.Ascend().Success);

        Check.Equal(15, engine.State.BuildingCount("b"), "保留比例应当向下取整。");
    }

    [Test]
    public static void ResetRun_WithoutInheritance_ClearsEverything()
    {
        // 默认继承比例是 0：经典转生的行为不能被改变。
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1e6;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 30);
        engine.State.CookiesEarnedAllTime = 1e12;
        engine.MarkDirty();

        Check.True(engine.Ascend().Success);

        Check.Equal(0, engine.State.BuildingCount("curled_cat"));
        Check.Equal(1, engine.State.Era, "没有分层的包，层号恒为 1。");
    }

    // ---------------------------------------------------------------- 存档与视图

    [Test]
    public static void SaveRoundTrip_PreservesEraState()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);
        engine.State.Counters["faith"] = 777;
        engine.Ascend(); // → 2
        engine.State.CookiesEarnedThisRun = 5e8;
        engine.MarkDirty();

        string json = engine.Save();

        GameEngine restored = TestGame.CreateNineLives(out _);
        restored.Load(json);

        Check.Equal(2, restored.State.Era);
        Check.True(restored.State.EraCompleted.Contains(1));
        Check.True(restored.State.EraHistory.ContainsKey(1));
        Check.Close(777, restored.State.Counters["faith"], 1e-9);
        Check.Close(engine.State.EraEnteredPlayTimeSeconds, restored.State.EraEnteredPlayTimeSeconds, 1e-6);
    }

    [Test]
    public static void EraView_IsNullForPacksWithoutEras()
    {
        GameSnapshot neko = TestGame.CreateNeko(out _).Snapshot();
        Check.Null(neko.Era, "没有分层转生的包，UI 应当自动隐藏舍命面板。");

        GameSnapshot nine = TestGame.CreateNineLives(out _).Snapshot();
        Check.NotNull(nine.Era);
        Check.Equal(1, nine.Era!.Index);
        Check.Equal(9, nine.Era.Total);
        Check.Equal(9, nine.Era.All.Count);
        Check.False(nine.Era.CanAdvance);
        Check.NotNull(nine.Era.BlockedReason);
        Check.Contains(nine.Era.ProgressText, "100,000");
    }

    [Test]
    public static void EraView_TracksCompletionAcrossEras()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out _, era1Earnings: 2e5);
        engine.Ascend();

        EraView era = engine.Snapshot().Era!;

        Check.Equal(2, era.Index);
        Check.True(era.All[0].Completed, "第 1 层应标记为已完成。");
        Check.False(era.All[0].Current);
        Check.True(era.All[1].Current);
    }

    // ---------------------------------------------------------------- G4：全程可达

    [Test]
    public static void G4_RobotWalksFromTheFirstEraToTheLast()
    {
        GameEngine engine = TestGame.CreateNineLives(out _, seed: 20240924);
        List<(int Era, double Hours)> timeline = [];

        const int roundSeconds = 30;
        const int maxRounds = 6000; // 50 小时模拟上限（保护性的，不是目标值）

        for (int round = 0; round < maxRounds && engine.State.Era < 9; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();

            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                AscensionResult result = engine.Ascend();
                Check.True(result.Success, $"第 {before} 层舍命失败：{result.Message}");
                timeline.Add((before, engine.State.PlayTimeSeconds / 3600));
            }

            engine.Simulate(roundSeconds);
        }

        foreach ((int era, double hours) in timeline)
            Console.WriteLine($"      第 {era} 命结束于 {hours:F1} 游戏小时");

        Check.Equal(9, engine.State.Era, $"机器人只走到第 {engine.State.Era} 命——某层的完成条件可能不可达。");
        Check.Equal(8, timeline.Count, "应当正好舍命 8 次。");
        Check.True(engine.EraGate is { CanAdvance: false, NextIndex: null }, "最后一层不该再有下一层。");
    }

    // ------------------------------------------------- 进度单调（R3 的运行期守卫）

    /// <summary>
    /// 纪元进度**只进不退**——R3（完成条件必须单调）在运行期的可观测形式，
    /// 也是"灰按钮不会闪烁"那件事的可测形式。<para>
    /// <b>为什么必须有这条守卫</b>：构建期只拦得住 <c>Cps</c> / 建筑数 / 当前货币那几类
    /// <b>明令禁止</b>的指标，而计数器（<c>UnlockCondition.Counter</c>）在白名单里——
    /// 它单调与否取决于内容怎么用它。阶段 5 之后有四个包的纪元门槛挂在计数器上
    /// （信仰 / 文化 / 算力 / 梦境能量），而"用了一个会掉的计数器"在运行期只表现为
    /// "进度条偶尔往回走一点"，没有任何报错。#9 的「被阅读度」正是那种会掉的计数器：
    /// 它没被写进完成条件，靠的是内容自觉——这条守卫就是替那份自觉兜底。
    /// </para>
    /// <summary>
    /// 它只检查单调性，不检查可达性（后者是各包 <c>RobotWalksAll…</c> 的职责）：
    /// 每个包跑一段真实游玩，**同一层之内**进度不得下降、灰按钮不得"亮了又灭"；
    /// 跨层时基线重置——新一层的主线当然从 0 开始。
    /// </para>
    /// <para>
    /// <b>只观测"完成条件里引用了计数器"的包</b>（这一步是内容派生的，不是手写清单）：
    /// 其余包的完成条件只用量级 / 成就数 / 时长那几类，构建期白名单已经替它们守住了。
    /// 这样这条守卫的代价与"真正的风险面"成正比，而不是与包的总数成正比。
    /// </para>
    /// </summary>
    [Test]
    public static void EraGate_ProgressNeverGoesBackwards()
    {
        int covered = 0;
        int skipped = 0;

        foreach ((string name, GameEngine engine) in TestGame.AllEraPacks())
        {
            if (!UsesContentCounterInCompletion(engine.Content))
            {
                skipped++;
                continue;
            }

            covered++;
            int layers = AssertProgressNeverGoesBackwards(name, engine, rounds: 1_200);

            Console.WriteLine(
                $"      {name}：{engine.State.PlayTimeSeconds / 3600:F1} 游戏小时内走过 {layers} 层，"
                + $"每层进度单调不减（末态 {engine.EraGate.Progress:P0}）");
        }

        Console.WriteLine(
            $"      观测了 {covered} 个「完成条件引用内容计数器」的包；"
            + $"跳过 {skipped} 个（只用量级 / 成就 / 时长，或只用引擎维护的峰值产量）。");

        Check.AtLeast(covered, 1, "没有任何包用计数器做完成条件——那样这条守卫就没有观测对象，它的前提需要重新审视。");
    }

    /// <summary>
    /// 该内容包的纪元完成条件里是否引用了**内容自己维护的**计数器
    /// （即决定要不要用真实游玩去观测它）。<para>
    /// <c>peak_cps</c> 被排除在外：它是引擎每个逻辑步取 <c>max</c> 维护的，
    /// 单调性由引擎保证（见 <see cref="EraSystem.PeakCpsCounterKey"/> 的注释），
    /// 观测它没有信息量。有信息量的是信仰 / 文化 / 算力 / 梦境能量 / 被阅读度这类
    /// **内容侧**计数器——它们的单调性只能靠内容自觉，而这条守卫就是那份自觉的兜底。
    /// </para>
    /// </summary>
    private static bool UsesContentCounterInCompletion(GameContent content)
        => content.Eras.Any(era => era.Completion.NumericLeaves().Any(
            leaf => leaf.Metric == NumericMetric.Counter
                    && !string.Equals(leaf.Id, EraSystem.PeakCpsCounterKey, StringComparison.Ordinal)));

    /// <summary>
    /// 故障注入：**证明上面那条守卫真的会拦**。<para>
    /// 合成一个"完成条件挂在一个会掉的计数器上"的内容包——那正是 #9「被阅读度」的形态
    /// （没人读就往下掉）。没有这条用例，上面那条守卫的判别力只是"看起来有"：
    /// 它完全可能因为取样太稀、基线记错而永远不报警——本项目对守卫的一贯要求是
    /// 拿故障注入证明它真的会红（见 <c>LoreTests.RevealConditions_AreUniqueWithinEachPack</c>）。
    /// </para>
    /// </summary>
    [Test]
    public static void MonotonicGuard_RejectsADecayingCounterInCompletion()
    {
        GameEngine engine = TestGame.Create(new GameContentBuilder("合成：会掉的计数器")
            .Add(new DrainingCounterModule())
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .AddEras(new EraDefinition
            {
                Index = 1,
                Id = "e1",
                Name = "一",
                Completion = UnlockCondition.Counter(DrainingCounterModule.CounterKey, 5_000),
                CompletionHint = "把「会被抽干的计数」攒到 5,000——可它是会掉的。",
            })
            .Build());

        Check.Throws<AssertionException>(
            () => AssertProgressNeverGoesBackwards("合成包（完成条件挂在会掉的计数器上）", engine, rounds: 200),
            "守卫应当拦下「完成条件引用了会下降的计数器」——它没报警，说明这条守卫没有判别力。");
    }

    /// <summary>
    /// 一段真实游玩，逐轮检查"同层之内进度不下降、舍命按钮不闪"。<para>
    /// 采买每 3 轮做一次：<c>BuyGreedily</c> 内部会取一次完整快照，是这条循环里最贵的一步
    /// （实测每轮约 5ms，其中绝大部分是它）。进度观测仍然逐轮做——那才是这条守卫要看的。
    /// </para>
    /// </summary>
    /// <param name="name">内容包显示名（报错信息用）。</param>
    /// <param name="engine">待观测的引擎。</param>
    /// <param name="rounds">轮数（每轮 30 游戏秒）。</param>
    /// <returns>走过的层数（跨层时基线重置，所以它只用于报告）。</returns>
    private static int AssertProgressNeverGoesBackwards(string name, GameEngine engine, int rounds)
    {
        int era = engine.State.Era;
        int layers = 1;
        double baseline = engine.EraGate.Progress;
        bool wasOpen = engine.EraGate.CanAdvance;

        for (int round = 0; round < rounds; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();

            if (round % 3 == 0) TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);

            EraGate gate = engine.EraGate;

            if (engine.State.Era != era)
            {
                era = engine.State.Era;
                layers++;
                baseline = gate.Progress;
                wasOpen = gate.CanAdvance;
                continue;
            }

            Check.AtLeast(
                gate.Progress,
                baseline - 1e-9,
                $"{name}：第 {era} 层的主线进度倒退了（{baseline:P2} → {gate.Progress:P2}）"
                + "——该层的完成条件里混进了会下降的指标（R3 明令禁止这件事）。");

            // 一亮一灭 = 玩家看到的"灰按钮闪烁"。进度单调时它不可能发生，所以这条是顺带守住的。
            if (wasOpen)
                Check.True(gate.CanAdvance, $"{name}：第 {era} 层的舍命按钮亮了又灭——完成条件在达成之后又变得不成立。");

            baseline = Math.Max(baseline, gate.Progress);
            wasOpen = gate.CanAdvance;
        }

        return layers;
    }

    /// <summary>
    /// 一条**只会掉**的计数器：创建时给一笔初始值，之后每秒被抽干 10 点。<para>
    /// 它是 #9「被阅读度」那种形态的最小复刻——只用于故障注入，不是内容。
    /// </para>
    /// </summary>
    private sealed class DrainingCounterModule : IGameModule
    {
        /// <summary>计数器键。</summary>
        public const string CounterKey = "draining";

        /// <inheritdoc />
        public string Name => "draining";

        /// <inheritdoc />
        public void Configure(GameContentBuilder builder) => builder.AddCounterName(CounterKey, "会被抽干的计数");

        /// <inheritdoc />
        public void OnAttach(GameEngine engine) => engine.State.SetCounter(CounterKey, 5_000);

        /// <inheritdoc />
        public void OnTick(GameEngine engine, double deltaSeconds)
            => engine.State.SetCounter(CounterKey, Math.Max(0, engine.State.GetCounter(CounterKey) - (10 * deltaSeconds)));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>构造一个只含一座建筑和一层纪元的最小内容，用于校验测试。</summary>
    private static GameContent BuildWithEra(UnlockCondition completion) =>
        new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .AddEras(new EraDefinition { Index = 1, Id = "e1", Name = "一", Completion = completion })
            .Build();
}
