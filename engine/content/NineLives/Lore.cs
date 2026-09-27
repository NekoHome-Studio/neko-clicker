using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 叙事条目：50 条，分四条线。<para>
/// 这个包的叙事节奏天然由<b>纪元</b>驱动——每条线都跟着"第几次醒来"逐段揭开，
/// 于是九命本身就是叙事的分卷。开场 10 分钟内只会放出 3 条。
/// </para>
/// <para>
/// <b>节奏规则（每条都别破坏）</b>：
/// <list type="number">
///   <item>除开篇外，每条都用 <c>All(EraAtLeast(n), 层内里程碑)</c>。</item>
///   <item>里程碑一律取该层完成门槛的固定百分比（10%~96%），且<b>全包唯一</b>。</item>
///   <item>同一条线内，门槛必须随 <see cref="LoreEntry.Order"/> 单调递增。</item>
///   <item>每一层至多一个弹窗。</item>
/// </list>
/// </para>
/// <para>
/// 之所以能靠"纪元门槛 + 层内百分比"根除"进层瞬间炸一堆"：<c>EarnedThisRunAtLeast</c>
/// 在舍命进层时<b>归零</b>（见 <c>Eras.cs</c> 顶部说明），所以纪元门槛一成立，
/// 层内里程碑必定是从 0 重新往上爬——进层那一刻，本层没有任何条目够条件。
/// 而里程碑数值全包互不相同，于是任一瞬间至多释放一条。
/// </para>
/// <para>
/// 反过来说，<b>层内里程碑的百分比不能超过该层的完成门槛</b>，否则玩家会在够条件前就舍命走人，
/// 这条剧情就永远读不到了（门槛越高越危险）。percent 一律 ≤ 100。
/// </para>
/// </summary>
internal static class Lore
{
    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        new()
        {
            Id = "nine",
            Name = "九命",
            Theme = "主线：她一次次醒来，一次次忘了自己是谁；四种可能的尽头。",
            Icon = "🌙",
            TotalEntries = 20,
        },
        new()
        {
            Id = "god",
            Name = "猫神寐娅",
            Theme = "那位把自己切成九份的神，和她的理由。",
            Icon = "👁️",
            TotalEntries = 12,
        },
        new()
        {
            Id = "ruin",
            Name = "人类遗毒",
            Theme = "人类为什么消失；他们留下了什么。",
            Icon = "🏚️",
            TotalEntries = 10,
        },
        new()
        {
            Id = "instinct",
            Name = "猫的本能",
            Theme = "在九个世界的间隙里，她只想晒太阳。",
            Icon = "🐾",
            TotalEntries = 8,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. NineLine(),
        .. GodLine(),
        .. RuinLine(),
        .. InstinctLine(),
    ];

    // ---------------------------------------------------------------- 主线：九命

