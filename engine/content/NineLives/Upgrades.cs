using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 升级表。<para>
/// 四种写法都在这里：批量生成的建筑强化档、按成就数成长的"呼噜线"、
/// 随纪元出现的专属升级、以及用情感能量购买并跨命保留的"前世技能"。
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
        .. PurrUpgrades(),
        .. EraUpgrades(),
        .. PastLifeUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 个 → ×2、10 个 → ×2、25 个 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "熟悉的"),
            (10, 100, "成套的"),
            (25, 500, "传说里的"),
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
                    Unlock = UnlockCondition.All(
                        UnlockCondition.BuildingsAtLeast(building.Id, required),
                        UnlockCondition.EraAtLeast(1)),
                    Modifiers = [Modifier.BuildingMultiplier(building.Id, 2)],
                    Category = $"building:{building.Id}",
                    Tier = required,
                    Tags = ["neko", "tier"],
                };
            }
        }
    }

    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "steady_hand",
            Name = Prose.Text("upgrades", "steady_hand", "name"),
            Icon = Prose.Text("upgrades", "steady_hand", "icon"),
            Description = Prose.Text("upgrades", "steady_hand", "description"),
            Price = 100,
            Unlock = UnlockCondition.ClicksAtLeast(10),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "touch",
            Tier = 1,
            Tags = ["touch"],
        };

        yield return new()
        {
            Id = "both_hands",
            Name = Prose.Text("upgrades", "both_hands", "name"),
            Icon = Prose.Text("upgrades", "both_hands", "icon"),
            Description = Prose.Text("upgrades", "both_hands", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.ClicksAtLeast(100),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "touch",
            Tier = 2,
            Tags = ["touch"],
        };

        yield return new()
        {
            Id = "behind_the_ears",
            Name = Prose.Text("upgrades", "behind_the_ears", "name"),
            Icon = Prose.Text("upgrades", "behind_the_ears", "icon"),
            Description = Prose.Text("upgrades", "behind_the_ears", "description"),
            Price = 500_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(500),
                UnlockCondition.UpgradeOwned("both_hands")),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "touch",
            Tier = 3,
            Tags = ["touch"],
        };

        yield return new()
        {
            Id = "purr_sync",
            Name = Prose.Text("upgrades", "purr_sync", "name"),
            Icon = Prose.Text("upgrades", "purr_sync", "icon"),
            Description = Prose.Text("upgrades", "purr_sync", "description"),
            Price = 50_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(2_000),
                UnlockCondition.UpgradeOwned("behind_the_ears")),
            Modifiers = [Modifier.ClickPercent(0.25)],
            Category = "touch",
            Tier = 4,
            Tags = ["touch"],
        };

        yield return new()
        {
            Id = "nine_tails",
            Name = Prose.Text("upgrades", "nine_tails", "name"),
            Icon = Prose.Text("upgrades", "nine_tails", "icon"),
            Description = Prose.Text("upgrades", "nine_tails", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.UpgradeOwned("purr_sync")),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "touch",
            Tier = 5,
            Tags = ["touch"],
        };
    }

    /// <summary>"呼噜线"：让成就数直接变成产量，多解锁永远划算。</summary>
    private static IEnumerable<UpgradeDefinition> PurrUpgrades()
    {
        yield return new()
        {
            Id = "purr_solo",
            Name = Prose.Text("upgrades", "purr_solo", "name"),
            Icon = Prose.Text("upgrades", "purr_solo", "icon"),
            Description = Prose.Text("upgrades", "purr_solo", "description"),
            Price = 9_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(5),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.01))],
            Category = "purr",
            Tier = 1,
            Tags = ["purr"],
        };

        yield return new()
        {
            Id = "purr_chorus",
            Name = Prose.Text("upgrades", "purr_chorus", "name"),
            Icon = Prose.Text("upgrades", "purr_chorus", "icon"),
            Description = Prose.Text("upgrades", "purr_chorus", "description"),
            Price = 90_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(20),
                UnlockCondition.UpgradeOwned("purr_solo")),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.02)),
                Modifier.ClickMultiplier(1.5),
            ],
            Category = "purr",
            Tier = 2,
            Tags = ["purr"],
        };

        yield return new()
        {
            Id = "world_heartbeat",
            Name = Prose.Text("upgrades", "world_heartbeat", "name"),
            Icon = Prose.Text("upgrades", "world_heartbeat", "icon"),
            Description = Prose.Text("upgrades", "world_heartbeat", "description"),
            Price = 9_000_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.AchievementsAtLeast(40),
                UnlockCondition.UpgradeOwned("purr_chorus")),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.03))],
            Category = "purr",
            Tier = 3,
            Tags = ["purr"],
        };
    }

    /// <summary>随纪元出现的专属升级：只在某一层生效的"那一世的遗产"。</summary>
    private static IEnumerable<UpgradeDefinition> EraUpgrades()
    {
        yield return new()
        {
            Id = "server_overclock",
            Name = Prose.Text("upgrades", "server_overclock", "name"),
            Icon = Prose.Text("upgrades", "server_overclock", "icon"),
            Description = Prose.Text("upgrades", "server_overclock", "description"),
            Price = 200_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("server_farm", 10)),
            Modifiers = [Modifier.BuildingMultiplier("server_farm", 2)],
            Category = "era",
            Tier = 5,
            Tags = ["era"],
        };

        yield return new()
        {
            Id = "eulogy_reader",
            Name = Prose.Text("upgrades", "eulogy_reader", "name"),
            Icon = Prose.Text("upgrades", "eulogy_reader", "icon"),
            Description = Prose.Text("upgrades", "eulogy_reader", "description"),
            Price = 5_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(6),
                UnlockCondition.AchievementsAtLeast(28)),
            Modifiers = [Modifier.GlobalPercent(0.30), Modifier.ClickMultiplier(2)],
            Category = "era",
            Tier = 6,
            Tags = ["era"],
        };

        yield return new()
        {
            Id = "camera_instinct",
            Name = Prose.Text("upgrades", "camera_instinct", "name"),
            Icon = Prose.Text("upgrades", "camera_instinct", "icon"),
            Description = Prose.Text("upgrades", "camera_instinct", "description"),
            Price = 400_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(7),
                UnlockCondition.BuildingsAtLeast("stream_studio", 10)),
            Modifiers = [Modifier.BuildingMultiplier("stream_studio", 2)],
            Category = "era",
            Tier = 7,
            Tags = ["era"],
        };

        yield return new()
        {
            Id = "lucid_dreaming",
            Name = Prose.Text("upgrades", "lucid_dreaming", "name"),
            Icon = Prose.Text("upgrades", "lucid_dreaming", "icon"),
            Description = Prose.Text("upgrades", "lucid_dreaming", "description"),
            Price = 8_000_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(8),
                UnlockCondition.AchievementsAtLeast(42)),
            Modifiers = [new Modifier(ModifierTarget.OfflineEfficiency, ModifierOperation.Multiplicative, 2.5)],
            Category = "era",
            Tier = 8,
            Tags = ["era"],
        };

        yield return new()
        {
            Id = "readership",
            Name = Prose.Text("upgrades", "readership", "name"),
            Icon = Prose.Text("upgrades", "readership", "icon"),
            Description = Prose.Text("upgrades", "readership", "description"),
            Price = 9_000_000_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(9),
                UnlockCondition.AchievementsAtLeast(50)),
            Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.05, Cap: 60))],
            Category = "era",
            Tier = 9,
            Tags = ["era"],
        };

        yield return new()
        {
            Id = "cat_god_whisper",
            Name = Prose.Text("upgrades", "cat_god_whisper", "name"),
            Icon = Prose.Text("upgrades", "cat_god_whisper", "icon"),
            Description = Prose.Text("upgrades", "cat_god_whisper", "description"),
            Price = 500_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.GoldenCookiesAtLeast(5)),
            Modifiers = [Modifier.GlobalMultiplier(1.5), Modifier.GoldenCookieFrequency(1.3)],
            Category = "era",
            Tier = 3,
            Tags = ["era"],
        };
    }

    /// <summary>前世技能：用情感能量购买，跨命保留（转生后依然有效）。</summary>
    private static IEnumerable<UpgradeDefinition> PastLifeUpgrades()
    {
        yield return new()
        {
            Id = "remembered_warmth",
            Name = Prose.Text("upgrades", "remembered_warmth", "name"),
            Icon = Prose.Text("upgrades", "remembered_warmth", "icon"),
            Description = Prose.Text("upgrades", "remembered_warmth", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(3),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "past_life",
            Tier = 1,
            Tags = ["past_life"],
        };

        yield return new()
        {
            Id = "remembered_name",
            Name = Prose.Text("upgrades", "remembered_name", "name"),
            Icon = Prose.Text("upgrades", "remembered_name", "icon"),
            Description = Prose.Text("upgrades", "remembered_name", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(8),
            Modifiers = [Modifier.PriceDiscount(0.12)],
            Category = "past_life",
            Tier = 1,
            Tags = ["past_life"],
        };

        yield return new()
        {
            Id = "remembered_path",
            Name = Prose.Text("upgrades", "remembered_path", "name"),
            Icon = Prose.Text("upgrades", "remembered_path", "icon"),
            Description = Prose.Text("upgrades", "remembered_path", "description"),
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(14),
            Modifiers = [new Modifier(ModifierTarget.OfflineEfficiency, ModifierOperation.Multiplicative, 1.5)],
            Category = "past_life",
            Tier = 2,
            Tags = ["past_life"],
        };

        yield return new()
        {
            Id = "remembered_god",
            Name = Prose.Text("upgrades", "remembered_god", "name"),
            Icon = Prose.Text("upgrades", "remembered_god", "icon"),
            Description = Prose.Text("upgrades", "remembered_god", "description"),
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(30),
            Modifiers = [Modifier.GlobalMultiplier(1.20)],
            Category = "past_life",
            Tier = 2,
            Tags = ["past_life"],
        };

        yield return new()
        {
            Id = "remembered_self",
            Name = Prose.Text("upgrades", "remembered_self", "name"),
            Icon = Prose.Text("upgrades", "remembered_self", "icon"),
            Description = Prose.Text("upgrades", "remembered_self", "description"),
            Price = 45,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeChipsAtLeast(60),
            Modifiers = [Modifier.GlobalMultiplier(1.35), Modifier.ClickMultiplier(5)],
            Category = "past_life",
            Tier = 3,
            Tags = ["past_life"],
        };
    }
}
