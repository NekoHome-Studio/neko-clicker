using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 叙事条目：46 条，分四条线。<para>
/// 这个包的叙事节奏天然由<b>纪元</b>驱动——每条线都跟着"第几次醒来"逐段揭开，
/// 于是九命本身就是叙事的分卷。开场 10 分钟内只会放出 3 条。
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
            Theme = "主线：她一次次醒来，一次次忘了自己是谁。",
            Icon = "🌙",
            TotalEntries = 16,
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
            "你问她叫什么。她想了很久，说：'我有好几个名字，但它们都不太对。'",
            UnlockCondition.EarnedThisRunAtLeast(1e7));

        yield return Log("nine_03", 3, "尾巴有九节",
            "你以为那是花纹。后来发现每一节都会在某个时刻轻微地亮一下，像是计数。",
            UnlockCondition.TotalBuildingsAtLeast(120));

        yield return Log("nine_04", 4, "第一次死亡",
            "她死的时候没有声音，只是变淡了一点。第二天早上，同一个位置又坐着一只猫。",
            UnlockCondition.EraAtLeast(2));

        yield return Popup("nine_05", 5, "第二次醒来",
            "她记得上一世的最后一秒。她不记得上一世的其余部分——她说那更像'听说过'。",
            UnlockCondition.EraAtLeast(2));

        yield return Log("nine_06", 6, "记忆是有重量的",
            "这一世她走路慢了一点。她说：'东西太多了。'你问什么东西，她指了指脑袋。",
            UnlockCondition.EraAtLeast(3));

        yield return Log("nine_07", 7, "她开始给自己留字条",
            "在吧台下面、窗缝里、猫爬架顶端，到处是字条。第一张写着：'别相信第三次。'",
            UnlockCondition.EraAtLeast(3));

        yield return Codex("nine_08", 8, "九节尾巴的含义",
            "九节不是花纹。那是九次机会，用掉一次，就少一节。她已经数不清还剩几节。",
            UnlockCondition.EraAtLeast(4));

        yield return Popup("nine_09", 9, "第四世的同一个梦",
            "每一世她都会梦到同一件事：一个人蹲下来，伸出手。她从来没看清那个人的脸。",
            UnlockCondition.EraAtLeast(4));

        yield return Log("nine_10", 10, "她试着不忘记",
            "这一世她决定记住一切。到第三天她开始头痛，到第七天她开始忘记自己头痛过。",
            UnlockCondition.EraAtLeast(5));

        yield return Log("nine_11", 11, "有人替她记",
            "她开始把记忆存在别处——存在杯子里、存在歌里、存在一只猫的呼噜频率里。",
            UnlockCondition.EraAtLeast(5));

        yield return Log("nine_12", 12, "第五世她笑了一次",
            "毫无理由地。她说：'刚才那一秒，我好像什么都想起来了。'然后那一秒就过去了。",
            UnlockCondition.EraAtLeast(6));

        yield return Popup("nine_13", 13, "她问你是不是同一个人",
            "'每一次陪我的都是你吗？'你答不上来。她说：'没关系，我也不是同一个我。'",
            UnlockCondition.EraAtLeast(6));

        yield return Log("nine_14", 14, "最后一节尾巴",
            "她摸了摸最后那节。它已经不亮了。她说：'这次用完，就没有下一个我了。'",
            UnlockCondition.EraAtLeast(8));

        yield return Codex("nine_15", 15, "九次之后",
            "九次机会，九个世界，九种规则。她终于明白：每一次醒来，都是为了把一件事做完。",
            UnlockCondition.EraAtLeast(8));

        yield return Popup("nine_16", 16, "第九次醒来",
            "这一次她睁眼的时候，先看的是书。她已经知道自己在哪一页了。",
            UnlockCondition.All(
                UnlockCondition.EraAtLeast(9),
                UnlockCondition.EarnedThisRunAtLeast(1e9)));
    }

    // ---------------------------------------------------------------- 支线：猫神寐娅

    private static IEnumerable<LoreEntry> GodLine()
    {
        yield return Log("god_01", 1, "神也会打盹",
            "神殿里那尊像一直在睡。管理员说这是写实的——她本来就是睡着的时候创世的。",
            UnlockCondition.ClicksAtLeast(25));

        yield return Log("god_02", 2, "她把名字拆开了",
            "寐娅原本是一个很长的名字。她把自己拆成九份的时候，顺手把名字也拆了。",
            UnlockCondition.EarnedThisRunAtLeast(5e9));

        yield return Codex("god_03", 3, "为什么要切九份",
            "因为一份装不下。人类留下的幸福感太多，一个身体扛不住，所以要九个。",
            UnlockCondition.EarnedThisRunAtLeast(1e10));

        yield return Popup("god_04", 4, "神的第一句话",
            "她创世时说的第一句话是'别怕'。这句话被切成了九段，每一世她只能想起一小截。",
            UnlockCondition.EraAtLeast(3));

        yield return Log("god_05", 5, "神的第二句话",
            "没人听过第二句。有人说那是一个名字，有人说是句骂人的话。她笑而不答。",
            UnlockCondition.EraAtLeast(4));

        yield return Log("god_06", 6, "她也是被留下的",
            "寐娅不是主动成神的。她只是最后一个还在意的人，所以只剩她还能做点什么。",
            UnlockCondition.EraAtLeast(5));

        yield return Codex("god_07", 7, "神殿里的空位",
            "主位旁边有九个空位。管理员说原来摆着九尊小像，后来都自己走掉了。",
            UnlockCondition.EraAtLeast(5));

        yield return Log("god_08", 8, "神不回应祈祷",
            "不是不听。是她睡着的时候，只能听见'谢谢'——而她一直在等的就是这两个字。",
            UnlockCondition.EraAtLeast(6));

        yield return Popup("god_09", 9, "第一次显灵",
            "香灰浮起来，在半空写了一个字，又散了。她盯着那个位置看了很久，没说话。",
            UnlockCondition.EraAtLeast(6));

        yield return Log("god_10", 10, "神的缺点",
            "她偏心。九个世界里她最爱的是第一个，因为它最笨、最容易心软、最像她自己。",
            UnlockCondition.EraAtLeast(7));

        yield return Log("god_11", 11, "她在看直播",
            "后台数据显示，有一个观众从头到尾没发过一条弹幕，但每一场都在。",
            UnlockCondition.EraAtLeast(8));

        yield return Popup("god_12", 12, "她要醒了吗",
            "神殿的地面在轻轻震动。管理员说这不是地震，是有人在很深的地方翻了个身。",
            UnlockCondition.EraAtLeast(9));
    }

    // ---------------------------------------------------------------- 支线：人类遗毒

    private static IEnumerable<LoreEntry> RuinLine()
    {
        yield return Log("ruin_01", 1, "废墟里没有风",
            "原因不明。所有去过那片废墟的人都说：里面连空气都是停着的。",
            UnlockCondition.EarnedThisRunAtLeast(5e9));

        yield return Codex("ruin_02", 2, "大静默",
            "官方记录里那件事只有一个编号。编号后面跟着一句话：'自愿停止。'",
            UnlockCondition.EarnedThisRunAtLeast(1e8));

        yield return Log("ruin_03", 3, "他们把幸福存起来了",
            "人类在消失之前做了一件事：把所有人的'满足感'抽出来，装进了容器。容器就是猫。",
            UnlockCondition.EraAtLeast(3));

        yield return Log("ruin_04", 4, "上传的排队号",
            "服务器里还存着排队名单。排在最前面的那个编号，前面写着'寐'。",
            UnlockCondition.EarnedThisRunAtLeast(1e9));

        yield return Popup("ruin_05", 5, "金库的第一层",
            "打开第一层的时候，里面装的全是照片。每一张都是一个蹲下来伸手的人。",
            UnlockCondition.EraAtLeast(5));

        yield return Log("ruin_06", 6, "照片背面",
            "背面写着同样的一行字：'对不起，我先走一步。'笔迹有几百种。",
            UnlockCondition.EraAtLeast(5));

        yield return Log("ruin_07", 7, "他们知道自己错了",
            "检索记录里有一份没提交的提案，标题是《关于限制吸猫总量的建议》。",
            UnlockCondition.EraAtLeast(6));

        yield return Codex("ruin_08", 8, "遗毒的意思",
            "不是恶意。是他们留下的东西太好用，好用到不需要再自己做任何事。",
            UnlockCondition.EraAtLeast(7));

        yield return Log("ruin_09", 9, "最后一条日志",
            "日志的最后一句话是：'如果有人看到这里——不要恨我们，我们只是太累了。'",
            UnlockCondition.EraAtLeast(8));

        yield return Popup("ruin_10", 10, "门的那一边",
            "你把金库最后一层打开。里面是空的，只有一张椅子，和窗外的草地。",
            UnlockCondition.EraAtLeast(9));
    }

    // ---------------------------------------------------------------- 支线：猫的本能

    private static IEnumerable<LoreEntry> InstinctLine()
    {
        yield return Log("instinct_01", 1, "九个世界里最好的那块地板",
            "她花了三辈子找它。找到了，在储藏间靠窗的位置，下午三点会有阳光。",
            UnlockCondition.ClicksAtLeast(100));

        yield return Log("instinct_02", 2, "纸箱永远比猫窝受欢迎",
            "哪怕猫窝贵五十倍。她解释过：'装得下我的才是家。'",
            UnlockCondition.EarnedThisRunAtLeast(2e8));

        yield return Log("instinct_03", 3, "推下桌子",
            "这一世的规矩是：任何放在桌子边缘的东西都必须被推下去。没有例外。",
            UnlockCondition.TotalBuildingsAtLeast(150));

        yield return Log("instinct_04", 4, "三点的困",
            "不管你正在做什么，下午三点她都会睡着。这不是懒，这是世界底层的心跳。",
            UnlockCondition.EarnedThisRunAtLeast(1e10));

        yield return Log("instinct_05", 5, "对激光笔的执念",
            "她知道那不是猎物。但她还是会扑。她说：'总要有一件事是不需要理由的。'",
            UnlockCondition.ClicksAtLeast(2_000));

        yield return Log("instinct_06", 6, "呼噜声的作用",
            "呼噜不只是产出的声音。她说那是她在确认自己还在这儿。",
            UnlockCondition.EarnedThisRunAtLeast(1e10));

        yield return Log("instinct_07", 7, "不喜欢被抱",
            "九世都是。但如果是你，她会忍三秒——三秒之后必须挣脱，这是原则问题。",
            UnlockCondition.All(
                UnlockCondition.ClicksAtLeast(10_000),
                UnlockCondition.EraAtLeast(4)));

        yield return Log("instinct_08", 8, "她终究是一只猫",
            "走过九个世界、见过人类灭绝、差点成神之后，她最喜欢的还是纸箱和一个会摸头的人。",
            UnlockCondition.AchievementsAtLeast(30));
    }

    // ---------------------------------------------------------------- 辅助

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
