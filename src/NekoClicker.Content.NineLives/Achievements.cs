using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 成就表：63 个，全部由"只会涨"的指标触发。<para>
/// 其中 36 个是建筑档位、8 个是按纪元推进（"第几次醒来"），
/// 剩下的覆盖赚取、产量、点击与随机事件。
/// </para>
/// <para>
/// 成就同时是"呼噜线"的燃料：<c>Scaling(AchievementCount)</c> 把它们直接换算成全局产量，
/// 所以多解锁一个成就永远划算——这也是纪元完成条件用成就数的原因。
/// </para>
/// </summary>
internal static class Achievements
{
    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All =>
    [
        .. BuildingMilestones(),
        .. EarningMilestones(),
        .. EraMilestones(),
        .. InteractionMilestones(),
    ];

    private static IEnumerable<AchievementDefinition> BuildingMilestones()
    {
        (int Count, string Suffix)[] tiers = [(1, "的开端"), (10, "成排"), (25, "成林")];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int count, string suffix) in tiers)
            {
                yield return new AchievementDefinition
                {
                    Id = $"{building.Id}_x{count}",
                    Name = $"{building.Name}{suffix}",
                    Icon = building.Icon,
                    Description = $"拥有 {count} 个「{building.Name}」。",
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                    Category = "building",
                    Tier = count switch { 1 => 1, 10 => 2, _ => 3 },
                };
            }
        }
    }

    private static IEnumerable<AchievementDefinition> EarningMilestones()
    {
        (double Amount, string Id, string Name, string Icon)[] earnings =
        [
            (1_000, "earn_1e3", "第一桶鱼干", "🪙"),
            (1_000_000, "earn_1e6", "够活了", "💰"),
            (1_000_000_000, "earn_1e9", "够养一屋子猫", "🏦"),
            (1_000_000_000_000, "earn_1e12", "兆级小店", "🏛️"),
            (1_000_000_000_000_000, "earn_1e15", "京级产业", "🌆"),
            (1e18, "earn_1e18", "鱼干通胀", "📈"),
            (1e21, "earn_1e21", "鱼干霸权", "👑"),
            (1e24, "earn_1e24", "鱼干宇宙", "🌌"),
        ];
        foreach ((double amount, string id, string name, string icon) in earnings)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"历史累计赚取 {Plain(amount)} 条小鱼干。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
            };
        }

        (double Cps, string Id, string Name, string Icon)[] cps =
        [
            (1_000_000, "cps_1e6", "每分钟都在呼噜", "🎵"),
            (1_000_000_000, "cps_1e9", "整栋楼都在响", "🔊"),
            (1_000_000_000_000, "cps_1e12", "世界的心跳", "💗"),
        ];
        foreach ((double value, string id, string name, string icon) in cps)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"每秒产量达到 {Plain(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
                Category = "progress",
            };
        }
    }

    /// <summary>按纪元推进的成就：第几次醒来。条件用 EraAtLeast，单调且必达。</summary>
    private static IEnumerable<AchievementDefinition> EraMilestones()
    {
        for (int era = 2; era <= 9; era++)
        {
            yield return new AchievementDefinition
            {
                Id = $"wake_{era}",
                Name = $"第 {era} 次醒来",
                Icon = "🌙",
                Description = $"进入第 {era} 纪元。她记得的东西又多了一点。",
                Unlock = UnlockCondition.EraAtLeast(era),
                Category = "era",
                Tier = era,
            };
        }
    }

    private static IEnumerable<AchievementDefinition> InteractionMilestones()
    {
        (double Count, string Id, string Name, string Icon)[] clicks =
        [
            (100, "click_100", "手有点酸", "👆"),
            (1_000, "click_1000", "她把头靠过来了", "✋"),
            (10_000, "click_10000", "闭上眼也知道是你", "💪"),
            (100_000, "click_100000", "不用睁眼的默契", "🦾"),
        ];
        foreach ((double count, string id, string name, string icon) in clicks)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"摸头 {Plain(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
                // 少数成就直接给数值：不是每个里程碑都要走"呼噜线"。
                Modifiers = id == "click_10000" ? [Modifier.ClickMultiplier(1.5)] : [],
                Category = "touch",
                Tier = 1,
            };
        }

        (double Count, string Id, string Name, string Icon)[] events =
        [
            (1, "echo_1", "第一次残响", "🌟"),
            (7, "echo_7", "她开始记得路", "🎐"),
            (27, "echo_27", "残响也会呼噜", "🔔"),
            (77, "echo_77", "九命共鸣", "⛓️"),
        ];
        foreach ((double count, string id, string name, string icon) in events)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"捕捉 {Plain(count)} 次情感残响。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
                Category = "echo",
            };
        }
    }

    private static string Plain(double value)
        => NekoClicker.Core.Numbers.NumFormat.Format(value, NekoClicker.Core.Numbers.NumberStyle.Plain);
}
