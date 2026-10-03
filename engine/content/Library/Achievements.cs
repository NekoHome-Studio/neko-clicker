using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（藏书档 / 页数档 / 产量档 / 提笔档 / 蠹虫档 / 成书档 / 读者档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（2 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>读者档是唯一一条"可能掉回去"的门槛</b>：被阅读度会衰减、每次开新书还会清零，
/// 所以这一档的成就只能在"读者正养着"的时候达成。这是刻意的——它逼着玩家把读者留住，
/// 而这个包的主题就是"有人读，书才在"。成就一旦解锁就永久保留，不会被收回去。
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
        .. BookwormTiers(),
        .. BookTiers(),
        .. ReaderTiers(),
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

    /// <summary>历史累计页数档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("pages_1e4", 1e4),
            ("pages_1e6", 1e6),
            ("pages_1e8", 1e8),
            ("pages_1e10", 1e10),
            ("pages_1e12", 1e12),
            ("pages_1e14", 1e14),
            ("pages_1e16", 1e16),
            ("pages_1e18", 1e18),
            ("pages_1e20", 1e20),
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

    /// <summary>提笔档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("write_100", 100),
            ("write_1000", 1_000),
            ("write_10000", 10_000),
            ("write_100000", 100_000),
            ("write_1000000", 1_000_000),
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

    /// <summary>蠹虫档案：从书页里钻出来的次数。</summary>
    private static IEnumerable<AchievementDefinition> BookwormTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("bookworm_1", 1),
            ("bookworm_10", 10),
            ("bookworm_50", 50),
            ("bookworm_200", 200),
            ("bookworm_500", 500),
            ("bookworm_1000", 1_000),
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

    /// <summary>成书档：每一本一条，最后一条带修饰符（五本书排在一起之后）。</summary>
    private static IEnumerable<AchievementDefinition> BookTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            string id = $"book_{index}";
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

    /// <summary>读者档。最后一条带修饰符——有人读本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> ReaderTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("readers_2000", 2_000),
            ("readers_8000", 8_000),
            ("readers_20000", 20_000),
            ("readers_38000", 38_000),
            ("readers_55000", 55_000),
            ("readers_70000", 70_000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(ReadershipModule.CounterKey, value),
                Modifiers = id == "readers_70000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
