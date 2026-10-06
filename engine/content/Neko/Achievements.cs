using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 示例内容包的成就表。<para>
/// 成就本身<b>几乎不给数值</b>——它们的作用是喂给"小猫"系列升级（见 <see cref="Upgrades"/>），
/// 换算成全局倍率。这样做的结果是：玩家在追求任何一个目标时，都会顺带解锁成就，
/// 而成就又立刻变成实打实的产量提升，不会出现"解锁了但没用"的失落感。
/// </para>
/// <para>少数成就直接给修饰符，用来演示另一条路径。</para>
/// </summary>
internal static class Achievements
{
    /// <summary>
    /// 本包的 <c>text.json</c>：成就文案与建筑文案<b>共用同一份实例</b>（<see cref="Buildings.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Buildings.Prose;

    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All => [.. BuildingMilestones(), .. GlobalMilestones()];

    private static IEnumerable<AchievementDefinition> BuildingMilestones()
    {
        int[] tiers =
        [
            1,
            25,
            50,
            100,
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach (int count in tiers)
            {
                string id = $"{building.Id}_x{count}";
                yield return new AchievementDefinition
                {
                    Id = id,
                    Name = Prose.Text("achievements", id, "name"),
                    Icon = Prose.Text("achievements", id, "icon"),
                    Description = Prose.Text("achievements", id, "description"),
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                    Category = "building",
                    Tier = count switch { 1 => 1, 25 => 2, 50 => 3, _ => 4 },
                };
            }
        }
    }

    private static IEnumerable<AchievementDefinition> GlobalMilestones()
    {
        // ---- 累计赚取 ----
        (double Amount, string Id)[] earnings =
        [
            (1_000, "earn_1k"),
            (1_000_000, "earn_1m"),
            (1_000_000_000, "earn_1b"),
            (1_000_000_000_000, "earn_1t"),
            (1_000_000_000_000_000, "earn_1q"),
            (1_000_000_000_000_000_000, "earn_1qi"),
            (1e21, "earn_1sx"),
            (1e24, "earn_1sp"),
        ];
        foreach ((double amount, string id) in earnings)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
            };
        }

        // ---- 每秒产量 ----
        (double Cps, string Id)[] cpsMilestones =
        [
            (1_000_000, "cps_1m"),
            (1_000_000_000, "cps_1b"),
            (1_000_000_000_000, "cps_1t"),
        ];
        foreach ((double cps, string id) in cpsMilestones)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.CpsAtLeast(cps),
                Category = "progress",
            };
        }

        // ---- 点击 ----
        yield return new AchievementDefinition
        {
            Id = "click_100",
            Name = Prose.Text("achievements", "click_100", "name"),
            Icon = Prose.Text("achievements", "click_100", "icon"),
            Description = Prose.Text("achievements", "click_100", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Category = "click",
        };

        yield return new AchievementDefinition
        {
            Id = "click_1000",
            Name = Prose.Text("achievements", "click_1000", "name"),
            Icon = Prose.Text("achievements", "click_1000", "icon"),
            Description = Prose.Text("achievements", "click_1000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(1_000),
            Category = "click",
        };

        yield return new AchievementDefinition
        {
            Id = "click_10000",
            Name = Prose.Text("achievements", "click_10000", "name"),
            Icon = Prose.Text("achievements", "click_10000", "icon"),
            Description = Prose.Text("achievements", "click_10000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(10_000),
            Modifiers = [Modifier.ClickMultiplier(1.5)],
            Category = "click",
            Tier = 2,
        };

        yield return new AchievementDefinition
        {
            Id = "click_100000",
            Name = Prose.Text("achievements", "click_100000", "name"),
            Icon = Prose.Text("achievements", "click_100000", "icon"),
            Description = Prose.Text("achievements", "click_100000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(100_000),
            Category = "click",
            Tier = 3,
        };

        // ---- 金猫 ----
        (double Count, string Id)[] golden =
        [
            (1, "golden_1"),
            (7, "golden_7"),
            (27, "golden_27"),
            (77, "golden_77"),
        ];
        foreach ((double count, string id) in golden)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
                Category = "golden",
            };
        }

        // ---- 规模 ----
        (double Count, string Id)[] scale =
        [
            (100, "scale_100"),
            (500, "scale_500"),
            (1_000, "scale_1000"),
        ];
        foreach ((double count, string id) in scale)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.TotalBuildingsAtLeast(count),
                Category = "scale",
            };
        }

        yield return new AchievementDefinition
        {
            Id = "full_house",
            Name = Prose.Text("achievements", "full_house", "name"),
            Icon = Prose.Text("achievements", "full_house", "icon"),
            Description = Prose.Text("achievements", "full_house", "description"),
            Unlock = UnlockCondition.All(Buildings.All.Select(b => UnlockCondition.BuildingsAtLeast(b.Id, 1)).ToArray()),
            Category = "scale",
            Tier = 2,
        };

        // ---- 转生 ----
        yield return new AchievementDefinition
        {
            Id = "ascend_1",
            Name = Prose.Text("achievements", "ascend_1", "name"),
            Icon = Prose.Text("achievements", "ascend_1", "icon"),
            Description = Prose.Text("achievements", "ascend_1", "description"),
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.ClickMultiplier(1.2)],
            Category = "prestige",
            Tier = 1,
        };

        yield return new AchievementDefinition
        {
            Id = "ascend_10",
            Name = Prose.Text("achievements", "ascend_10", "name"),
            Icon = Prose.Text("achievements", "ascend_10", "icon"),
            Description = Prose.Text("achievements", "ascend_10", "description"),
            Unlock = UnlockCondition.PrestigeLevelAtLeast(10),
            Category = "prestige",
            Tier = 2,
        };

        yield return new AchievementDefinition
        {
            Id = "ascend_100",
            Name = Prose.Text("achievements", "ascend_100", "name"),
            Icon = Prose.Text("achievements", "ascend_100", "icon"),
            Description = Prose.Text("achievements", "ascend_100", "description"),
            Unlock = UnlockCondition.PrestigeLevelAtLeast(100),
            Hidden = true,
            Category = "prestige",
            Tier = 4,
        };
    }

    private static string NekoFormat(double value)
        => NekoClicker.Core.Numbers.NumFormat.Format(value, NekoClicker.Core.Numbers.NumberStyle.Short);
}
