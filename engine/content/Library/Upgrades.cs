using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 升级表（47 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、按<b>被阅读度</b>成长的"读者"线、
/// 用建筑数量成长的"互文"线、每本书专属的"书稿"线，以及用书签购买并跨书保留的"批注"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对全部包同时成立。
/// </para>
/// <para>
/// <b>读者线的解锁条件挂在被阅读度上，而这个包的被阅读度是会掉的、每次开新书还会清零。</b>
/// 这不是 bug：普通升级本来就会在重启时清空，所以"解锁 → 又锁上"没有任何不一致
/// （与 4A 里"建筑解锁不能用本轮累计"那条完全是两回事——建筑是跨重启保留的）。
/// </para>
/// </summary>
internal static class Upgrades
{
    /// <summary>全部升级。</summary>
    public static UpgradeDefinition[] All =>
    [
        .. BuildingTierUpgrades(),
        .. ClickUpgrades(),
        .. ReadershipUpgrades(),
        .. LinkUpgrades(),
        .. BookUpgrades(),
        .. BookmarkUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "上过架的"),
            (10, 100, "整面墙的"),
            (25, 500, "一排接一排的"),
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
                    Tags = ["library", "tier"],
                };
            }
        }
    }

    /// <summary>提笔线：这个包里手是真的在写字，所以点击比实验室、公司都强。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "nib",
            Name = "换一支笔尖",
            Icon = "🖊️",
            Description = "每次提笔额外获得 1 页。旧的那支被她收在抽屉里，没扔。",
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["library", "click"],
        };

        yield return new()
        {
            Id = "writing_habit",
            Name = "每天三千字",
            Icon = "📅",
            Description = "点击收益 ×2。习惯比灵感可靠，这是她写了十年才知道的。",
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["library", "click"],
        };

        yield return new()
        {
            Id = "typewriter",
            Name = "打字机",
            Icon = "⌨️",
            Description = "点击收益 ×3。键帽被磨得发亮，声音在夜里能传很远。",
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["library", "click"],
        };

        yield return new()
        {
            Id = "the_muse",
            Name = "缪斯",
            Icon = "🕊️",
            Description = "点击收益 ×4，且每次提笔额外获得 1e4 页。她从来不承认有这个人在。",
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(10_000)],
            Category = "click",
            Tier = 4,
            Tags = ["library", "click"],
        };
    }

    /// <summary>
    /// 读者线：由「被阅读度」驱动。<para>
    /// 这是第二资源参与数值的地方——不是拿它买东西，而是<b>让"有人在读"直接变成产能</b>。
    /// 门槛压在几千到几万：被阅读度的均衡值是"240 × 读者建筑数"，
    /// 而它在每次开新书时清零，所以这条线的每一档都是"这一本书里重新攒出来的"。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> ReadershipUpgrades()
    {
        yield return new()
        {
            Id = "reading_group",
            Name = "读书会",
            Icon = "👥",
            Description = "全局产量 +0.2%／每点被阅读度（最多 40,000 点，+80%）。每周三晚上，六个人，同一本书。",
            Price = 8_000_000,
            Unlock = UnlockCondition.Counter(ReadershipModule.CounterKey, 4_000),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 40_000, Id: ReadershipModule.CounterKey)),
            ],
            Category = "reader",
            Tier = 1,
            Tags = ["library", "reader"],
        };

        yield return new()
        {
            Id = "book_club_network",
            Name = "共读网络",
            Icon = "🕸️",
            Description = "全局产量 ×2，增益时长 ×1.3。一本书被同时读到的地方越多，它越不容易散。",
            Price = 400_000_000,
            Unlock = UnlockCondition.Counter(ReadershipModule.CounterKey, 20_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.3),
            ],
            Category = "reader",
            Tier = 2,
            Tags = ["library", "reader"],
        };

        yield return new()
        {
            Id = "everyone_knows",
            Name = "每个人都记得一句",
            Icon = "💬",
            Description = "全局产量 ×2.5、蠹虫奖励 ×2。她没写过那句，但所有人都在引用。",
            Price = 3e10,
            Unlock = UnlockCondition.Counter(ReadershipModule.CounterKey, 60_000),
            Modifiers = [Modifier.GlobalMultiplier(2.5), Modifier.GoldenCookieReward(2)],
            Category = "reader",
            Tier = 3,
            Tags = ["library", "reader"],
        };
    }

    /// <summary>
    /// 互文线：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 图书馆的隐喻本来就是"书与书互相引用"，所以这条线在这里比在别的包更贴题。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> LinkUpgrades()
    {
        yield return new()
        {
            Id = "shelf_to_room",
            Name = "把书架搬进阅览室",
            Icon = "🪑",
            Description = "阅览室产量 +2%／每座书架（上限 +200%）。她把最常被翻的那几本单独摆到了手边。",
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("bookshelf", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "reading_room",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 200, Id: "bookshelf")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["library", "link"],
        };

        yield return new()
        {
            Id = "copier_to_banned",
            Name = "禁书区的复印件",
            Icon = "📠",
            Description = "禁书区产量 +1.5%／每台复印机（上限 +150%）。栅栏挡得住原本，挡不住副本。",
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("copier", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "banned_section",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 150, Id: "copier")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["library", "link"],
        };

        yield return new()
        {
            Id = "index_to_endless",
            Name = "索引到尽头",
            Icon = "🗼",
            Description = "无尽书架产量 +1%／每座索引塔（上限 +300%）。她说找不到的书等于没有。",
            Price = 4e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("index_tower", 50),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "endless_shelf",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "index_tower")),
            ],
            Category = "link",
            Tier = 3,
            Tags = ["library", "link"],
        };
    }

    /// <summary>书稿线：每本书各一条，只在那本书里出现。</summary>
    private static IEnumerable<UpgradeDefinition> BookUpgrades()
    {
        yield return new()
        {
            Id = "first_sentence",
            Name = "第一句话",
            Icon = "✒️",
            Description = "全局产量 ×1.5。那句话是自己掉下来的，她只是接住了。",
            Price = 5_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "book",
            Tier = 1,
            Tags = ["library", "book"],
        };

        yield return new()
        {
            Id = "world_map",
            Name = "世界地图",
            Icon = "🗺️",
            Description = "全局产量 ×2。先把地形画完再放人进去，这一条是从第 1 本的混乱里学来的。",
            Price = 30_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("reading_room", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "book",
            Tier = 2,
            Tags = ["library", "book"],
        };

        yield return new()
        {
            Id = "red_line",
            Name = "书脊上那道红笔",
            Icon = "🖍️",
            Description = "全局产量 ×2，建筑价格 ×0.9。被划掉的那天起，这本书才开始真的被人读。",
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("banned_section", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.9)],
            Category = "book",
            Tier = 3,
            Tags = ["library", "book"],
        };

        yield return new()
        {
            Id = "timeline",
            Name = "时间线校订",
            Icon = "🧵",
            Description = "全局产量 ×2.5、增益时长 ×1.2。合订本最难的部分不是装订，是让四个人的时间对得上。",
            Price = 8e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("printing_house", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.2),
            ],
            Category = "book",
            Tier = 4,
            Tags = ["library", "book"],
        };

        yield return new()
        {
            Id = "last_page",
            Name = "最后一页",
            Icon = "🔖",
            Description = "全局产量 ×3，但增益时长 ×0.8。写完之后她坐了很久，没有立刻合上。",
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("endless_shelf", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.8),
            ],
            Category = "book",
            Tier = 5,
            Tags = ["library", "book"],
        };
    }

    /// <summary>批注：用「书签」购买，<b>跨开新书保留</b>。这是"写新书开新世界观"这个转生语义的落点。</summary>
    private static IEnumerable<UpgradeDefinition> BookmarkUpgrades()
    {
        yield return new()
        {
            Id = "margin_notes",
            Name = "页边的批注",
            Icon = "🗒️",
            Description = "全局产量 +25%。上一轮写在页边的话，这一轮还能看见。",
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "bookmark",
            Tier = 1,
            Tags = ["library", "bookmark"],
        };

        yield return new()
        {
            Id = "known_tropes",
            Name = "已经用过的桥段",
            Icon = "🎭",
            Description = "建筑价格 ×0.8。有些套路用过一次就不用再摸索一遍。",
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "bookmark",
            Tier = 2,
            Tags = ["library", "bookmark"],
        };

        yield return new()
        {
            Id = "your_voice",
            Name = "你的语气",
            Icon = "🗣️",
            Description = "点击收益 ×6、蠹虫奖励 ×1.5。换了很多个世界，第一人称一直没换。",
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "bookmark",
            Tier = 3,
            Tags = ["library", "bookmark"],
        };

        yield return new()
        {
            Id = "old_readers",
            Name = "老读者",
            Icon = "👤",
            Description = "全局产量 ×2、蠹虫奖励 ×1.5。有人每一本都读了，而且每一本都记得上一本。",
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "bookmark",
            Tier = 4,
            Tags = ["library", "bookmark"],
        };

        yield return new()
        {
            Id = "same_shelf",
            Name = "同一个书架",
            Icon = "📚",
            Description = "全局产量 ×2.5、增益时长 ×1.4。五本书排在一起，书脊上是同一个名字。",
            Price = 45,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(9),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "bookmark",
            Tier = 5,
            Tags = ["library", "bookmark"],
        };
    }
}
