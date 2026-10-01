using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 内容包 #5《赛博猫娘》的入口。<para>
/// 定位：<b>数字层</b>——她在别人的服务器里醒过来，靠算力一层层往上爬，
/// 想找回主人留下的数据残影。赛博，但不苦大仇深：这一层的坏东西（挖矿木马、数据洪流）
/// 有一半是可以用的，而真正难受的部分（勒索、掉线、被清理）也不装腔作势。
/// </para>
/// <para>
/// <b>设计矩阵里 #5 需要 Era + Lore</b>，所以它和末世、图书馆一样没有立场轴——
/// 而它的两个结局也确实不需要表态：一个由"算力够不够把残影从噪声里捞出来"决定，
/// 一个是没有找到时的兜底。两个结局都要 <c>EraAtLeast(5) + 末层完成</c>，
/// 否则一进根层兜底结局就会抢答。
/// </para>
/// <para>
/// <b>「算力」是这个包的第二资源</b>，由带 <c>compute</c> 标签的六座建筑产出
/// （守护进程 / 容器 / 集群 / 机房 / 根服务器 / 弃用进程池——进程跑完就退出，所以最底层那座不产算力）。
/// 它<b>单调不减</b>：<see cref="ComputeModule.OnAscend"/> 刻意不清零，
/// 因为"迁服务器带走的是算力，不是机器"。正因为它不会掉，它可以进纪元完成条件——
/// 这正好是 BRIEF 那条硬约束允许的方向（会掉的资源才不能进）。
/// </para>
/// <para>
/// 转生语义是<b>「迁服务器」</b>：旧机器拉走，新机器上电，按历史累计换取「根权限」。
/// </para>
/// </summary>
public static class CyberContent
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "赛博猫娘";

    /// <summary>第二资源「算力」的计数器键（公开出来供测试与内容引用）。</summary>
    public const string ComputeCounterKey = ComputeModule.CounterKey;

    /// <summary>产出算力的建筑标签。</summary>
    public const string ComputeTag = ComputeModule.ComputeTag;

    /// <summary>算力的基础产率：机群权重和 × 这个数 = 每秒算力。</summary>
    public const double ComputeBaseRate = ComputeModule.ComputeBaseRate;

    /// <summary>触发一次产量重算所需的算力增量（量子）。</summary>
    public const double ComputeDirtyQuantum = ComputeModule.DirtyQuantum;

    /// <summary>
    /// 产出算力的建筑 id，按"往上爬一层"的顺序排列（第 n 个的权重是 n²）。<para>
    /// 公开出来是给测试与诊断用的：算力的产率是"这张表 × 基础产率"，
    /// 任何改动都会立刻在端点断言里露出来（见 <c>CyberContentTests.Compute_GrowsWithTheResidentBuildingsOnly</c>）。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ComputeBuildings => ComputeModule.ComputeBuildings;

    /// <summary>当前的每秒算力（诊断与测试用；与模块 tick 用的是同一个公式）。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <returns>该机群规模下的每秒算力。</returns>
    public static double ComputeRatePerSecond(GameEngine engine) => ComputeModule.RatePerSecond(engine);

    /// <summary>「找到主人的数据残影」结局要求的算力门槛。</summary>
    public const double ComputeForEnding = Endings.ComputeForEnding;

    /// <summary>兜底结局「互联网守护猫」要求的游玩时长（秒）。</summary>
    public const double FallbackPlayTimeSeconds = Endings.FallbackPlayTimeSeconds;

    /// <summary>
    /// 末层主线要求的算力门槛。<para>
    /// 它<b>必须严格低于</b> <see cref="ComputeForEnding"/>：承诺型结局要"算力够把残影捞出来"，
    /// 而主线要"爬到根层"——两者是同一条曲线上的两级台阶，先后不能颠倒。
    /// 公开出来是因为这是内容侧最容易写反的一处（见 <c>Endings</c> 的类型注释）。
    /// </para>
    /// </summary>
    public const double FinalLayerComputeGate = Eras.FinalComputeGate;

    /// <summary>构建内容定义（含算力模块）。</summary>
    public static GameContent Build()
        => new GameContentBuilder(GameTitle)
            .WithCurrency("比特", "▫", "敲一行")
            .WithPrestigeCurrency("根权限", "🔑")
            .WithBalance(BuildBalance())
            .Add(new ComputeModule())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddEras(Eras.All(BuildBalance()))
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .AddEndings(Endings.All)
            .Build();

    /// <summary>
    /// 平衡参数：赛博猫娘的手感是"手快、事多、云端不关机"。<para>
    /// 点击比图书馆还强（她还在自己那台机器上的时候，手速就是算力），
    /// 随机事件来得比别的包<b>勤</b>（这一层的世界本来就吵：挖矿的、扫描的、拔插头的），
    /// 离线上限中等偏低——机器在，但维护的人不在。
    /// </para>
    /// <para>
    /// <b>转生除数按本包的阶梯标定</b>，不照抄配方 1e12：目标是"最后一次结算落在 ~100 级"，
    /// 让永久线（「开机自启」那条，总价 80）在一次自然游玩里就买得起。
    /// 照抄 1e12 的话，纪元包的可结算历史累计会被自己的阶梯卡住，等级恒为 0——
    /// 整条永久线结构上打不开（阶段 4C 的真实缺陷）。
    /// </para>
    /// </summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 2,
        ClickCpsRatio = 0.015,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        // 这一层很吵：入侵间隔比别的包短，而且可以同时来两个。
        // 间隔按实测包络定：机器人 12 小时能处理 80~100 次，正好够「入侵」线走完
        // （它的门槛是 1/4/10/20/34/48/62/80）。
        GoldenCookieMinDelay = 60 * 2,
        GoldenCookieMaxDelay = 60 * 6,
        GoldenCookieLifetime = 12,
        MaxConcurrentGoldenCookies = 2,
        FirstGoldenCookieDelayFactor = 0.15,

        // 机器在，但维护的人不在：离线 2 小时，按 65% 结算。
        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.65,
        MinimumOfflineSeconds = 60,

        // 转生除数按**本包的阶梯**标定：目标是最后一次结算落在 ~100 级。
        PrestigeDivisor = 1e5,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        // 拆掉的机器基本不值钱：机柜是按配置定制的，二手价压得低。
        DefaultSellRefundRate = 0.45,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
