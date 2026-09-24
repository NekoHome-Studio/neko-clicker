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
        (double Amount, string Id, string Name, string Icon, string Flavor)[] tiers =
        [
            (1e3, "earned_1e3", "第一笔流水", "🧾", "收银盒第一次装不下硬币。"),
            (1e6, "earned_1e6", "六位数的账本", "💵", "你换了本更厚的账本，还是很快写满了。"),
            (1e9, "earned_1e9", "一个亿的小目标", "🧮", "算盘换成了计算器。"),
            (1e12, "earned_1e12", "兆级咖啡馆", "🏦", "银行经理开始亲自来喝咖啡。"),
            (1e15, "earned_1e15", "千兆流水", "🌠", "账本上的数字开始不像钱，像天文。"),
            (1e18, "earned_1e18", "百京级账本", "🌌", "你已经不再数零，只数页数。"),
            (1e21, "earned_1e21", "泽它级流水", "🪐", "会计事务所派来了一个团队。"),
            (1e24, "earned_1e24", "尧它级的传说", "👑", "这条街的名字，后来跟着这家店一起被写进了地图。"),
        ];

        foreach ((double amount, string id, string name, string icon, string flavor) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = flavor,
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
                Tier = (int)Math.Log10(amount) / 3,
            };
        }
    }

    /// <summary>每秒产量：3 档。产量类条件适合做"上了新台阶"的确认感。</summary>
    private static IEnumerable<AchievementDefinition> CpsAchievements()
    {
        (double Amount, string Id, string Name, string Icon)[] tiers =
        [
            (1e6, "cps_1e6", "每秒一百万", "⚡"),
            (1e9, "cps_1e9", "每秒十亿", "🚀"),
            (1e12, "cps_1e12", "每秒一兆", "🌠"),
        ];

        foreach ((double amount, string id, string name, string icon) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = $"每秒产量达到 {amount:0e0}。",
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
            yield return new()
            {
                Id = $"{building.Id}_x1",
                Name = $"添置{building.Name}",
                Icon = building.Icon,
                Description = $"拥有 1 座「{building.Name}」。",
                Unlock = UnlockCondition.BuildingsAtLeast(building.Id, 1),
                Category = "building",
                Tier = 1,
            };

            yield return new()
            {
                Id = $"{building.Id}_x25",
                Name = $"{building.Name}收藏家",
                Icon = building.Icon,
                Description = $"拥有 25 座「{building.Name}」。",
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
            Name = "第一百杯",
            Icon = "☕",
            Description = "手已经比脑子先记住动作了。",
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Category = "click",
            Tier = 1,
        };

        yield return new()
        {
            Id = "click_1000",
            Name = "第一千杯",
            Icon = "☕",
            Description = "有客人问你是不是从来不休息。",
            Unlock = UnlockCondition.ClicksAtLeast(1_000),
            Category = "click",
            Tier = 2,
        };

        yield return new()
        {
            Id = "click_10000",
            Name = "万次手冲",
            Icon = "☄️",
            Description = "手腕形成了肌肉记忆。点击收益 ×1.5。",
            Unlock = UnlockCondition.ClicksAtLeast(10_000),
            Modifiers = [Modifier.ClickMultiplier(1.5)],
            Category = "click",
            Tier = 3,
        };

        yield return new()
        {
            Id = "click_100000",
            Name = "十万杯",
            Icon = "🏆",
            Description = "有人开始专程来看你冲咖啡。",
            Unlock = UnlockCondition.ClicksAtLeast(100_000),
            Category = "click",
            Tier = 4,
        };
    }

    /// <summary>客人（金猫）：4 档。「走错门的客人」是本题材下对随机事件的称呼。</summary>
    private static IEnumerable<AchievementDefinition> GuestAchievements()
    {
        (double Count, string Id, string Name, string Icon, string Flavor)[] tiers =
        [
            (1, "guest_1", "第一位走错门的客人", "🌟", "他说他是找别的地方，但坐下就没走。"),
            (7, "guest_7", "第七位客人", "✨", "你开始怀疑这条街的地图印错了。"),
            (27, "guest_27", "二十七位常客", "🌠", "有人带着朋友来，朋友又带着朋友来。"),
            (77, "guest_77", "门口排起了队", "🎇", "队伍拐过街角，没人知道它在排什么。"),
        ];

        foreach ((double count, string id, string name, string icon, string flavor) in tiers)
        {
            yield return new()
            {
                Id = id,
                Name = name,
                Icon = icon,
                Description = flavor,
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
            Name = "暖意",
            Icon = "🌤️",
            Description = "有人的肩膀放松下来了。",
            Unlock = UnlockCondition.Counter(HappinessModule.CounterKey, 500),
            Category = "happiness",
            Tier = 1,
        };

        yield return new()
        {
            Id = "happiness_5000",
            Name = "满座",
            Icon = "🫧",
            Description = "没有一张空桌子，但没有人显得着急。",
            Unlock = UnlockCondition.Counter(HappinessModule.CounterKey, 5_000),
            Category = "happiness",
            Tier = 2,
        };

        yield return new()
        {
            Id = "happiness_50000",
            Name = "？？？",
            Icon = "❔",
            Description = "店里安静得能听见所有人心跳对上了拍。",
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
            Name = "第一次店休",
            Icon = "📪",
            Description = "推倒重来的时候，你发现有人记得这里。点击收益 ×1.2。",
            Unlock = UnlockCondition.PrestigeChipsAtLeast(1),
            Modifiers = [Modifier.ClickMultiplier(1.2)],
            Category = "regular",
            Tier = 1,
        };

        yield return new()
        {
            Id = "regular_10",
            Name = "第十封信",
            Icon = "💌",
            Description = "信箱里躺着十封字迹不同的信。",
            Unlock = UnlockCondition.PrestigeChipsAtLeast(10),
            Category = "regular",
            Tier = 2,
        };

        yield return new()
        {
            Id = "regular_100",
            Name = "一百封信",
            Icon = "📮",
            Description = "你把它们按时间顺序排好，发现最早那封没有署名。",
            Unlock = UnlockCondition.PrestigeChipsAtLeast(100),
            Category = "regular",
            Tier = 3,
        };
    }
}
