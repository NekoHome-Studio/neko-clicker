using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 成就表（66 条）。<para>
/// 大部分由生成器铺出来（设备档 / 赚取档 / 产量档 / 记录档 / 事故档 / 批次档 / 伦理档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（5 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
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
        .. AccidentTiers(),
        .. BatchTiers(),
        .. EthicsTiers(),
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

    /// <summary>历史累计数据档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("data_1e4", "第一批数据", 1e4),
            ("data_1e6", "够写一篇", 1e6),
            ("data_1e8", "够写一本书", 1e8),
            ("data_1e10", "够开一次发布会", 1e10),
            ("data_1e12", "够建一座新楼", 1e12),
            ("data_1e14", "够买下整个机构", 1e14),
            ("data_1e16", "够重来七次", 1e16),
            ("data_1e18", "够忘掉为什么开始", 1e18),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "📊",
                Description = $"历史累计赚取 {Core.Numbers.NumFormat.Format(amount)} 条数据。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "仪器在响", 1e3),
            ("cps_1e6", "整层楼在响", 1e6),
            ("cps_1e9", "整栋楼在响", 1e9),
            ("cps_1e12", "城市听得到", 1e12),
            ("cps_1e15", "你听不到了", 1e15),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🔊",
                Description = $"每秒产量达到 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>手动记录档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("log_100", "亲手记了一百次", 100),
            ("log_1000", "亲手记了一千次", 1_000),
            ("log_10000", "亲手记了一万次", 10_000),
            ("log_100000", "手比仪器更熟", 100_000),
            ("log_1000000", "你成了仪器的一部分", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "✍️",
                Description = $"手动记录 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>实验事故档。</summary>
    private static IEnumerable<AchievementDefinition> AccidentTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("accident_1", "第一次事故", 1),
            ("accident_10", "见过十次了", 10),
            ("accident_50", "应急预案背下来了", 50),
            ("accident_200", "事故也是数据", 200),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "💥",
                Description = $"处理 {Core.Numbers.NumFormat.Format(count)} 次实验事故。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>批次档：每一批一条，最后一条带修饰符（她开始记得了）。</summary>
    private static IEnumerable<AchievementDefinition> BatchTiers()
    {
        for (int index = 1; index <= 7; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"batch_{index}",
                Name = index == 7 ? "第七批" : $"第 {index} 批",
                Icon = index == 7 ? "🗄️" : "🧪",
                Description = index == 7
                    ? "进入最后一批。架子上最上面那层终于等到人了。"
                    : $"开启第 {index} 批。残留的记忆又多了一点。",
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 7 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>伦理值档。最后一条带修饰符——伦理值本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> EthicsTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("ethics_50", "有人提了一句", 50, "会议记录里第一次出现「她」。"),
            ("ethics_200", "第一次表决", 200, "六票里有一票是反对。"),
            ("ethics_800", "章程生效", 800, "从此停机需要两个人签字。"),
            ("ethics_2000", "外部审计", 2000, "来了一个不归你管的人。"),
            ("ethics_5000", "行业标准", 5000, "你做的事变成了别人要遵守的规矩。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚖️",
                Description = $"{note}（伦理值 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(EthicsModule.CounterKey, value),
                Modifiers = id == "ethics_5000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
