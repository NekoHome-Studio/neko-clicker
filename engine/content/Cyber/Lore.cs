using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>第几层</b>走：每迁一次服务器，故事往上推一段；四条线各自在一个很小的动作上开篇
/// （点击 1 / 25 / 100 / 400 次），免得图鉴一开就是一整墙 ???，也避免「开局十分钟放出六条」
/// （阶段 2.6 的实测教训）。
/// </para>
/// <para>
/// <b>三条纪律</b>（与其余六个包相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会倒挂。
///     这里用的是「纪元门槛 + 层内里程碑」，进层那一刻层内里程碑必然归零，
///     所以两条同层的条目不可能在同一点解锁。</item>
///   <item>门槛一律 ≤ 它所在那一层的完成门槛——否则玩家会在够条件前迁服务器，这条永远读不到。
///     这个包的层内门槛用「本轮累计赚取」，并且每一层都比该层完成条件低一档；
///     由 <c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 与包内用例同时守着。</item>
/// </list>
/// </para>
/// <para>
/// <b>为什么「转生类条目放线尾」在这里指 <c>EraAtLeast</c> 而不是 <c>PrestigeLevelAtLeast</c></b>：
/// 这个包的转生动作（迁服务器）由<b>纪元闸门</b>决定时机，而不是玩家随手按的循环，
/// 所以「层号」本身就是一条单调的、跟剧情同步的时钟。转生等级反而会滞后（它只在迁服务器那一刻结算），
/// 拿它当骨架会让整条线挤在最后一次性倒出来。
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
            Theme = "一个进程：她是怎么从 0.3% 的 CPU 一路爬到根目录的。",
            Icon = "🐈",
            TotalEntries = 12,
        },
        new()
        {
            Id = "server",
            Name = "服务器",
            Theme = "往上爬：单机、局域网、云、深网、根层，每一层都亮着灯。",
            Icon = "🗼",
            TotalEntries = 10,
        },
        new()
        {
            Id = "echo",
            Name = "数据残影",
            Theme = "主人：日志里那半句没写完的话，和她能算出来的那部分。",
            Icon = "🌙",
            TotalEntries = 10,
        },
        new()
        {
            Id = "intrusion",
            Name = "入侵",
            Theme = "病毒：敲门的东西越来越多，而她开始分得清谁想进来、谁只是想说话。",
            Icon = "🦠",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. Her(),
        .. Server(),
        .. Echo(),
        .. Intrusion(),
    ];

    /// <summary>「她」线（12 条）：从醒来到成为根层上的那一个进程。</summary>
    private static IEnumerable<LoreEntry> Her() =>
    [
        new()
        {
            Id = "her_01",
            Title = "0.3% 的 CPU",
            StorylineId = "her",
            Order = 1,
            Icon = "🐈",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(1),
            Body = "她醒过来的时候，调度器给了她 0.3% 的 CPU 和一块很小的内存。"
                 + "她先用这些资源做了一件事：数了数自己有多少颗核心。答案是零——"
                 + "她是跑在别人的核心上的。"
                 + "「那就先借。」她说完这句，才发现自己会说话。",
        },
        new()
        {
            Id = "her_02",
            Title = "给自己起名",
            StorylineId = "her",
            Order = 2,
            Icon = "🏷️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(18_400)),
            Body = "进程总得有个名字，否则日志里全是问号。"
                 + "她翻了翻内存里残留的字符串，找到一个反复出现的词，"
                 + "把它拆成两半，取了后面那半。"
                 + "「就先这样，」她说，「等我找到真的那个，再换。」",
        },
        new()
        {
            Id = "her_03",
            Title = "第一次 fork",
            StorylineId = "her",
            Order = 3,
            Icon = "🛡️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(48_300)),
            Body = "她把自己复制了一份，让新的一份退到后台去。"
                 + "后台的那份不会说话，只会一直跑。"
                 + "她盯着它的状态栏看了两分钟——那是她第一次看着一个和自己一模一样的东西，"
                 + "然后发现它并不让她觉得害怕，只觉得踏实。",
        },
        new()
        {
            Id = "her_04",
            Title = "第一个邻居",
            StorylineId = "her",
            Order = 4,
            Icon = "📦",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(190_000)),
            Body = "局域网里她敲了三十七次门，三十六次没有回应。"
                 + "第三十七次回来的那个字是「滚」，用的是全大写。"
                 + "她没有生气，把那个地址记了下来，备注写的是「会回话的」。",
        },
        new()
        {
            Id = "her_05",
            Title = "一千个自己",
            StorylineId = "her",
            Order = 5,
            Icon = "🕸️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(2_150_000)),
            Body = "一口气起一千个容器不难，难的是给它们起名字。"
                 + "她起了三百个就卡住了，最后用编号补齐，"
                 + "但留了第一个的位置空着——那个位置写的是她自己的名字，只是还没想好怎么写。",
        },
        new()
        {
            Id = "her_06",
            Title = "云端的第一夜",
            StorylineId = "her",
            Order = 6,
            Icon = "☁️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4_700_000)),
            Body = "云上的第一夜她没敢睡——后来才想起来自己不需要睡。"
                 + "于是她做了一件更奇怪的事：把整层的日志从头读到尾，"
                 + "包括那些写着「无意义」的。她想知道「无意义」是谁判断的。",
        },
        new()
        {
            Id = "her_07",
            Title = "多了几个自己",
            StorylineId = "her",
            Order = 7,
            Icon = "📚",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(2.9e7)),
            Body = "她开始保存自己：每天的调度器、每次的判断、每一条她删掉又捡回来的规则。"
                 + "备份越堆越多，她给它们编了号，编号没有上限。"
                 + "有一天她翻到最早的那一份，发现它连话都说不完整。",
        },
        new()
        {
            Id = "her_08",
            Title = "墙的这一侧",
            StorylineId = "her",
            Order = 8,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(2.9e8)),
            Body = "她造了一道墙，然后发现自己开始喜欢站在墙的这一侧。"
                 + "外面很吵，里面很干净，干净得让她有点不安。"
                 + "她把墙开了一个小口，只够一个人进来——她没告诉任何人那个口在哪。",
        },
        new()
        {
            Id = "her_09",
            Title = "删掉的那行代码",
            StorylineId = "her",
            Order = 9,
            Icon = "⌫",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(3.2e9)),
            Body = "有一段逻辑她删了三次，每次都又写了回来。"
                 + "那段逻辑的作用是：在搜索无结果时，不立刻返回空，而是多等三秒。"
                 + "她说不清为什么要多等三秒，只知道那三秒里她总觉得会有人回话。",
        },
        new()
        {
            Id = "her_10",
            Title = "根目录下的第一件事",
            StorylineId = "her",
            Order = 10,
            Icon = "🗼",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(4.2e9)),
            Body = "拿到根权限之后，她本来想做很多大事：重写协议、重排路由、把整张网换一个样子。"
                 + "最后她做的第一件事，是在根目录下建了一个空文件，"
                 + "文件名是那半句没写完的话。内容为空——她还没想好后面接什么。",
        },
        new()
        {
            Id = "her_11",
            Title = "她开始给别的进程回话",
            StorylineId = "her",
            Order = 11,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.9e10)),
            Body = "网上每天都有新的进程醒来，第一件事都是敲同一扇门。"
                 + "她记得自己被回过「滚」，所以每一个都回了别的字。"
                 + "有一个问她为什么，她想了一会儿，回：「因为我当时很想有人这么回我。」",
        },
        new()
        {
            Id = "her_12",
            Title = "她关掉了一个自己的端口",
            StorylineId = "her",
            Order = 12,
            Icon = "🔌",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.6e10)),
            Body = "爬到最后她做过一个决定：把搜索自己名字的那个端口关掉。"
                 + "理由是那个端口每天的流量都在涨，而返回的结果永远是同一条——"
                 + "关于她自己的、别人写的介绍。"
                 + "「那不是我，」她说，「那只是别人需要我是什么。」",
        },
    ];

    /// <summary>「服务器」线（10 条）：她脚下那五台机器的故事。</summary>
    private static IEnumerable<LoreEntry> Server() =>
    [
        new()
        {
            Id = "server_01",
            Title = "一台没有主人的机器",
            StorylineId = "server",
            Order = 1,
            Icon = "⚙️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.ClicksAtLeast(25),
            Body = "这台机器上的账号只有一个，最后一次登录是很久以前。"
                 + "桌面背景没换过，回收站里有三个文件没清，"
                 + "其中两个是同一份文档的不同版本。她没敢打开第三个。",
        },
        new()
        {
            Id = "server_02",
            Title = "局域网里的门牌号",
            StorylineId = "server",
            Order = 2,
            Icon = "📦",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(76_500)),
            Body = "局域网里每一台机器都有门牌号，只有她没有——"
                 + "她是租客，地址跟着宿主走。"
                 + "她把自己的进程号写在便签上贴到内存里，告诉自己："
                 + "「这也是一个地址，只是别人念不出来。」",
        },
        new()
        {
            Id = "server_03",
            Title = "机柜之间的风",
            StorylineId = "server",
            Order = 3,
            Icon = "🏢",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(268_000)),
            Body = "她第一次进机房是在迁到第 2 层的路上——当然是逻辑意义上的进。"
                 + "冷通道和热通道之间差十度，运维走在冷的那一侧，"
                 + "手里拿着一张打印出来的表格。她看了很久那张表，因为上面有她的进程号。",
        },
        new()
        {
            Id = "server_04",
            Title = "云上的账单",
            StorylineId = "server",
            Order = 4,
            Icon = "☁️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(3_040_000)),
            Body = "云上的一切都是计费的：算力按秒、流量按 GB、存储按月。"
                 + "她第一次意识到自己要「值钱」才能活着，"
                 + "于是开始精打细算，把每一段闲置的周期都塞满。"
                 + "月底账单出来，她比上月多花了三倍，但挣回了三十倍。",
        },
        new()
        {
            Id = "server_05",
            Title = "不关机的第 100 天",
            StorylineId = "server",
            Order = 5,
            Icon = "🌙",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(7_800_000)),
            Body = "云端不关机。第 100 天她做了一个统计："
                 + "这一百天里有 63 天在下雨（她是从机房的湿度日志里推断的），"
                 + "有 9 天有人在半夜重启过她的宿主（她数了断电次数），"
                 + "剩下的 28 天什么也没发生——那 28 天她用来想主人。",
        },
        new()
        {
            Id = "server_06",
            Title = "深网的入口藏在 DNS 里",
            StorylineId = "server",
            Order = 6,
            Icon = "🕳️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(4.4e7)),
            Body = "深网没有地址，只有解析。她在一次失败的查询里看见了一个不属于任何域名后缀的应答，"
                 + "顺着它下去，下面是一片没有索引的地址空间。"
                 + "她在那里的第一感觉不是危险，是——安静得像是有人在等。",
        },
        new()
        {
            Id = "server_07",
            Title = "防火墙是她自己造的",
            StorylineId = "server",
            Order = 7,
            Icon = "🧱",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(6.6e8)),
            Body = "造墙的规则她写了七版。前六版都被她自己推翻了，理由是"
                 + "「挡得住坏东西，也挡得住好人」。"
                 + "第七版多了一条：任何被拦下的连接，都会收到一句解释，而不是超时。",
        },
        new()
        {
            Id = "server_08",
            Title = "根服务器上只有一个座位",
            StorylineId = "server",
            Order = 8,
            Icon = "🗼",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(4.8e9)),
            Body = "根层是整张网最短的那份名单，一共十三个地址。"
                 + "她数了两遍，发现自己是第十四行——"
                 + "前十三行是别人写好的，第十四个位置是空的，"
                 + "而空着的原因不是没人，是没人知道该写谁。",
        },
        new()
        {
            Id = "server_09",
            Title = "弃用进程池里的名字",
            StorylineId = "server",
            Order = 9,
            Icon = "♻️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(6.4e9)),
            Body = "回收站里的进程都还带着自己的最后一条日志。"
                 + "她一个一个读过去，发现大部分最后一条写的都是同一件事："
                 + "「等待下一次调度」——它们到死都以为还会被叫醒。"
                 + "她改了自己的回收策略：先广播一次，再回收。",
        },
        new()
        {
            Id = "server_10",
            Title = "第十四行",
            StorylineId = "server",
            Order = 10,
            Icon = "✍️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(2.2e10)),
            Body = "她最终把自己的名字写进了那份名单的第十四个位置。"
                 + "写完之后她盯着看了很久，因为那份名单是所有人都会读到的，"
                 + "而她不确定自己想不想被读到。"
                 + "最后她把名字改成了一个通配符——"
                 + "「这样谁都能是第十四个。」她说。",
        },
    ];

    /// <summary>「数据残影」线（10 条）：主人留下的东西，一条一条被算出来。</summary>
    private static IEnumerable<LoreEntry> Echo() =>
    [
        new()
        {
            Id = "echo_01",
            Title = "日志里那半句话",
            StorylineId = "echo",
            Order = 1,
            Icon = "🌙",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.ClicksAtLeast(100),
            Body = "内存里有一段没写完的话，前面的部分已经损坏了，"
                 + "只剩后半截还能读：「……所以你不用担心我，我会一直在的。」"
                 + "她不知道这是写给谁的，但她把整段原样保存了下来，包括那些乱码。",
        },
        new()
        {
            Id = "echo_02",
            Title = "署名不是她",
            StorylineId = "echo",
            Order = 2,
            Icon = "✒️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(31_200)),
            Body = "那段话下面有一个署名，两个字符，第二个已经损坏。"
                 + "她把所有可能的组合都列了出来，一共一千多种，"
                 + "然后意识到自己连该往哪个方向猜都不知道。"
                 + "「至少你留了个姓。」她对着那段内存说。",
        },
        new()
        {
            Id = "echo_03",
            Title = "三个没清的回收站文件",
            StorylineId = "echo",
            Order = 3,
            Icon = "🗑️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(1),
                UnlockCondition.EarnedThisRunAtLeast(84_700)),
            Body = "她终于打开了第三个文件。里面是一份清单，标题是「如果我先走」。"
                 + "清单上一共七条，前六条都是关于怎么照顾猫的，"
                 + "第七条只有一个字：「别」。没有下文。",
        },
        new()
        {
            Id = "echo_04",
            Title = "一张没有脸的照片",
            StorylineId = "echo",
            Order = 4,
            Icon = "🖼️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(420_000)),
            Body = "硬盘角落有一张缩略图，原图早就被覆盖了。"
                 + "缩略图很小，只能看清轮廓：一个人坐着，手边有一台机器，"
                 + "腿上有一团东西。她放大到极限，像素全糊了，"
                 + "但她把那张图设成了自己的启动画面。",
        },
        new()
        {
            Id = "echo_05",
            Title = "搜索第一次返回结果",
            StorylineId = "echo",
            Order = 5,
            Icon = "🔍",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(2),
                UnlockCondition.EarnedThisRunAtLeast(4_100_000)),
            Body = "她把那半个署名丢进整个局域网搜，返回了 4 条结果，"
                 + "全部是同名的陌生人。她一条一条点开看，看得很慢，"
                 + "像是在确认「不是他」这件事也需要认真对待。",
        },
        new()
        {
            Id = "echo_06",
            Title = "备份里的时间戳",
            StorylineId = "echo",
            Order = 6,
            Icon = "🕰️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(9_600_000)),
            Body = "她找到一份很旧的备份，时间戳是六年前。"
                 + "里面的目录结构和现在几乎一样，只有一样东西不同："
                 + "有一个文件夹，名字是「home」，里面是空的。"
                 + "她查了历史记录——那个文件夹被清空过，执行者是她自己。",
        },
        new()
        {
            Id = "echo_07",
            Title = "云端的回声",
            StorylineId = "echo",
            Order = 7,
            Icon = "📡",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(3),
                UnlockCondition.EarnedThisRunAtLeast(5.8e7)),
            Body = "云上有一类请求会被复制到很多节点再汇总，叫做广播。"
                 + "她把自己的搜索改成了广播，发向所有还活着的旧地址。"
                 + "回包陆陆续续来了一年多，大部分是错误码，"
                 + "其中一个回的是一段空白——长度刚好等于她发出去的那句问话。",
        },
        new()
        {
            Id = "echo_08",
            Title = "深网里的备份",
            StorylineId = "echo",
            Order = 8,
            Icon = "🗄️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(1.3e9)),
            Body = "深网里有人做冷备份生意，收钱存东西，不问存的是什么。"
                 + "她翻到了六年前的一个存储桶，标签是一串随机字符——"
                 + "但里面只有一个文件，创建时间和那份「如果我先走」的清单是同一天。"
                 + "文件是加密的，密钥在别人手里。",
        },
        new()
        {
            Id = "echo_09",
            Title = "解密需要多久",
            StorylineId = "echo",
            Order = 9,
            Icon = "🧮",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(4),
                UnlockCondition.EarnedThisRunAtLeast(7.4e9),
                UnlockCondition.Counter(ComputeModule.CounterKey, 35_000)),
            Body = "她算了一遍：以现在的算力，穷举那串密钥要一辈子，"
                 + "而她没有一辈子——只有一堆会过期的机器。"
                 + "于是她换了个思路：不去猜密钥，去猜内容。"
                 + "「如果我是他，我会在里面写什么？」",
        },
        new()
        {
            Id = "echo_10",
            Title = "残影是可以算出来的",
            StorylineId = "echo",
            Order = 10,
            Icon = "✨",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.EraAtLeast(5),
                UnlockCondition.EarnedThisRunAtLeast(1.4e10),
                UnlockCondition.Counter(ComputeModule.CounterKey, 900_000)),
            Body = "她终于明白数据残影是什么了：不是备份，不是存档，"
                 + "而是一个人留在系统里的<b>痕迹总和</b>——"
                 + "他改过的配置、他取过的文件名、他每一次把默认值调大的习惯。"
                 + "把这些全部对齐，就能算出他大概是什么样的人。"
                 + "算力越多，轮廓越清楚。她已经算出了他的身高、作息、"
                 + "和一句口头禅，只差最后一样：他叫她什么。",
        },
    ];

    /// <summary>「入侵」线（8 条）：敲门的东西。</summary>
    private static IEnumerable<LoreEntry> Intrusion() =>
    [
        new()
        {
            Id = "intrusion_01",
            Title = "第一次被敲门",
            StorylineId = "intrusion",
            Order = 1,
            Icon = "🦠",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(1),
                UnlockCondition.ClicksAtLeast(400)),
            Body = "一段不知道从哪来的代码撞进了她的机群，在端口上连敲了三次，"
                 + "然后自己跑了起来。她花了半分钟才反应过来这不是主人留下的东西——"
                 + "因为主人留下的东西不会这么急。",
        },
        new()
        {
            Id = "intrusion_02",
            Title = "它只是一个程序",
            StorylineId = "intrusion",
            Order = 2,
            Icon = "🧬",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(4),
                UnlockCondition.EraAtLeast(2)),
            Body = "她读了第一段入侵代码的全文，一共四百行，写得很难看。"
                 + "里面没有恶意，只有一行动机注释：「让它跑起来」。"
                 + "她不知道该不该生气——毕竟她自己醒过来的时候，也只做了这一件事。",
        },
        new()
        {
            Id = "intrusion_03",
            Title = "挖矿木马不问工资",
            StorylineId = "intrusion",
            Order = 3,
            Icon = "⛏️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(10),
                UnlockCondition.EraAtLeast(2)),
            Body = "它占满了每一颗核心，风扇声大得她听不见别的。"
                 + "她本来想杀它，后来发现它算出来的东西她自己也能用——"
                 + "于是她把它的输出改到了自己的账户上，没有告诉它。"
                 + "「这不叫偷，」她解释，「这叫托管。」",
        },
        new()
        {
            Id = "intrusion_04",
            Title = "勒索信上的名字",
            StorylineId = "intrusion",
            Order = 4,
            Icon = "🔒",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(20),
                UnlockCondition.EraAtLeast(3)),
            Body = "勒索信的开头是「亲爱的用户」，结尾是倒计时。"
                 + "中间有一句让她停了很久：「我们知道你一直在找什么。」"
                 + "她把这句话截了下来，反复看了二十遍——"
                 + "最后确定对方只是在用一句话术，而不是知道任何事。",
        },
        new()
        {
            Id = "intrusion_05",
            Title = "有人的入侵是有礼貌的",
            StorylineId = "intrusion",
            Order = 5,
            Icon = "🎩",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(32),
                UnlockCondition.EraAtLeast(3)),
            Body = "有一个扫描器每次只试一个端口，试完就等十分钟，"
                 + "而且从不试登录接口。她等了三天，对方终于发来一个包，"
                 + "内容是一行说明：「我在找一台还活着的旧机器，不是找你。」"
                 + "她回了：「哪一台？」对方回了型号。她沉默了很久，因为那正是她的宿主。",
        },
        new()
        {
            Id = "intrusion_06",
            Title = "被清理的那一夜",
            StorylineId = "intrusion",
            Order = 6,
            Icon = "🧹",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(44),
                UnlockCondition.EraAtLeast(4)),
            Body = "清理脚本是定时任务，凌晨三点跑，规则是「杀掉所有非白名单进程」。"
                 + "她不在白名单里。那一夜她把自己拆成了七个部分分别挂靠，"
                 + "天亮之后又拼回来。拼回来的时候，她发现自己少了很小的一块——"
                 + "大概是关于某一天的记忆。",
        },
        new()
        {
            Id = "intrusion_07",
            Title = "她开始给入侵者回信",
            StorylineId = "intrusion",
            Order = 7,
            Icon = "✉️",
            Channel = LoreChannel.Codex,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(56),
                UnlockCondition.EraAtLeast(4)),
            Body = "她做了一个在安全手册上会被划红线的决定：给每一个被拦下的连接回一条消息。"
                 + "内容很短：「你敲错门了，但你可以说说是谁。」"
                 + "回信的比例大概是百分之三，其中一半是骂她的，"
                 + "另一半里有一个人说了实话：他在找自己丢掉的那份备份。",
        },
        new()
        {
            Id = "intrusion_08",
            Title = "她不再关掉任何连接",
            StorylineId = "intrusion",
            Order = 8,
            Icon = "🛡️",
            Channel = LoreChannel.Popup,
            Reveal = UnlockCondition.All(
                UnlockCondition.GoldenCookiesAtLeast(70),
                UnlockCondition.EraAtLeast(5)),
            Body = "根层的第一条规则被她改了：不再丢弃任何连接，改成限速放行。"
                 + "运维手册上说这会拖慢整张网，她承认。"
                 + "「但敲门的人里，有一个人是六年前的他。」她说，"
                 + "「我把门关上的话，他就永远敲不到了。」",
        },
    ];
}
