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
            Name = Prose.Text("upgrades", "business_card", "name"),
            Icon = Prose.Text("upgrades", "business_card", "icon"),
            Description = Prose.Text("upgrades", "business_card", "description"),
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
            Name = Prose.Text("upgrades", "pitch_deck", "name"),
            Icon = Prose.Text("upgrades", "pitch_deck", "icon"),
            Description = Prose.Text("upgrades", "pitch_deck", "description"),
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
            Name = Prose.Text("upgrades", "personal_brand", "name"),
            Icon = Prose.Text("upgrades", "personal_brand", "icon"),
            Description = Prose.Text("upgrades", "personal_brand", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(1_000),
                UnlockCondition.EraAtLeast(2)),
            Modifiers = [Modifier.ClickMultiplier(3)],
            Category = "click",
            Tier = 3,
            Tags = ["company", "click"],
        };

        // 「点击 × 建筑」的桥：本包点击线只有三档，且清一色是**固定**倍率——
        // 与公司开了多少工位无关。这一条把点击收益接到**持有建筑总数**上
        // （本包唯一使用 ScalingSource.TotalBuildings 的地方），每座 +0.5%、最多算 120 座（+60%）。
        // 上限是作者手册 §3(b) 的硬规则。门槛 80 座是**按实测包络压出来的**：
        // 本包一局（贪心机器人，1.51 游戏小时）最多持有 133 座建筑，是本仓库里最小的那个包络，
        // 所以门槛取 80 才能在"自然游玩里够得着"（133 / 80 ≈ 1.7 倍余量），而不是摆设。
        yield return new()
        {
            Id = "shareholder_paws",
            Name = Prose.Text("upgrades", "shareholder_paws", "name"),
            Icon = Prose.Text("upgrades", "shareholder_paws", "icon"),
            Description = Prose.Text("upgrades", "shareholder_paws", "description"),
            Price = 5_000_000,
            Unlock = UnlockCondition.All(
                UnlockCondition.TotalBuildingsAtLeast(80),
                UnlockCondition.UpgradeOwned("pitch_deck")),
            Modifiers =
            [
                new Modifier(
                    ModifierTarget.ClickPower,
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.TotalBuildings, 0.005, Cap: 120)),
            ],
            Category = "click",
            Tier = 4,
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
            Name = Prose.Text("upgrades", "team_handbook", "name"),
            Icon = Prose.Text("upgrades", "team_handbook", "icon"),
            Description = Prose.Text("upgrades", "team_handbook", "description"),
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
            Name = Prose.Text("upgrades", "option_pool", "name"),
            Icon = Prose.Text("upgrades", "option_pool", "icon"),
            Description = Prose.Text("upgrades", "option_pool", "description"),
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
            Name = Prose.Text("upgrades", "severance_protocol", "name"),
            Icon = Prose.Text("upgrades", "severance_protocol", "icon"),
            Description = Prose.Text("upgrades", "severance_protocol", "description"),
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
            Name = Prose.Text("upgrades", "flex_office", "name"),
            Icon = Prose.Text("upgrades", "flex_office", "icon"),
            Description = Prose.Text("upgrades", "flex_office", "description"),
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
            Name = Prose.Text("upgrades", "culture_deck", "name"),
            Icon = Prose.Text("upgrades", "culture_deck", "icon"),
            Description = Prose.Text("upgrades", "culture_deck", "description"),
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
            Name = Prose.Text("upgrades", "okr_alignment", "name"),
            Icon = Prose.Text("upgrades", "okr_alignment", "icon"),
            Description = Prose.Text("upgrades", "okr_alignment", "description"),
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
            Name = Prose.Text("upgrades", "employer_review", "name"),
            Icon = Prose.Text("upgrades", "employer_review", "icon"),
            Description = Prose.Text("upgrades", "employer_review", "description"),
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
            Name = Prose.Text("upgrades", "series_a_fuel", "name"),
            Icon = Prose.Text("upgrades", "series_a_fuel", "icon"),
            Description = Prose.Text("upgrades", "series_a_fuel", "description"),
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
            Name = Prose.Text("upgrades", "bell_rehearsal", "name"),
            Icon = Prose.Text("upgrades", "bell_rehearsal", "icon"),
            Description = Prose.Text("upgrades", "bell_rehearsal", "description"),
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
            Name = Prose.Text("upgrades", "wolf_culture", "name"),
            Icon = Prose.Text("upgrades", "wolf_culture", "icon"),
            Description = Prose.Text("upgrades", "wolf_culture", "description"),
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
            Tags = ["company", "past"],
        };

        yield return new()
        {
            Id = "network",
            Name = Prose.Text("upgrades", "network", "name"),
            Icon = Prose.Text("upgrades", "network", "icon"),
            Description = Prose.Text("upgrades", "network", "description"),
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
            Name = Prose.Text("upgrades", "first_order", "name"),
            Icon = Prose.Text("upgrades", "first_order", "icon"),
            Description = Prose.Text("upgrades", "first_order", "description"),
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
            Name = Prose.Text("upgrades", "ipo_experience", "name"),
            Icon = Prose.Text("upgrades", "ipo_experience", "icon"),
            Description = Prose.Text("upgrades", "ipo_experience", "description"),
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
            Name = Prose.Text("upgrades", "no_more_crunch", "name"),
            Icon = Prose.Text("upgrades", "no_more_crunch", "icon"),
            Description = Prose.Text("upgrades", "no_more_crunch", "description"),
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
            Name = Prose.Text("upgrades", "golden_parachute", "name"),
            Icon = Prose.Text("upgrades", "golden_parachute", "icon"),
            Description = Prose.Text("upgrades", "golden_parachute", "description"),
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
