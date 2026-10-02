using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 《猫娘神明》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// 这九座的价与产量与图书馆包逐行相同，就是为了让"同一套手感"这件事可以被回归测试直接证明。
/// </para>
/// <para>
/// 建筑线本身在讲这个包的笑话：<b>神是靠业务升级的</b>。
/// 从灶台边的家神龛开始，一路盖到太阳方尖碑、神谕林、雷霆殿；
/// 到第 7 座画风突变——直播间（神也要恰饭），第 8 座是周边工厂（恰饭的实体形态），
/// 第 9 座才是克苏鲁猫的深渊大教堂。这座"从神话到带货再回到神话"的排布，
/// 就是设计文档给这个包定的语气：轻松搞笑 meta。
/// </para>
/// <para>
/// 带 <c>temple</c> 标签的五座是唯一能大量产出「信仰」的建筑（神龛 / 神殿 / 祭坛 / 雷霆殿 /
/// 深渊大教堂）：<b>香火来自神殿，人气来自全部建筑</b>，两者都算进信仰的产率里（见
/// <see cref="FaithModule.FaithPerSecond"/>）。带 <c>stream</c> 标签的直播间额外贡献"在线位"。
/// </para>
/// <para>
/// 解锁条件用「本轮累计」是<b>有意的</b>：这个包不做跨层继承（<c>InheritBuildingRatio</c> 全为 0），
/// 每换一套神话体系，世界重新揭示一遍——上一套体系的神殿在新体系里不成立，
/// 这正是"换皮"这个动作本身的喜剧感。继承与"本轮累计"的搭配见
/// <c>engine/docs/CONTENT_AUTHORING.md</c> §11.1。
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

    /// <summary>产出「信仰」的建筑标签。</summary>
    public const string TempleTag = FaithModule.TempleTag;

    /// <summary>贡献「直播在线人数」的建筑标签。</summary>
    public const string StreamTag = FaithModule.StreamTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "house_shrine",
            Name = Prose.Text("buildings", "house_shrine", "name"),
            Icon = "🏠",
            Description = Prose.Text("buildings", "house_shrine", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = [TempleTag],
        },
        new()
        {
            Id = "stone_temple",
            Name = Prose.Text("buildings", "stone_temple", "name"),
            Icon = "🏛️",
            Description = Prose.Text("buildings", "stone_temple", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [TempleTag],
        },
        new()
        {
            Id = "offering_altar",
            Name = Prose.Text("buildings", "offering_altar", "name"),
            Icon = "🕯️",
            Description = Prose.Text("buildings", "offering_altar", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = [TempleTag],
        },
        new()
        {
            Id = "sun_obelisk",
            Name = Prose.Text("buildings", "sun_obelisk", "name"),
            Icon = "☀️",
            Description = Prose.Text("buildings", "sun_obelisk", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
        },
        new()
        {
            Id = "oracle_grove",
            Name = Prose.Text("buildings", "oracle_grove", "name"),
            Icon = "🌳",
            Description = Prose.Text("buildings", "oracle_grove", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
        },
        new()
        {
            Id = "thunder_hall",
            Name = Prose.Text("buildings", "thunder_hall", "name"),
            Icon = "⚡",
            Description = Prose.Text("buildings", "thunder_hall", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [TempleTag],
        },
        new()
        {
            Id = "stream_studio",
            Name = Prose.Text("buildings", "stream_studio", "name"),
            Icon = "📹",
            Description = Prose.Text("buildings", "stream_studio", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = [StreamTag],
        },
        new()
        {
            Id = "merch_factory",
            Name = Prose.Text("buildings", "merch_factory", "name"),
            Icon = "🧸",
            Description = Prose.Text("buildings", "merch_factory", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
        },
        new()
        {
            Id = "abyssal_cathedral",
            Name = Prose.Text("buildings", "abyssal_cathedral", "name"),
            Icon = "🐙",
            Description = Prose.Text("buildings", "abyssal_cathedral", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [TempleTag],
        },
    ];
}
