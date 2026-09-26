using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几本书</b>走：每开一本新书，故事往前推一段；四条线各自在很小的动作上开篇
/// （点击 1 / 30 页 / 100 次 / 400 次），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出六条"（阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余五个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。
///     这里用的是"纪元门槛 + 层内里程碑"，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前开新书，这条永远读不到。</item>
/// </list>
/// </para>
/// <para>
/// <b>「读者」线是这个包独有的麻烦：它的门槛指标会掉。</b>被阅读度会衰减，每次开新书还会清零，
/// 所以那几条的达成时刻取决于"读者正养着的时候"，而不是"曾经达到过"。
/// 这没有违反纪律 2——它们在真实游玩里的解锁顺序仍然单调，用例是在真跑图里验的
/// （<c>LibraryContentTests.Storylines_ReadInOrderDuringARealPlaythrough</c>），
/// 而不是靠静态推理。</para>
/// </summary>
internal static class Lore
{
    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        new()
        {
            Id = "she",
            Name = "她",
            Theme = "作者：一个把世界写出来的人，和她不敢写的那一页。",
            Icon = "🖋️",
            TotalEntries = 12,
        },
        new()
        {
            Id = "shelf",
            Name = "书架",
            Theme = "图书馆：一排一排的架子，和架子上正在发生的事。",
            Icon = "📚",
            TotalEntries = 10,
        },
        new()
        {
            Id = "reader",
            Name = "读者",
            Theme = "被阅读度：有人翻开它的时候，它才存在。",
            Icon = "👤",
            TotalEntries = 10,
        },
        new()
        {
            Id = "blank",
            Name = "空白",
            Theme = "悬疑：没人读的那部分，到底去了哪里。",
            Icon = "🕳️",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. She(),
        .. Shelf(),
        .. Reader(),
        .. Blank(),
    ];

