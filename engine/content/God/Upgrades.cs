using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 升级表（49 条）。<para>
/// 六种写法都在这里：批量生成的建筑强化档、由<b>信仰</b>驱动成长的"香火"线、
/// 由<b>在线人数</b>驱动的"直播"线、用建筑数量成长的"联动"线、
/// 每套神话专属的"经文"线，以及用神格购买并跨体系保留的"神格"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对全部包同时成立——换包换的是叙事，不是手感。
/// </para>
/// <para>
/// <b>信仰线在这里是"解锁条件"而不是"计价货币"</b>：信仰只涨不花（<see cref="FaithModule"/>），
/// 所以它买不了东西，它只能当门槛——攒够了香火，某条经文才肯显灵。
/// 真正能买东西的第二资源会破坏"信仰单调"这条前提，而四层纪元的完成条件正挂在它上面。
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
        .. FaithUpgrades(),
        .. StreamUpgrades(),
        .. LinkUpgrades(),
        .. MythUpgrades(),
        .. DivinityUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "开过光的"),
            (10, 100, "香火不断的"),
            (25, 500, "一整条街的"),
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
                    Tags = ["god", "tier"],
                };
            }
        }
    }

    /// <summary>显灵线：这个包里手是"神迹"的现场演示，所以点击比大多数包都强。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "prayer_beads",
            Name = Prose.Text("upgrades", "prayer_beads", "name"),
            Icon = Prose.Text("upgrades", "prayer_beads", "icon"),
            Description = Prose.Text("upgrades", "prayer_beads", "description"),
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["god", "click"],
        };

        yield return new()
        {
            Id = "live_demo",
            Name = Prose.Text("upgrades", "live_demo", "name"),
            Icon = Prose.Text("upgrades", "live_demo", "icon"),
            Description = Prose.Text("upgrades", "live_demo", "description"),
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["god", "click"],
        };

        yield return new()
        {
            Id = "fan_service",
            Name = Prose.Text("upgrades", "fan_service", "name"),
            Icon = Prose.Text("upgrades", "fan_service", "icon"),
            Description = Prose.Text("upgrades", "fan_service", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["god", "click"],
        };

        yield return new()
        {
            Id = "algorithm_god",
            Name = Prose.Text("upgrades", "algorithm_god", "name"),
            Icon = Prose.Text("upgrades", "algorithm_god", "icon"),
            Description = Prose.Text("upgrades", "algorithm_god", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(10_000)],
            Category = "click",
            Tier = 4,
            Tags = ["god", "click"],
        };
    }

    /// <summary>
    /// 香火线：由「信仰」驱动。<para>
    /// 这一层是第二资源参与数值的地方——信仰不但自己变成产量（每层纪元的常驻规则），
    /// 攒到一定量还会解锁更狠的经文。门槛压在几千到几十万：信仰是累加量，
    /// 而且这一层的门槛与四层纪元的完成门槛共用同一把尺子（见 <see cref="Eras"/>）。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> FaithUpgrades()
    {
        yield return new()
        {
            Id = "incense_monopoly",
            Name = Prose.Text("upgrades", "incense_monopoly", "name"),
            Icon = Prose.Text("upgrades", "incense_monopoly", "icon"),
            Description = Prose.Text("upgrades", "incense_monopoly", "description"),
            Price = 8_000_000,
            Unlock = UnlockCondition.Counter(FaithModule.CounterKey, 3_000),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "faith",
            Tier = 1,
            Tags = ["god", "faith"],
        };

        yield return new()
        {
            Id = "tithe_network",
            Name = Prose.Text("upgrades", "tithe_network", "name"),
            Icon = Prose.Text("upgrades", "tithe_network", "icon"),
            Description = Prose.Text("upgrades", "tithe_network", "description"),
            Price = 400_000_000,
            Unlock = UnlockCondition.Counter(FaithModule.CounterKey, 40_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.3),
            ],
            Category = "faith",
            Tier = 2,
            Tags = ["god", "faith"],
        };

        yield return new()
        {
            Id = "faith_inflation",
            Name = Prose.Text("upgrades", "faith_inflation", "name"),
            Icon = Prose.Text("upgrades", "faith_inflation", "icon"),
            Description = Prose.Text("upgrades", "faith_inflation", "description"),
            Price = 3e10,
            Unlock = UnlockCondition.Counter(FaithModule.CounterKey, 300_000),
            Modifiers = [Modifier.GlobalMultiplier(2.5), Modifier.GoldenCookieReward(2)],
            Category = "faith",
            Tier = 3,
            Tags = ["god", "faith"],
        };
    }

    /// <summary>
    /// 直播线：由「直播在线人数」驱动。<para>
    /// 在线人数记的是历史峰值（<see cref="FaithModule"/>），所以它同样单调，
    /// 门槛不会被"下播"打回去。这条线负责把第 5 层的主场（直播间）变成真正的主力。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> StreamUpgrades()
    {
        yield return new()
        {
            Id = "stream_overlay",
            Name = Prose.Text("upgrades", "stream_overlay", "name"),
            Icon = Prose.Text("upgrades", "stream_overlay", "icon"),
            Description = Prose.Text("upgrades", "stream_overlay", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.Counter(FaithModule.ViewerCounterKey, 5_000),
            Modifiers = [Modifier.BuildingMultiplier("stream_studio", 30), Modifier.GlobalMultiplier(1.5)],
            Category = "stream",
            Tier = 1,
            Tags = ["god", "stream"],
        };

        yield return new()
        {
            Id = "merch_collab",
            Name = Prose.Text("upgrades", "merch_collab", "name"),
            Icon = Prose.Text("upgrades", "merch_collab", "icon"),
            Description = Prose.Text("upgrades", "merch_collab", "description"),
            Price = 8e10,
            Unlock = UnlockCondition.Counter(FaithModule.ViewerCounterKey, 50_000),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.9)],
            Category = "stream",
            Tier = 2,
            Tags = ["god", "stream"],
        };
    }

    /// <summary>
    /// 联动线：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 神明的隐喻本来就是"香火越旺，庙越大"：小庙的数量决定大神殿的排面，
    /// 祭坛的数量决定方尖碑的排面，雷霆殿决定深渊大教堂的排面。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> LinkUpgrades()
    {
        yield return new()
        {
            Id = "shrine_to_temple",
            Name = Prose.Text("upgrades", "shrine_to_temple", "name"),
            Icon = Prose.Text("upgrades", "shrine_to_temple", "icon"),
            Description = Prose.Text("upgrades", "shrine_to_temple", "description"),
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("house_shrine", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "stone_temple",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 200, Id: "house_shrine")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["god", "link"],
        };

        yield return new()
        {
            Id = "altar_to_obelisk",
            Name = Prose.Text("upgrades", "altar_to_obelisk", "name"),
            Icon = Prose.Text("upgrades", "altar_to_obelisk", "icon"),
            Description = Prose.Text("upgrades", "altar_to_obelisk", "description"),
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("offering_altar", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "sun_obelisk",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 150, Id: "offering_altar")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["god", "link"],
        };

        yield return new()
        {
            Id = "hall_to_abyss",
            Name = Prose.Text("upgrades", "hall_to_abyss", "name"),
            Icon = Prose.Text("upgrades", "hall_to_abyss", "icon"),
            Description = Prose.Text("upgrades", "hall_to_abyss", "description"),
            Price = 4e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("thunder_hall", 50),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "abyssal_cathedral",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "thunder_hall")),
            ],
            Category = "link",
            Tier = 3,
            Tags = ["god", "link"],
        };
    }

    /// <summary>经文线：每套神话各一条，只在那套体系里出现。</summary>
    private static IEnumerable<UpgradeDefinition> MythUpgrades()
    {
        yield return new()
        {
            Id = "hearth_sutra",
            Name = Prose.Text("upgrades", "hearth_sutra", "name"),
            Icon = Prose.Text("upgrades", "hearth_sutra", "icon"),
            Description = Prose.Text("upgrades", "hearth_sutra", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "myth",
            Tier = 1,
            Tags = ["god", "myth"],
        };

        yield return new()
        {
            Id = "book_of_the_dead",
            Name = Prose.Text("upgrades", "book_of_the_dead", "name"),
            Icon = Prose.Text("upgrades", "book_of_the_dead", "icon"),
            Description = Prose.Text("upgrades", "book_of_the_dead", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("stone_temple", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "myth",
            Tier = 2,
            Tags = ["god", "myth"],
        };

        yield return new()
        {
            Id = "oracle_collection",
            Name = Prose.Text("upgrades", "oracle_collection", "name"),
            Icon = Prose.Text("upgrades", "oracle_collection", "icon"),
            Description = Prose.Text("upgrades", "oracle_collection", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("oracle_grove", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.9)],
            Category = "myth",
            Tier = 3,
            Tags = ["god", "myth"],
        };

        yield return new()
        {
            Id = "ragnarok_schedule",
            Name = Prose.Text("upgrades", "ragnarok_schedule", "name"),
            Icon = Prose.Text("upgrades", "ragnarok_schedule", "icon"),
            Description = Prose.Text("upgrades", "ragnarok_schedule", "description"),
            Price = 8e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("thunder_hall", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.2),
            ],
            Category = "myth",
            Tier = 4,
            Tags = ["god", "myth"],
        };

        yield return new()
        {
            Id = "rlyeh_phrasebook",
            Name = Prose.Text("upgrades", "rlyeh_phrasebook", "name"),
            Icon = Prose.Text("upgrades", "rlyeh_phrasebook", "icon"),
            Description = Prose.Text("upgrades", "rlyeh_phrasebook", "description"),
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("abyssal_cathedral", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.8),
            ],
            Category = "myth",
            Tier = 5,
            Tags = ["god", "myth"],
        };
    }

    /// <summary>
    /// 神格：用转生货币购买，<b>跨神话体系保留</b>。<para>
    /// 这是"切换神话体系"这个转生语义的落点——换掉的是抬头、仪轨和庙的样子，
    /// 带得走的是她自己。价格总价 80，压在"一次自然游玩结算出的神格"之下
    /// （<c>PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun</c> 每次都替你验一遍）。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> DivinityUpgrades()
    {
        yield return new()
        {
            Id = "old_altar_stone",
            Name = Prose.Text("upgrades", "old_altar_stone", "name"),
            Icon = Prose.Text("upgrades", "old_altar_stone", "icon"),
            Description = Prose.Text("upgrades", "old_altar_stone", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "divinity",
            Tier = 1,
            Tags = ["god", "divinity"],
        };

        yield return new()
        {
            Id = "priest_handbook",
            Name = Prose.Text("upgrades", "priest_handbook", "name"),
            Icon = Prose.Text("upgrades", "priest_handbook", "icon"),
            Description = Prose.Text("upgrades", "priest_handbook", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "divinity",
            Tier = 2,
            Tags = ["god", "divinity"],
        };

        yield return new()
        {
            Id = "her_own_voice",
            Name = Prose.Text("upgrades", "her_own_voice", "name"),
            Icon = Prose.Text("upgrades", "her_own_voice", "icon"),
            Description = Prose.Text("upgrades", "her_own_voice", "description"),
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "divinity",
            Tier = 3,
            Tags = ["god", "divinity"],
        };

        yield return new()
        {
            Id = "loyal_flock",
            Name = Prose.Text("upgrades", "loyal_flock", "name"),
            Icon = Prose.Text("upgrades", "loyal_flock", "icon"),
            Description = Prose.Text("upgrades", "loyal_flock", "description"),
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "divinity",
            Tier = 4,
            Tags = ["god", "divinity"],
        };

        yield return new()
        {
            Id = "same_cat",
            Name = Prose.Text("upgrades", "same_cat", "name"),
            Icon = Prose.Text("upgrades", "same_cat", "icon"),
            Description = Prose.Text("upgrades", "same_cat", "description"),
            Price = 45,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(9),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "divinity",
            Tier = 5,
            Tags = ["god", "divinity"],
        };
    }
}
