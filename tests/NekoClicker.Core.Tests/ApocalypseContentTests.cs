using NekoClicker.Content.Apocalypse;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #6《猫娘末世》的验收（阶段 4 的前半）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>跨转生继承</b>——全项目第一个用 <c>InheritBuildingRatio</c> 的包；
///   继承比例必须在真跑里真的生效，而不是"配置里写了"。</item>
///   <item><b>继承是数值的一部分</b>——记忆残片的产率取决于上一轮留下多少，
///   所以"留下的东西会继续影响下一轮"，不是一个静态事实。</item>
///   <item><b>没有立场轴的结局</b>——三个结局由"记住了多少 + 图鉴读了多少"决定，
///   与另外四个包的立场驱动结局共用同一套 <c>EndingDefinition</c>。</item>
/// </list>
/// </para>
/// </summary>
public static class ApocalypseContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Apocalypse;

        Check.Equal("猫娘末世", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五次重启，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、三个结局。这个包刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(3, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        // 同条件的条目必然同时解锁。别的包的这条规则由 LoreTests 守着，
        // 这里给末世包补一份——内容包不该靠"没人测"来通过。
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Apocalypse.LoreEntries)
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
        GameContent content = TestGame.Apocalypse;

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
        // 咖啡馆那次有 11 处倒挂，全是"看起来没问题"写出来的。所以这里不推理，
        // 直接在真实游玩里记录每一条的解锁时刻，再验同一线内序号与时刻同序。
        //
        // 用"第几次循环"而不是循环内的先后：同一拍解锁的两条谁先谁后由 HashSet 决定，
        // 拿它当判据会误报。跨拍倒挂才是真倒挂。
        GameEngine engine = TestGame.CreateApocalypse(out _, seed: 20240924);
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
        Console.WriteLine(
            $"      结束时：记忆残片 {engine.State.GetCounter(ApocalypseContent.ShardsCounterKey):F0}；"
            + $"图鉴 {engine.State.LoreUnlocked.Count}；成就 {engine.State.Achievements.Count}；"
            + $"未读到的条目 [{string.Join(", ", engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)))}]");
    }

    [Test]
    public static void EveryStorylineOpensEarlyEnough()
    {
        int early = 0;

        foreach (StorylineDefinition storyline in TestGame.Apocalypse.Storylines)
        {
            LoreEntry first = TestGame.Apocalypse.LoreOf(storyline.Id).First();
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
        GameEngine engine = TestGame.CreateApocalypse(out _, seed: 4242);

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

    [Test]
    public static void MemoryShards_AreProducedOnlyByWhatSurvived()
    {
        GameEngine engine = TestGame.CreateApocalypse(out _);

        // 第一轮没有任何东西被留下 → 记忆残片恒为 0。
        // 这不是"还没跑到"，是设计：第 1 轮里"记忆"这个概念还不存在。
        engine.Simulate(600);
        Check.Close(0, engine.State.GetCounter(ApocalypseContent.ShardsCounterKey), 1e-9);

        // 造一个"上一轮留下了 120 座建筑"的局面，然后重启。
        engine.State.BuildingCounts["ruins"] = 100;
        engine.State.BuildingCounts["generator"] = 20;
        engine.State.CookiesEarnedThisRun = 2e5;
        engine.MarkDirty();

        Check.True(engine.EraGate.CanAdvance, "第 1 层的完成条件应当已经满足。");
        engine.Ascend();

        // 第 2 层继承 25%，向下取整：废墟 25 座、发电机 5 座。
        Check.Equal(25, engine.State.BuildingCount("ruins"));
        Check.Equal(5, engine.State.BuildingCount("generator"));

        // 产率 = 留下来的座位数 ÷ 5；重启之后才开始涨。
        double before = engine.State.GetCounter(ApocalypseContent.ShardsCounterKey);
        engine.Simulate(10);
        Check.Close(
            before + 30 * 10 / ApocalypseContent.SeatsPerShardPerSecond,
            engine.State.GetCounter(ApocalypseContent.ShardsCounterKey),
            1e-6);

        // 再次重启：比例写到目标层上（第 3 层 40% + 发电机白名单），所以发电机不会再按比例缩水。
        engine.State.CookiesEarnedThisRun = 4e8;
        engine.State.Achievements.Clear();
        for (int i = 0; i < 10; i++) engine.State.Achievements.Add($"dummy_{i}");
        engine.MarkDirty();
        Check.True(engine.EraGate.CanAdvance, "第 2 层的完成条件应当已经满足。");
        engine.Ascend();

        Check.Equal(10, engine.State.BuildingCount("ruins"), "废墟应当按 40% 继承（25 → 10）。");
        Check.Equal(5, engine.State.BuildingCount("generator"), "白名单里的建筑应当原样留下。");
    }

    [Test]
    public static void InheritBuildingRatio_DefaultsToZero_Elsewhere()
    {
        // 回归：继承参数默认 0，别的包一个都没碰过它——它们的重启语义仍然是"全清"。
        foreach (GameContent content in new[] { TestGame.NineLives, TestGame.Lab, TestGame.Company })
        {
            foreach (EraDefinition era in content.Eras)
            {
                Check.Equal(0.0, era.InheritBuildingRatio, $"「{content.Title}」的第 {era.Index} 层不该继承建筑。");
                Check.Equal(0, era.InheritBuildings.Count, $"「{content.Title}」的第 {era.Index} 层不该有白名单。");
            }
        }

        // 而末世逐层加码：0 → 25% → 40% → 55% → 70%。
        double[] expected = [0, 0.25, 0.4, 0.55, 0.7];
        for (int i = 0; i < expected.Length; i++)
            Check.Equal(expected[i], TestGame.Apocalypse.Eras[i].InheritBuildingRatio);
    }

    [Test]
    public static void OwnedBuildings_StayUnlockedAfterARestart()
    {
        // 继承暴露出的一个坑：解锁条件若用"本轮累计"，重启归零之后，
        // 你手上那 50 座建筑会显示成「未解锁」——产量照算，但列表里看不到、也卖不掉。
        // 末世的解锁条件一律用"历史累计"，这条用例把它钉住。
        GameEngine engine = TestGame.CreateApocalypse(out _);
        engine.State.CookiesEarnedAllTime = 2e9;   // 解锁条件用的是历史累计，先把它顶到全部解锁之上
        engine.State.BuildingCounts["relic_city"] = 7;
        engine.State.BuildingCounts["data_tower"] = 12;
        engine.State.CookiesEarnedThisRun = 2e5;
        engine.MarkDirty();
        engine.Ascend();

        Check.Equal(1, engine.State.BuildingCount("relic_city"), "第 2 层应当继承 25%（7 → 1）。");
        SnapshotAndCheckUnlocked(engine);

        // 再来一次，确保不是"第一次重启恰好没事"。
        engine.State.CookiesEarnedThisRun = 4e8;
        for (int i = 0; i < 10; i++) engine.State.Achievements.Add($"dummy_{i}");
        engine.MarkDirty();
        engine.Ascend();
        SnapshotAndCheckUnlocked(engine);
    }

    [Test]
    public static void RobotWalksAllFiveRestarts()
    {
        // 与九命 / 实验室包同构：证明这五层门槛在真实曲线下**走得完**，
        // 而不只是"配置看起来合理"。阶段 2.7 就是被这类测试救回来的。
        GameEngine engine = TestGame.CreateApocalypse(out _, seed: 20240924);
        List<(int Era, double Hours)> timeline = [];

        for (int round = 0; round < 60_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                engine.Ascend();
                timeline.Add((before, engine.State.PlayTimeSeconds / 3600));
            }

            engine.Simulate(30);
        }

        foreach ((int era, double hours) in timeline)
            Console.WriteLine($"      第 {era} 次重启结束于 {hours:F1} 游戏小时");

        Console.WriteLine(
            $"      五次重启用时 {engine.State.PlayTimeSeconds / 3600:F1} 小时；" +
            $"记忆残片 {engine.State.GetCounter(ApocalypseContent.ShardsCounterKey):F0}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.Apocalypse.LoreEntries.Count}；" +
            $"成就 {engine.State.Achievements.Count}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 次重启——某层的完成条件可能不可达。");
        Check.Equal(4, timeline.Count, "应当正好重启 4 次（第 5 层不再重启）。");
    }

    [Test]
    public static void MemoryShards_ExceedTheEndingThreshold_InARealRun()
    {
        // 阈值不能靠推理定。这条用例守在"真实跑图里够不够得着"这一侧：
        // 够不着的话，结局再怎么写也只是文档。
        GameEngine engine = LoadFrom(LastRestartSave.Value);

        Check.Equal(5, engine.State.Era, "前提：机器人已经走到最后一次重启。");
        Check.AtLeast(
            engine.State.GetCounter(ApocalypseContent.ShardsCounterKey),
            ApocalypseContent.ShardsForEnding,
            "走完五次重启之后，记忆残片仍然够不到结局门槛——那个结局永远拿不到。");
    }

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        // 这个包没有立场轴，所以结局不是"你表过什么态"决定的，而是"你记住了多少 +
        // 图鉴读了多少"决定的。三段验证共用同一次真实跑图（读档三份，互不影响）：
        // 记得够多且读得够全 → 复活人类；记得够多但没读全 → 成为新人类；什么都没留下 → 安静结束。
        Check.Equal("end_revive", EndingWith(enoughShards: true, readEnough: true));
        Check.Equal("end_newhuman", EndingWith(enoughShards: true, readEnough: false));
        Check.Equal("end_quiet", EndingWith(enoughShards: false, readEnough: false));

        // 第一行那个状态<b>同时</b>满足 end_revive 与 end_newhuman 的条件，拿到的是
        // Priority 更小的那个——这才是"互斥"的实现方式：判定按优先级取第一个，记下之后不再判。
        GameEngine both = CompletedWith(enoughShards: true, readEnough: true);
        Check.True(
            both.Content.EndingById["end_newhuman"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下「成为新人类」的条件也应当是成立的。");
        Check.Equal("end_revive", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }



    // ---------------------------------------------------------------- 辅助

    private static void SnapshotAndCheckUnlocked(GameEngine engine)
    {
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.BuyMax);

        foreach (BuildingView building in snapshot.Buildings)
        {
            if (building.Owned <= 0) continue;
            Check.True(
                building.IsUnlocked,
                $"拥有 {building.Owned} 座「{building.Name}」，它在列表里却是未解锁的。");
        }
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateApocalypse(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>
    /// 一次真实游玩，停在"机器人刚进入第 5 层"这一刻。<para>
    /// 只跑一次，用例各读一份，互不影响——与九命 / 公司包的结局验收同构。
    /// 停在进入第 5 层而不是更早，是因为末层的完成门槛要真的够得着；
    /// 停在终点也不行——那时结局已经判过了，三份读档会拿到同一个答案。
    /// </para>
    /// </summary>
    private static readonly Lazy<string> LastRestartSave = new(() => PlayToEra(5).Save());

    private static GameEngine PlayToEra(int target)
    {
        GameEngine engine = TestGame.CreateApocalypse(out _, seed: 20240924);

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
        Check.Null(engine.ReachedEnding, "还没走完主线就判定结局了。");
        return engine;
    }

    /// <summary>
    /// 从"刚进第 5 层"的状态出发，把记忆量与图鉴调到指定水平，然后推完末层主线。
    /// </summary>
    /// <param name="enoughShards">是否给够记忆残片。</param>
    /// <param name="readEnough">是否把图鉴补到「复活人类」要求的条数。</param>
    private static GameEngine CompletedWith(bool enoughShards, bool readEnough)
    {
        GameEngine engine = LoadFrom(LastRestartSave.Value);

        engine.State.SetCounter(
            ApocalypseContent.ShardsCounterKey,
            enoughShards ? ApocalypseContent.ShardsForEnding * 2 : 0);

        if (!readEnough)
        {
            engine.State.LoreUnlocked.Clear();
        }
        else
        {
            foreach (LoreEntry entry in engine.Content.LoreEntries)
            {
                if (engine.State.LoreUnlocked.Count >= ApocalypseContent.LoreForRevival) break;
                engine.State.LoreUnlocked.Add(entry.Id);
            }
        }

        engine.State.CookiesEarnedThisRun = 1.5e11;
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(bool enoughShards, bool readEnough)
    {
        GameEngine engine = CompletedWith(enoughShards, readEnough);
        Check.NotNull(engine.ReachedEnding, "推完末层主线之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
