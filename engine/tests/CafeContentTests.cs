using NekoClicker.Content.Cafe;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #1《猫娘咖啡馆》的完整性与专属行为。<para>
/// 覆盖 PACK_01 §13 的验收清单：规模基线、曲线区间、常客记忆的计价硬约束、
/// 随机事件的正反馈占比，以及幸福感模块的累加 / 离线补算 / 跨转生保留。
/// 长跑（6 小时贪心模拟）在 <see cref="SimulationTests"/> 中，与示例包共用同一套机器人。
/// </para>
/// </summary>
public static class CafeContentTests
{
    /// <summary>规模基线：改动内容时这里会提醒你"是不是悄悄少了一条"。</summary>
    [Test]
    public static void CafeContent_BuildsWithExpectedShape()
    {
        GameContent content = TestGame.CafeContent;

        Check.Equal(10, content.Buildings.Count);
        Check.Equal(48, content.Upgrades.Count);
        Check.Equal(45, content.Achievements.Count);
        Check.Equal(5, content.Buffs.Count);
        Check.Equal(8, content.GoldenCookieOutcomes.Count);
        Check.Equal(1, content.Modules.Count, "咖啡馆包应挂载幸福感模块。");

        Check.Equal("猫娘咖啡馆", content.Title);
        Check.Equal("小鱼干", content.CurrencyName);
        Check.Equal("常客的信", content.PrestigeCurrencyName);
        Check.Equal("做咖啡", content.ClickActionName);
    }

