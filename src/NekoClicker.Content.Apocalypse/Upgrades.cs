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
                yield return new UpgradeDefinition
                {
                    Id = $"{building.Id}_tier{required}",
                    Name = $"{prefix}{building.Name}",
                    Icon = building.Icon,
                    Description = $"「{building.Name}」的产量翻倍。",
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
            Name = "翻找的手套",
            Icon = "🧤",
            Description = "每次翻找额外获得 1 点物资。手指头上的口子终于有人管了。",
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
            Name = "金属探测仪",
            Icon = "🔔",
            Description = "点击收益 ×2。从那以后她不再凭手感挖。",
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
            Name = "无人机眼",
            Icon = "🛩️",
            Description = "点击收益 ×3。它飞一圈要二十分钟，比她自己走一天看到的多。",
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
            Name = "废墟直觉",
            Icon = "🧭",
            Description = "点击收益 ×4，且每次翻找额外获得 1e4 点物资。有些地方她就是知道下面有东西。",
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
            Name = "残片镜",
            Icon = "🔍",
            Description = "全局产量 +0.05%／每片记忆残片（上限 +75%）。把碎片举到光底下，能看见上面还有字。",
            Price = 8_000_000,
            Unlock = UnlockCondition.Counter(ShardsModule.CounterKey, 200),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.0005, Cap: 75, Id: ShardsModule.CounterKey)),
            ],
            Category = "memory",
            Tier = 1,
            Tags = ["apocalypse", "memory"],
        };

        yield return new()
        {
            Id = "memory_weave",
            Name = "记忆编织",
            Icon = "🧶",
            Description = "全局产量 ×2，增益时长 ×1.3。她知道哪几片应该接在一起——接错了会疼。",
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
            Name = "继承者的声音",
            Icon = "🗣️",
            Description = "全局产量 ×2.5、变异体奖励 ×2。她开始用别人的语气说话，然后道歉。",
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
            Name = "从废墟里架起来的天线",
            Icon = "📡",
            Description = "数据塔产量 +2%／每座废墟（上限 +200%）。天线是拿拆下来的钢筋搭的。",
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("ruins", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "data_tower",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 200, Id: "ruins")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["apocalypse", "link"],
        };

        yield return new()
        {
            Id = "water_to_greenhouse",
            Name = "水浇出来的绿",
            Icon = "🚿",
            Description = "温室产量 +1.5%／每台净水器（上限 +150%）。干净的水先给能长东西的地方。",
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("water_purifier", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "greenhouse",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 150, Id: "water_purifier")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["apocalypse", "link"],
        };

        yield return new()
        {
            Id = "reactor_to_city",
            Name = "整座城的电",
            Icon = "🔋",
            Description = "遗迹之城产量 +1%／每座聚变堆（上限 +300%）。她给整座废墟通了电，只为了让灯替她守着。",
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
            Name = "油灯",
            Icon = "🪔",
            Description = "全局产量 ×1.5。没有电的晚上也得干活，这是第一件她自己做的东西。",
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
            Name = "局域电网",
            Icon = "🗼",
            Description = "全局产量 ×2。把三台发电机接在一起，报废任何一台都不至于全黑。",
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
            Name = "围墙",
            Icon = "🧱",
            Description = "全局产量 ×2，建筑价格 ×0.9。墙是把「我们」和「外面」分开的第一件东西。",
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
            Name = "方舟图纸",
            Icon = "📐",
            Description = "全局产量 ×2.5、增益时长 ×1.2。图纸上是一套能装下四千人的地下结构，只画完了一半。",
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
            Name = "最后的灯",
            Icon = "🔆",
            Description = "全局产量 ×3，但增益时长 ×0.8。全城的灯都亮着，她一个人站在最高的那盏下面。",
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
            Name = "余烬之手",
            Icon = "🤲",
            Description = "全局产量 +25%。手上的茧是上一世留下的，这一世不用重新长。",
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
            Name = "余烬图纸",
            Icon = "📜",
            Description = "建筑价格 ×0.8。图纸边角被烧掉了，剩下的部分够用。",
            Price = 8,
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
            Name = "余烬之名",
            Icon = "✍️",
            Description = "点击收益 ×6、变异体奖励 ×1.5。她记得自己叫什么，这在重启里并不常见。",
            Price = 25,
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
            Name = "余烬之约",
            Icon = "🕯️",
            Description = "全局产量 ×2、记忆残片产率 ×1.5。她答应过的东西比她自己记得的多。",
            Price = 70,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.0002, Cap: 60, Id: ShardsModule.CounterKey)),
            ],
            Category = "ember",
            Tier = 4,
            Tags = ["apocalypse", "ember"],
        };

        yield return new()
        {
            Id = "ember_always",
            Name = "余烬不灭",
            Icon = "🔥",
            Description = "全局产量 ×2.5、增益时长 ×1.4。她终于承认：她不是在被重启，她是在接着往下活。",
            Price = 300,
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
