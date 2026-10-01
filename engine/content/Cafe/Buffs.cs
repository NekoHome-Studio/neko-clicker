using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的限时增益表（5 条）。<para>
/// 结构与九命轮回包完全一致，只换名称与目标建筑——这就是"同一机制十种包装"的最小样本。
/// 增益的定位是短时间高倍率，价值取决于玩家是否在线并作出反应，与离线收益互补。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "caffeine_overload",
            Name = "咖啡因过载",
            Icon = "⚡",
            Description = "所有人都进入了某种过载状态。全部建筑产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "cat_chorus",
            Name = "猫娘合唱",
            Icon = "🎶",
            Description = "不知道谁起的头，所有猫一起唱了起来。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "boss_treats",
            Name = "老板请客",
            Icon = "🎁",
            Description = "今天的账全记在别人头上。全部建筑产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "failed_steam",
            Name = "打发失败",
            Icon = "💨",
            Description = "奶泡怎么都立不起来。全部建筑产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "new_beans",
            Name = "新豆上市",
            Icon = "🫘",
            Description = "今年的第一批豆子到店。烘焙间产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("bakery", 30)],
        },
    ];
}
