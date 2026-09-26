using NekoClicker.Content.Library;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #9《猫娘图书馆》的验收（阶段 4B）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>虚无化真的成立</b>——被阅读度按比例衰减、按闭式解演化、
///   每次开新书清零，而产量乘数跟着它走。</item>
///   <item><b>它不会把玩家锁死</b>——阶段 4B 的验收 ②：乘数有下限（×0.5），
///   归零时仍然在赚钱、仍然爬得回来。</item>
///   <item><b>它是内容侧实现的</b>——S-D 没有新增核心能力，落在已有的
///   <c>IGameModule</c> + <c>Scaling(ScalingSource.CustomCounter)</c> 上。</item>
/// </list>
/// </para>
/// </summary>
public static class LibraryContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Library;

        Check.Equal("猫娘图书馆", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五本书，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、两个结局。这个包和末世一样刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(2, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);

        // 至少五座建筑能养出被阅读度——否则"虚无化"无从谈起。
        Check.AtLeast(
            content.Buildings.Count(b => b.Tags.Contains(LibraryContent.ReaderTag, StringComparer.Ordinal)),
            5);
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Library.LoreEntries)
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
        GameContent content = TestGame.Library;

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
        // 「读者」线的门槛指标是会掉的，静态推理完全靠不住——只能在真跑图里验时刻。
        GameEngine engine = TestGame.CreateLibrary(out _, seed: 20240924);
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

        foreach (StorylineDefinition storyline in TestGame.Library.Storylines)
        {
            LoreEntry first = TestGame.Library.LoreOf(storyline.Id).First();
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
        GameEngine engine = TestGame.CreateLibrary(out _, seed: 4242);

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

    // ------------------------------------------------------------ 虚无化（S-D）

    [Test]
    public static void Readership_DecaysExponentiallyTowardTheEquilibrium()
    {
        // 10 座阅览室 → 产率 10/s、衰减率 1/240 → 均衡点 2400。
        // 跑一个时间常数（240 秒）之后应当到 2400 × (1 − 1/e)。
        // 这条断言的意义是：衰减**不是线性的**，也不是拍脑袋的曲线。
        GameEngine engine = TestGame.CreateLibrary(out _);
        engine.State.BuildingCounts["reading_room"] = 10;
        engine.MarkDirty();

        engine.Simulate(LibraryContent.DecaySeconds);

        double expected = 2400 * (1 - Math.Exp(-1));
        Check.Close(expected, engine.State.GetCounter(LibraryContent.ReadershipCounterKey), 1e-6);

        // 反方向：把读者建筑卖光，被阅读度按同一个时间常数往下掉。
        engine.State.BuildingCounts.Clear();
        engine.MarkDirty();
        double before = engine.State.GetCounter(LibraryContent.ReadershipCounterKey);
        engine.Simulate(LibraryContent.DecaySeconds);
        Check.Close(before * Math.Exp(-1), engine.State.GetCounter(LibraryContent.ReadershipCounterKey), 1e-6);
    }

    [Test]
    public static void Readership_OfflineUsesTheSameClosedForm()
    {
        // 离线 240 秒与在线 240 秒必须给出同一个数——这正是闭式解存在的理由。
        // 步进算是做不到的：离线 3 小时是三十二万步，既慢又攒浮点误差。
        GameEngine online = TestGame.CreateLibrary(out _);
        online.State.BuildingCounts["reading_room"] = 10;
        online.MarkDirty();
        online.Simulate(LibraryContent.DecaySeconds);
        double ticked = online.State.GetCounter(LibraryContent.ReadershipCounterKey);

        GameEngine offline = TestGame.CreateLibrary(out _);
        offline.State.BuildingCounts["reading_room"] = 10;
        offline.MarkDirty();
        OfflineProgress? progress = offline.ApplyOfflineProgress(TimeSpan.FromSeconds(LibraryContent.DecaySeconds));
        Check.NotNull(progress, "240 秒离线应当触发结算。");
        double jumped = offline.State.GetCounter(LibraryContent.ReadershipCounterKey);

        Check.Close(ticked, jumped, 1e-6);

        // 再验一次会掉的那一侧：没人读的时候，离线也是按比例掉。
        GameEngine decaying = TestGame.CreateLibrary(out _);
        decaying.State.SetCounter(LibraryContent.ReadershipCounterKey, 2400);
        decaying.MarkDirty();
        decaying.ApplyOfflineProgress(TimeSpan.FromSeconds(LibraryContent.DecaySeconds * 2));
        Check.Close(2400 * Math.Exp(-2), decaying.State.GetCounter(LibraryContent.ReadershipCounterKey), 1e-6);
    }

    [Test]
    public static void Readership_ResetsWhenANewBookStarts()
    {
        GameEngine engine = TestGame.CreateLibrary(out _);
        engine.State.SetCounter(LibraryContent.ReadershipCounterKey, 50_000);
        engine.State.CookiesEarnedThisRun = 2e5;
        engine.MarkDirty();

        Check.True(engine.EraGate.CanAdvance, "第 1 本的完成条件应当已经满足。");
        engine.Ascend();

        Check.Equal(2, engine.State.Era);
        Check.Close(
            0,
            engine.State.GetCounter(LibraryContent.ReadershipCounterKey),
            1e-9,
            "上一本的读者不会自动读新书——开新书必须把被阅读度清零。");
    }

    [Test]
    public static void Production_MultiplierFollowsReadership_AndNeverReachesZero()
    {
        // 阶段 4B 的验收 ②：虚无化的数值下限不会让产量归零到死锁。
        //
        // 这条用例同时是这个包最重要的一道算术保险：Scaling 的 Cap 限的是**原始计数值**，
        // 不是加成结果（Apply = base + PerUnit × min(计数, Cap)）。第一次实现就踩了这个坑——
        // 写成 Cap: 300 之后，300 点被阅读度就把乘数顶到上限，衰减与清零全都失去意义。
        // 把三个端点钉死，这类误读就再也藏不住。
        double atZero = CpsWithReadership(0);
        double atBreakEven = CpsWithReadership(20_000);
        double atCap = CpsWithReadership(60_000);
        double beyond = CpsWithReadership(500_000);

        Check.Greater(atZero, 0, "没有读者时产量归零了——玩家再也买不起任何东西，直接死锁。");
        Check.CloseRelative(atBreakEven, atZero * 2, 1e-9, "被阅读度 2 万应当正好回到 ×1.0。");
        Check.CloseRelative(atCap, atZero * 4, 1e-9, "被阅读度 6 万应当正好到 ×2.0。");
        Check.CloseRelative(beyond, atCap, 1e-9, "被阅读度超过 6 万之后应当封顶。");

        // 而且"没读者"这件事是可逆的：买回读者建筑，读者会自己长回来。
        GameEngine starved = TestGame.CreateLibrary(out _);
        starved.State.BuildingCounts["bookshelf"] = 100;
        starved.State.SetCounter(LibraryContent.ReadershipCounterKey, 0);
        double baseline = starved.Production.CookiesPerSecond;
        starved.State.BuildingCounts["reading_room"] = 300;
        starved.MarkDirty();
        starved.Simulate(3600);

        Check.AtLeast(
            starved.State.GetCounter(LibraryContent.ReadershipCounterKey),
            60_000,
            "被阅读度没有自己长回来——那才叫死锁。");
        Check.Greater(starved.Production.CookiesPerSecond, baseline * 3, "读者回来了，产量却没回来。");
    }

    [Test]
    public static void Readership_RefreshesProduction_WithoutAnyOtherEvent()
    {
        // Step 的顺序是「先重算，再结算，最后才 module.OnTick」——模块在 tick 里改了计数器
        // 并不会自动让产量变脏。这条用例在**只买了一次建筑、之后什么都不做**的前提下
        // 跑 30 秒（第一个金猫在 60 秒之后才可能来，所以这段时间里没有任何外部脏源），
        // 断言产量确实跟着被阅读度涨上去了。
        GameEngine engine = TestGame.CreateLibrary(out _);
        engine.State.BuildingCounts["reading_room"] = 100;
        engine.MarkDirty();

        engine.Simulate(1);                      // 让首批"拥有 N 座"的成就先结算掉
        double before = engine.Production.CookiesPerSecond;

        int guard = 0;
        while (engine.State.GetCounter(LibraryContent.ReadershipCounterKey) < 2_000 && guard++ < 40)
            engine.Simulate(1);

        double after = engine.Production.CookiesPerSecond;
        Check.AtLeast(
            engine.State.GetCounter(LibraryContent.ReadershipCounterKey),
            2_000,
            "30 秒里被阅读度没涨到 2000——前提不成立，后面的断言无意义。");
        Check.Greater(after, before * 1.05, $"被阅读度涨上去了，产量却停在 {before:F3}——模块改了计数器却没让产量变脏。");
    }

    [Test]
    public static void RobotWalksAllFiveBooks()
    {
        // 与其余纪元包同构：证明这五层门槛在真实曲线下**走得完**。
        // 这个包尤其需要——每开一本新书，被阅读度清零 + 产量乘数掉回 ×0.5，
        // 是六个包里唯一一个"每层开局都会变弱"的包。
        GameEngine engine = TestGame.CreateLibrary(out _, seed: 20240924);
        List<(int Era, double Hours, double Readership)> timeline = [];

        for (int round = 0; round < 60_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                double readers = engine.State.GetCounter(LibraryContent.ReadershipCounterKey);
                engine.Ascend();
                timeline.Add((before, engine.State.PlayTimeSeconds / 3600, readers));
            }

            engine.Simulate(30);
        }

        foreach ((int era, double hours, double readers) in timeline)
            Console.WriteLine($"      第 {era} 本结束于 {hours:F1} 游戏小时（当时被阅读度 {readers:F0}）");

        Console.WriteLine(
            $"      五本书用时 {engine.State.PlayTimeSeconds / 3600:F1} 小时；" +
            $"被阅读度 {engine.State.GetCounter(LibraryContent.ReadershipCounterKey):F0}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.Library.LoreEntries.Count}；" +
            $"成就 {engine.State.Achievements.Count}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 本——某本的完成条件可能不可达。");
        Check.Equal(4, timeline.Count, "应当正好开新书 4 次（第 5 本不再开）。");
    }

    [Test]
    public static void ReadershipAtTheEnd_ExceedsTheEndingThreshold()
    {
        GameEngine engine = LoadFrom(FinishedSave.Value);

        double readers = engine.State.GetCounter(LibraryContent.ReadershipCounterKey);
        List<string> missing =
        [
            .. engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)),
        ];

        Console.WriteLine(
            $"      读到最后时：被阅读度 {readers:F0}（门槛 {LibraryContent.ReadershipForEnding:F0}）；" +
            $"读者类建筑 {ReaderBuildings(engine)}；成就 {engine.State.Achievements.Count}；" +
            $"未读到的条目 [{string.Join(", ", missing)}]");

        Check.Equal("end_read_to_the_end", engine.ReachedEnding?.Id, "自然游玩应当落到「被读到最后」。");
    }

    /// <summary>读者类建筑总数（诊断用）。</summary>
    private static int ReaderBuildings(GameEngine engine)
    {
        int total = 0;
        foreach (BuildingDefinition building in engine.Content.Buildings)
        {
            if (building.Tags.Contains(LibraryContent.ReaderTag, StringComparer.Ordinal))
                total += engine.State.BuildingCount(building.Id);
        }
        return total;
    }

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        Check.Equal("end_read_to_the_end", EndingWith(LibraryContent.ReadershipForEnding * 2));
        Check.Equal("end_no_one_reads", EndingWith(0));

        GameEngine both = CompletedWith(LibraryContent.ReadershipForEnding * 2);
        Check.True(
            both.Content.EndingById["end_no_one_reads"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下兜底结局的条件也应当是成立的。");
        Check.Equal("end_read_to_the_end", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }

    // ---------------------------------------------------------------- 辅助

    private static double CpsWithReadership(double readership)
    {
        GameEngine engine = TestGame.CreateLibrary(out _);
        engine.State.BuildingCounts["bookshelf"] = 100;
        engine.State.SetCounter(LibraryContent.ReadershipCounterKey, readership);
        engine.MarkDirty();
        return engine.Production.CookiesPerSecond;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateLibrary(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>一次真实游玩，停在"刚进入第 5 本"这一刻（三份读档共用）。</summary>
    private static readonly Lazy<string> LastBookSave = new(() => PlayToEra(5).Save());

    /// <summary>从"刚进入第 5 本"出发，让机器人自己把最后这一本读完。</summary>
    private static readonly Lazy<string> FinishedSave = new(() => PlayToTheEnd().Save());

    private static GameEngine PlayToEra(int target)
    {
        GameEngine engine = TestGame.CreateLibrary(out _, seed: 20240924);

        for (int round = 0; round < 60_000 && engine.State.Era < target; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(target, engine.State.Era, $"机器人没能走到第 {target} 本。");
        Check.Null(engine.ReachedEnding, "还没读到最后就判定结局了。");
        return engine;
    }

    private static GameEngine PlayToTheEnd()
    {
        GameEngine engine = LoadFrom(LastBookSave.Value);

        for (int round = 0; round < 200_000 && engine.ReachedEnding is null; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            engine.Simulate(engine.State.Era >= 5 ? 0.25 : 30);
        }

        Check.NotNull(engine.ReachedEnding, "机器人没能读到最后。");
        return engine;
    }

    private static GameEngine CompletedWith(double readership)
    {
        GameEngine engine = LoadFrom(LastBookSave.Value);
        engine.State.SetCounter(LibraryContent.ReadershipCounterKey, readership);
        engine.State.CookiesEarnedThisRun = 1e11;
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(double readership)
    {
        GameEngine engine = CompletedWith(readership);
        Check.NotNull(engine.ReachedEnding, "读到最后之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
