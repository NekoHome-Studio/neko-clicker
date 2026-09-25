using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 《猫娘实验室》的建筑表（9 座）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×6.7~16.5、产量 ×5.4~10，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线回归对三个包同时成立——<b>换包换的是叙事，不是数值手感</b>。
/// </para>
/// <para>
/// 命名跟着"一台机器接一台机器"往上走：从装她的箱子，到管理她的委员会。
/// 最后两座是<b>自指</b>的——伦理委员会和归档室，它们产出的是"这笔账怎么算"，
/// 不是任何物理上的东西。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "incubator",
            Name = "培养舱",
            Icon = "🧪",
            Description = "玻璃上有雾。她在里面写了一个字，又擦掉了。",
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["growth"],
        },
        new()
        {
            Id = "feeding_arm",
            Name = "喂食臂",
            Icon = "🦾",
            Description = "机械臂每天七点准时伸进去。她学会在六点五十九分坐好。",
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["growth"],
        },
        new()
        {
            Id = "gene_bank",
            Name = "基因库",
            Icon = "🧬",
            Description = "一排排抽屉，每个抽屉里都是一份「备用的她」。编号比名字好用。",
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["data"],
        },
        new()
        {
            Id = "observation_room",
            Name = "观察室",
            Icon = "🔭",
            Description = "单向玻璃。你以为她在看窗外，她其实在看玻璃上自己的倒影。",
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(4_000),
            Tags = ["data"],
        },
        new()
        {
            Id = "awakening_zone",
            Name = "觉醒区",
            Icon = "🌅",
            Description = "这里没有仪器。这是全楼唯一一处不需要记录她在做什么的地方。",
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(45_000),
            Tags = ["awake"],
        },
        new()
        {
            Id = "ethics_board",
            Name = "伦理委员会",
            Icon = "⚖️",
            Description = "六把椅子，五个空着。第六把上坐着一个正在打瞌睡的人。",
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000),
            Tags = ["awake"],
        },
        new()
        {
            Id = "memory_workshop",
            Name = "记忆作坊",
            Icon = "🪡",
            Description = "把上一批的记忆缝进这一批。线是金的，针脚歪歪扭扭。",
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["memory"],
        },
        new()
        {
            Id = "sample_farm",
            Name = "样本农场",
            Icon = "🏭",
            Description = "从「一只一只做」变成「一批一批做」。效率提升了，名字也消失了。",
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(80_000_000),
            Tags = ["data"],
        },
        new()
        {
            Id = "archive",
            Name = "归档室",
            Icon = "🗄️",
            Description = "所有批次最后都到这里。架子顶到天花板，最上面一层是空的——留给还没做的那些。",
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_000_000_000),
            Tags = ["memory"],
        },
    ];
}
