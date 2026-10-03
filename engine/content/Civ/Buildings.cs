using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 《猫娘文明》的建筑表（9 座），从一只猫窝走到星港。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 带 <c>culture</c> 标签的五座（集市 / 学院 / 神殿 / 灵桥 / 深空中继）是这个包唯一能养出
/// 「文化」的建筑：她可以先有一百座城墙，文化纹丝不动——<b>城墙挡得住洪水，挡不住遗忘</b>。
/// 于是"哪一种建筑在养文化"是玩家能从界面上直接读出来的事。
/// </para>
/// <para>
/// 解锁条件用「本轮累计」而不是「历史累计」：这个包<b>不做继承</b>（<c>InheritBuildingRatio</c> 全为 0），
/// 每一次跨时代都是重头爬一遍，所以每层重新逐层揭示是有意的节奏——手册 §4 第 13 条。
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

    /// <summary>产出「文化」的建筑标签。</summary>
    public const string RecorderTag = CultureModule.RecorderTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序，也是"时代"的骨架。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "cat_nest",
            Name = Prose.Text("buildings", "cat_nest", "name"),
            Icon = Prose.Text("buildings", "cat_nest", "icon"),
            Description = Prose.Text("buildings", "cat_nest", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "village",
            Name = Prose.Text("buildings", "village", "name"),
            Icon = Prose.Text("buildings", "village", "icon"),
            Description = Prose.Text("buildings", "village", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "city_wall",
            Name = Prose.Text("buildings", "city_wall", "name"),
            Icon = Prose.Text("buildings", "city_wall", "icon"),
            Description = Prose.Text("buildings", "city_wall", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
        },
        new()
        {
            Id = "market",
            Name = Prose.Text("buildings", "market", "name"),
            Icon = Prose.Text("buildings", "market", "icon"),
            Description = Prose.Text("buildings", "market", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "academy",
            Name = Prose.Text("buildings", "academy", "name"),
            Icon = Prose.Text("buildings", "academy", "icon"),
            Description = Prose.Text("buildings", "academy", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "temple",
            Name = Prose.Text("buildings", "temple", "name"),
            Icon = Prose.Text("buildings", "temple", "icon"),
            Description = Prose.Text("buildings", "temple", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "star_port",
            Name = Prose.Text("buildings", "star_port", "name"),
            Icon = Prose.Text("buildings", "star_port", "icon"),
            Description = Prose.Text("buildings", "star_port", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
        },
        new()
        {
            Id = "spirit_bridge",
            Name = Prose.Text("buildings", "spirit_bridge", "name"),
            Icon = Prose.Text("buildings", "spirit_bridge", "icon"),
            Description = Prose.Text("buildings", "spirit_bridge", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
        },
        new()
        {
            Id = "deep_space_relay",
            Name = Prose.Text("buildings", "deep_space_relay", "name"),
            Icon = Prose.Text("buildings", "deep_space_relay", "icon"),
            Description = Prose.Text("buildings", "deep_space_relay", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [RecorderTag],
        },
    ];
}
