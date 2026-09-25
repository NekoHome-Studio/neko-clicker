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
                yield return new UpgradeDefinition
                {
                    Id = $"{building.Id}_tier{required}",
                    Name = $"{prefix}{building.Name}",
                    Icon = building.Icon,
                    Description = $"「{building.Name}」的产量翻倍。",
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
            Name = "稳住的手",
            Icon = "🖐️",
            Description = "每次摸头额外获得 1 条小鱼干。",
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
            Name = "两只手都用上",
            Icon = "🤲",
            Description = "点击收益 ×2。她说这样比较舒服。",
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
            Name = "耳后那块",
            Icon = "🐱",
            Description = "点击收益 ×3。据说那里是猫神的接口。",
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
            Name = "呼噜同步",
            Icon = "💤",
            Description = "点击收益 +25%。你和她的心跳对上了。",
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
            Name = "九条尾巴",
            Icon = "✨",
            Description = "点击收益 ×3。摸一次，等于摸了九条命。",
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
            Name = "一只猫的呼噜",
            Icon = "🎵",
            Description = "每个成就让所有建筑产量 +1%。",
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
            Name = "一屋子的呼噜",
            Icon = "🎶",
            Description = "每个成就让所有建筑产量 +2%，点击收益 ×1.5。",
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
            Name = "世界的心跳",
            Icon = "💗",
            Description = "每个成就让所有建筑产量 +3%。呼噜声就是这个世界的地基。",
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
            Name = "超频许可",
            Icon = "⚡",
            Description = "服务器农场产量 ×2。赛博纪元特有的权限。",
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
            Name = "悼词朗读",
            Icon = "🕯️",
            Description = "所有建筑产量 +30%，点击收益 ×2。念出来的名字才不算消失。",
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
            Name = "镜头直觉",
            Icon = "🎥",
            Description = "直播间产量 ×2。她知道哪个角度最像神。",
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
            Name = "清醒梦",
            Icon = "🌀",
            Description = "离线收益效率 ×2.5。梦里也在场。",
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
            Name = "被阅读的权利",
            Icon = "📖",
            Description = "每个成就让所有建筑产量 +5%。被读到的部分才存在。",
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
            Name = "猫神的低语",
            Icon = "🌙",
            Description = "所有建筑产量 ×1.5，随机事件更频繁。寐娅在九层之外看着你。",
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
            Name = "记得的温度",
            Icon = "🔥",
            Description = "点击收益 ×3。跨越九次转生都不肯凉掉的东西。",
            Price = 3,
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
            Name = "记得的名字",
            Icon = "🏷️",
            Description = "所有建筑价格 −12%。有人叫得出你的名字，你就不能算消失。",
            Price = 8,
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
            Name = "记得的路",
            Icon = "🧭",
            Description = "离线收益效率 +50%。第九次醒来时你还认得方向。",
            Price = 14,
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
            Name = "记得的神",
            Icon = "🌟",
            Description = "所有建筑产量 ×1.20。你已经想起来了她是谁。",
            Price = 30,
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
            Name = "记得的自己",
            Icon = "🪞",
            Description = "所有建筑产量 ×1.35、点击 ×5。九条命记住了同一个人。",
            Price = 60,
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
