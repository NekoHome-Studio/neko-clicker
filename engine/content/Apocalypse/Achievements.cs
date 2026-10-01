using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 成就表（67 条）。<para>
/// 大部分由生成器铺出来（建筑档 / 物资档 / 产量档 / 翻找档 / 变异体档 / 重启档 / 记忆档），
/// 少数几条手写——手写的那几条带修饰符，是"里程碑真的给东西"的地方。
/// </para>
/// <para>
/// 结局成就（3 条）也在这里：它们自己声明 <c>Unlock = EndingReached(...)</c>，
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
        .. MutationTiers(),
        .. RestartTiers(),
        .. MemoryTiers(),
        .. Endings.Achievements,
    ];

    /// <summary>每座建筑三档：1 / 25 / 50 座。</summary>
    private static IEnumerable<AchievementDefinition> BuildingTiers()
    {
        (int Count, string Suffix)[] tiers = [(1, "的第一座"), (25, "连成片"), (50, "成规模")];

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

    /// <summary>历史累计物资档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("supplies_1e4", "够吃一阵子", 1e4),
            ("supplies_1e6", "够过一整个冬天", 1e6),
            ("supplies_1e8", "够把地下室填满", 1e8),
            ("supplies_1e10", "够养活一群人", 1e10),
            ("supplies_1e12", "够重修一条街", 1e12),
            ("supplies_1e14", "够让整座城亮起来", 1e14),
            ("supplies_1e16", "够重启一次文明", 1e16),
            ("supplies_1e18", "够重启很多次", 1e18),
            ("supplies_1e20", "够把时间买回来一点", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🥫",
                Description = $"历史累计挖到 {Core.Numbers.NumFormat.Format(amount)} 物资。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "够她自己用", 1e3),
            ("cps_1e6", "够十个人用", 1e6),
            ("cps_1e9", "够一层楼用", 1e9),
            ("cps_1e12", "够一条街用", 1e12),
            ("cps_1e15", "够一座城用", 1e15),
            ("cps_1e18", "数字开始不像东西", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚙️",
                Description = $"每秒物资达到 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>亲手翻找档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("scavenge_100", "翻了一百次", 100),
            ("scavenge_1000", "手套破了", 1_000),
            ("scavenge_10000", "闭着眼都知道挖哪", 10_000),
            ("scavenge_100000", "手比工具快", 100_000),
            ("scavenge_1000000", "你成了废墟的一部分", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⛏️",
                Description = $"亲手翻找 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>变异体档案：从废墟里钻出来的次数。</summary>
    private static IEnumerable<AchievementDefinition> MutationTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("mutant_1", "第一次见到它", 1),
            ("mutant_10", "知道它怕光了", 10),
            ("mutant_50", "知道它什么时候来", 50),
            ("mutant_200", "开始给它留门", 200),
            ("mutant_500", "它开始认得她了", 500),
            ("mutant_1000", "你们算是一起活着", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🧬",
                Description = $"处理 {Core.Numbers.NumFormat.Format(count)} 次变异体。",
                Unlock = UnlockCondition.GoldenCookiesAtLeast(count),
            };
        }
    }

    /// <summary>重启档：每一轮一条，最后一条带修饰符（这一次她带够了东西）。</summary>
    private static IEnumerable<AchievementDefinition> RestartTiers()
    {
        for (int index = 1; index <= 5; index++)
        {
            yield return new AchievementDefinition
            {
                Id = $"restart_{index}",
                Name = index switch
                {
                    1 => "地下室",
                    2 => "第一盏灯",
                    3 => "聚集地",
                    4 => "记忆网",
                    _ => "最后的遗迹",
                },
                Icon = index switch
                {
                    1 => "🕯️",
                    2 => "💡",
                    3 => "🏕️",
                    4 => "🕸️",
                    _ => "🏚️",
                },
                Description = index switch
                {
                    1 => "在没有任何人、任何电、任何信号的地方，从今天算起。",
                    2 => "灯亮了整晚。上一次她也点过一盏灯，只是那时候没觉得值得记。",
                    3 => "有人循着灯光找过来，然后留下来。",
                    4 => "她开始在能被挖出来的地方写字。",
                    _ => "城墙外埋着一整座城市，完整地压在地下三米。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }

    /// <summary>记忆档。最后一条带修饰符——记得住本身开始有产能。</summary>
    private static IEnumerable<AchievementDefinition> MemoryTiers()
    {
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("shards_500", "第一片", 500, "玻璃渣一样的东西，举到光下能看见上面有字。"),
            ("shards_4000", "开始分类", 4_000, "她给抽屉贴了标签，用的还是上一世的字。"),
            ("shards_15000", "拼出第一句话", 15_000, "那句话是「不要一个人出去」。"),
            ("shards_45000", "拼出一张脸", 45_000, "她盯着那张脸看了很久，然后把它收起来了。"),
            ("shards_90000", "拼出一座城", 90_000, "整座城的轮廓，包括城墙外那一片。"),
            ("shards_150000", "拼出一个人类", 150_000, "完整的那一种，会说话，会写字，会留下东西。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🔮",
                Description = $"{note}（记忆残片 {Core.Numbers.NumFormat.Format(value)}）",
                Unlock = UnlockCondition.Counter(ShardsModule.CounterKey, value),
                Modifiers = id == "shards_150000" ? [Modifier.GlobalMultiplier(1.4)] : [],
            };
        }
    }
}
