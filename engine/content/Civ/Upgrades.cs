using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 升级表（51 条）。<para>
/// 六种写法都在这里：批量生成的建筑强化档、按<b>文化</b>成长的"传承"线、
/// 用建筑数量成长的"互文"线、每个时代专属的"时代"线，以及用「火种」购买并跨时代保留的"遗产"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对全部包同时成立。
/// </para>
/// <para>
/// <b>「传承」线是这个包第二资源的落点</b>：文化不是拿去买东西的货币，而是<b>直接变成产能</b>——
/// 每一点文化都在给全局产量加成。三档的 <c>Cap</c> 依次是 2 万 / 15 万 / 40 万，
/// 对应"她记下来的东西第一次开始帮她干活"到"整个文明靠记忆运转"。
/// 端点由 <c>CivContentTests.CultureScaling_HitsItsEndpoints</c> 钉死——
/// <c>Scaling.Cap</c> 限的是<b>原始计数值</b>而不是加成结果，这类误读只有端点断言拦得住。
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
        .. CultureUpgrades(),
        .. LinkUpgrades(),
        .. EraUpgrades(),
        .. HeritageUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "修好的"),
            (10, 100, "成片的"),
            (25, 500, "一眼望不到头的"),
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
                    Tags = ["civ", "tier"],
                };
            }
        }
    }

    /// <summary>
    /// 筑巢线：第 1 层她只有爪子，所以这个包的手比图书馆、公司都硬——
    /// 点击的起点是 2 而不是 1，而最后一档给的是"整座城的产能都算她亲手做的"。
    /// </summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "bare_paws",
            Name = Prose.Text("upgrades", "bare_paws", "name"),
            Icon = Prose.Text("upgrades", "bare_paws", "icon"),
            Description = Prose.Text("upgrades", "bare_paws", "description"),
            Price = 150,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(2)],
            Category = "click",
            Tier = 1,
            Tags = ["civ", "click"],
        };

        yield return new()
        {
            Id = "stone_knife",
            Name = Prose.Text("upgrades", "stone_knife", "name"),
            Icon = Prose.Text("upgrades", "stone_knife", "icon"),
            Description = Prose.Text("upgrades", "stone_knife", "description"),
            Price = 12_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["civ", "click"],
        };

        yield return new()
        {
            Id = "writing_brush",
            Name = Prose.Text("upgrades", "writing_brush", "name"),
            Icon = Prose.Text("upgrades", "writing_brush", "icon"),
            Description = Prose.Text("upgrades", "writing_brush", "description"),
            Price = 80_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(3)),
            Modifiers = [Modifier.ClickMultiplier(3), Modifier.ClickFlat(5_000)],
            Category = "click",
            Tier = 3,
            Tags = ["civ", "click"],
        };

        yield return new()
        {
            Id = "her_own_hands",
            Name = Prose.Text("upgrades", "her_own_hands", "name"),
            Icon = Prose.Text("upgrades", "her_own_hands", "icon"),
            Description = Prose.Text("upgrades", "her_own_hands", "description"),
            Price = 6e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(1e7)],
            Category = "click",
            Tier = 4,
            Tags = ["civ", "click"],
        };

        // 「点击 × 建筑」的桥：点击线四档清一色是**固定**倍率，与这个文明盖了多少东西无关。
        // 这一条把点击收益接到**持有建筑总数**上（本包唯一使用 ScalingSource.TotalBuildings 的地方），
        // 每座 +0.5%、最多算 120 座（+60%）——上限是作者手册 §3(b) 的硬规则。
        // 门槛 80 座：实测本包一局（贪心机器人，1.36 游戏小时）最多持有 561 座建筑。
        yield return new()
        {
            Id = "hands_of_the_people",
            Name = Prose.Text("upgrades", "hands_of_the_people", "name"),
            Icon = Prose.Text("upgrades", "hands_of_the_people", "icon"),
            Description = Prose.Text("upgrades", "hands_of_the_people", "description"),
            Price = 6e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.TotalBuildingsAtLeast(80),
                UnlockCondition.UpgradeOwned("stone_knife")),
            Modifiers =
            [
                new Modifier(
                    ModifierTarget.ClickPower,
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.TotalBuildings, 0.005, Cap: 120)),
            ],
            Category = "click",
            Tier = 5,
            Tags = ["civ", "click"],
        };
    }

    /// <summary>
    /// 传承线：由「文化」驱动。<para>
    /// 这是第二资源参与数值的地方——不是拿它买东西，而是<b>让"记得住"直接变成产能</b>。
    /// 门槛压在 3000 / 30000 / 180000：文化只由五座记录者建筑产出，
    /// 而它<b>跨时代不清零</b>，所以这条线是"越往后越厚"的复利。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> CultureUpgrades()
    {
        yield return new()
        {
            Id = "oral_tradition",
            Name = Prose.Text("upgrades", "oral_tradition", "name"),
            Icon = Prose.Text("upgrades", "oral_tradition", "icon"),
            Description = Prose.Text("upgrades", "oral_tradition", "description"),
            Price = 9_000_000,
            Unlock = UnlockCondition.Counter(CultureModule.CounterKey, 2_000),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.0002, Cap: 2_000, Id: CultureModule.CounterKey)),
            ],
            Category = "culture",
            Tier = 1,
            Tags = ["civ", "culture"],
        };

        yield return new()
        {
            Id = "chronicle",
            Name = Prose.Text("upgrades", "chronicle", "name"),
            Icon = Prose.Text("upgrades", "chronicle", "icon"),
            Description = Prose.Text("upgrades", "chronicle", "description"),
            Price = 4e8,
            Unlock = UnlockCondition.Counter(CultureModule.CounterKey, 20_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00025, Cap: 4_000, Id: CultureModule.CounterKey)),
            ],
            Category = "culture",
            Tier = 2,
            Tags = ["civ", "culture"],
        };

        yield return new()
        {
            Id = "everyone_remembers",
            Name = Prose.Text("upgrades", "everyone_remembers", "name"),
            Icon = Prose.Text("upgrades", "everyone_remembers", "icon"),
            Description = Prose.Text("upgrades", "everyone_remembers", "description"),
            Price = 3e10,
            Unlock = UnlockCondition.Counter(CultureModule.CounterKey, 120_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 40_000, Id: CultureModule.CounterKey)),
                Modifier.GoldenCookieReward(2),
            ],
            Category = "culture",
            Tier = 3,
            Tags = ["civ", "culture"],
        };
    }

    /// <summary>
    /// 互文线：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 文明的隐喻本来就是"上一代的东西喂给下一代"，所以这条线在这里比在别的包更贴题。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> LinkUpgrades()
    {
        yield return new()
        {
            Id = "nest_to_village",
            Name = Prose.Text("upgrades", "nest_to_village", "name"),
            Icon = Prose.Text("upgrades", "nest_to_village", "icon"),
            Description = Prose.Text("upgrades", "nest_to_village", "description"),
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("cat_nest", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "village",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 100, Id: "cat_nest")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["civ", "link"],
        };

        yield return new()
        {
            Id = "market_to_temple",
            Name = Prose.Text("upgrades", "market_to_temple", "name"),
            Icon = Prose.Text("upgrades", "market_to_temple", "icon"),
            Description = Prose.Text("upgrades", "market_to_temple", "description"),
            Price = 9e8,
            Unlock = UnlockCondition.BuildingsAtLeast("market", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "temple",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 100, Id: "market")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["civ", "link"],
        };

        yield return new()
        {
            Id = "academy_to_starport",
            Name = Prose.Text("upgrades", "academy_to_starport", "name"),
            Icon = Prose.Text("upgrades", "academy_to_starport", "icon"),
            Description = Prose.Text("upgrades", "academy_to_starport", "description"),
            Price = 4e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("academy", 50),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "star_port",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "academy")),
            ],
            Category = "link",
            Tier = 3,
            Tags = ["civ", "link"],
        };
    }

    /// <summary>时代线：每个时代各一条，只在那一段里出现——"这一代人做出来的东西"。</summary>
    private static IEnumerable<UpgradeDefinition> EraUpgrades()
    {
        yield return new()
        {
            Id = "first_fire",
            Name = Prose.Text("upgrades", "first_fire", "name"),
            Icon = Prose.Text("upgrades", "first_fire", "icon"),
            Description = Prose.Text("upgrades", "first_fire", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "era",
            Tier = 1,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "the_first_law",
            Name = Prose.Text("upgrades", "the_first_law", "name"),
            Icon = Prose.Text("upgrades", "the_first_law", "icon"),
            Description = Prose.Text("upgrades", "the_first_law", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("village", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "era",
            Tier = 2,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "siege_engineering",
            Name = Prose.Text("upgrades", "siege_engineering", "name"),
            Icon = Prose.Text("upgrades", "siege_engineering", "icon"),
            Description = Prose.Text("upgrades", "siege_engineering", "description"),
            Price = 2e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("city_wall", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.92)],
            Category = "era",
            Tier = 3,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "the_press",
            Name = Prose.Text("upgrades", "the_press", "name"),
            Icon = Prose.Text("upgrades", "the_press", "icon"),
            Description = Prose.Text("upgrades", "the_press", "description"),
            Price = 8e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("academy", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.25),
            ],
            Category = "era",
            Tier = 4,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "dyson_swarm",
            Name = Prose.Text("upgrades", "dyson_swarm", "name"),
            Icon = Prose.Text("upgrades", "dyson_swarm", "icon"),
            Description = Prose.Text("upgrades", "dyson_swarm", "description"),
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("star_port", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.8),
            ],
            Category = "era",
            Tier = 5,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "the_long_record",
            Name = Prose.Text("upgrades", "the_long_record", "name"),
            Icon = Prose.Text("upgrades", "the_long_record", "icon"),
            Description = Prose.Text("upgrades", "the_long_record", "description"),
            Price = 2e12,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 5e6)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00003, Cap: 400_000, Id: CultureModule.CounterKey)),
            ],
            Category = "era",
            Tier = 6,
            Tags = ["civ", "era"],
        };

        yield return new()
        {
            Id = "the_first_promise",
            Name = Prose.Text("upgrades", "the_first_promise", "name"),
            Icon = Prose.Text("upgrades", "the_first_promise", "icon"),
            Description = Prose.Text("upgrades", "the_first_promise", "description"),
            Price = 6e12,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("deep_space_relay", 25)),
            Modifiers = [Modifier.GlobalMultiplier(4), Modifier.PriceMultiplier(0.9)],
            Category = "era",
            Tier = 7,
            Tags = ["civ", "era"],
        };
    }

    /// <summary>
    /// 遗产：用「火种」购买，<b>跨时代保留</b>。这是"时代更替至星际"这个转生语义的落点——
    /// 建筑会消失、货币会归零，只有她学会的东西跟着她走。<para>
    /// 总价 21 点火种，而一次自然游玩在最后一次结算时能拿到远多于这个数（守卫
    /// <c>PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun</c> 每次替你跑）。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> HeritageUpgrades()
    {
        yield return new()
        {
            Id = "remembered_tools",
            Name = Prose.Text("upgrades", "remembered_tools", "name"),
            Icon = Prose.Text("upgrades", "remembered_tools", "icon"),
            Description = Prose.Text("upgrades", "remembered_tools", "description"),
            Price = 1,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "heritage",
            Tier = 1,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "old_blueprints",
            Name = Prose.Text("upgrades", "old_blueprints", "name"),
            Icon = Prose.Text("upgrades", "old_blueprints", "icon"),
            Description = Prose.Text("upgrades", "old_blueprints", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.85)],
            Category = "heritage",
            Tier = 2,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "calloused_hands",
            Name = Prose.Text("upgrades", "calloused_hands", "name"),
            Icon = Prose.Text("upgrades", "calloused_hands", "icon"),
            Description = Prose.Text("upgrades", "calloused_hands", "description"),
            Price = 3,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(3),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "heritage",
            Tier = 3,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "the_same_stars",
            Name = Prose.Text("upgrades", "the_same_stars", "name"),
            Icon = Prose.Text("upgrades", "the_same_stars", "icon"),
            Description = Prose.Text("upgrades", "the_same_stars", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "heritage",
            Tier = 4,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "written_everywhere",
            Name = Prose.Text("upgrades", "written_everywhere", "name"),
            Icon = Prose.Text("upgrades", "written_everywhere", "icon"),
            Description = Prose.Text("upgrades", "written_everywhere", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(5),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00003, Cap: 400_000, Id: CultureModule.CounterKey)),
            ],
            Category = "heritage",
            Tier = 5,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "generation_ships",
            Name = Prose.Text("upgrades", "generation_ships", "name"),
            Icon = Prose.Text("upgrades", "generation_ships", "icon"),
            Description = Prose.Text("upgrades", "generation_ships", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.ClickMultiplier(3)],
            Category = "heritage",
            Tier = 6,
            Tags = ["civ", "heritage"],
        };

        yield return new()
        {
            Id = "she_was_here",
            Name = Prose.Text("upgrades", "she_was_here", "name"),
            Icon = Prose.Text("upgrades", "she_was_here", "icon"),
            Description = Prose.Text("upgrades", "she_was_here", "description"),
            Price = 3,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(8),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "heritage",
            Tier = 7,
            Tags = ["civ", "heritage"],
        };
    }
}
