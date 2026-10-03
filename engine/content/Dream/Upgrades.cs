using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 升级表（52 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、按<b>梦境能量</b>成长的"梦浓"线、
/// 用建筑数量成长的"衔接"线、每一层梦专属的"梦层"线，以及用梦屑购买并跨层保留的"梦屑"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10 / 110 / 550 倍），
/// 所以曲线回归对全部包同时成立。
/// </para>
/// <para>
/// <b>梦浓线的门槛挂在梦境能量上，而它只涨不落、跨层不清零</b>——所以这条线一旦解锁
/// 就不会再锁回去，和图书馆那条"会掉回去"的读者线是两种完全不同的手感：
/// 这里它是一条<b>进度尺</b>，玩家看得见自己离梦核还有多远。
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
        .. DreamEnergyUpgrades(),
        .. ChainUpgrades(),
        .. SleepLayerUpgrades(),
        .. DreamShardUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "铺好的"),
            (10, 110, "铺了一层的"),
            (25, 550, "一层压一层的"),
        ];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int required, double priceFactor, string prefix) in tiers)
            {
                string id = $"{building.Id}_t{required}";
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
                    Tags = ["dream", "tier"],
                };
            }
        }
    }

    /// <summary>闭眼线：浅眠那一层一动就醒，所以这个包的点击比图书馆以外的包都强。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "count_sheep",
            Name = Prose.Text("upgrades", "count_sheep", "name"),
            Icon = Prose.Text("upgrades", "count_sheep", "icon"),
            Description = Prose.Text("upgrades", "count_sheep", "description"),
            Price = 250,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["dream", "click"],
        };

        yield return new()
        {
            Id = "half_asleep",
            Name = Prose.Text("upgrades", "half_asleep", "name"),
            Icon = Prose.Text("upgrades", "half_asleep", "icon"),
            Description = Prose.Text("upgrades", "half_asleep", "description"),
            Price = 22_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["dream", "click"],
        };

        yield return new()
        {
            Id = "remember_the_dream",
            Name = Prose.Text("upgrades", "remember_the_dream", "name"),
            Icon = Prose.Text("upgrades", "remember_the_dream", "icon"),
            Description = Prose.Text("upgrades", "remember_the_dream", "description"),
            Price = 6_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["dream", "click"],
        };

        yield return new()
        {
            Id = "wake_control",
            Name = Prose.Text("upgrades", "wake_control", "name"),
            Icon = Prose.Text("upgrades", "wake_control", "icon"),
            Description = Prose.Text("upgrades", "wake_control", "description"),
            Price = 2.2e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(10_000)],
            Category = "click",
            Tier = 4,
            Tags = ["dream", "click"],
        };

        yield return new()
        {
            Id = "seven_minutes",
            Name = Prose.Text("upgrades", "seven_minutes", "name"),
            Icon = Prose.Text("upgrades", "seven_minutes", "icon"),
            Description = Prose.Text("upgrades", "seven_minutes", "description"),
            Price = 8e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(20_000),
                UnlockCondition.EraAtLeast(5)),
            Modifiers = [Modifier.ClickMultiplier(5)],
            Category = "click",
            Tier = 5,
            Tags = ["dream", "click"],
        };
    }

    /// <summary>
    /// 梦浓线：由「梦境能量」驱动。<para>
    /// 这是第二资源参与数值的地方——不是拿它买东西，而是<b>让"梦有多浓"直接变成产能</b>。
    /// 门槛压在 2,000 到 6 亿：数值是<b>按实测包络</b>排开的（手册 §12.2），
    /// 所以四档分别落在第 1 小时、第 2 小时、第 3 小时与第 6 小时前后，
    /// 而不是挤在开局那五分钟里。每一条的描述都把"最多计入多少点"写清楚——
    /// <c>Cap</c> 限的是原始计数值，不是加成结果（手册 §8）。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> DreamEnergyUpgrades()
    {
        yield return new()
        {
            Id = "thickening",
            Name = Prose.Text("upgrades", "thickening", "name"),
            Icon = Prose.Text("upgrades", "thickening", "icon"),
            Description = Prose.Text("upgrades", "thickening", "description"),
            Price = 3_000_000,
            Unlock = UnlockCondition.Counter(DreamEnergyModule.CounterKey, 2_000),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 20_000, Id: DreamEnergyModule.CounterKey)),
            ],
            Category = "energy",
            Tier = 1,
            Tags = ["dream", "energy"],
        };

        yield return new()
        {
            Id = "dream_substance",
            Name = Prose.Text("upgrades", "dream_substance", "name"),
            Icon = Prose.Text("upgrades", "dream_substance", "icon"),
            Description = Prose.Text("upgrades", "dream_substance", "description"),
            Price = 4.5e8,
            Unlock = UnlockCondition.Counter(DreamEnergyModule.CounterKey, 300_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.25),
            ],
            Category = "energy",
            Tier = 2,
            Tags = ["dream", "energy"],
        };

        yield return new()
        {
            Id = "make_things",
            Name = Prose.Text("upgrades", "make_things", "name"),
            Icon = Prose.Text("upgrades", "make_things", "icon"),
            Description = Prose.Text("upgrades", "make_things", "description"),
            Price = 2.5e10,
            Unlock = UnlockCondition.Counter(DreamEnergyModule.CounterKey, 20_000_000),
            Modifiers = [Modifier.GlobalMultiplier(2.5), Modifier.GoldenCookieReward(2)],
            Category = "energy",
            Tier = 3,
            Tags = ["dream", "energy"],
        };

        yield return new()
        {
            Id = "core_resonance",
            Name = Prose.Text("upgrades", "core_resonance", "name"),
            Icon = Prose.Text("upgrades", "core_resonance", "icon"),
            Description = Prose.Text("upgrades", "core_resonance", "description"),
            Price = 8e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.Counter(DreamEnergyModule.CounterKey, 600_000_000),
                UnlockCondition.EraAtLeast(5)),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.000003, Cap: 600_000_000, Id: DreamEnergyModule.CounterKey)),
            ],
            Category = "energy",
            Tier = 4,
            Tags = ["dream", "energy"],
        };
    }

    /// <summary>
    /// 衔接线：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 这个包的隐喻是"层与层套在一起"，所以这一条线写的是"上面那层决定下面那层"。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> ChainUpgrades()
    {
        yield return new()
        {
            Id = "nest_under_pillow",
            Name = Prose.Text("upgrades", "nest_under_pillow", "name"),
            Icon = Prose.Text("upgrades", "nest_under_pillow", "icon"),
            Description = Prose.Text("upgrades", "nest_under_pillow", "description"),
            Price = 55_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("pillow", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "nightmare_nest",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 100, Id: "pillow")),
            ],
            Category = "chain",
            Tier = 1,
            Tags = ["dream", "chain"],
        };

        yield return new()
        {
            Id = "weave_the_layer",
            Name = Prose.Text("upgrades", "weave_the_layer", "name"),
            Icon = Prose.Text("upgrades", "weave_the_layer", "icon"),
            Description = Prose.Text("upgrades", "weave_the_layer", "description"),
            Price = 8e8,
            Unlock = UnlockCondition.BuildingsAtLeast("dream_layer", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "dream_weaver",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 100, Id: "dream_layer")),
            ],
            Category = "chain",
            Tier = 2,
            Tags = ["dream", "chain"],
        };

        yield return new()
        {
            Id = "nest_to_lucid",
            Name = Prose.Text("upgrades", "nest_to_lucid", "name"),
            Icon = Prose.Text("upgrades", "nest_to_lucid", "icon"),
            Description = Prose.Text("upgrades", "nest_to_lucid", "description"),
            Price = 3e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("nightmare_nest", 75),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "lucid_zone",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 200, Id: "nightmare_nest")),
            ],
            Category = "chain",
            Tier = 3,
            Tags = ["dream", "chain"],
        };

        yield return new()
        {
            Id = "tower_to_core",
            Name = Prose.Text("upgrades", "tower_to_core", "name"),
            Icon = Prose.Text("upgrades", "tower_to_core", "icon"),
            Description = Prose.Text("upgrades", "tower_to_core", "description"),
            Price = 6e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("nesting_tower", 50),
                UnlockCondition.EraAtLeast(5)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "dream_core",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "nesting_tower")),
            ],
            Category = "chain",
            Tier = 4,
            Tags = ["dream", "chain"],
        };
    }

    /// <summary>梦层线：每一层梦各一条，只在那层里出现。</summary>
    private static IEnumerable<UpgradeDefinition> SleepLayerUpgrades()
    {
        yield return new()
        {
            Id = "the_first_fall",
            Name = Prose.Text("upgrades", "the_first_fall", "name"),
            Icon = Prose.Text("upgrades", "the_first_fall", "icon"),
            Description = Prose.Text("upgrades", "the_first_fall", "description"),
            Price = 6_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.8)],
            Category = "sleep",
            Tier = 1,
            Tags = ["dream", "sleep"],
        };

        yield return new()
        {
            Id = "heavier",
            Name = Prose.Text("upgrades", "heavier", "name"),
            Icon = Prose.Text("upgrades", "heavier", "icon"),
            Description = Prose.Text("upgrades", "heavier", "description"),
            Price = 32_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("dream_layer", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.92)],
            Category = "sleep",
            Tier = 2,
            Tags = ["dream", "sleep"],
        };

        yield return new()
        {
            Id = "my_own_dream",
            Name = Prose.Text("upgrades", "my_own_dream", "name"),
            Icon = Prose.Text("upgrades", "my_own_dream", "icon"),
            Description = Prose.Text("upgrades", "my_own_dream", "description"),
            Price = 2.2e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("lucid_zone", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2.2), Modifier.GoldenCookieFrequency(1.15)],
            Category = "sleep",
            Tier = 3,
            Tags = ["dream", "sleep"],
        };

        yield return new()
        {
            Id = "things_i_did",
            Name = Prose.Text("upgrades", "things_i_did", "name"),
            Icon = Prose.Text("upgrades", "things_i_did", "icon"),
            Description = Prose.Text("upgrades", "things_i_did", "description"),
            Price = 9e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("nightmare_nest", 100)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.2),
            ],
            Category = "sleep",
            Tier = 4,
            Tags = ["dream", "sleep"],
        };

        yield return new()
        {
            Id = "the_core_answered",
            Name = Prose.Text("upgrades", "the_core_answered", "name"),
            Icon = Prose.Text("upgrades", "the_core_answered", "icon"),
            Description = Prose.Text("upgrades", "the_core_answered", "description"),
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("dream_core", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.85),
            ],
            Category = "sleep",
            Tier = 5,
            Tags = ["dream", "sleep"],
        };

        yield return new()
        {
            Id = "one_more_layer",
            Name = Prose.Text("upgrades", "one_more_layer", "name"),
            Icon = Prose.Text("upgrades", "one_more_layer", "icon"),
            Description = Prose.Text("upgrades", "one_more_layer", "description"),
            Price = 1.2e12,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(DreamEnergyModule.CounterKey, 40_000)),
            Modifiers = [Modifier.GlobalMultiplier(2.5), Modifier.PriceMultiplier(0.88)],
            Category = "sleep",
            Tier = 6,
            Tags = ["dream", "sleep"],
        };
    }

    /// <summary>梦屑：用「梦屑」购买，<b>跨层保留</b>。这是"往下睡一层"这个转生语义的落点。</summary>
    private static IEnumerable<UpgradeDefinition> DreamShardUpgrades()
    {
        yield return new()
        {
            Id = "kept_fragments",
            Name = Prose.Text("upgrades", "kept_fragments", "name"),
            Icon = Prose.Text("upgrades", "kept_fragments", "icon"),
            Description = Prose.Text("upgrades", "kept_fragments", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "shard",
            Tier = 1,
            Tags = ["dream", "shard"],
        };

        yield return new()
        {
            Id = "know_the_way_down",
            Name = Prose.Text("upgrades", "know_the_way_down", "name"),
            Icon = Prose.Text("upgrades", "know_the_way_down", "icon"),
            Description = Prose.Text("upgrades", "know_the_way_down", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.82)],
            Category = "shard",
            Tier = 2,
            Tags = ["dream", "shard"],
        };

        yield return new()
        {
            Id = "hand_still_works",
            Name = Prose.Text("upgrades", "hand_still_works", "name"),
            Icon = Prose.Text("upgrades", "hand_still_works", "icon"),
            Description = Prose.Text("upgrades", "hand_still_works", "description"),
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "shard",
            Tier = 3,
            Tags = ["dream", "shard"],
        };

        yield return new()
        {
            Id = "same_night",
            Name = Prose.Text("upgrades", "same_night", "name"),
            Icon = Prose.Text("upgrades", "same_night", "icon"),
            Description = Prose.Text("upgrades", "same_night", "description"),
            Price = 18,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "shard",
            Tier = 4,
            Tags = ["dream", "shard"],
        };

        yield return new()
        {
            Id = "remember_every_layer",
            Name = Prose.Text("upgrades", "remember_every_layer", "name"),
            Icon = Prose.Text("upgrades", "remember_every_layer", "icon"),
            Description = Prose.Text("upgrades", "remember_every_layer", "description"),
            Price = 27,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(9),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "shard",
            Tier = 5,
            Tags = ["dream", "shard"],
        };
    }
}
