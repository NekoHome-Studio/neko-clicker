using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 成就表（71 条）。<para>
/// 大部分由生成器铺出来（神庙档 / 香火档 / 产量档 / 显灵档 / 神迹档 / 神话档 / 信仰档 / 在线档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（3 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>信仰档与在线档的最后一档刻意压在末层门槛之下</b>：信仰只涨不花，它的量级被五层纪元
/// 的门槛钉在同一个包络里，所以成就门槛只要贴着包络设就一定能拿到（对照阶段 4B：
/// 那一批"最高 200 万"的记忆档就是没贴着包络设，实测全是死内容）。
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
        .. MiracleTiers(),
        .. MythTiers(),
        .. FaithTiers(),
        .. ViewerTiers(),
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

    /// <summary>历史累计香火档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("incense_1e4", 1e4),
            ("incense_1e6", 1e6),
            ("incense_1e8", 1e8),
            ("incense_1e10", 1e10),
            ("incense_1e12", 1e12),
            ("incense_1e14", 1e14),
            ("incense_1e16", 1e16),
            ("incense_1e18", 1e18),
            ("incense_1e20", 1e20),
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

    /// <summary>显灵档（点击）。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("bless_100", 100),
            ("bless_1000", 1_000),
            ("bless_10000", 10_000),
            ("bless_100000", 100_000),
            ("bless_1000000", 1_000_000),
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

    /// <summary>神迹档：金猫（神迹）来了多少次。</summary>
    private static IEnumerable<AchievementDefinition> MiracleTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("miracle_1", 1),
            ("miracle_10", 10),
            ("miracle_50", 50),
            ("miracle_200", 200),
            ("miracle_500", 500),
            ("miracle_1000", 1_000),
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

    /// <summary>神话档：每一套体系一条，最后一条带修饰符（五套都走过之后）。</summary>
    private static IEnumerable<AchievementDefinition> MythTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            string id = $"myth_{index}";
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

    /// <summary>信仰档。最后一条带修饰符——香火自己开始产能。</summary>
    private static IEnumerable<AchievementDefinition> FaithTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("faith_500", 500),
            ("faith_5k", 5_000),
            ("faith_40k", 40_000),
            ("faith_300k", 300_000),
            ("faith_1_2m", 1.2e6),
            ("faith_3m", 3e6),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(FaithModule.CounterKey, value),
                Modifiers = id == "faith_3m" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }

    /// <summary>在线档：直播间的历史峰值（最后一条压在末层门槛之下）。</summary>
    private static IEnumerable<AchievementDefinition> ViewerTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("viewers_100", 100),
            ("viewers_2500", 2_500),
            ("viewers_20000", 20_000),
            ("viewers_80000", 80_000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(FaithModule.ViewerCounterKey, value),
            };
        }
    }
}
