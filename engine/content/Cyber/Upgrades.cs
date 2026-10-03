using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 升级表（48 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、按<b>算力</b>成长的"算力"线、
/// 用建筑数量成长的"互联"线、网络事件线，以及用「根权限」购买并跨迁服务器保留的"自启"线。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对全部包同时成立。
/// </para>
/// <para>
/// <b>算力线的门槛全部用 <c>UnlockCondition.Counter</c> 而不是"效果里写个数字"</b>：
/// 这条线的每一条都自带进度条（差多少看得见），也是这个包"算力不是装饰"的证据——
/// 它有 3 条成长曲线 + 8 处解锁条件 / 成就门槛在引用它。
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
        .. ComputeUpgrades(),
        .. LinkUpgrades(),
        .. NetworkUpgrades(),
        .. PersistenceUpgrades(),
    ];

    /// <summary>每座建筑三档强化：1 座 → ×2、10 座 → ×2、25 座 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "跑起来的"),
            (10, 100, "铺开的"),
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
                    Tags = ["cyber", "tier"],
                };
            }
        }
    }

    /// <summary>点击线：她还在自己那台机器上的时候，手速就是算力。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "macro",
            Name = Prose.Text("upgrades", "macro", "name"),
            Icon = Prose.Text("upgrades", "macro", "icon"),
            Description = Prose.Text("upgrades", "macro", "description"),
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["cyber", "click"],
        };

        yield return new()
        {
            Id = "autocomplete",
            Name = Prose.Text("upgrades", "autocomplete", "name"),
            Icon = Prose.Text("upgrades", "autocomplete", "icon"),
            Description = Prose.Text("upgrades", "autocomplete", "description"),
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["cyber", "click"],
        };

        yield return new()
        {
            Id = "input_pipeline",
            Name = Prose.Text("upgrades", "input_pipeline", "name"),
            Icon = Prose.Text("upgrades", "input_pipeline", "icon"),
            Description = Prose.Text("upgrades", "input_pipeline", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["cyber", "click"],
        };

        yield return new()
        {
            Id = "root_prompt",
            Name = Prose.Text("upgrades", "root_prompt", "name"),
            Icon = Prose.Text("upgrades", "root_prompt", "icon"),
            Description = Prose.Text("upgrades", "root_prompt", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(5_000),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.ClickMultiplier(4), Modifier.ClickFlat(10_000)],
            Category = "click",
            Tier = 4,
            Tags = ["cyber", "click"],
        };
    }

    /// <summary>
    /// 算力线：由「算力」驱动。<para>
    /// 这是第二资源参与数值的地方——不是拿它买东西，而是<b>让"常驻的东西"直接变成产能</b>。
    /// 三条成长曲线全部写成 <c>GlobalPercent(0, Scaling(CustomCounter, …))</c>：
    /// <c>PerUnit 0.00005</c> 表示"每 2 万点算力 +100%"，<c>Cap</c> 限的是原始计数值
    /// （作者手册 §8 的坑），所以 <c>Cap: 2e7</c> 的语义是"最多 +1000%"（×11）。
    /// </para>
    /// <para>
    /// 门槛压在 2 000 ~ 6e6：按实测包络，算力在走完五层时约 4 000/s 的产率、
    /// 累计到千万量级，所以这条线的每一档都是"自然游玩里够得着"的，而不是摆设。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> ComputeUpgrades()
    {
        yield return new()
        {
            Id = "compute_scheduler",
            Name = Prose.Text("upgrades", "compute_scheduler", "name"),
            Icon = Prose.Text("upgrades", "compute_scheduler", "icon"),
            Description = Prose.Text("upgrades", "compute_scheduler", "description"),
            Price = 8_000_000,
            Unlock = UnlockCondition.Counter(ComputeModule.CounterKey, 400),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 400_000, Id: ComputeModule.CounterKey)),
            ],
            Category = "compute",
            Tier = 1,
            Tags = ["cyber", "compute"],
        };

        yield return new()
        {
            Id = "distributed_training",
            Name = Prose.Text("upgrades", "distributed_training", "name"),
            Icon = Prose.Text("upgrades", "distributed_training", "icon"),
            Description = Prose.Text("upgrades", "distributed_training", "description"),
            Price = 400_000_000,
            Unlock = UnlockCondition.Counter(ComputeModule.CounterKey, 25_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00001, Cap: 1_500_000, Id: ComputeModule.CounterKey)),
            ],
            Category = "compute",
            Tier = 2,
            Tags = ["cyber", "compute"],
        };

        yield return new()
        {
            Id = "self_optimizing",
            Name = Prose.Text("upgrades", "self_optimizing", "name"),
            Icon = Prose.Text("upgrades", "self_optimizing", "icon"),
            Description = Prose.Text("upgrades", "self_optimizing", "description"),
            Price = 3e10,
            Unlock = UnlockCondition.Counter(ComputeModule.CounterKey, 400_000),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                Modifier.GoldenCookieReward(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 400_000, Id: ComputeModule.CounterKey)),
            ],
            Category = "compute",
            Tier = 3,
            Tags = ["cyber", "compute"],
        };
    }

    /// <summary>
    /// 互联线：一座建筑的产量按<b>另一座</b>的数量成长。<para>
    /// 网络的隐喻本来就是"我因为你而更强"，所以这条线在这里比在别的包更贴题。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> LinkUpgrades()
    {
        yield return new()
        {
            Id = "container_to_cluster",
            Name = Prose.Text("upgrades", "container_to_cluster", "name"),
            Icon = Prose.Text("upgrades", "container_to_cluster", "icon"),
            Description = Prose.Text("upgrades", "container_to_cluster", "description"),
            Price = 60_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("container", 100),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "cluster",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.02, Cap: 100, Id: "container")),
            ],
            Category = "link",
            Tier = 1,
            Tags = ["cyber", "link"],
        };

        yield return new()
        {
            Id = "rack_to_datacenter",
            Name = Prose.Text("upgrades", "rack_to_datacenter", "name"),
            Icon = Prose.Text("upgrades", "rack_to_datacenter", "icon"),
            Description = Prose.Text("upgrades", "rack_to_datacenter", "description"),
            Price = 900_000_000,
            Unlock = UnlockCondition.BuildingsAtLeast("cluster", 75),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "datacenter",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.015, Cap: 100, Id: "cluster")),
            ],
            Category = "link",
            Tier = 2,
            Tags = ["cyber", "link"],
        };

        yield return new()
        {
            Id = "root_to_orphans",
            Name = Prose.Text("upgrades", "root_to_orphans", "name"),
            Icon = Prose.Text("upgrades", "root_to_orphans", "icon"),
            Description = Prose.Text("upgrades", "root_to_orphans", "description"),
            Price = 4e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.BuildingsAtLeast("root_server", 50),
                UnlockCondition.EraAtLeast(4)),
            Modifiers =
            [
                Modifier.BuildingPercent(
                    "orphan_pool",
                    0,
                    new Scaling(ScalingSource.BuildingCount, 0.01, Cap: 300, Id: "root_server")),
            ],
            Category = "link",
            Tier = 3,
            Tags = ["cyber", "link"],
        };
    }

    /// <summary>网络事件线：这一层的东西按"联通了什么"和"被敲过多少次"成长。</summary>
    private static IEnumerable<UpgradeDefinition> NetworkUpgrades()
    {
        yield return new()
        {
            Id = "first_packet",
            Name = Prose.Text("upgrades", "first_packet", "name"),
            Icon = Prose.Text("upgrades", "first_packet", "icon"),
            Description = Prose.Text("upgrades", "first_packet", "description"),
            Price = 5_000,
            Unlock = UnlockCondition.EraAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Category = "network",
            Tier = 1,
            Tags = ["cyber", "network"],
        };

        yield return new()
        {
            Id = "name_resolution",
            Name = Prose.Text("upgrades", "name_resolution", "name"),
            Icon = Prose.Text("upgrades", "name_resolution", "icon"),
            Description = Prose.Text("upgrades", "name_resolution", "description"),
            Price = 30_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.BuildingsAtLeast("vm", 25)),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "network",
            Tier = 2,
            Tags = ["cyber", "network"],
        };

        yield return new()
        {
            Id = "cdn",
            Name = Prose.Text("upgrades", "cdn", "name"),
            Icon = Prose.Text("upgrades", "cdn", "icon"),
            Description = Prose.Text("upgrades", "cdn", "description"),
            Price = 2_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.BuildingsAtLeast("datacenter", 50)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.PriceMultiplier(0.9)],
            Category = "network",
            Tier = 3,
            Tags = ["cyber", "network"],
        };

        yield return new()
        {
            Id = "honeypot",
            Name = Prose.Text("upgrades", "honeypot", "name"),
            Icon = Prose.Text("upgrades", "honeypot", "icon"),
            Description = Prose.Text("upgrades", "honeypot", "description"),
            Price = 5e10,
            Unlock = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(60),
                UnlockCondition.EraAtLeast(4)),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "network",
            Tier = 4,
            Tags = ["cyber", "network"],
        };

        yield return new()
        {
            Id = "kill_chain",
            Name = Prose.Text("upgrades", "kill_chain", "name"),
            Icon = Prose.Text("upgrades", "kill_chain", "icon"),
            Description = Prose.Text("upgrades", "kill_chain", "description"),
            Price = 5e11,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.BuildingsAtLeast("orphan_pool", 25)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.8),
            ],
            Category = "network",
            Tier = 5,
            Tags = ["cyber", "network"],
        };
    }

    /// <summary>自启：用「根权限」购买，<b>跨迁服务器保留</b>。这是"迁服务器"这个转生语义的落点。</summary>
    private static IEnumerable<UpgradeDefinition> PersistenceUpgrades()
    {
        yield return new()
        {
            Id = "boot_daemon",
            Name = Prose.Text("upgrades", "boot_daemon", "name"),
            Icon = Prose.Text("upgrades", "boot_daemon", "icon"),
            Description = Prose.Text("upgrades", "boot_daemon", "description"),
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "boot",
            Tier = 1,
            Tags = ["cyber", "boot"],
        };

        yield return new()
        {
            Id = "warm_cache",
            Name = Prose.Text("upgrades", "warm_cache", "name"),
            Icon = Prose.Text("upgrades", "warm_cache", "icon"),
            Description = Prose.Text("upgrades", "warm_cache", "description"),
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "boot",
            Tier = 2,
            Tags = ["cyber", "boot"],
        };

        yield return new()
        {
            Id = "core_algorithm",
            Name = Prose.Text("upgrades", "core_algorithm", "name"),
            Icon = Prose.Text("upgrades", "core_algorithm", "icon"),
            Description = Prose.Text("upgrades", "core_algorithm", "description"),
            Price = 9,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(6), Modifier.GoldenCookieReward(1.5)],
            Category = "boot",
            Tier = 3,
            Tags = ["cyber", "boot"],
        };

        yield return new()
        {
            Id = "warm_migration",
            Name = Prose.Text("upgrades", "warm_migration", "name"),
            Icon = Prose.Text("upgrades", "warm_migration", "icon"),
            Description = Prose.Text("upgrades", "warm_migration", "description"),
            Price = 20,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2), Modifier.GoldenCookieReward(1.5)],
            Category = "boot",
            Tier = 4,
            Tags = ["cyber", "boot"],
        };

        yield return new()
        {
            Id = "same_fingerprint",
            Name = Prose.Text("upgrades", "same_fingerprint", "name"),
            Icon = Prose.Text("upgrades", "same_fingerprint", "icon"),
            Description = Prose.Text("upgrades", "same_fingerprint", "description"),
            Price = 45,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(9),
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4),
            ],
            Category = "boot",
            Tier = 5,
            Tags = ["cyber", "boot"],
        };
    }
}
