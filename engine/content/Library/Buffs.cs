using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 蠹虫带来的东西不全是坏事：畅销能让整馆爆满，但纸荒、抄本泛滥、查禁也会同时压下来。
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
            Id = "bookworm_swarm",
            Name = Prose.Text("buffs", "bookworm_swarm", "name"),
            Icon = Prose.Text("buffs", "bookworm_swarm", "icon"),
            Description = Prose.Text("buffs", "bookworm_swarm", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "overnight_draft",
            Name = Prose.Text("buffs", "overnight_draft", "name"),
            Icon = Prose.Text("buffs", "overnight_draft", "icon"),
            Description = Prose.Text("buffs", "overnight_draft", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "bestseller",
            Name = Prose.Text("buffs", "bestseller", "name"),
            Icon = Prose.Text("buffs", "bestseller", "icon"),
            Description = Prose.Text("buffs", "bestseller", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "paper_shortage",
            Name = Prose.Text("buffs", "paper_shortage", "name"),
            Icon = Prose.Text("buffs", "paper_shortage", "icon"),
            Description = Prose.Text("buffs", "paper_shortage", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "plagiarism",
            Name = Prose.Text("buffs", "plagiarism", "name"),
            Icon = Prose.Text("buffs", "plagiarism", "icon"),
            Description = Prose.Text("buffs", "plagiarism", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "censorship",
            Name = Prose.Text("buffs", "censorship", "name"),
            Icon = Prose.Text("buffs", "censorship", "icon"),
            Description = Prose.Text("buffs", "censorship", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "night_reading_room",
            Name = Prose.Text("buffs", "night_reading_room", "name"),
            Icon = Prose.Text("buffs", "night_reading_room", "icon"),
            Description = Prose.Text("buffs", "night_reading_room", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("reading_room", 30)],
        },
        new()
        {
            Id = "proofread",
            Name = Prose.Text("buffs", "proofread", "name"),
            Icon = Prose.Text("buffs", "proofread", "icon"),
            Description = Prose.Text("buffs", "proofread", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
