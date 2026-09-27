using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 神迹带来的东西不全是好事：神恩降临、朝圣潮很香，但供品荒、异端审判、
/// 以及最现代的一种——「玩梗潮」（大家只玩梗，没人烧香）也很真实。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// <para>
/// 第 5 层（克苏鲁猫）会把<b>所有</b>增益的时长乘 0.5——那是这一层的规则
/// （<see cref="Eras"/>），也是它"全局 ×3"的代价：看得越久，掉 san 越快。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "divine_frenzy",
            Name = "神恩降临",
            Icon = "✨",
            Description = "天上的云裂开一条缝，光正好打在她的神龛上。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "manifest_frenzy",
            Name = "显灵停不下来",
            Icon = "🙌",
            Description = "她一口气满足了三十七个愿望，其中三十六个是「再来一次」。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "pilgrim_flood",
            Name = "朝圣潮",
            Icon = "🚶",
            Description = "路上全是人，队伍从山脚排到神殿门口，有人带着帐篷。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "offering_shortage",
            Name = "供品荒",
            Icon = "📉",
            Description = "今年收成不好，祭坛上摆的是塑料水果。她照样收了，但气压有点低。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "heresy_trial",
            Name = "异端审判",
            Icon = "⚖️",
            Description = "隔壁那套神话派人来辩论，辩到一半开始互相举报。她被要求「先停业配合调查」。全部产量 ×0.6。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "meme_wave",
            Name = "玩梗潮",
            Icon = "😹",
            Description = "所有人都在转那张图，没有一个人记得她管什么。热度是真的，香火是假的。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "prime_time",
            Name = "黄金档",
            Icon = "📹",
            Description = "平台把她排进了首页推荐位。补光灯全开，弹幕滚得看不清。直播间产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("stream_studio", 30)],
        },
        new()
        {
            Id = "sutra_reading",
            Name = "诵经",
            Icon = "📿",
            Description = "整座神殿同时开口，声音居然是对齐的。她愣了两秒，然后跟着念。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
