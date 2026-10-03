using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 《猫娘公司》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线回归对四个包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 命名跟着一家公司真实的扩张顺序走：一张桌子 → 一间会议室 → 把活外包出去 →
/// 自己的机房 → 增长团队 → 路演厅 → 数据中心 → 海外分部 → 总部大楼。
/// 其中带 <c>team</c> 标签的三座产出「士气」，其余只产出营收。
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

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "desk",
            Name = Prose.Text("buildings", "desk", "name"),
            Icon = Prose.Text("buildings", "desk", "icon"),
            Description = Prose.Text("buildings", "desk", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["team"],
        },
        new()
        {
            Id = "meeting_room",
            Name = Prose.Text("buildings", "meeting_room", "name"),
            Icon = Prose.Text("buildings", "meeting_room", "icon"),
            Description = Prose.Text("buildings", "meeting_room", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["team"],
        },
        new()
        {
            Id = "outsourcing_base",
            Name = Prose.Text("buildings", "outsourcing_base", "name"),
            Icon = Prose.Text("buildings", "outsourcing_base", "icon"),
            Description = Prose.Text("buildings", "outsourcing_base", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["labor"],
        },
        new()
        {
            Id = "server_room",
            Name = Prose.Text("buildings", "server_room", "name"),
            Icon = Prose.Text("buildings", "server_room", "icon"),
            Description = Prose.Text("buildings", "server_room", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = ["infra"],
        },
        new()
        {
            Id = "growth_team",
            Name = Prose.Text("buildings", "growth_team", "name"),
            Icon = Prose.Text("buildings", "growth_team", "icon"),
            Description = Prose.Text("buildings", "growth_team", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = ["team"],
        },
        new()
        {
            Id = "roadshow_hall",
            Name = Prose.Text("buildings", "roadshow_hall", "name"),
            Icon = Prose.Text("buildings", "roadshow_hall", "icon"),
            Description = Prose.Text("buildings", "roadshow_hall", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = ["capital"],
        },
        new()
        {
            Id = "data_center",
            Name = Prose.Text("buildings", "data_center", "name"),
            Icon = Prose.Text("buildings", "data_center", "icon"),
            Description = Prose.Text("buildings", "data_center", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["infra"],
        },
        new()
        {
            Id = "overseas_branch",
            Name = Prose.Text("buildings", "overseas_branch", "name"),
            Icon = Prose.Text("buildings", "overseas_branch", "icon"),
            Description = Prose.Text("buildings", "overseas_branch", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = ["labor"],
        },
        new()
        {
            Id = "headquarters",
            Name = Prose.Text("buildings", "headquarters", "name"),
            Icon = Prose.Text("buildings", "headquarters", "icon"),
            Description = Prose.Text("buildings", "headquarters", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = ["capital"],
        },
    ];
}
