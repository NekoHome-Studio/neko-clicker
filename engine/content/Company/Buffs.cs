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
            Id = "requirement_change",
            Name = Prose.Text("buffs", "requirement_change", "name"),
            Icon = Prose.Text("buffs", "requirement_change", "icon"),
            Description = Prose.Text("buffs", "requirement_change", "description"),
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
            Id = "funding_round",
            Name = Prose.Text("buffs", "funding_round", "name"),
            Icon = Prose.Text("buffs", "funding_round", "icon"),
            Description = Prose.Text("buffs", "funding_round", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "server_outage",
            Name = Prose.Text("buffs", "server_outage", "name"),
            Icon = Prose.Text("buffs", "server_outage", "icon"),
            Description = Prose.Text("buffs", "server_outage", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "viral_post",
            Name = Prose.Text("buffs", "viral_post", "name"),
            Icon = Prose.Text("buffs", "viral_post", "icon"),
            Description = Prose.Text("buffs", "viral_post", "description"),
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(25)],
        },
        new()
        {
            Id = "poached",
            Name = Prose.Text("buffs", "poached", "name"),
            Icon = Prose.Text("buffs", "poached", "icon"),
            Description = Prose.Text("buffs", "poached", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "team_offsite",
            Name = Prose.Text("buffs", "team_offsite", "name"),
            Icon = Prose.Text("buffs", "team_offsite", "icon"),
            Description = Prose.Text("buffs", "team_offsite", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("desk", 30)],
        },
        new()
        {
            Id = "year_end_bonus",
            Name = Prose.Text("buffs", "year_end_bonus", "name"),
            Icon = Prose.Text("buffs", "year_end_bonus", "icon"),
            Description = Prose.Text("buffs", "year_end_bonus", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
