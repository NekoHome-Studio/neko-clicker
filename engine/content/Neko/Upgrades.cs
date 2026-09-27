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
                yield return new UpgradeDefinition
                {
                    Id = $"{building.Id}_tier{required}",
                    Name = $"{suffix}{building.Name}",
                    Icon = building.Icon,
                    Description = $"「{building.Name}」的产量翻倍。",
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
            Name = "更暖的手",
            Icon = "🖐️",
            Description = "每次撸猫额外获得 1 条小鱼干。",
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
            Name = "毛绒手套",
            Icon = "🧤",
            Description = "点击收益 ×2。猫更喜欢这种触感。",
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
            Name = "电动逗猫棒",
            Icon = "🪄",
            Description = "点击收益 ×2，并且每秒自动撸猫一次的效果（体现在点击收益基数上）。",
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
            Name = "激光笔",
            Icon = "🔴",
            Description = "点击收益 ×3。没有任何猫能抵抗这个红点。",
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
            Name = "呼噜共振",
            Icon = "💤",
            Description = "点击收益 +25%。猫的呼噜声据说能治愈一切，包括你的钱包。",
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
            Name = "猫窝里的小猫",
            Icon = "🐾",
            Description = "每个「蜷缩的猫」让「猫窝」产量 +2%（上限 100 个）。",
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
            Name = "咖啡馆供应链",
            Icon = "🚚",
            Description = "每 1 个「自动喂食器」让「猫咪咖啡馆」产量 +1%（上限 200 个）。",
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
            Name = "越吸越上头",
            Icon = "😻",
            Description = "每座「猫薄荷田」让所有建筑产量 +0.5%（上限 150 座）。",
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
            Name = "小猫帮手",
            Icon = "🐱",
            Description = "每个成就让所有建筑产量 +1%。",
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
            Name = "小猫工人",
            Icon = "😼",
            Description = "每个成就让所有建筑产量 +2%，并且点击收益 ×1.5。",
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
            Name = "小猫经理",
            Icon = "😾",
            Description = "每个成就让所有建筑产量 +3%。它开始管你要 KPI 了。",
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
            Name = "猫粮批发券",
            Icon = "🎟️",
            Description = "所有建筑产量 ×1.5。可以囤 10 张，每张涨价 2.5 倍。",
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
            Name = "砍价高手",
            Icon = "💬",
            Description = "所有建筑价格 -5%。猫在柜台后面看着你，表情复杂。",
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
            Name = "幸运猫爪",
            Icon = "🍀",
            Description = "金猫奖励 ×1.25，出现频率 ×1.2。",
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
            Name = "永恒肉垫",
            Icon = "🐾",
            Description = "点击收益 ×3。转生后依然有效。",
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
            Name = "猫神眷顾",
            Icon = "🙏",
            Description = "所有建筑价格 -10%。转生后依然有效。",
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
            Name = "天使猫",
            Icon = "😇",
            Description = "离线收益效率 +50%。你睡觉时它替你值班。",
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
            Name = "时间领主猫",
            Icon = "⌛",
            Description = "所有建筑产量 ×1.15。转生后依然有效。",
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
            Name = "黄金胡须",
            Icon = "✨",
            Description = "金猫出现频率 ×1.5、停留时间 ×1.5。转生后依然有效。",
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
