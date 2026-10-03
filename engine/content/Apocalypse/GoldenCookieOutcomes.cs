using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 「变异体」结果表（10 条）。<para>
/// 换皮自金猫：从废墟里钻出来的东西，带来的不全是坏事——变异潮与战前缓存都很香，
/// 但断电、辐射、疫病也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比。
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
            Id = "buried_warehouse",
            Name = Prose.Text("goldenCookies", "buried_warehouse", "name"),
            Icon = Prose.Text("goldenCookies", "buried_warehouse", "icon"),
            Description = Prose.Text("goldenCookies", "buried_warehouse", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "mutation",
            Name = Prose.Text("goldenCookies", "mutation", "name"),
            Icon = Prose.Text("goldenCookies", "mutation", "icon"),
            Description = Prose.Text("goldenCookies", "mutation", "description"),
            Weight = 32,
            BuffId = "mutation_wave",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "frenzy",
            Name = Prose.Text("goldenCookies", "frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "frenzy", "description"),
            Weight = 10,
            BuffId = "scavenge_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "raid",
            Name = Prose.Text("goldenCookies", "raid", "name"),
            Icon = Prose.Text("goldenCookies", "raid", "icon"),
            Description = Prose.Text("goldenCookies", "raid", "description"),
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "nothing",
            Name = Prose.Text("goldenCookies", "nothing", "name"),
            Icon = Prose.Text("goldenCookies", "nothing", "icon"),
            Description = Prose.Text("goldenCookies", "nothing", "description"),
            Weight = 3,
        },
        new()
        {
            Id = "cache",
            Name = Prose.Text("goldenCookies", "cache", "name"),
            Icon = Prose.Text("goldenCookies", "cache", "icon"),
            Description = Prose.Text("goldenCookies", "cache", "description"),
            Weight = 5,
            BuffId = "prewar_cache",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "salvage",
            Name = Prose.Text("goldenCookies", "salvage", "name"),
            Icon = Prose.Text("goldenCookies", "salvage", "icon"),
            Description = Prose.Text("goldenCookies", "salvage", "description"),
            Weight = 6,
            BuffId = "salvage_order",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "double_mutation",
            Name = Prose.Text("goldenCookies", "double_mutation", "name"),
            Icon = Prose.Text("goldenCookies", "double_mutation", "icon"),
            Description = Prose.Text("goldenCookies", "double_mutation", "description"),
            Weight = 2,
            BuffId = "mutation_wave",
            BuffSeconds = 77,
            SecondaryBuffId = "scavenge_frenzy",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "blackout",
            Name = Prose.Text("goldenCookies", "blackout", "name"),
            Icon = Prose.Text("goldenCookies", "blackout", "icon"),
            Description = Prose.Text("goldenCookies", "blackout", "description"),
            Weight = 22,
            BuffId = "blackout",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "rain_night",
            Name = Prose.Text("goldenCookies", "rain_night", "name"),
            Icon = Prose.Text("goldenCookies", "rain_night", "icon"),
            Description = Prose.Text("goldenCookies", "rain_night", "description"),
            Weight = 6,
            BuffId = "shelter_night",
            BuffSeconds = 30,
        },
    ];
}
