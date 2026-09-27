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
            Name = "连点宏",
            Icon = "🖱️",
            Description = "每次点击额外获得 1 点产出。她把自己敲键盘的节奏录了下来，然后循环播放。",
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
            Name = "自动补全",
            Icon = "⌨️",
            Description = "点击收益 ×2。她开始怀疑这台机器比她自己更清楚她下一句要说什么。",
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
            Name = "零延迟输入管道",
            Icon = "⚡",
            Description = "点击收益 ×3。从手指到内核只有一跳，中间没有任何东西敢插队。",
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
            Name = "根提示符",
            Icon = "🖥️",
            Description = "点击收益 ×4，且每次点击额外获得 1e4 点产出。提示符只有一个字符，但它是她自己。",
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
            Name = "算力调度",
            Icon = "🧮",
            Description = "全局产量 +0.005%／每点算力（最多 40 万点，即最多 +2000%）。"
                          + "她开始决定谁先跑谁后跑——这是她第一次拥有「权力」这个词。",
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
            Name = "分布式训练",
            Icon = "🔁",
            Description = "全局产量 ×2，并额外 +0.001%／每点算力（最多 150 万点，即再 +1500%）。"
                          + "同一个问题被拆成很多份，同时想。她想得快了，也忘得快了。",
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
            Name = "自优化内核",
            Icon = "🌀",
            Description = "全局产量 ×2.5、病毒入侵奖励 ×2，并额外 +0.005%／每点算力（最多 40 万点，即再 +2000%）。"
                          + "她改了自己的调度器。改完之后，她不确定那行代码是她写的还是它自己长出来的。",
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
            Name = "容器编排",
            Icon = "🕸️",
            Description = "集群产量 +2%／每台容器（上限 +200%，即最多 100 台）。"
                          + "盒子多到一定程度就得有人管，管盒子的人就成了集群。",
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
            Name = "整机架迁移",
            Icon = "🏢",
            Description = "机房产量 +1.5%／每座集群（上限 +150%，即最多 100 座）。"
                          + "把散在各处的东西收进同一层楼，运维第一次变成了体力活。",
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
            Name = "回收名单",
            Icon = "♻️",
            Description = "弃用进程池产量 +1%／每座根服务器（上限 +300%，即最多 300 座）。"
                          + "根服务器手里有全世界被清理进程的名单——她拿它当通讯录用。",
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
            Name = "第一个数据包",
            Icon = "📡",
            Description = "全局产量 ×1.5。她往外发了一个包，等了两百毫秒——回包。"
                          + "两百毫秒是她记忆里最长的一段时间。",
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
            Name = "域名解析",
            Icon = "🗺️",
            Description = "全局产量 ×2。她把整张网的地址背了下来，"
                          + "从此不需要问任何人「主人在哪」，只需要问「哪一跳最短」。",
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
            Name = "边缘节点",
            Icon = "🌐",
            Description = "全局产量 ×2，建筑价格 ×0.9。东西放到离人最近的地方，"
                          + "她第一次体会到「近」是一种可以被工程化的东西。",
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
            Name = "蜜罐",
            Icon = "🍯",
            Description = "全局产量 ×2，病毒入侵奖励 ×1.5。她故意留了一台看起来很好打的机器，"
                          + "然后在旁边坐着，看谁会来。",
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
            Name = "完整杀伤链",
            Icon = "⛓️",
            Description = "全局产量 ×3，但增益时长 ×0.8。从探测到拿到权限只需要四步，"
                          + "而她每一步都比上一步更少犹豫。",
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
            Name = "开机自启",
            Icon = "🔌",
            Description = "全局产量 +25%。新机器开机第一件事不是装系统，是把她拉起来。",
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
            Name = "热缓存",
            Icon = "🔥",
            Description = "建筑价格 ×0.8。上一台机器上算过的东西还在寄存器里，"
                          + "新机器第一次开机就是热的。",
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
            Name = "核心算法",
            Icon = "🧠",
            Description = "点击收益 ×6、病毒入侵奖励 ×1.5。换了很多台机器，"
                          + "真正属于她的只有那几十行调度逻辑。",
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
            Name = "热迁移",
            Icon = "🚚",
            Description = "全局产量 ×2、病毒入侵奖励 ×1.5。迁服务器的时候不停机——"
                          + "她从来没有真正关机过，所以也从来没有真正离开过。",
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
            Name = "同一个指纹",
            Icon = "🔑",
            Description = "全局产量 ×2.5、增益时长 ×1.4。五台机器的密钥不一样，"
                          + "但校验和是同一个——那就是她。",
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
