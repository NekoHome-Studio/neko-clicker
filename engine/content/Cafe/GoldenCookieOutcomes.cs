using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的金猫结果表（8 条）——「走错门的客人」。<para>
/// 权重设计：非负面权重 89 / 93 ≈ 95.7%，负面只有 4.3%，
/// 且负面只扣 5% 存量、不会扣成负数（引擎保证）。治愈系包的底线是"随机性只制造惊喜，不制造挫折"。
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
            BuffId = "caffeine_overload",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = Prose.Text("goldenCookies", "click_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "click_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "click_frenzy", "description"),
            Weight = 8,
            BuffId = "cat_chorus",
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
            Id = "building_special",
            Name = Prose.Text("goldenCookies", "building_special", "name"),
            Icon = Prose.Text("goldenCookies", "building_special", "icon"),
            Description = Prose.Text("goldenCookies", "building_special", "description"),
            Weight = 3,
            BuffId = "new_beans",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = Prose.Text("goldenCookies", "bloodlust", "name"),
            Icon = Prose.Text("goldenCookies", "bloodlust", "icon"),
            Description = Prose.Text("goldenCookies", "bloodlust", "description"),
            Weight = 3,
            BuffId = "boss_treats",
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
            BuffId = "caffeine_overload",
            BuffSeconds = 30,
            SecondaryBuffId = "cat_chorus",
            SecondaryBuffSeconds = 10,
            IsRare = true,
        },
    ];
}
