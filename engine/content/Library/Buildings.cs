using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 《猫娘图书馆》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 带 <c>reader</c> 标签的五座是唯一能带来「被阅读度」的建筑：阅览室、复印机、禁书区、
/// 索引塔、无尽书架。它们同时也是主力产出——所以这个包里"多赚钱"和"有人读"不是两件事，
/// 而是同一件事的两面：<b>书不摆出来就没人读，没人读就赚不动</b>。
/// </para>
/// <para>
/// 解锁条件用「历史累计」而不是「本轮累计」：这个包每次开新书都会重置本轮累计，
/// 而重启之后你手上还有建筑——用本轮累计会让它们显示成「未解锁」（见 ROADMAP 4A 的
/// 那条新不变量与 <c>engine/docs/CONTENT_AUTHORING.md</c> §11.1）。
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

    /// <summary>产出「被阅读度」的建筑标签。</summary>
    public const string ReaderTag = ReadershipModule.ReaderTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "bookshelf",
            Name = Prose.Text("buildings", "bookshelf", "name"),
            Icon = "📚",
            Description = Prose.Text("buildings", "bookshelf", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "reading_room",
            Name = Prose.Text("buildings", "reading_room", "name"),
            Icon = "🪑",
            Description = Prose.Text("buildings", "reading_room", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "copier",
            Name = Prose.Text("buildings", "copier", "name"),
            Icon = "📠",
            Description = Prose.Text("buildings", "copier", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(330),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "banned_section",
            Name = Prose.Text("buildings", "banned_section", "name"),
            Icon = "🔒",
            Description = Prose.Text("buildings", "banned_section", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(4_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "author_room",
            Name = Prose.Text("buildings", "author_room", "name"),
            Icon = "🖋️",
            Description = Prose.Text("buildings", "author_room", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(45_000),
        },
        new()
        {
            Id = "index_tower",
            Name = Prose.Text("buildings", "index_tower", "name"),
            Icon = "🗼",
            Description = Prose.Text("buildings", "index_tower", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(500_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "printing_house",
            Name = Prose.Text("buildings", "printing_house", "name"),
            Icon = "🖨️",
            Description = Prose.Text("buildings", "printing_house", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(6_000_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "world_workshop",
            Name = Prose.Text("buildings", "world_workshop", "name"),
            Icon = "🧩",
            Description = Prose.Text("buildings", "world_workshop", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(80_000_000),
        },
        new()
        {
            Id = "endless_shelf",
            Name = Prose.Text("buildings", "endless_shelf", "name"),
            Icon = "♾️",
            Description = Prose.Text("buildings", "endless_shelf", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(1_000_000_000),
            Tags = [ReaderTag],
        },
    ];
}
