using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>批次</b>走——每开一批，故事往前推一段。开场 10 分钟内只放 3 条。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1/#2 相同，都由测试或构建期守住）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现"第 3 条还锁着，第 4 条已亮"。</item>
///   <item>门槛一律取该批次完成门槛的固定百分比，且 ≤ 它——否则玩家会在够条件前舍命走人，这条永远读不到。</item>
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
            Id = "sample",
            Name = "样本",
            Theme = "主线：她的编号、她的名字、她记得的东西。",
            Icon = "🧪",
            TotalEntries = 14,
        },
        new()
        {
            Id = "researcher",
            Name = "研究员",
            Theme = "你签过的每一个字，以及你绕开的那些。",
            Icon = "🥼",
            TotalEntries = 10,
        },
        new()
        {
            Id = "board",
            Name = "委员会",
            Theme = "六把椅子，和它们愿意看见的东西。",
            Icon = "⚖️",
            TotalEntries = 8,
        },
        new()
        {
            Id = "data",
            Name = "数据",
            Theme = "数字不会说谎，但数字也不会说完。",
            Icon = "📊",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. SampleLine(),
        .. ResearcherLine(),
        .. BoardLine(),
        .. DataLine(),
    ];

    // ---------------------------------------------------------------- 主线：样本

    private static IEnumerable<LoreEntry> SampleLine()
    {
        yield return Popup("sample_01", 1, "样本 01",
            "培养舱的标签上写着「样本 01」。她学会认这三个字比学会走路还早。",
            Era(1, 2e4));

        yield return Log("sample_02", 2, "她数自己的爪子",
            "醒来后的第一件事是数爪子。数到第四只的时候停了一下，好像在确认什么。",
            Era(2, 1.2e6));

        yield return Log("sample_03", 3, "她开始在意玻璃",
            "以前她对着玻璃是照镜子。现在她会越过自己的倒影，去看后面有没有人。",
            Era(2, 2.6e6));

        yield return Codex("sample_04", 4, "编号与名字",
            "档案里她是 A-1。她给自己起的名字写在舱壁上，用爪子划的，第二天就被擦掉了。",
            Era(3, 1.2e7));

        yield return Log("sample_05", 5, "第一批的遗留",
            "基因库里有一份没有编号的样本，备注栏写着「不要打开」。没有人记得是谁写的。",
            Era(3, 2.6e7));

        yield return Popup("sample_06", 6, "第一次问为什么",
            "她问：「为什么是我？」记录仪还在转。你意识到这句话会被写进档案，永久保存。",
            Era(4, 3e7));

        yield return Log("sample_07", 7, "她学会了沉默",
            "问题变少了。不是因为她懂了，是因为她发现问了也没人回答。",
            Era(4, 7e7));

        yield return Log("sample_08", 8, "她给自己排了班",
            "没人教过她。她自己定了起床、吃饭、晒太阳的时间，然后严格执行。",
            Era(5, 5e7));

        yield return Codex("sample_09", 9, "记忆的接缝",
            "移植过的记忆会有接缝。她摸着自己后颈的位置，说：「这里有一条线。」",
            Era(5, 9e7));

        yield return Log("sample_10", 10, "她开始收集东西",
            "一颗螺丝、半张纸、一段断掉的线。她把这些藏在垫子底下，从不解释。",
            Era(6, 8e7));

        yield return Popup("sample_11", 11, "她记得上一批",
            "她说：「我做过这个梦。」而这是第七批——她不该有任何「上一批」。",
            Era(6, 1.5e8));

        yield return Log("sample_12", 12, "她在档案室待了一夜",
            "监控里她一整夜都在翻抽屉。第二天早上，最上面那层被擦干净了。",
            Era(7, 1.2e8));

        yield return Codex("sample_13", 13, "她找到了自己的抽屉",
            "抽屉里是空的，标签上写着「A-1」。她把自己的名字写了上去，然后合上。",
            Era(7, 3e8));

        yield return Popup("sample_14", 14, "她说她知道",
            "「我知道我是第几个。」她说这话的时候没有看你，「我只是不知道，下一个还是不是我。」",
            Era(7, 6e8));
    }

    // ---------------------------------------------------------------- 支线：研究员

    private static IEnumerable<LoreEntry> ResearcherLine()
    {
        yield return Log("researcher_01", 1, "入职第一天",
            "工牌上的照片是你，名字那栏是空白的。人事说系统还没录进去。",
            Era(1, 4.5e4));

        yield return Log("researcher_02", 2, "第一次签字",
            "你签的第一份文件是《样本处置授权》。流程比你想的简单得多。",
            Era(2, 4.1e6));

        yield return Codex("researcher_03", 3, "你绕开的那些",
            "走廊尽头有一扇门，所有新人都被提醒「不必进去」。你到现在也没进去过。",
            Era(2, 5.5e6));

        yield return Log("researcher_04", 4, "你的手很稳",
            "第一次采血的时候你的手一点都不抖。老同事说这是天赋，你不太确定这算不算。",
            Era(3, 4.1e7));

        yield return Popup("researcher_05", 5, "你开始记笔记",
            "不是为了存档。你只是想记住某一个具体的她，而不是第几批的第几只。",
            Era(3, 5.5e7));

        yield return Log("researcher_06", 6, "同事调走了",
            "他走的那天把所有资料整理得很整齐。三个月后你收到一张明信片，上面什么都没写。",
            Era(4, 1.1e8));

        yield return Log("researcher_07", 7, "你学会了不看她",
            "记录数据的时候只盯着屏幕。这个技巧花了你两年才练成。",
            Era(5, 1.4e8));

        yield return Codex("researcher_08", 8, "你的编号",
            "员工手册最后一页写着：研究员在档案里同样只有编号。你查过自己那一栏——也是空的。",
            Era(5, 1.9e8));

        yield return Log("researcher_09", 9, "你在门外站了很久",
            "观察室的门是自动的。你站在感应区外，它就永远不会开——这个设计你想了很久才明白是给谁用的。",
            Era(6, 2.4e8));

        yield return Popup("researcher_10", 10, "结题",
            "所有项目都会结题，包括这一项。你在最后一栏签字的时候，发现笔画比你记忆里抖。",
            Era(7, 4.5e8));
    }

    // ---------------------------------------------------------------- 支线：委员会

    private static IEnumerable<LoreEntry> BoardLine()
    {
        yield return Log("board_01", 1, "六把椅子",
            "委员会有六把椅子。开会时通常来两个人，其中一个是来签到的。",
            Era(2, 7e6));

        yield return Log("board_02", 2, "第一次有人提问",
            "「她疼吗？」这个问题让会议延长了四十分钟，最后写进了纪要的附注。",
            Era(3, 6.5e7));

        yield return Codex("board_03", 3, "章程第四版",
            "第四版删掉了「样本」这个词，换成「对象」。他们觉得这样更准确，也更温和。",
            Era(4, 1.5e8));

        yield return Popup("board_04", 4, "第五把椅子",
            "有人开始每次都来了。他坐在最边上，不说话，只是听。",
            Era(5, 1.9e8));

        yield return Log("board_05", 5, "表决",
            "第一次出现反对票。虽然只有一票，但纪要里必须记录——这是流程规定的。",
            Era(5, 2.3e8));

        yield return Log("board_06", 6, "预算与伦理",
            "一份内部文件写着：「伦理审查周期每延长一周，项目成本增加 3%。」数字是冷的。",
            Era(6, 3.2e8));

        yield return Log("board_07", 7, "外部审计",
            "来了一个不归这里管的人。他看完档案后问的第一个问题是：「她有自己的档案吗？」",
            Era(7, 7.5e8));

        yield return Popup("board_08", 8, "第六把椅子",
            "她坐进来那天，会议记录上第一次出现了「列席人员」以外的身份。没有人提议反对。",
            Era(7, 8.8e8));
    }

    // ---------------------------------------------------------------- 支线：数据

    private static IEnumerable<LoreEntry> DataLine()
    {
        yield return Log("data_01", 1, "表格里的空栏",
            "每一批的表格都有「备注」一栏。前三批全是空的，第四批开始有人写东西。",
            Era(1, 7e4));

        yield return Codex("data_02", 2, "单位的问题",
            "数据的单位是「条」。没有人定义过一条到底是多少——它只是一直在用。",
            Era(2, 8.8e6));

        yield return Log("data_03", 3, "异常值",
            "有一批数据被标了红。点开看，是她在无人时段的自发行为记录，时长一整夜。",
            Era(3, 8.8e7));

        yield return Log("data_04", 4, "曲线是漂亮的",
            "产量曲线光滑得不像真的。因为不光滑的部分被单独归档了。",
            Era(4, 1.9e8));

        yield return Log("data_05", 5, "伦理值是怎么算的",
            "伦理值不是一个指标，是一堆指标的加权和。权重是谁定的，档案里没有写。",
            Era(5, 2.6e8));

        yield return Codex("data_06", 6, "被删掉的那一列",
            "有一版表格里存在「意愿」这一列。它只活了两周，之后整列消失，连表头都没留。",
            Era(6, 3.9e8));

        yield return Log("data_07", 7, "归档的代价",
            "归档一份样本平均需要四个小时。七个批次加起来，是一个人不睡觉的一个月。",
            Era(6, 4.6e8));

        yield return Popup("data_08", 8, "最后一条记录",
            "档案的最后一行是空的，但序号已经打上去了。它留给下一条记录——如果有的话。",
            Era(7, 9.6e8));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 批次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该批次的完成门槛，
    /// 且全包唯一。
    /// </summary>
    private static UnlockCondition Era(int batch, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(batch),
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
        "sample" => "🧪",
        "researcher" => "🥼",
        "board" => "⚖️",
        "data" => "📊",
        _ => "📖",
    };
}
