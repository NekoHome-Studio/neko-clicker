using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 限时增益表（10 条）。<para>
/// 「梦魇」带来的东西不全是坏事：清明梦、梦中梦很香，但鬼压床、掉进噩梦层也很真实。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// <para>
/// <b>减益占三条，比别的包多一条</b>：这不是为了难，而是这一包的手感需要
/// "梦会不听你的"这件事被玩家摸到（详见 <c>docs/NINE_LIVES_DESIGN.md</c> §2 的梦层语气）。
/// 它们全都是限时的，且没有一条会动到「梦境能量」——第二资源只涨不落。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "lucid_dream",
            Name = "清明梦",
            Icon = "💡",
            Description = "她忽然知道自己在做梦。知道之后，整层梦都听她的。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "controlled_dream",
            Name = "梦中梦",
            Icon = "🌀",
            Description = "梦见自己在睡，然后又在那一层里睡着。醒来的时候要往上数四层。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "dream_leap",
            Name = "入梦",
            Icon = "⤵️",
            Description = "她一脚踏空，落进更深的那一层。落地的时候手是张开的，什么都抓得住。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "sleep_paralysis",
            Name = "鬼压床",
            Icon = "🪨",
            Description = "她醒了，但只有眼睛醒了。胸口上有东西坐着，它不动，也不说话。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "falling",
            Name = "坠落",
            Icon = "🕳️",
            Description = "脚下的地面没了。她往下掉的时候数着楼层，数到第四层就不敢数了。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "nightmare_tide",
            Name = "梦魇潮",
            Icon = "🌊",
            Description = "褶皱里的东西一起翻上来了。它们不追她，只是把整层梦压得很低。全部产量 ×0.6。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "deeper_layer",
            Name = "更深的一层",
            Icon = "🌌",
            Description = "梦层又往下一层。这一层比上面大得多，也亮得多。全部产量 ×2.5。",
            Duration = 45,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.GlobalMultiplier(2.5)],
        },
        new()
        {
            Id = "dream_core_pulse",
            Name = "梦核的搏动",
            Icon = "🔮",
            Description = "梦核跳了一下，整座梦跟着抖了一下。所有东西都清楚了一倍。梦核产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("dream_core", 30)],
        },
        new()
        {
            Id = "crafting",
            Name = "织梦",
            Icon = "🧶",
            Description = "她把上一晚剩下的线头接了起来。今晚的梦会接着说昨天那一句。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
        new()
        {
            Id = "waking_moment",
            Name = "将醒未醒",
            Icon = "🌅",
            Description = "她听见外面有人在收摊。那一瞬间她分得清哪个是真的。全部产量 ×9。",
            Duration = 40,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(9)],
        },
    ];
}
