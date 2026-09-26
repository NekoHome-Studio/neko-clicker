using NekoClicker.Content.Cyber;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #5《赛博猫娘》的验收（阶段 5）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>算力真的成立</b>——它由"常驻类"建筑产出、单调不减、迁服务器不清零，
///   而且产量跟着它走（三条成长曲线 + 8 处门槛）。</item>
///   <item><b>五层走得完</b>——层与层之间的门槛在真实曲线下够得着，而且要摊平。</item>
///   <item><b>它是内容侧实现的</b>——没有新增任何核心能力，落在已有的
///   <c>IGameModule</c> + <c>Scaling(ScalingSource.CustomCounter)</c> 上。</item>
/// </list>
/// </para>
/// </summary>
public static class CyberContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Cyber;

        Check.Equal("赛博猫娘", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五层数字层，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、两个结局。这个包和末世、图书馆一样刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(2, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);

        // 九座建筑里六座产算力——"算力不是装饰"的前提。
        Check.AtLeast(
            content.Buildings.Count(b => b.Tags.Contains(CyberContent.ComputeTag, StringComparer.Ordinal)),
            6);

        // 最底层那座**不**产算力（进程跑完就退出），这是这个包的设定而不是疏漏。
        Check.False(
            content.BuildingById["process"].Tags.Contains(CyberContent.ComputeTag, StringComparer.Ordinal),
            "「进程」不该产算力：它跑完就退出，常驻的才是算力。");
    }

    [Test]
    public static void Buildings_CurveIsSane()
    {
        // 与通用守卫同构，但在这里对**这个包**再钉一次：曲线手感是跨包不变量，
        // 换包换的是叙事不是数值。
        GameContent content = TestGame.Cyber;

        for (int i = 1; i < content.Buildings.Count; i++)
        {
            BuildingDefinition previous = content.Buildings[i - 1];
            BuildingDefinition current = content.Buildings[i];

            double priceRatio = current.BasePrice / previous.BasePrice;
            double cpsRatio = current.BaseCps / previous.BaseCps;

            Check.True(priceRatio is >= 5 and <= 20, $"「{previous.Name}」→「{current.Name}」价格倍率 {priceRatio:F2} 越界。");
            Check.True(cpsRatio is >= 3 and <= 12, $"「{previous.Name}」→「{current.Name}」产量倍率 {cpsRatio:F2} 越界。");
            if (i >= 2) Check.Greater(priceRatio, cpsRatio, $"「{current.Name}」的产量涨得比价格快，早期建筑永远不会被淘汰。");
        }
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Cyber.LoreEntries)
        {
            List<string> leaves =
            [
                .. entry.Reveal.NumericLeaves()
                    .Select(leaf => $"{leaf.Metric}:{leaf.Id}:{leaf.Target:R}")
                    .Order(StringComparer.Ordinal),
            ];

            string key = string.Join("|", leaves) + "|" + entry.Reveal.GetType().Name;
            Check.True(
                seen.TryAdd(key, entry.Id),
                $"{entry.Id} 与 {seen.GetValueOrDefault(key)} 的释放条件完全相同——它们会同时解锁。");
        }
    }

    [Test]
    public static void Orders_AreContiguousWithinEachStoryline()
    {
        GameContent content = TestGame.Cyber;

        foreach (StorylineDefinition storyline in content.Storylines)
        {
            List<LoreEntry> entries = [.. content.LoreOf(storyline.Id)];

            Check.Equal(storyline.TotalEntries, entries.Count, $"「{storyline.Name}」的条数与声明不符。");

            for (int i = 0; i < entries.Count; i++)
                Check.Equal(i + 1, entries[i].Order, $"「{storyline.Name}」的序号有空洞或重复。");
        }
    }

    [Test]
    public static void EveryStorylineOpensEarlyEnough()
    {
        int early = 0;

        foreach (StorylineDefinition storyline in TestGame.Cyber.Storylines)
        {
            LoreEntry first = TestGame.Cyber.LoreOf(storyline.Id).First();
            double target = LoreTests.FirstThreshold(first.Reveal);

            Check.Finite(target, $"「{storyline.Name}」的开篇条件无法量化。");
            Check.AtMost(target, 1e12, $"「{storyline.Name}」的开篇门槛高到几乎读不到。");
            if (target <= 400) early++;
        }

        Check.AtLeast(early, 2, "真正早期能读到的剧情线太少，图鉴开场就全是 ???。");
    }

    [Test]
    public static void G5_FirstTenMinutesRevealAtMostThreeEntries()
    {
        GameEngine engine = TestGame.CreateCyber(out _, seed: 4242);

        for (int second = 0; second < 600; second += 5)
        {
            for (int i = 0; i < 2; i++) engine.Click();
            TestGame.BuyGreedily(engine);
            engine.Simulate(5);
        }

        Check.AtMost(
            engine.State.LoreUnlocked.Count,
            3,
            $"开局 10 分钟释放了 {engine.State.LoreUnlocked.Count} 条剧情——世界观被一次性讲掉了。");
        Check.AtLeast(engine.State.LoreUnlocked.Count, 1, "开局 10 分钟一条都没放出来。");
    }

    // ------------------------------------------------------------ 算力（第二资源）

    [Test]
    public static void Compute_GrowsWithTheResidentBuildingsOnly()
    {
        // 算力的产率是"常驻类建筑的规模 × 基础产率"，与产量、增益、等级都无关。
        // 这条用例把公式钉死：改动 ComputeBuildings 的阶梯会立刻在这里露出来。
        GameEngine engine = TestGame.CreateCyber(out _);

        Check.Close(0, CyberContent.ComputeRatePerSecond(engine), 1e-9, "没有建筑时不该产算力。");

        // 「进程」不产算力：它是跑完就退出的那种。
        engine.State.BuildingCounts["process"] = 1_000;
        engine.MarkDirty();
        Check.Close(0, CyberContent.ComputeRatePerSecond(engine), 1e-9, "「进程」不该产算力。");

        // 守护进程的权重是 1² = 1。
        engine.State.BuildingCounts["daemon"] = 10;
        engine.MarkDirty();
        Check.CloseRelative(CyberContent.ComputeBaseRate * 10, CyberContent.ComputeRatePerSecond(engine), 1e-9);

        // 弃用进程池的权重是 6² = 36：**往上一层，算力快一大截**。
        engine.State.BuildingCounts["orphan_pool"] = 10;
        engine.MarkDirty();
        Check.CloseRelative(
            CyberContent.ComputeBaseRate * (10 + 10 * 36),
            CyberContent.ComputeRatePerSecond(engine),
            1e-9);
    }

    [Test]
    public static void Compute_AccumulatesAndNeverGoesBackwards()
    {
        GameEngine engine = TestGame.CreateCyber(out _);
        engine.State.BuildingCounts["container"] = 50;   // 权重 2² = 4 → 0.05 × 200 = 10/s
        engine.MarkDirty();

        engine.Simulate(60);
        double after = engine.State.GetCounter(CyberContent.ComputeCounterKey);
        Check.CloseRelative(600, after, 1e-6, "算力没有按产率累加。");

        // 把建筑全卖掉，算力也不该掉：它记的是"已经算过的东西"，不是"现在有多少机器"。
        engine.State.BuildingCounts.Clear();
        engine.MarkDirty();
        engine.Simulate(600);
        Check.Close(
            after,
            engine.State.GetCounter(CyberContent.ComputeCounterKey),
            1e-9,
            "算力掉了——它是单调不减的资源，BRIEF 明确要求不做衰减。");
    }

    [Test]
    public static void Compute_SurvivesMigration()
    {
        // 「迁服务器带走的是算力，不是机器」——这条设定是这个包能把它写进
        // 纪元完成条件的前提（会掉的资源不能进完成条件）。
        GameEngine engine = TestGame.CreateCyber(out _);
        engine.State.BuildingCounts["container"] = 50;
        engine.State.CookiesEarnedThisRun = 1e5;
        engine.State.SetCounter(CyberContent.ComputeCounterKey, 1_200);
        engine.MarkDirty();

        Check.True(engine.EraGate.CanAdvance, "第 1 层的完成条件应当已经满足。");
        engine.Ascend();

        Check.Equal(2, engine.State.Era);
        Check.Equal(0, engine.State.BuildingCount("container"), "迁服务器应当清空建筑。");
        Check.Close(
            1_200,
            engine.State.GetCounter(CyberContent.ComputeCounterKey),
            1e-9,
            "迁服务器把算力清零了——它是唯一带得走的东西。");
    }

    [Test]
    public static void Compute_AccruesOfflineWithTheSameFormula()
    {
        // 模块自己维护的计数器不经过 OnTick，离线必须显式补算，否则玩家下线一次就落后一截。
        GameEngine online = TestGame.CreateCyber(out _);
        online.State.BuildingCounts["datacenter"] = 20;   // 权重 4² = 16 → 0.05 × 320 = 16/s
        online.MarkDirty();
        online.Simulate(3_600);

        GameEngine offline = TestGame.CreateCyber(out _);
        offline.State.BuildingCounts["datacenter"] = 20;
        offline.MarkDirty();
        OfflineProgress? progress = offline.ApplyOfflineProgress(TimeSpan.FromSeconds(3_600));
        Check.NotNull(progress, "一小时的离线应当触发结算。");

        Check.CloseRelative(
            online.State.GetCounter(CyberContent.ComputeCounterKey),
            offline.State.GetCounter(CyberContent.ComputeCounterKey),
            1e-6,
            "离线和在线算出来的算力不一样——补算漏了或重复了。");
    }

    [Test]
    public static void Compute_DrivesProduction_AndTheCapIsInRawPoints()
    {
        // 这是这个包最重要的一道算术保险：Scaling 的 Cap 限的是**原始计数值**，
        // 不是加成结果（Apply = base + PerUnit × min(计数, Cap)）。
        // 断言的是**比例**而不是绝对值：算力 0 与算力 X 的产量之比，
        // 应当恰好等于 1 + Σ(PerUnit × min(X, Cap))。
        //
        // 三档的门槛按实测包络定（0.4h → 1.3e3、4.5h → 1.2e6、7h → 6.4e6），
        // 所以三个端点分别对应"刚开局""中段""已经到顶"。三条曲线各限一段，
        // 于是"Cap 限的是原始计数"这件事在每一段上都能被验出来。
        double baseline = CpsWithCompute(0);

        Check.CloseRelative(baseline, CpsWithCompute(0), 1e-9);

        // 1 点算力：+0.00005×1 + 0.00001×1 + 0.00005×1 = +0.011% —— 曲线真的从 0 开始长。
        Check.CloseRelative(
            baseline * (1 + (0.00011 * 1)),
            CpsWithCompute(1),
            1e-6,
            "算力 1 点时三条曲线都该刚刚起步。");
        // 中段 5e5 点：第一条与第三条已经封顶（Cap 4e5），第二条还差 1e6 到顶。
        Check.CloseRelative(
            baseline * (1 + (0.00005 * 400_000) + (0.00001 * 500_000) + (0.00005 * 400_000)),
            CpsWithCompute(500_000),
            1e-6,
            "中段的加成比例不对——Cap 限的是原始计数值。");

        // 到顶 2e6 点：三条全部封顶，再加多少都不动。
        double ceiling = 1 + (0.00005 * 400_000) + (0.00001 * 1_500_000) + (0.00005 * 400_000);
        Check.CloseRelative(baseline * ceiling, CpsWithCompute(2_000_000), 1e-6, "封顶处的加成比例不对。");
        Check.CloseRelative(
            CpsWithCompute(2_000_000),
            CpsWithCompute(90_000_000),
            1e-9,
            "算力超过 Cap 之后应当封顶。");
    }

    [Test]
    public static void Compute_IsReferencedByAtLeastFiveUnlockGates()
    {
        // BRIEF 的硬要求：第二资源必须真的有用——至少 5 处解锁条件 / 成就门槛引用它。
        GameContent content = TestGame.Cyber;

        int gates = 0;
        gates += content.Upgrades.Count(u => References(u.Unlock));
        gates += content.Achievements.Count(a => References(a.Unlock));
        gates += content.Eras.Count(e => References(e.Completion));
        gates += content.LoreEntries.Count(l => References(l.Reveal));
        gates += content.Endings.Count(e => References(e.Condition));
        Check.AtLeast(gates, 5, $"引用「算力」的门槛只有 {gates} 处，第二资源会退化成装饰。");

        // 成长曲线也必须有：三条（升级线三条各一段）。
        int curves = content.Upgrades
            .SelectMany(u => u.Modifiers)
            .Count(m => m.Scaling is { Source: ScalingSource.CustomCounter } s
                        && s.Id == CyberContent.ComputeCounterKey);
        Check.AtLeast(curves, 1, "算力没有任何成长曲线——它不会影响产量。");
    }

    /// <summary>
    /// 从"刚进第 5 层"出发，把主线所需的东西补齐，再设好算力与游玩时长。<para>
    /// 这样两个结局的取样点才能各自独立：算力高 → 承诺型；算力一般但时间够 → 兜底。
    /// 三条门槛（主线 5e5、承诺型 6e6、兜底 7 小时）于是能被分别踩到。
    /// </para>
    /// </summary>
    private static GameEngine FundedLastLayer(double compute, double hours)
    {
        GameEngine engine = LoadFrom(LastLayerSave.Value);

        // 算力单调递增，所以先把机群清空——否则跑几十秒它就会自己长过门槛。
        engine.State.BuildingCounts.Clear();
        engine.State.EndingsReached.Clear();
        engine.State.CookiesEarnedThisRun = 5.5e13;      // 末层主线的赚取门槛是 5e13
        engine.State.SetCounter(CyberContent.ComputeCounterKey, compute);
        engine.State.PlayTimeSeconds = hours * 3600;
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    /// <summary>
    /// 末层主线是否完成。<para>
    /// <b>不能用 <c>EraGate.CanAdvance</c></b>：最后一层没有"下一层"，
    /// 引擎刻意让那个按钮<b>永远</b>置灰（这是设计，不是缺陷），
    /// 所以末层只能直接问完成条件。
    /// </para>
    /// </summary>
    private static bool MainLineDone(GameEngine engine)
        => engine.Content.EraByIndex[engine.State.Era].Completion.IsMet(engine.Metrics, engine.Content);

    /// <summary>某个条件是否引用了「算力」计数器。</summary>
    private static bool References(UnlockCondition condition)
        => condition.NumericLeaves().Any(
            leaf => leaf.Metric == NumericMetric.Counter && leaf.Id == CyberContent.ComputeCounterKey);

    // ------------------------------------------------------------ 包络标定

    /// <summary>
    /// 把真实曲线上的包络量出来，供门槛标定（BRIEF §6：先量包络再设值）。<para>
    /// 它<b>不做断言</b>——门槛该定在哪是设计决定，不是回归基线。留着它是为了让下一次
    /// 调参有据可依：改完任何一个门槛，跑一次这个用例就能看见整条曲线的形状。
    /// </para>
    /// </summary>
    [Test]
    public static void EnvelopeProbe_ReportsTheRealCurve()
    {
        GameEngine engine = TestGame.CreateCyber(out _, seed: 20240924);
        const double step = 30;

        Console.WriteLine("      时长    层  本轮累计     每秒产量     算力       成就 病毒 图鉴 建筑");

        for (int round = 0; round < 1_440; round++)   // 1440 × 30s = 12 游戏小时
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();

            if (round % 60 == 0)
            {
                Console.WriteLine(
                    $"      {engine.State.PlayTimeSeconds / 3600,5:F1}h  {engine.State.Era}  " +
                    $"{engine.State.CookiesEarnedThisRun,11:E2}  {engine.Production.CookiesPerSecond,11:E2}  " +
                    $"{engine.State.GetCounter(CyberContent.ComputeCounterKey),10:E2}  " +
                    $"{engine.State.Achievements.Count,4} {engine.State.GoldenCookiesClicked,4} " +
                    $"{engine.State.LoreUnlocked.Count,3}/{engine.Content.LoreEntries.Count} " +
                    $"{engine.State.TotalBuildings(),5:F0}");
            }

            engine.Simulate(step);
        }
    }

    // ------------------------------------------------------------ 全程可达

    [Test]
    public static void RobotWalksAllFiveLayers()
    {
        // 与其余纪元包同构：证明这五层门槛在真实曲线下**走得完**。
        // 这个包尤其需要——迁服务器会清空全部建筑，而算力是唯一留下来的东西。
        GameEngine engine = TestGame.CreateCyber(out _, seed: 20240924);

        // 时间线按"进入第 n 层的时刻"记：第 1 层是 0 点，此后每次迁服务器记一笔。
        // 用"进入第 n+1 层的时刻 − 进入第 n 层的时刻"算每层耗时，
        // 末层的耗时就是"进入第 5 层 → 末层主线完成"，不必给整局加一个尾巴。
        List<double> layerStartHours = [0];
        List<double> earnedAtStart = [0];
        List<double> computeAtStart = [0];

        for (int round = 0; round < 120_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                engine.Ascend();
                layerStartHours.Add(engine.State.PlayTimeSeconds / 3600);
                earnedAtStart.Add(engine.State.CookiesEarnedThisRun);
                computeAtStart.Add(engine.State.GetCounter(CyberContent.ComputeCounterKey));
            }

            engine.Simulate(30);
        }

        // 末层再跑到主线完成。
        for (int round = 0; round < 20_000 && !MainLineDone(engine); round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);
            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            engine.Simulate(30);
        }

        double finishHours = engine.State.PlayTimeSeconds / 3600;
        layerStartHours.Add(finishHours);

        for (int layer = 1; layer <= 5; layer++)
        {
            Console.WriteLine(
                $"      第 {layer} 层：{layerStartHours[layer - 1],5:F1}h → {layerStartHours[layer],5:F1}h" +
                $"（{layerStartHours[layer] - layerStartHours[layer - 1],4:F1}h）" +
                $"（进层时累计 {earnedAtStart[layer - 1],9:E2}，算力 {computeAtStart[layer - 1],9:E2}）");
        }

        Console.WriteLine(
            $"      五层用时 {finishHours:F1} 小时；" +
            $"算力 {engine.State.GetCounter(CyberContent.ComputeCounterKey):E2}；" +
            $"成就 {engine.State.Achievements.Count}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.Cyber.LoreEntries.Count}；" +
            $"产率 {CyberContent.ComputeRatePerSecond(engine):E0}/s；" +
            $"结算根权限 {engine.State.PrestigeChips:F0}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 层——某层的完成条件可能不可达。");
        Check.True(MainLineDone(engine), "走到第 5 层之后主线仍然完不成——末层门槛不可达。");
        Check.Equal(6, layerStartHours.Count, "应当正好迁服务器 4 次（第 5 层不再迁）。");

        // 门槛要摊平：任何一层都不该吃掉整局的一半时间。
        for (int layer = 1; layer <= 5; layer++)
        {
            double span = layerStartHours[layer] - layerStartHours[layer - 1];
            Check.AtMost(
                span,
                finishHours * 0.6,
                $"第 {layer} 层用了 {span:F1} 小时，占整局 {finishHours:F1} 小时的一大半——门槛没摊平。");
        }
    }

    [Test]
    public static void Storylines_ReadInOrderDuringARealPlaythrough()
    {
        GameEngine engine = TestGame.CreateCyber(out _, seed: 20240924);
        Dictionary<string, int> unlockedAt = new(StringComparer.Ordinal);

        for (int round = 0; round < 60_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);

            foreach (string id in engine.State.LoreUnlocked) unlockedAt.TryAdd(id, round);
        }

        foreach (StorylineDefinition storyline in engine.Content.Storylines)
        {
            List<LoreEntry> entries = [.. engine.Content.LoreOf(storyline.Id)];

            for (int i = 0; i < entries.Count; i++)
            {
                if (!unlockedAt.ContainsKey(entries[i].Id)) continue;

                for (int j = i + 1; j < entries.Count; j++)
                {
                    if (!unlockedAt.ContainsKey(entries[j].Id)) continue;

                    Check.AtMost(
                        unlockedAt[entries[i].Id],
                        unlockedAt[entries[j].Id],
                        $"「{storyline.Name}」第 {i + 1} 条比第 {j + 1} 条晚解锁——图鉴里会看到倒挂。");
                }
            }
        }

        Console.WriteLine($"      一次完整游玩解锁 {unlockedAt.Count}/{engine.Content.LoreEntries.Count} 条叙事");
    }

    // ------------------------------------------------------------ 结局

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        // 承诺型单独可达：主线完成、算力越过 6e6，而时长还没到 7 小时。
        // 用 PlayToEra(5) 的存档当起点——它正好停在"刚进第 5 层"。
        GameEngine committed = FundedLastLayer(compute: CyberContent.ComputeForEnding * 1.1, hours: 4.5);
        Check.True(MainLineDone(committed), "前提：这个状态下末层主线应当已经完成。");
        Check.Equal("end_owner_echo", committed.ReachedEnding?.Id, "算力越过 6e6 时应当落到承诺型结局。");

        // 兜底单独可达：主线完成、时长已经越过 7 小时，但算力还停在门槛之下。
        GameEngine fallback = FundedLastLayer(compute: CyberContent.FinalLayerComputeGate * 1.2, hours: 8);
        Check.True(MainLineDone(fallback), "前提：这个状态下末层主线应当已经完成。");
        Check.Equal("end_guardian_cat", fallback.ReachedEnding?.Id, "主线完成又没继续算时应当落到兜底结局。");

        // 两个条件同时成立时取 Priority 更小的那个。
        GameEngine both = FundedLastLayer(compute: CyberContent.ComputeForEnding * 1.1, hours: 8);
        Check.True(
            both.Content.EndingById["end_guardian_cat"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下兜底结局的条件也应当是成立的。");
        Check.Equal("end_owner_echo", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }

    [Test]
    public static void EndingThresholds_AreOrdered()
    {
        // 末层主线要算力、承诺型要更多算力：两级台阶的先后不能颠倒。
        // 实际游玩的次序正是照它来的——主线在 4.0 小时处成立（算力 5.06e5），
        // 6e6 在 7 小时前后到达（见 EnvelopeProbe）。
        Check.Greater(
            CyberContent.ComputeForEnding,
            CyberContent.FinalLayerComputeGate,
            "承诺型的门槛必须高于末层主线的算力门槛，否则它会在主线开启的同一刻成立。");

        // 而兜底**自己额外加的那一条**挂在另一个指标上，这正是两条路都能走通的原因：
        // 若它也挂算力，两条门槛会在主线开启时同时成立、门槛低的那条永远赢，
        // 另一条结构上不可达（第一版就是这样）。
        UnlockCondition guardian = TestGame.Cyber.EndingById["end_guardian_cat"].Condition;
        Check.True(guardian is AllCondition { Conditions.Count: 2 }, "兜底的条件应当是「末层主线 + 自己的那一条」。");
        UnlockCondition guardianExtra = ((AllCondition)guardian).Conditions[1];
        Check.True(
            guardianExtra.NumericLeaves().All(l => l.Metric != NumericMetric.Counter),
            "兜底自己那一条不该挂在算力计数器上（算力单调不减，两条门槛会同时成立）。");
        Check.True(
            guardianExtra.NumericLeaves().Any(l => l.Metric == NumericMetric.PlayTimeSeconds),
            "兜底自己那一条应当挂游玩时长——它才是与「算得多深」真正正交的那个指标。");
    }
    [Test]
    public static void Endings_AreAllGatedBehindTheLastLayer()
    {
        // 一进第 5 层就抢答是这个项目的经典坑（实验室包踩过）：兜底结局只依赖末层完成条件，
        // 所以"末层完成"必须同时含 EraAtLeast(5) 与末层主线，两个结局一个都不能漏。
        foreach (EndingDefinition ending in TestGame.Cyber.Endings)
        {
            List<NumericCondition> leaves = [.. ending.Condition.NumericLeaves()];

            Check.True(
                leaves.Any(l => l.Metric == NumericMetric.Era && l.Target >= 5),
                $"结局「{ending.Name}」没有 EraAtLeast(5)——它可能在第 5 层之前就成立。");

            Check.True(
                leaves.Any(l => l.Metric == NumericMetric.CookiesEarnedThisRun && l.Target >= 5e13),
                $"结局「{ending.Name}」没有引用末层主线门槛——一进第 5 层它就会抢答。");        }
    }

    [Test]
    public static void ComputeAtTheEnd_ExceedsTheEndingThreshold()
    {
        GameEngine engine = LoadFrom(FinishedSave.Value);

        double compute = engine.State.GetCounter(CyberContent.ComputeCounterKey);
        List<string> missing =
        [
            .. engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)),
        ];

        Console.WriteLine(
            $"      走到最后时：算力 {compute:F0}（门槛 {CyberContent.ComputeForEnding:F0}）；" +
            $"产率 {CyberContent.ComputeRatePerSecond(engine):F0}/s；成就 {engine.State.Achievements.Count}；" +
            $"未读到的条目 [{string.Join(", ", missing)}]");

        Check.Equal("end_owner_echo", engine.ReachedEnding?.Id, "自然游玩应当落到「找到主人的数据残影」。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 固定建筑规模下、把三条算力曲线全部买下之后的每秒产量。<para>
    /// 三条曲线都有自己的解锁门槛（算力 400 / 2.5 万 / 40 万，各自带进度条），
    /// 而测试是直接把算力写进状态的——解锁条件不会自己结算，所以这里
    /// <b>先把门槛抬到第三条之上</b>，再显式买下三条，最后才把算力调到要断言的值。
    /// 不这么做的话，断言测到的是"算力没影响产量"这个恒等式，而不是曲线本身。
    /// </para>
    /// </summary>
    private static double CpsWithCompute(double compute)
    {
        GameEngine engine = TestGame.CreateCyber(out _);
        engine.State.BuildingCounts["process"] = 100;
        engine.State.Cookies = 1e12;          // 买下三条曲线（最贵的一条 3e10）
        engine.State.SetCounter(CyberContent.ComputeCounterKey, 400_000);
        engine.MarkDirty();
        engine.Simulate(1);                   // 让成就与解锁条件先结算一遍

        foreach (string id in new[] { "compute_scheduler", "distributed_training", "self_optimizing" })
        {
            PurchaseResult result = engine.BuyUpgrade(id);
            Check.True(result.Success, $"测试前提：{id} 应当买得下。原因：{result.Message}");
        }

        engine.State.SetCounter(CyberContent.ComputeCounterKey, compute);
        engine.MarkDirty();
        return engine.Production.CookiesPerSecond;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateCyber(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>一次真实游玩，停在"刚进入第 5 层"这一刻（两份读档共用）。</summary>
    private static readonly Lazy<string> LastLayerSave = new(() => PlayToEra(5).Save());

    /// <summary>从"刚进入第 5 层"出发，让机器人自己把最后这一层走完。</summary>
    private static readonly Lazy<string> FinishedSave = new(() => PlayToTheEnd().Save());

    private static GameEngine PlayToEra(int target)
    {
        GameEngine engine = TestGame.CreateCyber(out _, seed: 20240924);

        for (int round = 0; round < 60_000 && engine.State.Era < target; round++)        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(target, engine.State.Era, $"机器人没能走到第 {target} 层。");
        Check.Null(engine.ReachedEnding, "还没走到最后就判定结局了。");
        return engine;
    }

    private static GameEngine PlayToTheEnd()
    {
        GameEngine engine = LoadFrom(LastLayerSave.Value);

        // 把算力压回末层门槛刚过的位置，让机器人自己把剩下的路走完。
        // 直接沿用存档里的算力是不行的：那份存档来自自然游玩，算力早已越过结局门槛，
        // 末层主线一完成结局就立刻成立——测出来的是 "门槛正好被踩中"，而不是"机器人走得到"。
        engine.State.SetCounter(CyberContent.ComputeCounterKey, 1.1e6);
        engine.State.CookiesEarnedThisRun = 5e13;
        engine.MarkDirty();
        for (int round = 0; round < 200_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);
        }

        Console.WriteLine(
            $"      走到最后用了 {engine.State.PlayTimeSeconds / 3600:F1} 游戏小时；" +
            $"算力 {engine.State.GetCounter(CyberContent.ComputeCounterKey):E2}；" +
            $"成就 {engine.State.Achievements.Count}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{engine.Content.LoreEntries.Count}");

        Check.NotNull(engine.ReachedEnding, "机器人没能走到最后。");
        return engine;
    }

    /// <summary>
    /// 把状态推进到"末层主线已完成"，再把算力设成指定值。<para>
    /// 三处都必须重置：<b>建筑</b>（算力是单调递增的，只要还有常驻类建筑在，
    /// 设成 0 之后跑几十秒它就会自己长回门槛之上）、<b>结局</b>（终局判定是一次性的，
    /// 存档里那个结局会一直盖着，新的条件成立了也不会重判）、以及算力本身。
    /// </para>
    /// </summary>
    private static GameEngine EndingStateWith(double compute)
    {
        GameEngine engine = LoadFrom(FinishedSave.Value);
        engine.State.BuildingCounts.Clear();
        engine.State.EndingsReached.Clear();
        engine.State.SetCounter(CyberContent.ComputeCounterKey, compute);
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(double compute)
    {
        GameEngine engine = EndingStateWith(compute);
        Check.NotNull(engine.ReachedEnding, "走到最后之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