    /// <summary>id 唯一且索引自洽。</summary>
    [Test]
    public static void CafeContent_IdsAreGloballyConsistent()
    {
        GameContent content = TestGame.CafeContent;

        foreach (BuildingDefinition building in content.Buildings)
            Check.Equal(building.Id, content.BuildingById[building.Id].Id);
        foreach (UpgradeDefinition upgrade in content.Upgrades)
            Check.Equal(upgrade.Id, content.UpgradeById[upgrade.Id].Id);
        foreach (AchievementDefinition achievement in content.Achievements)
            Check.Equal(achievement.Id, content.AchievementById[achievement.Id].Id);
        foreach (BuffDefinition buff in content.Buffs)
            Check.Equal(buff.Id, content.BuffById[buff.Id].Id);

        Check.Equal(content.Buildings.Count, content.Buildings.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(content.Upgrades.Count, content.Upgrades.Select(u => u.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(content.Achievements.Count, content.Achievements.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(content.Buffs.Count, content.Buffs.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(content.GoldenCookieOutcomes.Count, content.GoldenCookieOutcomes.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>条件阈值都必须为正，且没有恒为假的成就（不可达内容是纯负担）。</summary>
    [Test]
    public static void CafeContent_EveryUnlockIsReachable()
    {
        GameContent content = TestGame.CafeContent;

        foreach (UpgradeDefinition upgrade in content.Upgrades)
        {
            foreach (NumericCondition condition in upgrade.Unlock.NumericLeaves())
                Check.Greater(condition.Target, 0, $"升级 {upgrade.Id} 的条件阈值应为正。");
        }

        foreach (AchievementDefinition achievement in content.Achievements)
        {
            Check.False(
                achievement.Unlock is ConstantCondition { Value: false },
                $"成就 {achievement.Id} 的条件恒为假。");
        }
    }

    /// <summary>与示例包共用同一条曲线区间回归（PACK_01 §4 声称数值可直接沿用）。</summary>
    [Test]
    public static void CafeContent_BuildingCurveIsSane()
    {
        GameContent content = TestGame.CafeContent;

        for (int i = 1; i < content.Buildings.Count; i++)
        {
            BuildingDefinition previous = content.Buildings[i - 1];
            BuildingDefinition current = content.Buildings[i];

            Check.Greater(current.BasePrice, previous.BasePrice, $"「{current.Name}」的价格应高于上一层。");
            Check.Greater(current.BaseCps, previous.BaseCps, $"「{current.Name}」的产量应高于上一层。");

            double priceRatio = current.BasePrice / previous.BasePrice;
            double cpsRatio = current.BaseCps / previous.BaseCps;
            Check.True(priceRatio is >= 5 and <= 20, $"「{previous.Name}」→「{current.Name}」价格倍率 {priceRatio:F2} 超出 5~20。");
            Check.True(cpsRatio is >= 3 and <= 12, $"「{previous.Name}」→「{current.Name}」产量倍率 {cpsRatio:F2} 超出 3~12。");

            if (i >= 2)
                Check.Greater(priceRatio, cpsRatio, $"「{current.Name}」的产量倍率不应超过价格倍率，否则价格曲线会失控。");
        }
    }

    /// <summary>建筑解锁门槛必须递增（否则开局会被一长串灰色条目淹没）。</summary>
    [Test]
    public static void CafeContent_UnlockThresholdsRiseWithTier()
    {
        GameContent content = TestGame.CafeContent;

        double previousThreshold = 0;
        foreach (BuildingDefinition building in content.Buildings)
        {
            NumericCondition? gate = building.Unlock.NumericLeaves()
                .FirstOrDefault(c => c.Metric == NumericMetric.CookiesEarnedThisRun);

            if (gate is null) continue; // 第一座建筑无条件

            Check.AtLeast(gate.Target, previousThreshold, $"「{building.Name}」的解锁门槛应递增。");
            previousThreshold = gate.Target;
        }
    }

    /// <summary>「常客的记忆」必须用「常客的信」购买并跨店休保留——这是转生语义的硬约束。</summary>
    [Test]
    public static void CafeContent_MemoryUpgradesArePermanentAndChipPriced()
    {
        GameContent content = TestGame.CafeContent;
        List<UpgradeDefinition> memories = [.. content.Upgrades.Where(u => u.Category == "heavenly")];

        Check.Equal(5, memories.Count, "常客的记忆应为 5 条。");
        foreach (UpgradeDefinition upgrade in memories)
        {
            Check.Equal(UpgradeCurrency.PrestigeChips, upgrade.Currency, $"{upgrade.Id} 应用「常客的信」购买。");
            Check.Equal(UpgradePersistence.Permanent, upgrade.Persistence, $"{upgrade.Id} 应在店休后保留。");
        }
    }

    /// <summary>治愈系的四处平衡差异（PACK_01 §3）。</summary>
    [Test]
    public static void CafeContent_BalanceMatchesTheHealingTone()
    {
        GameBalance balance = TestGame.CafeContent.Balance;

        Check.Close(0.015, balance.ClickCpsRatio, 1e-12, "点咖啡应更有存在感。");
        Check.Close(60 * 4, balance.GoldenCookieMinDelay);
        Check.Close(60 * 12, balance.GoldenCookieMaxDelay);
        Check.Close(15, balance.GoldenCookieLifetime, 1e-12, "客人应停留更久，降低手忙脚乱。");
        Check.Close(0.6, balance.DefaultSellRefundRate, 1e-12, "治愈系不该惩罚试错。");
    }

    /// <summary>非负面权重必须占绝对多数（治愈系包的底线）。</summary>
    [Test]
    public static void CafeContent_EventWeightsArePositiveFeedbackDominant()
    {
        GameContent content = TestGame.CafeContent;

        double total = content.GoldenCookieOutcomes.Sum(o => o.Weight);
        double positive = content.GoldenCookieOutcomes.Where(o => o.StealBankFraction <= 0).Sum(o => o.Weight);

        Check.Greater(total, 0);
        Check.AtLeast(positive / total, 0.85, "正反馈（不扣存量）权重占比不应低于 85%。");
    }

    /// <summary>每个随机事件引用的增益都必须真实存在（构建期已校验，这里显式断言一次）。</summary>
    [Test]
    public static void CafeContent_OutcomeBuffReferencesExist()
    {
        GameContent content = TestGame.CafeContent;

        foreach (GoldenCookieOutcome outcome in content.GoldenCookieOutcomes)
        {
            if (outcome.BuffId is { } buffId)
                Check.True(content.BuffById.ContainsKey(buffId), $"{outcome.Id} 引用了不存在的增益 {buffId}。");
            if (outcome.SecondaryBuffId is { } secondaryId)
                Check.True(content.BuffById.ContainsKey(secondaryId), $"{outcome.Id} 引用了不存在的连锁增益 {secondaryId}。");
        }
    }

    /// <summary>幸福感会随店铺规模自然增长——这是"涨满了会怎样"这个牵引感的源头。</summary>
    [Test]
    public static void Cafe_HappinessModuleAccumulates()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("coffee_machine", 50).Success);

        engine.Simulate(600); // 50 座建筑 → 1 点/秒 → 约 600 点

        double happiness = engine.State.GetCounter(CafeContent.HappinessCounterKey);
        Check.Greater(happiness, 0, "幸福感应随 tick 增长。");
        Check.AtLeast(happiness, 500, "50 座建筑跑 10 分钟应至少积累 500 点幸福感。");
    }

    /// <summary>离线期间不经过 OnTick，幸福感必须由 OnOffline 补算。</summary>
    [Test]
    public static void Cafe_HappinessModuleAccruesOffline()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("coffee_machine", 50).Success);

        OfflineProgress? offline = engine.ApplyOfflineProgress(TimeSpan.FromHours(1));

        Check.True(offline is not null, "1 小时离线应触发结算。");
        double happiness = engine.State.GetCounter(CafeContent.HappinessCounterKey);
        Check.AtLeast(happiness, 3_000, "50 座建筑离线 1 小时应补算约 3600 点幸福感。");
    }

    /// <summary>幸福感存在 Counters 里，而转生不清空 Counters →「常客的记忆」天然保留。</summary>
    [Test]
    public static void Cafe_HappinessSurvivesAscension()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.State.Cookies = 1e12;
        engine.State.CookiesEarnedThisRun = 1e12;
        engine.State.CookiesEarnedAllTime = 1e12;
        engine.State.AddCounter(CafeContent.HappinessCounterKey, 4321);
        engine.MarkDirty();

        Check.True(engine.Ascend().Success, "历史累计 1 兆应可以店休。");

        Check.Close(4321, engine.State.GetCounter(CafeContent.HappinessCounterKey), 1e-9, "幸福感不应被店休清零。");
        Check.Close(0, engine.State.TotalBuildings(), 1e-9, "店休应清空本轮建筑。");
    }

    /// <summary>「常客名单」把幸福感接进产量——幸福感必须能作为带进度条的解锁条件。</summary>
    [Test]
    public static void Cafe_HappinessGatesUpgradesWithProgressBar()
    {
        GameContent content = TestGame.CafeContent;
        UpgradeDefinition regulars = content.UpgradeById["regulars_list"];

        NumericCondition? happinessGate = regulars.Unlock.NumericLeaves()
            .FirstOrDefault(c => c.Metric == NumericMetric.Counter && c.Id == CafeContent.HappinessCounterKey);

        Check.NotNull(happinessGate, "「常客名单」应包含幸福感解锁条件。");
        if (happinessGate is null) return; // Check 不会让编译器知道非空，这一句是给它看的

        GameEngine engine = TestGame.CreateCafe(out _);
        Check.False(happinessGate.IsMet(engine.Metrics, content));
        Check.True(
            happinessGate.TryGetProgress(engine.Metrics, out double current, out double target),
            "幸福感条件必须给出进度（灰按钮/进度条依赖它）。");
        Check.Close(0, current);
        Check.Close(500, target);

        engine.State.AddCounter(CafeContent.HappinessCounterKey, 500);
        engine.MarkDirty();
        Check.True(happinessGate.IsMet(engine.Metrics, content));
    }

    /// <summary>幸福感要能驱动修饰符（ScalingSource.CustomCounter），否则第二资源只是装饰。</summary>
    [Test]
    public static void Cafe_HappinessCanDriveModifiers()
    {
        GameEngine engine = TestGame.CreateCafe(out _);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        Check.True(engine.BuyBuilding("coffee_machine", 10).Success);

        double cpsBefore = engine.CookiesPerSecond;
        engine.State.AddCounter(CafeContent.HappinessCounterKey, 1_000);
        engine.MarkDirty();

        // 幸福感本身不直接改产量（那是内容的事），但引擎必须能读到它——
        // 否则 Scaling(CustomCounter, ...) 这类内容写法会静默失效。
        Check.Close(1_000, engine.Metrics.GetCounter(CafeContent.HappinessCounterKey));
        Check.Greater(cpsBefore, 0, "购买建筑后产量应大于 0。");
    }
}
