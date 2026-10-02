using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 《猫娘实验室》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线回归对三个包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 命名跟着"一台机器接一台机器"往上走：从装她的箱子，到管理她的委员会。
/// 最后两座是<b>自指</b>的——伦理委员会和归档室，它们产出的是"这笔账怎么算"，
/// 不是任何物理上的东西。
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
            Id = "incubator",
            Name = Prose.Text("buildings", "incubator", "name"),
            Icon = "🧪",
            Description = Prose.Text("buildings", "incubator", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["growth"],
        },
        new()
        {
            Id = "feeding_arm",
            Name = Prose.Text("buildings", "feeding_arm", "name"),
            Icon = "🦾",
            Description = Prose.Text("buildings", "feeding_arm", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["growth"],
        },
        new()
        {
            Id = "gene_bank",
            Name = Prose.Text("buildings", "gene_bank", "name"),
            Icon = "🧬",
            Description = Prose.Text("buildings", "gene_bank", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["data"],
        },
        new()
        {
            Id = "observation_room",
            Name = Prose.Text("buildings", "observation_room", "name"),
            Icon = "🔭",
            Description = Prose.Text("buildings", "observation_room", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = ["data"],
        },
        new()
        {
            Id = "awakening_zone",
            Name = Prose.Text("buildings", "awakening_zone", "name"),
            Icon = "🌅",
            Description = Prose.Text("buildings", "awakening_zone", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = ["awake"],
        },
        new()
        {
            Id = "ethics_board",
            Name = Prose.Text("buildings", "ethics_board", "name"),
            Icon = "⚖️",
            Description = Prose.Text("buildings", "ethics_board", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = ["awake"],
        },
        new()
        {
            Id = "memory_workshop",
            Name = Prose.Text("buildings", "memory_workshop", "name"),
            Icon = "🪡",
            Description = Prose.Text("buildings", "memory_workshop", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["memory"],
        },
        new()
        {
            Id = "sample_farm",
            Name = Prose.Text("buildings", "sample_farm", "name"),
            Icon = "🏭",
            Description = Prose.Text("buildings", "sample_farm", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = ["data"],
        },
        new()
        {
            Id = "archive",
            Name = Prose.Text("buildings", "archive", "name"),
            Icon = "🗄️",
            Description = Prose.Text("buildings", "archive", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = ["memory"],
        },
    ];
}
