using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 「神迹」结果表（10 条）。<para>
/// 换皮自金猫：这个包里金猫是"神迹"——天上掉下来的一次事件。
/// 权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，所以随机事件的期望值与其它包可比；
/// 「幸运 + 狂热」合计超过 70%，负面结果只占约 4%，稀有结果负责制造"截图发群"的时刻。
/// </para>
/// <para>
/// 这个包的负面结果有一半是<b>现代型</b>的：供品荒、异端审判、玩梗潮。
/// 信仰这件事在 meta 语气里最好笑的地方，就是它撞上流量之后的形状。
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
            Id = "forgotten_offering",
            Name = Prose.Text("goldenCookies", "forgotten_offering", "name"),
            Icon = Prose.Text("goldenCookies", "forgotten_offering", "icon"),
            Description = Prose.Text("goldenCookies", "forgotten_offering", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "divine_frenzy",
            Name = Prose.Text("goldenCookies", "divine_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "divine_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "divine_frenzy", "description"),
            Weight = 32,
            BuffId = "divine_frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "manifest_frenzy",
            Name = Prose.Text("goldenCookies", "manifest_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "manifest_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "manifest_frenzy", "description"),
            Weight = 10,
            BuffId = "manifest_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "snatched_offering",
            Name = Prose.Text("goldenCookies", "snatched_offering", "name"),
            Icon = Prose.Text("goldenCookies", "snatched_offering", "icon"),
            Description = Prose.Text("goldenCookies", "snatched_offering", "description"),
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
            Id = "pilgrim_flood",
            Name = Prose.Text("goldenCookies", "pilgrim_flood", "name"),
            Icon = Prose.Text("goldenCookies", "pilgrim_flood", "icon"),
            Description = Prose.Text("goldenCookies", "pilgrim_flood", "description"),
            Weight = 5,
            BuffId = "pilgrim_flood",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "sutra_reading",
            Name = Prose.Text("goldenCookies", "sutra_reading", "name"),
            Icon = Prose.Text("goldenCookies", "sutra_reading", "icon"),
            Description = Prose.Text("goldenCookies", "sutra_reading", "description"),
            Weight = 6,
            BuffId = "sutra_reading",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "double_miracle",
            Name = Prose.Text("goldenCookies", "double_miracle", "name"),
            Icon = Prose.Text("goldenCookies", "double_miracle", "icon"),
            Description = Prose.Text("goldenCookies", "double_miracle", "description"),
            Weight = 2,
            BuffId = "divine_frenzy",
            BuffSeconds = 77,
            SecondaryBuffId = "manifest_frenzy",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "offering_shortage",
            Name = Prose.Text("goldenCookies", "offering_shortage", "name"),
            Icon = Prose.Text("goldenCookies", "offering_shortage", "icon"),
            Description = Prose.Text("goldenCookies", "offering_shortage", "description"),
            Weight = 22,
            BuffId = "offering_shortage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "prime_time",
            Name = Prose.Text("goldenCookies", "prime_time", "name"),
            Icon = Prose.Text("goldenCookies", "prime_time", "icon"),
            Description = Prose.Text("goldenCookies", "prime_time", "description"),
            Weight = 6,
            BuffId = "prime_time",
            BuffSeconds = 30,
        },
    ];
}
