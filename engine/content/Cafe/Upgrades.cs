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
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

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
                string id = $"{building.Id}_tier{required}";
                yield return new UpgradeDefinition
                {
                    Id = id,
                    Name = Prose.Text("upgrades", id, "name"),
                    Icon = Prose.Text("upgrades", id, "icon"),
                    Description = Prose.Text("upgrades", id, "description"),
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
            Name = Prose.Text("upgrades", "steady_hands", "name"),
            Icon = Prose.Text("upgrades", "steady_hands", "icon"),
            Description = Prose.Text("upgrades", "steady_hands", "description"),
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
            Name = Prose.Text("upgrades", "latte_art", "name"),
            Icon = Prose.Text("upgrades", "latte_art", "icon"),
            Description = Prose.Text("upgrades", "latte_art", "description"),
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
            Name = Prose.Text("upgrades", "single_origin", "name"),
            Icon = Prose.Text("upgrades", "single_origin", "icon"),
            Description = Prose.Text("upgrades", "single_origin", "description"),
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
            Name = Prose.Text("upgrades", "hand_drip", "name"),
            Icon = Prose.Text("upgrades", "hand_drip", "icon"),
            Description = Prose.Text("upgrades", "hand_drip", "description"),
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
            Name = Prose.Text("upgrades", "barista_soul", "name"),
            Icon = Prose.Text("upgrades", "barista_soul", "icon"),
            Description = Prose.Text("upgrades", "barista_soul", "description"),
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
            Name = Prose.Text("upgrades", "purr_chorus", "name"),
            Icon = Prose.Text("upgrades", "purr_chorus", "icon"),
            Description = Prose.Text("upgrades", "purr_chorus", "description"),
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
            Name = Prose.Text("upgrades", "purr_symphony", "name"),
            Icon = Prose.Text("upgrades", "purr_symphony", "icon"),
            Description = Prose.Text("upgrades", "purr_symphony", "description"),
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
            Name = Prose.Text("upgrades", "heartbeat_of_world", "name"),
            Icon = Prose.Text("upgrades", "heartbeat_of_world", "icon"),
            Description = Prose.Text("upgrades", "heartbeat_of_world", "description"),
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
            Name = Prose.Text("upgrades", "regulars_list", "name"),
            Icon = Prose.Text("upgrades", "regulars_list", "icon"),
            Description = Prose.Text("upgrades", "regulars_list", "description"),
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
            Name = Prose.Text("upgrades", "secret_recipe", "name"),
            Icon = Prose.Text("upgrades", "secret_recipe", "icon"),
            Description = Prose.Text("upgrades", "secret_recipe", "description"),
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
            Name = Prose.Text("upgrades", "otherworld_supply", "name"),
            Icon = Prose.Text("upgrades", "otherworld_supply", "icon"),
            Description = Prose.Text("upgrades", "otherworld_supply", "description"),
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
            Name = Prose.Text("upgrades", "memory_blend", "name"),
            Icon = Prose.Text("upgrades", "memory_blend", "icon"),
            Description = Prose.Text("upgrades", "memory_blend", "description"),
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
            Name = Prose.Text("upgrades", "warm_light", "name"),
            Icon = Prose.Text("upgrades", "warm_light", "icon"),
            Description = Prose.Text("upgrades", "warm_light", "description"),
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
            Name = Prose.Text("upgrades", "remembers_your_name", "name"),
            Icon = Prose.Text("upgrades", "remembers_your_name", "icon"),
            Description = Prose.Text("upgrades", "remembers_your_name", "description"),
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
            Name = Prose.Text("upgrades", "usual_order", "name"),
            Icon = Prose.Text("upgrades", "usual_order", "icon"),
            Description = Prose.Text("upgrades", "usual_order", "description"),
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
            Name = Prose.Text("upgrades", "table_by_window", "name"),
            Icon = Prose.Text("upgrades", "table_by_window", "icon"),
            Description = Prose.Text("upgrades", "table_by_window", "description"),
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
            Name = Prose.Text("upgrades", "birthday_cake", "name"),
            Icon = Prose.Text("upgrades", "birthday_cake", "icon"),
            Description = Prose.Text("upgrades", "birthday_cake", "description"),
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
            Name = Prose.Text("upgrades", "still_open", "name"),
            Icon = Prose.Text("upgrades", "still_open", "icon"),
            Description = Prose.Text("upgrades", "still_open", "description"),
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
