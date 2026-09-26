using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几个时代</b>走：每跨一个时代，故事往前推一段；四条线各自在很小的动作上开篇
/// （拍 1 / 25 / 100 / 600 次），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出四条"（阶段 2.6 的实测教训：G5 只允许 ≤3 条）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余七个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会看到倒挂。
///     这里用的是"纪元门槛 + 层内里程碑"，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前跨时代，这条永远读不到。
///     本包第 2~5 层的完成门槛是 1.5e7 / 3e8 / 6e9 / 1e11，所以层内里程碑最远只用到 2e9。</item>
/// </list>
/// </para>
/// <para>
/// <b>「文化」线是这个包唯一一条门槛指标跨时代不清零的线</b>（文化单调不减、且
/// <c>OnAscend</c> 有意不清零），所以它可以在同一个门槛上横跨很多层——
/// 而它读起来也正好是这么回事：<b>她记得住的东西，比任何一个时代都活得久。</b>
/// </para>
/// </summary>
internal static class Lore
{
    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        new()
        {
            Id = "cat",
            Name = "她",
            Theme = "起点：一只猫娘，和一堆她搬得动的石头。",
            Icon = "🐾",
            TotalEntries = 12,
        },
        new()
        {
            Id = "settle",
            Name = "家园",
            Theme = "聚落：从踩出来的那条路，到能看见星星的地方。",
            Icon = "🏘️",
            TotalEntries = 10,
        },
        new()
        {
            Id = "age",
            Name = "时代",
            Theme = "更替：上一个时代留下的东西，下一个时代还认不认得。",
            Icon = "⏳",
            TotalEntries = 6,
        },
        new()
        {
            Id = "memory",
            Name = "记忆",
            Theme = "文化：她把事情记下来之后，事情才开始算数。",
            Icon = "📜",
            TotalEntries = 12,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Cat(),
        .. Settle(),
        .. Age(),
        .. Memory(),
    ];

