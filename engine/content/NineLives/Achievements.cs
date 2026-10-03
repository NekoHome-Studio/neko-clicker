using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 成就表：63 个，全部由"只会涨"的指标触发。<para>
/// 其中 36 个是建筑档位、8 个是按纪元推进（"第几次醒来"），
/// 剩下的覆盖赚取、产量、点击与随机事件。
/// </para>
/// <para>
/// 成就同时是"呼噜线"的燃料：<c>Scaling(AchievementCount)</c> 把它们直接换算成全局产量，
/// 所以多解锁一个成就永远划算——这也是纪元完成条件用成就数的原因。
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
        .. BuildingMilestones(),
        .. EarningMilestones(),
        .. EraMilestones(),
        .. InteractionMilestones(),
    ];

    private static IEnumerable<AchievementDefinition> BuildingMilestones()
    {
        int[] tiers = [1, 10, 25];

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
                    Category = "building",
                    Tier = count switch { 1 => 1, 10 => 2, _ => 3 },
                };
            }
        }
    }

    private static IEnumerable<AchievementDefinition> EarningMilestones()
    {
        (double Amount, string Id)[] earnings =
        [
            (1_000, "earn_1e3"),
            (1_000_000, "earn_1e6"),
            (1_000_000_000, "earn_1e9"),
            (1_000_000_000_000, "earn_1e12"),
            (1_000_000_000_000_000, "earn_1e15"),
            (1e18, "earn_1e18"),
            (1e21, "earn_1e21"),
            (1e24, "earn_1e24"),
        ];
        foreach ((double amount, string id) in earnings)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
                Category = "progress",
            };
        }

        (double Cps, string Id)[] cps =
        [
            (1_000_000, "cps_1e6"),
            (1_000_000_000, "cps_1e9"),
            (1_000_000_000_000, "cps_1e12"),
        ];
        foreach ((double value, string id) in cps)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.CpsAtLeast(value),
                Category = "progress",
            };
        }
    }

    /// <summary>按纪元推进的成就：第几次醒来。条件用 EraAtLeast，单调且必达。</summary>
    private static IEnumerable<AchievementDefinition> EraMilestones()
    {
        for (int era = 2; era <= 9; era++)
        {
            string id = $"wake_{era}";
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.EraAtLeast(era),
                Category = "era",
                Tier = era,
            };
        }
    }

    private static IEnumerable<AchievementDefinition> InteractionMilestones()
    {
        (double Count, string Id)[] clicks =
        [
            (100, "click_100"),
            (1_000, "click_1000"),
            (10_000, "click_10000"),
            (100_000, "click_100000"),
        ];
        foreach ((double count, string id) in clicks)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.ClicksAtLeast(count),
                // 少数成就直接给数值：不是每个里程碑都要走"呼噜线"。
                Modifiers = id == "click_10000" ? [Modifier.ClickMultiplier(1.5)] : [],
                Category = "touch",
                Tier = 1,
            };
        }

        (double Count, string Id)[] events =
        [
            (1, "echo_1"),
            (7, "echo_7"),
            (27, "echo_27"),
            (77, "echo_77"),
        ];
        foreach ((double count, string id) in events)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = Prose.Text("achievements", id, "name"),
                Icon = Prose.Text("achievements", id, "icon"),
                Description = Prose.Text("achievements", id, "description"),
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
                Category = "echo",
            };
        }
    }

    private static string Plain(double value)
        => NekoClicker.Core.Numbers.NumFormat.Format(value, NekoClicker.Core.Numbers.NumberStyle.Plain);
}
