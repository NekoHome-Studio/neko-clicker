using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的成就表（45 条，7 类）。<para>
/// 成就本身大多不给数值，而是驱动「呼噜线」升级（按成就数量换全局加成）——
/// 玩家追求任何目标都会顺带解锁成就，成就又立刻变成实打实的产量。
/// 两条成就直接给修饰符，用来演示另一条路径（见 PACK_01 §6）。
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
        .. EarnedAchievements(),
        .. CpsAchievements(),
        .. BuildingAchievements(),
        .. ClickAchievements(),
        .. GuestAchievements(),
        .. HappinessAchievements(),
        .. RegularAchievements(),
    ];

    /// <summary>累计赚取：8 档，覆盖到 1e24（后期的主要内容节奏）。</summary>
    private static IEnumerable<AchievementDefinition> EarnedAchievements()
    {
        (double Amount, string Id)[] tiers =
        [
            (1e3, "earned_1e3"),
            (1e6, "earned_1e6"),
            (1e9, "earned_1e9"),
            (1e12, "earned_1e12"),
            (1e15, "earned_1e15"),
            (1e18, "earned_1e18"),
            (1e21, "earned_1e21"),
            (1e24, "earned_1e24"),
        ];

        foreach ((double amount, string id) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
                Tier = (int)Math.Log10(amount) / 3,
            };
        }
    }

    /// <summary>每秒产量：3 档。产量类条件适合做"上了新台阶"的确认感。</summary>
    private static IEnumerable<AchievementDefinition> CpsAchievements()
    {
        (double Amount, string Id)[] tiers =
        [
            (1e6, "cps_1e6"),
            (1e9, "cps_1e9"),
            (1e12, "cps_1e12"),
        ];

        foreach ((double amount, string id) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.CpsAtLeast(amount),
                Category = "progress",
                Tier = (int)Math.Log10(amount) / 3,
            };
        }
    }

    /// <summary>建筑档位：每座建筑 1 座 / 25 座两档，共 20 条（控制总量，第 3 档留给后续扩展）。</summary>
    private static IEnumerable<AchievementDefinition> BuildingAchievements()
    {
        foreach (BuildingDefinition building in Buildings.All)
        {
            string id = $"{building.Id}_x1";
            yield return new()
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.BuildingsAtLeast(building.Id, 1),
                Category = "building",
                Tier = 1,
            };

            string id2 = $"{building.Id}_x25";
            yield return new()
            {
                Id = id2,
                Name = Prose.Text("achievements", id2, "name"),
                Icon = Prose.Text("achievements", id2, "icon"),
                Description = Prose.Text("achievements", id2, "description"),
                Unlock = UnlockCondition.BuildingsAtLeast(building.Id, 25),
                Category = "building",
                Tier = 2,
            };
        }
    }

    /// <summary>点击线：亲手做咖啡的四个台阶；「万次手冲」直接给点击倍率。</summary>
    private static IEnumerable<AchievementDefinition> ClickAchievements()
    {
        yield return new()
        {
            Id = "click_100",
            Name = Prose.Text("achievements", "click_100", "name"),
            Icon = Prose.Text("achievements", "click_100", "icon"),
            Description = Prose.Text("achievements", "click_100", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Category = "click",
            Tier = 1,
        };

        yield return new()
        {
            Id = "click_1000",
            Name = Prose.Text("achievements", "click_1000", "name"),
            Icon = Prose.Text("achievements", "click_1000", "icon"),
            Description = Prose.Text("achievements", "click_1000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(1_000),
            Category = "click",
            Tier = 2,
        };

        yield return new()
        {
            Id = "click_10000",
            Name = Prose.Text("achievements", "click_10000", "name"),
            Icon = Prose.Text("achievements", "click_10000", "icon"),
            Description = Prose.Text("achievements", "click_10000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(10_000),
            Modifiers = [Modifier.ClickMultiplier(1.5)],
            Category = "click",
            Tier = 3,
        };

        yield return new()
        {
            Id = "click_100000",
            Name = Prose.Text("achievements", "click_100000", "name"),
            Icon = Prose.Text("achievements", "click_100000", "icon"),
            Description = Prose.Text("achievements", "click_100000", "description"),
            Unlock = UnlockCondition.ClicksAtLeast(100_000),
            Category = "click",
            Tier = 4,
        };
    }

    /// <summary>客人（金猫）：4 档。「走错门的客人」是本题材下对随机事件的称呼。</summary>
    private static IEnumerable<AchievementDefinition> GuestAchievements()
    {
        (double Count, string Id)[] tiers =
        [
            (1, "guest_1"),
            (7, "guest_7"),
            (27, "guest_27"),
            (77, "guest_77"),
        ];

        foreach ((double count, string id) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
                Category = "guest",
                Tier = (int)Math.Log10(count) + 1,
            };
        }
    }

    /// <summary>幸福感（第二资源）：3 档，最后一档隐藏。</summary>
    private static IEnumerable<AchievementDefinition> HappinessAchievements()
    {
        yield return new()
        {
            Id = "happiness_500",
            Name = Prose.Text("achievements", "happiness_500", "name"),
            Icon = Prose.Text("achievements", "happiness_500", "icon"),
            Description = Prose.Text("achievements", "happiness_500", "description"),
            Unlock = UnlockCondition.Counter(HappinessModule.CounterKey, 500),
            Category = "happiness",
            Tier = 1,
        };

        yield return new()
        {
            Id = "happiness_5000",
            Name = Prose.Text("achievements", "happiness_5000", "name"),
            Icon = Prose.Text("achievements", "happiness_5000", "icon"),
            Description = Prose.Text("achievements", "happiness_5000", "description"),
            Unlock = UnlockCondition.Counter(HappinessModule.CounterKey, 5_000),
            Category = "happiness",
            Tier = 2,
        };

        yield return new()
        {
            Id = "happiness_50000",
            Name = Prose.Text("achievements", "happiness_50000", "name"),
            Icon = Prose.Text("achievements", "happiness_50000", "icon"),
            Description = Prose.Text("achievements", "happiness_50000", "description"),
            Unlock = UnlockCondition.Counter(HappinessModule.CounterKey, 50_000),
            Hidden = true,
            Category = "happiness",
            Tier = 3,
        };
    }

    /// <summary>常客的信（转生货币）：3 档；「第一次店休」直接给点击倍率。</summary>
    private static IEnumerable<AchievementDefinition> RegularAchievements()
    {
        yield return new()
        {
            Id = "regular_1",
            Name = Prose.Text("achievements", "regular_1", "name"),
            Icon = Prose.Text("achievements", "regular_1", "icon"),
            Description = Prose.Text("achievements", "regular_1", "description"),
            Unlock = UnlockCondition.PrestigeChipsAtLeast(1),
            Modifiers = [Modifier.ClickMultiplier(1.2)],
            Category = "regular",
            Tier = 1,
        };

        yield return new()
        {
            Id = "regular_10",
            Name = Prose.Text("achievements", "regular_10", "name"),
            Icon = Prose.Text("achievements", "regular_10", "icon"),
            Description = Prose.Text("achievements", "regular_10", "description"),
            Unlock = UnlockCondition.PrestigeChipsAtLeast(10),
            Category = "regular",
            Tier = 2,
        };

        yield return new()
        {
            Id = "regular_100",
            Name = Prose.Text("achievements", "regular_100", "name"),
            Icon = Prose.Text("achievements", "regular_100", "icon"),
            Description = Prose.Text("achievements", "regular_100", "description"),
            Unlock = UnlockCondition.PrestigeChipsAtLeast(100),
            Category = "regular",
            Tier = 3,
        };
    }
}
