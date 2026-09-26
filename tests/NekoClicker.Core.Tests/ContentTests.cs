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
    public static void BuildingCurve_FollowsTheRecipeInEveryContentPack()
    {
        // 曲线配方（CONTENT_AUTHORING §2 / STAGE_5_RESKINS §4 第 1 条）：相邻价格 ×5~20、
        // 产量 ×3~12，且从第 3 座起价格涨得必须比产量快——否则早期建筑永远不会被淘汰，
        // 玩家会一直买第一座。**换包换的是叙事，不是手感**，所以这条对每个包都成立。
        //
        // 加这条通用守卫之前先把既有 8 个包量了一遍（含九命那 12 座）：全部落在带内，
        // 所以它不是"给新包开的特例"，而是把一条一直靠人记的纪律变成守卫。
        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            for (int i = 1; i < content.Buildings.Count; i++)
            {
                BuildingDefinition previous = content.Buildings[i - 1];
                BuildingDefinition current = content.Buildings[i];

                Check.Greater(current.BasePrice, previous.BasePrice, $"{name}：「{current.Name}」的价格应高于上一层。");
                Check.Greater(current.BaseCps, previous.BaseCps, $"{name}：「{current.Name}」的产量应高于上一层。");

                double priceRatio = current.BasePrice / previous.BasePrice;
                double cpsRatio = current.BaseCps / previous.BaseCps;
                Check.True(priceRatio is >= 5 and <= 20, $"{name}：「{previous.Name}」→「{current.Name}」价格倍率 {priceRatio:F2} 超出 5~20。");
                Check.True(cpsRatio is >= 3 and <= 12, $"{name}：「{previous.Name}」→「{current.Name}」产量倍率 {cpsRatio:F2} 超出 3~12。");

                if (i >= 2)
                    Check.Greater(priceRatio, cpsRatio, $"{name}：「{current.Name}」的产量倍率不应超过价格倍率，否则价格曲线会失控。");
            }
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
    public static void CounterNames_AreRegisteredForEveryReferencedCounter()
    {
        // 计数器键是内部标识（`readership` / `memory_shards` / `morale`…），而它会出现在
        // 解锁提示、升级效果说明与"本层规则"里。没有显示名的话，玩家看到的是
        // 「每点「readership」 +0.01%」这种半成品文案——七个包全都有这个问题，因为
        // 渲染层一直在拿键当名字用。
        //
        // 修法是把"显示名"变成内容可以声明的东西（`IGameModule.Configure` 里登记），
        // 这条守卫则保证**内容引用到的每一个计数器都登记过**——而不是靠人去记。
        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            foreach (string key in ReferencedCounters(content))
            {
                Check.True(
                    content.CounterNames.TryGetValue(key, out string? display),
                    $"{name} 引用了计数器「{key}」却没有登记显示名——玩家会看到内部键。"
                    + "在模块的 Configure 里调用 AddCounterName 即可。");

                // 光登记还不够：两条渲染路径都得**真的**用上它。
                string hint = UnlockCondition.Counter(key, 1).Describe(content);
                Check.Contains(hint, display!, $"{name} 的解锁提示没有用计数器的显示名：{hint}");
                Check.False(
                    hint.Contains(key, StringComparison.Ordinal),
                    $"{name} 的解锁提示里还露着内部键：{hint}");

                string effect = Modifier
                    .GlobalPercent(0, new Scaling(ScalingSource.CustomCounter, 0.001, Id: key))
                    .Describe(content);
                Check.Contains(effect, display!, $"{name} 的升级效果没有用计数器的显示名：{effect}");
                Check.False(
                    effect.Contains(key, StringComparison.Ordinal),
                    $"{name} 的升级效果里还露着内部键：{effect}");
            }
        }
    }

    /// <summary>扫出一个内容包里所有被引用的计数器键（成长曲线 + 解锁条件）。</summary>
    private static IEnumerable<string> ReferencedCounters(GameContent content)
    {
        List<UnlockCondition> conditions =
        [
            .. content.Buildings.Select(b => b.Unlock),
            .. content.Upgrades.Select(u => u.Unlock),
            .. content.Achievements.Select(a => a.Unlock),
            .. content.Eras.Select(e => e.Completion),
            .. content.LoreEntries.Select(l => l.Reveal),
            .. content.Choices.Select(c => c.Trigger),
            .. content.Endings.Select(e => e.Condition),
        ];

        List<IReadOnlyList<Modifier>> modifierLists =
        [
            .. content.Upgrades.Select(u => u.Modifiers),
            .. content.Achievements.Select(a => a.Modifiers),
            .. content.Buffs.Select(b => b.Modifiers),
            .. content.Eras.Select(e => e.Modifiers),
            .. content.Choices.SelectMany(c => c.Options).Select(o => o.Modifiers),
        ];

        return conditions
            .SelectMany(c => c.NumericLeaves())
            .Where(leaf => leaf.Metric == NumericMetric.Counter && !string.IsNullOrEmpty(leaf.Id))
            .Select(leaf => leaf.Id!)
            .Concat(modifierLists
                .SelectMany(list => list)
                .Where(m => m.Scaling is { Source: ScalingSource.CustomCounter } s
                            && !string.IsNullOrEmpty(s.Id))
                .Select(m => m.Scaling!.Id!))
            .Distinct(StringComparer.Ordinal);
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
