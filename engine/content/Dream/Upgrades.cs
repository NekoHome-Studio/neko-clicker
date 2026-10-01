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
                yield return new UpgradeDefinition
                {
                    Id = $"{building.Id}_t{required}",
                    Name = $"{prefix}{building.Name}",
                    Icon = building.Icon,
                    Description = $"「{building.Name}」的产量翻倍。",
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
            Name = "数到第七只",
            Icon = "🐑",
            Description = "每次闭眼额外获得 1 点梦。她数到第七只就不数了，因为第七只开始说话。",
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
            Name = "半梦半醒",
            Icon = "😑",
            Description = "点击收益 ×2。只有一半睡着了的人，手指还能动。",
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
            Name = "记住这个梦",
            Icon = "📝",
            Description = "点击收益 ×3。醒来之后她写了三行，写完发现字是反的。",
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
            Name = "想醒就能醒",
            Icon = "⏱️",
            Description = "点击收益 ×4，且每次闭眼额外获得 1e4 点梦。她试过一次，那次她真的醒了，然后又睡了回去。",
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
            Name = "再睡七分钟",
            Icon = "⏰",
            Description = "点击收益 ×5。这是她说得最多的一句话，也是这个梦里最像谎的一句。",
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
            Name = "梦在变浓",
            Icon = "🌫️",
            Description = "全局产量 +0.05%／每点梦境能量（最多计入 20,000 点，即 +100%）。她抬手的时候，空气有一点阻力。",
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
            Name = "梦有实体",
            Icon = "🧱",
            Description = "全局产量 ×2，增益时长 ×1.25。她按了一下墙，墙面凹下去一小块，慢慢又弹了回来。",
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
            Name = "梦里能造东西",
            Icon = "🪄",
            Description = "全局产量 ×2.5、梦魇奖励 ×2。她捏了一只猫，捏出来是热的，还会打呼。",
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
            Name = "和梦核共振",
            Icon = "🔮",
            Description = "全局产量 +0.3%／每点梦境能量（最多计入 600 million 点，即 +180%）。她和那颗东西对上了频率。",
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
            Name = "枕头底下压着一层梦",
            Icon = "🛏️",
            Description = "噩梦巢产量 +1.5%／每座枕头（最多计入 100 座，即 +150%）。她把枕头掀开过，底下是凉的。",
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
            Name = "用梦层织梦",
            Icon = "🧶",
            Description = "织梦者产量 +2%／每层梦层（最多计入 100 层，即 +200%）。线头是从下面那一层抽上来的。",
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
            Name = "从噩梦里带出来的清醒",
            Icon = "💡",
            Description = "清醒区产量 +1%／每座噩梦巢（最多计入 200 座，即 +200%）。在噩梦里待过的人，做梦时最清醒。",
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
            Name = "塔的重量压在核上",
            Icon = "🗼",
            Description = "梦核产量 +1%／每座嵌套塔（最多计入 300 座，即 +300%）。塔越往上，核转得越快。",
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
            Name = "第一次掉下去",
            Icon = "⤵️",
            Description = "全局产量 ×1.8。入睡的那一瞬间像踩空一格楼梯，谁都躲不过。",
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
            Name = "越来越沉",
            Icon = "🪨",
            Description = "全局产量 ×2、建筑价格 ×0.92。醒来的时候身体像灌了铅，那是因为梦舍不得放人。",
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
            Name = "这是我的梦",
            Icon = "💡",
            Description = "全局产量 ×2.2，梦魇间隔 ×1.15（来得更少）。清明之后梦魇反而少了一点——因为它也得听她的。",
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
            Name = "没做完的那些事",
            Icon = "🕷️",
            Description = "全局产量 ×2.5、增益时长 ×1.2。噩梦层里的东西全是从上面掉下来的，包括她自己的。",
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
            Name = "梦核答应了一声",
            Icon = "🔮",
            Description = "全局产量 ×3，但增益时长 ×0.85。它转得快了，所以梦也散得快。",
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
            Name = "再搭一层",
            Icon = "🧱",
            Description = "全局产量 ×2.5、建筑价格 ×0.88。最里面那层是她自己搭的，所以她说了算。",
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
            Name = "留下来的碎片",
            Icon = "🧩",
            Description = "全局产量 +25%。上一层的梦碎在这里，拼起来还能认出形状。",
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
            Name = "记得往下走的路",
            Icon = "🪜",
            Description = "建筑价格 ×0.82。往下走过一次之后，第二次就不用摸黑。",
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
            Name = "手还能动",
            Icon = "✋",
            Description = "点击收益 ×6、梦魇奖励 ×1.5。在浅眠那一层，能动手就是全部的本事。",
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
            Name = "还是同一个晚上",
            Icon = "🌙",
            Description = "全局产量 ×2、梦魇奖励 ×1.5。睡了这么深，外面的天还没亮。",
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
            Name = "每一层都记得",
            Icon = "🧠",
            Description = "全局产量 ×2.5、增益时长 ×1.4。她能把五层梦按顺序背出来，一层不差。",
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
