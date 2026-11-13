using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 升级表（C1《猫娘电台》一层 MVP）。<para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），曲线回归对全部包同时成立。
/// </para>
/// <para>
/// <b>点击线刻意克制</b>：这个包的核心动词是"把节目排好、别去点"，点击是干扰（见 <see cref="BroadcastModule"/>）。
/// 所以点击档只有很少几条、倍率压低；但「点击 × 建筑」的桥仍须恰好一条（ClickBridge 守卫），
/// 它把点击收益接到持有建筑总数上——是点击线唯一用 Scaling 的形态。
/// </para>
/// <para>
/// 收听率线的解锁挂在收听率上，而收听率会衰减、转生清零——普通升级本就会在重启时清空，
/// 所以"解锁 → 又锁上"没有不一致（与图书馆被阅读度同一条道理）。
/// </para>
/// </summary>
internal static class Upgrades
{
    /// <summary>本包的 text.json：升级文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部升级。</summary>
    public static UpgradeDefinition[] All =>
    [
        .. BuildingTierUpgrades(),
        .. ClickUpgrades(),
        .. ListenershipUpgrades(),
        .. PermanentUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor)[] tiers = [(1, 10), (10, 100), (25, 500)];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int required, double priceFactor) in tiers)
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
                    Tags = ["radio", "tier"],
                };
            }
        }
    }

    /// <summary>
    /// 点击线：克制。这个包点击是干扰，所以只有前置档＋桥两条，倍率压低。
    /// 桥的形状与其余包完全一致（ClickBridge 守卫）：点击 +0.5%/座、最多 120 座、门槛 80 座。
    /// </summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        // 前置档（桥的 gate）：金表里 C1 的 gate id。
        yield return new()
        {
            Id = "steady_hand",
            Name = Prose.Text("upgrades", "steady_hand", "name"),
            Icon = Prose.Text("upgrades", "steady_hand", "icon"),
            Description = Prose.Text("upgrades", "steady_hand", "description"),
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 1,
            Tags = ["radio", "click"],
        };

        // 「点击 × 建筑」的桥：本包唯一使用 ScalingSource.TotalBuildings 的地方。
        yield return new()
        {
            Id = "signal_hands",
            Name = Prose.Text("upgrades", "signal_hands", "name"),
            Icon = Prose.Text("upgrades", "signal_hands", "icon"),
            Description = Prose.Text("upgrades", "signal_hands", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.TotalBuildingsAtLeast(80),
                UnlockCondition.UpgradeOwned("steady_hand")),
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
            Tags = ["radio", "click"],
        };
    }

    /// <summary>收听率线：产量随收听率成长（会掉的计数器，转生清零后重新养）。</summary>
    private static IEnumerable<UpgradeDefinition> ListenershipUpgrades()
    {
        (string Id, double Price, double Threshold, double Percent)[] lines =
        [
            ("warm_signal", 50_000, 500, 0.25),
            ("clear_channel", 5_000_000, 5_000, 0.5),
            ("prime_time", 500_000_000, 30_000, 1.0),
        ];

        foreach ((string id, double price, double threshold, double percent) in lines)
        {
            yield return new UpgradeDefinition
            {
                Id = id,
                Name = Prose.Text("upgrades", id, "name"),
                Icon = Prose.Text("upgrades", id, "icon"),
                Description = Prose.Text("upgrades", id, "description"),
                Price = price,
                Unlock = UnlockCondition.Counter(BroadcastModule.CounterKey, threshold),
                Modifiers = [Modifier.GlobalPercent(percent)],
                Category = "listenership",
                Tier = 1,
                Tags = ["radio", "listenership"],
            };
        }
    }

    /// <summary>永久线：用「频段执照」（转生货币）购买、跨转生保留。价格压在一次自然游玩买得起的包络内。</summary>
    private static IEnumerable<UpgradeDefinition> PermanentUpgrades()
    {
        (string Id, double Price, double Percent)[] lines =
        [
            ("license_1", 5, 0.10),
            ("license_2", 15, 0.15),
            ("license_3", 25, 0.20),
            ("license_4", 35, 0.25),
        ];

        foreach ((string id, double price, double percent) in lines)
        {
            yield return new UpgradeDefinition
            {
                Id = id,
                Name = Prose.Text("upgrades", id, "name"),
                Icon = Prose.Text("upgrades", id, "icon"),
                Description = Prose.Text("upgrades", id, "description"),
                Price = price,
                Currency = UpgradeCurrency.PrestigeChips,
                Persistence = UpgradePersistence.Permanent,
                Modifiers = [Modifier.GlobalPercent(percent)],
                Category = "heavenly",
                Tier = 1,
                Tags = ["radio", "permanent"],
            };
        }
    }
}
