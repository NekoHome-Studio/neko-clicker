using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 纪元内的<b>阶段</b>（1.9.0）：声明、派生、播报、存档与休眠。<para>
/// 这一套的失效方式<b>全是沉默的</b>——门槛写高了那一阶段永远不出现、门槛写错了界面只是
/// 少一段进度、播报重放只是多一条通知、没声明阶段的包多出一行也没人会发现。
/// 所以下面每一条都对着一种沉默：结构用<b>写死的金表</b>钉（不是照镜子重算一遍），
/// 判别力用<b>故障注入</b>证明（合成一个坏包，断言守卫真的会红），
/// 休眠用"另外 8 个包一个字段都不多"证明。
/// </para>
/// </summary>
public static class EraStageTests
{
    // ---------------------------------------------------------------- 结构（公司包 = 第一个试点）

    /// <summary>
    /// 公司包三轮各自的阶段边界。<b>这是一张写死的金表，不是从内容里重算的</b>：
    /// 它是"这一层应该在哪几处分段"这个决定的<b>独立</b>陈述，于是内容的解锁门槛一改、
    /// 或者多出一座建筑而忘了声明阶段，这条就会红——而那正是"文档里的数必须是真的"要的。
    /// <para>
    /// 三个数从哪来：<c>TUNING_ANALYSIS</c> §4.4 量出来的"第一次买到上一层没有的东西"。
    /// 第 3 轮的 <c>headquarters</c>（解锁在本轮赚 1e9）<b>不在</b>表里——第 3 轮的完成门槛是 5e8，
    /// 它比完成门槛还高，正常流程里买不到，所以它不是一条阶段线。
    /// </para>
    /// </summary>
    private static readonly (string EraId, string[] StageIds)[] CompanyGolden =
    [
        ("garage", ["meeting_room", "outsourcing_base", "server_room", "growth_team"]),
        ("series_a",
            ["meeting_room", "outsourcing_base", "server_room", "growth_team",
             "roadshow_hall", "data_center", "overseas_branch"]),
        ("ipo",
            ["meeting_room", "outsourcing_base", "server_room", "growth_team",
             "roadshow_hall", "data_center", "overseas_branch"]),
    ];

    [Test]
    public static void Company_StagesAreExactlyTheBuildingsThatOpenWithinEachEra()
    {
        GameContent content = TestGame.Company;

        foreach ((string eraId, string[] expected) in CompanyGolden)
        {
            EraDefinition era = content.Eras.Single(e => e.Id == eraId);

            Check.Equal(
                string.Join("、", expected),
                string.Join("、", era.Stages.Select(s => s.Id)),
                $"公司「{eraId}」的阶段边界与金表对不上。");

            // 阶段与解锁线必须是**同一个条件对象的值**（不是在两处各写一个数字）：
            // 不相等就说明有人单独改了一边，而运行期只会表现为"那一阶段来得早/晚了一点"。
            foreach (string buildingId in expected)
            {
                BuildingDefinition building = content.BuildingById[buildingId];
                EraStage stage = era.Stages.Single(s => s.Id == buildingId);

                Check.True(
                    stage.At == building.Unlock,
                    $"公司「{eraId}」的阶段「{buildingId}」的门槛与那座建筑的解锁条件不一致" +
                    $"（阶段 {stage.At.Describe(content)} / 建筑 {building.Unlock.Describe(content)}）——" +
                    "两处各写一个数字就会这样漂开。");

                Check.Equal(building.Name, stage.Name, $"阶段「{buildingId}」的名字应当就是那座建筑的名字。");
            }
        }
    }

    /// <summary>阶段的条数（= 边界数 + 1）与计划文档里写的那三个数一致：5 / 8 / 8。</summary>
    [Test]
    public static void Company_StageCountsPerEra_AreFiveEightEight()
    {
        GameContent content = TestGame.Company;

        foreach ((string eraId, int expectedBoundaries) in new[]
                 {
                     ("garage", 4), ("series_a", 7), ("ipo", 7),
                 })
        {
            EraDefinition era = content.Eras.Single(e => e.Id == eraId);
            Check.Equal(expectedBoundaries, era.Stages.Count, $"公司「{eraId}」的边界数变了。");

            // 界面上报的是"共几个阶段"，它含开场那一个——这里把它也钉住，
            // 免得将来有人把 Count 的意思改成"边界数"而文档还写着 5/8/8。
            GameEngine engine = TestGame.CreateCompany(out _);
            engine.State.Era = era.Index;
            engine.State.CookiesEarnedThisRun = 0;
            engine.MarkDirty();
            Check.Equal(expectedBoundaries + 1, EraSystem.Stage(engine).Count, $"公司「{eraId}」的阶段总数变了。");
        }
    }

