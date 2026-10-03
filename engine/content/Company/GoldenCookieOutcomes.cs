using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 「甲方改需求」结果表（10 条）。<para>
/// 换皮自金猫：跳出来的是甲方，而甲方带来的不全是坏事——融资、出圈都很香，
/// 但半夜改需求、宕机、挖角也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
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
            Id = "scope_creep",
            Name = Prose.Text("goldenCookies", "scope_creep", "name"),
            Icon = Prose.Text("goldenCookies", "scope_creep", "icon"),
            Description = Prose.Text("goldenCookies", "scope_creep", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "midnight_change",
            Name = Prose.Text("goldenCookies", "midnight_change", "name"),
            Icon = Prose.Text("goldenCookies", "midnight_change", "icon"),
            Description = Prose.Text("goldenCookies", "midnight_change", "description"),
            Weight = 32,
            BuffId = "requirement_change",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "overtime_sprint",
            Name = Prose.Text("goldenCookies", "overtime_sprint", "name"),
            Icon = Prose.Text("goldenCookies", "overtime_sprint", "icon"),
            Description = Prose.Text("goldenCookies", "overtime_sprint", "description"),
            Weight = 10,
            BuffId = "all_nighter",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "client_vanished",
            Name = Prose.Text("goldenCookies", "client_vanished", "name"),
            Icon = Prose.Text("goldenCookies", "client_vanished", "icon"),
            Description = Prose.Text("goldenCookies", "client_vanished", "description"),
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "empty_meeting",
            Name = Prose.Text("goldenCookies", "empty_meeting", "name"),
            Icon = Prose.Text("goldenCookies", "empty_meeting", "icon"),
            Description = Prose.Text("goldenCookies", "empty_meeting", "description"),
            Weight = 3,
        },
        new()
        {
            Id = "funding_round",
            Name = Prose.Text("goldenCookies", "funding_round", "name"),
            Icon = Prose.Text("goldenCookies", "funding_round", "icon"),
            Description = Prose.Text("goldenCookies", "funding_round", "description"),
            Weight = 5,
            BuffId = "funding_round",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "viral_post",
            Name = Prose.Text("goldenCookies", "viral_post", "name"),
            Icon = Prose.Text("goldenCookies", "viral_post", "icon"),
            Description = Prose.Text("goldenCookies", "viral_post", "description"),
            Weight = 6,
            BuffId = "viral_post",
            BuffSeconds = 45,
        },
        new()
        {
            Id = "double_change",
            Name = Prose.Text("goldenCookies", "double_change", "name"),
            Icon = Prose.Text("goldenCookies", "double_change", "icon"),
            Description = Prose.Text("goldenCookies", "double_change", "description"),
            Weight = 2,
            BuffId = "requirement_change",
            BuffSeconds = 77,
            SecondaryBuffId = "all_nighter",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "server_outage",
            Name = Prose.Text("goldenCookies", "server_outage", "name"),
            Icon = Prose.Text("goldenCookies", "server_outage", "icon"),
            Description = Prose.Text("goldenCookies", "server_outage", "description"),
            Weight = 22,
            BuffId = "server_outage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "team_offsite",
            Name = Prose.Text("goldenCookies", "team_offsite", "name"),
            Icon = Prose.Text("goldenCookies", "team_offsite", "icon"),
            Description = Prose.Text("goldenCookies", "team_offsite", "description"),
            Weight = 6,
            BuffId = "team_offsite",
            BuffSeconds = 30,
        },
    ];
}
