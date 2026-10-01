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
            Name = "一双爪子",
            Icon = "🐾",
            Description = "每次筑巢额外获得 2 点产能。她什么都还没有的时候，这是唯一拿得出来的东西。",
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
            Name = "磨过的石片",
            Icon = "🪨",
            Description = "点击收益 ×2。第一次发现工具可以让自己变强，而不是让自己变累。",
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
            Name = "一支笔",
            Icon = "🖌️",
            Description = "点击收益 ×3，且每次额外获得 5e3 点产能。写下第一个字的那天，她的手第一次不是用来搬石头的。",
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
            Name = "她自己的手",
            Icon = "✋",
            Description = "点击收益 ×4，且每次额外获得 1e7 点产能。每一座建筑的第一块石头都是她亲手放的，这条规矩一直没改。",
            Price = 6e9,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(1e7)],
            Category = "click",
            Tier = 4,
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
            Name = "口耳相传",
            Icon = "🗣️",
            Description = "全局产量 +0.02%／每点文化（最多 2,000 点，+40%）。"
                          + "她开始把「怎么做」讲给下一只猫听，而不用每次都自己示范一遍。",
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
            Name = "编年史",
            Icon = "📜",
            Description = "全局产量 ×2、+0.025%／每点文化（最多 4,000 点，+100%）。"
                          + "第一本按年份排好的册子，翻到哪一年都能对上。",
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
            Name = "每个人都记得一段",
            Icon = "🌍",
            Description = "全局产量 ×3、+0.005%／每点文化（最多 40,000 点，+200%）、天灾奖励 ×2。"
                          + "她没写过那一段，但整颗星球都在讲。",
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
            Name = "从猫窝到村庄",
            Icon = "🏘️",
            Description = "村庄产量 +2%／每座猫窝（上限 +200%）。第一间屋子是照着她的猫窝盖的。",
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
            Name = "集市边上盖起来的神殿",
            Icon = "⛩️",
            Description = "神殿产量 +1.5%／每座集市（上限 +150%）。香火钱是集市收的，神殿只负责记得。",
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
            Name = "学院画出来的航线",
            Icon = "🚀",
            Description = "星港产量 +1%／每座学院（上限 +300%）。星图不是观测出来的，是算出来的。",
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
            Name = "第一堆火",
            Icon = "🔥",
            Description = "全局产量 ×1.5。火让夜晚变成可以干活的时间，也让所有猫窝第一次挨在了一起。",
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
            Name = "第一条规矩",
            Icon = "⚖️",
            Description = "全局产量 ×2。规矩写下来之后就不用每次吵一遍——省下的时间全是产能。",
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
            Name = "攻城术",
            Icon = "🛡️",
            Description = "全局产量 ×2、建筑价格 ×0.92。她修墙是为了不打架，但墙得先打得过别人。",
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
            Name = "印刷术",
            Icon = "🖨️",
            Description = "全局产量 ×2.5、增益时长 ×1.25。一件事能被印出来之后，它就不再只属于知道它的那几个人。",
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
            Name = "戴森环",
            Icon = "🛰️",
            Description = "全局产量 ×3，但增益时长 ×0.8。把整颗恒星围起来之后，她的作息彻底乱了。",
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
            Name = "长记录",
            Icon = "🗄️",
            Description = "全局产量 ×2、+0.003%／每点文化（最多 400,000 点，+1200%）。"
                          + "把所有时代的账本合订在一起，厚得需要一间屋子专门放。",
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
            Name = "第一个承诺",
            Icon = "🤝",
            Description = "全局产量 ×4、建筑价格 ×0.9。她在第 1 层的石堆上说过「我们会走到有星星的地方」，"
                          + "那时候她还不知道星星有多远。",
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
            Name = "记得住的工具",
            Icon = "🧰",
            Description = "全局产量 +25%。下一代的工具是从上一代的形状改出来的，不用从石头重新开始。",
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
            Name = "旧的图纸",
            Icon = "📐",
            Description = "建筑价格 ×0.85。有些弯路走一次就够了，图纸会让下一代直接跳过它们。",
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
            Name = "手上的茧",
            Icon = "🖐️",
            Description = "点击收益 ×6、天灾奖励 ×1.5。换了很多个时代，放第一块石头的动作一直没变。",
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
            Name = "同一片星空",
            Icon = "✨",
            Description = "全局产量 ×2、天灾奖励 ×1.5。她在每一个时代抬头看，看到的都是同一片——这是她唯一确定的事。",
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
            Name = "到处都写着",
            Icon = "🗿",
            Description = "全局产量 ×2.5、+0.002%／每点文化（最多 400,000 点，+800%）。"
                          + "墙上有、碗底有、星星的排列里有。她留下的字比她自己活得久。",
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
            Name = "代际船",
            Icon = "🛳️",
            Description = "全局产量 ×2、点击收益 ×3。船上的人一辈子到不了目的地，他们只是把接力棒递出去。",
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
            Name = "她来过这里",
            Icon = "🐾",
            Description = "全局产量 ×3、增益时长 ×1.4。不是为了留名——是为了让之后来的那只猫知道，"
                          + "这条路有人走通过。",
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
