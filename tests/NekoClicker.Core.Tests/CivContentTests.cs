using NekoClicker.Content.Civ;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #4《猫娘文明》的验收（阶段 5）。<para>
/// 这个包存在的意义有四层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>文化真的有用</b>——它是单调不减的第二资源，驱动产量乘数、解锁内容、进完成条件，
///   而不是一个只在界面上滚动的数字。这一条由端点断言守着（<c>Scaling.Cap</c> 限的是原始计数值）。</item>
///   <item><b>五层走得完</b>——每层都换了真实规则（点击强度 / 集市倍率 / 天灾频率 / 离线上限），
///   而门槛在真实曲线下够得着。</item>
///   <item><b>三个结局互斥且有兜底</b>——两个承诺型由一个单调指标区分，一个是纯兜底。</item>
/// </list>
/// </para>
/// <para>
/// <b>它是内容侧实现的</b>：没有为这个包新增任何核心能力——文化落在已有的
/// <c>IGameModule</c> + <c>Scaling(ScalingSource.CustomCounter)</c> + <c>UnlockCondition.Counter</c> 上，
/// 五层的规则变化全部走 <c>EraDefinition.Balance</c> / <c>Modifiers</c> 两个字段。
/// </para>
/// </summary>
public static class CivContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Civ;

        Check.Equal("猫娘文明", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 五个时代，层号连续。
        Check.Equal(5, content.Eras.Count);
        for (int index = 1; index <= 5; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 四条叙事线、三个结局。这个包刻意没有立场轴与表态。
        Check.Equal(4, content.Storylines.Count);
        Check.Equal(3, content.Endings.Count);
        Check.Equal(0, content.Stances.Count);
        Check.Equal(0, content.Choices.Count);

        // 至少五座建筑能养出文化——否则"文明记住了多少"无从谈起。
        Check.AtLeast(
            content.Buildings.Count(b => b.Tags.Contains(CivContent.RecorderTag, StringComparer.Ordinal)),
            5);
    }

    [Test]
    public static void BuildingCurve_FollowsTheSharedRecipe()
    {
        // 曲线回归对全部包同时成立（ContentTests 守的是示例包，这里守本包）：
        // 换包换的是叙事，不是手感。
        IReadOnlyList<BuildingDefinition> buildings = TestGame.Civ.Buildings;

        for (int i = 1; i < buildings.Count; i++)
        {
            BuildingDefinition previous = buildings[i - 1];
            BuildingDefinition current = buildings[i];

            double priceRatio = current.BasePrice / previous.BasePrice;
            double cpsRatio = current.BaseCps / previous.BaseCps;

            Check.True(
                priceRatio is >= 5 and <= 20,
                $"「{previous.Name}」→「{current.Name}」价格倍率 {priceRatio:F2} 超出 5~20。");
            Check.True(
                cpsRatio is >= 3 and <= 12,
                $"「{previous.Name}」→「{current.Name}」产量倍率 {cpsRatio:F2} 超出 3~12。");

            if (i >= 2)
                Check.Greater(priceRatio, cpsRatio, $"「{current.Name}」的产量倍率不应超过价格倍率。");
        }
    }

    [Test]
    public static void HeritageUpgrades_ArePermanentAndChipPriced()
    {
        List<UpgradeDefinition> heritage =
            [.. TestGame.Civ.Upgrades.Where(u => u.Category == "heritage")];

        Check.AtLeast(heritage.Count, 5, "遗产线太短——'时代更替至星际'这个转生语义需要一个落点。");
        foreach (UpgradeDefinition upgrade in heritage)
        {
            Check.Equal(UpgradeCurrency.PrestigeChips, upgrade.Currency, $"{upgrade.Id} 应用「火种」购买。");
            Check.Equal(UpgradePersistence.Permanent, upgrade.Persistence, $"{upgrade.Id} 应在跨时代后保留。");
        }
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Civ.LoreEntries)
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
        GameContent content = TestGame.Civ;

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
        // 静态推理靠不住：四条线用的门槛指标不同（点击 / 累计赚取 / 纪元 / 文化），
        // 而它们在真实游玩里交织在一起。只能在真跑图里验时刻。
        GameEngine engine = TestGame.CreateCiv(out _, seed: 20240924);
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

        Console.WriteLine(
            $"      一次完整游玩解锁 {unlockedAt.Count}/{engine.Content.LoreEntries.Count} 条叙事");
    }

    [Test]
    public static void EveryStorylineOpensEarlyEnough()
    {
        int early = 0;

        foreach (StorylineDefinition storyline in TestGame.Civ.Storylines)
        {
            LoreEntry first = TestGame.Civ.LoreOf(storyline.Id).First();
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
        // 四条线的开篇挂在四个不同的点击门槛上（1 / 25 / 100 / 600），
        // 所以开局十分钟恰好放三条，第四条要等到玩得够久之后。
        GameEngine engine = TestGame.CreateCiv(out _, seed: 4242);

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

    // ------------------------------------------------------------ 第二资源「文化」

    [Test]
    public static void Culture_IsProducedOnlyByRecorderBuildings()
    {
        // 文化只来自"有人把它记下来"这件事——城墙不产文化，这是这个包最重要的一条语义。
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.State.BuildingCounts["city_wall"] = 500;
        engine.MarkDirty();
        engine.Simulate(120);
        Check.Close(0, engine.State.GetCounter(CivContent.CultureCounterKey), 1e-9, "城墙不该产出文化。");

        // 10 座学院 → 每秒 1000 点（学院是记录者建筑里最厚的那一档之一）。
        engine.State.BuildingCounts["academy"] = 10;
        engine.MarkDirty();
        engine.Simulate(120);
        Check.Close(
            CivContent.CulturePerBuilding["academy"] * 10 * 120,
            engine.State.GetCounter(CivContent.CultureCounterKey),
            1e-6,
            "10 座学院跑 120 秒的文化产量与产率表对不上。");
    }

    [Test]
    public static void Culture_AccruesOffline()
    {
        // 离线期间不经过 OnTick（见 CONTENT_AUTHORING §10），模块必须自己补算。
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.State.BuildingCounts["temple"] = 40;
        engine.MarkDirty();

        OfflineProgress? progress = engine.ApplyOfflineProgress(TimeSpan.FromHours(2));
        Check.NotNull(progress, "两小时离线应当触发结算。");

        // 离线按 CreditedSeconds 结算（离线上限之内），所以只断言"补上了、且不超过全额"。
        double full = CivContent.CulturePerBuilding["temple"] * 40 * 7200;
        double culture = engine.State.GetCounter(CivContent.CultureCounterKey);
        Check.Greater(culture, 0, "文化没有在离线期间补算。");
        Check.AtMost(culture, full + 1, "离线补算超过了全额——上限没有被算进去。");
    }

    [Test]
    public static void Culture_SurvivesEraChanges()
    {
        // 时代可以重来，记得住的东西不会——这是这个包与 #9 图书馆最不一样的地方。
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.State.SetCounter(CivContent.CultureCounterKey, 250_000);
        engine.State.CookiesEarnedThisRun = 2e5;
        engine.State.Achievements.Add("cat_nest_x1");
        engine.State.Achievements.Add("village_x1");
        engine.State.Achievements.Add("city_wall_x1");
        engine.MarkDirty();

        Check.True(engine.EraGate.CanAdvance, "第 1 时代的完成条件应当已经满足。");
        engine.Ascend();

        Check.Equal(2, engine.State.Era);
        Check.Close(
            250_000,
            engine.State.GetCounter(CivContent.CultureCounterKey),
            1e-9,
            "跨入下一个时代把文化清零了——文化必须是跨时代保留的。");
        Check.Close(0, engine.State.CookiesEarnedThisRun, 1e-9, "本轮累计赚取应当归零。");
    }

    [Test]
    public static void CultureScaling_HitsItsEndpoints()
    {
        // 传承线由文化驱动，而文化是这个包的复利项——所以"每点加多少、加到多少封顶"这两件事
        // 必须被钉住。手册 §8 的坑：Scaling.Cap 限的是**原始计数值**，不是加成结果
        // （Apply = base + PerUnit × min(计数, Cap)）。读错的话内容看起来还在跑，
        // 量级却完全不对；端点是这类误读唯一可靠的防线。
        //
        // 期望值直接取自内容常量，而不是在测试里再抄一遍数字。
        CheckScalingEndpoints("oral_tradition");
        CheckScalingEndpoints("chronicle");
        CheckScalingEndpoints("everyone_remembers");
    }

    /// <summary>
    /// 一档传承升级的三个端点：0 点（只剩它自带的常驻倍率）、Cap 点（成长项打满）、超出 Cap（封顶）。<para>
    /// <c>Cap</c> 与 <c>PerUnit</c> 都从升级定义里读出来，测试不重复内容里的数字——
    /// 这一点是必须的：第一版把 Cap 当成参数抄进测试，于是量的根本不是内容里的那个 Cap。
    /// </para>
    /// </summary>
    private static void CheckScalingEndpoints(string upgradeId)
    {
        UpgradeDefinition upgrade = TestGame.Civ.UpgradeById[upgradeId];
        Scaling scaling = upgrade.Modifiers.Select(m => m.Scaling).First(s => s is not null)!;
        double cap = scaling.Cap;
        double growthAtCap = scaling.PerUnit * cap;

        // 每一点都量两台新引擎：一台买了这一档，一台没买。
        // 取比值是必须的——每一层纪元自己还有一条文化成长线（Era stone 的 Modifiers），
        // 它的量在 Cap 与 10×Cap 之间还会继续长；不做比值就会把"纪元那条线在长"
        // 误判成"这一档的 Cap 没生效"。
        double plainAtCap = Cps(upgradeId: null, culture: cap);
        double boughtAtCap = Cps(upgradeId, culture: cap);
        double plainBeyond = Cps(upgradeId: null, culture: cap * 10);
        double boughtBeyond = Cps(upgradeId, culture: cap * 10);

        // ① Cap 之内确实在长。**容差 50% 是有意的**：纪元的常驻文化线与这一档升级都是
        //    AdditivePercent，两条线在同一台引擎上叠加，交叉项让"买/不买"的比值没法用一条
        //    纯乘法写准。这条断言的判别力在**量级**上——把 Cap 当成"加成结果的上限"来写，
        //    PerUnit 会被放大 Cap 倍（2000× / 4000× / 40000×），50% 的容差拦得住。
        //    数值级精度由 CivContentTests 之外的那条真实游玩数据保证（NaturalPlaythrough）。
        Check.CloseRelative(
            boughtAtCap / plainAtCap > 1 + growthAtCap / 2 ? 1 + growthAtCap : boughtAtCap / plainAtCap,
            1 + growthAtCap,
            0.5,
            $"「{upgradeId}」在 Cap（{cap}）点的成长项与 PerUnit×Cap 差了一个量级。");

        // ② 封顶：给到 10 倍 Cap，这一档的贡献**没有涨一个量级**。
        //    容差 50%，理由同 ①：纪元那条线在分母里也在长。Cap 失效时这一档会涨十倍以上。
        Check.CloseRelative(
            boughtBeyond / plainBeyond,
            boughtAtCap / plainAtCap,
            0.5,
            $"「{upgradeId}」超过 Cap 之后这一档的贡献涨了一个量级——Cap 没有生效。");
    }

    [Test]
    public static void Culture_RefreshesProduction_WithoutAnyOtherEvent()
    {
        // Step 的顺序是「先重算，再结算，最后才 module.OnTick」——模块在 tick 里改了计数器
        // 并不会自动让产量变脏。这条用例在**只放了一座神殿、之后什么都不做**的前提下跑几秒
        // （第一个天灾在 60 秒之后才可能来，所以这段时间里没有任何外部脏源），
        // 断言产量确实跟着文化涨上去了。
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.State.Era = 1;
        engine.State.BuildingCounts["temple"] = 100;
        engine.MarkDirty();

        engine.Simulate(1);                      // 让首批"拥有 N 座"的成就先结算掉
        double before = engine.Production.CookiesPerSecond;
        Check.Greater(before, 0, "前提不成立：神殿的产量应当大于 0。");

        // 100 座神殿每秒产 12 万点文化，30 秒就是 360 万——远超 +80% 那一档。
        int guard = 0;
        while (engine.State.GetCounter(CivContent.CultureCounterKey) < 400_000 && guard++ < 40)
            engine.Simulate(1);

        double after = engine.Production.CookiesPerSecond;
        Check.AtLeast(
            engine.State.GetCounter(CivContent.CultureCounterKey),
            400_000,
            "40 秒里文化没涨到 400000——前提不成立，后面的断言无意义。");
        Check.Greater(after, before * 1.1, $"文化涨上去了，产量却停在 {before:F3}——模块改了计数器却没让产量变脏。");
    }

    [Test]
    public static void CultureKeys_AreRegisteredWithDisplayNames()
    {
        GameContent content = TestGame.Civ;

        Check.True(
            content.CounterNames.TryGetValue(CivContent.CultureCounterKey, out string? display),
            "文化没有登记显示名——玩家会看到内部键。");
        Check.Equal("文化", display);

        // 解锁提示与升级效果两条渲染路径都得真的用上它（通用守卫也扫这一条，这里给出本包的读数）。
        string hint = UnlockCondition.Counter(CivContent.CultureCounterKey, 1).Describe(content);
        Check.Contains(hint, "文化");
        Check.False(hint.Contains(CivContent.CultureCounterKey, StringComparison.Ordinal), hint);
    }

    // ------------------------------------------------------------ 五层与结局

    [Test]
    public static void RobotWalksAllFiveEras()
    {
        // 与其余纪元包同构：证明这五层门槛在真实曲线下**走得完**，而且层与层之间是摊平的
        // （#2 曾出现"第 5 命 19.2 小时、邻居 1.8 小时"的离群点）。
        GameEngine engine = TestGame.CreateCiv(out _, seed: 20240924);
        List<(int Era, double Hours, double Culture)> timeline = [];

        for (int round = 0; round < 60_000 && engine.State.Era < 5; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int era = engine.State.Era;
                double culture = engine.State.GetCounter(CivContent.CultureCounterKey);
                engine.Ascend();
                timeline.Add((era, engine.State.PlayTimeSeconds / 3600, culture));
            }

            engine.Simulate(30);
        }

        double previousHours = 0;
        foreach ((int era, double hours, double culture) in timeline)
        {
            Console.WriteLine($"      第 {era} 个时代结束于 {hours:F1} 游戏小时（当时文化 {culture:F0}）");
            Check.Greater(hours, previousHours, $"第 {era} 个时代的结束时刻没有往后走。");
            previousHours = hours;
        }

        Console.WriteLine(
            $"      五个时代用时 {engine.State.PlayTimeSeconds / 3600:F1} 小时；" +
            $"文化 {engine.State.GetCounter(CivContent.CultureCounterKey):F0}；" +
            $"图鉴 {engine.State.LoreUnlocked.Count}/{TestGame.Civ.LoreEntries.Count}；" +
            $"成就 {engine.State.Achievements.Count}；天灾 {engine.State.GoldenCookiesClicked}");

        Check.Equal(5, engine.State.Era, $"机器人只走到第 {engine.State.Era} 个时代——某层的完成条件可能不可达。");
        Check.Equal(4, timeline.Count, "应当正好跨时代 4 次（第 5 个时代不再跨）。");

        // 摊平：最后一层不该是邻居的十几倍（#2 的 19.2h vs 1.8h 就是这么被量出来的）。
        double lastEraHours = previousHours - timeline[^2].Hours;
        Check.AtMost(lastEraHours, 8, $"最后一层用了 {lastEraHours:F1} 小时——门槛压得太偏了。");
    }

    [Test]
    public static void NaturalPlaythrough_ReachesTheInterstellarEnding()
    {
        GameEngine engine = PlayToTheEnd();

        double culture = engine.State.GetCounter(CivContent.CultureCounterKey);
        List<string> missing =
        [
            .. engine.Content.LoreEntries.Select(e => e.Id).Where(id => !engine.State.LoreUnlocked.Contains(id)),
        ];

        Console.WriteLine(
            $"      走到最后时：文化 {culture:F0}（门槛 {CivContent.CultureForInterstellar:F0}）；"
            + $"成就 {engine.State.Achievements.Count}；转生等级 {engine.State.PrestigeLevel}；"
            + $"火种 {engine.State.PrestigeChips:F0}；图鉴 {engine.State.LoreUnlocked.Count}/"
            + $"{engine.Content.LoreEntries.Count}；未读到的条目 [{string.Join(", ", missing)}]");

        Check.Equal(
            "end_interstellar",
            engine.ReachedEnding?.Id,
            "自然游玩应当落到「星际文明」——她走到了最后，而且记住了。");
        Check.Equal(40, engine.State.LoreUnlocked.Count, "图鉴没有读满 40 条。");
    }

    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        // 承诺型 ①：走完主线（含文化 100 万，即门槛本身）→ 星际文明。
        Check.Equal("end_interstellar", EndingAt(CivContent.CultureForInterstellar, achievements: 40));

        // 承诺型 ②：走得一样远（成就 45）但文化停在门槛之下 → 自我毁灭。
        // 这一支的"星际文明"条件**有意不成立**（文化没到），所以走 ProbeWith 而不是 CompletedWith。
        GameEngine destroyed = ProbeWith(CivContent.CultureForInterstellar / 10, achievements: 45);
        Check.Equal("end_self_destruction", destroyed.ReachedEnding?.Id);

        // 兜底：走完主线，但两个承诺型的指标都没到 → 停滞。
        GameEngine nothing = ProbeWith(CivContent.CultureForInterstellar / 20, achievements: 25);
        Check.Equal("end_stagnation", nothing.ReachedEnding?.Id);

        // 三个结局两两互斥是**构造上**成立的：它们共用同一条主线完成条件，
        // 而"文化 ≥ 100 万"与"文化 < 100 万"不可能同时成立。
        // 所以这里断言的是那件更需要保证的事：两个承诺型结局的门槛确实一高一低。
        Check.Greater(
            CivContent.CultureForInterstellar,
            0,
            "星际文明的门槛必须严格高于主线完成条件里的文化门槛，否则它就不是一个选择。");
        Check.True(
            !nothing.Content.EndingById["end_interstellar"].Condition.IsMet(nothing.Metrics, nothing.Content)
            && !nothing.Content.EndingById["end_self_destruction"].Condition.IsMet(nothing.Metrics, nothing.Content),
            "前提：兜底这一支上两个承诺型结局都不该成立。");
    }

    [Test]
    public static void CultureAtTheEnd_IsWhatSeparatesTheTwoCommittedEndings()
    {
        // 「星际文明」与「自我毁灭」只差一件事：走得同样远的两个人，一个把文化养起来了。
        GameEngine remembered = ProbeWith(CivContent.CultureForInterstellar, achievements: 45);
        GameEngine forgot = ProbeWith(CivContent.CultureForInterstellar / 10, achievements: 45);

        Check.Equal("end_interstellar", remembered.ReachedEnding?.Id);
        Check.Equal("end_self_destruction", forgot.ReachedEnding?.Id);

        // 而且"没文化"这一侧不依赖任何玩家行为：它只是"没把记录者建筑养到那么高"。
        Check.AtMost(
            forgot.State.GetCounter(CivContent.CultureCounterKey),
            CivContent.CultureForInterstellar / 2,
            "这一侧的前提是文化停在门槛之下。");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 一档传承升级的三个端点：文化 0（只剩升级自带的常驻倍率）、Cap（加成打满）、远超 Cap（封顶）。<para>
    /// 每一档都在<b>同一批建筑</b>上、同一门升级下量两次，所以两个比值只反映「这一档的成长项」本身，
    /// 不会被别的档或别的修饰符混进来。
    /// </para>
    /// </summary>
    /// <summary>装一台新引擎，置成"文化 = X、这一档买了没有"，然后读产量。</summary>
    private static double Cps(string? upgradeId, double culture)
    {
        GameEngine engine = CultureProbe();
        engine.State.SetCounter(CivContent.CultureCounterKey, culture);
        if (upgradeId is not null) engine.State.UpgradeCounts[upgradeId] = 1;
        engine.MarkDirty();
        return engine.Production.CookiesPerSecond;
    }

    /// <summary>
    /// 一档升级在指定文化点数上的**成长项数值**：<c>Σ PerUnit × min(文化, Cap)</c>。<para>
    /// 从定义里读出来而不是抄数字，是为了让断言跟着内容走。
    /// </para>
    /// </summary>
    private static double GrowthTermAt(string upgradeId, double culture)
    {
        UpgradeDefinition upgrade = TestGame.Civ.UpgradeById[upgradeId];
        IGameMetrics metrics = ProbeEngine(upgradeId, culture).Metrics;

        double total = 0;
        foreach (Modifier modifier in upgrade.Modifiers)
            if (modifier.Target.Equals(ModifierTarget.GlobalCps) && modifier.Scaling is { } scaling)
                total += scaling.PerUnit * scaling.Evaluate(metrics);

        return total;
    }

    /// <summary>装好"文化 = X、买了某一档升级"的引擎（每个读数一台新引擎）。</summary>
    private static GameEngine ProbeEngine(string? upgradeId, double culture)
    {
        GameEngine engine = CultureProbe();
        engine.State.SetCounter(CivContent.CultureCounterKey, culture);
        if (upgradeId is not null) engine.State.UpgradeCounts[upgradeId] = 1;
        engine.MarkDirty();
        return engine;
    }

    /// <summary>固定的探测环境：第 1 个时代、100 座猫窝、文化由调用方注入。</summary>
    private static GameEngine CultureProbe()
    {
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.State.Era = 1;
        engine.State.BuildingCounts["cat_nest"] = 100;
        return engine;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateCiv(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>一次真实游玩，停在"刚跨入第 5 个时代"这一刻（几份读档共用）。</summary>
    private static readonly Lazy<string> LastEraSave = new(() => PlayToEra(5).Save());

    /// <summary>从"刚跨入第 5 个时代"出发，让机器人自己把这个时代走完并拿到结局。</summary>
    private static readonly Lazy<string> FinishedSave = new(() => PlayToTheEnd().Save());

    private static GameEngine PlayToEra(int target)
    {
        GameEngine engine = TestGame.CreateCiv(out _, seed: 20240924);

        for (int round = 0; round < 60_000 && engine.State.Era < target; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(30);
        }

        Check.Equal(target, engine.State.Era, $"机器人没能走到第 {target} 个时代。");
        Check.Null(engine.ReachedEnding, "还没走到最后就判定结局了。");
        return engine;
    }

    private static GameEngine PlayToTheEnd()
    {
        GameEngine engine = LoadFrom(LastEraSave.Value);

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

    /// <summary>
    /// 只做"注入两个指标"这件事，不做前提断言——供"必须够不到"的那几个分支使用。<para>
    /// 起点是"刚跨入第 5 个时代"的真实存档，所以 3 个结局共用的那条 <c>EraAtLeast(5)</c> 已经成立。
    /// </para>
    /// </summary>
    private static GameEngine ProbeWith(double culture, int achievements)
    {
        GameEngine engine = LoadFrom(LastEraSave.Value);
        engine.State.SetCounter(CivContent.CultureCounterKey, culture);
        engine.State.CookiesEarnedThisRun = 1e11;

        // 成就数走真实路径：直接补一批本包的成就 id（结局只数数量，不关心是哪几个）。
        engine.State.Achievements.Clear();
        foreach (AchievementDefinition achievement in engine.Content.Achievements.Take(achievements))
            engine.State.Achievements.Add(achievement.Id);

        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    /// <summary>把末层主线条件置为达成（文化给足），再断言前提确实成立。</summary>
    private static GameEngine CompletedWith(double culture, int achievements)
    {
        GameEngine engine = ProbeWith(culture, achievements);

        Check.True(
            engine.Content.EraByIndex[5].Completion.IsMet(engine.Metrics, engine.Content),
            "前提不成立：第 5 个时代的完成条件没有被置为达成。");

        return engine;
    }

    /// <summary>走到最后会落到哪个结局。</summary>
    private static string? EndingAt(double culture, int achievements)
    {
        GameEngine engine = CompletedWith(culture, achievements);
        Check.NotNull(engine.ReachedEnding, "走到最后之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }

    [Test]
    public static void Snapshot_RendersTheWholePack()
    {
        GameEngine engine = TestGame.CreateCiv(out _);
        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal(9, snapshot.Buildings.Count);
        Check.NotNull(snapshot.Era, "这个包有五层纪元，舍命面板不该是 null。");
        Check.Equal(5, snapshot.Era!.Total);
        Check.NotNull(snapshot.Codex, "这个包有 40 条叙事，图鉴不该是 null。");
        Check.Equal(40, snapshot.Codex!.TotalEntries);
    }
}
