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
            Name = "戴上手套",
            Icon = "🧤",
            Description = "每次记录额外获得 1 条数据。规范要求，不是为了她。",
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
            Name = "双手记录",
            Icon = "✍️",
            Description = "点击收益 ×2。左手的字比右手好看。",
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
            Name = "对视",
            Icon = "👁️",
            Description = "点击收益 ×3，但她会记住你看了她多久。",
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
            Name = "伦理章程",
            Icon = "📜",
            Description = "全局产量 +8%／每 100 点伦理值（上限 +80%）。终于有人把规矩写下来了。",
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
            Name = "第五把椅子",
            Icon = "🪑",
            Description = "伦理值产出 +50%／每 1000 点（上限 +300%）。委员会终于坐满了。",
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
            Name = "独立监察",
            Icon = "🕊️",
            Description = "全局产量 +120%。多了一个不归你管的部门。",
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
            Name = "停机协议",
            Icon = "🛑",
            Description = "设备价格 ×0.7，但事故奖励 ×0.8。你开始准备退场了。",
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
            Name = "卡片索引",
            Icon = "🗂️",
            Description = "全局产量 +5%／每个成就。整理过的东西才算数。",
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
            Name = "交叉索引",
            Icon = "🔗",
            Description = "设备产量 +3%／每个成就。两条记录对上的一瞬间，你会觉得值得。",
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
            Name = "结题报告",
            Icon = "📕",
            Description = "事故奖励 ×1.6、事故频率 +30%。最后一次，你想把话说完整。",
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
            Name = "单盲流程",
            Icon = "🕶️",
            Description = "全局产量 ×1.8。她不知道你在记录什么。",
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
            Name = "批次标准化",
            Icon = "📐",
            Description = "全局产量 ×2.5。所有个体长得一样，档案好写多了。",
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
            Name = "知情同意书",
            Icon = "✒️",
            Description = "全局产量 ×3，但增益持续时间 ×0.7。她签了，字很好看。",
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
            Name = "肌肉记忆",
            Icon = "💪",
            Description = "全局产量 +25%。手比脑子先想起来。",
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
            Name = "流程记忆",
            Icon = "🧠",
            Description = "设备价格 ×0.8。你知道哪一步可以省。",
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
            Name = "第一批",
            Icon = "🥚",
            Description = "点击收益 ×5、事故奖励 ×1.5。你还记得她第一次睁眼的样子。",
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
            Name = "第七批",
            Icon = "🗄️",
            Description = "全局产量 ×2。你只做过六批——这个名字是留给还没做的那一批的。",
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
