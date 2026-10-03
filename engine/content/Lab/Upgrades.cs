using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 升级表（46 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、伦理值驱动的"守则"线、
/// 按成就数成长的"档案"线、批次专属升级、以及用残留记忆购买并跨批次保留的"前世技能"。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对三个包同时成立。
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
        .. EthicsUpgrades(),
        .. ArchiveUpgrades(),
        .. BatchUpgrades(),
        .. PastBatchUpgrades(),
    ];

    /// <summary>每台设备三档强化：1 台 → ×2、10 台 → ×2、25 台 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "校准过的"),
            (10, 100, "成批的"),
            (25, 500, "停不下来的"),
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
                    Tags = ["lab", "tier"],
                };
            }
        }
    }

    /// <summary>点击线：研究员的手。刻意做得比别的包弱——这里不是你在干活。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "gloved_hand",
            Name = Prose.Text("upgrades", "gloved_hand", "name"),
            Icon = Prose.Text("upgrades", "gloved_hand", "icon"),
            Description = Prose.Text("upgrades", "gloved_hand", "description"),
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["lab", "click"],
        };

        yield return new()
        {
            Id = "two_hands_log",
            Name = Prose.Text("upgrades", "two_hands_log", "name"),
            Icon = Prose.Text("upgrades", "two_hands_log", "icon"),
            Description = Prose.Text("upgrades", "two_hands_log", "description"),
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["lab", "click"],
        };

        yield return new()
        {
            Id = "eye_contact",
            Name = Prose.Text("upgrades", "eye_contact", "name"),
            Icon = Prose.Text("upgrades", "eye_contact", "icon"),
            Description = Prose.Text("upgrades", "eye_contact", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["lab", "click"],
        };
    }

    /// <summary>
    /// 伦理线：由"在场者"累积的伦理值驱动。<para>
    /// 这是第二资源真正参与数值的地方——不是拿它买东西，而是<b>让克制变成产能</b>。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> EthicsUpgrades()
    {
        yield return new()
        {
            Id = "ethics_charter",
            Name = Prose.Text("upgrades", "ethics_charter", "name"),
            Icon = Prose.Text("upgrades", "ethics_charter", "icon"),
            Description = Prose.Text("upgrades", "ethics_charter", "description"),
            Price = 2_000_000,
            Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, 100),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.08, Cap: 80, Id: EthicsModule.CounterKey)),
            ],
            Category = "ethics",
            Tier = 1,
            Tags = ["lab", "ethics"],
        };

        yield return new()
        {
            Id = "fifth_seat",
            Name = Prose.Text("upgrades", "fifth_seat", "name"),
            Icon = Prose.Text("upgrades", "fifth_seat", "icon"),
            Description = Prose.Text("upgrades", "fifth_seat", "description"),
            Price = 60_000_000,
            Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, 500),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "ethics_board",
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.5, Cap: 300, Id: EthicsModule.CounterKey)),
            ],
            Category = "ethics",
            Tier = 2,
            Tags = ["lab", "ethics"],
        };

        yield return new()
        {
            Id = "independent_watch",
            Name = Prose.Text("upgrades", "independent_watch", "name"),
            Icon = Prose.Text("upgrades", "independent_watch", "icon"),
            Description = Prose.Text("upgrades", "independent_watch", "description"),
            Price = 900_000_000,
            Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, 1500),
            Modifiers = [Modifier.GlobalMultiplier(2.2)],
            Category = "ethics",
            Tier = 3,
            Tags = ["lab", "ethics"],
        };

        yield return new()
        {
            Id = "decommission_protocol",
            Name = Prose.Text("upgrades", "decommission_protocol", "name"),
            Icon = Prose.Text("upgrades", "decommission_protocol", "icon"),
            Description = Prose.Text("upgrades", "decommission_protocol", "description"),
            Price = 40_000_000,
            Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, 800),
            Modifiers = [Modifier.PriceMultiplier(0.7), Modifier.GoldenCookieReward(0.8)],
            Category = "ethics",
            Tier = 2,
            Tags = ["lab", "ethics"],
        };
    }

    /// <summary>档案线：按<b>成就数</b>成长（"牛奶"式加成），越像研究机构越有效率。</summary>
    private static IEnumerable<UpgradeDefinition> ArchiveUpgrades()
    {
        yield return new()
        {
            Id = "card_index",
            Name = Prose.Text("upgrades", "card_index", "name"),
            Icon = Prose.Text("upgrades", "card_index", "icon"),
            Description = Prose.Text("upgrades", "card_index", "description"),
            Price = 500_000,
            Unlock = UnlockCondition.AchievementsAtLeast(8),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.05, Cap: 400)),
            ],
            Category = "archive",
            Tier = 1,
            Tags = ["lab", "archive"],
        };

        yield return new()
        {
            Id = "cross_reference",
            Name = Prose.Text("upgrades", "cross_reference", "name"),
            Icon = Prose.Text("upgrades", "cross_reference", "icon"),
            Description = Prose.Text("upgrades", "cross_reference", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(18),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.03, Cap: 300)),
            ],
            Category = "archive",
            Tier = 2,
            Tags = ["lab", "archive"],
        };

        yield return new()
        {
            Id = "final_report",
            Name = Prose.Text("upgrades", "final_report", "name"),
            Icon = Prose.Text("upgrades", "final_report", "icon"),
            Description = Prose.Text("upgrades", "final_report", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(30),
            Modifiers = [Modifier.GoldenCookieReward(1.6), Modifier.GoldenCookieFrequency(1.3)],
            Category = "archive",
            Tier = 3,
            Tags = ["lab", "archive"],
        };
    }

    /// <summary>批次专属升级：随纪元出现，做成"每一批只有这一批才有"的东西。</summary>
    private static IEnumerable<UpgradeDefinition> BatchUpgrades()
    {
        yield return new()
        {
            Id = "single_blind",
            Name = Prose.Text("upgrades", "single_blind", "name"),
            Icon = Prose.Text("upgrades", "single_blind", "icon"),
            Description = Prose.Text("upgrades", "single_blind", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.EraAtLeast(2),
            Modifiers = [Modifier.GlobalMultiplier(1.8)],
            Category = "batch",
            Tier = 1,
            Tags = ["lab", "batch"],
        };

        yield return new()
        {
            Id = "batch_standardization",
            Name = Prose.Text("upgrades", "batch_standardization", "name"),
            Icon = Prose.Text("upgrades", "batch_standardization", "icon"),
            Description = Prose.Text("upgrades", "batch_standardization", "description"),
            Price = 400_000_000,
            Unlock = UnlockCondition.EraAtLeast(3),
            Modifiers = [Modifier.GlobalMultiplier(2.5)],
            Category = "batch",
            Tier = 2,
            Tags = ["lab", "batch"],
        };

        yield return new()
        {
            Id = "informed_consent",
            Name = Prose.Text("upgrades", "informed_consent", "name"),
            Icon = Prose.Text("upgrades", "informed_consent", "icon"),
            Description = Prose.Text("upgrades", "informed_consent", "description"),
            Price = 6_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(EthicsModule.CounterKey, 300)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.7),
            ],
            Category = "batch",
            Tier = 3,
            Tags = ["lab", "batch"],
        };
    }

    /// <summary>
    /// 前世技能：用「残留记忆」购买，<b>跨批次保留</b>。<para>
    /// 这是"残留记忆"这个转生语义的落点——你丢掉了设备，但记住了怎么做。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> PastBatchUpgrades()
    {
        yield return new()
        {
            Id = "muscle_memory",
            Name = Prose.Text("upgrades", "muscle_memory", "name"),
            Icon = Prose.Text("upgrades", "muscle_memory", "icon"),
            Description = Prose.Text("upgrades", "muscle_memory", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "past",
            Tier = 1,
            Tags = ["lab", "past"],
        };

        yield return new()
        {
            Id = "protocol_memory",
            Name = Prose.Text("upgrades", "protocol_memory", "name"),
            Icon = Prose.Text("upgrades", "protocol_memory", "icon"),
            Description = Prose.Text("upgrades", "protocol_memory", "description"),
            Price = 5,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "past",
            Tier = 2,
            Tags = ["lab", "past"],
        };

        yield return new()
        {
            Id = "first_batch",
            Name = Prose.Text("upgrades", "first_batch", "name"),
            Icon = Prose.Text("upgrades", "first_batch", "icon"),
            Description = Prose.Text("upgrades", "first_batch", "description"),
            Price = 12,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(5), Modifier.GoldenCookieReward(1.5)],
            Category = "past",
            Tier = 3,
            Tags = ["lab", "past"],
        };

        yield return new()
        {
            Id = "seventh_batch",
            Name = Prose.Text("upgrades", "seventh_batch", "name"),
            Icon = Prose.Text("upgrades", "seventh_batch", "icon"),
            Description = Prose.Text("upgrades", "seventh_batch", "description"),
            Price = 28,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "past",
            Tier = 4,
            Tags = ["lab", "past"],
        };
    }
}
