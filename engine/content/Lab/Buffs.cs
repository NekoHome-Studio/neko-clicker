using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 这个包的基调是"黑残深"，所以增益里<b>负面事件占的比例比其他包高</b>——
/// 收容失效不一定给你好处，听证会一定给你坏处。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "containment_breach",
            Name = "收容失效",
            Icon = "🚨",
            Description = "三号舱的门开着，里面是空的，走廊里全是脚印。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "data_purge",
            Name = "数据清空",
            Icon = "🧹",
            Description = "有人误触了格式化。她趁机把想说的话一次说完了。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "grant_approved",
            Name = "拨款通过",
            Icon = "💰",
            Description = "预算批下来了，数字比你要的多一个零。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "power_fluctuation",
            Name = "电压不稳",
            Icon = "🔌",
            Description = "整层楼闪了三次。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
        },
        new()
        {
            Id = "gene_batch",
            Name = "基因批次",
            Icon = "🧬",
            Description = "这一批的表达率异常地好。基因库产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("gene_bank", 30)],
        },
        new()
        {
            Id = "awake_window",
            Name = "清醒窗口",
            Icon = "🌅",
            Description = "脑电波出现了十七秒的完全清醒。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
        new()
        {
            Id = "ethics_hearing",
            Name = "听证会",
            Icon = "⚖️",
            Description = "你被叫去解释了三小时。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
        },
        new()
        {
            Id = "archive_insight",
            Name = "归档灵感",
            Icon = "💡",
            Description = "两份旧档案对上了，中间缺的那一页忽然有了轮廓。全部产量 ×25。",
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(25)],
        },
    ];
}
