using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 示例内容包的建筑表。<para>
/// 数值节奏参考 Cookie Clicker：相邻建筑的<b>价格约 ×10~13</b>、<b>单产约 ×6~8</b>。
/// 这个比例决定了"每解锁一层新建筑，都会在几分钟内成为主力，然后被下一层取代"的循环；
/// 想让玩家在某一层多停留一会儿，就缩小价格倍率或拉大产量倍率。
/// </para>
/// <para>
/// 解锁条件统一用<b>本轮累计赚取量</b>（约为该建筑价格的 30%）：玩家在快要买得起时
/// 就能看到下一层建筑，形成"再攒一点就解锁"的牵引感；而转生后重新逐层揭示，
/// 也避免了开局就被一长串买不起的灰色条目淹没。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "curled_cat",
            Name = "蜷缩的猫",
            Icon = "🐈",
            Description = "一只愿意在你腿上打呼噜的猫。它自己不会做什么，但一切从这里开始。",
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["cat", "warm"],
        },
        new()
        {
            Id = "scratching_post",
            Name = "猫抓板",
            Icon = "🪵",
            Description = "麻绳缠的柱子。猫抓得开心，掉下来的碎屑能换钱——别问怎么换的。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["cat"],
        },
        new()
        {
            Id = "cat_bed",
            Name = "猫窝",
            Icon = "🛏️",
            Description = "猫在里面睡 16 小时，剩下 8 小时思考要不要出来。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["cat", "warm"],
        },
        new()
        {
            Id = "auto_feeder",
            Name = "自动喂食器",
            Icon = "🍽️",
            Description = "定时投喂。猫已经不记得你长什么样了，但它记得这个机器。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(3_600),
            Tags = ["machine"],
        },
        new()
        {
            Id = "cat_cafe",
            Name = "猫咪咖啡馆",
            Icon = "☕",
            Description = "客人花钱来被猫无视。这是本店最畅销的体验。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(39_000),
            Tags = ["business"],
        },
        new()
        {
            Id = "catnip_field",
            Name = "猫薄荷田",
            Icon = "🌿",
            Description = "合法种植，非法上头。猫们在田里翻滚出一片经济学奇迹。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(420_000),
            Tags = ["farm"],
        },
        new()
        {
            Id = "cat_portal",
            Name = "猫咪传送门",
            Icon = "🌀",
            Description = "猫从这个门进去，从另一个门出来，中间的部分没有人敢研究。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["exotic"],
        },
        new()
        {
            Id = "time_cat",
            Name = "时间猫",
            Icon = "⏳",
            Description = "它同时存在于你打翻水杯之前和之后。主要是之后。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(99_000_000),
            Tags = ["exotic"],
        },
        new()
        {
            Id = "cat_temple",
            Name = "维度猫神殿",
            Icon = "🏛️",
            Description = "供奉着那位从纸箱中创世的猫神。祭品是纸箱。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_530_000_000),
            Tags = ["exotic", "holy"],
        },
        new()
        {
            Id = "cat_universe",
            Name = "猫猫宇宙",
            Icon = "🌌",
            Description = "一个由猫构成、为猫运行、最终也会被猫推下桌的宇宙。",
            BasePrice = 75_000_000_000,
            BaseCps = 1_600_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(22_500_000_000),
            Tags = ["exotic", "cosmic"],
        },
    ];
}