    private static IEnumerable<LoreEntry> She() =>
    [
        new()
        {
            Id = "she_01",
            Title = "第一句话",
            StorylineId = "she",
            Order = 1,
            Icon = "📄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她醒来的时候，手里握着一支笔，面前摊着一本一个字都没有的书。"
                 + "她以为自己失忆了，直到她试着写了一个字——纸把它吃了，然后长出了下一页。"
                 + "「哦，」她说，「原来是我在写。」",
        },
        new()
        {
            Id = "she_02",
            Title = "第二句话是别人的",
            StorylineId = "she",
            Order = 2,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2e5)),
            Body = "写到第三天，她发现自己写下了一句不是自己想说的话。"
                 + "那句话很顺畅，顺畅得像本来就该在那里。"
                 + "她没有划掉它，只是在旁边打了个小小的问号。",
        },
        new()
        {
            Id = "she_03",
            Title = "先画地图",
            StorylineId = "she",
            Order = 3,
            Icon = "🗺️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2e6)),
            Body = "第一个世界塌过一次，因为她在第二章改了地形，第一章的桥就悬在半空。"
                 + "所以第二个世界她先画地图，再往里放人。「写世界比写人累，」她说，"
                 + "「因为世界会在你看不见的地方互相拆台。」",
        },
        new()
        {
            Id = "she_04",
            Title = "被红笔划过的那天",
            StorylineId = "she",
            Order = 4,
            Icon = "🖍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5e6)),
            Body = "书脊上多了一道红笔。她盯着那道线看了很久，然后做了一件她后来一直没承认的事："
                 + "她把自己写的另一本书，也划了同一条线。",
        },
        new()
        {
            Id = "she_05",
            Title = "写给谁",
            StorylineId = "she",
            Order = 5,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4e7)),
            Body = "有人问她写给谁看。她说不知道，然后补了一句："
                 + "「但我知道不是写给我自己。」"
                 + "「为什么？」「因为我自己看的时候，不心疼。」",
        },
        new()
        {
            Id = "she_06",
            Title = "她也进了合订本",
            StorylineId = "she",
            Order = 6,
            Icon = "📖",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1e8)),
            Body = "装订到一半她停手了。前三本书里有一个角色，一直在做她做过的事："
                 + "醒来、握笔、写第一句话。她翻回去数了数，每一本书里都有这个人，"
                 + "而且每一次都换了名字。",
        },
        new()
        {
            Id = "she_07",
            Title = "不借出去的那本",
            StorylineId = "she",
            Order = 7,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(8e8)),
            Body = "合订本做出来之后，她把它放在书架最上层，说谁也不借。"
                 + "有读者问为什么，她说：「因为这本里有我。」"
                 + "「那更该借出去。」「那更不该。」",
        },
        new()
        {
            Id = "she_08",
            Title = "每一本都记得上一本",
            StorylineId = "she",
            Order = 8,
            Icon = "👤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(5e9)),
            Body = "有一个人从第一本读到了第四本，而且每次都能指出「这一句」和上一本哪一句是同一句。"
                 + "她后来才知道，这个人把自己的本子留在了阅览室，"
                 + "上面全是跨书的对照。她没敢借走那本本子。",
        },
        new()
        {
            Id = "she_09",
            Title = "写得很慢的最后一本",
            StorylineId = "she",
            Order = 9,
            Icon = "🔖",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2e10)),
            Body = "最后一本她写了很久。每写完一页就停下来摸一摸，像是在确认纸还在。"
                 + "她已经知道结局是什么了，只是不太想写到那里。",
        },
        new()
        {
            Id = "she_10",
            Title = "她读过自己的书吗",
            StorylineId = "she",
            Order = 10,
            Icon = "❓",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4e10)),
            Body = "有读者当面问过这个问题。她想了很久，说：「读过一本。」"
                 + "「哪一本？」「第一本。那时候我还不知道写出来会怎么样。」"
                 + "「后来呢？」「后来我学会了，写的时候读，写完就不读了。」",
        },
        new()
        {
            Id = "she_11",
            Title = "笔尖抬起来",
            StorylineId = "she",
            Order = 11,
            Icon = "✒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8e10)),
            Body = "最后一页的最后一句，她写了三遍。第一遍太满，第二遍太轻，"
                 + "第三遍只有八个字，写完她就放下了笔。"
                 + "放下之后她没有立刻合上——她坐在那儿，让最后那一页晾着。",
        },
        new()
        {
            Id = "she_12",
            Title = "合上的声音",
            StorylineId = "she",
            Order = 12,
            Icon = "📕",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.5e10)),
            Body = "书自己合上了。声音不大，但整座图书馆都安静了一下——"
                 + "像是有很多人同时抬起头。"
                 + "她把手放在封面上，没有推开，也没有再打开。",
        },
    ];

    private static IEnumerable<LoreEntry> Shelf() =>
    [
        new()
        {
            Id = "shelf_01",
            Title = "一本一个字都没有的书",
            StorylineId = "shelf",
            Order = 1,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedAllTimeAtLeast(30),
            Body = "书架上只有一本是没有书名的。她翻过很多次，每次都是空白。"
                 + "后来她明白了：那本不是没写完，是还没开始——它是一本等着被写的书，"
                 + "而且它只等一个人。",
        },
        new()
        {
            Id = "shelf_02",
            Title = "书架会自己长",
            StorylineId = "shelf",
            Order = 2,
            Icon = "🪜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5e4)),
            Body = "她量过那面墙，一开始是十一米。第二本书写到一半的时候变成了十三米，"
                 + "多出来的那两米不是她加。她没量第三次。",
        },
        new()
        {
            Id = "shelf_03",
            Title = "阅览室的绿罩灯",
            StorylineId = "shelf",
            Order = 3,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5e5)),
            Body = "那排绿罩灯是旧的，灯罩上有一圈被照黄的痕迹。"
                 + "她数过：有六盏灯的黄斑比别的深，说明那六个位置被人坐得最久。"
                 + "她把其中一把椅子的腿修好了，没告诉任何人。",
        },
        new()
        {
            Id = "shelf_04",
            Title = "复印机是个好东西",
            StorylineId = "shelf",
            Order = 4,
            Icon = "📠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e6)),
            Body = "第一台复印机搬进来那天，她按了整整一下午。"
                 + "一页变成两页，两页变成四页。"
                 + "「写一本只有一个人读，复印一本就有很多人读。」这是她第一次觉得"
                 + "自己做的事可以被别人接着做。",
        },
        new()
        {
            Id = "shelf_05",
            Title = "禁书区反而最抢手",
            StorylineId = "shelf",
            Order = 5,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1e7)),
            Body = "上锁的第一周，铁栅栏上就被人掰开了一道缝。"
                 + "她修了三次，第四次没修，只是把缝隙的位置记了下来——"
                 + "那道缝对着的正好是最常被借走的那一排。",
        },
        new()
        {
            Id = "shelf_06",
            Title = "索引塔",
            StorylineId = "shelf",
            Order = 6,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2e8)),
            Body = "没有索引的时候，一本书的平均被找到时间是四十分钟；"
                 + "有了索引塔之后是三分钟。她给这个变化做了一个牌子，"
                 + "挂在塔的入口：「找得到，才有人读。」",
        },
        new()
        {
            Id = "shelf_07",
            Title = "印坊的油墨味",
            StorylineId = "shelf",
            Order = 7,
            Icon = "🖨️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4e8)),
            Body = "第一次印出不是手抄的整本那天，油墨味顺着走廊一直飘到门口。"
                 + "有个读者站在门口闻了很久，说：「这味道像新的。」"
                 + "她纠正他：「不是像新的，是新的。」",
        },
        new()
        {
            Id = "shelf_08",
            Title = "便签墙",
            StorylineId = "shelf",
            Order = 8,
            Icon = "🧩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2e9)),
            Body = "世界观工坊的墙上贴满了便签，每一张是一条规则。"
                 + "有人问她为什么不写进书里，她说规则是给写的人看的，"
                 + "不是给读的人看的——「读的人只要觉得是真的就行。」",
        },
        new()
        {
            Id = "shelf_09",
            Title = "走到头要一天",
            StorylineId = "shelf",
            Order = 9,
            Icon = "♾️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.5e10)),
            Body = "无尽书架装好之后，有个孩子问走到头要多久。"
                 + "她算了一下，说：「一天。」孩子真的去了，第二天早上才回来，说没走到。"
                 + "她说：「对，所以不用走到头。读到哪里，哪里就是书架的尽头。」",
        },
        new()
        {
            Id = "shelf_10",
            Title = "书架最上层",
            StorylineId = "shelf",
            Order = 10,
            Icon = "⬆️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9e10)),
            Body = "五本书排在同一层。最左边那本没有书名，最右边那本是合订本。"
                 + "中间三本的书脊上，印着同一个名字——那个名字她一直没改过，"
                 + "哪怕换过五个世界。",
        },
    ];

    private static IEnumerable<LoreEntry> Reader() =>
    [
        new()
        {
            Id = "reader_01",
            Title = "第一个读者",
            StorylineId = "reader",
            Order = 1,
            Icon = "👤",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "第一个进门的人没有借书，只是在阅览室坐了一下午，把一本翻了一半。"
                 + "她站在书架后面看了很久，没敢出声。"
                 + "那人走的时候把椅子推回了原位，她为此高兴了一整天。",
        },
        new()
        {
            Id = "reader_02",
            Title = "借阅卡上的名字",
            StorylineId = "reader",
            Order = 2,
            Icon = "🗂️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 1_500)),
            Body = "借阅卡上第一次出现了同一个名字两次。"
                 + "她把那张卡抽出来，看了一会儿，又原样插了回去——"
                 + "「不能让人知道我在看这个。」",
        },
        new()
        {
            Id = "reader_03",
            Title = "读第二遍的人在读什么",
            StorylineId = "reader",
            Order = 3,
            Icon = "🔍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 5_000)),
            Body = "第二遍读的人读得比第一遍慢。她偷偷观察过：那人不看正文，"
                 + "看的是页边和注释，有时候停在某一行很久，然后笑一下。"
                 + "她回去把那一行重写了一遍。",
        },
        new()
        {
            Id = "reader_04",
            Title = "有人开始引用",
            StorylineId = "reader",
            Order = 4,
            Icon = "💬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 12_000)),
            Body = "她在别人的本子上看见了自己写过的一句话，"
                 + "括号里写着「引自」。她盯着那两个字看了一会儿，忽然明白了一件事："
                 + "那句话已经不完全属于她了。",
        },
        new()
        {
            Id = "reader_05",
            Title = "没有寄件人的信",
            StorylineId = "reader",
            Order = 5,
            Icon = "✉️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 30_000)),
            Body = "信箱里出现了一封没有寄件人的信，里面只有一行字："
                 + "「下一本什么时候？」她把这封信夹进了正在写的那一本里，"
                 + "当成书签用——她需要一个理由让这句话一直看得见。",
        },
        new()
        {
            Id = "reader_06",
            Title = "读者不知道作者在书架后面",
            StorylineId = "reader",
            Order = 6,
            Icon = "📕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 45_000)),
            Body = "有人当着她的面批评这本书：说第三章写得敷衍。"
                 + "她站在书架后面，一句话没说；那人走了之后，她回去重写了第三章。"
                 + "重写的那一版比原来长了一倍。",
        },
        new()
        {
            Id = "reader_07",
            Title = "读到最后的人",
            StorylineId = "reader",
            Order = 7,
            Icon = "🏁",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 58_000)),
            Body = "第一次有人读到了最后一页。那人合上书之后坐了很久，"
                 + "久到她把灯关了又开。"
                 + "「后面呢？」那人问。「后面没有了。」她说。"
                 + "「那我怎么办？」「那是你的事了。」",
        },
        new()
        {
            Id = "reader_08",
            Title = "读者会走",
            StorylineId = "reader",
            Order = 8,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 62_000)),
            Body = "她慢慢发现，读者是留不住的：今天来的人明天不一定来，"
                 + "借走的那本可能永远不还。她起初焦虑，后来习惯了——"
                 + "「不是他们走了，是我还以为他们在。」",
        },
        new()
        {
            Id = "reader_09",
            Title = "一个读了五本的人",
            StorylineId = "reader",
            Order = 9,
            Icon = "👣",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 67_000)),
            Body = "有人五本都读了，而且顺序没乱。那人说：「我一直在等第五本里的那一句。」"
                 + "她问哪一句。那人说：「你不知道？那你写得比你自己以为的多。」",
        },
        new()
        {
            Id = "reader_10",
            Title = "被读到最后",
            StorylineId = "reader",
            Order = 10,
            Icon = "🕯️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.Counter(ReadershipModule.CounterKey, 70_000)),
            Body = "闭馆的时间早就过了，阅览室还亮着一盏灯。她走过去，"
                 + "看见最后一个人正读到最后一页。"
                 + "她没有催，也没有出声，只是把另一盏灯也打开了——"
                 + "这样对方就不会知道自己正被看着。",
        },
    ];

    private static IEnumerable<LoreEntry> Blank() =>
    [
        new()
        {
            Id = "blank_01",
            Title = "没人读的那一页",
            StorylineId = "blank",
            Order = 1,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(400),
            Body = "她写过一段没人读过的东西：不在任何一本书里，只在她随手的纸上。"
                 + "第二天那张纸是空白的。她以为自己记错了，"
                 + "直到她发现——不是纸空了，是那件事没了。",
        },
        new()
        {
            Id = "blank_02",
            Title = "查一下借阅记录",
            StorylineId = "blank",
            Order = 2,
            Icon = "🗃️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(12),
            Body = "她开始查借阅记录：凡是被借走过一次的书，都还在；"
                 + "从来没被登记过的，有几本她明明记得自己写过。"
                 + "「不是丢了，」她在本子上写，「是从来没人知道它在。」",
        },
        new()
        {
            Id = "blank_03",
            Title = "空白的第二排",
            StorylineId = "blank",
            Order = 3,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(16),
            Body = "书架第二排有几格一直是空的，但她量过：那几格比别的地方窄。"
                 + "窄出来的尺寸正好是一本书的厚度。"
                 + "她没有去填那几格，因为不知道该填什么。",
        },
        new()
        {
            Id = "blank_04",
            Title = "写给不存在的读者",
            StorylineId = "blank",
            Order = 4,
            Icon = "✒️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.AchievementsAtLeast(20)),
            Body = "她试过一次：故意写一段不给任何人看的，然后盯着它，"
                 + "看它会不会消失。第二天它还在。"
                 + "她松了一口气，随即又觉得不好——"
                 + "「原来只要我自己读过，它就算被读过。」",
        },
        new()
        {
            Id = "blank_05",
            Title = "合订本缺的那一页",
            StorylineId = "blank",
            Order = 5,
            Icon = "📖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.AchievementsAtLeast(24)),
            Body = "合订本装订完，页码是连续的，但有一处的厚度不对。"
                 + "她把那页对着灯看了很久——字还在，只是她想不起来写过。"
                 + "「这一页不是我写的。」她说，「但它是我的字迹。」",
        },
        new()
        {
            Id = "blank_06",
            Title = "它需要有人读",
            StorylineId = "blank",
            Order = 6,
            Icon = "👁️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.AchievementsAtLeast(28)),
            Body = "结论是她推出来的：这个世界不是「写完就存在」，是「被读才存在」。"
                 + "写只是把它放到那里，读才是把它留下来。"
                 + "「所以我写的每一本，其实都是给正在读的那个人写的。」",
        },
        new()
        {
            Id = "blank_07",
            Title = "如果没人再读",
            StorylineId = "blank",
            Order = 7,
            Icon = "🌑",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.AchievementsAtLeast(32)),
            Body = "她想过那个最坏的情况：所有人都走了，灯全灭了，"
                 + "五本书从书架上开始变薄，最后一页一页地空白下去。"
                 + "想到最后她停下了——不是因为可怕，是因为她发现自己在想的时候，"
                 + "还在写。",
        },
        new()
        {
            Id = "blank_08",
            Title = "空白之书是给谁的",
            StorylineId = "blank",
            Order = 8,
            Icon = "📄",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.AchievementsAtLeast(34)),
            Body = "最后一本写完那天，她终于去翻了书架最左端那本没有书名的书。"
                 + "第一页上有字了，是她的笔迹，写着一句她从来没写过的句子："
                 + "「你已经写完了，现在轮到别人读。」"
                 + "她合上它，放回原处，然后第一次坐在了阅览室的椅子上。",
        },
    ];
}
