using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 《猫娘末世》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// <b>这个包的解锁条件刻意用「历史累计」而不是「本轮累计」。</b>别处都用本轮累计，因为
/// 那样才有"每一次重来都要重新爬一遍"的节奏。末世不行：这类包会把上一轮的建筑继承下来，
/// 于是会出现一座你已经拥有 50 个、却在列表里显示「未解锁」的建筑——因为本轮还没赚到门槛，
/// 而它的产量一直在算。<c>EarnedAllTimeAtLeast</c> 把这条不一致从根上消掉：
/// <b>造过一次的东西，重启之后还是会的</b>，这也正是这个包的设定。
/// </para>
/// <para>
/// 带 <c>relic</c> 标签的三座（数据塔 / 记忆档案馆 / 遗迹之城）是唯一能从废墟里读出记忆的结构，
/// 但它们也是最贵的一条线——「想记得住」和「想跑得快」在这里是两笔钱。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>能从废墟里读出记忆的建筑标签（第二资源「记忆残片」的产出口）。</summary>
    public const string RelicTag = "relic";

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "ruins",
            Name = "废墟",
            Icon = "🧱",
            Description = "一层压着一层的地基，最底下那层是混凝土，最上面那层是别人的家。她从这里开始翻。",
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "generator",
            Name = "发电机",
            Icon = "🔌",
            Description = "柴油味很重，但灯亮了。有灯以后，晚上也算一天。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30),
        },
        new()
        {
            Id = "water_purifier",
            Name = "净水器",
            Icon = "💧",
            Description = "三道滤芯，滤出来的水要先静置一晚。她说这水有股铁锈味，但比上一批好。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(330),
        },
        new()
        {
            Id = "shelter",
            Name = "避难所",
            Icon = "🛖",
            Description = "地下二层，门朝里开。墙上她用炭笔画了一道线，写着「到这里为止淹过」。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(4_000),
        },
        new()
        {
            Id = "greenhouse",
            Name = "温室",
            Icon = "🌱",
            Description = "第一株活下来的东西不是她种的，是自己在裂缝里长出来的。它被搬了进来。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(45_000),
        },
        new()
        {
            Id = "data_tower",
            Name = "数据塔",
            Icon = "📡",
            Description = "天线歪着，但还能收到东西。绝大部分是噪声，偶尔有一句完整的话。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(500_000),
            Tags = [RelicTag],
        },
        new()
        {
            Id = "archive",
            Name = "记忆档案馆",
            Icon = "🗄️",
            Description = "一格一格的抽屉，标签是手写的。最上面那排是空的——她说那是留给还没发生的事。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(6_000_000),
            Tags = [RelicTag],
        },
        new()
        {
            Id = "fusion_reactor",
            Name = "聚变堆",
            Icon = "⚛️",
            Description = "上一批人类留下的图纸，缺了最后三页。她补了四年，补出来的版本比原来的小一圈。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(80_000_000),
        },
        new()
        {
            Id = "relic_city",
            Name = "遗迹之城",
            Icon = "🏚️",
            Description = "整座城市被留下来当档案。她不准任何人拆——「拆了就真的没有了」。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(1_000_000_000),
            Tags = [RelicTag],
        },
    ];
}
