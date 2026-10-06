using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 叙事释放系统（S-B）—— 阶段 2 的验收。<para>
/// ROADMAP §9 的四条特有验收：
/// <list type="number">
///   <item>图鉴进度与 <c>TotalEntries</c> 一致。</item>
///   <item>同一剧情线内序号不重复。</item>
///   <item>前 10 分钟释放 ≤3 条（G5：剧情不该一次讲完）。</item>
///   <item>存档往返保留已释放的条目。</item>
/// </list>
/// </para>
/// </summary>
public static class LoreTests
{
    // ---------------------------------------------------------------- 内容自洽

    [Test]
    public static void StorylineTotals_MatchActualEntryCounts()
    {
        foreach (GameContent content in new[] { TestGame.CafeContent, TestGame.NineLives })
        {
            Check.AtLeast(content.LoreEntries.Count, 40, $"{content.Title} 的叙事条目太少。");

            int declared = 0;
            foreach (StorylineDefinition storyline in content.Storylines)
            {
                declared += storyline.TotalEntries;
                int actual = content.LoreOf(storyline.Id).Count;
                Check.Equal(storyline.TotalEntries, actual, $"「{storyline.Name}」声明的条数与实际不一致。");
            }

            Check.Equal(content.LoreEntries.Count, declared, $"{content.Title} 的声明总数与实际总数不一致。");
        }
    }

    [Test]
    public static void Orders_AreUniqueWithinEachStoryline()
    {
        foreach (GameContent content in new[] { TestGame.CafeContent, TestGame.NineLives })
        {
            foreach (StorylineDefinition storyline in content.Storylines)
            {
                List<int> orders = [.. content.LoreOf(storyline.Id).Select(e => e.Order)];
                Check.Equal(orders.Count, orders.Distinct().Count(), $"「{storyline.Name}」里有重复序号。");

                // 序号即阅读顺序，图鉴按它排列。有空洞说明改过条数却忘了重排。
                List<int> sorted = [.. orders.OrderBy(o => o)];
                for (int i = 0; i < sorted.Count; i++)
                    Check.Equal(i + 1, sorted[i], $"「{storyline.Name}」的序号不连续（缺 {i + 1}）。");
            }
        }
    }

    [Test]
    public static void RevealConditions_AreUniqueWithinEachPack()
    {
        // 同一个条件的条目<b>必然在同一瞬间一起解锁</b>——首版 50 条里有 23 条落在 10 个重复组里，
        // 九命最坏一次放出 6 条。这类错误是沉默失败：测试不会红，只有玩到那一段才看得出来
        // （G5 只管开局 10 分钟）。所以把"撞车"直接变成构建期的硬错误。
        //
        // 键用叶子多重集而不是 Describe()：Describe 会用 NumFormat 格式化数值，
        // 1e10 与 1.05e10 可能格式化成同一个字符串，拿它当键会误报。
        // 当前所有 Reveal 都由 All / 单叶子构成，叶子多重集对它们是忠实映射。
        foreach (GameContent content in new[] { TestGame.CafeContent, TestGame.NineLives })
        {
            Dictionary<string, string> seen = new(StringComparer.Ordinal);
            foreach (LoreEntry entry in content.LoreEntries)
            {
                string key = RevealKey(entry.Reveal);
                if (seen.TryGetValue(key, out string? first))
                {
                    Check.True(
                        false,
                        $"{content.Title}：{entry.Id} 与 {first} 的释放条件完全相同——它们会同时解锁。"
                        + "把数值错开（别挑圆整数）。");
                }
                seen[key] = entry.Id;
            }
        }
    }

    [Test]
    public static void EveryStoryline_OpensReachablyAndMostOpenEarly()
    {
        foreach (GameContent content in new[] { TestGame.CafeContent, TestGame.NineLives })
        {            int earlyOpeners = 0;

            foreach (StorylineDefinition storyline in content.Storylines)
            {
                LoreEntry first = content.LoreOf(storyline.Id).First();
                double target = FirstThreshold(first.Reveal);

                // 每条线都得能读到——不能有"永远放不出来"的剧情。
                Check.Finite(target, $"「{storyline.Name}」的开篇条件无法量化。");
                Check.AtMost(target, 1e12, $"「{storyline.Name}」的开篇门槛高到几乎读不到。");

                // 早期开篇（点击 / 少量建筑 / 很少的累计）：至少一半的线要满足，
                // 否则图鉴一开就是一整墙 ???。晚开的线是刻意的（例如"人类遗毒"要等到后期）。
                if (target <= 200) earlyOpeners++;
            }

            Check.AtLeast(
                earlyOpeners,
                Math.Max(1, content.Storylines.Count / 2),
                $"{content.Title} 里真正早期能读到的剧情线太少，图鉴开场就全是 ???。");
        }
    }

