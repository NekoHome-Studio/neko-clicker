using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的限时增益表（5 条）。<para>
/// 结构与九命轮回包完全一致，只换名称与目标建筑——这就是"同一机制十种包装"的最小样本。
/// 增益的定位是短时间高倍率，价值取决于玩家是否在线并作出反应，与离线收益互补。
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
            Id = "caffeine_overload",
            Name = Prose.Text("buffs", "caffeine_overload", "name"),
            Icon = Prose.Text("buffs", "caffeine_overload", "icon"),
            Description = Prose.Text("buffs", "caffeine_overload", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "cat_chorus",
            Name = Prose.Text("buffs", "cat_chorus", "name"),
            Icon = Prose.Text("buffs", "cat_chorus", "icon"),
            Description = Prose.Text("buffs", "cat_chorus", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "boss_treats",
            Name = Prose.Text("buffs", "boss_treats", "name"),
            Icon = Prose.Text("buffs", "boss_treats", "icon"),
            Description = Prose.Text("buffs", "boss_treats", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "failed_steam",
            Name = Prose.Text("buffs", "failed_steam", "name"),
            Icon = Prose.Text("buffs", "failed_steam", "icon"),
            Description = Prose.Text("buffs", "failed_steam", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "new_beans",
            Name = Prose.Text("buffs", "new_beans", "name"),
            Icon = Prose.Text("buffs", "new_beans", "icon"),
            Description = Prose.Text("buffs", "new_beans", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("bakery", 30)],
        },
    ];
}
