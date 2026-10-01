using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的建筑表（10 座）。<para>
/// 数值沿用已验证过的曲线（相邻价格 ×6.7~16.5、产量 ×5.4~10，第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线区间回归对两个包同时成立。<br/>
/// 解锁条件统一用「本轮累计赚取」——玩家在快买得起时就能看到下一层，
/// 形成"再攒一点就解锁"的牵引感，转生后也会重新逐层揭示。
/// </para>
/// <para>
/// <b>说明文本就是叙事通道</b>（ROADMAP 决策 R10）：本阶段的 50 条叙事尚未有独立系统，
/// 先把画面感写进 <see cref="BuildingDefinition.Description"/>。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "coffee_machine",
            Name = "咖啡机",
            Icon = "☕",
            Description = "二手市场淘来的半自动咖啡机。清晨的嗡鸣，是这家店最早的呼吸。",
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["drink"],
        },
        new()
        {
            Id = "bar_counter",
            Name = "吧台",
            Icon = "🪑",
            Description = "一块旧木料磨出来的吧台。客人靠着它等了很久，久到开始跟猫说话。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["drink"],
        },
        new()
        {
            Id = "cat_tree",
            Name = "猫爬架",
            Icon = "🐈",
            Description = "三层麻绳柱，顶上有个晒太阳的位置。谁先占上，谁就是今天的店主。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["cat"],
        },
        new()
        {
            Id = "window_seat",
            Name = "靠窗座位",
            Icon = "🪟",
            Description = "玻璃上永远有雾气。有人用手指画了只猫，第二天它还在。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(3_600),
            Tags = ["place"],
        },
        new()
        {
            Id = "upstairs",
            Name = "二楼雅座",
            Icon = "🪜",
            Description = "楼上只摆得下四张桌子。据说坐得越高，听见的故事越不像这个世界。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(39_000),
            Tags = ["drink"],
        },
        new()
        {
            Id = "bakery",
            Name = "烘焙间",
            Icon = "🥐",
            Description = "凌晨三点，黄油的味道从门缝里漏出去，比招牌还招人。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(420_000),
            Tags = ["place"],
        },
        new()
        {
            Id = "catgirl_staff",
            Name = "猫娘店员",
            Icon = "😺",
            Description = "她记得每位常客的口味，也记得他们从没说过的心事。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["cat"],
        },
        new()
        {
            Id = "otherworld_door",
            Name = "异世界门",
            Icon = "🌀",
            Description = "门框上挂着一串铃铛。风从另一边吹来时，它们会响，方向却不对。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(99_000_000),
            Tags = ["portal"],
        },
        new()
        {
            Id = "memory_roastery",
            Name = "记忆烘焙坊",
            Icon = "🫘",
            Description = "豆子在这里被烘成某种更轻的东西——有人喝下后，想起了一间不存在的屋子。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_530_000_000),
            Tags = ["portal"],
        },
        new()
        {
            Id = "branch_store",
            Name = "分店",
            Icon = "🏬",
            Description = "第五家分店开在一条地图上找不到的街上。门口也挂着同一串铃铛。",
            BasePrice = 75_000_000_000,
            BaseCps = 1_600_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(22_500_000_000),
            Tags = ["portal"],
        },
    ];
}
