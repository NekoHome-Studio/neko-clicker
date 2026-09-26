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
/// 免得开局被一长串灰色条目淹没（见 <c>docs/CONTENT_AUTHORING.md</c> §11.1）。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>产出「梦境能量」的建筑标签。</summary>
    public const string DreamLayerTag = DreamEnergyModule.DreamLayerTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "pillow",
            Name = "枕头",
            Icon = "🛏️",
            Description = "她侧过身，把脸埋进那一小块凹下去的地方。世界安静下来，只剩自己的心跳在数数。",
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "dream_layer",
            Name = "梦层",
            Icon = "🌙",
            Description = "第一层梦。楼下的街是熟悉的，只是所有的招牌都换成了她认得的字。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_mirror",
            Name = "梦镜",
            Icon = "🪞",
            Description = "镜子里的她比她慢半拍。她抬手，镜子里的人还没来得及抬——然后两个人都笑了。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
        },
        new()
        {
            Id = "nightmare_nest",
            Name = "噩梦巢",
            Icon = "🕷️",
            Description = "梦的褶皱里积着没做完的坏事。它们不追人，只是挤在一起，把那一块梦境压得很低。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "insomnia_corridor",
            Name = "失眠走廊",
            Icon = "🚪",
            Description = "两边全是门，每一扇后面都是同一个房间。她走到第 47 扇才承认自己在绕圈。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
        },
        new()
        {
            Id = "lucid_zone",
            Name = "清醒区",
            Icon = "💡",
            Description = "在这里她知道自己在做梦。知道这件事之后，楼可以折起来，海可以倒过来流。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_weaver",
            Name = "织梦者",
            Icon = "🧶",
            Description = "把上一晚剩下的线头接起来。接得好的话，今晚的梦会接着说昨天那一句。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "nesting_tower",
            Name = "嵌套塔",
            Icon = "🗼",
            Description = "塔顶有一扇门，门后是同一座塔。她数到第九层就不数了——反正每一层都在往上。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = [DreamLayerTag],
        },
        new()
        {
            Id = "dream_core",
            Name = "梦核",
            Icon = "🔮",
            Description = "所有梦层套着的那一颗芯。她把手放上去的时候，整座梦轻轻震了一下，像是认出了她。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [DreamLayerTag],
        },
    ];
}
