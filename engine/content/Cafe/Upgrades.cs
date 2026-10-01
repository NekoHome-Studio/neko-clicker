using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的升级表（48 条）。<para>
/// 结构：建筑强化档 30 + 点击线 5 + 呼噜线 3 + 特色线 5 + 常客记忆 5。<br/>
/// 其中「常客记忆」是转生语义的载体：用「常客的信」购买、<c>Permanent</c> 保留——
/// 玩家保留下来的不是"永久升级"，而是"某个客人还记得你"。
/// </para>
/// </summary>
internal static class Upgrades
{
    /// <summary>全部升级。</summary>
    public static UpgradeDefinition[] All =>
    [
        .. BuildingTierUpgrades(),
        .. ClickUpgrades(),
        .. PurrUpgrades(),
        .. SignatureUpgrades(),
        .. RegularMemoryUpgrades(),
    ];

    /// <summary>每座建筑三档强化（1 / 5 / 25 个 → ×2），价格取建筑基础价的 10 / 100 / 500 倍。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix, string Flavor)[] tiers =
        [
            (1, 10, "熟能生巧的", "做得多了，手就记得了。"),
            (5, 100, "小有名气的", "有人专程为了它推门进来。"),
            (25, 500, "招牌级的", "整条街提起这家店，先提起它。"),
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int required, double priceFactor, string prefix, string flavor) in tiers)
            {
                yield return new UpgradeDefinition
                {
                    Id = $"{building.Id}_tier{required}",
                    Name = $"{prefix}{building.Name}",
                    Icon = building.Icon,
                    Description = $"{flavor}「{building.Name}」的产量翻倍。",
                    Price = building.BasePrice * priceFactor,
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, required),
                    Modifiers = [Modifier.BuildingMultiplier(building.Id, 2)],
                    Category = $"building:{building.Id}",
                    Tier = required,
                    Tags = ["tier"],
                };
            }
        }
    }

    /// <summary>点击线：把"亲手做一杯"变成一条能跟得上后期的收益曲线。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "steady_hands",
            Name = "稳定的手",
            Icon = "🖐️",
            Description = "手腕不再抖了。每次做咖啡额外获得 1 条小鱼干。",
            Price = 100,
            Unlock = UnlockCondition.ClicksAtLeast(10),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "latte_art",
            Name = "拉花艺术",
            Icon = "🎨",
            Description = "把叶子画进奶泡里。点击收益 ×2。",
            Price = 5_000,
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "single_origin",
            Name = "单品豆",
            Icon = "🫘",
            Description = "只进一个产季、一个产地的豆子。点击收益 ×2。",
            Price = 500_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(500),
                UnlockCondition.UpgradeOwned("latte_art")),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 3,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "hand_drip",
            Name = "手冲技法",
            Icon = "🫖",
            Description = "水柱细而稳，一圈一圈把香气叫醒。点击收益 ×3。",
            Price = 50_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(2_000),
                UnlockCondition.UpgradeOwned("single_origin")),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 4,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "barista_soul",
            Name = "咖啡师之魂",
            Icon = "🔥",
            Description = "你终于明白，做咖啡是替别人留出一段时间。点击收益 +25%。",
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.UpgradeOwned("hand_drip")),
            Modifiers = [Modifier.ClickPercent(0.25)],
            Category = "click",
            Tier = 5,
            Tags = ["click"],
        };
    }

    /// <summary>呼噜线：按成就数量给全局加成——追求任何目标都会顺带变成产量。</summary>
    private static IEnumerable<UpgradeDefinition> PurrUpgrades()
    {
        yield return new()
        {
            Id = "purr_chorus",
            Name = "呼噜合唱",
            Icon = "🎶",
            Description = "三只猫同时打呼噜时，你发现吧台在跟着震。每个成就让全部产量 +1%。",
            Price = 9_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(5),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.01))],
            Category = "purr",
            Tier = 1,
            Tags = ["purr"],
        };

        yield return new()
        {
            Id = "purr_symphony",
            Name = "呼噜交响",
            Icon = "🎻",
            Description = "猫多了，呼噜声就有了声部。每个成就让全部产量 +2%，点击收益 ×1.5。",
            Price = 90_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(20),
                UnlockCondition.UpgradeOwned("purr_chorus")),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.02)),
                Modifier.ClickMultiplier(1.5),
            ],
            Category = "purr",
            Tier = 2,
            Tags = ["purr"],
        };

        yield return new()
        {
            Id = "heartbeat_of_world",
            Name = "世界心跳",
            Icon = "💗",
            Description = "店安静下来的时候，你能听见它和另一种心跳对上了拍。每个成就让全部产量 +3%。",
            Price = 9_000_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(40),
                UnlockCondition.UpgradeOwned("purr_symphony")),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.03))],
            Category = "purr",
            Tier = 3,
            Tags = ["purr"],
        };
    }

    /// <summary>特色线：把第二资源、联动成长、随机事件与"手气"接到产量上。</summary>
    private static IEnumerable<UpgradeDefinition> SignatureUpgrades()
    {
        yield return new()
        {
            Id = "regulars_list",
            Name = "常客名单",
            Icon = "📒",
            Description = "开始有人固定坐同一张桌子。全部建筑产量 ×1.4。",
            Price = 60_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("cat_tree", 10),
                UnlockCondition.Counter(HappinessModule.CounterKey, 500)),
            Modifiers = [Modifier.GlobalMultiplier(1.4)],
            Category = "signature",
            Tier = 1,
            Tags = ["signature"],
        };

        yield return new()
        {
            Id = "secret_recipe",
            Name = "私藏配方",
            Icon = "📜",
            Description = "写在杯垫背面的配方，越翻越厚。每座「猫爬架」让全部产量 +1%（最多 200 座）。",
            Price = 8_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("upstairs", 15),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 200, Id: "cat_tree")),
            ],
            Category = "signature",
            Tier = 2,
            Tags = ["signature"],
        };

        yield return new()
        {
            Id = "otherworld_supply",
            Name = "异世界供货",
            Icon = "🚚",
            Description = "货车半夜来卸货，纸箱上没有一个字是朝上的。「异世界门」产量 ×3。",
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("otherworld_door", 20),
            Modifiers = [Modifier.BuildingMultiplier("otherworld_door", 3)],
            Category = "signature",
            Tier = 3,
            Tags = ["signature"],
        };

        yield return new()
        {
            Id = "memory_blend",
            Name = "记忆拼配",
            Icon = "🥣",
            Description = "把常客记得的味道配成一支豆子。每项已购升级让全部产量 +2%（最多 50 项）。",
            Price = 1_000_000_000_000,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(3),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.PurchasedUpgrades, 0.02, Cap: 50))],
            Category = "signature",
            Tier = 4,
            Tags = ["signature"],
        };

        yield return new()
        {
            Id = "warm_light",
            Name = "暖光",
            Icon = "💡",
            Description = "把顶灯换成暖色之后，走错门的客人变多了。客人奖励 ×1.25、出现频率 ×1.2。",
            Price = 7_777_777,
            Unlock = UnlockCondition.GoldenCookiesAtLeast(3),
            Modifiers =
            [
                Modifier.GoldenCookieReward(1.25),
                Modifier.GoldenCookieFrequency(1.2),
            ],
            Category = "signature",
            Tier = 5,
            Tags = ["signature"],
        };
    }

    /// <summary>
    /// 常客记忆：转生后保留，且<strong>只能用「常客的信」购买</strong>。
    /// <c>GameContentBuilder</c> 会强制这一条（Permanent 升级用普通货币会构建期报错），
    /// 于是"记忆只能用信来换"是硬规则而不是文案。
    /// </summary>
    private static IEnumerable<UpgradeDefinition> RegularMemoryUpgrades()
    {
        yield return new()
        {
            Id = "remembers_your_name",
            Name = "他记得你的名字",
            Icon = "💌",
            Description = "店休回来第一天，他隔着吧台叫出了你的名字。点击收益 ×3。",
            Price = 3,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(3),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "heavenly",
            Tier = 1,
            Tags = ["memory"],
        };

        yield return new()
        {
            Id = "usual_order",
            Name = "老样子",
            Icon = "☕",
            Description = "他不用看菜单。全部建筑价格 −10%。",
            Price = 8,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(8),
            Modifiers = [Modifier.PriceDiscount(0.10)],
            Category = "heavenly",
            Tier = 2,
            Tags = ["memory"],
        };

        yield return new()
        {
            Id = "table_by_window",
            Name = "窗边那张桌子",
            Icon = "🪟",
            Description = "他说那是他太太以前最喜欢的位置。离线收益效率 ×1.5。",
            Price = 12,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(12),
            Modifiers = [new Modifier(ModifierTarget.OfflineEfficiency, ModifierOperation.Multiplicative, 1.5)],
            Category = "heavenly",
            Tier = 3,
            Tags = ["memory"],
        };

        yield return new()
        {
            Id = "birthday_cake",
            Name = "生日蛋糕",
            Icon = "🎂",
            Description = "他不知道自己的生日，就把遇到你的那天定成了生日。客人出现频率 ×1.5、停留时间 ×1.5。",
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(20),
            Modifiers =
            [
                Modifier.GoldenCookieFrequency(1.5),
                new Modifier(ModifierTarget.GoldenCookieDuration, ModifierOperation.Multiplicative, 1.5),
            ],
            Category = "heavenly",
            Tier = 4,
            Tags = ["memory"],
        };

        yield return new()
        {
            Id = "still_open",
            Name = "还开着啊",
            Icon = "🔑",
            Description = "他推门进来，只说了这四个字。全部建筑产量 ×1.15。",
            Price = 30,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(30),
            Modifiers = [Modifier.GlobalMultiplier(1.15)],
            Category = "heavenly",
            Tier = 5,
            Tags = ["memory"],
        };
    }
}
