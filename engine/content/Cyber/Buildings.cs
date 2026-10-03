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
/// 所以每层重新揭示一遍是有意的（与 <c>engine/docs/CONTENT_AUTHORING.md</c> §11.1 的表格一致）——
/// 用历史累计反而会让后期层失去"重新解锁"的节奏。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>
    /// 本包的 <c>text.json</c>：建筑文案与剧情散文<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>产出「算力」的建筑标签。</summary>
    public const string ComputeTag = ComputeModule.ComputeTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "process",
            Name = Prose.Text("buildings", "process", "name"),
            Icon = Prose.Text("buildings", "process", "icon"),
            Description = Prose.Text("buildings", "process", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "daemon",
            Name = Prose.Text("buildings", "daemon", "name"),
            Icon = Prose.Text("buildings", "daemon", "icon"),
            Description = Prose.Text("buildings", "daemon", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "container",
            Name = Prose.Text("buildings", "container", "name"),
            Icon = Prose.Text("buildings", "container", "icon"),
            Description = Prose.Text("buildings", "container", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "vm",
            Name = Prose.Text("buildings", "vm", "name"),
            Icon = Prose.Text("buildings", "vm", "icon"),
            Description = Prose.Text("buildings", "vm", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
        },
        new()
        {
            Id = "cluster",
            Name = Prose.Text("buildings", "cluster", "name"),
            Icon = Prose.Text("buildings", "cluster", "icon"),
            Description = Prose.Text("buildings", "cluster", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "datacenter",
            Name = Prose.Text("buildings", "datacenter", "name"),
            Icon = Prose.Text("buildings", "datacenter", "icon"),
            Description = Prose.Text("buildings", "datacenter", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "firewall",
            Name = Prose.Text("buildings", "firewall", "name"),
            Icon = Prose.Text("buildings", "firewall", "icon"),
            Description = Prose.Text("buildings", "firewall", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
        },
        new()
        {
            Id = "root_server",
            Name = Prose.Text("buildings", "root_server", "name"),
            Icon = Prose.Text("buildings", "root_server", "icon"),
            Description = Prose.Text("buildings", "root_server", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = [ComputeTag],
        },
        new()
        {
            Id = "orphan_pool",
            Name = Prose.Text("buildings", "orphan_pool", "name"),
            Icon = Prose.Text("buildings", "orphan_pool", "icon"),
            Description = Prose.Text("buildings", "orphan_pool", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [ComputeTag],
        },
    ];
}