    private static IEnumerable<LoreEntry> NineLine()
    {
        yield return Popup("nine_01", 1, "第一次睁眼",
            "先有纸箱，然后有光，然后有她。她做的第一件事是确认自己有几条腿。",
            UnlockCondition.ClicksAtLeast(1));

        yield return Log("nine_02", 2, "她数不清自己",
            "你问她叫什么。她想了很久，说：“我有好几个名字，但它们都不太对。”"
            + "她说完看了一眼门口那块空招牌，没再说话。",
            Era(1, 6e4));

        yield return Log("nine_03", 3, "尾巴有九节",
            "你以为那是花纹。后来发现每一节都会在某个时刻轻微地亮一下，像是计数。",
            Era(2, 1.5e6));

        yield return Log("nine_04", 4, "第一次死亡",
            "她死的时候没有声音，只是变淡了一点。第二天早上，同一个位置又坐着一只猫。",
            Era(2, 6.5e6));

        yield return Popup("nine_05", 5, "第二次醒来",
            "她记得上一世的最后一秒。她不记得上一世的其余部分——她说那更像“听说过”。",
            Era(3, 1.5e7));

        yield return Log("nine_06", 6, "记忆是有重量的",
            "这一世她走路慢了一点。她说：“东西太多了。”你问什么东西，她指了指脑袋。",
            Era(3, 4.8e7));

        yield return Log("nine_07", 7, "她开始给自己留字条",
            "在吧台下面、窗缝里、猫爬架顶端，到处是字条。第一张写着：“别相信第三次。”",
            Era(3, 8.2e7));

        yield return Codex("nine_08", 8, "九节尾巴的含义",
            "九节不是花纹。那是九次机会，用掉一次，就少一节。她已经数不清还剩几节。",
            Era(4, 4e7));

        yield return Popup("nine_09", 9, "第四世的同一个梦",
            "每一世她都会梦到同一件事：一个人蹲下来，伸出手。她从来没看清那个人的脸。",
            Era(4, 1.2e8));

        yield return Log("nine_10", 10, "她试着不忘记",
            "这一世她决定记住一切。到第三天她开始头痛，到第七天她开始忘记自己头痛过。",
            Era(5, 6.6e7));

        yield return Log("nine_11", 11, "有人替她记",
            "她开始把记忆存在别处——存在杯子里、存在歌里、存在一只猫的呼噜频率里。",
            Era(5, 1.38e8));

        yield return Log("nine_12", 12, "第五世她笑了一次",
            "毫无理由地。她说：“刚才那一秒，我好像什么都想起来了。”然后那一秒就过去了。",
            Era(5, 2.1e8));

        yield return Popup("nine_13", 13, "她问你是不是同一个人",
            "“每一次陪我的都是你吗？”你答不上来。她说：“没关系，我也不是同一个我。”",
            Era(6, 1.65e8));

        yield return Log("nine_14", 14, "最后一节尾巴",
            "她摸了摸最后那节。它已经不亮了。她说：“这次用完，就没有下一个我了。”",
            Era(7, 1.82e8));

        yield return Codex("nine_15", 15, "九次之后",
            "九次机会，九个世界，九种规则。她终于明白：每一次醒来，都是为了把一件事做完。",
            Era(8, 2e8));

        yield return Popup("nine_16", 16, "第九次醒来",
            "这一次她睁眼的时候，先看的是书。她已经知道自己在哪一页了。",
            Era(9, 2.7e8));

        // ---- 四条终局伏笔。机制留到系统 C（选择 + 立场轴），但路先铺好：
        // 每一条都写明"她试过"，于是玩家在结局时看到的是一个已经想过很多次的选项，
        // 而不是四个突然冒出来的按钮。
        yield return Codex("nine_17", 17, "成神那条路",
            "她试过把自己拼回一份，接上那个名字剩下的笔画。拼到一半停了："
            + "拼回去的那个，不会记得纸箱。",
            Era(9, 5.7e8));

        yield return Codex("nine_18", 18, "变人那条路",
            "她试着用两条腿走路，试着把想说的话说完。第三个月她发现自己数不清窗外的鸟了，"
            + "就不学了。",
            Era(9, 8.7e8));

        yield return Codex("nine_19", 19, "永为猫那条路",
            "留下最后一节不点亮，就可以一直换下去。这个世界会一直有阳光和纸箱——"
            + "只是每一世都得重新认识你一次。",
            Era(9, 1.17e9));

        yield return Popup("nine_20", 20, "破轮回那条路",
            "把九节一起点亮，然后什么都不做。钟停了。她坐在那里，第一次不知道接下来会发生什么——"
            + "她说这是她想要很久的东西。",
            Era(9, 1.44e9));
    }

    // ---------------------------------------------------------------- 支线：猫神寐娅

    private static IEnumerable<LoreEntry> GodLine()
    {
        yield return Log("god_01", 1, "神也会打盹",
            "神殿里那尊像一直在睡。管理员说这是写实的——她本来就是睡着的时候创世的。",
            UnlockCondition.ClicksAtLeast(25));

        yield return Log("god_02", 2, "她把名字拆开了",
            "寐娅原本是一个很长的名字。她把自己拆成九份的时候，顺手把名字也拆了。",
            Era(2, 3.2e6));

        yield return Codex("god_03", 3, "为什么要切九份",
            "因为一份装不下。人类留下的幸福感太多，一个身体扛不住，所以要九个。",
            Era(3, 3.2e7));

        yield return Popup("god_04", 4, "神的第一句话",
            "她创世时说的第一句话是“别怕”。这句话被切成了九段，每一世她只能想起一小截。",
            Era(4, 8e7));

        yield return Log("god_05", 5, "神的第二句话",
            "没人听过第二句。有人说那是一个名字，有人说是句骂人的话。她笑而不答。",
            Era(5, 3e7));

        yield return Log("god_06", 6, "她也是被留下的",
            "寐娅不是主动成神的。她只是最后一个还在意的人，所以只剩她还能做点什么。",
            Era(5, 1.74e8));

        yield return Codex("god_07", 7, "神殿里的空位",
            "主位旁边有九个空位。管理员说原来摆着九尊小像，后来都自己走掉了。",
            Era(6, 7.5e7));

        yield return Log("god_08", 8, "神不回应祈祷",
            "不是不听。是她睡着的时候，只能听见“谢谢”——而她一直在等的就是这两个字。",
            Era(6, 3.4e8));

        yield return Popup("god_09", 9, "第一次显灵",
            "香灰浮起来，在半空写了一个字，又散了。她盯着那个位置看了很久，没说话。",
            Era(7, 8.4e7));

        yield return Log("god_10", 10, "神的缺点",
            "她偏心。九个世界里她最爱的是第一个，因为它最笨、最容易心软、最像她自己。"
            + "——第一个世界，就是那家招牌上缺一个字的店。",
            Era(7, 4.76e8));

        yield return Log("god_11", 11, "她在看直播",
            "后台数据显示，有一个观众从头到尾没发过一条弹幕，但每一场都在。"
            + "她看的不是直播，是那家店的后门。",
            Era(8, 4.5e8));

        yield return Popup("god_12", 12, "她要醒了吗",
            "神殿的地面在轻轻震动。管理员说这不是地震，是有人在很深的地方翻了个身。",
            Era(9, 4.2e8));
    }

