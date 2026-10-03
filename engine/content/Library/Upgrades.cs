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
            Name = Prose.Text("upgrades", "nib", "name"),
            Icon = Prose.Text("upgrades", "nib", "icon"),
            Description = Prose.Text("upgrades", "nib", "description"),
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
            Name = Prose.Text("upgrades", "writing_habit", "name"),
            Icon = Prose.Text("upgrades", "writing_habit", "icon"),
            Description = Prose.Text("upgrades", "writing_habit", "description"),
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
            Name = Prose.Text("upgrades", "typewriter", "name"),
            Icon = Prose.Text("upgrades", "typewriter", "icon"),
            Description = Prose.Text("upgrades", "typewriter", "description"),
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
            Name = Prose.Text("upgrades", "the_muse", "name"),
            Icon = Prose.Text("upgrades", "the_muse", "icon"),
            Description = Prose.Text("upgrades", "the_muse", "description"),
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
            Name = Prose.Text("upgrades", "reading_group", "name"),
            Icon = Prose.Text("upgrades", "reading_group", "icon"),
            Description = Prose.Text("upgrades", "reading_group", "description"),
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
            Name = Prose.Text("upgrades", "book_club_network", "name"),
            Icon = Prose.Text("upgrades", "book_club_network", "icon"),
            Description = Prose.Text("upgrades", "book_club_network", "description"),
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
            Name = Prose.Text("upgrades", "everyone_knows", "name"),
            Icon = Prose.Text("upgrades", "everyone_knows", "icon"),
            Description = Prose.Text("upgrades", "everyone_knows", "description"),
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
            Name = Prose.Text("upgrades", "shelf_to_room", "name"),
            Icon = Prose.Text("upgrades", "shelf_to_room", "icon"),
            Description = Prose.Text("upgrades", "shelf_to_room", "description"),
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
            Name = Prose.Text("upgrades", "copier_to_banned", "name"),
            Icon = Prose.Text("upgrades", "copier_to_banned", "icon"),
            Description = Prose.Text("upgrades", "copier_to_banned", "description"),
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
            Name = Prose.Text("upgrades", "index_to_endless", "name"),
            Icon = Prose.Text("upgrades", "index_to_endless", "icon"),
            Description = Prose.Text("upgrades", "index_to_endless", "description"),
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
            Name = Prose.Text("upgrades", "first_sentence", "name"),
            Icon = Prose.Text("upgrades", "first_sentence", "icon"),
            Description = Prose.Text("upgrades", "first_sentence", "description"),
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
            Name = Prose.Text("upgrades", "world_map", "name"),
            Icon = Prose.Text("upgrades", "world_map", "icon"),
            Description = Prose.Text("upgrades", "world_map", "description"),
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
            Name = Prose.Text("upgrades", "red_line", "name"),
            Icon = Prose.Text("upgrades", "red_line", "icon"),
            Description = Prose.Text("upgrades", "red_line", "description"),
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
            Name = Prose.Text("upgrades", "timeline", "name"),
            Icon = Prose.Text("upgrades", "timeline", "icon"),
            Description = Prose.Text("upgrades", "timeline", "description"),
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
            Name = Prose.Text("upgrades", "last_page", "name"),
            Icon = Prose.Text("upgrades", "last_page", "icon"),
            Description = Prose.Text("upgrades", "last_page", "description"),
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
            Name = Prose.Text("upgrades", "margin_notes", "name"),
            Icon = Prose.Text("upgrades", "margin_notes", "icon"),
            Description = Prose.Text("upgrades", "margin_notes", "description"),
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
            Name = Prose.Text("upgrades", "known_tropes", "name"),
            Icon = Prose.Text("upgrades", "known_tropes", "icon"),
            Description = Prose.Text("upgrades", "known_tropes", "description"),
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
            Name = Prose.Text("upgrades", "your_voice", "name"),
            Icon = Prose.Text("upgrades", "your_voice", "icon"),
            Description = Prose.Text("upgrades", "your_voice", "description"),
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
            Name = Prose.Text("upgrades", "old_readers", "name"),
            Icon = Prose.Text("upgrades", "old_readers", "icon"),
            Description = Prose.Text("upgrades", "old_readers", "description"),
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
            Name = Prose.Text("upgrades", "same_shelf", "name"),
            Icon = Prose.Text("upgrades", "same_shelf", "icon"),
            Description = Prose.Text("upgrades", "same_shelf", "description"),
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
