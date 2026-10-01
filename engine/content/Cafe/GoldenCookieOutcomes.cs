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
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "lucky",
            Name = "熟客",
            Icon = "🪙",
            Description = "一位熟客一次付清了整年的账。获得 {amount} 条小鱼干。",
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "frenzy",
            Name = "团体客",
            Icon = "⚡",
            Description = "一整个社团推门进来。咖啡因过载 {duration}。",
            Weight = 30,
            BuffId = "caffeine_overload",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "click_frenzy",
            Name = "网红打卡",
            Icon = "🎶",
            Description = "有人直播了猫娘合唱，弹幕全在问「店在哪」。点击狂热 {duration}。",
            Weight = 8,
            BuffId = "cat_chorus",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "ruin",
            Name = "打翻咖啡",
            Icon = "💥",
            Description = "有人把整壶咖啡打翻在账单上。损失 {amount} 条小鱼干。",
            Weight = 4,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "blab",
            Name = "迷路的猫",
            Icon = "🐈",
            Description = "一只猫从门外走进来，绕了一圈，又从同一个门出去了。它没留下任何东西，除了一个念头。",
            Weight = 2,
        },
        new()
        {
            Id = "building_special",
            Name = "新豆到货",
            Icon = "🫘",
            Description = "新豆子到了，烘焙间整晚没关灯。新豆上市 {duration}。",
            Weight = 3,
            BuffId = "new_beans",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "bloodlust",
            Name = "猫神路过",
            Icon = "🐾",
            Description = "门口的风停了一秒，所有猫同时抬头。老板请客 {duration}。",
            Weight = 3,
            BuffId = "boss_treats",
            BuffSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "chain",
            Name = "两界信使",
            Icon = "💌",
            Description = "铃铛响了两次。有人从门那边递来一封信，信里只有两种味道：{duration}。",
            Weight = 1,
            BuffId = "caffeine_overload",
            BuffSeconds = 30,
            SecondaryBuffId = "cat_chorus",
            SecondaryBuffSeconds = 10,
            IsRare = true,
        },
    ];
}
