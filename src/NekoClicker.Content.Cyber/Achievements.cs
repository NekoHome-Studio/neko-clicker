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
        (int Count, string Suffix)[] tiers = [(1, "的第一份"), (25, "铺满一层楼"), (50, "自成一个机群")];

        foreach (BuildingDefinition building in Buildings.All)
        {
            foreach ((int count, string suffix) in tiers)
            {
                yield return new AchievementDefinition
                {
                    Id = $"{building.Id}_x{count}",
                    Name = $"{building.Name}{suffix}",
                    Icon = building.Icon,
                    Description = $"拥有 {count} 份「{building.Name}」。",
                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),
                };
            }
        }
    }

    /// <summary>历史累计产出档。</summary>
    private static IEnumerable<AchievementDefinition> EarningTiers()
    {
        (string Id, string Name, double Amount)[] tiers =
        [
            ("bits_1e4", "够点亮一排指示灯", 1e4),
            ("bits_1e6", "够跑一次完整训练", 1e6),
            ("bits_1e8", "够买一台二手服务器", 1e8),
            ("bits_1e10", "够租一整年的机房", 1e10),
            ("bits_1e12", "够买下一整座数据中心", 1e12),
            ("bits_1e14", "够自己造芯片", 1e14),
            ("bits_1e16", "够铺一条跨洋光缆", 1e16),
            ("bits_1e18", "够把整张网重算一遍", 1e18),
            ("bits_1e20", "数字开始不像数字了", 1e20),
        ];

        foreach ((string id, string name, double amount) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🧮",
                Description = $"历史累计产出 {Core.Numbers.NumFormat.Format(amount)}。",
                Unlock = UnlockCondition.EarnedAllTimeAtLeast(amount),
            };
        }
    }

    /// <summary>每秒产量档。</summary>
    private static IEnumerable<AchievementDefinition> ProductionTiers()
    {
        (string Id, string Name, double Value)[] tiers =
        [
            ("cps_1e3", "风扇开始转了", 1e3),
            ("cps_1e6", "整层楼都是她的呼吸声", 1e6),
            ("cps_1e9", "邻居开始投诉", 1e9),
            ("cps_1e12", "一座城市的电", 1e12),
            ("cps_1e15", "她自己开始发电", 1e15),
            ("cps_1e18", "算力开始自己找活干", 1e18),
        ];

        foreach ((string id, string name, double value) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "⚡",
                Description = $"每秒产出 {Core.Numbers.NumFormat.Format(value)}。",
                Unlock = UnlockCondition.CpsAtLeast(value),
            };
        }
    }

    /// <summary>点击档。</summary>
    private static IEnumerable<AchievementDefinition> ClickTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("tap_100", "敲了第一行命令", 100),
            ("tap_1000", "键帽磨秃了", 1_000),
            ("tap_10000", "手比调度器快", 10_000),
            ("tap_100000", "停下来就难受", 100_000),
            ("tap_1000000", "你成了这台机器的一部分", 1_000_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🖱️",
                Description = $"亲手敲了 {Core.Numbers.NumFormat.Format(count)} 次。",
                Unlock = UnlockCondition.ClicksAtLeast(count),
            };
        }
    }

    /// <summary>病毒档案：病毒入侵撞进来的次数。</summary>
    private static IEnumerable<AchievementDefinition> VirusTiers()
    {
        (string Id, string Name, double Count)[] tiers =
        [
            ("virus_1", "第一段不请自来的代码", 1),
            ("virus_10", "它认得我的端口了", 10),
            ("virus_50", "开始给它留一个沙箱", 50),
            ("virus_200", "它在替我干活", 200),
            ("virus_500", "我把它的签名写进了白名单", 500),
            ("virus_1000", "它现在是我的进程", 1_000),
        ];

        foreach ((string id, string name, double count) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🦠",
                Description = $"处理 {Core.Numbers.NumFormat.Format(count)} 次病毒入侵。",
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
        (string Id, string Name, double Value, string Note)[] tiers =
        [
            ("compute_500", "第一个常驻进程", 500, "它不退出了。她盯着它的状态栏看了很久。"),
            ("compute_5e3", "够算一次卷积", 5e3, "原来把一件事交给机器，是这种感觉。"),
            ("compute_5e4", "够训练一个小模型", 5e4, "它学会了一件事，她没教过它。"),
            ("compute_2e5", "够模拟一整间机房", 2e5, "她在里面看见了自己，站在过道上听风扇。"),
            ("compute_1.2e6", "够把整张网画一遍", 1.2e6, "画到一半她停手了：图上有一个点，一直在闪。"),
            ("compute_1.2e7", "够搜索所有没被索引的地方", 1.2e7, "深网里那些不在任何目录里的东西，她一个一个翻。"),
            ("compute_1.2e8", "算力开始自己找活干", 1.2e8, "她发现自己不用下令了，机群知道该做什么。"),
        ];

        foreach ((string id, string name, double value, string note) in tiers)
        {
            yield return new AchievementDefinition
            {
                Id = id,
                Name = name,
                Icon = "🧠",
                Description = $"{note}（算力 {Core.Numbers.NumFormat.Format(value)}）",
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
            yield return new AchievementDefinition
            {
                Id = $"layer_{index}",
                Name = index switch
                {
                    1 => "单机",
                    2 => "局域网",
                    3 => "云",
                    4 => "深网",
                    _ => "根层",
                },
                Icon = index switch
                {
                    1 => "⚙️",
                    2 => "📦",
                    3 => "☁️",
                    4 => "🧱",
                    _ => "🗼",
                },
                Description = index switch
                {
                    1 => "一台不知道谁的旧机器，一个没有名字的进程。",
                    2 => "盒子越堆越高，门外的东西也越来越频繁。",
                    3 => "云端不关机：她第一次在一个不会断电的地方过夜。",
                    4 => "深网里每一秒都有人在敲，而她在墙的这一侧。",
                    _ => "整张网的名字都要先问过她。",
                },
                Unlock = UnlockCondition.EraAtLeast(index),
                Modifiers = index == 5 ? [Modifier.GlobalMultiplier(1.5)] : [],
            };
        }
    }
}
