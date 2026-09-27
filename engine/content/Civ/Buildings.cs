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
    /// <summary>产出「文化」的建筑标签。</summary>
    public const string RecorderTag = CultureModule.RecorderTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序，也是"时代"的骨架。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "cat_nest",
            Name = "猫窝",
            Icon = "🪹",
            Description = "几根树枝搭出来的一个坑，里面垫着干草。她第一次在里面睡了一整夜，没有被雨淋醒。",
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "village",
            Name = "村庄",
            Icon = "🏘️",
            Description = "猫窝挨着猫窝，中间留出一条踩出来的路。路是文明的第一件公共设施。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "city_wall",
            Name = "城墙",
            Icon = "🧱",
            Description = "石头一块一块垒起来，高过她的头顶。她说这道墙不是为了拦住谁，是为了让里面的人敢睡着。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
        },
        new()
        {
            Id = "market",
            Name = "集市",
            Icon = "🏪",
            Description = "第一次有人用东西换东西。她发现原来可以让别人替自己干活——代价是把这件事讲清楚。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "academy",
            Name = "学院",
            Icon = "🏛️",
            Description = "她第一次把「为什么」写下来，而不是记住它。写下来的那一天，知道的人从一变成了不止一。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "temple",
            Name = "神殿",
            Icon = "⛩️",
            Description = "柱子很高，里面很暗。她把最早那只猫窝的形状刻在了正中间，说这是为了让后来的人记得起点。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = [RecorderTag],
        },
        new()
        {
            Id = "star_port",
            Name = "星港",
            Icon = "🚀",
            Description = "第一艘船不是往外飞的，是往上飞的。她站在下面仰着头，直到看不见为止。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
        },
        new()
        {
            Id = "spirit_bridge",
            Name = "灵桥",
            Icon = "🌉",
            Description = "为了让下一艘船找得到回来。桥上没有车，只有一串一直在发的信号——它在说「这里有人」。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
        },
        new()
        {
            Id = "deep_space_relay",
            Name = "深空中继",
            Icon = "📡",
            Description = "信号跳了一次、两次、无数次，跳到连她自己也听不清。它还在发，因为另一端也许有人在等。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = [RecorderTag],
        },
    ];
}