    [Test]
    public static void EraGatedLore_StaysBelowItsEraCompletion()
    {
        // 阶段 3B 定下的纪律：挂了纪元门槛的叙事，其层内门槛必须**低于**该层的完成门槛。
        // 否则玩家会在够条件前舍命走人，这条永远读不到——而运行期完全看不出来
        // （它只是"一直没出现"），所以必须由测试拦住。
        //
        // 这条纪律以前只写在文档里。#9 的 she_12 挂在 1.4e11 上、而第 5 本的完成门槛是 1e11，
        // 于是它是**结构上读不到**的死内容（最后一条还是靠"冲过头"才偶然读到的），
        // 所以把它变成通用守卫，对全部内容包生效。
        foreach (GameContent content in AllPacks())
        {
            foreach (LoreEntry entry in content.LoreEntries)
            {
                NumericCondition? eraLeaf = entry.Reveal.NumericLeaves()
                    .FirstOrDefault(leaf => leaf.Metric == NumericMetric.Era);
                NumericCondition? runLeaf = entry.Reveal.NumericLeaves()
                    .FirstOrDefault(leaf => leaf.Metric == NumericMetric.CookiesEarnedThisRun);

                if (eraLeaf is null || runLeaf is null) continue;
                if (!content.EraByIndex.TryGetValue((int)eraLeaf.Target, out EraDefinition? era)) continue;

                // 该层没用量级门槛（例如只考成就数），就没有可比的上限，跳过。
                NumericCondition? ceiling = era.Completion.NumericLeaves()
                    .FirstOrDefault(leaf => leaf.Metric == NumericMetric.CookiesEarnedThisRun);
                if (ceiling is null) continue;

                Check.True(
                    runLeaf.Target < ceiling.Target,
                    $"{content.Title} 的 {entry.Id} 门槛是 {runLeaf.Target:R}，"
                    + $"而第 {(int)eraLeaf.Target} 层的完成门槛是 {ceiling.Target:R}——"
                    + "两层一跨过去玩家就舍命走人了，这条永远读不到。");
            }
        }
    }

    /// <summary>全部内容包（守卫用例要横扫每一个，而不是只盯着最近改的那个）。</summary>
    private static GameContent[] AllPacks() =>
        [.. TestGame.AllContentPacks().Select(p => p.Content)];

    [Test]
    public static void Era9Rule_UsesLoreCount()
    {
        // 图书馆纪元的"被阅读量"应当由叙事数驱动，而不是别的替身指标。
        EraDefinition era9 = TestGame.NineLives.FindEra(9)!;
        Check.True(
            era9.Modifiers.Any(m => m.Scaling?.Source == ScalingSource.LoreCount),
            "第九命的规则应当以 ScalingSource.LoreCount 成长。");
    }

    // ---------------------------------------------------------------- G5：不许一次讲完

    [Test]
    public static void G5_FirstTenMinutesRevealAtMostThreeEntries()
    {
        foreach ((string name, GameEngine engine) in new[]
        {
            ("猫娘咖啡馆", TestGame.CreateCafe(out _, seed: 4242)),
            ("九命轮回", TestGame.CreateNineLives(out _, seed: 4242)),
        })
        {
            for (int second = 0; second < 600; second += 5)
            {
                for (int i = 0; i < 2; i++) engine.Click();
                TestGame.BuyGreedily(engine);
                engine.Simulate(5);
            }

            Check.AtMost(
                engine.State.LoreUnlocked.Count,
                3,
                $"{name} 在开局 10 分钟内释放了 {engine.State.LoreUnlocked.Count} 条剧情——世界观被一次性讲掉了。");
            Check.AtLeast(engine.State.LoreUnlocked.Count, 1, $"{name} 在开局 10 分钟内一条都没放出来。");
        }
    }

    // ---------------------------------------------------------------- 释放机制

    [Test]
    public static void Reveal_FiresOnce_AndPublishesEvent()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        List<string> events = [];
        using IDisposable subscription = engine.Events.Subscribe<LoreRevealedEvent>(e => events.Add(e.Id));

        engine.Click();
        engine.CheckLore();
        int afterFirst = engine.State.LoreUnlocked.Count;
        Check.AtLeast(afterFirst, 1);

