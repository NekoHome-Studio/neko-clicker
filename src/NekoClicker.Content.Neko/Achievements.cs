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
    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All => [.. BuildingMilestones(), .. GlobalMilestones()];

    private static IEnumerable<AchievementDefinition> BuildingMilestones()
    {
        (int Count, string Prefix, string Suffix)[] tiers =
        [
            (1, string.Empty, "的开端"),
            (25, string.Empty, "收藏家"),
            (50, string.Empty, "军团"),
            (100, string.Empty, "帝国"),
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int count, string prefix, string suffix) in tiers)
            {
                yield return new AchievementDefinition
                {
                    Id = $"{building.Id}_x{count}",
                    Name = $"{prefix}{building.Name}{suffix}",
                    Icon = building.Icon,
                    Description = $"拥有 {count} 个「{building.Name}」。",
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
        (double Amount, string Id, string Name, string Icon)[] earnings =
        [
            (1_000, "earn_1k", "第一桶金", "🪙"),
            (1_000_000, "earn_1m", "小有积蓄", "💰"),
            (1_000_000_000, "earn_1b", "猫粮自由", "🏦"),
            (1_000_000_000_000, "earn_1t", "兆级猫咖", "🏛️"),
            (1_000_000_000_000_000, "earn_1q", "京级猫咖", "🌆"),
            (1_000_000_000_000_000_000, "earn_1qi", "猫粮通胀", "📈"),
            (1e21, "earn_1sx", "猫粮霸权", "👑"),
            (1e24, "earn_1sp", "猫粮宇宙", "🌌"),
        ];
        foreach ((double amount, string id, string name, string icon) in earnings)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"历史累计赚取 {NekoFormat(amount)} 条小鱼干。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
            };
        }

        // ---- 每秒产量 ----
        (double Cps, string Id, string Name)[] cpsMilestones =
        [
            (1_000_000, "cps_1m", "流量猫咖"),
            (1_000_000_000, "cps_1b", "十亿产量"),
            (1_000_000_000_000, "cps_1t", "兆级流水线"),
        ];
        foreach ((double cps, string id, string name) in cpsMilestones)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒产量达到 {NekoFormat(cps)}。",
                Unlock = UnlockCondition.CpsAtLeast(cps),
                Category = "progress",
            };
        }

        // ---- 点击 ----
        yield return new AchievementDefinition
        {
            Id = "click_100",
            Name = "手有点酸",
            Icon = "👆",
            Description = "点击 100 次。",
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Category = "click",
        };

        yield return new AchievementDefinition
        {
            Id = "click_1000",
            Name = "千次撸猫",
            Icon = "✋",
            Description = "点击 1,000 次。",
            Unlock = UnlockCondition.ClicksAtLeast(1_000),
            Category = "click",
        };

        yield return new AchievementDefinition
        {
            Id = "click_10000",
            Name = "万次撸猫",
            Icon = "💪",
            Description = "点击 10,000 次。作为奖励，点击收益 ×1.5。",
            Unlock = UnlockCondition.ClicksAtLeast(10_000),
            Modifiers = [Modifier.ClickMultiplier(1.5)],
            Category = "click",
            Tier = 2,
        };

        yield return new AchievementDefinition
        {
            Id = "click_100000",
            Name = "机械手臂",
            Icon = "🦾",
            Description = "点击 100,000 次。你已经分不清是你在撸猫还是猫在撸你。",
            Unlock = UnlockCondition.ClicksAtLeast(100_000),
            Category = "click",
            Tier = 3,
        };

        // ---- 金猫 ----
        (double Count, string Id, string Name, string Icon)[] golden =
        [
            (1, "golden_1", "第一只金猫", "🌟"),
            (7, "golden_7", "幸运七", "🎰"),
            (27, "golden_27", "金猫猎人", "🏹"),
            (77, "golden_77", "金猫传说", "🐱‍🚀"),
        ];
        foreach ((double count, string id, string name, string icon) in golden)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"点中 {NekoFormat(count)} 只金猫。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
                Category = "golden",
            };
        }

        // ---- 规模 ----
        (double Count, string Id, string Name, string Icon)[] scale =
        [
            (100, "scale_100", "小型猫咖", "🏠"),
            (500, "scale_500", "连锁猫咖", "🏢"),
            (1_000, "scale_1000", "猫咖王国", "🏰"),
        ];
        foreach ((double count, string id, string name, string icon) in scale)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"拥有 {NekoFormat(count)} 座建筑。",
                Unlock = UnlockCondition.TotalBuildingsAtLeast(count),
                Category = "scale",
            };
        }

        yield return new AchievementDefinition
        {
            Id = "full_house",
            Name = "全家福",
            Icon = "📸",
            Description = "每种建筑至少拥有 1 个。",
            Unlock = UnlockCondition.All(Buildings.All.Select(b => UnlockCondition.BuildingsAtLeast(b.Id, 1)).ToArray()),
            Category = "scale",
            Tier = 2,
        };

        // ---- 转生 ----
        yield return new AchievementDefinition
        {
            Id = "ascend_1",
            Name = "第一次轮回",
            Icon = "🔁",
            Description = "完成第一次转生。作为奖励，点击收益 ×1.2。",
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.ClickMultiplier(1.2)],
            Category = "prestige",
            Tier = 1,
        };

        yield return new AchievementDefinition
        {
            Id = "ascend_10",
            Name = "十世猫奴",
            Icon = "♻️",
            Description = "转生等级达到 10。",
            Unlock = UnlockCondition.PrestigeLevelAtLeast(10),
            Category = "prestige",
            Tier = 2,
        };

        yield return new AchievementDefinition
        {
            Id = "ascend_100",
            Name = "？？？",
            Icon = "???",
            Description = "隐藏成就。",
            Unlock = UnlockCondition.PrestigeLevelAtLeast(100),
            Hidden = true,
            Category = "prestige",
            Tier = 4,
        };
    }

    private static string NekoFormat(double value)
        => NekoClicker.Core.Numbers.NumFormat.Format(value, NekoClicker.Core.Numbers.NumberStyle.Plain);
}
