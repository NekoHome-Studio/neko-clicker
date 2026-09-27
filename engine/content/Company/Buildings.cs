using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 《猫娘公司》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线回归对四个包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 命名跟着一家公司真实的扩张顺序走：一张桌子 → 一间会议室 → 把活外包出去 →
/// 自己的机房 → 增长团队 → 路演厅 → 数据中心 → 海外分部 → 总部大楼。
/// 其中带 <c>team</c> 标签的三座产出「士气」，其余只产出营收。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "desk",
            Name = "工位",
            Icon = "🪑",
            Description = "一张桌子，一把椅子，一台借来的显示器。她贴了张便利贴：「先活下去。」",
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["team"],
        },
        new()
        {
            Id = "meeting_room",
            Name = "会议室",
            Icon = "🗣️",
            Description = "白板上的箭头越画越多，最后指回原点。会开完了，事没动，人倒是熟了些。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["team"],
        },
        new()
        {
            Id = "outsourcing_base",
            Name = "外包基地",
            Icon = "🧵",
            Description = "把活分给更便宜的手。她们在另一个时区，也在另一张价格表里。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["labor"],
        },
        new()
        {
            Id = "server_room",
            Name = "服务器",
            Icon = "🖥️",
            Description = "机房冷得像冰箱。值班的人裹着毯子，盯着不会说话的灯。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = ["infra"],
        },
        new()
        {
            Id = "growth_team",
            Name = "增长团队",
            Icon = "📈",
            Description = "他们负责把「还行」说成「爆发」。数据确实涨了，只是没人说得清为什么。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = ["team"],
        },
        new()
        {
            Id = "roadshow_hall",
            Name = "IPO 路演厅",
            Icon = "🎤",
            Description = "灯打在你脸上，PPT 翻到第 42 页。台下有人问：「你们的护城河是什么？」",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = ["capital"],
        },
        new()
        {
            Id = "data_center",
            Name = "数据中心",
            Icon = "🗄️",
            Description = "一整层楼在低声嗡鸣。她的工位搬到了这里，因为只有这里离服务器最近。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["infra"],
        },
        new()
        {
            Id = "overseas_branch",
            Name = "海外分部",
            Icon = "🌏",
            Description = "时差刚好接上：这边下班，那边上班。灯一天二十四小时都亮着。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = ["labor"],
        },
        new()
        {
            Id = "headquarters",
            Name = "总部大楼",
            Icon = "🏢",
            Description = "玻璃幕墙，前台，工牌，一间没有窗的会议室。楼顶那盏灯谁都没关过。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = ["capital"],
        },
    ];
}
