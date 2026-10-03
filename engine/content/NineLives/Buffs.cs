using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>限时增益。九命版的"情绪波动"。</summary>
internal static class Buffs
{
    /// <summary>
    /// 本包的 <c>text.json</c>：增益 / 升级 / 金猫结果的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "purr_frenzy",
            Name = Prose.Text("buffs", "purr_frenzy", "name"),
            Icon = Prose.Text("buffs", "purr_frenzy", "icon"),
            Description = Prose.Text("buffs", "purr_frenzy", "description"),
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "headpat_frenzy",
            Name = Prose.Text("buffs", "headpat_frenzy", "name"),
            Icon = Prose.Text("buffs", "headpat_frenzy", "icon"),
            Description = Prose.Text("buffs", "headpat_frenzy", "description"),
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "god_gaze",
            Name = Prose.Text("buffs", "god_gaze", "name"),
            Icon = Prose.Text("buffs", "god_gaze", "icon"),
            Description = Prose.Text("buffs", "god_gaze", "description"),
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "memory_flash",
            Name = Prose.Text("buffs", "memory_flash", "name"),
            Icon = Prose.Text("buffs", "memory_flash", "icon"),
            Description = Prose.Text("buffs", "memory_flash", "description"),
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("memory_vault", 40)],
        },
        new()
        {
            Id = "nine_resonance",
            Name = Prose.Text("buffs", "nine_resonance", "name"),
            Icon = Prose.Text("buffs", "nine_resonance", "icon"),
            Description = Prose.Text("buffs", "nine_resonance", "description"),
            Duration = 12,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(66)],
            IsDebuff = false,
        },
        new()
        {
            Id = "void_creep",
            Name = Prose.Text("buffs", "void_creep", "name"),
            Icon = Prose.Text("buffs", "void_creep", "icon"),
            Description = Prose.Text("buffs", "void_creep", "description"),
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "dream_dive",
            Name = Prose.Text("buffs", "dream_dive", "name"),
            Icon = Prose.Text("buffs", "dream_dive", "icon"),
            Description = Prose.Text("buffs", "dream_dive", "description"),
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50), Modifier.GlobalMultiplier(3)],
        },
        new()
        {
            Id = "deleted",
            Name = Prose.Text("buffs", "deleted", "name"),
            Icon = Prose.Text("buffs", "deleted", "icon"),
            Description = Prose.Text("buffs", "deleted", "description"),
            Duration = 40,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.25)],
            IsDebuff = true,
        },
    ];
}

/// <summary>
/// 随机事件表：「游荡的情感残响」——一只还没被任何人记起的猫娘虚影。<para>
/// 权重与咖啡馆包同构：非负面占九成以上，负面只扣 5% 且不会扣成负数。
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
            BuffId = "purr_frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = Prose.Text("goldenCookies", "click_frenzy", "name"),
            Icon = Prose.Text("goldenCookies", "click_frenzy", "icon"),
            Description = Prose.Text("goldenCookies", "click_frenzy", "description"),
            Weight = 8,
            BuffId = "headpat_frenzy",
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
            IsRare = true,
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
            Id = "memory",
            Name = Prose.Text("goldenCookies", "memory", "name"),
            Icon = Prose.Text("goldenCookies", "memory", "icon"),
            Description = Prose.Text("goldenCookies", "memory", "description"),
            Weight = 3,
            BuffId = "memory_flash",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = Prose.Text("goldenCookies", "bloodlust", "name"),
            Icon = Prose.Text("goldenCookies", "bloodlust", "icon"),
            Description = Prose.Text("goldenCookies", "bloodlust", "description"),
            Weight = 3,
            BuffId = "god_gaze",
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
            BuffId = "purr_frenzy",
            BuffSeconds = 30,
            SecondaryBuffId = "headpat_frenzy",
            SecondaryBuffSeconds = 10,
            IsRare = true,
        },
        new()
        {
            Id = "deletion",
            Name = Prose.Text("goldenCookies", "deletion", "name"),
            Icon = Prose.Text("goldenCookies", "deletion", "icon"),
            Description = Prose.Text("goldenCookies", "deletion", "description"),
            Weight = 2,
            BuffId = "deleted",
            BuffSeconds = 40,
            IsRare = true,
        },
        new()
        {
            Id = "dream",
            Name = Prose.Text("goldenCookies", "dream", "name"),
            Icon = Prose.Text("goldenCookies", "dream", "icon"),
            Description = Prose.Text("goldenCookies", "dream", "description"),
            Weight = 2,
            BuffId = "dream_dive",
            BuffSeconds = 45,
            IsRare = true,
        },
    ];
}
