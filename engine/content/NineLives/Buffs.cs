using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>限时增益。九命版的"情绪波动"。</summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "purr_frenzy",
            Name = "呼噜狂暴",
            Icon = "💤",
            Description = "所有建筑产量 ×7。整栋楼在共振。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "headpat_frenzy",
            Name = "摸头停不下来",
            Icon = "🖐️",
            Description = "点击收益 ×777。手已经不是你的了。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "god_gaze",
            Name = "猫神注视",
            Icon = "👁️",
            Description = "所有建筑产量 ×15。寐娅看了一眼这边。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "memory_flash",
            Name = "记忆闪回",
            Icon = "⚡",
            Description = "「记忆金库」产量 ×40。她想起来了一段不属于自己的童年。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("memory_vault", 40)],
        },
        new()
        {
            Id = "nine_resonance",
            Name = "九命共鸣",
            Icon = "⛓️",
            Description = "所有建筑产量 ×66。九条命同时醒着。",
            Duration = 12,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(66)],
            IsDebuff = false,
        },
        new()
        {
            Id = "void_creep",
            Name = "虚无侵蚀",
            Icon = "🕳️",
            Description = "所有建筑产量 ×0.5。有人忘了读她那一段。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "dream_dive",
            Name = "沉入梦层",
            Icon = "🌀",
            Description = "点击收益 ×50，所有建筑产量 ×3。",
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50), Modifier.GlobalMultiplier(3)],
        },
        new()
        {
            Id = "deleted",
            Name = "被删除",
            Icon = "⌫",
            Description = "所有建筑产量 ×0.25。她少了一页。",
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
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "lucky",
            Name = "抹不掉的记忆",
            Icon = "🍀",
            Description = "获得 {amount} 条小鱼干。她记得你，所以你还在。",
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "frenzy",
            Name = "呼噜狂暴",
            Icon = "💤",
            Description = "产量 ×7，持续 {duration}。",
            Weight = 30,
            BuffId = "purr_frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = "摸头停不下来",
            Icon = "🖐️",
            Description = "点击收益 ×777，持续 {duration}。",
            Weight = 8,
            BuffId = "headpat_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "ruin",
            Name = "虚无侵蚀",
            Icon = "🕳️",
            Description = "损失 {amount} 条小鱼干...有人忘了读她那一段。",
            Weight = 4,
            StealBankFraction = 0.05,
            IsRare = true,
        },
        new()
        {
            Id = "blab",
            Name = "未命名",
            Icon = "💬",
            Description = "她对你说了一个音节，然后忘了自己本来想说什么。",
            Weight = 2,
        },
        new()
        {
            Id = "memory",
            Name = "记忆闪回",
            Icon = "⚡",
            Description = "「记忆金库」产量 ×40，持续 {duration}。",
            Weight = 3,
            BuffId = "memory_flash",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = "猫神注视",
            Icon = "👁️",
            Description = "产量 ×15，持续 {duration}。",
            Weight = 3,
            BuffId = "god_gaze",
            BuffSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "chain",
            Name = "九命共鸣",
            Icon = "⛓️",
            Description = "产量 ×7 且点击 ×777，持续 {duration}。",
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
            Name = "被删除",
            Icon = "⌫",
            Description = "产量 ×0.25，持续 {duration}。",
            Weight = 2,
            BuffId = "deleted",
            BuffSeconds = 40,
            IsRare = true,
        },
        new()
        {
            Id = "dream",
            Name = "沉入梦层",
            Icon = "🌀",
            Description = "点击 ×50 且产量 ×3，持续 {duration}。",
            Weight = 2,
            BuffId = "dream_dive",
            BuffSeconds = 45,
            IsRare = true,
        },
    ];
}
