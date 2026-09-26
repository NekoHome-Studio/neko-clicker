using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几层梦</b>走：每往下睡一层，故事往前推一段；四条线各自在很小的动作上开篇，
/// 免得图鉴一开就是一整墙 ???。
/// </para>
/// <para>
/// <b>四条纪律</b>（与其余包相同，每一条都在这个包里踩过一次）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同一个条件必然在同一瞬间一起解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增。这个包的多指标混排最容易在这里翻车：
///     "本轮累计"在往下睡一层时会归零，所以纪元门槛与层内里程碑必须<b>按层分块</b>，
///     否则会出现"第 3 条还锁着，第 4 条已经亮了"。</item>
///   <item>挂了 <c>EraAtLeast(n)</c> 的条目，层内门槛必须 &lt; 第 n 层的完成门槛
///     （<c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 通用守卫）。</item>
///   <item>转生类条目只能放线的尾部——这个包<b>没有</b>转生类条目：往下睡一层的时机由玩家定。</item>
/// </list>
/// </para>
/// <para>
/// <b>「开局十分钟 ≤ 3 条」是怎么做到的</b>：四条线里只有三条早开，
/// 而且它们的开篇动作各不相同（点击 1 / 25 / 100）；第四条（梦层）晚一步，
/// 挂在"点到 80 下之后"上——它是这一包里唯一的"进得去才发现"的那条线。
/// 其余条目的门槛一律压在 <b>2e7 以上</b>，而开局十分钟的包络只有 2.7e7，
/// 所以它们在 G5 窗口里根本够不着。这条数值边界由
/// <c>DreamContentTests.G5_FirstTenMinutesRevealAtMostThreeEntries</c> 守着。
/// </para>
/// </summary>
internal static class Lore
{
    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        new()
        {
            Id = "her",
            Name = "她",
            Theme = "做梦的人：一层一层往下走，和她在每一层里留下的东西。",
            Icon = "🛏️",
            TotalEntries = 12,
        },
        new()
        {
            Id = "layers",
            Name = "梦层",
            Theme = "梦的地形：脖子上的灰、会慢半拍的镜子、四十七遍都走不完的走廊。",
            Icon = "🌙",
            TotalEntries = 10,
        },
        new()
        {
            Id = "mare",
            Name = "梦魇",
            Theme = "梦的褶皱：没做完的坏事挤在一起，把那一块梦压得很低。",
            Icon = "🕷️",
            TotalEntries = 10,
        },
        new()
        {
            Id = "waking",
            Name = "清醒",
            Theme = "梦外面：那个还在睡的人、凉的枕头，和「再睡五分钟」。",
            Icon = "🌅",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Her(),
        .. Layers(),
        .. Mare(),
        .. Waking(),
    ];

