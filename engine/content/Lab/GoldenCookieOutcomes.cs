using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 「实验事故」结果表（10 条）。<para>
/// 换皮自金猫：出现的是<b>事故</b>而不是好运，所以结果分布比别的包更偏——
/// 坏事与"看起来像坏事"的比例明显更高，权重最高的那条也只是"数据涌入"这种中性偏好的事。
/// </para>
/// <para>
/// 数值沿用已验证的配方（权重总和约 140，银行抽成 0.15、封顶 900 秒产量），
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
            Id = "data_surge",
            Name = Prose.Text("goldenCookies", "data_surge", "name"),
            Icon = Prose.Text("goldenCookies", "data_surge", "icon"),
            Description = Prose.Text("goldenCookies", "data_surge", "description"),
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "breach",
            Name = Prose.Text("goldenCookies", "breach", "name"),
            Icon = Prose.Text("goldenCookies", "breach", "icon"),
            Description = Prose.Text("goldenCookies", "breach", "description"),
            Weight = 30,
            BuffId = "containment_breach",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "purge",
            Name = Prose.Text("goldenCookies", "purge", "name"),
            Icon = Prose.Text("goldenCookies", "purge", "icon"),
            Description = Prose.Text("goldenCookies", "purge", "description"),
            Weight = 8,
            BuffId = "data_purge",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "blackout",
            Name = Prose.Text("goldenCookies", "blackout", "name"),
            Icon = Prose.Text("goldenCookies", "blackout", "icon"),
            Description = Prose.Text("goldenCookies", "blackout", "description"),
            Weight = 5,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "glance",
            Name = Prose.Text("goldenCookies", "glance", "name"),
            Icon = Prose.Text("goldenCookies", "glance", "icon"),
            Description = Prose.Text("goldenCookies", "glance", "description"),
            Weight = 2,
        },
        new()
        {
            Id = "gene_expression",
            Name = Prose.Text("goldenCookies", "gene_expression", "name"),
            Icon = Prose.Text("goldenCookies", "gene_expression", "icon"),
            Description = Prose.Text("goldenCookies", "gene_expression", "description"),
            Weight = 3,
            BuffId = "gene_batch",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "budget",
            Name = Prose.Text("goldenCookies", "budget", "name"),
            Icon = Prose.Text("goldenCookies", "budget", "icon"),
            Description = Prose.Text("goldenCookies", "budget", "description"),
            Weight = 3,
            BuffId = "grant_approved",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "cascade",
            Name = Prose.Text("goldenCookies", "cascade", "name"),
            Icon = Prose.Text("goldenCookies", "cascade", "icon"),
            Description = Prose.Text("goldenCookies", "cascade", "description"),
            Weight = 1,
            BuffId = "containment_breach",
            BuffSeconds = 77,
            SecondaryBuffId = "data_purge",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "hearing",
            Name = Prose.Text("goldenCookies", "hearing", "name"),
            Icon = Prose.Text("goldenCookies", "hearing", "icon"),
            Description = Prose.Text("goldenCookies", "hearing", "description"),
            Weight = 6,
            BuffId = "ethics_hearing",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "insight",
            Name = Prose.Text("goldenCookies", "insight", "name"),
            Icon = Prose.Text("goldenCookies", "insight", "icon"),
            Description = Prose.Text("goldenCookies", "insight", "description"),
            Weight = 2,
            BuffId = "archive_insight",
            BuffSeconds = 45,
        },
    ];
}
