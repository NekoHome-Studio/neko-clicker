using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（设备档 / 赚取档 / 产量档 / 记录档 / 事故档 / 批次档 / 伦理档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（5 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
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
        .. AccidentTiers(),
        .. BatchTiers(),
        .. EthicsTiers(),
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

    /// <summary>历史累计数据档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("data_1e4", 1e4),
            ("data_1e6", 1e6),
            ("data_1e8", 1e8),
            ("data_1e10", 1e10),
            ("data_1e12", 1e12),
            ("data_1e14", 1e14),
            ("data_1e16", 1e16),
            ("data_1e18", 1e18),
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

    /// <summary>手动记录档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("log_100", 100),
            ("log_1000", 1_000),
            ("log_10000", 10_000),
            ("log_100000", 100_000),
            ("log_1000000", 1_000_000),
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

    /// <summary>实验事故档。</summary>
    private static IEnumerable<AchievementDefinition> AccidentTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("accident_1", 1),
            ("accident_10", 10),
            ("accident_50", 50),
            ("accident_200", 200),
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

    /// <summary>批次档：每一批一条，最后一条带修饰符（她开始记得了）。</summary>
    private static IEnumerable<AchievementDefinition> BatchTiers()
    {
        for (int index = 1; index <= 7; index++)
        {
            string id = $"batch_{index}";
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 7 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>伦理值档。最后一条带修饰符——伦理值本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> EthicsTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("ethics_50", 50),
            ("ethics_200", 200),
            ("ethics_800", 800),
            ("ethics_2000", 2000),
            ("ethics_5000", 5000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, value),
                Modifiers = id == "ethics_5000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
