using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 限时增益表（8 条，C1《猫娘电台》）。<para>
/// 听众来电带来的东西不全是好事：爆款能让整台爆满，但信号干扰、空播、频偏也会同时压下来。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），随机事件力度与其它包可比。
/// </para>
/// <para>
/// 注意「回授啸叫」这条点击增益在本包是<b>双刃</b>：点击本身会打断播音掉收听率（见 <see cref="BroadcastModule"/>），
/// 所以哪怕点击被放大，乱点仍是亏的——这正是本包反转核心动词的一部分。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>本包的 text.json：增益文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "viral_broadcast",
            Name = Prose.Text("buffs", "viral_broadcast", "name"),
            Icon = Prose.Text("buffs", "viral_broadcast", "icon"),
            Description = Prose.Text("buffs", "viral_broadcast", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "feedback_loop",
            Name = Prose.Text("buffs", "feedback_loop", "name"),
            Icon = Prose.Text("buffs", "feedback_loop", "icon"),
            Description = Prose.Text("buffs", "feedback_loop", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "prime_time_slot",
            Name = Prose.Text("buffs", "prime_time_slot", "name"),
            Icon = Prose.Text("buffs", "prime_time_slot", "icon"),
            Description = Prose.Text("buffs", "prime_time_slot", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "signal_jam",
            Name = Prose.Text("buffs", "signal_jam", "name"),
            Icon = Prose.Text("buffs", "signal_jam", "icon"),
            Description = Prose.Text("buffs", "signal_jam", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "dead_air",
            Name = Prose.Text("buffs", "dead_air", "name"),
            Icon = Prose.Text("buffs", "dead_air", "icon"),
            Description = Prose.Text("buffs", "dead_air", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "frequency_drift",
            Name = Prose.Text("buffs", "frequency_drift", "name"),
            Icon = Prose.Text("buffs", "frequency_drift", "icon"),
            Description = Prose.Text("buffs", "frequency_drift", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "night_shift",
            Name = Prose.Text("buffs", "night_shift", "name"),
            Icon = Prose.Text("buffs", "night_shift", "icon"),
            Description = Prose.Text("buffs", "night_shift", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("studio", 30)],
        },
        new()
        {
            Id = "sound_check",
            Name = Prose.Text("buffs", "sound_check", "name"),
            Icon = Prose.Text("buffs", "sound_check", "icon"),
            Description = Prose.Text("buffs", "sound_check", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
