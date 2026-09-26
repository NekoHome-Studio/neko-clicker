using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 这个包的基调是"加班与人性互相拉扯"，所以增益里有一半是<b>负面</b>的：
/// 需求变更不一定给你好处，宕机一定给你坏处。数值沿用已验证的配方
/// （狂热 ×7 / 77 秒、点击 ×777 / 13 秒），所以随机事件的力度与其它包可比。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "requirement_change",
            Name = "需求变更",
            Icon = "🌀",
            Description = "甲方半夜发来新需求，所有排期作废，所有人都在动。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "all_nighter",
            Name = "通宵冲刺",
            Icon = "☕",
            Description = "咖啡机第三次见底，交付日期提前了一天。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "funding_round",
            Name = "融资到账",
            Icon = "💸",
            Description = "钱到了，账上第一次有了不用算着花的数字。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "server_outage",
            Name = "服务器宕机",
            Icon = "🔥",
            Description = "线上全挂，客诉涌进来，所有人都在等一个人修好它。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "viral_post",
            Name = "意外出圈",
            Icon = "📣",
            Description = "她随手发的一条动态上了热门，服务器差点没扛住。全部产量 ×25。",
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(25)],
        },
        new()
        {
            Id = "poached",
            Name = "被挖角",
            Icon = "🚪",
            Description = "两个核心同事递了辞呈，理由写的是「想换个环境」。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "team_offsite",
            Name = "团建",
            Icon = "🏕️",
            Description = "两天一夜，山里没有信号。回来后大家说话的语气不一样了。工位产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("desk", 30)],
        },
        new()
        {
            Id = "year_end_bonus",
            Name = "年终奖",
            Icon = "🧧",
            Description = "红包发下去的那一刻，办公室安静了三秒，然后掌声。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
