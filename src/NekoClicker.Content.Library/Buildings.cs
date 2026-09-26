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
/// 那条新不变量与 <c>docs/CONTENT_AUTHORING.md</c> §11.1）。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>产出「被阅读度」的建筑标签。</summary>
    public const string ReaderTag = ReadershipModule.ReaderTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "bookshelf",
            Name = "书架",
            Icon = "📚",
            Description = "一排从地板顶到天花板的架子，上面什么都有，也什么都没人拿下来过。",
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "reading_room",
            Name = "阅览室",
            Icon = "🪑",
            Description = "长桌，绿罩灯，椅背上有别人留下的温度。她第一次听见翻页的声音。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "copier",
            Name = "复印机",
            Icon = "📠",
            Description = "复印是唯一的传播方式。墨粉贵得要命，但一本变成两本就是两倍的读者。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(330),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "banned_section",
            Name = "禁书区",
            Icon = "🔒",
            Description = "铁栅栏后面那排书反而最抢手。她说早知道这样，当初就不该上锁。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(4_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "author_room",
            Name = "作者室",
            Icon = "🖋️",
            Description = "一张桌子，一盏灯，一把永远是热的椅子。她在这儿写下一个世界的第一句话。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(45_000),
        },
        new()
        {
            Id = "index_tower",
            Name = "索引塔",
            Icon = "🗼",
            Description = "把整座图书馆的目录立起来，高到能看见天台。找得到才有人读。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(500_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "printing_house",
            Name = "印坊",
            Icon = "🖨️",
            Description = "第一次印出不是手抄的整本。油墨味顺着走廊一直飘到门口。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(6_000_000),
            Tags = [ReaderTag],
        },
        new()
        {
            Id = "world_workshop",
            Name = "世界观工坊",
            Icon = "🧩",
            Description = "墙上贴满便签，每一张都是一条规则。规则越多，住进去的人越不容易掉出来。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(80_000_000),
        },
        new()
        {
            Id = "endless_shelf",
            Name = "无尽书架",
            Icon = "♾️",
            Description = "走到头要花掉一整天。她说不必走到头，「读到哪里，哪里就是书架的尽头」。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(1_000_000_000),
            Tags = [ReaderTag],
        },
    ];
}
