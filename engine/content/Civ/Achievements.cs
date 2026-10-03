using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 成就表（60 条）。<para>
/// 大部分由生成器铺出来（建筑档 / 产能档 / 产量档 / 拍档 / 天灾档 / 时代档 / 文化档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// <b>文化档是这个包唯一一条与本包第二资源直接挂钩的成就线</b>（2500 / 12000 / 80000 / 400000），
/// 而它同时是"文明到底记住了多少"的读数：文化只由五座记录者建筑产出，
/// 所以这一档亮起来的时候，玩家已经能指出是哪几座建筑在养它。
/// </para>
/// <para>
/// 末层的完成条件要求 20 个成就，三个结局都不额外要求成就数——
/// 里程碑只负责让"走得够远"这件事在数值上留痕。
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
        .. DisasterTiers(),
        .. EraTiers(),
        .. CultureTiers(),
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

    /// <summary>历史累计产能档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("earned_1e4", 1e4),
            ("earned_1e6", 1e6),
            ("earned_1e8", 1e8),
            ("earned_1e10", 1e10),
            ("earned_1e12", 1e12),
            ("earned_1e14", 1e14),
            ("earned_1e16", 1e16),
            ("earned_1e18", 1e18),
            ("earned_1e20", 1e20),
            ("earned_1e22", 1e22),
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

    /// <summary>每秒产能档。</summary>
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

    /// <summary>拍（点击）档：这个包的手比别的包硬一点，所以门槛也高一点。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("build_100", 100),
            ("build_1000", 1_000),
            ("build_10000", 10_000),
            ("build_100000", 100_000),
            ("build_1000000", 1_000_000),
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

    /// <summary>天灾档：从洪水到丰收年，都是同一套随机事件。</summary>
    private static IEnumerable<AchievementDefinition> DisasterTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("disaster_1", 1),
            ("disaster_10", 10),
            ("disaster_50", 50),
            ("disaster_200", 200),
            ("disaster_500", 500),
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

    /// <summary>时代档：每一层一条，最后一条带修饰符（五个时代都走完之后）。</summary>
    private static IEnumerable<AchievementDefinition> EraTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            string id = $"era_{index}";
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>文化档：最后一条带修饰符——"记得住"本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> CultureTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("culture_2500", 2_500),
            ("culture_12000", 12_000),
            ("culture_80000", 80_000),
            ("culture_400000", 400_000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(CultureModule.CounterKey, value),
                Modifiers = id == "culture_400000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
