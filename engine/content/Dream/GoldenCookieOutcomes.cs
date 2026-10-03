using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 「梦魇」结果表（10 条）。<para>
/// 换皮自金猫：从梦的褶皱里翻上来的东西，带来的不全是坏事——清明梦、梦中梦很香，
/// 但鬼压床、坠落、梦魇潮也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比。
/// </para>
/// <para>
/// <b>负面结果总权重 37 / 140（约 26%）</b>，比图书馆（28 / 140，20%）高一点——
/// 这正是第 4 层「噩梦层」在文案里承诺过的事，而且第 3 层的金猫频率被调高了，
/// 所以它在真实游玩里的体感比这个比例更重。构建期只校验权重为正，
/// "负面偏多"这条是内容自觉，由 <c>DreamContentTests</c> 的端点用例守着。
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
            Id = "warm_hollow",
            Name = Prose.Text("goldenCookies", "warm_hollow", "name"),
            Icon = Prose.Text("goldenCookies", "warm_hollow", "icon"),
            Description = Prose.Text("goldenCookies", "warm_hollow", "description"),
            Weight = 46,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "nested",
            Name = Prose.Text("goldenCookies", "nested", "name"),
            Icon = Prose.Text("goldenCookies", "nested", "icon"),
            Description = Prose.Text("goldenCookies", "nested", "description"),
            Weight = 30,
            BuffId = "controlled_dream",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "lucid",
            Name = Prose.Text("goldenCookies", "lucid", "name"),
            Icon = Prose.Text("goldenCookies", "lucid", "icon"),
            Description = Prose.Text("goldenCookies", "lucid", "description"),
            Weight = 9,
            BuffId = "lucid_dream",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "paralysis",
            Name = Prose.Text("goldenCookies", "paralysis", "name"),
            Icon = Prose.Text("goldenCookies", "paralysis", "icon"),
            Description = Prose.Text("goldenCookies", "paralysis", "description"),
            Weight = 18,
            BuffId = "sleep_paralysis",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "name_called",
            Name = Prose.Text("goldenCookies", "name_called", "name"),
            Icon = Prose.Text("goldenCookies", "name_called", "icon"),
            Description = Prose.Text("goldenCookies", "name_called", "description"),
            Weight = 7,
            CookiesFromCpsSeconds = 30,
        },
        new()
        {
            Id = "replay",
            Name = Prose.Text("goldenCookies", "replay", "name"),
            Icon = Prose.Text("goldenCookies", "replay", "icon"),
            Description = Prose.Text("goldenCookies", "replay", "description"),
            Weight = 5,
            CookiesFromCpsSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "alarm_inside",
            Name = Prose.Text("goldenCookies", "alarm_inside", "name"),
            Icon = Prose.Text("goldenCookies", "alarm_inside", "icon"),
            Description = Prose.Text("goldenCookies", "alarm_inside", "description"),
            Weight = 6,
            BuffId = "dream_leap",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "becoming",
            Name = Prose.Text("goldenCookies", "becoming", "name"),
            Icon = Prose.Text("goldenCookies", "becoming", "icon"),
            Description = Prose.Text("goldenCookies", "becoming", "description"),
            Weight = 7,
            BuffId = "nightmare_tide",
            BuffSeconds = 72,
        },
        new()
        {
            Id = "falling_dream",
            Name = Prose.Text("goldenCookies", "falling_dream", "name"),
            Icon = Prose.Text("goldenCookies", "falling_dream", "icon"),
            Description = Prose.Text("goldenCookies", "falling_dream", "description"),
            Weight = 8,
            BuffId = "falling",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "dragged_deeper",
            Name = Prose.Text("goldenCookies", "dragged_deeper", "name"),
            Icon = Prose.Text("goldenCookies", "dragged_deeper", "icon"),
            Description = Prose.Text("goldenCookies", "dragged_deeper", "description"),
            Weight = 4,
            StealBankFraction = 0.05,
        },
    ];
}
