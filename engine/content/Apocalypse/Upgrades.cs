using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 升级表（47 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、按记忆残片成长的"记忆"线、
/// 用建筑数量成长的"连起来"线、每次重启专属的"纪元"线，以及用火种购买并跨重启保留的"余烬"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对全部包同时成立。
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
        .. MemoryUpgrades(),
        .. LinkUpgrades(),
        .. EraUpgrades(),
        .. EmberUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "清出来的"),
            (10, 100, "连成片的"),
            (25, 500, "能自给的"),
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int required, double priceFactor, string prefix) in tiers)
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
                    Tags = ["apocalypse", "tier"],
                };
            }
        }
    }

    /// <summary>点击线：末世里手是最靠得住的工具，所以这条线比别的包长一点。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "scavenger_hands",
            Name = Prose.Text("upgrades", "scavenger_hands", "name"),
            Icon = Prose.Text("upgrades", "scavenger_hands", "icon"),
            Description = Prose.Text("upgrades", "scavenger_hands", "description"),
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["apocalypse", "click"],
        };

        yield return new()
        {
            Id = "metal_detector",
            Name = Prose.Text("upgrades", "metal_detector", "name"),
            Icon = Prose.Text("upgrades", "metal_detector", "icon"),
            Description = Prose.Text("upgrades", "metal_detector", "description"),
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["apocalypse", "click"],
        };

        yield return new()
        {
            Id = "drone_eye",
            Name = Prose.Text("upgrades", "drone_eye", "name"),
            Icon = Prose.Text("upgrades", "drone_eye", "icon"),
            Description = Prose.Text("upgrades", "drone_eye", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["apocalypse", "click"],
        };

        yield return new()
        {
            Id = "ruin_sense",
            Name = Prose.Text("upgrades", "ruin_sense", "name"),
            Icon = Prose.Text("upgrades", "ruin_sense", "icon"),
            Description = Prose.Text("upgrades", "ruin_sense", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(10_000)],
            Category = "click",
            Tier = 4,
            Tags = ["apocalypse", "click"],
        };
    }

    /// <summary>
    /// 记忆线：由「记忆残片」驱动。<para>
    /// 这是第二资源真正参与数值的地方——不是拿它买东西，而是<b>让"记得住"变成产能</b>。
    /// 门槛刻意压在几个不大的数上：残片的产率取决于上一次重启留下多少，所以它天然是后期资源，
    /// 但第一条必须在第 2 次重启里就能摸到，否则这条线就只是图鉴上的装饰。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> MemoryUpgrades()
    {
        yield return new()
        {
            Id = "shard_lens",
            Name = Prose.Text("upgrades", "shard_lens", "name"),
            Icon = Prose.Text("upgrades", "shard_lens", "icon"),
            Description = Prose.Text("upgrades", "shard_lens", "description"),
            Price = 8_000_000,
            Unlock = UnlockCondition.Counter(ShardsModule.CounterKey, 200),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.0005, Cap: 1_500, Id: ShardsModule.CounterKey)),
            ],
            Category = "memory",
            Tier = 1,
            Tags = ["apocalypse", "memory"],
        };

        yield return new()
        {
            Id = "memory_weave",
            Name = Prose.Text("upgrades", "memory_weave", "name"),
            Icon = Prose.Text("upgrades", "memory_weave", "icon"),
            Description = Prose.Text("upgrades", "memory_weave", "description"),
            Price = 400_000_000,
            Unlock = UnlockCondition.Counter(ShardsModule.CounterKey, 2_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.3),
            ],
            Category = "memory",
            Tier = 2,
            Tags = ["apocalypse", "memory"],
        };

        yield return new()
        {
            Id = "inherited_voice",
            Name = Prose.Text("upgrades", "inherited_voice", "name"),
            Icon = Prose.Text("upgrades", "inherited_voice", "icon"),
            Description = Prose.Text("upgrades", "inherited_voice", "description"),
            Price = 3e10,
            Unlock = UnlockCondition.Counter(ShardsModule.CounterKey, 20_000),
            Modifiers = [Modifier.GlobalMultiplier(2.5), Modifier.GoldenCookieReward(2)],
            Category = "memory",
            Tier = 3,
            Tags = ["apocalypse", "memory"],
        };
    }

    /// <summary>
    /// 连起来：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 末世的建筑线本来是一条线性清单，这三条把清单变成网：废墟喂数据塔、水喂温室、
    /// 堆喂城。它们也是唯一让"早点铺量"在数值上直接有回报的线。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> LinkUpgrades()
    {
        yield return new()
        {
            Id = "ruins_to_tower",
            Name = Prose.Text("upgrades", "ruins_to_tower", "name"),
            Icon = Prose.Text("upgrades", "ruins_to_tower", "icon"),
            Description = Prose.Text("upgrades", "ruins_to_tower", "description"),
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("ruins", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "data_tower",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 100, Id: "ruins")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["apocalypse", "link"],
        };

        yield return new()
        {
            Id = "water_to_greenhouse",
            Name = Prose.Text("upgrades", "water_to_greenhouse", "name"),
            Icon = Prose.Text("upgrades", "water_to_greenhouse", "icon"),
            Description = Prose.Text("upgrades", "water_to_greenhouse", "description"),
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("water_purifier", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "greenhouse",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 100, Id: "water_purifier")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["apocalypse", "link"],
        };

        yield return new()
        {
            Id = "reactor_to_city",
            Name = Prose.Text("upgrades", "reactor_to_city", "name"),
            Icon = Prose.Text("upgrades", "reactor_to_city", "icon"),
            Description = Prose.Text("upgrades", "reactor_to_city", "description"),
            Price = 4e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("fusion_reactor", 50),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "relic_city",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "fusion_reactor")),
            ],
            Category = "link",
            Tier = 3,
            Tags = ["apocalypse", "link"],
        };
    }

    /// <summary>纪元线：每次重启各一条，只在那一层出现。</summary>
    private static IEnumerable<UpgradeDefinition> EraUpgrades()
    {
        yield return new()
        {
            Id = "oil_lamp",
            Name = Prose.Text("upgrades", "oil_lamp", "name"),
            Icon = Prose.Text("upgrades", "oil_lamp", "icon"),
            Description = Prose.Text("upgrades", "oil_lamp", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "era",
            Tier = 1,
            Tags = ["apocalypse", "era"],
        };

        yield return new()
        {
            Id = "grid",
            Name = Prose.Text("upgrades", "grid", "name"),
            Icon = Prose.Text("upgrades", "grid", "icon"),
            Description = Prose.Text("upgrades", "grid", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("generator", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "era",
            Tier = 2,
            Tags = ["apocalypse", "era"],
        };

        yield return new()
        {
            Id = "wall",
            Name = Prose.Text("upgrades", "wall", "name"),
            Icon = Prose.Text("upgrades", "wall", "icon"),
            Description = Prose.Text("upgrades", "wall", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("shelter", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.9)],
            Category = "era",
            Tier = 3,
            Tags = ["apocalypse", "era"],
        };

        yield return new()
        {
            Id = "ark",
            Name = Prose.Text("upgrades", "ark", "name"),
            Icon = Prose.Text("upgrades", "ark", "icon"),
            Description = Prose.Text("upgrades", "ark", "description"),
            Price = 8e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("archive", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.2),
            ],
            Category = "era",
            Tier = 4,
            Tags = ["apocalypse", "era"],
        };

        yield return new()
        {
            Id = "last_light",
            Name = Prose.Text("upgrades", "last_light", "name"),
            Icon = Prose.Text("upgrades", "last_light", "icon"),
            Description = Prose.Text("upgrades", "last_light", "description"),
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("relic_city", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.8),
            ],
            Category = "era",
            Tier = 5,
            Tags = ["apocalypse", "era"],
        };
    }

    /// <summary>
    /// 余烬：用「火种」购买，<b>跨重启保留</b>。<para>
    /// 这是"重启文明"这个转生语义的落点——世界没了，但你要带过去的那点东西还在。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> EmberUpgrades()
    {
        yield return new()
        {
            Id = "ember_hands",
            Name = Prose.Text("upgrades", "ember_hands", "name"),
            Icon = Prose.Text("upgrades", "ember_hands", "icon"),
            Description = Prose.Text("upgrades", "ember_hands", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "ember",
            Tier = 1,
            Tags = ["apocalypse", "ember"],
        };

        yield return new()
        {
            Id = "ember_blueprint",
            Name = Prose.Text("upgrades", "ember_blueprint", "name"),
            Icon = Prose.Text("upgrades", "ember_blueprint", "icon"),
            Description = Prose.Text("upgrades", "ember_blueprint", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "ember",
            Tier = 2,
            Tags = ["apocalypse", "ember"],
        };

        yield return new()
        {
            Id = "ember_name",
            Name = Prose.Text("upgrades", "ember_name", "name"),
            Icon = Prose.Text("upgrades", "ember_name", "icon"),
            Description = Prose.Text("upgrades", "ember_name", "description"),
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "ember",
            Tier = 3,
            Tags = ["apocalypse", "ember"],
        };

        yield return new()
        {
            Id = "ember_promise",
            Name = Prose.Text("upgrades", "ember_promise", "name"),
            Icon = Prose.Text("upgrades", "ember_promise", "icon"),
            Description = Prose.Text("upgrades", "ember_promise", "description"),
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 60_000, Id: ShardsModule.CounterKey)),
            ],
            Category = "ember",
            Tier = 4,
            Tags = ["apocalypse", "ember"],
        };

        yield return new()
        {
            Id = "ember_always",
            Name = Prose.Text("upgrades", "ember_always", "name"),
            Icon = Prose.Text("upgrades", "ember_always", "icon"),
            Description = Prose.Text("upgrades", "ember_always", "description"),
            Price = 45,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(9),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "ember",
            Tier = 5,
            Tags = ["apocalypse", "ember"],
        };
    }
}
