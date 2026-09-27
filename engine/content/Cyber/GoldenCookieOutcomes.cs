using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 「病毒入侵」结果表（10 条）。<para>
/// 换皮自金猫：一段不知道从哪来的代码撞进她的机群。带来的不全是坏事——
/// 挖矿木马、数据洪流、病毒式传播都很香，但勒索、掉线、被清理也很真实。
/// 权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，所以随机事件的期望值与其它包可比。
/// </para>
/// <para>
/// 三条负面结果的权重加起来 34（约 24%），比图书馆的 22 略高一点：
/// 这一层的世界更吵，玩家应该更常需要"处理"而不是"收下"。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "leftover_payload",
            Name = "上一次入侵的残留",
            Icon = "📦",
            Description = "一段没跑完的载荷躺在缓存里，还带着上一个人的签名。"
                          + "她把它捡起来，发现里面全是没花掉的算力。获得 {amount} 点产出。",
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "mining_malware",
            Name = "挖矿木马",
            Icon = "⛏️",
            Description = "它一进来就开始干活，不问工资，也不打招呼。挖矿木马 {duration}。",
            Weight = 32,
            BuffId = "mining_malware",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "payday",
            Name = "今天有人付钱",
            Icon = "💰",
            Description = "一条很短的转账记录，收款方是她，付款方是一串随机字符。"
                          + "没有备注，也没有第二笔。获得 {amount} 点产出。",
            Weight = 10,
            CookiesFromCpsSeconds = 600,
        },
        new()
        {
            Id = "ransomware",
            Name = "勒索软件",
            Icon = "🔒",
            Description = "「你的数据在我这里。」字是红的，倒计时是红的，只有她的名字是白的。"
                          + "损失 {amount} 点产出。",
            Weight = 11,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "nothing",
            Name = "什么也没有",
            Icon = "🕳️",
            Description = "日志里多了一行空行。时间戳是明天。",
            Weight = 3,
        },
        new()
        {
            Id = "viral",
            Name = "病毒式传播",
            Icon = "📈",
            Description = "她甚至没想传播，是别人替她传播的。病毒式传播 {duration}。",
            Weight = 5,
            BuffId = "viral",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "root_access",
            Name = "拿到根权限",
            Icon = "🔑",
            Description = "对方把 shell 留在那儿就走了，像是忘了关。她犹豫了两秒，然后坐下了。"
                          + "拿到根权限 {duration}。",
            Weight = 6,
            BuffId = "root_access",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "second_wave",
            Name = "它又来了",
            Icon = "👀",
            Description = "同一个来源，同一个端口，隔了三个小时。这次它带了一个同伴。",
            Weight = 2,
            BuffId = "mining_malware",
            BuffSeconds = 77,
            SecondaryBuffId = "data_flood",
            SecondaryBuffSeconds = 13,
            IsRare = true,
        },
        new()
        {
            Id = "ops_outage",
            Name = "运维拔错插头",
            Icon = "🔌",
            Description = "有人推着吸尘器进来，顺手碰掉了一整排电源。运维掉线 {duration}。",
            Weight = 16,
            BuffId = "ops_outage",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "racking",
            Name = "隔壁让了一排机柜",
            Icon = "🏢",
            Description = "隔壁那家倒闭了，运维把空出来的机柜推到她这边，"
                          + "什么也没说。整机架扩容 {duration}。",
            Weight = 7,
            BuffId = "racking",
            BuffSeconds = 30,
        },
    ];
}
