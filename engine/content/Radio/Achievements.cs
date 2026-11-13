using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 成就表（C1《猫娘电台》一层 MVP）。<para>
/// 大部分由生成器铺出来（建筑档 / 信号档 / 产量档 / 点击档 / 来电档 / 频段档 / 收听档），
/// 少数几条带修饰符——手写的那几条是"里程碑真的给东西"的地方。结局成就也在这里，
/// 它们自己声明 <c>Unlock = EndingReached(...)</c>，走常规成就检查路径解锁。
/// </para>
/// <para>
/// <b>收听档是唯一一条"可能掉回去"的门槛</b>：收听率会衰减、点击会掉、转生清零，
/// 所以这一档只能在"收听率正养着"的时候达成——逼着玩家把听众留住，而这个包的主题就是
/// "有人听，电台才在"。成就一旦解锁就永久保留，不会被收回去。
/// </para>
/// </summary>
internal static class Achievements
{
    /// <summary>本包的 text.json：成就文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All =>
    [
        .. BuildingTiers(),
        .. EarningTiers(),
        .. ProductionTiers(),
        .. ClickTiers(),
        .. CallerTiers(),
        .. EraTiers(),
        .. ListenerTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        int[] tiers = [1, 25, 50];
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
                };
            }
        }
    }

    /// <summary>历史累计信号档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("signal_1e4", 1e4),
            ("signal_1e6", 1e6),
            ("signal_1e8", 1e8),
            ("signal_1e10", 1e10),
            ("signal_1e12", 1e12),
            ("signal_1e14", 1e14),
            ("signal_1e16", 1e16),
            ("signal_1e18", 1e18),
        ];
        foreach ((string id, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("cps_1e3", 1e3),
            ("cps_1e6", 1e6),
            ("cps_1e9", 1e9),
            ("cps_1e12", 1e12),
            ("cps_1e15", 1e15),
            ("cps_1e18", 1e18),
        ];
        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>点击档：这个包点击是干扰，但手痒的次数仍被记下来（成就一旦解锁不收回）。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("touch_100", 100),
            ("touch_1000", 1_000),
            ("touch_10000", 10_000),
            ("touch_100000", 100_000),
            ("touch_1000000", 1_000_000),
        ];
        foreach ((string id, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>来电档：金猫（听众来电）点中的次数。</summary>
    private static IEnumerable<AchievementDefinition> CallerTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("caller_1", 1),
            ("caller_10", 10),
            ("caller_50", 50),
            ("caller_200", 200),
            ("caller_500", 500),
            ("caller_1000", 1_000),
        ];
        foreach ((string id, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>频段档：一层 MVP 只有一条（进入第 1 频段），带修饰符。</summary>
    private static IEnumerable<AchievementDefinition> EraTiers()
    {
        yield return new AchievementDefinition
        {
            Id = "band_1",
            Name = Prose.Text("achievements", "band_1", "name"),
            Icon = Prose.Text("achievements", "band_1", "icon"),
            Description = Prose.Text("achievements", "band_1", "description"),
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
        };
    }

    /// <summary>收听档。最后一条带修饰符——有人听本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> ListenerTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("listeners_2000", 2_000),
            ("listeners_8000", 8_000),
            ("listeners_20000", 20_000),
            ("listeners_38000", 38_000),
            ("listeners_55000", 55_000),
            ("listeners_70000", 70_000),
        ];
        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(BroadcastModule.CounterKey, value),
                Modifiers = id == "listeners_70000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
