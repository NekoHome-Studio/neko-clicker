using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>重启次数</b>走：每重启一次，故事往前推一段；四条线各自在很小的动作上开篇
/// （点击 1 / 25 / 100 / 400 次），免得图鉴一开就是一整墙 ???，也避免「开局十分钟放出六条」。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1~#3、#10 相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁，
///     阶段 2.6 实测九命 46 条里有 33 条撞车、进第 5 命一次放出 6 条，全是这么来的。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现「第 3 条还锁着，第 4 条已亮」。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前重启走人，这条永远读不到。
///     末世尤其要注意：<c>CookiesEarnedThisRun</c> 在重启时归零，所以门槛必须落在
///     <b>单层之内</b>够得到的量级上，不能拿「多次重启累计」当条件（那是 <c>EarnedAllTime</c> 的活）。</item>
/// </list>
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
            Theme = "主线：从地下室醒过来的那个人，一共重启了五次。",
            Icon = "🐾",
            TotalEntries = 12,
        },
        new()
        {
            Id = "ruin",
            Name = "废墟",
            Theme = "世界：上一批人类是怎么结束的，以及他们留下了什么。",
            Icon = "🧱",
            TotalEntries = 10,
        },
        new()
        {
            Id = "echo",
            Name = "回声",
            Theme = "继承：重启之后仍然站在原地的那些东西。",
            Icon = "🔁",
            TotalEntries = 10,
        },
        new()
        {
            Id = "seed",
            Name = "种子",
            Theme = "将来：她要不要把人类叫回来。",
            Icon = "🌱",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Her(),
        .. Ruin(),
        .. Echo(),
        .. Seed(),
    ];

    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = "第一天",
            StorylineId = "her",
            Order = 1,
            Icon = "🕯️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她醒过来的时候，先听见的是水声——不是雨，是墙里面。地下室塌了一角，"
                 + "外面的光从那道口子斜进来，正好落在一张翻倒的桌子上。"
                 + "她坐起来，把手在裤子上擦干净，然后说：「那就从今天算起。」"
                 + "没有人回答她，她也没有等。",
        },
        new()
        {
            Id = "her_02",
            Title = "先找水",
            StorylineId = "her",
            Order = 2,
            Icon = "💧",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(2e5)),
            Body = "她花了两天找到一间还存着水的房子。水塔里的水是绿的，静置一夜之后能用。"
                 + "她把第一杯倒进锅里，煮开，等它凉，才喝。"
                 + "「先找水，再找电，最后才找人。」她把这句写在了墙上，怕自己忘。",
        },
        new()
        {
            Id = "her_03",
            Title = "名字",
            StorylineId = "her",
            Order = 3,
            Icon = "✍️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(2e6)),
            Body = "她想不起自己的名字。想不起的那部分很干净，像一块被反复擦过的黑板。"
                 + "她试着在墙上写了几个字，都不对。最后她写了一个「她」字，"
                 + "盯着看了一会儿，决定先这么用着。",
        },
        new()
        {
            Id = "her_04",
            Title = "灯",
            StorylineId = "her",
            Order = 4,
            Icon = "💡",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(5e6)),
            Body = "第三台发电机接上之后，地下室第一次整晚亮着。她坐在灯下面，什么也没干，"
                 + "就坐着看到天亮。后来她说，那是重启之后她第一次觉得「这一天」和「下一天」"
                 + "是连着的——有灯的时候，晚上也算一天。",
        },
        new()
        {
            Id = "her_05",
            Title = "写下来",
            StorylineId = "her",
            Order = 5,
            Icon = "📝",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(4e7)),
            Body = "她开始记账：今天挖到多少，用了多少，还差多少。写着写着她发现，"
                 + "这些数字比她自己记得清楚。「那就一直写。」"
                 + "她把账本放在门口，出门前看一眼，回来再看一眼。",
        },
        new()
        {
            Id = "her_06",
            Title = "第二次醒来",
            StorylineId = "her",
            Order = 6,
            Icon = "🔁",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(1e8)),
            Body = "雨把地下室泡塌了。她在地面上睁眼，看见的第一个东西是她自己上一次架的天线——"
                 + "歪着，还在。她愣了很久，然后走过去把它扶正。"
                 + "「原来有些东西是不会忘的。」她在账本第一页写下这句，划掉了，又写上。",
        },
        new()
        {
            Id = "her_07",
            Title = "避难所",
            StorylineId = "her",
            Order = 7,
            Icon = "🛖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(8e8)),
            Body = "地下二层是她自己挖的，门朝里开，因为朝外开的话，水压一大就打不开了。"
                 + "墙上有一道炭笔画的线，旁边写着「到这里为止淹过」。"
                 + "那条线比她刚来的时候高了半尺。",
        },
        new()
        {
            Id = "her_08",
            Title = "她们来了",
            StorylineId = "her",
            Order = 8,
            Icon = "🏕️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(6e9)),
            Body = "天线上第一次收到人声。三公里外有人问：「那边是不是有电？」"
                 + "她对着话筒想了很久，最后说的是：「有。你们走过来要多久？」"
                 + "那天下午来了四个人，其中一个进门之后先哭了。",
        },
        new()
        {
            Id = "her_09",
            Title = "变异体",
            StorylineId = "her",
            Order = 9,
            Icon = "🧬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(2e10)),
            Body = "它第一次出现是在东边的仓房，比人矮，走得比人快。她躲了一整天。"
                 + "第二次它没有靠近，只是站在门口看她搬东西。"
                 + "第三次她扔了一块肉过去。现在它每天傍晚来一次，站在同一个位置。",
        },
        new()
        {
            Id = "her_10",
            Title = "挖出来",
            StorylineId = "her",
            Order = 10,
            Icon = "🕸️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(5e10)),
            Body = "她在墙上写字，写完又擦掉，改成三个字：「挖出来」。"
                 + "档案馆封顶那天，她把上一世留下的第一片记忆残片放进了第一格抽屉，"
                 + "标签上写的还是「她」。",
        },
        new()
        {
            Id = "her_11",
            Title = "记得太多",
            StorylineId = "her",
            Order = 11,
            Icon = "🫥",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1e11)),
            Body = "拼到一定数量之后，她开始分不清哪些记忆是自己的。"
                 + "有些句子她会说，但不是她的语气；有些疼她记得，但想不起是在哪儿疼的。"
                 + "她把这些单独放在一排抽屉里，标签写着「不属于我，但我不想扔」。",
        },
        new()
        {
            Id = "her_12",
            Title = "第五次",
            StorylineId = "her",
            Order = 12,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1.4e11)),
            Body = "第五次醒来的时候，她没有先去地下室。她直接上了城墙。"
                 + "探地雷达上是一片一片的回波——城墙外埋着整座城市，完整地压在地下三米。"
                 + "风很大，她站了很久，然后下去准备工具。",
        },
    ];

    private static IEnumerable<LoreEntry> Ruin() =>
    [
        new()
        {
            Id = "ruin_01",
            Title = "一层压着一层",
            StorylineId = "ruin",
            Order = 1,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.EarnedAllTimeAtLeast(30),
            Body = "废墟不是一堆碎东西，是很多层完整的房子叠在一起。挖下去的时候能看出年份："
                 + "最上面那层是别人的家，再往下是办公室、店面、地铁。"
                 + "最底下那层是混凝土，厚得凿不动。",
        },
        new()
        {
            Id = "ruin_02",
            Title = "不是一次结束的",
            StorylineId = "ruin",
            Order = 2,
            Icon = "📉",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(5e4)),
            Body = "她原本以为世界是被某件事一下毁掉的。但地下三米的房子里，"
                 + "桌子是摆好的，碗是洗过的，日历停在同一天之前很久。"
                 + "「他们是慢慢结束的，」她说，「不是一下。」",
        },
        new()
        {
            Id = "ruin_03",
            Title = "留下的清单",
            StorylineId = "ruin",
            Order = 3,
            Icon = "📋",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.EarnedThisRunAtLeast(5e5)),
            Body = "一间办公室的白板上还留着最后一版清单，写了二十几行，前面几行打了勾："
                 + "「水」「药」「发电机」「给妈妈的信」。最后一行没有打勾，字很小："
                 + "「如果还有人，别一个人住。」",
        },
        new()
        {
            Id = "ruin_04",
            Title = "净水器",
            StorylineId = "ruin",
            Order = 4,
            Icon = "💧",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(1e6)),
            Body = "她的净水器是照着废墟里那台修的，连滤芯的顺序都没改。"
                 + "「他们试过很多次才排成这个顺序。」她说这句话的时候没有骄傲，"
                 + "像是在替别人把话说完。",
        },
        new()
        {
            Id = "ruin_05",
            Title = "图书馆",
            StorylineId = "ruin",
            Order = 5,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(1e7)),
            Body = "城南那栋楼她一直没敢进去，因为门口的车排得太整齐了。"
                 + "第三年她进去了：一楼是阅览室，桌上摊着书，书页朝下扣着，"
                 + "像是有人只是出去接了个电话。",
        },
        new()
        {
            Id = "ruin_06",
            Title = "温室",
            StorylineId = "ruin",
            Order = 6,
            Icon = "🌱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.EarnedThisRunAtLeast(2e8)),
            Body = "第一株活下来的东西不是她种的。它在开裂的柏油路缝里长出来，"
                 + "她路过三次，第四次停下来，把它挖了出来搬进屋里。"
                 + "「它自己都要活，那就让它活。」",
        },
        new()
        {
            Id = "ruin_07",
            Title = "最后一封信",
            StorylineId = "ruin",
            Order = 7,
            Icon = "✉️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(4e8)),
            Body = "在一台还能开机的终端里，她读到一封没有发出去的信，收件人是「所有还活着的人」。"
                 + "信里说：我们已经做不到什么了，但如果你在读这封信，"
                 + "说明你在一个我们没去过的地方，请把这里的东西带走一些。"
                 + "她读了三遍，然后把信拷进了自己的账本。",
        },
        new()
        {
            Id = "ruin_08",
            Title = "城市为什么在地下",
            StorylineId = "ruin",
            Order = 8,
            Icon = "⛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.EarnedThisRunAtLeast(2e9)),
            Body = "不是地震把它埋起来的。是她自己人干的——为了把不能带走的东西封存好，"
                 + "灰浆一层一层浇进去，浇得非常仔细。"
                 + "「这是有人留下来的，」她说，「留着给以后的人挖。」",
        },
        new()
        {
            Id = "ruin_09",
            Title = "遗迹之城",
            StorylineId = "ruin",
            Order = 9,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(3e10)),
            Body = "她把整座城留着当档案，不准任何人拆。有人说这里能住几千人，"
                 + "她说：「拆了就真的没有了。」两句话都对，但她说了算。",
        },
        new()
        {
            Id = "ruin_10",
            Title = "他们本来想去哪",
            StorylineId = "ruin",
            Order = 10,
            Icon = "🛰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.EarnedThisRunAtLeast(1.2e11)),
            Body = "数据塔最深处有一份没发出去的航行计划，目标是一颗编号很好记的行星。"
                 + "推进器造了一半，剩下的一半在图纸上。"
                 + "「他们想走，」她说，「但是没走成。那就说明这里有值得留的东西。」",
        },
    ];

    private static IEnumerable<LoreEntry> Echo() =>
    [
        new()
        {
            Id = "echo_01",
            Title = "还站在原地的东西",
            StorylineId = "echo",
            Order = 1,
            Icon = "🔁",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "第一次重启之后她发现，有一些东西没有跟着世界一起消失："
                 + "一段路她记得怎么走，一台机器她记得怎么修。"
                 + "她花了很久才接受一件事——<b>忘掉世界的时候，并没有把她的经验一起忘掉</b>。",
        },
        new()
        {
            Id = "echo_02",
            Title = "熟悉的手感",
            StorylineId = "echo",
            Order = 2,
            Icon = "🤲",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.Counter(ShardsModule.CounterKey, 500)),
            Body = "她在完全陌生的地方，用一模一样的顺序清理一块空地：先清边角，再清中间，"
                 + "最后才搬重物。她没有想过为什么是这个顺序——手比脑子先想起来了。",
        },
        new()
        {
            Id = "echo_03",
            Title = "第二次从零开始",
            StorylineId = "echo",
            Order = 3,
            Icon = "💡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(2), UnlockCondition.Counter(ShardsModule.CounterKey, 3000)),
            Body = "第一次重启时，她以为原来的东西会全部消失，所以用了整整一个月做心理准备。"
                 + "结果她走进东边那栋楼的二楼，发电机还在原地。"
                 + "她在那儿站了十分钟，然后开始拆锈。",
        },
        new()
        {
            Id = "echo_04",
            Title = "第一批碎片",
            StorylineId = "echo",
            Order = 4,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.Counter(ShardsModule.CounterKey, 1e4)),
            Body = "从废墟里挖出来的东西里，有一小部分是她自己的：一段走路的声音，"
                 + "一个拧螺丝的手势，一句没说完的话。她把这些单独收起来，"
                 + "因为「这些不是世界的东西，是我的」。",
        },
        new()
        {
            Id = "echo_05",
            Title = "档案馆的第一格",
            StorylineId = "echo",
            Order = 5,
            Icon = "🗄️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.Counter(ShardsModule.CounterKey, 3e4)),
            Body = "第一格抽屉里的东西她换过三次。最早放的是账本，后来放的是一张脸，"
                 + "最后放的是一片什么都没有的碎片——她说那上面本来是有一句话的，"
                 + "后来那句话被她自己用掉了。",
        },
        new()
        {
            Id = "echo_06",
            Title = "第三次",
            StorylineId = "echo",
            Order = 6,
            Icon = "🏕️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 6e4)),
            Body = "第三次重启之后，她不再是唯一一个「记得上一次」的人了："
                 + "有三个人保留着上一次的手艺，其中一个还记得她上一次说过的一句玩笑。"
                 + "她听见那句话被重复出来的那个瞬间，忽然觉得重启这件事没有那么冷。",
        },
        new()
        {
            Id = "echo_07",
            Title = "记得住的重量",
            StorylineId = "echo",
            Order = 7,
            Icon = "⚖️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 1.2e5)),
            Body = "残片越攒越多之后，产量反而开始变好——她自己也没想到。"
                 + "「记得住的人少走弯路。」她在账本上写，然后补了一句："
                 + "「但也走得慢一点，因为会回头。」",
        },
        new()
        {
            Id = "echo_08",
            Title = "第四次重启之前",
            StorylineId = "echo",
            Order = 8,
            Icon = "🕸️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.Counter(ShardsModule.CounterKey, 2e5)),
            Body = "这一次她提前把整座档案馆抄了一遍关键字，抄在能带走的金属片上。"
                 + "有人说没必要，她说不，有必要：「上一次我也以为没必要。」",
        },
        new()
        {
            Id = "echo_09",
            Title = "不属于任何人的记忆",
            StorylineId = "echo",
            Order = 9,
            Icon = "🌫️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.Counter(ShardsModule.CounterKey, 2.5e5)),
            Body = "档案馆里有一整排没有人认领的记忆：一双手在系鞋带、一个孩子在笑、"
                 + "一场雨里有人跑过马路。她给这排抽屉起了个名字叫「无论如何」。"
                 + "有人问她为什么不删掉，她说：「删掉更麻烦，得先决定谁有资格删。」",
        },
        new()
        {
            Id = "echo_10",
            Title = "第五次带过去的东西",
            StorylineId = "echo",
            Order = 10,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.Counter(ShardsModule.CounterKey, 3e5)),
            Body = "第五次重启之后她清点过一遍：这条街上七成的东西都在，"
                 + "连最老的那批废墟都还在原地。她在账本上写："
                 + "「这一次，我几乎把整个文明搬过来了。」写完又划掉，改成「带过来了」。",
        },
    ];

    private static IEnumerable<LoreEntry> Seed() =>
    [
        new()
        {
            Id = "seed_01",
            Title = "要不要叫回来",
            StorylineId = "seed",
            Order = 1,
            Icon = "🌱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(400),
            Body = "问题是在第三次重启之后才出现的：既然记忆能挖出来，"
                 + "那能不能把人也挖回来？她把这个问题写在了墙上，"
                 + "在下面画了一道横线，横线下面一直空着。",
        },
        new()
        {
            Id = "seed_02",
            Title = "第一批留下来的人",
            StorylineId = "seed",
            Order = 2,
            Icon = "🛖",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(12),
            Body = "聚集地里的人不是被叫醒的，是自己走过来的。"
                 + "他们会害怕、会累、会因为一锅汤吵架，"
                 + "这些在没有人的世界里都不会发生。「原来麻烦也是活着的证据。」她说。",
        },
        new()
        {
            Id = "seed_03",
            Title = "一个人也能过",
            StorylineId = "seed",
            Order = 3,
            Icon = "🕯️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.AchievementsAtLeast(16),
            Body = "最省事的方案是不叫任何人回来：她能干，她记得，她不会跟人吵架。"
                 + "这个方案她已经执行了四次。第五次的时候，她第一次觉得这个方案有点冷。",
        },
        new()
        {
            Id = "seed_04",
            Title = "拼出来的是什么",
            StorylineId = "seed",
            Order = 4,
            Icon = "🔮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.AchievementsAtLeast(20)),
            Body = "用记忆残片拼一个人，拼出来的是「他是什么样」，不是「他是谁」。"
                 + "她会得到一张会说话的脸，一段会讲笑话的语气，"
                 + "但那里面到底有没有人，她不知道，也没有人能告诉她。",
        },
        new()
        {
            Id = "seed_05",
            Title = "代价写在附页上",
            StorylineId = "seed",
            Order = 5,
            Icon = "📄",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(3), UnlockCondition.AchievementsAtLeast(24)),
            Body = "把一整座城市挖出来要挖几十年，聚变堆要一座一座修，"
                 + "档案馆上面那排空白抽屉要一格一格填。"
                 + "她把所有代价列在一张纸上算过，结论是：做得到，但要活得很久。",
        },
        new()
        {
            Id = "seed_06",
            Title = "第四次重启的赌注",
            StorylineId = "seed",
            Order = 6,
            Icon = "🕸️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.AchievementsAtLeast(28)),
            Body = "第四次重启，她把赌注压在「记得住」上：宁可少建两座堆，也要把档案馆先立起来。"
                 + "那一年产量落后了，但下一轮她省下了重新学一遍的全部时间。",
        },
        new()
        {
            Id = "seed_07",
            Title = "有人替她做了决定",
            StorylineId = "seed",
            Order = 7,
            Icon = "🗣️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(4), UnlockCondition.AchievementsAtLeast(32)),
            Body = "聚集地为这件事开过一次会，开到半夜。最后是一个老人说的："
                 + "「把我们叫回来的人，得愿意跟我们住在一起。」"
                 + "她点头点得很慢，因为那意味着她不能再重启了。",
        },
        new()
        {
            Id = "seed_08",
            Title = "城墙下",
            StorylineId = "seed",
            Order = 8,
            Icon = "🏚️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(UnlockCondition.EraAtLeast(5), UnlockCondition.AchievementsAtLeast(34)),
            Body = "她站在城墙上，看着下面那座还没被挖出来的城市。"
                 + "风很大，探地雷达还在响。她想起自己第一天在地下室里说过的那句话——"
                 + "「那就从今天算起」——然后下去了。",
        },
    ];
}
