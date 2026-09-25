using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 「实验事故」结果表（10 条）。<para>
/// 换皮自金猫：出现的是<b>事故</b>而不是好运，所以结果分布比别的包更偏——
/// 坏事与"看起来像坏事"的比例明显更高，权重最高的那条也只是"数据涌入"这种中性偏好的事。
/// </para>
/// <para>
/// 数值沿用已验证的配方（权重总和约 140，银行抽成 0.15、封顶 900 秒产量），
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
            Id = "data_surge",
            Name = "数据涌入",
            Icon = "📈",
            Description = "一次失败的反应意外产出了三个月的数据。获得 {amount} 条数据。",
            Weight = 42,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "breach",
            Name = "收容失效",
            Icon = "🚨",
            Description = "三号舱的门开着。所有人都被叫去追，而产出翻了七倍——因为没人再记录。收容失效 {duration}。",
            Weight = 30,
            BuffId = "containment_breach",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "purge",
            Name = "误触格式化",
            Icon = "🧹",
            Description = "实习生手抖了。她在数据消失前把想说的话一次说完。数据清空 {duration}。",
            Weight = 8,
            BuffId = "data_purge",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "blackout",
            Name = "全楼停电",
            Icon = "🔌",
            Description = "保险丝烧了。你摸黑走到观察室，发现玻璃后面的她也在看你。损失 {amount} 条数据。",
            Weight = 5,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "glance",
            Name = "她看了你一眼",
            Icon = "👁️",
            Description = "什么都没发生。记录表上什么都没多，什么都没少——只有你记得那一秒。",
            Weight = 2,
        },
        new()
        {
            Id = "gene_expression",
            Name = "表达率异常",
            Icon = "🧬",
            Description = "这一批的表达率好得不像话。基因批次 {duration}。",
            Weight = 3,
            BuffId = "gene_batch",
            BuffSeconds = 30,
        },
        new()
        {
            Id = "budget",
            Name = "预算批下来了",
            Icon = "💰",
            Description = "你要 100 万，他们给了 1000 万。你猜他们没细看你的方案。拨款通过 {duration}。",
            Weight = 3,
            BuffId = "grant_approved",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "cascade",
            Name = "连锁事故",
            Icon = "💥",
            Description = "一台机器倒了，砸到第二台，第二台砸到第三台。之后整层楼的效率反而高了。",
            Weight = 1,
            BuffId = "containment_breach",
            BuffSeconds = 77,
            SecondaryBuffId = "data_purge",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "hearing",
            Name = "被叫去听证",
            Icon = "⚖️",
            Description = "委员会终于想起来有你这个人。听证会 {duration}。",
            Weight = 6,
            BuffId = "ethics_hearing",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "insight",
            Name = "两份档案对上了",
            Icon = "💡",
            Description = "第三批的日志和第二十批的日志，中间缺的那一页有了轮廓。归档灵感 {duration}。",
            Weight = 2,
            BuffId = "archive_insight",
            BuffSeconds = 45,
        },
    ];
}
