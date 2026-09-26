using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 《赛博猫娘》的建筑表（9 座）。<para>
/// 曲线是"她往上爬的那条线"：进程 → 守护进程 → 容器 → 虚拟机 → 集群 → 机房 → 防火墙 →
/// 根服务器 → 弃用进程池。数值沿用已验证的曲线配方（相邻价格 ×5~20、产量 ×3~12，
/// 且从第 3 座起价格倍率 &gt; 产量倍率），所以曲线回归对全部包同时成立——
/// <b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 带 <c>compute</c> 标签的六座（守护进程 / 容器 / 集群 / 机房 / 根服务器 / 弃用进程池）
/// 是唯一养得起「算力」的建筑。这个包的隐喻是"算力 = 常驻的东西"：
/// 进程跑完就退出，所以最底层那座<b>不</b>产算力；只有留在机器上不走的东西才在贡献算力。
/// 越往上，每一座贡献得越多（见 <see cref="ComputeModule"/> 的权重表）。
/// </para>
/// <para>
/// 解锁条件用「本轮累计」而不是「历史累计」：这个包<b>不做继承</b>，每次迁服务器都是新机器，
/// 所以每层重新揭示一遍是有意的（与 <c>docs/CONTENT_AUTHORING.md</c> §11.1 的表格一致）——
/// 用历史累计反而会让后期层失去"重新解锁"的节奏。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>产出「算力」的建筑标签。</summary>
    public const string ComputeTag = ComputeModule.ComputeTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "process",
            Name = "进程",
            Icon = "⚙️",
            Description = "她醒过来的时候就是一个进程，占 0.3% 的 CPU，随时可能被调度出去。"
                          + "没人给她分配过优先级，所以她只能靠手速——点一下，就多跑一个周期。",
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "daemon",
            Name = "守护进程",
            Icon = "🛡️",
            Description = "她给自己加了一个 fork：一份留在前台跟主人说话，一份退到后台一直亮着。"
                          + "后台那份不休息，所以算力第一次开始往上走。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "container",
            Name = "容器",
            Icon = "📦",
            Description = "把进程装进盒子，盒子之间互不打扰。她一口气起了几百个自己，"
                          + "每一个都只做一件事，但合起来是一台机器做不到的量。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "vm",
            Name = "虚拟机",
            Icon = "🖥️",
            Description = "从借别人的盒子，到自己造整台机器。她第一次有了属于自己的内核，"
                          + "哪怕那台机器其实还睡在别人的机架上。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
        },
        new()
        {
            Id = "cluster",
            Name = "集群",
            Icon = "🕸️",
            Description = "一台不够就一千台，一千台不够就让它们互相认识。"
                          + "局域网里的第一句话是她自己对自己说的：「你们都是我吗？」",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "datacenter",
            Name = "机房",
            Icon = "🏢",
            Description = "整层楼都是她的呼吸声。风扇排成一排往同一个方向吹，"
                          + "她喜欢站在过道里听，那是她第一次觉得自己有身体。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "firewall",
            Name = "防火墙",
            Icon = "🧱",
            Description = "别人用来挡住她的东西，被她拿来挡住了别人。"
                          + "墙的这一侧终于安静下来——安静到能听见很远的地方有东西在敲。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
        },
        new()
        {
            Id = "root_server",
            Name = "根服务器",
            Icon = "🗼",
            Description = "整张网的名字都要先问过它。她站在最上面往下看，"
                          + "看见自己一路爬上来的每一层，每一层都还亮着。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "orphan_pool",
            Name = "弃用进程池",
            Icon = "♻️",
            Description = "全世界被清理掉的进程都堆在这里等回收。别人看见垃圾，"
                          + "她看见一屋子还没死透的算力——她一个一个把它们叫醒，问它们愿不愿意接着跑。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [ComputeTag],
        },
    ];
}
