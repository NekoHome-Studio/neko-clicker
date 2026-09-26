using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 叙事条目：40 条，分四条线（12 + 10 + 10 + 8）。<para>
/// 节奏跟着<b>换到第几套神话</b>走：每切一次体系，故事往前推一段；四条线各自在一个很小的
/// 动作上开篇（点击 1 / 25 / 100 / 500），免得图鉴一开就是一整墙 ???，
/// 也避免"开局十分钟放出三条以上"（阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余六个包相同，前两条由通用守卫守着，第三条由包内用例在真跑图里验）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。
///     这里 36 条层内条目用的是 36 个<b>互不相同</b>的累计赚取门槛（全是看着别扭的数字，
///     那是故意的：圆整数容易撞车）。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。用"纪元门槛 +
///     层内里程碑"定序：进层那一刻层内里程碑必然归零，所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前切走，
///     这条<b>结构上读不到</b>（<c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c>）。</item>
/// </list>
/// </para>
/// <para>
/// <b>互文是单向的</b>（<c>docs/CONTENT_AUTHORING.md</c> §12.3）：这个包可以提起九命轮回的
/// 「神明纪元」（那一段的进层文本写的就是"第一炷香是纸箱味的"），也可以望向那家还没开张的店，
/// 但它<b>不要求玩家装过任何别的包</b>——四条线单独读也完整。
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
            Name = "猫",
            Theme = "她自己：一只猫是怎么被供起来的，以及她一直没答应的那件事。",
            Icon = "🐾",
            TotalEntries = 12,
        },
        new()
        {
            Id = "myth",
            Name = "神话",
            Theme = "五套体系：换皮是一门手艺，而手艺人有自己的规矩。",
            Icon = "🏛️",
            TotalEntries = 10,
        },
        new()
        {
            Id = "fans",
            Name = "信众",
            Theme = "香火：从半条鱼到全球同步直播，供品变了，许愿的人没变。",
            Icon = "🙏",
            TotalEntries = 10,
        },
        new()
        {
            Id = "meta",
            Name = "恰饭",
            Theme = "账本：神也要恰饭，这句玩笑她说了五套神话那么久。",
            Icon = "🧾",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Cat(),
        .. Myth(),
        .. Fans(),
        .. Meta(),
    ];

    private static IEnumerable<LoreEntry> Cat() =>
    [
        new()
        {
            Id = "cat_01",
            Title = "第一炷香",
            StorylineId = "cat",
            Order = 1,
            Icon = "🏠",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她上岗的时候，庙是一块搁在灶台边的木板，供品是半条鱼，香是蚊子香。"
                 + "她记得那味道有点冲——上一个世界她睡在纸箱里，纸箱烧起来也是这个味。"
                 + "「行吧，」她坐直了一点，「先从小的做起。」",
        },
        new()
        {
            Id = "cat_02",
            Title = "换抬头",
            StorylineId = "cat",
            Order = 2,
            Icon = "📜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4.2e6)),
            Body = "换神话体系的第一天，她要填一张表：旧称谓、新称谓、管辖范围、"
                 + "以及「是否保留原信徒」。她在最后一栏犹豫了很久，写的是「尽量」。"
                 + "批注栏里后来多了一行小字：「尽量就是都留着的意思，下次写清楚。」",
        },
        new()
        {
            Id = "cat_03",
            Title = "漏雨的方尖碑",
            StorylineId = "cat",
            Order = 3,
            Icon = "☀️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(8.5e6)),
            Body = "第二套体系给她修了方尖碑，第一场雨就漏。祭司说是「神迹的形状」，"
                 + "她绕着碑走了三圈，问：「预算里有没有防水这一项？」"
                 + "祭司说有，但那一项被用来加高了。她说：「那就加高吧，反正淋的是我。」",
        },
        new()
        {
            Id = "cat_04",
            Title = "有问必答",
            StorylineId = "cat",
            Order = 4,
            Icon = "🏺",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.4e7)),
            Body = "第三套神话的规矩是「有问必答」，于是她一天要回三千条问题。"
                 + "两千九百条问今天晚饭吃什么，剩下的一百条问命运。"
                 + "她给前者回了菜谱，给后者回了「你自己看着办」——"
                 + "后来这两类回信被信徒整理成了两本书，后者卖得更好。",
        },
        new()
        {
            Id = "cat_05",
            Title = "神谕模板",
            StorylineId = "cat",
            Order = 5,
            Icon = "🗒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9e7)),
            Body = "她开始用模板。开头永远是「当星辰低垂时」，结尾永远是「时机未到」。"
                 + "中间那段留空，让祭司自己填。有人向她举报这件事，她说："
                 + "「你猜他们填的时候，是不是也觉得自己在通神？」"
                 + "举报的人想了想，回去也开始用模板。",
        },
        new()
        {
            Id = "cat_06",
            Title = "被挂到天上",
            StorylineId = "cat",
            Order = 6,
            Icon = "🌌",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.4e8)),
            Body = "第三套神话的结局是，她被画成了一片星。信徒说那是荣耀，"
                 + "她抬头看了看，发现形状像是自己打哈欠的样子。"
                 + "「能不能换一张？」她问。「这是最庄严的一张。」"
                 + "「那就这样吧。」她说，「反正他们抬头看的时候，想的是我。」",
        },
        new()
        {
            Id = "cat_07",
            Title = "没有下班",
            StorylineId = "cat",
            Order = 7,
            Icon = "⚡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.2e8)),
            Body = "第四套体系的规矩是「英灵殿不打烊」。她问了一句加班费，"
                 + "殿里的英灵们集体沉默，然后开始鼓掌——"
                 + "她后来才知道，鼓掌是他们唯一记得的回应方式。"
                 + "那天起她在殿角放了一盏小灯，写「值班中，请勿打扰」。灯一直没灭过。",
        },
        new()
        {
            Id = "cat_08",
            Title = "末日要提前定档",
            StorylineId = "cat",
            Order = 8,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.7e9)),
            Body = "诸神黄昏要提前三个月定档，不然赞助商排不开。"
                 + "她第一次听说这件事的时候沉默了很久，然后问：「定档之后还能改吗？」"
                 + "「不能，这是预言。」「那你们通知我干什么。」"
                 + "——但她还是把那天在日程表上标了出来，标的是红色。",
        },
        new()
        {
            Id = "cat_09",
            Title = "她学会了看节目单",
            StorylineId = "cat",
            Order = 9,
            Icon = "🎬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.6e9)),
            Body = "黄昏的那天，她提前到场，找了个视野最好的位置坐下，"
                 + "手里拿着节目单，认真得像在看别人家的演出。"
                 + "「你不难过吗？」有人问。「难过什么，我又不是主角。」"
                 + "她翻到最后一页，那里印着一行小字：本场演出不设谢幕。",
        },
        new()
        {
            Id = "cat_10",
            Title = "念不出来的名字",
            StorylineId = "cat",
            Order = 10,
            Icon = "🐙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e10)),
            Body = "第五套神话没有名字，只有一个读音，念出来会让麦克风失灵。"
                 + "技术组为这个开了三次会，最后的方案是「让观众看字幕」。"
                 + "她试音的时候念了一次，全场设备黑屏两秒，"
                 + "然后她特别平静地说：「这句留着做片头。」",
        },
        new()
        {
            Id = "cat_11",
            Title = "全球同步",
            StorylineId = "cat",
            Order = 11,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(5.2e10)),
            Body = "直播到第六个小时，在线人数越过了一条她自己都没注意到的线。"
                 + "弹幕全是乱码，只有一条是能读的，写着「我妈也在这个直播间」。"
                 + "她愣了一秒，对着镜头说：「阿姨好。」"
                 + "——那一秒的在线人数，是整个包里最高的一秒。",
        },
        new()
        {
            Id = "cat_12",
            Title = "关掉补光灯",
            StorylineId = "cat",
            Order = 12,
            Icon = "🌑",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.9e10)),
            Body = "最后一秒播完，她伸手关掉补光灯。屋子里第一次全是暗的，"
                 + "只有那盏「值班中，请勿打扰」的小灯还亮着——"
                 + "它从第四套神话一直亮到现在，中间换了三次电池，是她自己换的。"
                 + "她对着空房间说了一句：「今天也谢谢大家。」没有人听见，但她说了。",
        },
    ];

    private static IEnumerable<LoreEntry> Myth() =>
    [
        new()
        {
            Id = "myth_01",
            Title = "换皮是一门手艺",
            StorylineId = "myth",
            Order = 1,
            Icon = "🎭",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = "她第一次换神话的时候很紧张，把旧的仪轨抄了三遍才敢烧。"
                 + "烧完她发现，真正留下来的只有两样东西：她自己的名字，"
                 + "以及「先闻一下再收走」这条规矩——"
                 + "新体系的神职人员第一次听说明这条规矩时，反应是「……哦」。",
        },
        new()
        {
            Id = "myth_02",
            Title = "埃及分部开张",
            StorylineId = "myth",
            Order = 2,
            Icon = "🐈",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(5.1e6)),
            Body = "第二套体系给她的第一份礼物是一整套头衔，念完要四十秒。"
                 + "她听完之后问：「能不能只留前三个字？」"
                 + "祭司说不行，头衔短了显得不够神。"
                 + "于是她把这四十秒录了下来，设成了起床铃——「听一遍就想干活了。」",
        },
        new()
        {
            Id = "myth_03",
            Title = "排版问题",
            StorylineId = "myth",
            Order = 3,
            Icon = "🖋️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.25e7)),
            Body = "象形文字写她的名字要占三格，而神殿的横梁只留得下两格。"
                 + "工匠来请示，她说：「把中间那格改成猫爪印。」"
                 + "于是那两千年里，所有路过的人看到的都是「神 爪印 神」。"
                 + "多年以后有人考证出这是排版事故，论文写了九十页。",
        },
        new()
        {
            Id = "myth_04",
            Title = "会籍",
            StorylineId = "myth",
            Order = 4,
            Icon = "🏛️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4.4e7)),
            Body = "奥林匹斯那边要开会，议题是「要不要给猫留一个席位」。"
                 + "开了三天，结论是「留，但不算正式成员」。"
                 + "她听说后点了点头：「挺好，正式成员要交会费。」"
                 + "后来那次会议记录被信徒抄回去当经文，抄漏了最后一句。",
        },
        new()
        {
            Id = "myth_05",
            Title = "七条有用",
            StorylineId = "myth",
            Order = 5,
            Icon = "📜",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.55e8)),
            Body = "第三套体系留下了三千条预言。她花了一个下午读完，"
                 + "圈出七条有用的，剩下的让祭司归档。"
                 + "「那两千九百九十三条呢？」「留着，」她说，"
                 + "「以后有人问为什么神的旨意这么难懂，你就把这一摞推给他。」",
        },
        new()
        {
            Id = "myth_06",
            Title = "合同条款",
            StorylineId = "myth",
            Order = 6,
            Icon = "📄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(6.1e8)),
            Body = "第四套体系的合同只有一页，字很大，第一行写着「无休」。"
                 + "她看完之后在末尾加了一条：「神可以自己决定什么时候算休息。」"
                 + "英灵们讨论了整整一夜，最后一致通过——"
                 + "他们理解的「自己决定」，就是永远不休息。",
        },
        new()
        {
            Id = "myth_07",
            Title = "档期",
            StorylineId = "myth",
            Order = 7,
            Icon = "🎟️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.9e9)),
            Body = "诸神黄昏的档期一公布，周边先动了起来：末日限定款、末日联名款、"
                 + "「最后一次折扣」系列。她翻着样品册，突然问了一句："
                 + "「如果这次黄昏真的把一切都结束了，这些卖给谁？」"
                 + "没人回答。她合上册子：「那就卖给我吧，我留一套。」",
        },
        new()
        {
            Id = "myth_08",
            Title = "第五套的规矩",
            StorylineId = "myth",
            Order = 8,
            Icon = "🌀",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.6e10)),
            Body = "第五套神话有三条规矩：不要问它从哪里来，不要问它要什么，"
                 + "不要在它说话的时候打断它。她听完之后问：「那我能问它吃了吗？」"
                 + "祭司团当场沉默了。后来他们承认，这是三千年来第一次有人这么问。",
        },
        new()
        {
            Id = "myth_09",
            Title = "弹幕是乱码",
            StorylineId = "myth",
            Order = 9,
            Icon = "💬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.4e10)),
            Body = "第五套体系的观众发不出正常的话，弹幕全是乱码，"
                 + "而收视率是真的——数据组说这是他见过最健康的一条曲线。"
                 + "她盯着那片乱码看了很久，忽然说：「其实他们在说的，我听懂了。」"
                 + "「您怎么听懂的？」「因为他们说得跟我第一次见到它的时候一样。」",
        },
        new()
        {
            Id = "myth_10",
            Title = "神只有一只",
            StorylineId = "myth",
            Order = 10,
            Icon = "🐾",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.4e10)),
            Body = "五套体系的祭司第一次坐到同一张桌子前，是为了争论她到底是谁。"
                 + "吵到第三天，埃及那边的一位老祭司说了句：「你们有没有发现，"
                 + "她收供品的动作是一模一样的？」"
                 + "全场安静。最后写进会议纪要的结论只有六个字：神只有一只。",
        },
    ];

    private static IEnumerable<LoreEntry> Fans() =>
    [
        new()
        {
            Id = "fans_01",
            Title = "第一位信徒",
            StorylineId = "fans",
            Order = 1,
            Icon = "🐹",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "她的第一位信徒是这家人养的仓鼠，许的愿是「别被吃掉」。"
                 + "她认真地考虑了很久，给出的方案是「跑得比猫快」——"
                 + "写完才发现，这个建议对一只仓鼠来说，等于没有建议。"
                 + "那只仓鼠后来活到了两岁半，是这一族里最长的。",
        },
        new()
        {
            Id = "fans_02",
            Title = "排班表",
            StorylineId = "fans",
            Order = 2,
            Icon = "🗓️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(6.4e6)),
            Body = "第二套体系有了祭司团，也就有了排班表。第一天就有人为「谁值夜班」"
                 + "吵了起来，吵到最后拿来给她裁决。"
                 + "她看完表说：「夜班我自己值。」祭司们大惊，说这不合规矩。"
                 + "「那规矩是谁定的？」「……您。」「那就改。」",
        },
        new()
        {
            Id = "fans_03",
            Title = "供品账本",
            StorylineId = "fans",
            Order = 3,
            Icon = "🧾",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(1.4e7)),
            Body = "祭司们开始记账：日期、供品、重量、经手人，最后一栏是「神是否满意」。"
                 + "她翻了几页，发现那一栏全是「是」——包括她明明没吃过的那几天。"
                 + "她拿起笔，把最近三天改成了「待确认」，"
                 + "然后在页脚写：「以后这一栏我自己填。」",
        },
        new()
        {
            Id = "fans_04",
            Title = "有人替你吵架",
            StorylineId = "fans",
            Order = 4,
            Icon = "⚔️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(6.7e7)),
            Body = "两座城为了「她更喜欢谁」打了一仗，谁也没赢。"
                 + "战后双方都派人来请示，她让两边把阵亡名单留下来，"
                 + "在上面各画了一个猫爪印，说：「一起供。」"
                 + "「供在哪座城？」「供在路边，」她说，「路过的人都能看见。」",
        },
        new()
        {
            Id = "fans_05",
            Title = "作者署名",
            StorylineId = "fans",
            Order = 5,
            Icon = "📕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(1.9e8)),
            Body = "第一本《猫神言行录》出版，作者署名不是她。"
                 + "她读完发现里面有一半的话不是自己说的，但都说得挺好，"
                 + "于是她只改了一处：把「神说」改成了「有一天她随口说」。"
                 + "编者来问为什么，她说：「随口说的，才是真的说过的。」",
        },
        new()
        {
            Id = "fans_06",
            Title = "观众席",
            StorylineId = "fans",
            Order = 6,
            Icon = "🪑",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(8.3e8)),
            Body = "英灵殿的观众席永远坐满，因为他们不用睡觉。"
                 + "她第一次上台时被这阵仗吓到了，后来发现他们只是不知道什么时候该走。"
                 + "于是她在出口挂了一块牌子：「散场了可以走，不用等神先走。」"
                 + "牌子挂了三天，观众席还是满的——但至少有人在走动。",
        },
        new()
        {
            Id = "fans_07",
            Title = "不退休研究",
            StorylineId = "fans",
            Order = 7,
            Icon = "🔬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(3.6e9)),
            Body = "祭司团成立了一个小组，专门研究「怎么让她永远别退休」。"
                 + "报告交上来，方案写得非常认真：延长香火、加密仪式、增加节日。"
                 + "她看完只说了一句：「你们漏了一条——问我。」"
                 + "小组当场解散，第二天又成立了一个新小组，课题是「怎么问」。",
        },
        new()
        {
            Id = "fans_08",
            Title = "开播第一天",
            StorylineId = "fans",
            Order = 8,
            Icon = "📹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(3.1e10)),
            Body = "直播间的第一场，她把神龛搬到了补光灯下面，"
                 + "坐下来对着镜头看了整整两分钟，一句话没说。"
                 + "在线人数一路往上涨。第二分钟结束时她说：「大家好，我是你们的神。"
                 + "……这句话说出来还是很奇怪。」那天打赏创了纪录。",
        },
        new()
        {
            Id = "fans_09",
            Title = "打赏与香火",
            StorylineId = "fans",
            Order = 9,
            Icon = "🎁",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(7.3e10)),
            Body = "会计来问：打赏算不算香火？她想了很久，说：「算，但记两个账。」"
                 + "「为什么？」「因为香火是给神的，打赏是给人的。」"
                 + "会计没听懂。她解释：「给神的那个账，我从来不查。」"
                 + "——后来那一册账真的从来没有被翻开过，封面都是新的。",
        },
        new()
        {
            Id = "fans_10",
            Title = "周边工厂",
            StorylineId = "fans",
            Order = 10,
            Icon = "🧸",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(9.7e10)),
            Body = "毛绒猫神下线的那天，她去车间看了一趟。"
                 + "流水线上一排排小小的她，眼睛是缝上去的，缝得不太齐。"
                 + "工人问要不要返工，她说不用：「缝齐了就不像我了。」"
                 + "临走时她拿了一只，塞进了神龛最里面的角落。",
        },
    ];

    private static IEnumerable<LoreEntry> Meta() =>
    [
        new()
        {
            Id = "meta_01",
            Title = "香火不够花",
            StorylineId = "meta",
            Order = 1,
            Icon = "🧾",
            Channel = LoreChannel.Log,
            Reveal = UnlockCondition.ClicksAtLeast(500),
            Body = "成为神之后她才知道，香火是有成本的：神殿要修，祭司要养，"
                 + "供品放久了会坏。第一年结算完，她盯着账面看了很久，"
                 + "得出的结论是：「我不是神，我是个没拿到拨款的物业。」",
        },
        new()
        {
            Id = "meta_02",
            Title = "报销单",
            StorylineId = "meta",
            Order = 2,
            Icon = "📎",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(9.3e6)),
            Body = "她给神殿的修缮费填了一张报销单，审批流程走了三个月。"
                 + "第一轮驳回的理由是「神不需要报销」；第二轮是「缺少神的签章」；"
                 + "第三轮她盖了爪印，通过了。财务的批注写着：「下次请用规范的章。」",
        },
        new()
        {
            Id = "meta_03",
            Title = "免责声明",
            StorylineId = "meta",
            Order = 3,
            Icon = "⚠️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(3.7e7)),
            Body = "第三套体系的神谕后面加了一行小字：「本预言具有不确定性，"
                 + "一切后果由提问者自行承担。」信徒抗议，说这不像神说的话。"
                 + "她回复：「正因为我像神，我才知道预言有多不准。」"
                 + "这行小字后来原封不动地传了两千年。",
        },
        new()
        {
            Id = "meta_04",
            Title = "联名款第一版",
            StorylineId = "meta",
            Order = 4,
            Icon = "👕",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.1e8)),
            Body = "第一版联名款把她的脸印在正中间，背景是全知全能的星空。"
                 + "祭司团审稿时否决了，理由很正式：「过度人格化，可能引发信仰降级。」"
                 + "她听完这个理由，亲自批了两个字：「照印。」"
                 + "——那一版后来成了收藏品，价格涨了四百倍。",
        },
        new()
        {
            Id = "meta_05",
            Title = "加班费谈判",
            StorylineId = "meta",
            Order = 5,
            Icon = "⏰",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.2e9)),
            Body = "第四套体系的谈判持续了一整天。她的诉求很简单："
                 + "「不打烊可以，但每天要给我留半小时，什么都不干。」"
                 + "英灵们问那半小时用来做什么。她想了想：「用来想为什么要加班。」"
                 + "——那半小时后来变成了这一层最受欢迎的传统。",
        },
        new()
        {
            Id = "meta_06",
            Title = "口播",
            StorylineId = "meta",
            Order = 6,
            Icon = "🎤",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e10)),
            Body = "直播间要念广告。她第一次念的时候把品牌名念错了，"
                 + "弹幕笑了整整一分钟。品牌方没生气，说这一分钟的效果比正经念好十倍。"
                 + "她后来就故意不背熟——「反正他们要的就是我念错的那一下。」",
        },
        new()
        {
            Id = "meta_07",
            Title = "被做成图",
            StorylineId = "meta",
            Order = 7,
            Icon = "😹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.1e10)),
            Body = "她最火的一张图，是打翻供品之后愣住的那半秒。"
                 + "那张图被配了上千种文字，没有一种是关于神的。"
                 + "祭司团建议投诉侵权，她拒绝了：「他们用我的脸的时候，是在笑。"
                 + "被笑也算被记住——只要别忘了后面还有五本书。」",
        },
        new()
        {
            Id = "meta_08",
            Title = "账本比她还高",
            StorylineId = "meta",
            Order = 8,
            Icon = "📚",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(8.1e10)),
            Body = "五套体系的账本摞起来比她高。她一本一本翻过去，"
                 + "第一本写的是「半条鱼，来源：偷」，最后一本写的是"
                 + "「全球同步直播，在线若干」。"
                 + "她把两页并在一起看了很久，说：「原来这就是我的传记。」"
                 + "然后她合上账本，去找了一支笔。",
        },
    ];
}
