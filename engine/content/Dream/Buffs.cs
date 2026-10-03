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
/// "梦会不听你的"这件事被玩家摸到（详见 <c>games/docs/NINE_LIVES_DESIGN.md</c> §2 的梦层语气）。
/// 它们全都是限时的，且没有一条会动到「梦境能量」——第二资源只涨不落。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "lucid_dream",
            Name = Prose.Text("buffs", "lucid_dream", "name"),
            Icon = Prose.Text("buffs", "lucid_dream", "icon"),
            Description = Prose.Text("buffs", "lucid_dream", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "controlled_dream",
            Name = Prose.Text("buffs", "controlled_dream", "name"),
            Icon = Prose.Text("buffs", "controlled_dream", "icon"),
            Description = Prose.Text("buffs", "controlled_dream", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "dream_leap",
            Name = Prose.Text("buffs", "dream_leap", "name"),
            Icon = Prose.Text("buffs", "dream_leap", "icon"),
            Description = Prose.Text("buffs", "dream_leap", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "sleep_paralysis",
            Name = Prose.Text("buffs", "sleep_paralysis", "name"),
            Icon = Prose.Text("buffs", "sleep_paralysis", "icon"),
            Description = Prose.Text("buffs", "sleep_paralysis", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "falling",
            Name = Prose.Text("buffs", "falling", "name"),
            Icon = Prose.Text("buffs", "falling", "icon"),
            Description = Prose.Text("buffs", "falling", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "nightmare_tide",
            Name = Prose.Text("buffs", "nightmare_tide", "name"),
            Icon = Prose.Text("buffs", "nightmare_tide", "icon"),
            Description = Prose.Text("buffs", "nightmare_tide", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "deeper_layer",
            Name = Prose.Text("buffs", "deeper_layer", "name"),
            Icon = Prose.Text("buffs", "deeper_layer", "icon"),
            Description = Prose.Text("buffs", "deeper_layer", "description"),
            Duration = 45,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.GlobalMultiplier(2.5)],
        },
        new()
        {
            Id = "dream_core_pulse",
            Name = Prose.Text("buffs", "dream_core_pulse", "name"),
            Icon = Prose.Text("buffs", "dream_core_pulse", "icon"),
            Description = Prose.Text("buffs", "dream_core_pulse", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("dream_core", 30)],
        },
        new()
        {
            Id = "crafting",
            Name = Prose.Text("buffs", "crafting", "name"),
            Icon = Prose.Text("buffs", "crafting", "icon"),
            Description = Prose.Text("buffs", "crafting", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
        new()
        {
            Id = "waking_moment",
            Name = Prose.Text("buffs", "waking_moment", "name"),
            Icon = Prose.Text("buffs", "waking_moment", "icon"),
            Description = Prose.Text("buffs", "waking_moment", "description"),
            Duration = 40,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(9)],
        },
    ];
}
