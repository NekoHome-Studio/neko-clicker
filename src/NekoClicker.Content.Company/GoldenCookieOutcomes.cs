using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 「甲方改需求」结果表（10 条）。<para>
/// 换皮自金猫：跳出来的是甲方，而甲方带来的不全是坏事——融资、出圈都很香，
/// 但半夜改需求、宕机、挖角也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
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
            Id = "scope_creep",
            Name = "需求蔓延",
            Icon = "📩",
            Description = "甲方把「顺便加一下」说了四次。团队边骂边做，居然真的做完了。获得 {amount} 营收。",
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "midnight_change",
            Name = "半夜改需求",
            Icon = "🌙",
            Description = "凌晨两点，消息弹出来：「方向要调整一下。」全员进入救火状态。需求变更 {duration}。",
            Weight = 32,
            BuffId = "requirement_change",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "overtime_sprint",
            Name = "通宵冲刺",
            Icon = "☕",
            Description = "咖啡机第三次见底，她把外套盖在腿上继续改。通宵冲刺 {duration}。",
            Weight = 10,
            BuffId = "all_nighter",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "client_vanished",
            Name = "甲方失联",
            Icon = "📵",
            Description = "对接人换了三任，最后一任的签名档写着「已离职」。预付款要不回来了。损失 {amount} 营收。",
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "empty_meeting",
            Name = "会议白开",
            Icon = "🕳️",
            Description = "两个小时的会，结论是「下次再对齐一下」。什么都没发生，也什么都没多。",
            Weight = 3,
        },
        new()
        {
            Id = "funding_round",
            Name = "融资到账",
            Icon = "💸",
            Description = "投资人回了邮件，只有一个词：「打款。」融资到账 {duration}。",
            Weight = 5,
            BuffId = "funding_round",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "viral_post",
            Name = "意外出圈",
            Icon = "📣",
            Description = "她随手发的一条动态上了热门，评论区一半在问产品，一半在问猫。出圈 {duration}。",
            Weight = 6,
            BuffId = "viral_post",
            BuffSeconds = 45,
        },
        new()
        {
            Id = "double_change",
            Name = "连环变更",
            Icon = "🌀",
            Description = "需求改到第三版的时候，甲方说：「还是第一版好。」所有人都笑了，笑完继续改。",
            Weight = 2,
            BuffId = "requirement_change",
            BuffSeconds = 77,
            SecondaryBuffId = "all_nighter",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "server_outage",
            Name = "线上事故",
            Icon = "🔥",
            Description = "监控全红的那三十秒，你听见自己的心跳。服务器宕机 {duration}。",
            Weight = 22,
            BuffId = "server_outage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "team_offsite",
            Name = "团建",
            Icon = "🏕️",
            Description = "两天一夜，山里没有信号。回来之后，工位上的灯亮得更久了。团建 {duration}。",
            Weight = 6,
            BuffId = "team_offsite",
            BuffSeconds = 30,
        },
    ];
}
