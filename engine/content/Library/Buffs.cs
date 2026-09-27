using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 蠹虫带来的东西不全是坏事：畅销能让整馆爆满，但纸荒、抄本泛滥、查禁也会同时压下来。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "bookworm_swarm",
            Name = "蠹虫成群",
            Icon = "🐛",
            Description = "它们啃穿了整整一排没人翻的书，油墨味顺着走廊散开。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "overnight_draft",
            Name = "通宵赶稿",
            Icon = "🌙",
            Description = "天亮前必须交。她写到手指发抖，但没有一行是凑的。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "bestseller",
            Name = "畅销",
            Icon = "📈",
            Description = "加印了三次还是不够。门口排队的人多到挡住了隔壁的招牌。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "paper_shortage",
            Name = "纸荒",
            Icon = "📉",
            Description = "印坊停了。有读者愿意拿自己的本子来换，她没换。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "plagiarism",
            Name = "抄本泛滥",
            Icon = "🪞",
            Description = "满街都是这本书，只是署名不是她。读的人越多，她越说不清哪个是自己写的。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "censorship",
            Name = "查禁",
            Icon = "🚫",
            Description = "书脊上又被划了一道。她把剩下的搬到地下室，灯不敢开。全部产量 ×0.6。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "night_reading_room",
            Name = "深夜阅览室",
            Icon = "🪑",
            Description = "闭馆之后还有人没走，她也就没关灯。阅览室产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("reading_room", 30)],
        },
        new()
        {
            Id = "proofread",
            Name = "校稿",
            Icon = "🔍",
            Description = "她自己从头到尾读了一遍，改掉十七个错字和三处前后矛盾。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
