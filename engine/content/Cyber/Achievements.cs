using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 成就表（74 条）。<para>
/// 大部分由生成器铺出来（进程档 / 产出档 / 产量档 / 点击档 / 病毒档 / 算力档 / 层数档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（2 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>算力档是这个包唯一一条"按第二资源给内容"的档</b>：它既当成就门槛（8 处），
/// 也在最后一条给修饰符（"算力本身开始变成产能"）。这是"算力不是装饰"这条要求的
/// 第二处落点（第一处是升级线的三条成长曲线）。
/// </para>
/// </summary>
internal static class Achievements
{
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All =>
    [
        .. BuildingTiers(),
        .. EarningTiers(),
        .. ProductionTiers(),
        .. ClickTiers(),
        .. VirusTiers(),
        .. ComputeTiers(),
        .. LayerTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        int[] tiers = [1, 25, 50];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach (int count in tiers)
            {
                string id = $"{building.Id}_x{count}";
                yield return new AchievementDefinition
                {
                    Id = id,
                    Name = Prose.Text("achievements", id, "name"),
                    Icon = Prose.Text("achievements", id, "icon"),
                    Description = Prose.Text("achievements", id, "description"),
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                };
            }
        }
    }

    /// <summary>历史累计产出档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, double Amount)[] tiers =
        [
            ("bits_1e4", 1e4),
            ("bits_1e6", 1e6),
            ("bits_1e8", 1e8),
            ("bits_1e10", 1e10),
            ("bits_1e12", 1e12),
            ("bits_1e14", 1e14),
            ("bits_1e16", 1e16),
            ("bits_1e18", 1e18),
            ("bits_1e20", 1e20),
        ];

        foreach ((string id, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("cps_1e3", 1e3),
            ("cps_1e6", 1e6),
            ("cps_1e9", 1e9),
            ("cps_1e12", 1e12),
            ("cps_1e15", 1e15),
            ("cps_1e18", 1e18),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>点击档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("tap_100", 100),
            ("tap_1000", 1_000),
            ("tap_10000", 10_000),
            ("tap_100000", 100_000),
            ("tap_1000000", 1_000_000),
        ];

        foreach ((string id, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>病毒档案：病毒入侵撞进来的次数。</summary>
    private static IEnumerable<AchievementDefinition> VirusTiers()
    {
        (string Id, double Count)[] tiers =
        [
            ("virus_1", 1),
            ("virus_10", 10),
            ("virus_50", 50),
            ("virus_200", 200),
            ("virus_500", 500),
            ("virus_1000", 1_000),
        ];

        foreach ((string id, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>
    /// 算力档。最后一条带修饰符——<b>算力本身开始变成产能</b>。<para>
    /// 门槛按实测包络铺（<c>EnvelopeProbe</c>）：第 1 层结束（约 0.4h）时算力 1.7e3、
    /// 第 3 层（1.5h）1.4e4、第 4 层（2.5h）1.5e5、第 5 层 3 小时 1.9e5、4.5 小时 2.0e6、
    /// 12 小时 1.9e7。所以最后一档压在 1.2e6——它在第 5 层的中段拿得到，
    /// 而不是变成一条跑两小时也追不上的尾巴。
    /// </para>
    /// </summary>
    private static IEnumerable<AchievementDefinition> ComputeTiers()
    {
        (string Id, double Value)[] tiers =
        [
            ("compute_500", 500),
            ("compute_5e3", 5e3),
            ("compute_5e4", 5e4),
            ("compute_2e5", 2e5),
            ("compute_1.2e6", 1.2e6),
            ("compute_1.2e7", 1.2e7),
            ("compute_1.2e8", 1.2e8),
        ];

        foreach ((string id, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.Counter(ComputeModule.CounterKey, value),
                Modifiers = id == "compute_1.2e8" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }

    /// <summary>层数档：每一层一条，最后一条带修饰符（爬到根层之后）。</summary>
    private static IEnumerable<AchievementDefinition> LayerTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            string id = $"layer_{index}";
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }
}
