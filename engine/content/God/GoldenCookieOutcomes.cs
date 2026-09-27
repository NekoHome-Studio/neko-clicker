using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 「神迹」结果表（10 条）。<para>
/// 换皮自金猫：这个包里金猫是"神迹"——天上掉下来的一次事件。
/// 权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，所以随机事件的期望值与其它包可比；
/// 「幸运 + 狂热」合计超过 70%，负面结果只占约 4%，稀有结果负责制造"截图发群"的时刻。
/// </para>
/// <para>
/// 这个包的负面结果有一半是<b>现代型</b>的：供品荒、异端审判、玩梗潮。
/// 信仰这件事在 meta 语气里最好笑的地方，就是它撞上流量之后的形状。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "forgotten_offering",
            Name = "有人偷偷补了供品",
            Icon = "🍥",
            Description = "祭坛上多了一盘还热着的鱼，旁边没有脚印。她没有去查是谁放的。获得 {amount} 香火。",
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "divine_frenzy",
            Name = "神恩降临",
            Icon = "✨",
            Description = "云裂开一条缝，光正好打在她的神龛上，围观的人开始拍照。神恩降临 {duration}。",
            Weight = 32,
            BuffId = "divine_frenzy",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "manifest_frenzy",
            Name = "显灵停不下来",
            Icon = "🙌",
            Description = "三十七个愿望一口气满足完，她喘了口气说「下一个」。显灵停不下来 {duration}。",
            Weight = 10,
            BuffId = "manifest_frenzy",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "snatched_offering",
            Name = "供品被顺走了",
            Icon = "🥷",
            Description = "案板是空的，供盘是热的，猫是装睡的。损失 {amount} 香火。",
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "nothing",
            Name = "什么也没发生",
            Icon = "🕳️",
            Description = "她在神龛前坐了一整个下午，只等到一只路过的猫，那只猫看了她一眼就走了。",
            Weight = 3,
        },
        new()
        {
            Id = "pilgrim_flood",
            Name = "朝圣潮",
            Icon = "🚶",
            Description = "路上全是人，队伍从山脚排到门口。有人问她能不能插队，她说「你问后面的人」。朝圣潮 {duration}。",
            Weight = 5,
            BuffId = "pilgrim_flood",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "sutra_reading",
            Name = "诵经",
            Icon = "📿",
            Description = "整座神殿同时开口，声音居然是对齐的，她愣了两秒然后跟着念。诵经 {duration}。",
            Weight = 6,
            BuffId = "sutra_reading",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "double_miracle",
            Name = "两套神话同时显灵",
            Icon = "🔀",
            Description = "两边的祭司都说是自己请来的，她两边都点了点头——这种场面，点头最省事。",
            Weight = 2,
            BuffId = "divine_frenzy",
            BuffSeconds = 77,
            SecondaryBuffId = "manifest_frenzy",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "offering_shortage",
            Name = "供品荒",
            Icon = "📉",
            Description = "今年收成不好，塑料水果摆了整整一季。供品荒 {duration}。",
            Weight = 22,
            BuffId = "offering_shortage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "prime_time",
            Name = "黄金档",
            Icon = "📹",
            Description = "平台把她排进了首页推荐位，补光灯全开。她对着镜头说了句「大家好」，弹幕炸了。黄金档 {duration}。",
            Weight = 6,
            BuffId = "prime_time",
            BuffSeconds = 30,
        },
    ];
}
