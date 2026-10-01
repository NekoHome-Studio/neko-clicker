using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>公司阶段</b>走——每重组一轮，故事往前推一段；开篇几条挂在很小的营收上，
/// 免得图鉴一开就是一整墙 ???。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1/#2/#3 相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现"第 3 条还锁着，第 4 条已亮"。</item>
///   <item>门槛一律 ≤ 该轮的完成门槛——否则玩家会在够条件前重组走人，这条永远读不到。</item>
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
            Theme = "主线：从 0002 号工牌，到招股书上的第一个名字。",
            Icon = "🐾",
            TotalEntries = 14,
        },
        new()
        {
            Id = "boss",
            Name = "你",
            Theme = "创始人：你签过的字，和你绕开的那些。",
            Icon = "🕴️",
            TotalEntries = 10,
        },
        new()
        {
            Id = "team",
            Name = "同事",
            Theme = "团队：加班餐、白板，和一份你不知道的章程。",
            Icon = "👥",
            TotalEntries = 8,
        },
        new()
        {
            Id = "ledger",
            Name = "账本",
            Theme = "钱：每一轮都更长的零，和每一页都更短的睡眠。",
            Icon = "📒",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. HerLine(),
        .. FounderLine(),
        .. TeamLine(),
        .. LedgerLine(),
    ];

    // ---------------------------------------------------------------- 主线：她

    private static IEnumerable<LoreEntry> HerLine()
    {
        yield return Log("her_01", 1, "工位上的猫",
            "第一个工位是靠窗的，她选了最里面那个。问她为什么，她说：「万一以后有人来，靠窗的留给别人。」",
            Round(1, 120));

        yield return Log("her_02", 2, "她记得每个人的口味",
            "公司只有三个人的时候，她把每个人的咖啡口味记在便签上。后来人多到记不住，她就把便签贴了一整面墙。",
            Round(1, 900));

        yield return Popup("her_03", 3, "第一次通宵",
            "订单赶完的那天早上，她趴在桌上睡着了。你给她披衣服的时候，看见她屏幕上有两个窗口：一个是交付文档，一个是领养页面。",
            Round(1, 6_000));

        yield return Log("her_04", 4, "她自己做的决定",
            "你出差回来，发现她把你没敢接的那个需求接了。她说：「你不在的时候，总得有人做决定。」",
            Round(1, 25_000));

        yield return Codex("her_05", 5, "第一张合影",
            "车库门口，三个人举着刚打出来的第一份合同。照片有点糊，但能看清她在笑。",
            Round(1, 70_000));

        yield return Log("her_06", 6, "新办公室的第一天",
            "搬进写字楼那天，她在自己的工位上坐了很久，说：「这里太亮了，我有点不敢摸鱼。」",
            Round(2, 3e5));

        yield return Log("her_07", 7, "她开始带人",
            "她第一次面试别人的时候，问的第一个问题是：「你介意加班吗？」问完自己先愣了一下。",
            Round(2, 3e6));

        yield return Popup("her_08", 8, "名单",
            "投资人的裁员名单上有她带出来的第一个人。她把名单拿给你，说：「你决定。但我想让你知道我看见了。」",
            Round(2, 1.5e7));

        yield return Codex("her_09", 9, "工牌编号",
            "她的工牌是 0002。有人问 0001 是谁，她说：「是那台咖啡机。」",
            Round(2, 4e7));

        yield return Log("her_10", 10, "她到点就走",
            "从某一天起，她到点就走。走之前会把第二天的清单写好，然后说：「我明天会做完的。」",
            Round(2, 7.5e7));

        yield return Popup("her_11", 11, "招股书上的名字",
            "招股书里要填核心团队成员。她把自己的名字排在最后，前面留了三个空行——那是车库时代的三个人。",
            Round(3, 1.3e8));

        yield return Log("her_12", 12, "她问你怕不怕",
            "路演前一天，她问你：「如果做大了，是不是就回不去了？」你说不知道。她点点头，好像这就够了。",
            Round(3, 2.2e8));

        yield return Log("her_13", 13, "一把钥匙",
            "她把办公室的备用钥匙还给你，说：「我现在不用这个了——我走正门。」",
            Round(3, 3.6e8));

        yield return Popup("her_14", 14, "敲钟前十分钟",
            "她在后台找到你，什么都没说，只是把手伸过来。你握了一下——那是这十年里你们唯一一次握手。",
            Round(3, 4.8e8));
    }

    // ---------------------------------------------------------------- 你

    private static IEnumerable<LoreEntry> FounderLine()
    {
        yield return Popup("boss_01", 1, "你签的第一份合同",
            "合同只有两页，你看了四遍。签字的时候手有点抖，她在旁边说：「没事，我第一次也这样。」",
            Round(1, 2_500));

        yield return Codex("boss_02", 2, "你学会说谎",
            "投资人问进度，你说了「快了」。挂掉电话之后，你在白板上把真实日期写了一遍，又擦掉。",
            Round(1, 40_000));

        yield return Log("boss_03", 3, "你的椅子",
            "从某天起，你不再坐在她旁边了。你的工位搬进单独的办公室，门是玻璃的，你说是为了透明。",
            Round(2, 1e6));

        yield return Log("boss_04", 4, "团建你没去",
            "那次团建你在谈下一轮融资。他们在山里给你发了一张照片，照片里每个人都比着同一个手势。",
            Round(2, 5e6));

        yield return Log("boss_05", 5, "你开始记不住名字",
            "新来的同事跟你打招呼，你只能笑着说「辛苦了」。那天晚上你把员工名单翻了一遍。",
            Round(2, 2e7));

        yield return Popup("boss_06", 6, "你替她做了决定",
            "她休假的时候，你签了那份她一直没签的文件。她回来后看了很久，最后只说：「下次等我。」",
            Round(2, 5.5e7));

        yield return Codex("boss_07", 7, "凌晨三点的办公室",
            "有一段时间你每天凌晨三点才走。后来你才知道，那段时间她也一样——只是你们谁都没碰见谁。",
            Round(2, 9e7));

        yield return Log("boss_08", 8, "拒绝收购的那天",
            "你拒绝了一个足够让所有人退休的报价。事后你在车里坐了半个小时，没告诉任何人。",
            Round(3, 1.6e8));

        yield return Log("boss_09", 9, "你第一次说「我们」",
            "记者问你公司是什么。你说：「我们是一群人，碰巧做成了点事。」说完你自己愣了一下。",
            Round(3, 3e8));

        yield return Popup("boss_10", 10, "钟声之前",
            "你站在台上，灯光很亮。你忽然想不起来第一笔订单的金额了——那个数字你曾经背得比自己生日还熟。",
            Round(3, 4.2e8));
    }

    // ---------------------------------------------------------------- 同事

    private static IEnumerable<LoreEntry> TeamLine()
    {
        yield return Log("team_01", 1, "第二个人",
            "第二个员工入职那天，她自己搬了张桌子进来，说：「我先坐这儿，等有位置了再挪。」",
            Round(1, 1_500));

        yield return Log("team_02", 2, "加班餐",
            "加班的人越来越多，行政开始统一订餐。订单备注里出现最多的三个字是「不要香菜」。",
            Round(1, 30_000));

        yield return Codex("team_03", 3, "匿名提问",
            "每月一次匿名提问，第一个月收到 4 条，第三个月收到 89 条。最多的一条问的是：「我们能不加班吗？」",
            Round(2, 2e6));

        yield return Log("team_04", 4, "有人离职",
            "第一个主动离职的人在告别邮件里写：「谢谢你们，但我想要周末。」HR 把这封邮件转发给了你。",
            Round(2, 9e6));

        yield return Log("team_05", 5, "茶水间的白板",
            "有人在茶水间放了块白板，写着「想说的都可以写」。第一周写满了，第二周被人擦干净了。",
            Round(2, 2.5e7));

        yield return Codex("team_06", 6, "工会筹备群",
            "一个你不知道的群里，47 个人在讨论章程。群公告只有一句：「别用公司电脑。」",
            Round(2, 6.5e7));

        yield return Popup("team_07", 7, "第一次谈判",
            "代表们坐在会议室里，对面是你和 HR。桌上摆着两份章程：一份是他们写的，一份是法务改的。",
            Round(3, 2e8));

        yield return Popup("team_08", 8, "投票结果",
            "投票在周五下午进行，结果贴在茶水间的白板上。有人拍照，有人没看，有人看了很久。",
            Round(3, 4.6e8));
    }

    // ---------------------------------------------------------------- 账本

    private static IEnumerable<LoreEntry> LedgerLine()
    {
        yield return Codex("ledger_01", 1, "第一笔支出",
            "账本第一页记的是三把椅子和一台二手显示器。第二页是咖啡豆，备注写着「不可省」。",
            Round(1, 8_000));

        yield return Log("ledger_02", 2, "第一次盈利",
            "那个月的账本第一次是正的。你把它打印出来，贴在了车库里。",
            Round(1, 50_000));

        yield return Codex("ledger_03", 3, "A 轮条款",
            "条款第 12 页写着「优先清算权」。法务说这是标准条款，所有人都签了。",
            Round(2, 5e5));

        yield return Log("ledger_04", 4, "烧钱率",
            "财务每月给你一张表：钱能撑多久。那张表上的数字从 18 变成 6，又回到 24。",
            Round(2, 4e6));

        yield return Codex("ledger_05", 5, "期权池",
            "期权池第一次被写进合同。HR 解释说这是「把未来分给大家」，有人问：「那现在呢？」",
            Round(2, 1e7));

        yield return Popup("ledger_06", 6, "收购报价单",
            "报价单上的数字后面有很多个零。你数了两遍才确认没数错。",
            Round(2, 8e7));

        yield return Log("ledger_07", 7, "招股书",
            "招股书有 400 页，你只反复看其中一页——风险提示里那句「本公司高度依赖核心人员」。",
            Round(3, 1.4e8));

        yield return Codex("ledger_08", 8, "敲钟那天的账",
            "敲钟当天的账本最后一行，是财务手写的一句：「今天的数字不代表明年。」",
            Round(3, 3.1e8));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 轮次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该轮的完成门槛，
    /// 且全包唯一（同条件的条目会同时解锁，是沉默失败）。
    /// </summary>
    private static UnlockCondition Round(int round, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(round),
            UnlockCondition.EarnedThisRunAtLeast(milestone));

    private static LoreEntry Popup(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Popup);

    private static LoreEntry Log(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Log);

    private static LoreEntry Codex(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Codex);

    private static LoreEntry Make(
        string id, int order, string title, string body, UnlockCondition reveal, LoreChannel channel)
    {
        int underscore = id.IndexOf('_');
        string storylineId = underscore > 0 ? id[..underscore] : id;

        return new LoreEntry
        {
            Id = id,
            Title = title,
            Body = body,
            Icon = IconFor(storylineId),
            StorylineId = storylineId,
            Order = order,
            Reveal = reveal,
            Channel = channel,
        };
    }

    private static string IconFor(string storylineId) => storylineId switch
    {
        "her" => "🐾",
        "boss" => "🕴️",
        "team" => "👥",
        "ledger" => "📒",
        _ => "📖",
    };
}
