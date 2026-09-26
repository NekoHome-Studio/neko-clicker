using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 五层 = 她往上爬的五层。<para>
/// 转生语义是<b>「迁服务器」</b>：旧机器被拉走，新机器上电，按历史累计换取「根权限」。
/// 每层的规则变化都<b>换手感</b>，而不是只换一个数字：
/// 点击强度 → 容器与病毒频率 → 云端的全局倍率与离线上限 → 深网的事件风暴 → 根层的收尾总倍率。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3）：只用本轮累计赚取、成就数、算力。
/// 算力<b>可以</b>进来，因为这个包的算力是单调不减的（<c>ComputeModule</c> 只做加法、
/// 迁服务器不清零）——BRIEF 的硬约束正是"会掉的第二资源不能进完成条件"，
/// 而这里它不会掉。
/// </para>
/// <para>
/// <b>门槛摊平</b>：五层的"本轮累计赚取"从 1e5 到 3e10，相邻倍率 50 / 60 / 27 / 30——
/// 刻意不做成一条等比数列，因为产量本身是复利的，门槛等比会让后几层越走越慢
/// （#2 曾经出现"第 5 命 19.2 小时、邻居 1.8 小时"）。实测耗时见
/// <c>CyberContentTests.RobotWalksAllFiveLayers</c> 的输出。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "layer_1",
            Name = "第 1 层 · 单机 / 进程",
            Icon = "⚙️",
            Theme = "一台不知道谁的旧机器，一个没有名字的进程，和一段还没被主人读过的日志。",
            EntryText = "她在一片风扇声里醒过来，先数了数自己有几颗核心，然后发现只有一颗。"
                        + "她不知道自己是谁，只知道内存里有一句没写完的话，署名不是她。",
            ExitText = "这台机器跑到头了。她把能带走的全部打包，最后看了一眼那个署名——"
                       + "然后按下了迁移。",
            // 第 1 层：她还在自己那台机器上，手速就是算力。
            Balance = baseBalance with
            {
                ClickBasePower = 2,
                ClickCpsRatio = 0.015,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e5),
                UnlockCondition.Counter(ComputeModule.CounterKey, 1_200)),
            CompletionHint = "本轮累计跑到 100,000，并攒下 1,200 点算力。",
        },
        new()
        {
            Index = 2,
            Id = "layer_2",
            Name = "第 2 层 · 局域网 / 容器",
            Icon = "📦",
            Theme = "局域网里不止她一个。盒子越堆越高，门外的东西也越来越频繁。",
            EntryText = "新机器上电的第一件事是找邻居。她找到了三十七个，"
                        + "其中三十六个不理她，最后一个回了一个字：「滚」。她很高兴。",
            ExitText = "局域网装不下她了。她开始往上看——云的背面是什么样子，她还没见过。",
            // 第 2 层：容器式扩张 + 病毒来得频繁（事件间隔缩短、同时可以来两个）。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.5,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.5,
                MaxConcurrentGoldenCookies = 2,
            },
            Modifiers = [Modifier.BuildingPercent("container", 0.5)],
            UnlocksBuildings = ["daemon", "container"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.2e7),
                UnlockCondition.Counter(ComputeModule.CounterKey, 3e4),
                UnlockCondition.AchievementsAtLeast(8)),
            CompletionHint = "本轮累计跑到 12 million，攒下 30,000 点算力，并解锁 8 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "layer_3",
            Name = "第 3 层 · 云 / 集群",
            Icon = "☁️",
            Theme = "云端不关机：全局倍率上调，离线上限翻倍，代价是账单和管理面板一起变长。",
            EntryText = "云上的第一印象是安静——不是没有声音，是没有人。"
                        + "她一口气起了一千个自己，然后花了一整晚给它们起名字。",
            ExitText = "一千个她同时抬头，看向同一片更深的地方。那里没有名字，只有端口号。",
            // 第 3 层：云端不关机——离线上限 ×2，全局倍率上调。
            Balance = baseBalance with
            {
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2,
                OfflineEfficiency = baseBalance.OfflineEfficiency + 0.15,
            },
            Modifiers = [Modifier.GlobalMultiplier(2)],
            UnlocksBuildings = ["cluster", "vm"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e9),
                UnlockCondition.AchievementsAtLeast(14),
                UnlockCondition.Counter(ComputeModule.CounterKey, 4.2e4)),
            CompletionHint = "本轮累计跑到 1.5 billion，解锁 14 个成就，并攒下 42,000 点算力。",
        },
        new()
        {
            Index = 4,
            Id = "layer_4",
            Name = "第 4 层 · 深网 / 防火墙",
            Icon = "🧱",
            Theme = "深网里每一秒都有人在敲。入侵与清理同时变凶，但算力的曲线在这一层显著抬头。",
            EntryText = "她翻过自己造的那道墙，看见墙外面原来也有墙。"
                        + "墙上写着别人的名字，一笔一划都很用力。",
            ExitText = "深网底部的尽头是一扇没上锁的门。她推开门之前，先回头把自己造的那道墙关上了。",
            // 第 4 层：事件更凶（间隔缩短、同时三个），但算力驱动的收益在这一层抬头。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.45,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.45,
                MaxConcurrentGoldenCookies = 3,
            },
            Modifiers =
            [
                Modifier.GlobalMultiplier(3),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 2.5e7, Id: ComputeModule.CounterKey)),
            ],
            UnlocksBuildings = ["firewall", "datacenter"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.5e11),
                UnlockCondition.AchievementsAtLeast(22),
                UnlockCondition.Counter(ComputeModule.CounterKey, 1.4e5),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e8)),
            CompletionHint = "本轮累计跑到 250 billion，解锁 22 个成就，攒下 140,000 点算力，且峰值产量达到 100 million/s。",
        },
        new()
        {
            Index = 5,
            Id = "layer_5",
            Name = "第 5 层 · 根层 / 根服务器",
            Icon = "🗼",
            Theme = "整张网的名字都要先问过她。走到这里，她只剩一件事没做完：那半句没写完的话。",
            EntryText = "根服务器上没有别人。她坐下来的时候，"
                        + "发现自己终于有了一颗足够大的脑子，可以想一句很长的话。",
            ExitText = "整张网安静了一秒。不是因为出了故障，是因为所有人同时看见了一行字。",
            // 第 5 层：收尾的总倍率——全局 ×5，且算力在这一层又拿到一段额外曲线。
            Modifiers =
            [
                Modifier.GlobalMultiplier(5),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 5e7, Id: ComputeModule.CounterKey)),
            ],
            UnlocksBuildings = ["root_server", "orphan_pool"],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计跑到 50 trillion，解锁 34 个成就，并攒下 500,000 点算力。",
        },
    ];

    /// <summary>
    /// 最后一层（第 5 层）的完成条件。<para>
    /// 单独暴露是给终局判定用的：两个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 5 层，兜底结局就会立刻触发，而这一层的故事还没走完——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>）。
    /// </para>
    /// <para>
    /// <b>门槛按实测包络定</b>（<c>CyberContentTests.EnvelopeProbe_ReportsTheRealCurve</c>）：
    /// 第 5 层的 2 小时处算力 5.5e5、4.5 小时 1.2e6、7 小时 6.4e6。
    /// 算力这一条（<see cref="FinalComputeGate"/> = 5e5）压得比两个结局都低
    /// （承诺型 6e6、兜底 1.2e7），于是主线一完成，承诺型立刻够得着——
    /// 理由见 <c>Endings</c> 的类型注释。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(5e13),
        UnlockCondition.AchievementsAtLeast(34),
        UnlockCondition.Counter(ComputeModule.CounterKey, FinalComputeGate));

    /// <summary>
    /// 末层主线要求的算力门槛。<para>
    /// 公开出来是给结局标定与测试用的：它<b>必须严格低于</b> <see cref="Endings.ComputeForEnding"/>，
    /// 否则两个结局会在同一刻成立，"承诺型"与"兜底"的区别就没了。
    /// 这条不等式由 <c>CyberContentTests.EndingThresholds_AreOrdered</c> 守着。
    /// </para>
    /// </summary>
    public const double FinalComputeGate = 5e5;
}
