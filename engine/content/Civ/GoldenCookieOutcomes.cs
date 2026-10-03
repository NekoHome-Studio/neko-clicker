using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 「天灾」结果表（11 条）。<para>
/// 换皮自金猫：文明演进路上砸下来的那些事——不全是坏事。丰收年、技术突破、黄金时代很香，
/// 但洪水、瘟疫、长冬、陨石也很真实。权重总和 131，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比（正面结果合计约 92%，稀有约 5%，负面约 3%）。
/// </para>
/// <para>
/// 「黄金时代」是唯一的 ×30：它对应设计里那句"文明偶尔会自己往前跳一大步"，
/// 而它的权重压得很低（3）——玩家应该偶尔撞上它，而不是指望它。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "old_granary",
            Name = Prose.Text("goldenCookies", "old_granary", "name"),
            Icon = Prose.Text("goldenCookies", "old_granary", "icon"),
            Description = Prose.Text("goldenCookies", "old_granary", "description"),
            Weight = 40,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "harvest",
            Name = Prose.Text("goldenCookies", "harvest", "name"),
            Icon = Prose.Text("goldenCookies", "harvest", "icon"),
            Description = Prose.Text("goldenCookies", "harvest", "description"),
            Weight = 28,
            BuffId = "harvest_year",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "late_night",
            Name = Prose.Text("goldenCookies", "late_night", "name"),
            Icon = Prose.Text("goldenCookies", "late_night", "icon"),
            Description = Prose.Text("goldenCookies", "late_night", "description"),
            Weight = 8,
            BuffId = "all_nighter",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "invention",
            Name = Prose.Text("goldenCookies", "invention", "name"),
            Icon = Prose.Text("goldenCookies", "invention", "icon"),
            Description = Prose.Text("goldenCookies", "invention", "description"),
            Weight = 4,
            BuffId = "breakthrough",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "flood",
            Name = Prose.Text("goldenCookies", "flood", "name"),
            Icon = Prose.Text("goldenCookies", "flood", "icon"),
            Description = Prose.Text("goldenCookies", "flood", "description"),
            Weight = 14,
            BuffId = "flood",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "plague",
            Name = Prose.Text("goldenCookies", "plague", "name"),
            Icon = Prose.Text("goldenCookies", "plague", "icon"),
            Description = Prose.Text("goldenCookies", "plague", "description"),
            Weight = 12,
            BuffId = "plague",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "long_winter",
            Name = Prose.Text("goldenCookies", "long_winter", "name"),
            Icon = Prose.Text("goldenCookies", "long_winter", "icon"),
            Description = Prose.Text("goldenCookies", "long_winter", "description"),
            Weight = 9,
            BuffId = "long_winter",
            BuffSeconds = 72,
        },
        new()
        {
            Id = "meteor",
            Name = Prose.Text("goldenCookies", "meteor", "name"),
            Icon = Prose.Text("goldenCookies", "meteor", "icon"),
            Description = Prose.Text("goldenCookies", "meteor", "description"),
            Weight = 7,
            BuffId = "meteor",
            BuffSeconds = 54,
        },
        new()
        {
            Id = "night_academy",
            Name = Prose.Text("goldenCookies", "night_academy", "name"),
            Icon = Prose.Text("goldenCookies", "night_academy", "icon"),
            Description = Prose.Text("goldenCookies", "night_academy", "description"),
            Weight = 4,
            BuffId = "night_shift",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "golden_age",
            Name = Prose.Text("goldenCookies", "golden_age", "name"),
            Icon = Prose.Text("goldenCookies", "golden_age", "icon"),
            Description = Prose.Text("goldenCookies", "golden_age", "description"),
            Weight = 3,
            BuffId = "golden_age",
            BuffSeconds = 45,
        },
        new()
        {
            Id = "two_winters",
            Name = Prose.Text("goldenCookies", "two_winters", "name"),
            Icon = Prose.Text("goldenCookies", "two_winters", "icon"),
            Description = Prose.Text("goldenCookies", "two_winters", "description"),
            Weight = 2,
            BuffId = "long_winter",
            BuffSeconds = 72,
            SecondaryBuffId = "flood",
            SecondaryBuffSeconds = 66,
        },
    ];
}
