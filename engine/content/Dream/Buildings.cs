using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 《猫娘梦境》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 带 <c>dream_layer</c> 标签的七座是唯一能养出「梦境能量」的建筑：除了第 1 座的枕头
/// （它是现实里的东西，不产梦），后面每一座都在梦里，而且越深产得越多。
/// 于是这个包的"多赚钱"和"梦更深"也是同一件事的两面：
/// <b>不往下睡，梦就不会变浓</b>。
/// </para>
/// <para>
/// 解锁条件用「本轮累计」：这个包**不做继承**，每往下睡一层都重新揭示一遍建筑线，
/// 免得开局被一长串灰色条目淹没（见 <c>engine/docs/CONTENT_AUTHORING.md</c> §11.1）。
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

    /// <summary>产出「梦境能量」的建筑标签。</summary>
    public const string DreamLayerTag = DreamEnergyModule.DreamLayerTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "pillow",
            Name = Prose.Text("buildings", "pillow", "name"),
            Icon = Prose.Text("buildings", "pillow", "icon"),
            Description = Prose.Text("buildings", "pillow", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "dream_layer",
            Name = Prose.Text("buildings", "dream_layer", "name"),
            Icon = Prose.Text("buildings", "dream_layer", "icon"),
            Description = Prose.Text("buildings", "dream_layer", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_mirror",
            Name = Prose.Text("buildings", "dream_mirror", "name"),
            Icon = Prose.Text("buildings", "dream_mirror", "icon"),
            Description = Prose.Text("buildings", "dream_mirror", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
        },
        new()
        {
            Id = "nightmare_nest",
            Name = Prose.Text("buildings", "nightmare_nest", "name"),
            Icon = Prose.Text("buildings", "nightmare_nest", "icon"),
            Description = Prose.Text("buildings", "nightmare_nest", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "insomnia_corridor",
            Name = Prose.Text("buildings", "insomnia_corridor", "name"),
            Icon = Prose.Text("buildings", "insomnia_corridor", "icon"),
            Description = Prose.Text("buildings", "insomnia_corridor", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
        },
        new()
        {
            Id = "lucid_zone",
            Name = Prose.Text("buildings", "lucid_zone", "name"),
            Icon = Prose.Text("buildings", "lucid_zone", "icon"),
            Description = Prose.Text("buildings", "lucid_zone", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_weaver",
            Name = Prose.Text("buildings", "dream_weaver", "name"),
            Icon = Prose.Text("buildings", "dream_weaver", "icon"),
            Description = Prose.Text("buildings", "dream_weaver", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "nesting_tower",
            Name = Prose.Text("buildings", "nesting_tower", "name"),
            Icon = Prose.Text("buildings", "nesting_tower", "icon"),
            Description = Prose.Text("buildings", "nesting_tower", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_core",
            Name = Prose.Text("buildings", "dream_core", "name"),
            Icon = Prose.Text("buildings", "dream_core", "icon"),
            Description = Prose.Text("buildings", "dream_core", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [DreamLayerTag],
        },
    ];
}
