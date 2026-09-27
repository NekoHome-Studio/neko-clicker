using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 成就表（71 条）。<para>
/// 大部分由生成器铺出来（神庙档 / 香火档 / 产量档 / 显灵档 / 神迹档 / 神话档 / 信仰档 / 在线档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（3 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>信仰档与在线档的最后一档刻意压在末层门槛之下</b>：信仰只涨不花，它的量级被五层纪元
/// 的门槛钉在同一个包络里，所以成就门槛只要贴着包络设就一定能拿到（对照阶段 4B：
/// 那一批"最高 200 万"的记忆档就是没贴着包络设，实测全是死内容）。
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
        .. MiracleTiers(),
        .. MythTiers(),
        .. FaithTiers(),
        .. ViewerTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一座"), (25, "香火鼎盛"), (50, "自成一派")];

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

    /// <summary>历史累计香火档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("incense_1e4", "够点一整年的香", 1e4),
            ("incense_1e6", "够翻修一次屋顶", 1e6),
            ("incense_1e8", "够盖一座像样的神殿", 1e8),
            ("incense_1e10", "够养活一个祭司团", 1e10),
            ("incense_1e12", "够买下一座城", 1e12),
            ("incense_1e14", "够做一次全球投放", 1e14),
            ("incense_1e16", "够供五套神话同时开张", 1e16),
            ("incense_1e18", "够把神龛开成连锁", 1e18),
            ("incense_1e20", "数字开始不像钱了", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🕯️",
                Description = $"历史累计收到 {Core.Numbers.NumFormat.Format(amount)} 点香火。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "有人开始许愿", 1e3),
            ("cps_1e6", "许愿要排队", 1e6),
            ("cps_1e9", "愿望开始自动实现", 1e9),
            ("cps_1e12", "一整个时区在烧香", 1e12),
            ("cps_1e15", "神龛自己在长", 1e15),
            ("cps_1e18", "香火开始自己收自己", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒收到 {Core.Numbers.NumFormat.Format(value)} 点香火。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>显灵档（点击）。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("bless_100", "第一次显灵", 100),
            ("bless_1000", "手比法杖快", 1_000),
            ("bless_10000", "显灵成瘾", 10_000),
            ("bless_100000", "信徒开始怀疑这是特效", 100_000),
            ("bless_1000000", "你自己也信了", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "✨",
                Description = $"亲手显灵 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>神迹档：金猫（神迹）来了多少次。</summary>
    private static IEnumerable<AchievementDefinition> MiracleTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("miracle_1", "第一次神迹", 1),
            ("miracle_10", "神迹开始规律出现", 10),
            ("miracle_50", "信徒学会了掐点", 50),
            ("miracle_200", "神迹写进了日历", 200),
            ("miracle_500", "神迹变成了综艺", 500),
            ("miracle_1000", "你已经分不清哪个是安排的", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🌟",
                Description = $"经历 {Core.Numbers.NumFormat.Format(count)} 次神迹。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>神话档：每一套体系一条，最后一条带修饰符（五套都走过之后）。</summary>
    private static IEnumerable<AchievementDefinition> MythTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"myth_{index}",
                Name = index switch
                {
                    1 => "家猫神",
                    2 => "埃及猫神",
                    3 => "希腊猫神",
                    4 => "北欧猫神",
                    _ => "克苏鲁猫",
                },
                Icon = index switch
                {
                    1 => "🏠",
                    2 => "🐈",
                    3 => "🏺",
                    4 => "⚡",
                    _ => "🐙",
                },
                Description = index switch
                {
                    1 => "一块木板，半条鱼，一个不敢许愿的人。",
                    2 => "有组织的神：账本、祭司排班表，以及第一座会漏雨的方尖碑。",
                    3 => "话多的神：神谕天天有，八卦比预言准。",
                    4 => "加班的神：英灵殿不打烊，员工也不打卡。",
                    _ => "不可名状的神：全球同步直播，弹幕全是乱码，收视率爆了。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>信仰档。最后一条带修饰符——香火自己开始产能。</summary>
    private static IEnumerable<AchievementDefinition> FaithTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("faith_500", "第一个常客", 500, "他每天都来，每次都只求同一件事。"),
            ("faith_5k", "有人替你说话", 5_000, "集市上出现了替她辩护的陌生人。"),
            ("faith_40k", "有人替你写书", 40_000, "第一本《猫神言行录》出版，作者署的是别人的名字。"),
            ("faith_300k", "有人替你吵架", 300_000, "两座城为「她更喜欢谁」打了一仗，谁也没赢。"),
            ("faith_1_2m", "有人替你不甘心", 1.2e6, "祭司团开始研究怎么让她永远别退休。"),
            ("faith_3m", "香火自己在产能", 3e6, "供品多到需要单独建一座仓库，仓库也满了。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🙏",
                Description = $"{note}（信仰 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(FaithModule.CounterKey, value),
                Modifiers = id == "faith_3m" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }

    /// <summary>在线档：直播间的历史峰值（最后一条压在末层门槛之下）。</summary>
    private static IEnumerable<AchievementDefinition> ViewerTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("viewers_100", "开播第一个观众", 100),
            ("viewers_2500", "弹幕开始刷屏", 2_500),
            ("viewers_20000", "上了首页推荐", 20_000),
            ("viewers_80000", "全球同步直播", 80_000),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📹",
                Description = $"「直播在线人数」峰值达到 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.Counter(FaithModule.ViewerCounterKey, value),
            };
        }
    }
}
