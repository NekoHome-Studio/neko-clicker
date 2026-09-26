using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 限时增益。<para>
/// 增益是增量游戏里少见的"横向"资源：它不改变长期曲线，但改变<b>当下该做什么</b>——
/// 狂热期间应该去点金猫、点击狂热期间应该疯狂点屏幕。数值设计上刻意做成
/// "短时间超高倍率"，这样它的价值取决于玩家是否在线并作出反应。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "frenzy",
            Name = "猫薄荷狂热",
            Icon = "🌿",
            Description = "所有建筑产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "click_frenzy",
            Name = "疯狂撸猫",
            Icon = "🖐️",
            Description = "点击收益 ×777。手速就是一切。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "cat_god_descends",
            Name = "猫神降临",
            Icon = "😻",
            Description = "所有建筑产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "elder_frenzy",
            Name = "远古猫怒",
            Icon = "🙀",
            Description = "所有建筑产量 ×666。极稀有。",
            Duration = 12,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(666)],
        },
        new()
        {
            Id = "cotton_bed_frenzy",
            Name = "棉被猫狂热",
            Icon = "🛏️",
            Description = "「猫窝」产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("cat_bed", 30)],
        },
        new()
        {
            Id = "clumsy_paws",
            Name = "猫爪迟钝",
            Icon = "🐾",
            Description = "所有建筑产量 ×0.5。猫把你的键盘坐住了。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
    ];
}

/// <summary>
/// 金猫结果表。<para>
/// 权重不是随手写的：把"幸运"和"狂热"的权重设在 70% 以上，是为了保证玩家每次点金猫
/// <b>大概率有正反馈</b>；负面结果（猫粮被抢）只占约 4%，并且只扣 5% 存量、不会扣成负数。
/// 稀有结果（合计约 6%）负责制造"截图发群"的时刻。
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
            Name = "幸运",
            Icon = "🍀",
            Description = "幸运！获得 {amount} 条小鱼干。",
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "frenzy",
            Name = "猫薄荷狂热",
            Icon = "🌿",
            Description = "猫薄荷狂热！产量 ×7，持续 {duration}。",
            Weight = 30,
            BuffId = "frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = "疯狂撸猫",
            Icon = "🖐️",
            Description = "疯狂撸猫！点击收益 ×777，持续 {duration}。快去点屏幕！",
            Weight = 8,
            BuffId = "click_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "ruin",
            Name = "猫粮被抢",
            Icon = "💢",
            Description = "猫粮被抢！损失 {amount} 条小鱼干...",
            Weight = 4,
            StealBankFraction = 0.05,
            IsRare = true,
        },
        new()
        {
            Id = "blab",
            Name = "猫语呢喃",
            Icon = "💬",
            Description = "金猫对你喵了一声，什么也没有发生。",
            Weight = 2,
        },
        new()
        {
            Id = "cotton_bed",
            Name = "棉被猫狂热",
            Icon = "🛏️",
            Description = "棉被猫狂热！「猫窝」产量 ×30，持续 {duration}。",
            Weight = 3,
            BuffId = "cotton_bed_frenzy",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = "猫神降临",
            Icon = "😻",
            Description = "猫神降临！产量 ×15，持续 {duration}。",
            Weight = 3,
            BuffId = "cat_god_descends",
            BuffSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "chain",
            Name = "连锁金猫",
            Icon = "⛓️",
            Description = "连锁金猫！同时获得产量 ×7 与点击收益 ×777，持续 {duration}。",
            Weight = 1,
            BuffId = "frenzy",
            BuffSeconds = 30,
            SecondaryBuffId = "click_frenzy",
            SecondaryBuffSeconds = 10,
            IsRare = true,
        },
        new()
        {
            Id = "elder_frenzy",
            Name = "远古猫怒",
            Icon = "🙀",
            Description = "远古猫怒！！产量 ×666，持续 {duration}！",
            Weight = 1,
            BuffId = "elder_frenzy",
            BuffSeconds = 12,
            IsRare = true,
        },
        new()
        {
            Id = "clumsy",
            Name = "猫爪迟钝",
            Icon = "🐾",
            Description = "猫爪迟钝...产量 ×0.5，持续 {duration}。",
            Weight = 2,
            BuffId = "clumsy_paws",
            BuffSeconds = 66,
            IsRare = true,
        },
    ];
}
