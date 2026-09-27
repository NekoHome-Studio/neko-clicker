using NekoClicker.Content.God;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #7《猫娘神明》的验收（ROADMAP 阶段 5 的第一个包）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>第二资源是单调的</b>——信仰只涨不花、切换神话体系也不清零，
///   所以它能进四层纪元的完成条件；「在线人数」取历史峰值，同理。</item>
///   <item><b>换皮不需要新能力</b>——五套神话体系全部落在已有的
///   <c>Era</c> + <c>Lore</c> + <c>Scaling(ScalingSource.CustomCounter)</c> 接缝上，
///   核心零改动。</item>
///   <item><b>它不会把玩家锁死</b>——机器人真的走得完五层，图鉴真的读得完 40 条，
///   自然游玩真的会落到承诺型结局。</item>
/// </list>
/// </para>
/// </summary>
public static class GodContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.God;

        Check.Equal("猫娘神明", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五套神话体系，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、三个结局。这个包和末世 / 图书馆一样刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(3, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);

        // 至少五座神殿类建筑能养出信仰——否则四层纪元的完成条件无从谈起。
        Check.AtLeast(
            content.Buildings.Count(b => b.Tags.Contains(GodContent.TempleTag, StringComparer.Ordinal)),
            5);

        Console.WriteLine(
            $"      #7 神明：{content.Buildings.Count} 建筑 / {content.Upgrades.Count} 升级 / "
            + $"{content.Achievements.Count} 成就 / {content.Buffs.Count} 增益 / "
            + $"{content.GoldenCookieOutcomes.Count} 神迹 / {content.LoreEntries.Count} 条叙事 / "
            + $"{content.Eras.Count} 层神话 / {content.Endings.Count} 个结局");
    }

    [Test]
    public static void EveryMythChangesARuleOfItsOwn()
    {
        // 每换一套神话都要改掉至少一条**属于自己**的规则，否则"换体系"就只是重复劳动。
        // 这里比九命那条守卫更严一格：信仰驱动产量是每一层共有的机制脊柱，
        // 不算"这一层自己的规则"——所以层数 ≥ 2 时必须另有一处变化
        // （Balance 覆盖，或一条不是 CustomCounter 成长的修饰符）。
        GameContent content = TestGame.God;

        for (int index = 2; index <= 5; index++)
        {
            EraDefinition era = content.FindEra(index)!;
            bool ownRule = era.Balance is not null
                           || era.Modifiers.Any(m => m.Scaling?.Source != ScalingSource.CustomCounter);
            Check.True(
                ownRule,
                $"第 {index} 层（{era.Name}）除了共有的信仰成长之外没有改任何规则——换皮换了个寂寞。");
        }
    }

    [Test]
    public static void EveryEndingHasItsOwnAchievement()
    {
        GameContent content = TestGame.God;

        foreach (EndingDefinition ending in content.Endings)
        {
            AchievementDefinition? match = content.Achievements.FirstOrDefault(achievement =>
                achievement.Unlock.OwnedLeaves()
                    .Any(owned => owned.Kind == OwnedKind.Ending && owned.Id == ending.Id));

            Check.NotNull(match, $"结局「{ending.Name}」没有对应的成就——抵达它的玩家拿不到任何纪念。");
        }
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.God.LoreEntries)
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
        GameContent content = TestGame.God;

        foreach (StorylineDefinition storyline in content.Storylines)
        {
            List<LoreEntry> entries = [.. content.LoreOf(storyline.Id)];

            Check.Equal(storyline.TotalEntries, entries.Count, $"「{storyline.Name}」的条数与声明不符。");

            for (int i = 0; i < entries.Count; i++)
                Check.Equal(i + 1, entries[i].Order, $"「{storyline.Name}」的序号有空洞或重复。");
        }
    }

    [Test]
    public static void Storylines_ReadInOrderDuringARealPlaythrough()
    {
        // 阶段 2.6 的教训：序号倒挂是**沉默失败**——测试不会红，只有玩到那一段才看得出来。
        // 所以不推理，直接在真实游玩里记录每一条的解锁时刻，再验同一线内序号与时刻同序。
        GameEngine engine = TestGame.CreateGod(out _, seed: 20240924);
        Dictionary<string, int> unlockedAt = new(StringComparer.Ordinal);

        for (int round = 0; round < 60_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);

            foreach (string id in engine.State.LoreUnlocked)
                unlockedAt.TryAdd(id, round);
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

    [Test]
    public static void EveryStorylineOpensEarlyEnough()
    {
        int early = 0;

        foreach (StorylineDefinition storyline in TestGame.God.Storylines)
        {
            LoreEntry first = TestGame.God.LoreOf(storyline.Id).First();
            double target = LoreTests.FirstThreshold(first.Reveal);

            Check.Finite(target, $"「{storyline.Name}」的开篇条件无法量化。");
            Check.AtMost(target, 1e12, $"「{storyline.Name}」的开篇门槛高到几乎读不到。");
            if (target <= 200) early++;
        }

        Check.AtLeast(early, 2, "真正早期能读到的剧情线太少，图鉴开场就全是 ???。");
    }

    [Test]
    public static void G5_FirstTenMinutesRevealAtMostThreeEntries()
    {
        GameEngine engine = TestGame.CreateGod(out _, seed: 4242);

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

    // ------------------------------------------------------------ 第二资源（信仰 / 在线人数）

    [Test]
    public static void Faith_GrowsWithTemplesAndWithTheCrowd()
    {
        GameEngine engine = TestGame.CreateGod(out _);

        // 什么都没有：不产信仰。
        engine.Simulate(30);
        Check.Close(0, engine.State.GetCounter(GodContent.FaithCounterKey), 1e-9);

        // 神殿类建筑是主产率：10 座神龛 → 5 点/秒（外加人气那一份）。
        engine.State.BuildingCounts["house_shrine"] = 10;
        engine.MarkDirty();
        engine.Simulate(10);
        Check.Close(10 * FaithRate(temples: 10, others: 0), engine.State.GetCounter(GodContent.FaithCounterKey), 1e-6);

        // 非神殿类建筑也贡献人气：每 200 座 / 秒 1 点。
        double before = engine.State.GetCounter(GodContent.FaithCounterKey);
        engine.State.BuildingCounts["merch_factory"] = 200;
        engine.MarkDirty();
        engine.Simulate(10);
        Check.Close(before + 10 * FaithRate(temples: 10, others: 200), engine.State.GetCounter(GodContent.FaithCounterKey), 1e-6);

        // 而且神殿那一份远大于人气那一份——"香火来自神殿"这条设定在数值上是成立的。
        Check.Greater(
            FaithRate(temples: 10, others: 0) / FaithRate(temples: 0, others: 200),
            4.0,
            "神殿与人气的产率差不多——那信仰就不是神殿类建筑的机制了。");
    }

    [Test]
    public static void Faith_IsMonotonic_AndSurvivesEveryMythSwitch()
    {
        // 这个包的四层完成条件挂在信仰上，所以"信仰只涨不掉"是结构性的前提。
        // 反面对照：图书馆包的被阅读度会在开新书时清零（那是那个包的专利），
        // 一旦这里也清零，四层的进度条会当场倒退到 0。
        GameEngine engine = TestGame.CreateGod(out _);
        engine.State.BuildingCounts["house_shrine"] = 1_000;
        engine.State.CookiesEarnedThisRun = 1e5;
        engine.Simulate(120);

        double atFirstMyth = engine.State.GetCounter(GodContent.FaithCounterKey);
        Check.AtLeast(atFirstMyth, 6e4, "前提：第 1 层的完成条件应当已经满足。");
        Check.True(engine.EraGate.CanAdvance);

        engine.Ascend();
        Check.Equal(2, engine.State.Era);
        Check.AtLeast(
            engine.State.GetCounter(GodContent.FaithCounterKey),
            atFirstMyth,
            "切换神话体系把信仰清零了——四层纪元的进度会当场倒退。");

        // 卖掉神殿也不掉（它是累加量，不是"当前规模"的函数）。
        engine.State.BuildingCounts.Clear();
        engine.MarkDirty();
        double before = engine.State.GetCounter(GodContent.FaithCounterKey);
        engine.Simulate(60);
        Check.AtLeast(engine.State.GetCounter(GodContent.FaithCounterKey), before);
    }

    [Test]
    public static void Faith_DrivesProduction_AndNeverOvershootsItsCap()
    {
        // 阶段 4B 的教训：Scaling 的 Cap 限的是**原始计数值**，不是加成结果
        // （Apply = base + PerUnit × min(计数, Cap)）。把三个端点钉死，这类误读就再也藏不住。
        // 每点信仰 +0.01%，10 万点封顶 → 满值正好是 ×11（1 + 0.0001 × 100,000）。
        double expected = 1 + (GodContent.FaithPercentPerPoint * GodContent.FaithScalingCap);

        double atZero = CpsWithFaith(0);
        double atCap = CpsWithFaith(GodContent.FaithScalingCap);
        double beyond = CpsWithFaith(GodContent.FaithScalingCap * 10);

        Check.Close(11.0, expected, 1e-9, "封顶值本身应当正好是 ×11。");
        Check.CloseRelative(atCap, atZero * expected, 1e-9, "信仰到封顶点时应当正好把全局产量拉到 ×11。");
        Check.CloseRelative(beyond, atCap, 1e-9, "信仰超过封顶点之后应当封顶。");
    }

    [Test]
    public static void Faith_RefreshesProduction_WithoutAnyOtherEvent()
    {
        // Step 的顺序是「先重算，再结算，最后才 module.OnTick」——模块在 tick 里改了计数器
        // 并不会自动让产量变脏。这条用例在**只摆建筑、之后什么都不做**的前提下跑，
        // 断言产量确实跟着信仰涨上去了：第一个神迹在 48 秒之后才可能来，所以这段时间里
        // 没有任何外部脏源可以替它兜底。
        GameEngine engine = TestGame.CreateGod(out _);
        engine.State.BuildingCounts["house_shrine"] = 200;
        engine.MarkDirty();

        engine.Simulate(1);                      // 让首批"拥有 N 座"的成就先结算掉
        double before = engine.Production.CookiesPerSecond;

        int guard = 0;
        while (engine.State.GetCounter(GodContent.FaithCounterKey) < 2_000 && guard++ < 40)
            engine.Simulate(1);

        double after = engine.Production.CookiesPerSecond;
        Check.AtLeast(
            engine.State.GetCounter(GodContent.FaithCounterKey),
            2_000,
            "20 秒里信仰没涨到 2000——前提不成立，后面的断言无意义。");
        Check.True(
            engine.State.PlayTimeSeconds <= 48,
            $"窗口里混进了别的脏源（{engine.State.PlayTimeSeconds:F1} 秒时已经可能出现第一个神迹）。");
        Check.Greater(after, before * 1.05, $"信仰涨上去了，产量却停在 {before:F3}——模块改了计数器却没让产量变脏。");
    }

    [Test]
    public static void Faith_OfflineAccruesLikeOnline()
    {
        // 离线期间不经过 OnTick，模块必须自己在 OnOffline 里补算，否则挂机收益会凭空少一截。
        GameEngine online = TestGame.CreateGod(out _);
        online.State.BuildingCounts["house_shrine"] = 100;
        online.MarkDirty();
        online.Simulate(600);
        double ticked = online.State.GetCounter(GodContent.FaithCounterKey);

        GameEngine offline = TestGame.CreateGod(out _);
        offline.State.BuildingCounts["house_shrine"] = 100;
        offline.MarkDirty();
        OfflineProgress? progress = offline.ApplyOfflineProgress(TimeSpan.FromSeconds(600));
        Check.NotNull(progress, "600 秒离线应当触发结算。");
        Check.Close(ticked, offline.State.GetCounter(GodContent.FaithCounterKey), 1e-6);
    }

    [Test]
    public static void Viewers_RecordThePeak_SoTheCthulhuGateCannotBeLost()
    {
        GameEngine engine = TestGame.CreateGod(out _);
        engine.State.SetCounter(GodContent.FaithCounterKey, 4_000);
        engine.State.BuildingCounts["stream_studio"] = 300;
        engine.MarkDirty();
        engine.Simulate(1);

        Check.Close(
            4_000 / GodContent.FaithPerViewer + 300 * GodContent.ViewersPerStudio,
            engine.State.GetCounter(GodContent.ViewerCounterKey),
            0.05);

        // 信仰只涨，所以在线人数（峰值）也只涨：传的是信仰阈值，读的是历史最高水位。
        double before = engine.State.GetCounter(GodContent.ViewerCounterKey);
        engine.State.SetCounter(GodContent.FaithCounterKey, 400_000);
        engine.Simulate(1);
        Check.AtLeast(engine.State.GetCounter(GodContent.ViewerCounterKey), before);
        Check.AtLeast(
            engine.State.GetCounter(GodContent.ViewerCounterKey),
            400_000 / GodContent.FaithPerViewer);
    }

    // ------------------------------------------------------------ 纪元与结局

    [Test]
    public static void NoMythInheritsBuildings_SoEveryLayerRedeclaresItsOwnWorld()
    {
        // 这个包刻意不做继承（换皮手册 §4 第 13 条）：每换一套神话，世界重新揭示一遍。
        // 因此建筑的解锁条件用「本轮累计」是**有意的**——继承与"本轮累计"的搭配只在末世包里成立。
        GameEngine engine = TestGame.CreateGod(out _);
        engine.State.BuildingCounts["stone_temple"] = 40;
        engine.State.CookiesEarnedThisRun = 1e5;
        engine.State.SetCounter(GodContent.FaithCounterKey, 2e5);
        engine.MarkDirty();

        Check.True(engine.EraGate.CanAdvance, "前提：第 1 层的完成条件应当已经满足。");
        engine.Ascend();

        Check.Equal(0, engine.State.BuildingCount("stone_temple"), "这个包不继承建筑。");
        Check.Equal(5, TestGame.God.Eras.Count);
        foreach (EraDefinition era in TestGame.God.Eras)
            Check.Equal(0.0, era.InheritBuildingRatio, $"第 {era.Index} 层不该继承建筑。");
    }

    [Test]
    public static void RobotWalksAllFiveMyths()
    {
        // 与其余纪元包同构：证明这五层门槛在真实曲线下**走得完**。
        // 这个包尤其需要——四层完成条件挂在信仰上，而信仰的产率取决于神殿规模，
        // 门槛定高一点点就会变成"某一层走不动"，只有真跑才看得出来。
        GameEngine engine = TestGame.CreateGod(out _, seed: 20240924);
        List<(int Era, double Hours, double EarnedAllTime, double Faith, double Viewers, double Chips)> timeline = [];

        for (int round = 0; round < 120_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                engine.Ascend();
                timeline.Add((
                    before,
                    engine.State.PlayTimeSeconds / 3600,
                    engine.State.CookiesEarnedAllTime,
                    engine.State.GetCounter(GodContent.FaithCounterKey),
                    engine.State.GetCounter(GodContent.ViewerCounterKey),
                    engine.State.PrestigeChips));
            }

            engine.Simulate(30);
        }

        foreach ((int era, double hours, double allTime, double faith, double viewers, double chips) in timeline)
            Console.WriteLine(
                $"      第 {era} 层结束于 {hours:F2} 游戏小时（历史累计 {allTime:E3}；"
                + $"信仰 {faith:E3}；在线 {viewers:E3}；结算神格 {chips:F0}）");

        Console.WriteLine(
            $"      五层用时 {engine.State.PlayTimeSeconds / 3600:F1} 小时；" +
            $"历史累计 {engine.State.CookiesEarnedAllTime:E3}；" +
            $"神格 {engine.State.PrestigeChips:F0}（等级 {engine.State.PrestigeLevel}）；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.God.LoreEntries.Count}；" +
            $"成就 {engine.State.Achievements.Count}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 层——某层的完成条件可能不可达。");
        Check.Equal(4, timeline.Count, "应当正好切换神话体系 4 次（第 5 层不再切）。");
    }

    [Test]
    public static void NaturalRunLandsOnThePromiseEnding()
    {
        // "自然跑图应当落到承诺型结局"——阈值不能靠推理定，这条用在真跑图里读到的条数把它钉住。
        GameEngine engine = LoadFrom(FinishedSave.Value);
        List<string> missing =
        [
            .. engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)),
        ];

        Console.WriteLine(
            $"      走到最后时：用时 {engine.State.PlayTimeSeconds / 3600:F2} 小时；"
            + $"信仰 {engine.State.GetCounter(GodContent.FaithCounterKey):E3}；"
            + $"在线 {engine.State.GetCounter(GodContent.ViewerCounterKey):E3}；"
            + $"图鉴 {engine.State.LoreUnlocked.Count}/{engine.Content.LoreEntries.Count}；"
            + $"成就 {engine.State.Achievements.Count}；"
            + $"神格 {engine.State.PrestigeChips:F0}；"
            + $"未读到的条目 [{string.Join(", ", missing)}]");

        Check.Equal("end_main_god", engine.ReachedEnding?.Id, "自然游玩应当落到「成为主神」。");
    }

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        Check.Equal("end_main_god", EndingWith(lore: 34));
        Check.Equal("end_meme", EndingWith(lore: 12));
        Check.Equal("end_forgotten", EndingWith(lore: 0));

        // 第一行那个状态<b>同时</b>满足「成为主神」与「变成 meme」的条件，拿到的是
        // Priority 更小的那个——这才是"互斥"的实现方式：判定按优先级取第一个，记下之后不再判。
        GameEngine both = CompletedWith(lore: 34);
        Check.True(
            both.Content.EndingById["end_meme"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下「变成 meme」的条件也应当是成立的。");
        Check.Equal("end_main_god", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 本包信仰产率的定义式（照抄 <c>FaithModule</c> 的两项之和）。<para>
    /// 用例里不调用模块内部方法，而是把公式写在这里：这样"产率由什么决定"这件事
    /// 在被测代码之外还有一份独立陈述，Formula 改了而用例没改会立刻红。
    /// </para>
    /// </summary>
    private static double FaithRate(int temples, int others)
        => temples * GodContent.FaithPerTemplePerSecond
           + (temples + others) / GodContent.BuildingsPerFaithPerSecond;

    private static double CpsWithFaith(double faith)
    {
        GameEngine engine = TestGame.CreateGod(out _);
        engine.State.BuildingCounts["house_shrine"] = 100;
        engine.State.SetCounter(GodContent.FaithCounterKey, faith);
        engine.MarkDirty();
        return engine.Production.CookiesPerSecond;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateGod(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>一次真实游玩，停在"刚切到第 5 套神话"这一刻（两份读档共用）。</summary>
    private static readonly Lazy<string> LastMythSave = new(() => PlayToEra(5).Save());

    /// <summary>从"刚切到第 5 套神话"出发，让机器人自己把最后这一层走完。</summary>
    private static readonly Lazy<string> FinishedSave = new(() => PlayToTheEnd().Save());

    private static GameEngine PlayToEra(int target)
    {
        GameEngine engine = TestGame.CreateGod(out _, seed: 20240924);

        for (int round = 0; round < 120_000 && engine.State.Era < target; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(target, engine.State.Era, $"机器人没能走到第 {target} 套神话。");
        Check.Null(engine.ReachedEnding, "还没走完主线就判定结局了。");
        return engine;
    }

    private static GameEngine PlayToTheEnd()
    {
        GameEngine engine = LoadFrom(LastMythSave.Value);

        for (int round = 0; round < 200_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);
        }

        Check.NotNull(engine.ReachedEnding, "机器人没能走到最后。");
        return engine;
    }

    /// <summary>从"刚切到第 5 套神话"出发，把图鉴条数调到指定水平，然后推完末层主线。</summary>
    private static GameEngine CompletedWith(double lore)
    {
        GameEngine engine = LoadFrom(LastMythSave.Value);

        engine.State.LoreUnlocked.Clear();
        foreach (LoreEntry entry in engine.Content.LoreEntries)
        {
            if (engine.State.LoreUnlocked.Count >= lore) break;
            engine.State.LoreUnlocked.Add(entry.Id);
        }

        engine.State.CookiesEarnedThisRun = 2e11;
        engine.State.SetCounter(GodContent.ViewerCounterKey, 2e6);
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(double lore)
    {
        GameEngine engine = CompletedWith(lore);
        Check.NotNull(engine.ReachedEnding, "推完末层主线之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
