using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 限时增益。<para>
/// 增益是增量游戏里少见的"横向"资源：它不改变长期曲线，但改变<b>当下该做什么</b>——
/// 狂热期间应该去点金猫、点击狂热期间应该疯狂点屏幕。数值设计上刻意做成
/// "短时间超高倍率"，这样它的价值取决于玩家是否在线并作出反应。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Buildings.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Buildings.Prose;

    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "frenzy",
            Name = Prose.Text("buffs", "frenzy", "name"),
            Icon = Prose.Text("buffs", "frenzy", "icon"),
            Description = Prose.Text("buffs", "frenzy", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "click_frenzy",
            Name = Prose.Text("buffs", "click_frenzy", "name"),
            Icon = Prose.Text("buffs", "click_frenzy", "icon"),
            Description = Prose.Text("buffs", "click_frenzy", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "cat_god_descends",
            Name = Prose.Text("buffs", "cat_god_descends", "name"),
            Icon = Prose.Text("buffs", "cat_god_descends", "icon"),
            Description = Prose.Text("buffs", "cat_god_descends", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "elder_frenzy",
            Name = Prose.Text("buffs", "elder_frenzy", "name"),
            Icon = Prose.Text("buffs", "elder_frenzy", "icon"),
            Description = Prose.Text("buffs", "elder_frenzy", "description"),
            Duration = 12,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(666)],
        },
        new()
        {
            Id = "cotton_bed_frenzy",
            Name = Prose.Text("buffs", "cotton_bed_frenzy", "name"),
            Icon = Prose.Text("buffs", "cotton_bed_frenzy", "icon"),
            Description = Prose.Text("buffs", "cotton_bed_frenzy", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("cat_bed", 30)],
        },
        new()
        {
            Id = "clumsy_paws",
            Name = Prose.Text("buffs", "clumsy_paws", "name"),
            Icon = Prose.Text("buffs", "clumsy_paws", "icon"),
            Description = Prose.Text("buffs", "clumsy_paws", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
    ];
}

/// <summary>
/// 金猫结果表。<para>
/// 权重不是随手写的：把"幸运"和"狂热"的权重设在 70% 以上，是为了保证玩家每次点金猫
/// <b>大概率有正反馈</b>；负面结果（猫粮被抢）只占约 4%，并且只扣 5% 存量、不会扣成负数。
/// 稀有结果（合计约 6%）负责制造"截图发群"的时刻。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Buildings.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Buildings.Prose;

    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "lucky",
            Name = Prose.Text("goldenCookies", "lucky", "name"),
            Icon = Prose.Text("goldenCookies", "lucky", "icon"),
            Description = Prose.Text("goldenCookies", "lucky", "description"),
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "frenzy",
            Name = Prose.Text("goldenCookies", "frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "frenzy", "description"),
            Weight = 30,
            BuffId = "frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = Prose.Text("goldenCookies", "click_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "click_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "click_frenzy", "description"),
            Weight = 8,
            BuffId = "click_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "ruin",
            Name = Prose.Text("goldenCookies", "ruin", "name"),
            Icon = Prose.Text("goldenCookies", "ruin", "icon"),
            Description = Prose.Text("goldenCookies", "ruin", "description"),
            Weight = 4,
            StealBankFraction = 0.05,
            IsRare = true,
        },
        new()
        {
            Id = "blab",
            Name = Prose.Text("goldenCookies", "blab", "name"),
            Icon = Prose.Text("goldenCookies", "blab", "icon"),
            Description = Prose.Text("goldenCookies", "blab", "description"),
            Weight = 2,
        },
        new()
        {
            Id = "cotton_bed",
            Name = Prose.Text("goldenCookies", "cotton_bed", "name"),
            Icon = Prose.Text("goldenCookies", "cotton_bed", "icon"),
            Description = Prose.Text("goldenCookies", "cotton_bed", "description"),
            Weight = 3,
            BuffId = "cotton_bed_frenzy",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = Prose.Text("goldenCookies", "bloodlust", "name"),
            Icon = Prose.Text("goldenCookies", "bloodlust", "icon"),
            Description = Prose.Text("goldenCookies", "bloodlust", "description"),
            Weight = 3,
            BuffId = "cat_god_descends",
            BuffSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "chain",
            Name = Prose.Text("goldenCookies", "chain", "name"),
            Icon = Prose.Text("goldenCookies", "chain", "icon"),
            Description = Prose.Text("goldenCookies", "chain", "description"),
            Weight = 1,
            BuffId = "frenzy",
            BuffSeconds = 30,
            SecondaryBuffId = "click_frenzy",
            SecondaryBuffSeconds = 10,
            IsRare = true,
        },
        new()
        {
            Id = "elder_frenzy",
            Name = Prose.Text("goldenCookies", "elder_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "elder_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "elder_frenzy", "description"),
            Weight = 1,
            BuffId = "elder_frenzy",
            BuffSeconds = 12,
            IsRare = true,
        },
        new()
        {
            Id = "clumsy",
            Name = Prose.Text("goldenCookies", "clumsy", "name"),
            Icon = Prose.Text("goldenCookies", "clumsy", "icon"),
            Description = Prose.Text("goldenCookies", "clumsy", "description"),
            Weight = 2,
            BuffId = "clumsy_paws",
            BuffSeconds = 66,
            IsRare = true,
        },
    ];
}