    /// <summary>「她」线：开篇是"点一下"，此后按层逐段推进。</summary>
    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = "第一层：枕头是温的",
            StorylineId = "her",
            Order = 1,
            Icon = "🛏️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她侧过身，把脸埋进枕头，数到第七只猫的时候，楼下的街换成了她认得的字。"
                 + "她没觉得奇怪——在梦里，认识的字本来就是自己写的。"
                 + "她只是有点困，困得连「我在做梦」这句话都懒得想。",
        },
        new()
        {
            Id = "her_02",
            Title = "她第一次往下走",
            StorylineId = "her",
            Order = 2,
            Icon = "⤵️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.75e7),
            Body = "这一层梦太薄了，薄得能看见床单的花纹。她试着往下一踩——"
                 + "地面没有拦她，只是软了一下，像踩进一块没干透的泥。"
                 + "「哦，」她说，「原来还能往下。」",
        },
        new()
        {
            Id = "her_03",
            Title = "第二层比第一层安静",
            StorylineId = "her",
            Order = 3,
            Icon = "😴",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.6e8)),
            Body = "往下走的时候没有脚步声。她停下来听了一会儿，发现这里连自己的心跳都是"
                 + "隔着一层东西传过来的，闷闷的。她开始明白：越深的地方，越舍不得放人走。",
        },
        new()
        {
            Id = "her_04",
            Title = "手指的边缘是糊的",
            StorylineId = "her",
            Order = 4,
            Icon = "🖐️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.5e9)),
            Body = "她抬起手，一根一根看过去：五根，都对。但边缘是糊的，"
                 + "像墨还没干。她把手放下，又抬起来——这次糊的地方换了一边。"
                 + "她想，这大概就是「快要看清了」的样子。",
        },
        new()
        {
            Id = "her_05",
            Title = "清明的那一刻",
            StorylineId = "her",
            Order = 5,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e10)),
            Body = "她在梦里停住，抬手看了看自己的手指——五根，但边缘有点糊。"
                 + "然后她说：「这是我的梦。」整层梦安静了一秒，接着开始按她说的长。"
                 + "她笑了一下，笑得有点不像她自己。",
        },
        new()
        {
            Id = "her_06",
            Title = "她开始数层数",
            StorylineId = "her",
            Order = 6,
            Icon = "🔢",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.8e11)),
            Body = "这一层待久了她开始数：浅眠、深眠、清明、噩梦、梦核。"
                 + "数完之后她愣了一会儿——她怎么知道一共有五层？"
                 + "她从来没数过上面那几层。",
        },
        new()
        {
            Id = "her_07",
            Title = "她见过这一层",
            StorylineId = "her",
            Order = 7,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.5e11)),
            Body = "噩梦层她来过。同样的墙、同样的呼吸节奏、同样在第 47 扇门后面停了一下。"
                 + "上一次也是在这儿醒的。她把手按在墙上，墙面是热的——"
                 + "她确定自己没有走过这里。",
        },
        new()
        {
            Id = "her_08",
            Title = "她试着捏一只猫",
            StorylineId = "her",
            Order = 8,
            Icon = "🐈",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.5e11)),
            Body = "梦里能造东西这件事，是她在第四层才敢信的。她捏了一只猫，捏出来是热的，"
                 + "还会打呼，呼噜声和她自己的一样。她抱着那只猫坐了很久，"
                 + "然后把它放回了梦里——带不上去。",
        },
        new()
        {
            Id = "her_09",
            Title = "梦核转了一下",
            StorylineId = "her",
            Order = 9,
            Icon = "🔮",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.8e11)),
            Body = "最里面那颗东西转得很慢。她把手放上去的时候，它停了一下，"
                 + "像是在认人；然后转得快了，和她心跳一样的节奏。"
                 + "从那一刻起，整座梦里的东西都开始听她的。",
        },
        new()
        {
            Id = "her_10",
            Title = "她给自己搭了一层",
            StorylineId = "her",
            Order = 10,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.5e11)),
            Body = "最里面那层是她自己搭的：一张床、一盏灯、一个枕头，和上面那层一模一样。"
                 + "她说这样比较安心。她没说安心的是哪一头的事。",
        },
        new()
        {
            Id = "her_11",
            Title = "她数到第五层就不再数了",
            StorylineId = "her",
            Order = 11,
            Icon = "🌀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.5e11)),
            Body = "再往上还是往下，她已经分不清了。她试过一次：一直往下走，走了很久，"
                 + "最后走进了一间和第一层一模一样的房间，枕头是凉的。"
                 + "她坐在床边，把这件事想成了「我大概是绕回来了」。",
        },
        new()
        {
            Id = "her_12",
            Title = "最后一晚",
            StorylineId = "her",
            Order = 12,
            Icon = "🌄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.8e11)),
            Body = "她把五层梦从头到尾走了一遍，每一层都回了一次头。"
                 + "走到最上面那层的时候，枕头还是温的——"
                 + "说明外面那个人的头，一直没离开过。她站了一会儿，然后睁开眼。"
                 + "天刚亮，窗外有人在收摊。",
        },
    ];

    /// <summary>「梦层」线：四条线里唯一晚开的一条——它讲的是梦的地形本身。</summary>
    private static IEnumerable<LoreEntry> Layers() =>
    [
        new()
        {
            Id = "lay_01",
            Title = "什么是梦层",
            StorylineId = "layers",
            Order = 1,
            Icon = "🌙",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.9e7),
            Body = "梦不是一整块。它一层一层地套着，像有人把好几个晚上叠在一起"
                 + "压进了同一个小时里。最外面的那层最薄，薄到你一动就醒；"
                 + "最里面的那层最厚，厚到能装下整座楼。",
        },
        new()
        {
            Id = "lay_02",
            Title = "梦镜",
            StorylineId = "layers",
            Order = 2,
            Icon = "🪞",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.45e8)),
            Body = "镜子里的她比她慢半拍。她抬手，镜子里的人还没来得及抬，然后两个人都笑了。"
                 + "她试了很多次，慢的永远是镜子里那个。"
                 + "后来她想明白：慢的那边才是梦，快的这边是她自己。",
        },
        new()
        {
            Id = "lay_03",
            Title = "脖子上的那点灰",
            StorylineId = "layers",
            Order = 3,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.5e8)),
            Body = "从第三层开始，空气里有灰一样的细颗粒。她伸手抹过自己的脖子，指腹上是白的。"
                 + "她闻了一下——是洗衣粉的味道，是上面那层世界的东西。"
                 + "梦层会漏，只是漏得很慢。",
        },
        new()
        {
            Id = "lay_04",
            Title = "两边的门是同一扇",
            StorylineId = "layers",
            Order = 4,
            Icon = "🚪",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.4e9)),
            Body = "这一层的走廊两边都是门。她推开左边那扇，看见的是右边的房间；"
                 + "退出来再推右边那扇，看见的是左边的。"
                 + "她试了十几次，最后坐在走廊中间，决定不再推了。",
        },
        new()
        {
            Id = "lay_05",
            Title = "清醒区的规矩",
            StorylineId = "layers",
            Order = 5,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(8.5e9)),
            Body = "清醒区只有一条规矩：知道自己在做梦的人，得为梦里的东西负责。"
                 + "她把楼折起来过，把海倒过来流过，也把一个人的脸改过——"
                 + "改完之后那张脸就一直跟着她，跟了整整一层梦。",
        },
        new()
        {
            Id = "lay_06",
            Title = "织梦者",
            StorylineId = "layers",
            Order = 6,
            Icon = "🧶",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.6e10)),
            Body = "织梦者不是一个人，是一间作坊。它把上一晚剩下的线头接起来，"
                 + "接得好的话，今晚的梦会接着说昨天那一句。她第一次进去的时候，"
                 + "听见里面有个人在说：这一根接不上，换一段吧。",
        },
        new()
        {
            Id = "lay_07",
            Title = "四十七遍的走廊",
            StorylineId = "layers",
            Order = 7,
            Icon = "🔁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.6e10)),
            Body = "失眠走廊两边全是门，每一扇后面都是同一个房间，房间里还是那条走廊。"
                 + "她走到第 47 扇才承认自己在绕圈，然后她数了一遍："
                 + "从第一扇走到第 47 扇，一共花了一分半。",
        },
        new()
        {
            Id = "lay_08",
            Title = "越深的梦越浓",
            StorylineId = "layers",
            Order = 8,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e11)),
            Body = "越深的梦越浓。浓到这个程度的时候，梦里的东西开始有重量："
                 + "她拿起来一个杯子，杯底有水的重量。「这不对，」她说，"
                 + "「梦里的东西不该有重量。」可她还是把杯子放下了，很轻地放。",
        },
        new()
        {
            Id = "lay_09",
            Title = "嵌套塔的第九层",
            StorylineId = "layers",
            Order = 9,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.7e11)),
            Body = "塔顶有一扇门，门后是同一座塔。她数到第九层就不数了——"
                 + "反正每一层都在往上。她只记住了一件事：第九层的窗户外面，"
                 + "云是往上的。",
        },
        new()
        {
            Id = "lay_10",
            Title = "一整块梦",
            StorylineId = "layers",
            Order = 10,
            Icon = "🧩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.8e11)),
            Body = "从外面看，五层梦是一整块东西：最外面薄得透亮，最里面厚得发黑。"
                 + "她绕着它走了一圈，走完之后她确认了一件事——"
                 + "这块东西的形状，和那个还在睡的人的侧脸是一样的。",
        },
    ];

    /// <summary>「梦魇」线：开篇是"点 25 下"。</summary>
    private static IEnumerable<LoreEntry> Mare() =>
    [
        new()
        {
            Id = "mar_01",
            Title = "第一只梦魇",
            StorylineId = "mare",
            Order = 1,
            Icon = "👁️",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = "它不是从门里进来的，而是从梦的褶皱里翻上来的。它没有形状，"
                 + "只有一块比周围暗一点的地方。她盯着那块地方看了一会儿，"
                 + "然后伸手碰了一下——是凉的。",
        },
        new()
        {
            Id = "mar_02",
            Title = "鬼压床",
            StorylineId = "mare",
            Order = 2,
            Icon = "🪨",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(2.95e7),
            Body = "她醒了，但只有眼睛醒了。胸口上有东西坐着，它不动，也不说话。"
                 + "她试着动手指，手指说它也想动。这样过了很久——久到她开始"
                 + "跟那个东西讲道理：你让我起来，我请你喝水。",
        },
        new()
        {
            Id = "mar_03",
            Title = "数到十只",
            StorylineId = "mare",
            Order = 3,
            Icon = "🕷️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.7e8)),
            Body = "第十只来的时候她已经不数了。它们不追她，只是挤在一起，"
                 + "把那一块梦境压得很低。低到从那个位置看，整层梦是斜的。",
        },
        new()
        {
            Id = "mar_04",
            Title = "变轻的梦魇",
            StorylineId = "mare",
            Order = 4,
            Icon = "🪶",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.3e9)),
            Body = "梦魇变轻了。它们不再挤在一起，而是像纸一样贴着墙。"
                 + "她撕下来一张，上面什么都没有。她想，这大概就是"
                 + "「没做完的坏事」在被扔掉之后的样子。",
        },
        new()
        {
            Id = "mar_05",
            Title = "变成梦魇的那一次",
            StorylineId = "mare",
            Order = 5,
            Icon = "🖤",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7.5e9)),
            Body = "她低头看见自己的手不对。有那么一小会儿，她不介意——"
                 + "那一小会儿里她什么都不怕，连醒着这件事都不怕。"
                 + "清醒过来之后她坐在原地，很久不敢看自己的手。",
        },
        new()
        {
            Id = "mar_06",
            Title = "它们认得她",
            StorylineId = "mare",
            Order = 6,
            Icon = "🫥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(7.8e10)),
            Body = "第五十只之后她发现它们会绕开她。不是怕，是认得——"
                 + "像狗认得每天路过的人那样，抬一下头，然后继续趴着。"
                 + "她开始跟它们说话，虽然它们从来不答。",
        },
        new()
        {
            Id = "mar_07",
            Title = "梦魇的形状是她的形状",
            StorylineId = "mare",
            Order = 7,
            Icon = "🪞",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.15e11)),
            Body = "这一层里有一只特别大的。它不动，只是躺在那儿，"
                 + "轮廓和她躺着的时候一模一样。她绕着它走了一圈，"
                 + "然后很小声地说：「你也是我。」它没有回答。",
        },
        new()
        {
            Id = "mar_08",
            Title = "它是她没做完的事",
            StorylineId = "mare",
            Order = 8,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.2e11)),
            Body = "她终于弄明白了：噩梦巢里堆的全是从上面掉下来的东西，"
                 + "而每一件都是她自己掉下来的。没回的短信、没接的电话、"
                 + "没说出口的那一句。它们在下面等她，等了很多层。",
        },
        new()
        {
            Id = "mar_09",
            Title = "她和梦魇谈了一次",
            StorylineId = "mare",
            Order = 9,
            Icon = "🤝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.9e11)),
            Body = "她坐下来跟那只最大的谈了一次。她说了很久，"
                 + "对方一句话也没说；说完之后，那一块暗的地方淡了一点。"
                 + "她站起来的时候膝盖是麻的——在梦里坐久了也会麻。",
        },
        new()
        {
            Id = "mar_10",
            Title = "梦魇在替她按着梦",
            StorylineId = "mare",
            Order = 10,
            Icon = "⚓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.4e11)),
            Body = "最后一层里，梦魇不再翻上来了，它们只是按着梦的四个角，"
                 + "不让它散。她看着它们干活，忽然明白："
                 + "这些东西留下来的原因，和她留下来的原因，是同一个。",
        },
    ];

    /// <summary>「清醒」线：开篇是"点 100 下"。</summary>
    private static IEnumerable<LoreEntry> Waking() =>
    [
        new()
        {
            Id = "wak_01",
            Title = "上面有人在睡",
            StorylineId = "waking",
            Order = 1,
            Icon = "😴",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "她第一次意识到这件事，是在第二层：这座梦的外面，"
                 + "是另一个人在睡。所有的梦层都是那个人做的，"
                 + "而她只是那个人梦里的一个动作。",
        },
        new()
        {
            Id = "wak_02",
            Title = "闹钟在梦里响",
            StorylineId = "waking",
            Order = 2,
            Icon = "⏰",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedThisRunAtLeast(3.2e7),
            Body = "闹钟的声音是从梦里面传出来的。她翻了半天，最后在梦的床头找到了它——"
                 + "它就摆在那儿，一直在响，响了很多年。她伸手按掉了它，"
                 + "然后梦继续。",
        },
        new()
        {
            Id = "wak_03",
            Title = "有人喊她的名字",
            StorylineId = "waking",
            Order = 3,
            Icon = "📣",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.9e8)),
            Body = "声音是从上面来的，隔着好几层梦，闷闷的。但它喊的是她真正的名字——"
                 + "不是梦里那个，是外面那个。她抬起头，只看见一层一层的天花板，"
                 + "一直叠到看不见的地方。",
        },
        new()
        {
            Id = "wak_04",
            Title = "枕头是凉的",
            StorylineId = "waking",
            Order = 4,
            Icon = "🛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.2e9)),
            Body = "这一层里有一张床，铺得整整齐齐，枕头是凉的。"
                 + "她摸了一下就缩回手——原来「没睡过」是这种感觉。"
                 + "她站在床边想了很久：这个位置，本来该是她躺的。",
        },
        new()
        {
            Id = "wak_05",
            Title = "外面天快亮了",
            StorylineId = "waking",
            Order = 5,
            Icon = "🌅",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.5e9)),
            Body = "梦的最上层开始泛灰。她说不上来那是什么颜色，"
                 + "只知道那是光从眼皮外面透进来的样子。"
                 + "她在那层光底下站了一会儿，觉得有点刺眼，就往下走了。",
        },
        new()
        {
            Id = "wak_06",
            Title = "她在写一张字条",
            StorylineId = "waking",
            Order = 6,
            Icon = "📝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(5.5e10)),
            Body = "醒来的人会给睡着的人留字条。她开始写，写完发现字是反的——"
                 + "因为在这边看是正的。她把字条压在枕头底下，"
                 + "压在那一块被焐热的地方。",
        },
        new()
        {
            Id = "wak_07",
            Title = "她已经攒够了",
            StorylineId = "waking",
            Order = 7,
            Icon = "🌬️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.1e11)),
            Body = "往上走要力气，而她攒了很久。攒下来的东西看不见，"
                 + "只有走到最上层的时候会显出来：脚下的地面开始变硬，"
                 + "呼吸开始有声音，手指开始有一点点冷。",
        },
        new()
        {
            Id = "wak_08",
            Title = "醒，或者再睡五分钟",
            StorylineId = "waking",
            Order = 8,
            Icon = "🌄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e11)),
            Body = "最上层只有一件事要做：睁开眼，或者翻个身。"
                 + "她试过两个都选：先睁开眼，看见天刚亮、窗外有人在收摊，"
                 + "然后翻了个身，说「再睡五分钟」。这句话她说了很多次，"
                 + "只有最后一次是真的。",
        },
    ];
}
