using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 成就表（68 条）。<para>
/// 大部分由生成器铺出来（建筑档 / 梦量档 / 产量档 / 闭眼档 / 梦魇档 / 梦层档 / 梦境能量档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（2 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
/// 走常规的成就检查路径解锁——结局不需要知道"谁是它的成就"。
/// </para>
/// <para>
/// <b>梦境能量档是这个包最实在的一条线</b>：计数器只涨不落（<c>DreamEnergyModule</c> 不在
/// 转生时清零），所以它一旦解锁就再也不会"退回去"。整条线从 2,000 铺到 70,000，
/// 门槛压在实测包络里——它同时也是玩家判断"离叫醒她还有多远"的刻度尺。
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
        .. NightmareTiers(),
        .. SleepLayerTiers(),
        .. DreamEnergyTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一座"), (25, "铺满一层梦"), (50, "自己成层")];

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

    /// <summary>历史累计梦量档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("dream_1e4", "够打一个盹", 1e4),
            ("dream_1e6", "够睡一下午", 1e6),
            ("dream_1e8", "够睡过一整天", 1e8),
            ("dream_1e10", "够睡过一整个冬天", 1e10),
            ("dream_1e12", "够睡到忘记醒", 1e12),
            ("dream_1e14", "够睡完别人一辈子", 1e14),
            ("dream_1e16", "够睡到世界换一遍", 1e16),
            ("dream_1e18", "数字开始不像觉了", 1e18),
            ("dream_1e20", "够睡到梦外面", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🌙",
                Description = $"历史累计睡出 {Core.Numbers.NumFormat.Format(amount)} 点梦。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "梦里开始有动静", 1e3),
            ("cps_1e6", "整层梦都在响", 1e6),
            ("cps_1e9", "梦自己在长", 1e9),
            ("cps_1e12", "一座梦抵一座城", 1e12),
            ("cps_1e15", "梦开始做梦", 1e15),
            ("cps_1e18", "梦核转得比心跳快", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒睡出 {Core.Numbers.NumFormat.Format(value)} 点梦。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>闭眼档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("close_100", "闭上眼", 100),
            ("close_1000", "开始数数", 1_000),
            ("close_10000", "数到记不清", 10_000),
            ("close_100000", "不用数也能睡着", 100_000),
            ("close_1000000", "你已经分不清哪边是醒了", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "😌",
                Description = $"亲手闭眼 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>梦魇档：从梦的褶皱里翻上来的次数。</summary>
    private static IEnumerable<AchievementDefinition> NightmareTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("nightmare_1", "第一只", 1),
            ("nightmare_10", "它认得这层梦", 10),
            ("nightmare_50", "开始给它留位置", 50),
            ("nightmare_200", "它比你先到这里", 200),
            ("nightmare_500", "它在替你按着梦", 500),
            ("nightmare_1000", "你把它织进了梦里", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "👁️",
                Description = $"面对 {Core.Numbers.NumFormat.Format(count)} 次梦魇。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>梦层档：每一层一条，最后一条带修饰符（五层套在一起之后）。</summary>
    private static IEnumerable<AchievementDefinition> SleepLayerTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"sleep_{index}",
                Name = index switch
                {
                    1 => "浅眠",
                    2 => "深眠",
                    3 => "清明梦",
                    4 => "噩梦层",
                    _ => "梦核",
                },
                Icon = index switch
                {
                    1 => "🛏️",
                    2 => "😴",
                    3 => "💡",
                    4 => "🕷️",
                    _ => "🔮",
                },
                Description = index switch
                {
                    1 => "半梦半醒：床垫的纹路还压在小腿上。",
                    2 => "睡得更沉：梦层开始自己长，离线的收益也更好。",
                    3 => "她意识到自己在做梦，于是整层梦都开始听她的。",
                    4 => "梦的褶皱里全是没做完的坏事，而这里的梦最浓。",
                    _ => "所有梦层套着的那一颗芯。走到这里只有两件事可做。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>梦境能量档。最后一条带修饰符——梦本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> DreamEnergyTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("energy_2000", "梦开始变浓", 2_000, "她伸手的时候，梦里的空气是有一点阻力的。"),
            ("energy_300000", "梦能拧出水", 300_000, "她把手指按在墙上，墙面凹下去一小块，慢慢又弹回来。"),
            ("energy_20000000", "这一层我们说了算", 20_000_000, "她抬了一下手，楼自己往旁边挪了半米。"),
            ("energy_600000000", "梦里能造东西", 600_000_000, "她试着捏了一只猫，捏出来是热的。"),
            ("energy_2000000000", "梦核认出了她", 2_000_000_000, "最里面那颗东西转得和她心跳一样快。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🔮",
                Description = $"{note}（梦境能量 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(DreamEnergyModule.CounterKey, value),
                Modifiers = id == "energy_2000000000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