    /// <summary>第 3 轮的最后一座建筑高过本轮的完成门槛，所以它<b>不该</b>是一条阶段线。</summary>
    [Test]
    public static void Company_Headquarters_IsNotAStageBecauseItSitsAboveTheFinalCompletion()
    {
        GameContent content = TestGame.Company;
        EraDefinition final = content.Eras.Single(e => e.Id == "ipo");

        double ceiling = Ceiling(final.Completion)!.Value;
        double unlock = Ceiling(content.BuildingById["headquarters"].Unlock)!.Value;

        Check.True(
            unlock > ceiling,
            $"headquarters 的解锁门槛（{unlock:R}）应当高于第 3 轮的完成门槛（{ceiling:R}）——" +
            "这条断言是上面那张金表里没有它的理由；两者关系一变，阶段结构就该重新想一遍。");
        Check.True(
            final.Stages.All(s => s.Id != "headquarters"),
            "headquarters 不该出现在阶段表里：它在正常流程里买不到。");
    }

    // ---------------------------------------------------------------- 休眠（另外 8 个包）

    /// <summary>
    /// 没声明阶段的包必须<b>一个字段都不多</b>：视图里没有阶段、引擎不发通知。<para>
    /// 这是"给一个包加阶段不改核心代码、也不影响别的包"这条主张的可执行版本。
    /// 它自动扫全部内容包，所以将来新加的包也在这里面。
    /// </para>
    /// </summary>
    [Test]
    public static void PacksWithoutStages_HaveNoneAnywhere()
    {
        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            if (!content.HasEras) continue;
            if (ReferenceEquals(content, TestGame.Company)) continue;

            foreach (EraDefinition era in content.Eras)
            {
                Check.Equal(0, era.Stages.Count, $"{name} 的第 {era.Index} 层声明了阶段——它不在这次试点里。");
            }

            GameEngine engine = TestGame.Create(content);
            EraStageGate gate = EraSystem.Stage(engine);
            Check.False(gate.HasStages, $"{name} 没有阶段，EraStageGate 却报了 {gate.Count} 个。");
            Check.Equal(0, gate.Index, $"{name} 没有阶段时 Index 应当是 0。");
            Check.Equal(0, engine.CheckEraStage(), $"{name} 没有阶段，不该产生任何阶段播报。");
            Check.Equal(0, engine.Snapshot(PurchaseMode.Buy1).Era?.StageCount ?? 0, $"{name} 的视图里多出了阶段。");
        }
    }

    // ---------------------------------------------------------------- 通用守卫 + 判别力

    /// <summary>
    /// 阶段门槛必须<b>低于</b>本层的完成门槛。<para>
    /// 与 <c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 是同一条纪律、同一个理由：
    /// 门槛一旦高过本层完成线，玩家会在够到它之前就舍命走人——那一阶段<b>永远到不了</b>，
    /// 而运行期完全看不出来（它只是"一直没到"）。
    /// </para>
    /// </summary>
    [Test]
    public static void StageGates_StayBelowTheirEraCompletion()
    {
        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
            AssertStageGatesBelowTheirEraCompletion(content, name);
    }

    /// <summary>
    /// 故障注入：<b>证明上面那条守卫真的会拦</b>。<para>
    /// 合成一个"阶段门槛高过本层完成门槛"的包，断言守卫会红。没有这一条，
    /// 上面的守卫可能因为取样太稀、基线记错而永远不报警——而"一条永远不报警的守卫"
    /// 与"一条正确的守卫"在输出里长得一模一样。
    /// </para>
    /// </summary>
    [Test]
    public static void StageGateGuard_RejectsAStageAboveItsEraCompletion()
    {
        GameContent bad = new GameContentBuilder("合成：阶段高过完成门槛")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .AddEras(new EraDefinition
            {
                Index = 1,
                Id = "e1",
                Name = "一",
                Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
                // 2e5 比本层完成门槛 1e5 还高：够到它之前玩家就舍命走人了。
                Stages = [new EraStage { Id = "s1", Name = "第一段", At = UnlockCondition.EarnedThisRunAtLeast(2e5) }],
            })
            .Build();

        Check.Throws<AssertionException>(
            () => AssertStageGatesBelowTheirEraCompletion(bad, "合成包（阶段高过完成门槛）"),
            "守卫应当拦下「阶段门槛不低于本层完成门槛」——它没报警，说明这条守卫没有判别力。");
    }

    // ---------------------------------------------------------------- 派生：门槛一到就进阶段

    [Test]
    public static void StageIndex_FollowsTheDeclaredThresholds()
    {
        GameEngine engine = TestGame.CreateCompany(out _);
        double[] thresholds = [30, 330, 4_000, 45_000];
        string[] names =
        [
            TestGame.Company.BuildingById["meeting_room"].Name,
            TestGame.Company.BuildingById["outsourcing_base"].Name,
            TestGame.Company.BuildingById["server_room"].Name,
            TestGame.Company.BuildingById["growth_team"].Name,
        ];

        // 门槛下方一点点：还停在前一阶段（严格小于才算跨过）
        for (int i = 0; i < thresholds.Length; i++)
        {
            engine.State.CookiesEarnedThisRun = thresholds[i] - 1e-6;
            engine.MarkDirty();

            EraStageGate before = EraSystem.Stage(engine);
            Check.Equal(i + 1, before.Index, $"赚到 {thresholds[i] - 1e-6:R} 时不该跨过第 {i + 1} 条边界。");
            Check.Equal(names[i], before.NextName, "下一个阶段的名字应当是刚解锁的那座建筑。");

            engine.State.CookiesEarnedThisRun = thresholds[i];
            engine.MarkDirty();

            EraStageGate after = EraSystem.Stage(engine);
            Check.Equal(i + 2, after.Index, $"赚到 {thresholds[i]:R} 时应当正好跨过第 {i + 1} 条边界。");
            Check.Equal(names[i], after.Name, "刚跨过的边界应当就是当前阶段的名字。");
        }

        // 最后一条边界之后：没有下一个阶段了，进度按定义是 1（而不是编一个 100% 出来）
        engine.State.CookiesEarnedThisRun = 50_000;
        engine.MarkDirty();
        EraStageGate last = EraSystem.Stage(engine);
        Check.Equal(5, last.Index, "第 1 轮应当有 5 个阶段。");
        Check.Equal(5, last.Count, "第 1 轮应当有 5 个阶段。");
        Check.Equal(string.Empty, last.NextName, "已在最后一个阶段时不该还有下一个。");
        Check.Close(1, last.Progress, 1e-12, "已在最后一个阶段时进度应当是 1。");

        // 向下一阶段的进度：赚到 15 时离 30 差一半
        engine.State.CookiesEarnedThisRun = 15;
        engine.MarkDirty();
        Check.Close(0.5, EraSystem.Stage(engine).Progress, 1e-9, "0 → 30 的中点应当是 50%。");
    }

    /// <summary>
    /// 真实游玩一遍公司第 1 轮：阶段只进不退，且在本层完成时已经走完所有阶段。<para>
    /// 前面那条按门槛直接设值，这条走的是<b>真实曲线</b>——它才回答"机器人玩得完吗"。
    /// </para>
    /// </summary>
    [Test]
    public static void Stage_NeverGoesBackwards_AndIsFinishedBeforeTheEraIs()
    {
        GameEngine engine = TestGame.CreateCompany(out _);
        int previous = EraSystem.Stage(engine).Index;
        int highest = previous;
        bool finished = false;

        for (int round = 0; round < 240 && !finished; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);
            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);
            engine.Simulate(30);

            EraStageGate gate = EraSystem.Stage(engine);
            Check.AtLeast(gate.Index, previous, "阶段跨过之后又退回去了——阶段条件必须是单调指标。");
            previous = gate.Index;
            highest = Math.Max(highest, gate.Index);

            if (engine.EraGate.CanAdvance)
            {
                finished = true;
                Check.Equal(
                    EraSystem.Stage(engine).Count,
                    highest,
                    "本层主线完成时应当已经把这一层的阶段走完了；差着就说明有一条阶段线在本层到不了。");
            }
        }

        Check.True(finished, "机器人应当在 240 轮（约 2 游戏小时）内完成公司第 1 轮——否则这条用例没测到东西。");
    }

    // ---------------------------------------------------------------- 播报：一次、按层、读档不补发

    [Test]
    public static void StageAnnouncement_FiresOncePerBoundary()
    {
        GameEngine engine = TestGame.CreateCompany(out _);

        Check.Equal(0, engine.CheckEraStage(), "第一次检查只记基线，不该播报（进层叙事已经讲过这一层开始了）。");

        engine.State.CookiesEarnedThisRun = 30;
        engine.MarkDirty();
        Check.Equal(1, engine.CheckEraStage(), "跨过第一条边界时应当播报一次。");

        string message = engine.Notifications[^1].Message;
        Check.Contains(message, "第 2/5 阶段", $"播报里应当写清是第几阶段：<{message}>");
        Check.Contains(
            message,
            TestGame.Company.BuildingById["meeting_room"].Name,
            $"播报里应当有阶段名（给玩家看的是名字，不是 id）：<{message}>");

        Check.Equal(0, engine.CheckEraStage(), "同一条边界不该播报第二次——否则每次检查周期都响一声。");

        engine.State.CookiesEarnedThisRun = 330;
        engine.MarkDirty();
        Check.Equal(1, engine.CheckEraStage(), "跨过第二条边界时应当再播报一次。");
        Check.Contains(engine.Notifications[^1].Message, "第 3/5 阶段");
    }

    /// <summary>
    /// 存档往返：① 阶段进度是<b>派生</b>的，所以读档后还在（不需要为它存任何东西）；
    /// ② "已经播报到第几阶段"这条记录随存档走，所以读档<b>不会补发</b>一串通知。
    /// </summary>
    [Test]
    public static void StageProgress_SurvivesALoad_AndIsNotReplayed()
    {
        GameEngine engine = TestGame.CreateCompany(out _);
        engine.CheckEraStage();                       // 记基线
        engine.State.CookiesEarnedThisRun = 4_000;
        engine.MarkDirty();
        engine.CheckEraStage();                       // 播报到第 4 阶段

        Check.Equal(4, EraSystem.Stage(engine).Index, "前置条件：应当已经到第 4 阶段。");

        string json = engine.Save();
        GameEngine restored = TestGame.CreateCompany(out _);
        restored.Load(json);

        Check.Equal(
            4,
            EraSystem.Stage(restored).Index,
            "读档后阶段进度丢了——它由本轮累计派生，而那是存档里的字段。");
        Check.Equal(
            0,
            restored.CheckEraStage(),
            "读档补发了阶段播报：一次读档会刷出一串通知。");
    }

    /// <summary>
    /// 播报的记录<b>按层分键</b>。<para>
    /// 计数器跨层保留（<c>PrestigeSystem.ResetRun</c> 刻意不清空），所以只存一个"最高阶段"
    /// 会让第 2 层一进层就以为自己已经播报到第 4 阶段——这条用例就是钉这件事的。
    /// </para>
    /// </summary>
    [Test]
    public static void StageAnnouncement_IsScopedToItsEra()
    {
        GameEngine engine = TestGame.CreateCompany(out _);
        engine.CheckEraStage();
        engine.State.CookiesEarnedThisRun = 4_000;
        engine.MarkDirty();
        engine.CheckEraStage();

        Check.True(
            engine.State.Counters.ContainsKey(EraSystem.StageCounterPrefix + "garage"),
            "第 1 轮的播报记录应当记在 $era_stage_garage 上（键前缀是公开约定）。");

        // 舍命进第 2 轮：本层累计清零（这就是 ResetRun 干的事），阶段回到第 1 个。
        engine.State.Era = 2;
        engine.State.CookiesEarnedThisRun = 0;
        engine.State.BuildingCounts.Clear();
        engine.MarkDirty();

        Check.Equal(1, EraSystem.Stage(engine).Index, "新的一层应当从第 1 阶段开始。");
        Check.Equal(0, engine.CheckEraStage(), "新的一层第一次检查只记基线。");

        engine.State.CookiesEarnedThisRun = 30;
        engine.MarkDirty();
        Check.Equal(1, engine.CheckEraStage(), "第 2 轮跨过第一条边界时应当照常播报（键不同，不会被第 1 轮的记录压住）。");
        Check.True(
            engine.State.Counters.ContainsKey(EraSystem.StageCounterPrefix + "series_a"),
            "第 2 轮的记录应当另起一个键。");
    }

    // ---------------------------------------------------------------- 构建期：写坏的阶段必须报错

    [Test]
    public static void StageWithoutACondition_IsRejectedAtBuildTime()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("合成：阶段没写条件")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .AddEras(new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "一",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
                    // 忘了写 At：默认是 Never，这一阶段永远打不开——而运行期只是个"一直没到"。
                    Stages = [new EraStage { Id = "s1", Name = "第一段" }],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "没有写 At 条件");
    }

    [Test]
    public static void StageWithADecayingMetric_IsRejectedAtBuildTime()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("合成：阶段挂在会掉的指标上")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .AddEras(new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "一",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
                    // 当前存量会被花掉：界面上「第 k/n 阶段」会来回跳。
                    Stages = [new EraStage { Id = "s1", Name = "第一段", At = UnlockCondition.CookiesAtLeast(50) }],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "会下降的指标");
    }

    [Test]
    public static void DuplicateStageId_IsRejectedAtBuildTime()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("合成：阶段 id 重复")
                .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
                .AddEras(new EraDefinition
                {
                    Index = 1,
                    Id = "e1",
                    Name = "一",
                    Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
                    Stages =
                    [
                        new EraStage { Id = "s1", Name = "第一段", At = UnlockCondition.EarnedThisRunAtLeast(10) },
                        new EraStage { Id = "s1", Name = "又一段", At = UnlockCondition.EarnedThisRunAtLeast(20) },
                    ],
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "阶段 id 重复");
    }

    // ---------------------------------------------------------------- 视图：两个宿主看到的东西

    [Test]
    public static void EraView_CarriesTheStage_AndHidesItWhenThereAreNone()
    {
        GameEngine company = TestGame.CreateCompany(out _);
        company.State.CookiesEarnedThisRun = 100;
        company.MarkDirty();

        EraView era = company.Snapshot(PurchaseMode.Buy1).Era!;
        Check.Equal(2, era.StageIndex, "赚到 100 时应当在第 2 阶段（刚跨过 30）。");
        Check.Equal(5, era.StageCount);
        Check.Equal(
            TestGame.Company.BuildingById["meeting_room"].Name,
            era.StageName,
            "当前阶段的名字应当是刚跨过的那条边界。");
        Check.Equal(
            TestGame.Company.BuildingById["outsourcing_base"].Name,
            era.StageNextName,
            "下一个阶段的名字应当是下一座会解锁的建筑。");
        Check.False(string.IsNullOrEmpty(era.StageProgressText), "阶段门槛的文本不该是空的——那一行要显示它。");

        // 没声明阶段的包：视图里必须是 0，宿主据此整块隐藏。
        EraView other = TestGame.CreateNineLives(out _).Snapshot(PurchaseMode.Buy1).Era!;
        Check.Equal(0, other.StageCount, "九命没有声明阶段，视图里不该有阶段。");
        Check.Equal(0, other.StageIndex, "九命没有声明阶段，StageIndex 应当是 0。");
        Check.Equal(string.Empty, other.StageName);
        Check.Equal(string.Empty, other.StageNextName);
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 通用守卫：内容包里每一条阶段门槛都必须严格低于它所在层的完成门槛。
    /// 单独抽成方法是为了让故障注入那条用例能直接喂一个坏包进来（证明它会红）。
    /// </summary>
    private static void AssertStageGatesBelowTheirEraCompletion(GameContent content, string label)
    {
        foreach (EraDefinition era in content.Eras)
        {
            // 完成条件没用量级门槛（例如只考成就数）时就没有可比的上限，跳过。
            if (Ceiling(era.Completion) is not { } ceiling) continue;

            foreach (EraStage stage in era.Stages)
            {
                foreach (NumericCondition leaf in stage.At.NumericLeaves())
                {
                    // 层号 / 转生等级是**进度序号**不是量级门槛，混进来比大小没有意义。
                    if (leaf.Metric is NumericMetric.Era or NumericMetric.PrestigeLevel) continue;

                    Check.True(
                        leaf.Target < ceiling,
                        $"{label} 第 {era.Index} 层的阶段「{stage.Id}」门槛是 {leaf.Target:R}（{leaf.Metric}），"
                        + $"而本层完成门槛是 {ceiling:R}——玩家在够到它之前就舍命走人了，"
                        + "这一阶段永远到不了，而运行期完全看不出来。");
                }
            }
        }
    }

    /// <summary>条件里的「本轮累计赚取」门槛；没有这条叶子时为 <c>null</c>。</summary>
    private static double? Ceiling(UnlockCondition condition)
    {
        foreach (NumericCondition leaf in condition.NumericLeaves())
            if (leaf.Metric == NumericMetric.CookiesEarnedThisRun) return leaf.Target;

        return null;
    }
}
