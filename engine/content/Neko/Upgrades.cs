using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 示例内容包的升级表。<para>
/// 展示了框架支持的四种升级写法：
/// <list type="number">
///   <item><b>批量生成</b>：每座建筑的三个强化档（×2 产量），用循环生成而不是手写 30 条。</item>
///   <item><b>联动成长</b>：用 <see cref="Scaling"/> 让升级效果随另一个建筑的数量增长。</item>
///   <item><b>可重复购买</b>：<c>MaxPurchases</c> + <c>PriceGrowth</c>。</item>
///   <item><b>永久升级</b>：用转生货币购买、转生后保留（天堂升级）。</item>
/// </list>
/// </para>
/// </summary>
internal static class Upgrades
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Buildings.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Buildings.Prose;

    /// <summary>全部升级。</summary>
    public static UpgradeDefinition[] All =>
    [
        .. BuildingTierUpgrades(),
        .. ClickUpgrades(),
        .. SynergyUpgrades(),
        .. MilkUpgrades(),
        .. SpecialUpgrades(),
        .. HeavenlyUpgrades(),
    ];

    /// <summary>
    /// 为每座建筑生成三个强化档：1 个解锁 → ×2，5 个 → ×2，25 个 → ×2。<para>
    /// 价格取建筑的 10 / 100 / 500 倍，与原版一致。这种"内容由代码生成"的写法在
    /// 建筑数量增加到几十座时是唯一能维护下去的方式。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Suffix)[] tiers =
        [
            (1, 10, "更好的"),
            (5, 100, "加倍的"),
            (25, 500, "传奇的"),
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int required, double priceFactor, string suffix) in tiers)
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
                    Tags = ["cat", "tier"],
                };
            }
        }
    }

    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "warmer_hands",
            Name = Prose.Text("upgrades", "warmer_hands", "name"),
            Icon = Prose.Text("upgrades", "warmer_hands", "icon"),
            Description = Prose.Text("upgrades", "warmer_hands", "description"),
            Price = 100,
            Unlock = UnlockCondition.ClicksAtLeast(10),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "plush_gloves",
            Name = Prose.Text("upgrades", "plush_gloves", "name"),
            Icon = Prose.Text("upgrades", "plush_gloves", "icon"),
            Description = Prose.Text("upgrades", "plush_gloves", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "electric_wand",
            Name = Prose.Text("upgrades", "electric_wand", "name"),
            Icon = Prose.Text("upgrades", "electric_wand", "icon"),
            Description = Prose.Text("upgrades", "electric_wand", "description"),
            Price = 500_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(500),
                UnlockCondition.UpgradeOwned("plush_gloves")),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 3,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "laser_pointer",
            Name = Prose.Text("upgrades", "laser_pointer", "name"),
            Icon = Prose.Text("upgrades", "laser_pointer", "icon"),
            Description = Prose.Text("upgrades", "laser_pointer", "description"),
            Price = 50_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(2_000),
                UnlockCondition.UpgradeOwned("electric_wand")),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 4,
            Tags = ["click"],
        };

        yield return new()
        {
            Id = "purring_resonance",
            Name = Prose.Text("upgrades", "purring_resonance", "name"),
            Icon = Prose.Text("upgrades", "purring_resonance", "icon"),
            Description = Prose.Text("upgrades", "purring_resonance", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.UpgradeOwned("laser_pointer")),
            Modifiers = [Modifier.ClickPercent(0.25)],
            Category = "click",
            Tier = 5,
            Tags = ["click"],
        };
    }

    /// <summary>联动成长：效果随另一座建筑的数量线性增长。</summary>
    private static IEnumerable<UpgradeDefinition> SynergyUpgrades()
    {
        yield return new()
        {
            Id = "bed_buddies",
            Name = Prose.Text("upgrades", "bed_buddies", "name"),
            Icon = Prose.Text("upgrades", "bed_buddies", "icon"),
            Description = Prose.Text("upgrades", "bed_buddies", "description"),
            Price = 60_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("cat_bed", 10),
                UnlockCondition.BuildingsAtLeast("curled_cat", 10)),
            Modifiers =
            [
                new Modifier(
                    ModifierTarget.BuildingCps("cat_bed"),
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 100, Id: "curled_cat")),
            ],
            Category = "synergy",
            Tier = 2,
            Tags = ["synergy"],
        };

        yield return new()
        {
            Id = "cafe_supply_chain",
            Name = Prose.Text("upgrades", "cafe_supply_chain", "name"),
            Icon = Prose.Text("upgrades", "cafe_supply_chain", "icon"),
            Description = Prose.Text("upgrades", "cafe_supply_chain", "description"),
            Price = 8_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("cat_cafe", 15),
                UnlockCondition.BuildingsAtLeast("auto_feeder", 15)),
            Modifiers =
            [
                new Modifier(
                    ModifierTarget.BuildingCps("cat_cafe"),
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 200, Id: "auto_feeder")),
            ],
            Category = "synergy",
            Tier = 3,
            Tags = ["synergy"],
        };

        yield return new()
        {
            Id = "catnip_addiction",
            Name = Prose.Text("upgrades", "catnip_addiction", "name"),
            Icon = Prose.Text("upgrades", "catnip_addiction", "icon"),
            Description = Prose.Text("upgrades", "catnip_addiction", "description"),
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("catnip_field", 20),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.BuildingCount, 0.005, Cap: 150, Id: "catnip_field")),
            ],
            Category = "synergy",
            Tier = 4,
            Tags = ["synergy"],
        };
    }

    /// <summary>
    /// "牛奶"式升级：效果随成就数量增长。<para>
    /// 这是让成就<b>有实际意义</b>的关键设计——成就本身不给数值，但通过这组升级
    /// 转成全局倍率，于是"多解锁一个成就"永远是有价值的。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> MilkUpgrades()
    {
        yield return new()
        {
            Id = "kitten_helpers",
            Name = Prose.Text("upgrades", "kitten_helpers", "name"),
            Icon = Prose.Text("upgrades", "kitten_helpers", "icon"),
            Description = Prose.Text("upgrades", "kitten_helpers", "description"),
            Price = 9_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(5),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.01))],
            Category = "kitten",
            Tier = 1,
            Tags = ["kitten"],
        };

        yield return new()
        {
            Id = "kitten_workers",
            Name = Prose.Text("upgrades", "kitten_workers", "name"),
            Icon = Prose.Text("upgrades", "kitten_workers", "icon"),
            Description = Prose.Text("upgrades", "kitten_workers", "description"),
            Price = 90_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(20),
                UnlockCondition.UpgradeOwned("kitten_helpers")),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.02)),
                Modifier.ClickMultiplier(1.5),
            ],
            Category = "kitten",
            Tier = 2,
            Tags = ["kitten"],
        };

        yield return new()
        {
            Id = "kitten_managers",
            Name = Prose.Text("upgrades", "kitten_managers", "name"),
            Icon = Prose.Text("upgrades", "kitten_managers", "icon"),
            Description = Prose.Text("upgrades", "kitten_managers", "description"),
            Price = 9_000_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(40),
                UnlockCondition.UpgradeOwned("kitten_workers")),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.03))],
            Category = "kitten",
            Tier = 3,
            Tags = ["kitten"],
        };
    }

    private static IEnumerable<UpgradeDefinition> SpecialUpgrades()
    {
        yield return new()
        {
            Id = "bulk_kibble_coupon",
            Name = Prose.Text("upgrades", "bulk_kibble_coupon", "name"),
            Icon = Prose.Text("upgrades", "bulk_kibble_coupon", "icon"),
            Description = Prose.Text("upgrades", "bulk_kibble_coupon", "description"),
            Price = 3_000_000,
            MaxPurchases = 10,
            PriceGrowth = 2.5,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "special",
            Tier = 2,
            Tags = ["bulk"],
        };

        yield return new()
        {
            Id = "wholesale_haggling",
            Name = Prose.Text("upgrades", "wholesale_haggling", "name"),
            Icon = Prose.Text("upgrades", "wholesale_haggling", "icon"),
            Description = Prose.Text("upgrades", "wholesale_haggling", "description"),
            Price = 1_000_000_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(100_000_000),
            Modifiers = [Modifier.PriceDiscount(0.05)],
            Category = "special",
            Tier = 3,
            Tags = ["economy"],
        };

        yield return new()
        {
            Id = "lucky_charm",
            Name = Prose.Text("upgrades", "lucky_charm", "name"),
            Icon = Prose.Text("upgrades", "lucky_charm", "icon"),
            Description = Prose.Text("upgrades", "lucky_charm", "description"),
            Price = 7_777_777,
            Unlock = UnlockCondition.GoldenCookiesAtLeast(3),
            Modifiers =
            [
                Modifier.GoldenCookieReward(1.25),
                Modifier.GoldenCookieFrequency(1.2),
            ],
            Category = "special",
            Tier = 2,
            Tags = ["golden"],
        };
    }

    /// <summary>天堂升级：用猫薄荷（转生货币）购买，转生后保留。</summary>
    private static IEnumerable<UpgradeDefinition> HeavenlyUpgrades()
    {
        yield return new()
        {
            Id = "eternal_paw",
            Name = Prose.Text("upgrades", "eternal_paw", "name"),
            Icon = Prose.Text("upgrades", "eternal_paw", "icon"),
            Description = Prose.Text("upgrades", "eternal_paw", "description"),
            Price = 3,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(3),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "heavenly",
            Tier = 1,
            Tags = ["heavenly"],
        };

        yield return new()
        {
            Id = "cat_god_favor",
            Name = Prose.Text("upgrades", "cat_god_favor", "name"),
            Icon = Prose.Text("upgrades", "cat_god_favor", "icon"),
            Description = Prose.Text("upgrades", "cat_god_favor", "description"),
            Price = 8,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(8),
            Modifiers = [Modifier.PriceDiscount(0.10)],
            Category = "heavenly",
            Tier = 1,
            Tags = ["heavenly"],
        };

        yield return new()
        {
            Id = "angel_cat",
            Name = Prose.Text("upgrades", "angel_cat", "name"),
            Icon = Prose.Text("upgrades", "angel_cat", "icon"),
            Description = Prose.Text("upgrades", "angel_cat", "description"),
            Price = 12,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(12),
            Modifiers =
            [
                new Modifier(ModifierTarget.OfflineEfficiency, ModifierOperation.Multiplicative, 1.5),
            ],
            Category = "heavenly",
            Tier = 2,
            Tags = ["heavenly"],
        };

        yield return new()
        {
            Id = "time_lord_cat",
            Name = Prose.Text("upgrades", "time_lord_cat", "name"),
            Icon = Prose.Text("upgrades", "time_lord_cat", "icon"),
            Description = Prose.Text("upgrades", "time_lord_cat", "description"),
            Price = 30,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(30),
            Modifiers = [Modifier.GlobalMultiplier(1.15)],
            Category = "heavenly",
            Tier = 2,
            Tags = ["heavenly"],
        };

        yield return new()
        {
            Id = "golden_whiskers",
            Name = Prose.Text("upgrades", "golden_whiskers", "name"),
            Icon = Prose.Text("upgrades", "golden_whiskers", "icon"),
            Description = Prose.Text("upgrades", "golden_whiskers", "description"),
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
            Tier = 2,
            Tags = ["heavenly"],
        };
    }
}
