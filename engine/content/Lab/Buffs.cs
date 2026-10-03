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
            Id = "containment_breach",
            Name = Prose.Text("buffs", "containment_breach", "name"),
            Icon = Prose.Text("buffs", "containment_breach", "icon"),
            Description = Prose.Text("buffs", "containment_breach", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "data_purge",
            Name = Prose.Text("buffs", "data_purge", "name"),
            Icon = Prose.Text("buffs", "data_purge", "icon"),
            Description = Prose.Text("buffs", "data_purge", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "grant_approved",
            Name = Prose.Text("buffs", "grant_approved", "name"),
            Icon = Prose.Text("buffs", "grant_approved", "icon"),
            Description = Prose.Text("buffs", "grant_approved", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "power_fluctuation",
            Name = Prose.Text("buffs", "power_fluctuation", "name"),
            Icon = Prose.Text("buffs", "power_fluctuation", "icon"),
            Description = Prose.Text("buffs", "power_fluctuation", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
        },
        new()
        {
            Id = "gene_batch",
            Name = Prose.Text("buffs", "gene_batch", "name"),
            Icon = Prose.Text("buffs", "gene_batch", "icon"),
            Description = Prose.Text("buffs", "gene_batch", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("gene_bank", 30)],
        },
        new()
        {
            Id = "awake_window",
            Name = Prose.Text("buffs", "awake_window", "name"),
            Icon = Prose.Text("buffs", "awake_window", "icon"),
            Description = Prose.Text("buffs", "awake_window", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
        new()
        {
            Id = "ethics_hearing",
            Name = Prose.Text("buffs", "ethics_hearing", "name"),
            Icon = Prose.Text("buffs", "ethics_hearing", "icon"),
            Description = Prose.Text("buffs", "ethics_hearing", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
        },
        new()
        {
            Id = "archive_insight",
            Name = Prose.Text("buffs", "archive_insight", "name"),
            Icon = Prose.Text("buffs", "archive_insight", "icon"),
            Description = Prose.Text("buffs", "archive_insight", "description"),
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(25)],
        },
    ];
}
