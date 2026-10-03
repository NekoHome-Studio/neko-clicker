using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 变异体带来的东西不全是坏事，但负面的比例是全项目最高的：辐射、疫病、断电各占一条。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
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
            Id = "mutation_wave",
            Name = Prose.Text("buffs", "mutation_wave", "name"),
            Icon = Prose.Text("buffs", "mutation_wave", "icon"),
            Description = Prose.Text("buffs", "mutation_wave", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "scavenge_frenzy",
            Name = Prose.Text("buffs", "scavenge_frenzy", "name"),
            Icon = Prose.Text("buffs", "scavenge_frenzy", "icon"),
            Description = Prose.Text("buffs", "scavenge_frenzy", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "prewar_cache",
            Name = Prose.Text("buffs", "prewar_cache", "name"),
            Icon = Prose.Text("buffs", "prewar_cache", "icon"),
            Description = Prose.Text("buffs", "prewar_cache", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "blackout",
            Name = Prose.Text("buffs", "blackout", "name"),
            Icon = Prose.Text("buffs", "blackout", "icon"),
            Description = Prose.Text("buffs", "blackout", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "radiation_cloud",
            Name = Prose.Text("buffs", "radiation_cloud", "name"),
            Icon = Prose.Text("buffs", "radiation_cloud", "icon"),
            Description = Prose.Text("buffs", "radiation_cloud", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "plague",
            Name = Prose.Text("buffs", "plague", "name"),
            Icon = Prose.Text("buffs", "plague", "icon"),
            Description = Prose.Text("buffs", "plague", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "shelter_night",
            Name = Prose.Text("buffs", "shelter_night", "name"),
            Icon = Prose.Text("buffs", "shelter_night", "icon"),
            Description = Prose.Text("buffs", "shelter_night", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("shelter", 30)],
        },
        new()
        {
            Id = "salvage_order",
            Name = Prose.Text("buffs", "salvage_order", "name"),
            Icon = Prose.Text("buffs", "salvage_order", "icon"),
            Description = Prose.Text("buffs", "salvage_order", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
