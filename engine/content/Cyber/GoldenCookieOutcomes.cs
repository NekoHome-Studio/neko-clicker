using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 「病毒入侵」结果表（10 条）。<para>
/// 换皮自金猫：一段不知道从哪来的代码撞进她的机群。带来的不全是坏事——
/// 挖矿木马、数据洪流、病毒式传播都很香，但勒索、掉线、被清理也很真实。
/// 权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，所以随机事件的期望值与其它包可比。
/// </para>
/// <para>
/// 三条负面结果的权重加起来 34（约 24%），比图书馆的 22 略高一点：
/// 这一层的世界更吵，玩家应该更常需要"处理"而不是"收下"。
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
            Id = "leftover_payload",
            Name = Prose.Text("goldenCookies", "leftover_payload", "name"),
            Icon = Prose.Text("goldenCookies", "leftover_payload", "icon"),
            Description = Prose.Text("goldenCookies", "leftover_payload", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "mining_malware",
            Name = Prose.Text("goldenCookies", "mining_malware", "name"),
            Icon = Prose.Text("goldenCookies", "mining_malware", "icon"),
            Description = Prose.Text("goldenCookies", "mining_malware", "description"),
            Weight = 32,
            BuffId = "mining_malware",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "payday",
            Name = Prose.Text("goldenCookies", "payday", "name"),
            Icon = Prose.Text("goldenCookies", "payday", "icon"),
            Description = Prose.Text("goldenCookies", "payday", "description"),
            Weight = 10,
            CookiesFromCpsSeconds = 600,
        },
        new()
        {
            Id = "ransomware",
            Name = Prose.Text("goldenCookies", "ransomware", "name"),
            Icon = Prose.Text("goldenCookies", "ransomware", "icon"),
            Description = Prose.Text("goldenCookies", "ransomware", "description"),
            Weight = 11,
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
            Id = "viral",
            Name = Prose.Text("goldenCookies", "viral", "name"),
            Icon = Prose.Text("goldenCookies", "viral", "icon"),
            Description = Prose.Text("goldenCookies", "viral", "description"),
            Weight = 5,
            BuffId = "viral",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "root_access",
            Name = Prose.Text("goldenCookies", "root_access", "name"),
            Icon = Prose.Text("goldenCookies", "root_access", "icon"),
            Description = Prose.Text("goldenCookies", "root_access", "description"),
            Weight = 6,
            BuffId = "root_access",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "second_wave",
            Name = Prose.Text("goldenCookies", "second_wave", "name"),
            Icon = Prose.Text("goldenCookies", "second_wave", "icon"),
            Description = Prose.Text("goldenCookies", "second_wave", "description"),
            Weight = 2,
            BuffId = "mining_malware",
            BuffSeconds = 77,
            SecondaryBuffId = "data_flood",
            SecondaryBuffSeconds = 13,
            IsRare = true,
        },
        new()
        {
            Id = "ops_outage",
            Name = Prose.Text("goldenCookies", "ops_outage", "name"),
            Icon = Prose.Text("goldenCookies", "ops_outage", "icon"),
            Description = Prose.Text("goldenCookies", "ops_outage", "description"),
            Weight = 16,
            BuffId = "ops_outage",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "racking",
            Name = Prose.Text("goldenCookies", "racking", "name"),
            Icon = Prose.Text("goldenCookies", "racking", "icon"),
            Description = Prose.Text("goldenCookies", "racking", "description"),
            Weight = 7,
            BuffId = "racking",
            BuffSeconds = 30,
        },
    ];
}
