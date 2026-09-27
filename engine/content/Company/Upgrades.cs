using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 升级表（46 条）。<para>
/// 六种写法都在这里：批量生成的设备强化档、士气驱动的"人事"线、
/// 按成就数成长的"文化"线、公司阶段专属升级、以及用期权购买并跨重组保留的"前世经验"。
/// </para>
/// <para>
/// 数值沿用已验证的配方（建筑档 ×2、价格取基准价的 10/100/500 倍），
/// 所以曲线回归对四个包同时成立。
/// </para>
/// </summary>
internal static class Upgrades
{
    /// <summary>全部升级。</summary>
    public static UpgradeDefinition[] All =>
    [
        .. BuildingTierUpgrades(),
        .. ClickUpgrades(),
        .. MoraleUpgrades(),
        .. CultureUpgrades(),
        .. RoundUpgrades(),
        .. PastCompanyUpgrades(),
    ];

    /// <summary>每台设备三档强化：1 台 → ×2、10 台 → ×2、25 台 → ×2。</summary>
    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()
    {
        (int Required, double PriceFactor, string Prefix)[] tiers =
        [
            (1, 10, "正式配齐的"),
            (10, 100, "成排的"),
            (25, 500, "整层楼的"),
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
                    Tags = ["company", "tier"],
                };
            }
        }
    }

    /// <summary>点击线：你自己也得干活。比实验室强，比咖啡馆弱。</summary>
    private static IEnumerable<UpgradeDefinition> ClickUpgrades()
    {
        yield return new()
        {
            Id = "business_card",
            Name = "名片",
            Icon = "💳",
            Description = "每次谈单额外获得 1 点营收。印了一千张，用掉三张。",
            Price = 200,
            Unlock = UnlockCondition.ClicksAtLeast(20),
            Modifiers = [Modifier.ClickFlat(1)],
            Category = "click",
            Tier = 1,
            Tags = ["company", "click"],
        };

        yield return new()
        {
            Id = "pitch_deck",
            Name = "路演 PPT",
            Icon = "📊",
            Description = "点击收益 ×2。第 7 页那张图是你熬了两个晚上画的。",
            Price = 20_000,
            Unlock = UnlockCondition.ClicksAtLeast(200),
            Modifiers = [Modifier.ClickMultiplier(2)],
            Category = "click",
            Tier = 2,
            Tags = ["company", "click"],
        };

        yield return new()
        {
            Id = "personal_brand",
            Name = "个人品牌",
            Icon = "🎙️",
            Description = "点击收益 ×3。客户开始点名要你亲自讲，你分不清这是好事还是坏事。",
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["company", "click"],
        };
    }

    /// <summary>
    /// 人事线：由「士气」驱动。<para>
    /// 这是第二资源真正参与数值的地方——不是拿它买东西，而是<b>让"人还愿意干"变成产能</b>。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> MoraleUpgrades()
    {
        yield return new()
        {
            Id = "team_handbook",
            Name = "员工手册",
            Icon = "📘",
            Description = "全局产量 +0.3%／每点士气（上限 +60%）。终于有人把规矩写下来了。",
            Price = 2_000_000,
            Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, 100),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.003, Cap: 200, Id: MoraleModule.CounterKey)),
            ],
            Category = "morale",
            Tier = 1,
            Tags = ["company", "morale"],
        };

        yield return new()
        {
            Id = "option_pool",
            Name = "期权池",
            Icon = "🎫",
            Description = "全局产量 +0.5%／每点士气（上限 +150%）。把未来的钱分出去，换现在的劲。",
            Price = 60_000_000,
            Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, 500),
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.005, Cap: 300, Id: MoraleModule.CounterKey)),
            ],
            Category = "morale",
            Tier = 2,
            Tags = ["company", "morale"],
        };

        yield return new()
        {
            Id = "severance_protocol",
            Name = "离职补偿",
            Icon = "📦",
            Description = "设备价格 ×0.7，但事故奖励 ×0.8。你开始认真算「最坏情况」了。",
            Price = 40_000_000,
            Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, 800),
            Modifiers = [Modifier.PriceMultiplier(0.7), Modifier.GoldenCookieReward(0.8)],
            Category = "morale",
            Tier = 2,
            Tags = ["company", "morale"],
        };

        yield return new()
        {
            Id = "flex_office",
            Name = "弹性办公",
            Icon = "🏡",
            Description = "全局产量 ×2.2。没人再打卡之后，活反而干完了。",
            Price = 900_000_000,
            Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, 1_500),
            Modifiers = [Modifier.GlobalMultiplier(2.2)],
            Category = "morale",
            Tier = 3,
            Tags = ["company", "morale"],
        };
    }

    /// <summary>文化线：按<b>成就数</b>成长（"牛奶"式加成），公司越像样越有效率。</summary>
    private static IEnumerable<UpgradeDefinition> CultureUpgrades()
    {
        yield return new()
        {
            Id = "culture_deck",
            Name = "文化手册",
            Icon = "📔",
            Description = "全局产量 +5%／每个成就。写下来的东西才叫文化，没写下来的叫习惯。",
            Price = 500_000,
            Unlock = UnlockCondition.AchievementsAtLeast(8),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.05, Cap: 400)),
            ],
            Category = "culture",
            Tier = 1,
            Tags = ["company", "culture"],
        };

        yield return new()
        {
            Id = "okr_alignment",
            Name = "OKR 对齐",
            Icon = "🎯",
            Description = "全局产量 +3%／每个成就（上限 +300%）。现在每个人的目标都挂在墙上。",
            Price = 30_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(18),
            Modifiers =
            [
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.03, Cap: 300)),
            ],
            Category = "culture",
            Tier = 2,
            Tags = ["company", "culture"],
        };

        yield return new()
        {
            Id = "employer_review",
            Name = "雇主口碑",
            Icon = "⭐",
            Description = "事故奖励 ×1.6、事故频率 +30%。好事开始主动找上门了。",
            Price = 2_000_000_000,
            Unlock = UnlockCondition.AchievementsAtLeast(30),
            Modifiers = [Modifier.GoldenCookieReward(1.6), Modifier.GoldenCookieFrequency(1.3)],
            Category = "culture",
            Tier = 3,
            Tags = ["company", "culture"],
        };
    }

    /// <summary>公司阶段专属升级：随重组推进而出现，每一轮只有这一轮才有的东西。</summary>
    private static IEnumerable<UpgradeDefinition> RoundUpgrades()
    {
        yield return new()
        {
            Id = "series_a_fuel",
            Name = "A 轮弹药",
            Icon = "🚀",
            Description = "全局产量 ×1.8。钱到账那天全员加班——这一条开始吃士气。",
            Price = 30_000_000,
            Unlock = UnlockCondition.EraAtLeast(2),
            Modifiers = [Modifier.GlobalMultiplier(1.8)],
            Category = "round",
            Tier = 1,
            Tags = ["company", "round", MoraleModule.CrunchTag],
        };

        yield return new()
        {
            Id = "bell_rehearsal",
            Name = "敲钟彩排",
            Icon = "🔔",
            Description = "全局产量 ×2.5。你对着空椅子练了十七遍「感谢我们的团队」。",
            Price = 400_000_000,
            Unlock = UnlockCondition.EraAtLeast(3),
            Modifiers = [Modifier.GlobalMultiplier(2.5)],
            Category = "round",
            Tier = 2,
            Tags = ["company", "round"],
        };

        yield return new()
        {
            Id = "wolf_culture",
            Name = "狼性文化",
            Icon = "🐺",
            Description = "全局产量 ×3，但增益时长 ×0.7。标语贴满了墙，士气开始往下掉。",
            Price = 6_000_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(MoraleModule.CounterKey, 300)),
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.7),
            ],
            Category = "round",
            Tier = 3,
            Tags = ["company", "round", MoraleModule.CrunchTag],
        };
    }

    /// <summary>
    /// 前世经验：用「期权」购买，<b>跨重组保留</b>。<para>
    /// 这是"重组"这个转生语义的落点——公司没了，但你学会的东西还在。
    /// </para>
    /// </summary>
    private static IEnumerable<UpgradeDefinition> PastCompanyUpgrades()
    {
        yield return new()
        {
            Id = "muscle_memory",
            Name = "肌肉记忆",
            Icon = "💪",
            Description = "全局产量 +25%。手比脑子先想起来怎么干。",
            Price = 2,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(1),
            Modifiers = [Modifier.GlobalMultiplier(1.25)],
            Category = "past",
            Tier = 1,
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "network",
            Name = "人脉",
            Icon = "🤝",
            Description = "设备价格 ×0.8。你知道谁的报价可以砍一半。",
            Price = 4,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(2),
            Modifiers = [Modifier.PriceMultiplier(0.8)],
            Category = "past",
            Tier = 2,
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "first_order",
            Name = "第一笔订单",
            Icon = "🧾",
            Description = "点击收益 ×5、事故奖励 ×1.5。你还留着那张手写的收据。",
            Price = 8,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(4),
            Modifiers = [Modifier.ClickMultiplier(5), Modifier.GoldenCookieReward(1.5)],
            Category = "past",
            Tier = 3,
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "ipo_experience",
            Name = "上市经验",
            Icon = "📈",
            Description = "全局产量 ×2。你记得流程的每一步，包括哪一步会卡住。",
            Price = 16,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(6),
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Category = "past",
            Tier = 4,
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "no_more_crunch",
            Name = "不再加班",
            Icon = "🌙",
            Description = "增益时长 ×1.4。你终于明白，通宵换来的效率是借来的。",
            Price = 30,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(8),
            Modifiers = [new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 1.4)],
            Category = "past",
            Tier = 5,
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "golden_parachute",
            Name = "金降落伞",
            Icon = "🪂",
            Description = "全局产量 ×1.5、事故奖励 ×2。这一次，退路是你自己给自己准备的。",
            Price = 55,
            Currency = UpgradeCurrency.PrestigeChips,
            Persistence = UpgradePersistence.Permanent,
            Unlock = UnlockCondition.PrestigeLevelAtLeast(10),
            Modifiers = [Modifier.GlobalMultiplier(1.5), Modifier.GoldenCookieReward(2)],
            Category = "past",
            Tier = 6,
            Tags = ["company", "past"],
        };
    }
}