    // ---------------------------------------------------------------- 支线：人类遗毒

    private static IEnumerable<LoreEntry> RuinLine()
    {
        yield return Log("ruin_01", 1, "废墟里没有风",
            "原因不明。所有去过那片废墟的人都说：里面连空气都是停着的。",
            Era(2, 4.8e6));

        yield return Codex("ruin_02", 2, "大静默",
            "官方记录里那件事只有一个编号。编号后面跟着一句话：“自愿停止。”",
            Era(3, 6.5e7));

        yield return Log("ruin_03", 3, "他们把幸福存起来了",
            "人类在消失之前做了一件事：把所有人的“满足感”抽出来，装进了容器。容器就是猫。",
            Era(4, 1.6e8));

        yield return Log("ruin_04", 4, "上传的排队号",
            "服务器里还存着排队名单。排在最前面的那个编号，前面写着“寐”。",
            Era(5, 1.02e8));

        yield return Popup("ruin_05", 5, "金库的第一层",
            "打开第一层的时候，里面装的全是照片。每一张都是一个蹲下来伸手的人。",
            Era(6, 2.5e8));

        yield return Log("ruin_06", 6, "照片背面",
            "背面写着同样的一行字：“对不起，我先走一步。”笔迹有几百种。",
            Era(7, 2.8e8));

        yield return Log("ruin_07", 7, "他们知道自己错了",
            "检索记录里有一份没提交的提案，标题是《关于限制吸猫总量的建议》。",
            Era(7, 5.74e8));

        yield return Codex("ruin_08", 8, "遗毒的意思",
            "不是恶意。是他们留下的东西太好用，好用到不需要再自己做任何事。",
            Era(8, 7e8));

        yield return Log("ruin_09", 9, "最后一条日志",
            "日志的最后一句话是：“如果有人看到这里——不要恨我们，我们只是太累了。”",
            Era(9, 1.2e8));

        yield return Popup("ruin_10", 10, "门的那一边",
            "你把金库最后一层打开。里面是空的，只有一张椅子，和窗外的草地。",
            Era(9, 1.32e9));
    }

    // ---------------------------------------------------------------- 支线：猫的本能

    private static IEnumerable<LoreEntry> InstinctLine()
    {
        yield return Log("instinct_01", 1, "九个世界里最好的那块地板",
            "她花了三辈子找它。找到了，在储藏间靠窗的位置，下午三点会有阳光。"
            + "她说前一个世界也有一块一样的——她说就是这里。",
            UnlockCondition.ClicksAtLeast(120));

        yield return Log("instinct_02", 2, "纸箱永远比猫窝受欢迎",
            "哪怕猫窝贵五十倍。她解释过：“装得下我的才是家。”",
            Era(2, 8.2e6));

        yield return Log("instinct_03", 3, "推下桌子",
            "这一世的规矩是：任何放在桌子边缘的东西都必须被推下去。没有例外。",
            Era(2, 9.4e6));

        yield return Log("instinct_04", 4, "三点的困",
            "不管你正在做什么，下午三点她都会睡着。这不是懒，这是世界底层的心跳。",
            Era(4, 1.86e8));

        yield return Log("instinct_05", 5, "对激光笔的执念",
            "她知道那不是猎物。但她还是会扑。她说：“总要有一件事是不需要理由的。”",
            Era(5, 2.46e8));

        yield return Log("instinct_06", 6, "呼噜声的作用",
            "呼噜不只是产出的声音。她说那是她在确认自己还在这儿。",
            Era(6, 4.25e8));

        yield return Log("instinct_07", 7, "不喜欢被抱",
            "九世都是。但如果是你，她会忍三秒——三秒之后必须挣脱，这是原则问题。",
            Era(7, 6.51e8));

        yield return Log("instinct_08", 8, "她终究是一只猫",
            "走过九个世界、见过人类灭绝、差点成神之后，她最喜欢的还是纸箱和一个会摸头的人。",
            Era(9, 1.02e9));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 纪元门槛 + 层内里程碑。<paramref name="milestone"/> 必须落在该层完成条件之下
    /// （否则玩家会在够条件前舍命走人，这条剧情就永远读不到），且全包唯一。
    /// </summary>
    private static UnlockCondition Era(int era, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(era),
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
        "nine" => "🌙",
        "god" => "👁️",
        "ruin" => "🏚️",
        "instinct" => "🐾",
        _ => "📖",
    };
}
