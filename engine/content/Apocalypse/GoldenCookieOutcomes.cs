using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 「变异体」结果表（10 条）。<para>
/// 换皮自金猫：从废墟里钻出来的东西，带来的不全是坏事——变异潮与战前缓存都很香，
/// 但断电、辐射、疫病也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "buried_warehouse",
            Name = "挖到仓库",
            Icon = "📦",
            Description = "凿开一堵墙，后面是整排货架，箱子上的日期是灾前。获得 {amount} 物资。",
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "mutation",
            Name = "变异",
            Icon = "🧬",
            Description = "它比昨天大了一圈，而且不躲人了。变异潮 {duration}。",
            Weight = 32,
            BuffId = "mutation_wave",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "frenzy",
            Name = "停不下来",
            Icon = "⛏️",
            Description = "她从天亮挖到天黑，中间只喝过一次水。翻找狂热 {duration}。",
            Weight = 10,
            BuffId = "scavenge_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "raid",
            Name = "被人趁夜搬空",
            Icon = "🥷",
            Description = "早上起床，西边的仓房空了一半。地上留着两行不属于这里的鞋印。损失 {amount} 物资。",
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "nothing",
            Name = "什么也没有",
            Icon = "🕳️",
            Description = "挖开之后是一个空房间。角落里有一把椅子，是面朝门摆着的。",
            Weight = 3,
        },
        new()
        {
            Id = "cache",
            Name = "战前缓存",
            Icon = "🗝️",
            Description = "钥匙就插在锁上，好像主人只是出门一趟。战前缓存 {duration}。",
            Weight = 5,
            BuffId = "prewar_cache",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "salvage",
            Name = "拆解有序",
            Icon = "🔧",
            Description = "她第一次把一台机器完整地拆成了能用的零件，一颗螺丝都没剩。拆解有序 {duration}。",
            Weight = 6,
            BuffId = "salvage_order",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "double_mutation",
            Name = "两只",
            Icon = "👥",
            Description = "洞里有两只，一大一小。小的那只学着大的样子朝她龇牙。",
            Weight = 2,
            BuffId = "mutation_wave",
            BuffSeconds = 77,
            SecondaryBuffId = "scavenge_frenzy",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "blackout",
            Name = "全城断电",
            Icon = "🌑",
            Description = "所有灯同时灭掉的那一瞬间，整座废墟安静得像一张纸。全城断电 {duration}。",
            Weight = 22,
            BuffId = "blackout",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "rain_night",
            Name = "雨夜",
            Icon = "🌧️",
            Description = "雨下了一整夜，没人出去。锅里炖着东西，几个人靠在墙上睡着了。避难所之夜 {duration}。",
            Weight = 6,
            BuffId = "shelter_night",
            BuffSeconds = 30,
        },
    ];
}
