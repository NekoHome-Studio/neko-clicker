using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 限时增益表（9 条）。<para>
/// 天灾带来的东西不全是坏事：丰收年能让整片地里的产能翻倍，技术突破能让她跳一整代，
/// 但洪水、瘟疫、长冬、陨石也很真实。数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比——<b>换包换的是叙事，不是手感</b>。
/// </para>
/// <para>
/// 第 3 层（城墙 / 帝国）的规则是"天灾间隔更稀"，所以那一层里每条增益的相对价值更高；
/// 第 5 层的间隔最密，于是增益在这层几乎连成一片——这是刻意的：末层的规则就是"什么都快"。
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
            Id = "harvest_year",
            Name = Prose.Text("buffs", "harvest_year", "name"),
            Icon = Prose.Text("buffs", "harvest_year", "icon"),
            Description = Prose.Text("buffs", "harvest_year", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "all_nighter",
            Name = Prose.Text("buffs", "all_nighter", "name"),
            Icon = Prose.Text("buffs", "all_nighter", "icon"),
            Description = Prose.Text("buffs", "all_nighter", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "breakthrough",
            Name = Prose.Text("buffs", "breakthrough", "name"),
            Icon = Prose.Text("buffs", "breakthrough", "icon"),
            Description = Prose.Text("buffs", "breakthrough", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "golden_age",
            Name = Prose.Text("buffs", "golden_age", "name"),
            Icon = Prose.Text("buffs", "golden_age", "icon"),
            Description = Prose.Text("buffs", "golden_age", "description"),
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(30)],
        },
        new()
        {
            Id = "flood",
            Name = Prose.Text("buffs", "flood", "name"),
            Icon = Prose.Text("buffs", "flood", "icon"),
            Description = Prose.Text("buffs", "flood", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
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
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "long_winter",
            Name = Prose.Text("buffs", "long_winter", "name"),
            Icon = Prose.Text("buffs", "long_winter", "icon"),
            Description = Prose.Text("buffs", "long_winter", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "meteor",
            Name = Prose.Text("buffs", "meteor", "name"),
            Icon = Prose.Text("buffs", "meteor", "icon"),
            Description = Prose.Text("buffs", "meteor", "description"),
            Duration = 54,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.75)],
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
            Modifiers = [Modifier.BuildingMultiplier("academy", 30)],
        },
    ];
}
