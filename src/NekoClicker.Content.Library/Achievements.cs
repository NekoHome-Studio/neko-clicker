using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（藏书档 / 页数档 / 产量档 / 提笔档 / 蠹虫档 / 成书档 / 读者档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（2 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>读者档是唯一一条"可能掉回去"的门槛</b>：被阅读度会衰减、每次开新书还会清零，
/// 所以这一档的成就只能在"读者正养着"的时候达成。这是刻意的——它逼着玩家把读者留住，
/// 而这个包的主题就是"有人读，书才在"。成就一旦解锁就永久保留，不会被收回去。
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
        .. BookwormTiers(),
        .. BookTiers(),
        .. ReaderTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一座"), (25, "排满一面墙"), (50, "自成一馆")];

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

    /// <summary>历史累计页数档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("pages_1e4", "够一篇短篇", 1e4),
            ("pages_1e6", "够一本长篇", 1e6),
            ("pages_1e8", "够一套系列", 1e8),
            ("pages_1e10", "够摆满一层楼", 1e10),
            ("pages_1e12", "够摆满一整座馆", 1e12),
            ("pages_1e14", "够写到忘记为什么写", 1e14),
            ("pages_1e16", "够让每个世界都住满人", 1e16),
            ("pages_1e18", "够盖一座图书馆", 1e18),
            ("pages_1e20", "数字开始不像字了", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📄",
                Description = $"历史累计写到 {Core.Numbers.NumFormat.Format(amount)} 页。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "有人在抄", 1e3),
            ("cps_1e6", "有人在印", 1e6),
            ("cps_1e9", "有人在排队", 1e9),
            ("cps_1e12", "一整个城市在翻页", 1e12),
            ("cps_1e15", "书自己在写自己", 1e15),
            ("cps_1e18", "字开始自己找读者", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒写出 {Core.Numbers.NumFormat.Format(value)} 页。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>提笔档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("write_100", "写了第一句", 100),
            ("write_1000", "笔尖磨秃了", 1_000),
            ("write_10000", "手比脑子快", 10_000),
            ("write_100000", "停下来就难受", 100_000),
            ("write_1000000", "你成了这句话的一部分", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🖊️",
                Description = $"亲手提笔 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>蠹虫档案：从书页里钻出来的次数。</summary>
    private static IEnumerable<AchievementDefinition> BookwormTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("bookworm_1", "第一只", 1),
            ("bookworm_10", "它吃的是没人翻的那部分", 10),
            ("bookworm_50", "开始给它留一页", 50),
            ("bookworm_200", "它认得你的笔迹", 200),
            ("bookworm_500", "它在替你校稿", 500),
            ("bookworm_1000", "你把它写进了书里", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🐛",
                Description = $"处理 {Core.Numbers.NumFormat.Format(count)} 只蠹虫。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>成书档：每一本一条，最后一条带修饰符（五本书排在一起之后）。</summary>
    private static IEnumerable<AchievementDefinition> BookTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"book_{index}",
                Name = index switch
                {
                    1 => "空白之书",
                    2 => "第一个世界",
                    3 => "被禁的书",
                    4 => "合订本",
                    _ => "最后一页",
                },
                Icon = index switch
                {
                    1 => "📄",
                    2 => "🌍",
                    3 => "🔒",
                    4 => "📖",
                    _ => "🔖",
                },
                Description = index switch
                {
                    1 => "书架上那本一个字都没有的书，第一句话是自己掉下来的。",
                    2 => "书里开始有人住了。他们不知道自己是写出来的。",
                    3 => "被列进禁书区之后，它成了唯一一本所有人都读过两遍的书。",
                    4 => "前几本的人物在同一本书里碰面，互相觉得对方眼熟。",
                    _ => "最后一页落笔。合上之后还有没有人读，是她唯一没法控制的事。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>读者档。最后一条带修饰符——有人读本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> ReaderTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("readers_2000", "第一个读者", 2_000, "她站在门口看了很久，没敢出声。"),
            ("readers_8000", "有人读完了", 8_000, "借阅卡上第一次出现了同一个名字两次。"),
            ("readers_20000", "有人读了两遍", 20_000, "第二遍读的是注释，比正文还慢。"),
            ("readers_38000", "有人开始引用", 38_000, "她在别人的本子上看见了自己写过的一句话。"),
            ("readers_55000", "有人在等下一本", 55_000, "门口的信箱里出现了没有寄件人的信。"),
            ("readers_70000", "书架自己在长", 70_000, "新的一排架子不是她搬进来的。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "👤",
                Description = $"{note}（被阅读度 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(ReadershipModule.CounterKey, value),
                Modifiers = id == "readers_70000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
