using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 「梦魇」结果表（10 条）。<para>
/// 换皮自金猫：从梦的褶皱里翻上来的东西，带来的不全是坏事——清明梦、梦中梦很香，
/// 但鬼压床、坠落、梦魇潮也很真实。权重总和 140，银行抽成 0.15 / 封顶 900 秒产量，
/// 所以随机事件的期望值与其它包可比。
/// </para>
/// <para>
/// <b>负面结果总权重 37 / 140（约 26%）</b>，比图书馆（28 / 140，20%）高一点——
/// 这正是第 4 层「噩梦层」在文案里承诺过的事，而且第 3 层的金猫频率被调高了，
/// 所以它在真实游玩里的体感比这个比例更重。构建期只校验权重为正，
/// "负面偏多"这条是内容自觉，由 <c>DreamContentTests</c> 的端点用例守着。
/// </para>
/// </summary>
internal static class GoldenCookieOutcomes
{
    /// <summary>全部结果。</summary>
    public static GoldenCookieOutcome[] All =>
    [
        new()
        {
            Id = "warm_hollow",
            Name = "枕头下的温热",
            Icon = "🛏️",
            Description = "她把手伸到枕头底下，摸到一块被自己焐热的地方。梦就是从那儿开始的。获得 {amount} 点梦。",
            Weight = 46,
            CookiesFromBankFraction = 0.15,
            CookiesFromBankFractionCapSecondsOfCps = 900,
            CookiesFromCpsSeconds = 13,
        },
        new()
        {
            Id = "nested",
            Name = "梦中梦",
            Icon = "🌀",
            Description = "她在梦里睡着了，然后又在那一层里睡着。醒的时候要往上数四层。梦中梦 {duration}。",
            Weight = 30,
            BuffId = "controlled_dream",
            BuffSeconds = 60,
        },
        new()
        {
            Id = "lucid",
            Name = "忽然清明",
            Icon = "💡",
            Description = "她停下来，抬手看了看自己的手指，然后说：这是我的梦。清明梦 {duration}。",
            Weight = 9,
            BuffId = "lucid_dream",
            BuffSeconds = 77,
        },
        new()
        {
            Id = "paralysis",
            Name = "鬼压床",
            Icon = "🪨",
            Description = "她醒了，但只有眼睛醒了。胸口上坐着的东西不动，也不说话。鬼压床 {duration}。",
            Weight = 18,
            BuffId = "sleep_paralysis",
            BuffSeconds = 66,
        },
        new()
        {
            Id = "name_called",
            Name = "有人喊她的名字",
            Icon = "📣",
            Description = "声音是从上面来的，隔着好几层梦，闷闷的，但她听清了。那声音喊的是她真正的名字。",
            Weight = 7,
            CookiesFromCpsSeconds = 30,
        },
        new()
        {
            Id = "replay",
            Name = "这一段做过",
            Icon = "🔁",
            Description = "她认得这面墙、这句台词、这个转身的弧度。上一次也是在这儿醒的。",
            Weight = 5,
            CookiesFromCpsSeconds = 60,
            IsRare = true,
        },
        new()
        {
            Id = "alarm_inside",
            Name = "闹钟在梦里响",
            Icon = "⏰",
            Description = "声音从梦里面传出来。她翻了半天才找到那个闹钟——它就摆在梦的床头。入梦 {duration}。",
            Weight = 6,
            BuffId = "dream_leap",
            BuffSeconds = 13,
        },
        new()
        {
            Id = "becoming",
            Name = "变成梦魇",
            Icon = "🕷️",
            Description = "她低头看见自己的手不对。有那么一小会儿，她不介意——那一小会儿里她什么都不怕。梦魇潮 {duration}。",
            Weight = 7,
            BuffId = "nightmare_tide",
            BuffSeconds = 72,
        },
        new()
        {
            Id = "falling_dream",
            Name = "楼在梦里塌了",
            Icon = "🕳️",
            Description = "她站的那一层忽然没了。往下掉的时候她数着楼层，数到第四层就不敢数了。坠落 {duration}。",
            Weight = 8,
            BuffId = "falling",
            BuffSeconds = 90,
        },
        new()
        {
            Id = "dragged_deeper",
            Name = "被拽进更深处",
            Icon = "🪝",
            Description = "有东西拉着她的脚踝往上——不对，是往下。她攥住了一层梦的边缘，还是被带走了。",
            Weight = 4,
            StealBankFraction = 0.05,
        },
    ];
}
