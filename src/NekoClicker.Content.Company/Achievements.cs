using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（设备档 / 营收档 / 产量档 / 谈单档 / 甲方档 / 重组档 / 士气档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（4 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// </summary>
internal static class Achievements
{
    /// <summary>全部成就。</summary>
    public static AchievementDefinition[] All =>
    [
        .. BuildingTiers(),
        .. EarningTiers(),
        .. ProductionTiers(),
        .. ClickTiers(),
        .. RequirementTiers(),
        .. RoundTiers(),
        .. MoraleTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每台设备三档：1 / 25 / 50 台。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一台"), (25, "成线"), (50, "成规模")];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int count, string suffix) in tiers)
            {
                yield return new AchievementDefinition
                {
                    Id = $"{building.Id}_x{count}",
                    Name = $"{building.Name}{suffix}",
                    Icon = building.Icon,
                    Description = $"拥有 {count} 台「{building.Name}」。",
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                };
            }
        }
    }

    /// <summary>历史累计营收档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("revenue_1e4", "第一笔像样的收入", 1e4),
            ("revenue_1e6", "够发一次工资", 1e6),
            ("revenue_1e8", "够租一层楼", 1e8),
            ("revenue_1e10", "够开一场发布会", 1e10),
            ("revenue_1e12", "够买一栋楼", 1e12),
            ("revenue_1e14", "够收购一家公司", 1e14),
            ("revenue_1e16", "够重来三次", 1e16),
            ("revenue_1e18", "够忘记为什么开始", 1e18),
            ("revenue_1e20", "够把自己买回来", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "💰",
                Description = $"历史累计赚取 {Core.Numbers.NumFormat.Format(amount)} 营收。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "现金流为正", 1e3),
            ("cps_1e6", "一个月回本", 1e6),
            ("cps_1e9", "独角兽的速度", 1e9),
            ("cps_1e12", "城市听得到", 1e12),
            ("cps_1e15", "数字开始不像钱", 1e15),
            ("cps_1e18", "你不再看报表", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📈",
                Description = $"每秒营收达到 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>亲手谈单档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("pitch_100", "亲手谈了一百次", 100),
            ("pitch_1000", "名片发完了", 1_000),
            ("pitch_10000", "嗓子哑了", 10_000),
            ("pitch_100000", "你成了销售本身", 100_000),
            ("pitch_1000000", "客户开始躲你", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🤝",
                Description = $"亲手谈单 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>甲方档案：处理过的「改需求」次数。</summary>
    private static IEnumerable<AchievementDefinition> RequirementTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("requirement_1", "第一次改需求", 1),
            ("requirement_10", "见过十次了", 10),
            ("requirement_50", "应急预案背下来了", 50),
            ("requirement_200", "你开始提前改", 200),
            ("requirement_500", "需求也是数据", 500),
            ("requirement_1000", "甲方的甲方也是甲方", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📩",
                Description = $"处理 {Core.Numbers.NumFormat.Format(count)} 次甲方改需求。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>重组档：每一轮一条，最后一条带修饰符（公司终于开始像样了）。</summary>
    private static IEnumerable<AchievementDefinition> RoundTiers()
    {
        for (int index = 1; index <= 3; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"round_{index}",
                Name = index switch
                {
                    1 => "车库创业",
                    2 => "A 轮",
                    _ => "上市",
                },
                Icon = index switch
                {
                    1 => "🚲",
                    2 => "🚀",
                    _ => "🔔",
                },
                Description = index switch
                {
                    1 => "从一张桌子开始。第一笔订单还没谈成。",
                    2 => "钱进来了，人进来了，加班也进来了。",
                    _ => "走到最后一轮。接下来要决定这家公司是谁的。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 3 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>士气档。最后一条带修饰符——人愿意干本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> MoraleTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("morale_100", "大家还愿意来", 100, "茶水间第一次有人笑出声。"),
            ("morale_500", "有人开始留下", 500, "下班后有人自愿多坐了一会儿。"),
            ("morale_2000", "团队成形", 2_000, "有人开始说「我们」而不是「你们」。"),
            ("morale_8000", "自愿加班", 8_000, "这一次不是被要求的。"),
            ("morale_20000", "不需要打卡", 20_000, "考勤机坏了一个月，没人提。"),
            ("morale_50000", "别人想来的地方", 50_000, "招聘邮箱开始收到主动投递。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "❤️‍🔥",
                Description = $"{note}（士气 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(MoraleModule.CounterKey, value),
                Modifiers = id == "morale_50000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