    private static IEnumerable<LoreEntry> Cat() =>
    [
        new()
        {
            Id = "cat_01",
            Title = "第一个坑",
            StorylineId = "cat",
            Order = 1,
            Icon = "🪨",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她醒来的时候在下雨。她没有先弄明白自己是谁，也没有先找吃的——"
                 + "她蹲下来，把身边的石头一块一块往自己面前推，围成一个刚好装得下她的坑。"
                 + "推到一半爪子就开始疼了，她换了个姿势继续推。"
                 + "那天晚上她躺在里面，听着雨打在石头上，第一次觉得「我」这个词有点用。",
        },
        new()
        {
            Id = "cat_02",
            Title = "第二次生火",
            StorylineId = "cat",
            Order = 2,
            Icon = "🔥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(60),
                UnlockCondition.EarnedThisRunAtLeast(1.2e4)),
            Body = "第一次生火是撞上的：两块石头磕在一起，火星掉进干草里。"
                 + "第二次她试了很久都没成功，蹲在那儿盯着那堆草看，看到天黑。"
                 + "她后来明白了——第一次是运气，第二次才是本事。"
                 + "「运气不写下来，就没有第二次。」",
        },
        new()
        {
            Id = "cat_03",
            Title = "她数到了十",
            StorylineId = "cat",
            Order = 3,
            Icon = "🖐️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2.5e5)),
            Body = "她发现自己一只爪子上的指头数得完，两只就数不完。"
                 + "于是她在墙上按了一下，又按了一下——十下之后划一道竖线。"
                 + "这是这个文明的第一件抽象工具：一个不指任何东西、只表示「多少」的符号。"
                 + "她在墙前面站了很久，觉得这件事比火还厉害。",
        },
        new()
        {
            Id = "cat_04",
            Title = "她第一次说谎",
            StorylineId = "cat",
            Order = 4,
            Icon = "🤥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(3.2e6)),
            Body = "仓里还剩多少她心里很清楚，但她说了「够」。"
                 + "说完她就后悔了——不是因为骗人，而是因为她发现这句话必须靠别人相信才成立。"
                 + "「原来我说的话，一半在别人那里。」"
                 + "从那天起她开始在墙上记数，不再靠嘴。",
        },
        new()
        {
            Id = "cat_05",
            Title = "第一道墙上的刻痕",
            StorylineId = "cat",
            Order = 5,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.5e6)),
            Body = "墙砌到一人高的时候她停了手，在最下面那块石头上刻了一道痕——"
                 + "和第一个时代围坑时随手划的那道一样。"
                 + "工匠问她这是什么意思，她说：「这是开工的日子。」"
                 + "她没说实话。那道痕的意思是「这里以前什么都没有」。",
        },
        new()
        {
            Id = "cat_06",
            Title = "她学会了让别人去做",
            StorylineId = "cat",
            Order = 6,
            Icon = "🤝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5e7)),
            Body = "集市开起来之后，她第一次一整天空着手。"
                 + "她本来以为自己会难受，结果只是站在边上看着，看别人搬她搬过的东西、"
                 + "砌她砌过的墙。晚上她跟自己说：「这不是我不干，是我干不完。」"
                 + "这句话她后来又对自己说了很多次，每次都比上一次更容易说出口。",
        },
        new()
        {
            Id = "cat_07",
            Title = "她写下第一个「为什么」",
            StorylineId = "cat",
            Order = 7,
            Icon = "🏛️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(9e7)),
            Body = "学院的第一块木板上，她本来只想写「怎么砌墙」。"
                 + "笔落下去的时候她自己改了：「为什么要砌墙。」"
                 + "写完她退后两步看那行字，发现它比一整面墙难回答得多——"
                 + "而且它不会塌。",
        },
        new()
        {
            Id = "cat_08",
            Title = "她第一次算错",
            StorylineId = "cat",
            Order = 8,
            Icon = "📐",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.1e8)),
            Body = "神殿的柱子立起来之后有点歪。她重算了一遍，又重算了一遍，"
                 + "发现错的不是工人，是她三年前在墙上划的那道竖线——她当年数错了十。"
                 + "她当着所有人的面把错的那面墙砸了，"
                 + "然后说了一句后来被写进课本的话：「我错的时候，你们要能算出来。」",
        },
        new()
        {
            Id = "cat_09",
            Title = "她把第一句话写下来",
            StorylineId = "cat",
            Order = 9,
            Icon = "🖌️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.ClicksAtLeast(120)),
            Body = "她在自己那间屋子里的墙上写下第一句话，不是给别人的："
                 + "「我在下雨的那天醒过来，用石头围了一个坑。」"
                 + "写完她发现这句话没有用——它不教任何人做任何事。"
                 + "但她把它留在了最显眼的地方，因为总得有一句话是只为了「发生过」而存在的。",
        },
        new()
        {
            Id = "cat_10",
            Title = "她抬头看了很久",
            StorylineId = "cat",
            Order = 10,
            Icon = "🌌",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e8)),
            Body = "星港的第一根柱子插进云里那天，全城的人都来了，只有她站得最远。"
                 + "她仰着头，脖子酸了也没低下来。"
                 + "有人问她是不是在想第一艘船什么时候能走，她说不是——"
                 + "她在想第一个时代那个坑：「原来从那儿到这里，是要走这么久的。」",
        },
        new()
        {
            Id = "cat_11",
            Title = "她数了数",
            StorylineId = "cat",
            Order = 11,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9e8)),
            Body = "有一天她一个人爬到城墙上坐着，把历代的记录摊在膝盖上数。"
                 + "她数到第七个时代的名字时停住了——那些名字有一半她不记得长什么样的人。"
                 + "她把册子合上，抱在怀里坐了很久。"
                 + "「原来我记的不是他们，」她想，「我记的是他们做过的那些事。」",
        },
        new()
        {
            Id = "cat_12",
            Title = "最后一次跨时代",
            StorylineId = "cat",
            Order = 12,
            Icon = "🌠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.6e10)),
            Body = "跨进最后一个时代的那天，全城没有一个人知道上一段是怎么结束的。"
                 + "只有她手上多了一点「火种」——那东西不占地方，也不能吃，"
                 + "但她每次跨时代都会把它先拿出来看一眼，确认它还在。"
                 + "「你们可以不知道，」她对自己说，「但我不行。」",
        },
    ];

    private static IEnumerable<LoreEntry> Settle() =>
    [
        new()
        {
            Id = "settle_01",
            Title = "踩出来的路",
            StorylineId = "settle",
            Order = 1,
            Icon = "🛤️",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = "第二个猫窝是紧挨着第一个搭的，第三个隔了一点，第四个隔得更远。"
                 + "它们之间没有商量过位置，但地上慢慢出现了一条浅色的带子——"
                 + "草被踩平了。她看着那条带子想：原来「公共的东西」是这样长出来的，"
                 + "不是谁决定要修，是所有人都在走同一条。",
        },
        new()
        {
            Id = "settle_02",
            Title = "第一件多余的东西",
            StorylineId = "settle",
            Order = 2,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.4e5)),
            Body = "有个陶罐被做得比装水需要的大了一圈，罐口还刻了一圈波纹。"
                 + "她问做它的人为什么要刻，那人说「好看」。"
                 + "她把这两个字咀嚼了一晚上——这是这个文明第一次为「没有用」的东西花力气，"
                 + "而她决定不管这件事。",
        },
        new()
        {
            Id = "settle_03",
            Title = "第一笔买卖",
            StorylineId = "settle",
            Order = 3,
            Icon = "🏪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.6e6)),
            Body = "她拿一筐干草换了三块燧石，双方都觉得赚了。"
                 + "回去的路上她突然停下——如果两个人都觉得赚了，那赚的是什么？"
                 + "她想了很久，结论是：「是我没有的东西，和他没有的东西。」"
                 + "这个文明最值钱的一课是从一个集市上学会的。",
        },
        new()
        {
            Id = "settle_04",
            Title = "轮流守夜",
            StorylineId = "settle",
            Order = 4,
            Icon = "🌙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9.5e6)),
            Body = "第一个守夜表是用刻痕记的：谁守过，就在木头上划一道。"
                 + "有人偷偷替别人多守了一夜，被发现了也不认。"
                 + "她后来把那张木头收了起来，说这是墙的第一块砖——"
                 + "「墙挡的是外面的东西，这张表挡的是里面的。」",
        },
        new()
        {
            Id = "settle_05",
            Title = "城墙上的门",
            StorylineId = "settle",
            Order = 5,
            Icon = "🚪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7.2e7)),
            Body = "墙修好之后有人提议把门改小一点，只留一个人侧身过去的宽度。"
                 + "她否决了，理由很实在：「门小了我们自己进出也慢。」"
                 + "后来有一次外面来了一群饿着的猫，那道门没有关上。"
                 + "她站在门内侧数着进来的人，数到最后自己也说不清这是好事还是坏事。",
        },
        new()
        {
            Id = "settle_06",
            Title = "广场上的那块石头",
            StorylineId = "settle",
            Order = 6,
            Icon = "🗿",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.5e6)),
            Body = "学院前面立了一块石头，上面什么字都没有。"
                 + "工匠问要不要刻点什么，她说：「不用，这块是留给以后刻的。」"
                 + "石头在那儿立了很多年，一直是空的——直到有一年有人在上面刻了"
                 + "第一行公式，然后是第二行，第三行。"
                 + "她每次路过都会停一下，看看今天又多了什么。",
        },
        new()
        {
            Id = "settle_07",
            Title = "学院的第一堂课",
            StorylineId = "settle",
            Order = 7,
            Icon = "🏛️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.2e7)),
            Body = "第一堂课只有一个学生，坐在最前排，紧张得尾巴一直在动。"
                 + "她在木板上写下那句话：「你不必相信我，你可以自己算一遍。」"
                 + "写完她转过身，看见那个学生在很认真地抄。"
                 + "「别抄这句，」她说，「这句你得自己想一遍。」",
        },
        new()
        {
            Id = "settle_08",
            Title = "第一个不信的人",
            StorylineId = "settle",
            Order = 8,
            Icon = "❓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.BuildingsAtLeast("academy", 12)),
            Body = "有个学生把她的历法从头到尾算了一遍，然后站起来说她三年前那道竖线错了。"
                 + "全场安静。她想了一会儿，说：「你说得对。」"
                 + "那天晚上她一个人把旧历法全烧了，烧的时候在想："
                 + "「终于有人敢说这句话了——这是我今天最高兴的一件事。」",
        },
        new()
        {
            Id = "settle_09",
            Title = "停不下来的星港",
            StorylineId = "settle",
            Order = 9,
            Icon = "🚀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.75e9)),
            Body = "星港造到一半的时候工程停了，理由是「没有那么多东西可以运」。"
                 + "她把账本翻了一遍，然后下了个所有人都没料到的命令："
                 + "「继续造。造好了先不运货，先运人。」"
                 + "有人问运谁，她说：「运想去看一眼的人。」",
        },
        new()
        {
            Id = "settle_10",
            Title = "空着的那块地",
            StorylineId = "settle",
            Order = 10,
            Icon = "🪹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.8e9)),
            Body = "第一艘船飞走之后，发射台下留了一块烧黑的水泥地。"
                 + "有人提议把那儿改成广场，她不同意，也说不清为什么。"
                 + "后来那块地就一直空着。她偶尔会过去站一会儿，"
                 + "脚下的地面和第一个时代那个坑，是同一个温度。",
        },
    ];

    private static IEnumerable<LoreEntry> Age() =>
    [
        new()
        {
            Id = "age_01",
            Title = "第一个时代怎么结束的",
            StorylineId = "age",
            Order = 1,
            Icon = "⏳",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(600),
            Body = "没有谁宣布它结束。只是有一天她发现，自己已经很久没有亲自搬过一块石头了；"
                 + "而石堆那个坑早就被新的东西围在中间，谁也看不见。"
                 + "她在坑边上坐了一晚上，第二天早上站起来的时候，知道那个时代已经过去了——"
                 + "「结束」不是一个时刻，是一个你事后才认出来的东西。",
        },
        new()
        {
            Id = "age_02",
            Title = "第二个时代的第一天",
            StorylineId = "age",
            Order = 2,
            Icon = "🏘️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(7.5e5)),
            Body = "跨进第二个时代的那天早上，她做的第一件事是去看第一个坑。"
                 + "它还在，只是被踩平了一半，里面长出了草。"
                 + "她本来想把它重新围起来，手伸出去又收回来了。"
                 + "「留着吧，」她说，「总得有个地方让人看出来我们是从哪儿开始的。」",
        },
        new()
        {
            Id = "age_03",
            Title = "第一个记得上一个时代的人",
            StorylineId = "age",
            Order = 3,
            Icon = "👵",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.15e7)),
            Body = "第三个时代开始的时候，城里已经有人是「生在墙里面」的了。"
                 + "有个老太太还能讲第一个时代的事，讲的时候围了一圈小孩。"
                 + "她站在外面听了一会儿，发现老太太讲的版本和她记忆里的不一样——"
                 + "但她没有纠正。「原来我记的那个也只是其中一个版本。」",
        },
        new()
        {
            Id = "age_04",
            Title = "历法",
            StorylineId = "age",
            Order = 4,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.6e8)),
            Body = "学院做的第一件「没有用」的事是编历法：把每一个时代的开头、每一场洪水、"
                 + "每一次丰收都定在一个日子上。"
                 + "有人问这有什么用，她说：「这样以后的人吵架的时候，至少吵的是同一天。」"
                 + "历法印出来的那天，这个文明第一次有了「以前」——不是传说，是可以查的以前。",
        },
        new()
        {
            Id = "age_05",
            Title = "博物馆",
            StorylineId = "age",
            Order = 5,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.1e9)),
            Body = "博物馆里最旧的一件展品是一块带刻痕的石头——从第一个时代的坑里挖出来的。"
                 + "展签是她自己写的，只有一行：「不知道是谁划的，也不知道为什么。」"
                 + "有参观者问为什么把不知道的东西放在正中间，"
                 + "她说：「因为剩下所有东西，都是从这一道痕开始的。」",
        },
        new()
        {
            Id = "age_06",
            Title = "轮回来了",
            StorylineId = "age",
            Order = 6,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e9)),
            Body = "有一次在很远的星球上，她看见一群刚学会用工具的东西——"
                 + "它们也在用石头围坑，围得歪歪扭扭，围到一半还会吵架。"
                 + "她站在很远的地方看了很久，没有过去。"
                 + "回去的路上她说了一句：「我们当年也是这样，而且我们也是对的。」",
        },
    ];

    private static IEnumerable<LoreEntry> Memory() =>
    [
        new()
        {
            Id = "memory_01",
            Title = "第一句话被传下去",
            StorylineId = "memory",
            Order = 1,
            Icon = "🗣️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "她把「火要留着」讲给第二只猫听，讲了很久；"
                 + "第二只猫又讲给第三只听，讲得比她还长。"
                 + "她在旁边听着，发现第三只猫记住的不是她的话，是第二只猫的话。"
                 + "「原来传下去的东西都会变一点点。」她想了想，觉得这样也对——"
                 + "不变的话，就只是她一个人在说。",
        },
        new()
        {
            Id = "memory_02",
            Title = "第一面记事的墙",
            StorylineId = "memory",
            Order = 2,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(CultureModule.CounterKey, 40)),
            Body = "村口那面墙上划满了记号：谁家添了人、哪天下了雨、哪次打猎空手回来。"
                 + "她走过去看的时候，发现有一条是别人替她记的——那天她病了。"
                 + "她站在那面墙前面看了很久。这是第一次，有东西记着她，而不是她记着东西。",
        },
        new()
        {
            Id = "memory_03",
            Title = "第一个字",
            StorylineId = "memory",
            Order = 3,
            Icon = "🔤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(CultureModule.CounterKey, 600)),
            Body = "她花了很久才画出一个不依赖上文的符号——它单独拿出来也有意思。"
                 + "她画的是「水」。画完之后她盯着它看，忽然意识到："
                 + "从这一刻起，她可以写下一个这里没有的东西了。"
                 + "「字比记性好，」她说，「因为字不会累。」",
        },
        new()
        {
            Id = "memory_04",
            Title = "她把名字写上去了",
            StorylineId = "memory",
            Order = 4,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(CultureModule.CounterKey, 2_000)),
            Body = "第一本册子的末页原来只写日期，她后来加了一行：「以上由我记录。」"
                 + "加完她有点不好意思，觉得这是虚荣。"
                 + "但下一个记录的人在下面接着写了一行：「以上由我抄写。」"
                 + "再下一个写了「以上由我核对」。"
                 + "多年以后她翻到这页，发现自己那一行早就不显眼了——她很高兴。",
        },
        new()
        {
            Id = "memory_05",
            Title = "被抄错的那一页",
            StorylineId = "memory",
            Order = 5,
            Icon = "📑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(CultureModule.CounterKey, 6_000)),
            Body = "抄写的人把「七天」抄成了「十天」，全城按十天过了一个月，谁也没发现。"
                 + "发现的时候已经过了三年。她把两个版本都留下来了，"
                 + "还在中间写了一行小字：「错的这一版也是真的发生过的。」"
                 + "后来编年史里最厚的一章，讲的就是这次抄错。",
        },
        new()
        {
            Id = "memory_06",
            Title = "她自己也需要提醒",
            StorylineId = "memory",
            Order = 6,
            Icon = "🗒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(CultureModule.CounterKey, 14_000)),
            Body = "编年史编到第三个时代的时候她卡住了：有一件事她记得清清楚楚，"
                 + "但所有的记录里都没有。她在屋里走了三天，最后决定不写。"
                 + "「如果只有我记得，那它就还是我的；写下去，它才是大家的。」"
                 + "她说完这句，把那一页留白了——现在博物馆里还留着那页空白。",
        },
        new()
        {
            Id = "memory_07",
            Title = "有人替她续写",
            StorylineId = "memory",
            Order = 7,
            Icon = "🖋️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(CultureModule.CounterKey, 32_000)),
            Body = "有一年她病了整整一季。等她回来的时候，册子已经被人接着写下去了——"
                 + "笔迹换了三种，字比她的整齐。"
                 + "她翻到最后，看见新写上的一行：「本期由我们代记，她好了以后可以改。」"
                 + "她把那行字读了两遍，然后什么都没改。",
        },
        new()
        {
            Id = "memory_08",
            Title = "墙上的字比墙活得久",
            StorylineId = "memory",
            Order = 8,
            Icon = "🏚️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 80_000)),
            Body = "第二道城墙拆的时候，工人把刻了字的那几块石头单独留了出来。"
                 + "墙拆完了，那几块石头被搬进学院的院子里立着。"
                 + "她站在那儿看了很久——墙是为了挡东西才存在的，"
                 + "而这些字不是为了挡任何东西才存在的。"
                 + "「原来先没的是墙。」",
        },
        new()
        {
            Id = "memory_09",
            Title = "二十万个字",
            StorylineId = "memory",
            Order = 9,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 180_000)),
            Body = "从第一个字到第很多万字，她用了好几个时代。"
                 + "最后一次盘点的时候，档案室的人告诉她：现在的总量，"
                 + "一个人一辈子读不完。她听完沉默了一会儿，问：「那还有人在从头读吗？」"
                 + "对方说有，每年都有几个。她笑了：「那就不算白写。」",
        },
        new()
        {
            Id = "memory_10",
            Title = "她开始忘事",
            StorylineId = "memory",
            Order = 10,
            Icon = "🕰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 420_000)),
            Body = "她第一次发现自己记不起某个工匠的名字时，正在写那一年的记录。"
                 + "她停下来翻了很久，翻到了——在第三个时代的某一页上，"
                 + "字是她自己写的，写得很用力。"
                 + "她把册子合上抱在怀里，坐了一晚上。"
                 + "「原来这就是为什么要写下来。」",
        },
        new()
        {
            Id = "memory_11",
            Title = "整颗星球都记得",
            StorylineId = "memory",
            Order = 11,
            Icon = "🌍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 900_000)),
            Body = "那一年她随口说的一句话，第二天有了三个版本在流传。"
                 + "她把三个版本都抄了下来，摆在桌上比较——三个都和她说的不一样，"
                 + "三个都比她说的好听。"
                 + "「这不是记错，」她想，「这是有人替我把它说完了。」"
                 + "她把三份都收进了档案，编号挨着。",
        },
        new()
        {
            Id = "memory_12",
            Title = "跨时代的时候先拿出来的东西",
            StorylineId = "memory",
            Order = 12,
            Icon = "🌠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 3.5e6)),
            Body = "每一次跨时代，建筑会消失、钱会归零，只有两样东西跟着她走："
                 + "一点「火种」，和一整本记录。"
                 + "有一次在很长的路上她累得坐下来，把册子摊在膝盖上，"
                 + "从第一页开始读——读完抬头，发现自己已经不认识刚才走过的那片地方了。"
                 + "「没关系，」她说，「我认得这本书。」",
        },
    ];
}
