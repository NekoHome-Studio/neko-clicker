using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（设备档 / 营收档 / 产量档 / 谈单档 / 甲方档 / 重组档 / 士气档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（4 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// </summary>
internal static class Achievements
{
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All =>
    [
        .. BuildingTiers(),
        .. EarningTiers(),
        .. ProductionTiers(),
        .. ClickTiers(),
        .. RequirementTiers(),
        .. RoundTiers(),
        .. MoraleTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每台设备三档：1 / 25 / 50 台。</summary>
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

    /// <summary>历史累计营收档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("revenue_1e4", 1e4),
            ("revenue_1e6", 1e6),
            ("revenue_1e8", 1e8),
            ("revenue_1e10", 1e10),
            ("revenue_1e12", 1e12),
            ("revenue_1e14", 1e14),
            ("revenue_1e16", 1e16),
            ("revenue_1e18", 1e18),
            ("revenue_1e20", 1e20),
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

    /// <summary>亲手谈单档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("pitch_100", 100),
            ("pitch_1000", 1_000),
            ("pitch_10000", 10_000),
            ("pitch_100000", 100_000),
            ("pitch_1000000", 1_000_000),
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

    /// <summary>甲方档案：处理过的「改需求」次数。</summary>
    private static IEnumerable<AchievementDefinition> RequirementTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("requirement_1", 1),
            ("requirement_10", 10),
            ("requirement_50", 50),
            ("requirement_200", 200),
            ("requirement_500", 500),
            ("requirement_1000", 1_000),
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

    /// <summary>重组档：每一轮一条，最后一条带修饰符（公司终于开始像样了）。</summary>
    private static IEnumerable<AchievementDefinition> RoundTiers()
    {
        for (int index = 1; index <= 3; index++)
        {
            string id = $"round_{index}";
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 3 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>士气档。最后一条带修饰符——人愿意干本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> MoraleTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("morale_100", 100),
            ("morale_500", 500),
            ("morale_2000", 2_000),
            ("morale_8000", 8_000),
            ("morale_20000", 20_000),
            ("morale_50000", 50_000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, value),
                Modifiers = id == "morale_50000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
