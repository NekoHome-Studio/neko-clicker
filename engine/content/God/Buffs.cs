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
            Id = "divine_frenzy",
            Name = Prose.Text("buffs", "divine_frenzy", "name"),
            Icon = Prose.Text("buffs", "divine_frenzy", "icon"),
            Description = Prose.Text("buffs", "divine_frenzy", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "manifest_frenzy",
            Name = Prose.Text("buffs", "manifest_frenzy", "name"),
            Icon = Prose.Text("buffs", "manifest_frenzy", "icon"),
            Description = Prose.Text("buffs", "manifest_frenzy", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "pilgrim_flood",
            Name = Prose.Text("buffs", "pilgrim_flood", "name"),
            Icon = Prose.Text("buffs", "pilgrim_flood", "icon"),
            Description = Prose.Text("buffs", "pilgrim_flood", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "offering_shortage",
            Name = Prose.Text("buffs", "offering_shortage", "name"),
            Icon = Prose.Text("buffs", "offering_shortage", "icon"),
            Description = Prose.Text("buffs", "offering_shortage", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "heresy_trial",
            Name = Prose.Text("buffs", "heresy_trial", "name"),
            Icon = Prose.Text("buffs", "heresy_trial", "icon"),
            Description = Prose.Text("buffs", "heresy_trial", "description"),
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "meme_wave",
            Name = Prose.Text("buffs", "meme_wave", "name"),
            Icon = Prose.Text("buffs", "meme_wave", "icon"),
            Description = Prose.Text("buffs", "meme_wave", "description"),
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "prime_time",
            Name = Prose.Text("buffs", "prime_time", "name"),
            Icon = Prose.Text("buffs", "prime_time", "icon"),
            Description = Prose.Text("buffs", "prime_time", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("stream_studio", 30)],
        },
        new()
        {
            Id = "sutra_reading",
            Name = Prose.Text("buffs", "sutra_reading", "name"),
            Icon = Prose.Text("buffs", "sutra_reading", "icon"),
            Description = Prose.Text("buffs", "sutra_reading", "description"),
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
