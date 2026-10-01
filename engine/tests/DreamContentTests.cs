using NekoClicker.Content.Dream;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #8《猫娘梦境》的验收（阶段 5）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>「越深的梦越大」真的成立</b>——梦境能量只涨不落、跨层不清零，
///   而产量乘数跟着它走。</item>
///   <item><b>五层梦换手感</b>——每层的规则变化不是同一个数字换五遍，
///   而是五条不同的修饰符（离线时长 / 梦层倍率 / 事件频率 / 第二资源曲线）。</item>
///   <item><b>它是内容侧实现的</b>——阶段 5 没有新增核心能力，落在已有的
///   <c>IGameModule</c> + <c>Scaling(ScalingSource.CustomCounter)</c> + <c>EraDefinition</c> 上。</item>
/// </list>
/// </para>
/// </summary>
public static class DreamContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Dream;

        Check.Equal("猫娘梦境", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五层梦，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、两个结局。这个包和末世、图书馆一样刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(2, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);

        // 至少五座建筑能养出梦境能量——否则"越深的梦越大"无从谈起。
        Check.AtLeast(
            content.Buildings.Count(b => b.Tags.Contains(DreamContent.DreamLayerTag, StringComparer.Ordinal)),
            5);

        // 第二资源必须在构建期登记过显示名，否则玩家会看到「每点「dream_energy」」。
        Check.Contains(content.CounterName(DreamContent.DreamEnergyCounterKey), "梦境能量");
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Dream.LoreEntries)
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
        GameContent content = TestGame.Dream;

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
        // 四条线的门槛一半挂在累计赚取、一半挂在纪元上，而"本轮累计"在往下睡一层时归零——
        // 静态推理完全靠不住，只能在真跑图里验时刻。
        GameEngine engine = TestGame.CreateDream(out _, seed: 20240808);
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
                Console.WriteLine(
                    $"      [{storyline.Name}] {i + 1,2} {entries[i].Id,-8} " +
                    (unlockedAt.TryGetValue(entries[i].Id, out int at) ? $"轮 {at,6}" : "    未读到") +
                    $" / 阈值 {LoreTests.FirstThreshold(entries[i].Reveal):E2}");
            }
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

        foreach (StorylineDefinition storyline in TestGame.Dream.Storylines)
        {
            LoreEntry first = TestGame.Dream.LoreOf(storyline.Id).First();
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
        GameEngine engine = TestGame.CreateDream(out _, seed: 4242);

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

        foreach (string id in engine.State.LoreUnlocked)
        {
            LoreEntry entry = engine.Content.LoreById[id];
            Console.WriteLine(
                $"      前十分钟放出：{id}（点击 {engine.State.TotalClicks:F0} / 本轮 {engine.State.CookiesEarnedThisRun:E2} / " +
                $"阈值 {LoreTests.FirstThreshold(entry.Reveal):E2}）");
        }

        foreach (string id in engine.State.LoreUnlocked)
        {
            LoreEntry entry = engine.Content.LoreById[id];
            Console.WriteLine(
                $"      前十分钟放出：{id}（点击 {engine.State.TotalClicks:F0} / 本轮 {engine.State.CookiesEarnedThisRun:E2} / " +
                $"阈值 {LoreTests.FirstThreshold(entry.Reveal):E2}）");
        }
    }

    // ------------------------------------------------------------ 第二资源（梦境能量）

    [Test]
    public static void DreamEnergy_IsProducedOnlyByDreamLayerBuildings()
    {
        // 「枕头」是唯一不带梦层标签的建筑，所以它不产梦。这条用例把"产率表"和
        // "标签"这两份数据钉在一起——任何一边被改动都会在这里露出来。
        IReadOnlyDictionary<string, double> rate = DreamContent.DreamEnergyRatePerBuilding;
        Check.False(rate.ContainsKey("pillow"));
        Check.False(rate.ContainsKey("dream_mirror"));
        Check.False(rate.ContainsKey("insomnia_corridor"));
        Check.Greater(rate["dream_layer"], 0);
        Check.Greater(rate["dream_core"], rate["dream_layer"]);

        // 真跑一遍：只放不带标签的建筑，30 秒后计数器必须还是 0。
        GameEngine idle = TestGame.CreateDream(out _);
        idle.State.BuildingCounts["pillow"] = 100;
        idle.State.BuildingCounts["dream_mirror"] = 100;
        idle.MarkDirty();
        idle.Simulate(30);
        Check.Close(0, idle.State.GetCounter(DreamContent.DreamEnergyCounterKey), 1e-9,
            "不产梦的建筑养出了梦境能量——产出语义和文案对不上。");

        // 换成梦层类建筑，同一个时长里必须涨起来。
        GameEngine dreaming = TestGame.CreateDream(out _);
        dreaming.State.BuildingCounts["dream_layer"] = 100;
        dreaming.MarkDirty();
        dreaming.Simulate(30);
        double expected = 100 * DreamContent.DreamEnergyRatePerBuilding["dream_layer"] * 30;
        Check.CloseRelative(
            expected,
            dreaming.State.GetCounter(DreamContent.DreamEnergyCounterKey),
            1e-9,
            "100 层梦层的产出与产率表对不上。");
    }

    [Test]
    public static void DreamEnergy_MatchesItsOwnRatePerSecond()
    {
        // 产率是按建筑逐座累加的，所以"一座梦核 vs 一百座枕头"这种错配会立刻显形。
        GameEngine engine = TestGame.CreateDream(out _);
        engine.State.BuildingCounts["dream_layer"] = 10;
        engine.State.BuildingCounts["lucid_zone"] = 3;
        engine.State.BuildingCounts["dream_core"] = 1;
        engine.MarkDirty();

        IReadOnlyDictionary<string, double> rate = DreamContent.DreamEnergyRatePerBuilding;
        double expected = (10 * rate["dream_layer"]) + (3 * rate["lucid_zone"]) + rate["dream_core"];

        engine.Simulate(10);
        Check.Close(
            expected * 10,
            engine.State.GetCounter(DreamContent.DreamEnergyCounterKey),
            1e-6,
            "产率表与逐建筑累加的结果对不上。");
    }

    [Test]
    public static void DreamEnergy_OnlyEverGrows_AndSurvivesGoingDeeper()
    {
        // 这个包**没有**"会衰减的第二资源"（手册 §4 第 15 条：那是 #9 的专利），
        // 而且往下睡一层不清零——"梦会留在她身上"是文案承诺过的事。
        GameEngine engine = TestGame.CreateDream(out _);
        double previous = 0;
        int samples = 0;

        for (int round = 0; round < 4_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);

            double now = engine.State.GetCounter(DreamContent.DreamEnergyCounterKey);
            Check.AtLeast(now, previous, $"第 {round} 轮之后梦境能量从 {previous:F0} 掉到了 {now:F0}——它必须是单调不减的。");
            previous = now;
            samples++;
        }

        Check.AtLeast(samples, 10, "机器人没跑起来，这条断言没有意义。");
        Check.Greater(previous, 0, "跑完之后梦境能量还是 0——它根本没在被产出。");

        // 再往下睡一层，梦境能量不归零。
        double before = engine.State.GetCounter(DreamContent.DreamEnergyCounterKey);
        if (engine.EraGate.CanAdvance) engine.Ascend();
        Check.AtLeast(
            engine.State.GetCounter(DreamContent.DreamEnergyCounterKey),
            before,
            "往下睡一层把梦境能量清零了——「梦会留在她身上」是文案承诺过的事。");
    }

    [Test]
    public static void DreamEnergy_OfflineAndOnlineAgreeOnTheSameElapsedTime()
    {
        // 模块自己维护的计数器不经过 OnTick 就不会在离线期间增长（手册 §10）：
        // 离线 600 秒与在线 600 秒必须给出同一个数。
        IReadOnlyDictionary<string, double> rate = DreamContent.DreamEnergyRatePerBuilding;
        double perSecond = 20 * rate["dream_layer"];

        GameEngine online = TestGame.CreateDream(out _);
        online.State.BuildingCounts["dream_layer"] = 20;
        online.MarkDirty();
        online.Simulate(600);
        double ticked = online.State.GetCounter(DreamContent.DreamEnergyCounterKey);
        Check.CloseRelative(perSecond * 600, ticked, 1e-9, "在线 600 秒的离线对照不成立。");

        GameEngine offline = TestGame.CreateDream(out _);
        offline.State.BuildingCounts["dream_layer"] = 20;
        offline.MarkDirty();
        OfflineProgress? recorded = offline.ApplyOfflineProgress(TimeSpan.FromSeconds(600));
        Check.NotNull(recorded, "600 秒离线应当触发结算。");
        OfflineProgress progress = recorded!.Value;

        // 离线按 **CreditedSeconds** 补算（会被 OfflineCapSeconds 截断），所以比的是
        // "模块认可的时长"，而不是墙上时间。
        Check.CloseRelative(
            perSecond * progress.CreditedSeconds,
            offline.State.GetCounter(DreamContent.DreamEnergyCounterKey),
            1e-9);
    }

    [Test]
    public static void DeepDreamScaling_HitsTheEndpointsWrittenInTheText()
    {
        // 手册 §8 的坑：Scaling 的 Cap 限的是**原始计数值**，不是加成结果
        // （Apply = base + PerUnit × min(计数, Cap)）。文案里承诺的是
        // 「10,000 点 → ×3、80,000 点 → ×17 封顶」，所以把三个端点钉死。
        double atZero = CpsWithDreamEnergy(0);
        double atTenThousand = CpsWithDreamEnergy(10_000);
        double atCap = CpsWithDreamEnergy(DreamContent.DreamEnergySoftCap);
        double beyond = CpsWithDreamEnergy(500_000);

        Check.Greater(atZero, 0, "没有梦境能量时产量归零了——玩家再也买不起任何东西，直接死锁。");
        Check.CloseRelative(atTenThousand, atZero * 3, 1e-9, "梦境能量 10,000 点应当是 ×3。");
        Check.CloseRelative(atCap, atZero * 17, 1e-9, "梦境能量 80,000 点应当是 ×17。");
        Check.CloseRelative(beyond, atCap, 1e-9, "梦境能量超过软上限之后应当封顶。");

        // 第 4 层「噩梦层」另外叠了一条更陡的曲线，那一层的乘数必须真的更高。
        Check.Greater(
            CpsWithDreamEnergy(60_000, era: 4),
            CpsWithDreamEnergy(60_000, era: 3),
            "噩梦层的「梦最浓」没有生效——它的额外成长曲线掉队了。");
    }

    [Test]
    public static void DreamEnergy_GatesUpgradesAndAchievementsWithAProgressBar()
    {
        // 第二资源必须"真的有用"：至少一条成长曲线 + 至少 5 处门槛引用它。
        GameContent content = TestGame.Dream;

        int gatedUpgrades = content.Upgrades.Count(u => ReferencesDreamEnergy(u.Unlock));
        int gatedAchievements = content.Achievements.Count(a => ReferencesDreamEnergy(a.Unlock));
        int scaling = content.Upgrades.Count(u => u.Modifiers.Any(m =>
            m.Scaling is { Source: ScalingSource.CustomCounter } s
            && s.Id == DreamContent.DreamEnergyCounterKey))
            + content.Eras.Count(e => e.Modifiers.Any(m =>
                m.Scaling is { Source: ScalingSource.CustomCounter } s
                && s.Id == DreamContent.DreamEnergyCounterKey));

        Check.AtLeast(gatedUpgrades + gatedAchievements, 5, "引用梦境能量的门槛少于 5 处——它只是个装饰。");
        Check.AtLeast(scaling, 3, "以梦境能量成长的曲线太少（每层一条 + 梦浓线至少两条）。");

        // 门槛必须能显示进度（灰按钮上要有进度条，而不是一句空话）。
        foreach (UpgradeDefinition upgrade in content.Upgrades.Where(u => ReferencesDreamEnergy(u.Unlock)))
        {
            Check.True(
                upgrade.Unlock.TryGetProgress(BuildMetrics(0), out _, out double target) && target > 0,
                $"升级「{upgrade.Name}」的梦境能量门槛没有进度可显示。");
        }
    }

    [Test]
    public static void DreamEnergy_RefreshesProduction_WithoutAnyOtherEvent()
    {
        // Step 的顺序是「先重算，再结算，最后才 module.OnTick」——模块在 tick 里改了计数器
        // 并不会自动让产量变脏。这条用例在**只买了一次建筑、之后什么都不做**的前提下
        // 跑 30 秒（第一个梦魇在几十秒之后才可能来），断言产量确实跟着梦境能量涨上去了。
        GameEngine engine = TestGame.CreateDream(out _);
        engine.State.BuildingCounts["dream_layer"] = 200;
        engine.MarkDirty();

        engine.Simulate(1);                      // 让首批"拥有 N 座"的成就先结算掉
        double before = engine.Production.CookiesPerSecond;

        int guard = 0;
        while (engine.State.GetCounter(DreamContent.DreamEnergyCounterKey) < 5_000 && guard++ < 40)
            engine.Simulate(1);

        double after = engine.Production.CookiesPerSecond;
        Check.AtLeast(
            engine.State.GetCounter(DreamContent.DreamEnergyCounterKey),
            5_000,
            "30 秒里梦境能量没涨到 5000——前提不成立，后面的断言无意义。");
        Check.Greater(after, before * 1.05, $"梦境能量涨上去了，产量却停在 {before:F3}——模块改了计数器却没让产量变脏。");
    }

    // ------------------------------------------------------------ 纪元与结局

    [Test]
    public static void Eras_EachLayerChangesTheRulesInItsOwnWay()
    {
        GameContent content = TestGame.Dream;

        // 「五层换手感」是可证伪的：每层的修饰符组合都不一样，而且离线时长/事件频率
        // 这两条"看得见的规则变化"确实各出现在一层上。
        string[] fingerprints = [.. content.Eras.Select(Fingerprint)];
        Check.Equal(fingerprints.Length, fingerprints.Distinct(StringComparer.Ordinal).Count(),
            "有两层的修饰符组合完全相同——那两层的手感是一样的。");

        Check.True(
            content.Eras[1].Balance is { } deep && deep.OfflineCapSeconds > content.Balance.OfflineCapSeconds,
            "第 2 层「深眠」必须把离线结算上限翻倍（那是它在文案里承诺过的规则变化）。");

        foreach (EraDefinition era in content.Eras)
        {
            Check.True(
                era.Modifiers.Any(m => m.Scaling?.Source == ScalingSource.CustomCounter
                                       && m.Scaling.Id == DreamContent.DreamEnergyCounterKey),
                $"第 {era.Index} 层没有以梦境能量成长的曲线——「越深的梦越大」在这一层断了。");
        }

        Check.True(
            content.Eras[2].Modifiers.Any(m => m.Target.Kind == ModifierTargetKind.GoldenCookieFrequency),
            "第 3 层「清明梦」必须改梦魇（金猫）的频率（那是它在文案里承诺过的规则变化）。");
    }

    [Test]
    public static void RobotWalksAllFiveSleepLayers()
    {
        // 与其余纪元包同构：证明这五层门槛在真实曲线下**走得完**。
        GameEngine engine = TestGame.CreateDream(out _, seed: 20240808);
        List<(int Era, double Hours, double Energy)> timeline = [];

        for (int round = 0; round < 60_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                double energy = engine.State.GetCounter(DreamContent.DreamEnergyCounterKey);
                engine.Ascend();
                timeline.Add((before, engine.State.PlayTimeSeconds / 3600, energy));
            }

            engine.Simulate(30);
        }

        foreach ((int era, double hours, double energy) in timeline)
            Console.WriteLine($"      第 {era} 层结束于 {hours:F1} 游戏小时（当时梦境能量 {energy:F0}）");

        Console.WriteLine(
            $"      五层梦用时 {engine.State.PlayTimeSeconds / 3600:F1} 小时；" +
            $"梦境能量 {engine.State.GetCounter(DreamContent.DreamEnergyCounterKey):F0}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.Dream.LoreEntries.Count}；" +
            $"成就 {engine.State.Achievements.Count}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 层——某层的完成条件可能不可达。");
        Check.Equal(4, timeline.Count, "应当正好往下睡 4 次（第 5 层不再往下）。");
    }

    [Test]
    public static void DreamEnergyAtTheEnd_ExceedsTheEndingThreshold()
    {
        // 门槛是"先量包络再设值"定的，所以这条用例就是那次测量的复验：
        // 一次与验收命令同时长的自然游玩（12 游戏小时 = `--simulate 43200`），
        // 必须真的把力气喘够，而且落到承诺型结局上。
        GameEngine engine = LoadFrom(FinishedSave.Value);

        double energy = engine.State.GetCounter(DreamContent.DreamEnergyCounterKey);
        List<string> missing =
        [
            .. engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)),
        ];

        Console.WriteLine(
            $"      睡到最深处时：{engine.State.PlayTimeSeconds / 3600:F1} 游戏小时；" +
            $"梦境能量 {energy:E2}（门槛 {DreamContent.DreamEnergyForEnding:E2}）；" +
            $"梦层类建筑 {DreamLayerBuildings(engine)}；成就 {engine.State.Achievements.Count}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{engine.Content.LoreEntries.Count}；" +
            $"未读到的条目 [{string.Join(", ", missing)}]");

        Check.AtLeast(
            energy,
            DreamContent.DreamEnergyForEnding,
            "12 游戏小时的自然包络没够着结局门槛——那个结局是永远拿不到的死内容。");
        Check.True(
            engine.Content.EndingById["end_wake_her"].Condition.IsMet(engine.Metrics, engine.Content),
            $"探针：结束时「叫醒梦者」的条件应当是成立的（能量 {energy:E2}）。");
        Check.Equal("end_wake_her", engine.ReachedEnding?.Id, "自然游玩应当落到「叫醒梦者」。");
    }

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        Check.Equal("end_wake_her", EndingWith(DreamContent.DreamEnergyForEnding * 2));
        Check.Equal("end_stay_forever", EndingWith(0));

        GameEngine both = CompletedWith(DreamContent.DreamEnergyForEnding * 2);
        Check.True(
            both.Content.EndingById["end_stay_forever"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下兜底结局的条件也应当是成立的。");
        Check.Equal("end_wake_her", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>梦层类建筑总数（诊断用）。</summary>
    private static int DreamLayerBuildings(GameEngine engine)
    {
        int total = 0;
        foreach (BuildingDefinition building in engine.Content.Buildings)
        {
            if (building.Tags.Contains(DreamContent.DreamLayerTag, StringComparer.Ordinal))
                total += engine.State.BuildingCount(building.Id);
        }
        return total;
    }

    /// <summary>把一层的修饰符组合压成一个指纹，用来断言"每层都不一样"。</summary>
    private static string Fingerprint(EraDefinition era)
        => string.Join(
            ";",
            era.Modifiers
                .Select(m => $"{m.Target.Kind}:{m.Target.Id}:{m.Operation}:{m.Value:R}" +
                             (m.Scaling is { } s ? $"/{s.Source}:{s.Id}:{s.PerUnit:R}:{s.Cap:R}" : string.Empty))
                .Order(StringComparer.Ordinal));

    private static bool ReferencesDreamEnergy(UnlockCondition condition)
        => condition.NumericLeaves().Any(leaf =>
            leaf.Metric == NumericMetric.Counter && leaf.Id == DreamContent.DreamEnergyCounterKey);

    /// <summary>在当前内容上取一个只读的指标视图（不推进任何状态）。</summary>
    private static IGameMetrics BuildMetrics(double dreamEnergy)
    {
        GameEngine engine = TestGame.CreateDream(out _);
        engine.State.SetCounter(DreamContent.DreamEnergyCounterKey, dreamEnergy);
        engine.MarkDirty();
        return engine.Metrics;
    }

    /// <summary>在指定层、指定梦境能量下的每秒产量（端点断言用）。</summary>
    private static double CpsWithDreamEnergy(double dreamEnergy, int era = 1)
    {
        GameEngine engine = TestGame.CreateDream(out _);
        for (int index = 1; index < era; index++) engine.State.Era = index + 1;
        engine.State.BuildingCounts["dream_layer"] = 100;
        engine.State.SetCounter(DreamContent.DreamEnergyCounterKey, dreamEnergy);
        engine.MarkDirty();
        return engine.Production.CookiesPerSecond;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateDream(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>一次真实游玩，停在"刚进入最后一层"这一刻。</summary>
    private static readonly Lazy<string> LastLayerSave = new(() => PlayToLayer(TestGame.Dream.MaxEraIndex).Save());

    /// <summary>从"刚进入第 5 层"出发，让机器人自己把最里面那层睡完。</summary>
    private static readonly Lazy<string> FinishedSave = new(() => PlayToTheEnd().Save());

    private static GameEngine PlayToLayer(int target)
    {
        GameEngine engine = TestGame.CreateDream(out _, seed: 20240808);

        for (int round = 0; round < 60_000 && engine.State.Era < target; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(target, engine.State.Era, $"机器人没能走到第 {target} 层。");
        Check.Null(engine.ReachedEnding, "还没睡到最深处就判定结局了。");
        return engine;
    }

    private static GameEngine PlayToTheEnd()
    {
        GameEngine engine = LoadFrom(LastLayerSave.Value);

        // 最里面那一层不设"完成就停"——这个包的承诺型结局要求玩家在梦核旁边
        // 攒够力气，而那件事只能靠时间（实测落在第 9 个游戏小时前后）。
        // 上界取 12 游戏小时：与验收命令 `--simulate 43200` 同一把尺子。
        for (int round = 0; round < 40_000; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            engine.Simulate(30);
            if (engine.State.PlayTimeSeconds > 12 * 3600) break;
        }

        Check.NotNull(engine.ReachedEnding, "机器人没能睡到最深处。");
        return engine;
    }

    /// <summary>
    /// 造出"末层主线刚好完成"的那一刻。<para>
    /// 必须从 <see cref="LastLayerSave"/> 出发而不是从终局存档出发：结局一旦判定就被记下、
    /// 再也不重判（<c>EndingSystem</c> 的规则），拿一份已经落地的存档是验不出互斥性的。
    /// 主线含一个时长门槛，所以这里也要把 <c>PlayTimeSeconds</c> 顶上去。
    /// </para>
    /// </summary>
    private static GameEngine CompletedWith(double dreamEnergy)
    {
        GameEngine engine = LoadFrom(LastLayerSave.Value);
        engine.State.SetCounter(DreamContent.DreamEnergyCounterKey, dreamEnergy);
        engine.State.CookiesEarnedThisRun = 1e12;
        engine.State.PlayTimeSeconds = 6 * 3600;
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(double dreamEnergy)
    {
        GameEngine engine = CompletedWith(dreamEnergy);
        Check.NotNull(engine.ReachedEnding, "睡到最深处之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
