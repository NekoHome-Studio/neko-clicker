using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 「天灾」结果表（11 条）。<para>
/// 换皮自金猫：文明演进路上砸下来的那些事——不全是坏事。丰收年、技术突破、黄金时代很香，
/// 但洪水、瘟疫、长冬、陨石也很真实。权重总和 131，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比（正面结果合计约 92%，稀有约 5%，负面约 3%）。
/// </para>
/// <para>
/// 「黄金时代」是唯一的 ×30：它对应设计里那句"文明偶尔会自己往前跳一大步"，
/// 而它的权重压得很低（3）——玩家应该偶尔撞上它，而不是指望它。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "old_granary",
            Name = "老粮仓里还有半仓",
            Icon = "🌾",
            Description = "最里面那间仓的钥匙丢了很久，撬开之后发现种子都还活着。获得 {amount}。",
            Weight = 40,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "harvest",
            Name = "丰收年",
            Icon = "🌾",
            Description = "一整个季节什么都没坏。她把多出来的那部分全留成了种子。丰收年 {duration}。",
            Weight = 28,
            BuffId = "harvest_year",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "late_night",
            Name = "她一个人干到天亮",
            Icon = "🌙",
            Description = "第二天早上大家来的时候，昨天剩下的活已经没有了。她的爪子上全是新的口子。通宵 {duration}。",
            Weight = 8,
            BuffId = "all_nighter",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "invention",
            Name = "技术突破",
            Icon = "💡",
            Description = "她盯着一块烧过的石头看了三天，然后明白了：火不只是用来取暖的。技术突破 {duration}。",
            Weight = 4,
            BuffId = "breakthrough",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "flood",
            Name = "洪水",
            Icon = "🌊",
            Description = "水从低处涨上来，第一层的东西全泡了。她站在屋顶上看着，没有下水去捞。洪水 {duration}。",
            Weight = 14,
            BuffId = "flood",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "plague",
            Name = "瘟疫",
            Icon = "🦠",
            Description = "集市先静下来，然后是整条街。她把「不要聚在一起」写在了墙上——那时候字还没几个人认得。瘟疫 {duration}。",
            Weight = 12,
            BuffId = "plague",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "long_winter",
            Name = "长冬",
            Icon = "❄️",
            Description = "冬天长到所有人都开始怀疑还有没有春天。她把最后一批种子分成了三份，藏在了三个地方。长冬 {duration}。",
            Weight = 9,
            BuffId = "long_winter",
            BuffSeconds = 72,
        },
        new()
        {
            Id = "meteor",
            Name = "陨石",
            Icon = "☄️",
            Description = "天上掉下来一块，砸掉了半个广场。她是最先跑过去的人——因为那块石头能用。陨石 {duration}。",
            Weight = 7,
            BuffId = "meteor",
            BuffSeconds = 54,
        },
        new()
        {
            Id = "night_academy",
            Name = "灯一盏都没熄",
            Icon = "🕯️",
            Description = "半夜路过的猫看见学院那几扇窗全亮着，里面有人在小声争论一个她没听懂的问题。夜里的学院 {duration}。",
            Weight = 4,
            BuffId = "night_shift",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "golden_age",
            Name = "黄金时代",
            Icon = "✨",
            Description = "整整一代人没打过仗、没饿过肚子。她站在城墙上往下看，第一次觉得这些东西真的留得住。黄金时代 {duration}。",
            Weight = 3,
            BuffId = "golden_age",
            BuffSeconds = 45,
        },
        new()
        {
            Id = "two_winters",
            Name = "连着两个长冬",
            Icon = "🥶",
            Description = "第二个冬天来的时候，没人说话了。她把囤的柴分了一半给隔壁，然后自己开始烧家具。",
            Weight = 2,
            BuffId = "long_winter",
            BuffSeconds = 72,
            SecondaryBuffId = "flood",
            SecondaryBuffSeconds = 66,
        },
    ];
}
