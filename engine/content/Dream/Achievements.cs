using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 成就表（68 条）。<para>
/// 大部分由生成器铺出来（建筑档 / 梦量档 / 产量档 / 闭眼档 / 梦魇档 / 梦层档 / 梦境能量档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（2 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>梦境能量档是这个包最实在的一条线</b>：计数器只涨不落（<c>DreamEnergyModule</c> 不在
/// 转生时清零），所以它一旦解锁就再也不会"退回去"。整条线从 2,000 铺到 70,000，
/// 门槛压在实测包络里——它同时也是玩家判断"离叫醒她还有多远"的刻度尺。
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
        .. NightmareTiers(),
        .. SleepLayerTiers(),
        .. DreamEnergyTiers(),
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

    /// <summary>历史累计梦量档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("dream_1e4", 1e4),
            ("dream_1e6", 1e6),
            ("dream_1e8", 1e8),
            ("dream_1e10", 1e10),
            ("dream_1e12", 1e12),
            ("dream_1e14", 1e14),
            ("dream_1e16", 1e16),
            ("dream_1e18", 1e18),
            ("dream_1e20", 1e20),
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

    /// <summary>闭眼档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("close_100", 100),
            ("close_1000", 1_000),
            ("close_10000", 10_000),
            ("close_100000", 100_000),
            ("close_1000000", 1_000_000),
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

    /// <summary>梦魇档：从梦的褶皱里翻上来的次数。</summary>
    private static IEnumerable<AchievementDefinition> NightmareTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("nightmare_1", 1),
            ("nightmare_10", 10),
            ("nightmare_50", 50),
            ("nightmare_200", 200),
            ("nightmare_500", 500),
            ("nightmare_1000", 1_000),
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

    /// <summary>梦层档：每一层一条，最后一条带修饰符（五层套在一起之后）。</summary>
    private static IEnumerable<AchievementDefinition> SleepLayerTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            string id = $"sleep_{index}";
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

    /// <summary>梦境能量档。最后一条带修饰符——梦本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> DreamEnergyTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("energy_2000", 2_000),
            ("energy_300000", 300_000),
            ("energy_20000000", 20_000_000),
            ("energy_600000000", 600_000_000),
            ("energy_2000000000", 2_000_000_000),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(DreamEnergyModule.CounterKey, value),
                Modifiers = id == "energy_2000000000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
