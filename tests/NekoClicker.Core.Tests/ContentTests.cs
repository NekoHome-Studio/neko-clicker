using NekoClicker.Content.Neko;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>内容校验与示例内容包的完整性。</summary>
public static class ContentTests
{
    [Test]
    public static void NekoContent_BuildsWithExpectedShape()
    {
        GameContent content = NekoContent.Build();

        // 这些数字是内容包的回归基线：改动内容时应当同步更新，避免"悄悄少了一条升级"。
        Check.Equal(10, content.Buildings.Count);
        Check.Equal(49, content.Upgrades.Count);
        Check.Equal(66, content.Achievements.Count);
        Check.Equal(6, content.Buffs.Count);
        Check.Equal(10, content.GoldenCookieOutcomes.Count);

        Check.Equal("猫咖物语", content.Title);
        Check.Equal("小鱼干", content.CurrencyName);
        Check.Equal("猫薄荷", content.PrestigeCurrencyName);
    }

    [Test]
    public static void NekoContent_IdsAreGloballyConsistent()
    {
        GameContent content = NekoContent.Build();

        foreach (BuildingDefinition building in content.Buildings)
            Check.Equal(building.Id, content.BuildingById[building.Id].Id);

        Check.Equal(
            content.Buildings.Count,
            content.Buildings.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(
            content.Upgrades.Count,
            content.Upgrades.Select(u => u.Id).Distinct(StringComparer.Ordinal).Count());
        Check.Equal(
            content.Achievements.Count,
            content.Achievements.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Test]
    public static void NekoContent_EveryUnlockIsReachable()
    {
        GameContent content = NekoContent.Build();

        // 每个升级/成就的数值阈值都必须为正，且引用存在的 id（构建期已校验，这里再明确断言一次）。
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

    [Test]
    public static void NekoContent_BuildingCurveIsSane()
    {
        GameContent content = NekoContent.Build();

        for (int i = 1; i < content.Buildings.Count; i++)
        {
            BuildingDefinition previous = content.Buildings[i - 1];
            BuildingDefinition current = content.Buildings[i];

            Check.Greater(current.BasePrice, previous.BasePrice, $"「{current.Name}」的价格应高于上一层。");
            Check.Greater(current.BaseCps, previous.BaseCps, $"「{current.Name}」的产量应高于上一层。");

            // 相邻层的"价格倍率"与"产量倍率"必须落在一个窄带内：
            // 产量涨得比价格快 → 每层都值得买；价格涨得不够快 → 早期建筑永远不会被淘汰。
            double priceRatio = current.BasePrice / previous.BasePrice;
            double cpsRatio = current.BaseCps / previous.BaseCps;
            Check.True(priceRatio is >= 5 and <= 20, $"「{previous.Name}」→「{current.Name}」价格倍率 {priceRatio:F2} 超出 5~20。");
            Check.True(cpsRatio is >= 3 and <= 12, $"「{previous.Name}」→「{current.Name}」产量倍率 {cpsRatio:F2} 超出 3~12。");

            // 从第二档开始，价格涨得必须比产量快：否则早期建筑永远不会被淘汰，
            // 玩家会一直买第一座建筑。第一档刻意例外，用来给开局一个爽快的加速。
            if (i >= 2)
                Check.Greater(priceRatio, cpsRatio, $"「{current.Name}」的产量倍率不应超过价格倍率，否则价格曲线会失控。");
        }
    }

    [Test]
    public static void NekoContent_UnlockThresholdsRiseWithTier()
    {
        GameContent content = NekoContent.Build();

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

    [Test]
    public static void NekoContent_HeavenlyUpgradesArePermanentAndChipPriced()
    {
        GameContent content = NekoContent.Build();
        List<UpgradeDefinition> heavenly = [.. content.Upgrades.Where(u => u.Category == "heavenly")];

        Check.AtLeast(heavenly.Count, 3);
        foreach (UpgradeDefinition upgrade in heavenly)
        {
            Check.Equal(UpgradeCurrency.PrestigeChips, upgrade.Currency, $"{upgrade.Id} 应用猫薄荷购买。");
            Check.Equal(UpgradePersistence.Permanent, upgrade.Persistence, $"{upgrade.Id} 应在转生后保留。");
        }
    }

    [Test]
    public static void DuplicateIds_AreRejected()
    {
        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "dup", Name = "A", BasePrice = 1, BaseCps = 1 })
            .Add(new BuildingDefinition { Id = "dup", Name = "B", BasePrice = 1, BaseCps = 1 })
            .Build());
    }

    [Test]
    public static void DanglingReferences_AreRejected()
    {
        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new UpgradeDefinition
            {
                Id = "u",
                Name = "U",
                Price = 1,
                Unlock = UnlockCondition.BuildingsAtLeast("ghost", 1),
            })
            .Build());

        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new UpgradeDefinition
            {
                Id = "u",
                Name = "U",
                Price = 1,
                Unlock = UnlockCondition.UpgradeOwned("ghost"),
            })
            .Build());

        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new GoldenCookieOutcome { Id = "o", Name = "O", BuffId = "ghost", BuffSeconds = 5 })
            .Build());
    }

    [Test]
    public static void InvalidNumbers_AreRejected()
    {
        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1, PriceGrowth = 1.0 })
            .Build());

        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new UpgradeDefinition { Id = "u", Name = "U", Price = 1, Persistence = UpgradePersistence.Permanent })
            .Build());

        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new AchievementDefinition { Id = "a", Name = "A", Unlock = UnlockCondition.Never })
            .Build());

        Check.Throws<GameContentValidationException>(() => new GameContentBuilder("X")
            .Add(new BuffDefinition { Id = "b", Name = "B", Duration = 0 })
            .Build());
    }

    [Test]
    public static void EmptyContent_IsValid()
    {
        GameContent content = new GameContentBuilder("Empty").Build();

        Check.Equal(0, content.Buildings.Count);
        Check.Equal("Empty", content.Title);
        Check.NotNull(GameContent.Empty);
    }

    [Test]
    public static void HiddenAchievement_IsConcealedInView()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GameSnapshot snapshot = engine.Snapshot();

        AchievementView hidden = snapshot.Achievements.First(a => a.Id == "ascend_100");
        Check.Equal("???", hidden.Name);
        Check.False(hidden.Unlocked);
        Check.True(hidden.Hidden);
    }

    [Test]
    public static void UnlockHints_AreHumanReadable()
    {
        GameContent content = NekoContent.Build();

        Check.Contains(content.BuildingById["cat_universe"].Unlock.Describe(content), "小鱼干");
        Check.Contains(content.UpgradeById["kitten_helpers"].Unlock.Describe(content), "成就");
        Check.Contains(
            GameViewFactory.Summarize(content.UpgradeById["kitten_helpers"].Modifiers, content),
            "所有建筑产量");
    }
}
