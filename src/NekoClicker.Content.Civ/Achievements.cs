using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 成就表（60 条）。<para>
/// 大部分由生成器铺出来（建筑档 / 产能档 / 产量档 / 拍档 / 天灾档 / 时代档 / 文化档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// <b>文化档是这个包唯一一条与本包第二资源直接挂钩的成就线</b>（2500 / 12000 / 80000 / 400000），
/// 而它同时是"文明到底记住了多少"的读数：文化只由五座记录者建筑产出，
/// 所以这一档亮起来的时候，玩家已经能指出是哪几座建筑在养它。
/// </para>
/// <para>
/// 末层的完成条件要求 20 个成就，三个结局都不额外要求成就数——
/// 里程碑只负责让"走得够远"这件事在数值上留痕。
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
        .. DisasterTiers(),
        .. EraTiers(),
        .. CultureTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一座"), (25, "连成一片"), (50, "铺满一整片地")];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int count, string suffix) in tiers)
            {
                yield return new AchievementDefinition
                {
                    Id = $"{building.Id}_x{count}",
                    Name = $"{building.Name}{suffix}",
                    Icon = building.Icon,
                    Description = $"拥有 {count} 座「{building.Name}」。",
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                };
            }
        }
    }

    /// <summary>历史累计产能档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("earned_1e4", "第一堆够烧一夜", 1e4),
            ("earned_1e6", "够盖一座村子", 1e6),
            ("earned_1e8", "够养一座城", 1e8),
            ("earned_1e10", "够修一道望不到头的墙", 1e10),
            ("earned_1e12", "够把知识印成书", 1e12),
            ("earned_1e14", "够点亮一整颗行星", 1e14),
            ("earned_1e16", "够造一支船队", 1e16),
            ("earned_1e18", "够把恒星围起来", 1e18),
            ("earned_1e20", "够重跑一遍文明", 1e20),
            ("earned_1e22", "数字开始不像产能了", 1e22),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🪨",
                Description = $"历史累计产出 {Core.Numbers.NumFormat.Format(amount)}。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产能档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "有人在搬石头", 1e3),
            ("cps_1e6", "有人在赶集", 1e6),
            ("cps_1e9", "整座城在动", 1e9),
            ("cps_1e12", "一整个行星在运转", 1e12),
            ("cps_1e15", "机器开始自己造机器", 1e15),
            ("cps_1e18", "文明自己在跑", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒产出 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>拍（点击）档：这个包的手比别的包硬一点，所以门槛也高一点。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("build_100", "拍出第一个坑", 100),
            ("build_1000", "爪子磨秃了", 1_000),
            ("build_10000", "手比脑子快", 10_000),
            ("build_100000", "停下来就难受", 100_000),
            ("build_1000000", "你成了这条路上的第一块石头", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🐾",
                Description = $"亲手拍下 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>天灾档：从洪水到丰收年，都是同一套随机事件。</summary>
    private static IEnumerable<AchievementDefinition> DisasterTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("disaster_1", "第一次洪水", 1),
            ("disaster_10", "开始会看天", 10),
            ("disaster_50", "学会了提前搬走", 50),
            ("disaster_200", "它认得你的屋顶", 200),
            ("disaster_500", "你在替它记账", 500),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🌊",
                Description = $"经历 {Core.Numbers.NumFormat.Format(count)} 次天灾。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>时代档：每一层一条，最后一条带修饰符（五个时代都走完之后）。</summary>
    private static IEnumerable<AchievementDefinition> EraTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"era_{index}",
                Name = index switch
                {
                    1 => "石堆",
                    2 => "村庄",
                    3 => "城墙",
                    4 => "学院",
                    _ => "星港",
                },
                Icon = index switch
                {
                    1 => "🪨",
                    2 => "🏘️",
                    3 => "🧱",
                    4 => "🏛️",
                    _ => "🚀",
                },
                Description = index switch
                {
                    1 => "她用石头围出一个坑，那一夜没有被雨淋醒。",
                    2 => "路被踩出来了，第一次有人替别人干活。",
                    3 => "墙垒起来了——她说这是为了让里面的人敢睡着。",
                    4 => "她把「为什么」写下来了，知识不必住在某一个脑子里。",
                    _ => "第一艘船往上飞了。文明的最后一道墙是天空。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>文化档：最后一条带修饰符——"记得住"本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> CultureTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("culture_2500", "第一句被传下去的话", 2_500, "她把「火要留着」讲了三遍，第三只猫记住了。"),
            ("culture_12000", "第一本册子", 12_000, "有人在墙上划记号，划满了整整一面。"),
            ("culture_80000", "有人替她记", 80_000, "她发现自己不用再讲第二遍了。"),
            ("culture_400000", "整颗星球都记得", 400_000, "她随便说一句话，第二天会有三个版本流传。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📜",
                Description = $"{note}（文化 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(CultureModule.CounterKey, value),
                Modifiers = id == "culture_400000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