        engine.CheckLore();
        Check.Equal(afterFirst, engine.State.LoreUnlocked.Count, "重复检查不应重复释放。");
        Check.Equal(afterFirst, events.Count);
    }

    [Test]
    public static void LogChannel_GoesToNotifications()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();

        LoreEntry revealed = engine.Content.LoreEntries.First(e => engine.State.LoreUnlocked.Contains(e.Id));
        Check.Equal(LoreChannel.Popup, revealed.Channel, "第一条应当是弹窗——开场需要一个明确的钩子。");
        Check.True(engine.State.PendingLorePopups.Contains(revealed.Id));
    }

    [Test]
    public static void PopupChannel_IsDismissable_AndNotLogged()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();

        string id = engine.State.PendingLorePopups.First();
        Check.True(engine.DismissLorePopup(id));
        Check.False(engine.State.PendingLorePopups.Contains(id));
        Check.False(engine.DismissLorePopup(id), "重复点掉应当返回 false。");
    }

    [Test]
    public static void PendingLore_AppearsInSnapshot()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();

        GameSnapshot snapshot = engine.Snapshot();
        Check.Equal(engine.State.PendingLorePopups.Count, snapshot.PendingLore.Count);
        Check.Equal(1, snapshot.PendingLore.Count);
        Check.True(snapshot.PendingLore[0].Unlocked);

        engine.DismissAllLorePopups();
        Check.Equal(0, engine.Snapshot().PendingLore.Count);
    }

    // ---------------------------------------------------------------- 纸条（通道 = Note）

    /// <summary>
    /// 纸条**没有自己的队列、也没有自己的"看过"集合**：它与剧情弹窗共用
    /// <c>PendingLorePopups</c> 与 <c>LoreUnlocked</c>——而这两个字段在 v1 存档格式里早就有，
    /// 所以"捡到过没有"这件事不需要动存档格式、也不需要迁移。
    /// 这条用例守的就是这件事：一旦有人给纸条开了第二套状态，它会红。
    /// 决定与代价见 <c>engine/docs/FOUND_NOTES_PLAN.md</c> §3.2。
    /// </summary>
    [Test]
    public static void NoteChannel_UsesTheSamePendingQueue_AndTheSameSeenSet()
    {
        GameEngine engine = TestGame.CreateApocalypse(out _);

        LoreEntry buy = engine.Content.LoreEntries.First(e => e.Id == "note_buy");
        Check.Equal(LoreChannel.Note, buy.Channel);

        // 门槛是"第一次**买得起**最便宜那座"（不是"买下了"）：纸条要在玩家动手之前到手。
        double price = engine.Content.Buildings.First(b => b.Id == "ruins").BasePrice;

        engine.CheckLore();
        Check.False(engine.State.PendingLorePopups.Contains("note_buy"), "还没买得起就不该捡到纸条。");

        while (engine.State.Cookies < price) engine.Click();
        engine.CheckLore();

        Check.True(engine.State.PendingLorePopups.Contains("note_buy"));
        Check.True(engine.State.LoreUnlocked.Contains("note_buy"), "待读与已读是两件事：它同时进了已读集合。");

        // 点掉 = 从待读队列划掉；它不会**再放一次**（已读集合拦着，与剧情同一条规矩）。
        Check.True(engine.DismissLorePopup("note_buy"));
        engine.CheckLore();
        Check.False(engine.State.PendingLorePopups.Contains("note_buy"));
    }

    /// <summary>
    /// 通道名是服务端给的 token：前端据此决定"这一条用哪张皮"，而**不需要**去解释枚举序数
    /// （与 <c>mode</c> / <c>modeName</c> 同一条规矩）。反面对照是剧情弹窗仍然是 <c>popup</c>——
    /// 两者在线上必须分得开，否则界面只能靠剧情线 id 去猜，那等于把内容知识写进前端。
    /// </summary>
    [Test]
    public static void NoteChannel_CarriesItsOwnWireToken_SoTheFrontendNeedNotReadOrdinals()
    {
        GameEngine engine = TestGame.CreateApocalypse(out _);
        while (engine.State.Cookies < engine.Content.Buildings.First(b => b.Id == "ruins").BasePrice) engine.Click();
        engine.CheckLore();

        LoreView note = engine.Snapshot().PendingLore.First(v => v.Id == "note_buy");
        Check.Equal("note", note.ChannelName);

        GameEngine cafe = TestGame.CreateCafe(out _);
        cafe.Click();
        cafe.CheckLore();
        Check.Equal("popup", cafe.Snapshot().PendingLore[0].ChannelName);
    }

    /// <summary>
    /// 金猫那张纸条的门槛是"第一次**真的有猫出现在场上**"——引擎自己写的
    /// <c>GoldenCookieSystem.SerialCounterKey</c> 计数器，而不是"第一次抓到"
    /// （<c>GoldenCookiesAtLeast(1)</c>）。理由与"错过"为什么判不了，见
    /// <c>engine/docs/FOUND_NOTES_PLAN.md</c> §5.4。
    /// </summary>
    [Test]
    public static void GoldenCatNote_FiresOnTheFirstSighting_NotOnTheFirstCatch()
    {
        GameEngine engine = TestGame.CreateApocalypse(out _);
        double price = engine.Content.Buildings.First(b => b.Id == "ruins").BasePrice;

        engine.CheckLore();
        Check.False(engine.State.PendingLorePopups.Contains("note_cat"), "还没有猫的时候不该有这张纸条。");

        Check.Equal(0, engine.State.GetCounter(GoldenCookieSystem.SerialCounterKey));
        GoldenCookieSystem.Spawn(engine);
        engine.CheckLore();
        Check.False(
            engine.State.PendingLorePopups.Contains("note_cat"),
            "光有猫还不够：第一张纸条（买东西）得先生效——顺序由构造保证，见 FOUND_NOTES_PLAN §4。");

        // 赚够第一座建筑的钱 = 第一张纸条的门槛。此后金猫那张才成立。
        while (engine.State.CookiesEarnedThisRun < price) engine.Click();
        engine.CheckLore();

        Check.True(engine.State.PendingLorePopups.Contains("note_cat"));
        Check.Equal(0, engine.State.GoldenCookiesClicked, "这条刻意**不**点猫：纸条是「看见了」就有的。");

        // 同一线里"先来的排在前面"：界面一次只画一张，靠的就是这个顺序。
        int buyIndex = engine.State.PendingLorePopups.IndexOf("note_buy");
        int catIndex = engine.State.PendingLorePopups.IndexOf("note_cat");
        Check.True(buyIndex >= 0 && buyIndex < catIndex, "先捡到「领料单」，再捡到「窗上的字条」。");
    }

    /// <summary>
    /// 纸条的正文就是那张纸上的字，所以它有两个特征要被钉住：
    /// ① 从 <c>text.json</c> 读（双向逐字那条守卫横扫全部条目，见 <c>ContentTextFileTests</c>）；
    /// ② **有换行**——纸条是"清单 / 落款"这种形状，清单没有换行就不成清单。
    /// 第二条是这条用例存在的唯一理由：它把"纸条该长什么样"钉在**数据**上，
    /// 而不是靠下一个人记得去写（正文一个字都不许是"一句话说明"）。
    /// </summary>
    [Test]
    public static void NoteChannel_TextIsALeafObject_WithLineBreaks()
    {
        foreach (string id in new[] { "note_buy", "note_cat" })
        {
            LoreEntry entry = TestGame.Apocalypse.LoreEntries.First(e => e.Id == id);
            Check.Equal(LoreChannel.Note, entry.Channel);
            Check.Contains(entry.Body, "\n", $"{id} 的正文没有换行——那就不像一张纸了。");
        }
    }

    // ---------------------------------------------------------------- 图鉴

    [Test]
    public static void Codex_IsNullForPacksWithoutLore()
    {
        Check.Null(TestGame.CreateNeko(out _).Snapshot().Codex, "没有叙事条目的包不该有图鉴。");
    }

    [Test]
    public static void Codex_ShowsProgressPerStoryline()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();

        CodexView codex = engine.Snapshot().Codex!;

        Check.Equal(engine.Content.Storylines.Count, codex.Storylines.Count);
        Check.Equal(engine.Content.LoreEntries.Count, codex.TotalEntries);
        Check.Equal(engine.State.LoreUnlocked.Count, codex.TotalUnlocked);

        int summed = codex.Storylines.Sum(s => s.Unlocked);
        Check.Equal(codex.TotalUnlocked, summed, "各线已解锁数之和应等于总数。");

        foreach (StorylineView storyline in codex.Storylines)
        {
            Check.Equal(storyline.Total, storyline.Entries.Count);
            Check.True(storyline.Progress is >= 0 and <= 1);
        }
    }

    [Test]
    public static void Codex_ConcealsLockedEntriesButKeepsTheChecklist()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        CodexView codex = engine.Snapshot().Codex!;

        Check.Equal(0, codex.TotalUnlocked);

        foreach (StorylineView storyline in codex.Storylines)
        {
            foreach (LoreView entry in storyline.Entries)
            {
                Check.False(entry.Unlocked);
                Check.Equal("???", entry.Title, "未解锁的条目标题必须被藏起来。");
                Check.Equal(string.Empty, entry.Body, "未解锁的条目正文必须被藏起来。");
                Check.Equal("🔒", entry.Icon);
                // 但"怎么才能读到"必须给出来，否则图鉴只是一墙问号。
                Check.True(entry.RevealHint.Length > 0, $"{entry.Id} 缺少释放条件描述。");
            }
        }
    }

    [Test]
    public static void Codex_RevealsTitleAndBodyOnceUnlocked()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();

        string id = engine.State.PendingLorePopups.First();
        LoreView view = engine.Snapshot().Codex!.Storylines
            .SelectMany(s => s.Entries)
            .First(e => e.Id == id);

        Check.True(view.Unlocked);
        Check.NotEqual("???", view.Title);
        Check.True(view.Body.Length > 0);
        Check.NotEqual("🔒", view.Icon);
    }

    // ---------------------------------------------------------------- 存档

    [Test]
    public static void SaveRoundTrip_PreservesLoreAndPopups()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.Click();
        engine.CheckLore();
        int unlocked = engine.State.LoreUnlocked.Count;
        Check.AtLeast(unlocked, 1);

        string json = engine.Save();

        GameEngine restored = TestGame.CreateCafe(out _);
        restored.Load(json);

        Check.Equal(unlocked, restored.State.LoreUnlocked.Count);
        Check.Equal(
            engine.State.PendingLorePopups.Count,
            restored.State.PendingLorePopups.Count,
            "待点掉的弹窗也要跨存档保留，否则读档后弹窗会消失。");
        Check.False(restored.Snapshot().PendingLore.Count == 0);
    }

    // ---------------------------------------------------------------- 计数指标

    [Test]
    public static void LoreCount_DrivesConditionsAndScaling()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        Check.Equal(0, engine.Metrics.LoreCount);

        UnlockCondition condition = UnlockCondition.LoreAtLeast(3);
        Check.False(condition.IsMet(engine.Metrics, engine.Content));
        Check.True(condition.TryGetProgress(engine.Metrics, out double current, out double target));
        Check.Close(0, current);
        Check.Close(3, target);

        engine.State.LoreUnlocked.Add("nine_01");
        engine.State.LoreUnlocked.Add("nine_02");
        engine.State.LoreUnlocked.Add("nine_03");
        Check.Equal(3, engine.Metrics.LoreCount);
        Check.True(condition.IsMet(engine.Metrics, engine.Content));
        Check.Contains(condition.Describe(engine.Content), "记忆");
    }

    // ---------------------------------------------------------------- 构建期校验

    [Test]
    public static void Validator_RejectsStorylineTotalMismatch()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 5 })
                .AddLore(Entry("s_1", "s", 1))
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "声明了 5 条");
    }

    [Test]
    public static void Validator_RejectsDuplicateOrder()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 2 })
                .AddLore(Entry("s_1", "s", 1), Entry("s_2", "s", 1))
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "序号");
    }

    [Test]
    public static void Validator_RejectsUnknownStoryline()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X").AddLore(Entry("ghost_1", "ghost", 1)).Build());

        Check.Contains(string.Join("\n", ex.Errors), "ghost");
    }

    [Test]
    public static void Validator_RejectsNeverReveal()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 1 })
                .AddLore(new LoreEntry
                {
                    Id = "s_1",
                    Title = "T",
                    Body = "B",
                    StorylineId = "s",
                    Order = 1,
                    Reveal = UnlockCondition.Never,
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "永远放不出来");
    }

    [Test]
    public static void Validator_RejectsEraTextChannel()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 1 })
                .AddLore(new LoreEntry
                {
                    Id = "s_1",
                    Title = "T",
                    Body = "B",
                    StorylineId = "s",
                    Order = 1,
                    Reveal = UnlockCondition.Always,
                    Channel = LoreChannel.EraText,
                })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "EraText");
    }

    [Test]
    public static void Validator_RejectsUnreachableLore()
    {
        // 依赖一个解不开的升级 → 这段剧情永远读不到，构建期就该报错。
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 1 })
                .Add(new UpgradeDefinition
                {
                    Id = "u",
                    Name = "U",
                    Price = 1,
                    Unlock = UnlockCondition.UpgradeOwned("missing"),
                })
                .AddLore(Entry("s_1", "s", 1))
                .Build());

        Check.True(ex.Errors.Count > 0);
    }

    [Test]
    public static void Validator_RejectsEmptyStoryline()
    {
        GameContentValidationException ex = Check.Throws<GameContentValidationException>(() =>
            new GameContentBuilder("X")
                .Add(new StorylineDefinition { Id = "s", Name = "线", TotalEntries = 0 })
                .Build());

        Check.Contains(string.Join("\n", ex.Errors), "没有任何叙事条目");
    }

    [Test]
    public static void RevealingLore_RecomputesProduction()
    {
        // 第 9 命的规则以 ScalingSource.LoreCount 成长，所以"读到新条目"必须让引擎重算产量。
        // 这条曾经是漏的：LoreSystem 放出叙事却不调用 MarkDirty，加成要等到下一次
        // 无关的购买才生效（表现为"读完一条剧情产量没动，买了个建筑才跳一下"）。
        GameEngine engine = TestGame.CreateNineLives(out _);
        engine.State.Era = 9;
        engine.MarkDirty();
        double before = engine.Modifiers.Multiplier(ModifierTarget.GlobalCps);
        Check.Close(1.0, before); // 还没读到任何记忆，第 9 命的规则不该有加成

        engine.State.TotalClicks = 1; // 满足 nine_01 的 ClicksAtLeast(1)
        IReadOnlyList<LoreEntry> revealed = engine.CheckLore();
        Check.AtLeast(revealed.Count, 1, "nine_01 的条件应当被满足。");

        double after = engine.Modifiers.Multiplier(ModifierTarget.GlobalCps);
        Check.True(
            after > before,
            $"读到 {revealed.Count} 条新剧情后，第 9 命的规则应当立刻生效（{before} → {after}）。");
    }

    // ---------------------------------------------------------------- 辅助

    private static LoreEntry Entry(string id, string storylineId, int order) => new()
    {
        Id = id,
        Title = id,
        Body = "正文",
        StorylineId = storylineId,
        Order = order,
        Reveal = UnlockCondition.ClicksAtLeast(1),
    };

    /// <summary>
    /// 取条件里最小的"量级"阈值，用于"开篇是不是太久读不到"的判断。<para>
    /// 刻意跳过 <see cref="NumericMetric.Era"/> 与 <see cref="NumericMetric.PrestigeLevel"/>：
    /// 它们是<b>进度序号</b>而不是量级阈值，取值都在 200 以下，混进来会把
    /// <c>All(EraAtLeast(2), EarnedThisRunAtLeast(6.5e6))</c> 误判成"早期可读"——
    /// 于是这条断言对任何带纪元门槛的开篇都失效（重排后九命四线全是这样，它就完全没判别力了）。
    /// </para>
    /// <para>没有量级叶子时返回 <see cref="Unjudgeable"/>，语义是"无法判早"，按不早期处理。</para>
    /// <para>
    /// <c>internal</c> 而不是 <c>private</c>：各内容包的专项用例（例如末世包的
    /// <c>EveryStorylineOpensEarlyEnough</c>）要用同一把尺子量自己那包的开篇，
    /// 各写一份迟早会长出两个不一致的判定。
    /// </para>
    /// </summary>
    private const double Unjudgeable = 1e12;

    internal static double FirstThreshold(UnlockCondition condition)
    {
        double min = double.PositiveInfinity;
        foreach (NumericCondition leaf in condition.NumericLeaves())
        {
            if (leaf.Metric is NumericMetric.Era or NumericMetric.PrestigeLevel) continue;
            if (leaf.Target < min) min = leaf.Target;
        }
        return double.IsPositiveInfinity(min) ? Unjudgeable : min;
    }

    /// <summary>把一个条件的叶子多重集规范成字符串键（见 <c>RevealConditions_AreUniqueWithinEachPack</c>）。</summary>
    private static string RevealKey(UnlockCondition condition)
    {
        List<string> parts = [];
        foreach (UnlockCondition leaf in condition.Flatten())
        {
            parts.Add(leaf switch
            {
                // "R" 是往返格式，保住 double 的精度，否则 1e10 与 1.05e10 会撞成同一个键。
                NumericCondition n => $"N:{n.Metric}:{n.Target:R}:{n.Id}",
                OwnedCondition o => $"O:{o.Kind}:{o.Id}",
                _ => leaf.Describe(),
            });
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join("|", parts);
    }
}
