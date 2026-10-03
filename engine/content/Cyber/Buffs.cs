using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 「病毒入侵」带来的东西不全是坏事：挖矿木马能让整层楼满负荷、数据洪流能让点击爆掉，
/// 但勒索、掉线、被清理也是真的。数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// <para>
/// 三条是负面：勒索（全局 ×0.55）、运维掉线（全局 ×0.7）、被清理（全局 ×0.6）。
/// 权重比别的包略高——数字层的世界本来就吵，见 <c>GoldenCookieOutcomes</c> 的权重表。
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
            Id = "mining_malware",
            Name = Prose.Text("buffs", "mining_malware", "name"),
            Icon = Prose.Text("buffs", "mining_malware", "icon"),
            Description = Prose.Text("buffs", "mining_malware", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "data_flood",
            Name = Prose.Text("buffs", "data_flood", "name"),
            Icon = Prose.Text("buffs", "data_flood", "icon"),
            Description = Prose.Text("buffs", "data_flood", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "viral",
            Name = Prose.Text("buffs", "viral", "name"),
            Icon = Prose.Text("buffs", "viral", "icon"),
            Description = Prose.Text("buffs", "viral", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "ransomware",
            Name = Prose.Text("buffs", "ransomware", "name"),
            Icon = Prose.Text("buffs", "ransomware", "icon"),
            Description = Prose.Text("buffs", "ransomware", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.55)],
            IsDebuff = true,
        },
        new()
        {
            Id = "ops_outage",
            Name = Prose.Text("buffs", "ops_outage", "name"),
            Icon = Prose.Text("buffs", "ops_outage", "icon"),
            Description = Prose.Text("buffs", "ops_outage", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "purge",
            Name = Prose.Text("buffs", "purge", "name"),
            Icon = Prose.Text("buffs", "purge", "icon"),
            Description = Prose.Text("buffs", "purge", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "racking",
            Name = Prose.Text("buffs", "racking", "name"),
            Icon = Prose.Text("buffs", "racking", "icon"),
            Description = Prose.Text("buffs", "racking", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("datacenter", 30)],
        },
        new()
        {
            Id = "root_access",
            Name = Prose.Text("buffs", "root_access", "name"),
            Icon = Prose.Text("buffs", "root_access", "icon"),
            Description = Prose.Text("buffs", "root_access", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
