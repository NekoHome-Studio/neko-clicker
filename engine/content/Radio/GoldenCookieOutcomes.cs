using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 「听众来电」结果表（10 条，C1《猫娘电台》）。<para>
/// 换皮自金猫：电台的热线电话，带来的不全是好事——爆款、黄金档都很香，
/// 但信号干扰、骚扰电话也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>本包的 text.json：金猫结果文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "tip_jar",
            Name = Prose.Text("goldenCookies", "tip_jar", "name"),
            Icon = Prose.Text("goldenCookies", "tip_jar", "icon"),
            Description = Prose.Text("goldenCookies", "tip_jar", "description"),
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "viral",
            Name = Prose.Text("goldenCookies", "viral", "name"),
            Icon = Prose.Text("goldenCookies", "viral", "icon"),
            Description = Prose.Text("goldenCookies", "viral", "description"),
            Weight = 32,
            BuffId = "viral_broadcast",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "request_line",
            Name = Prose.Text("goldenCookies", "request_line", "name"),
            Icon = Prose.Text("goldenCookies", "request_line", "icon"),
            Description = Prose.Text("goldenCookies", "request_line", "description"),
            Weight = 10,
            BuffId = "feedback_loop",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "prank_call",
            Name = Prose.Text("goldenCookies", "prank_call", "name"),
            Icon = Prose.Text("goldenCookies", "prank_call", "icon"),
            Description = Prose.Text("goldenCookies", "prank_call", "description"),
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "wrong_number",
            Name = Prose.Text("goldenCookies", "wrong_number", "name"),
            Icon = Prose.Text("goldenCookies", "wrong_number", "icon"),
            Description = Prose.Text("goldenCookies", "wrong_number", "description"),
            Weight = 3,
        },
        new()
        {
            Id = "prime_time",
            Name = Prose.Text("goldenCookies", "prime_time", "name"),
            Icon = Prose.Text("goldenCookies", "prime_time", "icon"),
            Description = Prose.Text("goldenCookies", "prime_time", "description"),
            Weight = 5,
            BuffId = "prime_time_slot",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "sound_check",
            Name = Prose.Text("goldenCookies", "sound_check", "name"),
            Icon = Prose.Text("goldenCookies", "sound_check", "icon"),
            Description = Prose.Text("goldenCookies", "sound_check", "description"),
            Weight = 6,
            BuffId = "sound_check",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "double_feature",
            Name = Prose.Text("goldenCookies", "double_feature", "name"),
            Icon = Prose.Text("goldenCookies", "double_feature", "icon"),
            Description = Prose.Text("goldenCookies", "double_feature", "description"),
            Weight = 2,
            BuffId = "viral_broadcast",
            BuffSeconds = 77,
            SecondaryBuffId = "feedback_loop",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "signal_jam",
            Name = Prose.Text("goldenCookies", "signal_jam", "name"),
            Icon = Prose.Text("goldenCookies", "signal_jam", "icon"),
            Description = Prose.Text("goldenCookies", "signal_jam", "description"),
            Weight = 22,
            BuffId = "signal_jam",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "night_shift",
            Name = Prose.Text("goldenCookies", "night_shift", "name"),
            Icon = Prose.Text("goldenCookies", "night_shift", "icon"),
            Description = Prose.Text("goldenCookies", "night_shift", "description"),
            Weight = 6,
            BuffId = "night_shift",
            BuffSeconds = 30,
        },
    ];
}
