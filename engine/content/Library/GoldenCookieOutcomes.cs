using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 「蠹虫」结果表（10 条）。<para>
/// 换皮自金猫：从书页里钻出来的虫子，带来的不全是坏事——畅销、加印都很香，
/// 但纸荒、抄本、查禁也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
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
            Id = "unsold_stock",
            Name = Prose.Text("goldenCookies", "unsold_stock", "name"),
            Icon = Prose.Text("goldenCookies", "unsold_stock", "icon"),
            Description = Prose.Text("goldenCookies", "unsold_stock", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "swarm",
            Name = Prose.Text("goldenCookies", "swarm", "name"),
            Icon = Prose.Text("goldenCookies", "swarm", "icon"),
            Description = Prose.Text("goldenCookies", "swarm", "description"),
            Weight = 32,
            BuffId = "bookworm_swarm",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "deadline",
            Name = Prose.Text("goldenCookies", "deadline", "name"),
            Icon = Prose.Text("goldenCookies", "deadline", "icon"),
            Description = Prose.Text("goldenCookies", "deadline", "description"),
            Weight = 10,
            BuffId = "overnight_draft",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "stolen_pages",
            Name = Prose.Text("goldenCookies", "stolen_pages", "name"),
            Icon = Prose.Text("goldenCookies", "stolen_pages", "icon"),
            Description = Prose.Text("goldenCookies", "stolen_pages", "description"),
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
            Id = "bestseller",
            Name = Prose.Text("goldenCookies", "bestseller", "name"),
            Icon = Prose.Text("goldenCookies", "bestseller", "icon"),
            Description = Prose.Text("goldenCookies", "bestseller", "description"),
            Weight = 5,
            BuffId = "bestseller",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "proofread",
            Name = Prose.Text("goldenCookies", "proofread", "name"),
            Icon = Prose.Text("goldenCookies", "proofread", "icon"),
            Description = Prose.Text("goldenCookies", "proofread", "description"),
            Weight = 6,
            BuffId = "proofread",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "second_worm",
            Name = Prose.Text("goldenCookies", "second_worm", "name"),
            Icon = Prose.Text("goldenCookies", "second_worm", "icon"),
            Description = Prose.Text("goldenCookies", "second_worm", "description"),
            Weight = 2,
            BuffId = "bookworm_swarm",
            BuffSeconds = 77,
            SecondaryBuffId = "overnight_draft",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "paper_shortage",
            Name = Prose.Text("goldenCookies", "paper_shortage", "name"),
            Icon = Prose.Text("goldenCookies", "paper_shortage", "icon"),
            Description = Prose.Text("goldenCookies", "paper_shortage", "description"),
            Weight = 22,
            BuffId = "paper_shortage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "night_reading_room",
            Name = Prose.Text("goldenCookies", "night_reading_room", "name"),
            Icon = Prose.Text("goldenCookies", "night_reading_room", "icon"),
            Description = Prose.Text("goldenCookies", "night_reading_room", "description"),
            Weight = 6,
            BuffId = "night_reading_room",
            BuffSeconds = 30,
        },
    ];
}
