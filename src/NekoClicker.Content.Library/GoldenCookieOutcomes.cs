using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 「蠹虫」结果表（10 条）。<para>
/// 换皮自金猫：从书页里钻出来的虫子，带来的不全是坏事——畅销、加印都很香，
/// 但纸荒、抄本、查禁也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
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
            Id = "unsold_stock",
            Name = "库房里还有一箱",
            Icon = "📦",
            Description = "最里面那排架子上翻出一整箱没拆过的初版，纸边还是白的。获得 {amount} 页。",
            Weight = 48,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "swarm",
            Name = "蠹虫成群",
            Icon = "🐛",
            Description = "它们从没人翻的那一头开始啃，啃到一半被光吸引过来了。蠹虫成群 {duration}。",
            Weight = 32,
            BuffId = "bookworm_swarm",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "deadline",
            Name = "交稿日就在今晚",
            Icon = "🌙",
            Description = "她看了一眼钟，把茶凉在桌上，重新摊开纸。通宵赶稿 {duration}。",
            Weight = 10,
            BuffId = "overnight_draft",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "stolen_pages",
            Name = "被人撕走了几页",
            Icon = "✂️",
            Description = "归还的时候书是热的，但中间缺了三页，撕口很整齐。损失 {amount} 页。",
            Weight = 6,
            StealBankFraction = 0.05,
        },
        new()
        {
            Id = "nothing",
            Name = "什么也没有",
            Icon = "🕳️",
            Description = "书页之间夹着一张借阅卡，上面只有日期，没有名字。",
            Weight = 3,
        },
        new()
        {
            Id = "bestseller",
            Name = "畅销",
            Icon = "📈",
            Description = "加印第三次的时候，印坊的师傅说这辈子没见过这样的。畅销 {duration}。",
            Weight = 5,
            BuffId = "bestseller",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "proofread",
            Name = "校稿",
            Icon = "🔍",
            Description = "她自己从头读了一遍，在页边改掉十七个错字，然后坐在那儿把那三页又读了一次。校稿 {duration}。",
            Weight = 6,
            BuffId = "proofread",
            BuffSeconds = 20,
        },
        new()
        {
            Id = "second_worm",
            Name = "又来一只",
            Icon = "👀",
            Description = "两只蠹虫在书脊上并排待着，一大一小，都朝她看。",
            Weight = 2,
            BuffId = "bookworm_swarm",
            BuffSeconds = 77,
            SecondaryBuffId = "overnight_draft",
            SecondaryBuffSeconds = 13,
        },
        new()
        {
            Id = "paper_shortage",
            Name = "纸荒",
            Icon = "📉",
            Description = "印坊的仓库空了，订单排到了明年。纸荒 {duration}。",
            Weight = 22,
            BuffId = "paper_shortage",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "night_reading_room",
            Name = "闭馆之后",
            Icon = "🪑",
            Description = "灯还亮着，靠窗那张桌子边坐着一个人，面前的茶已经凉了。深夜阅览室 {duration}。",
            Weight = 6,
            BuffId = "night_reading_room",
            BuffSeconds = 30,
        },
    ];
}
