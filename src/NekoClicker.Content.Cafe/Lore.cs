using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 叙事条目：51 条，分三条线。<para>
/// 节奏是刻意编排的——开场 10 分钟内只会放出 3 条，其余按进度慢慢渗出来。
/// 世界观不该一次讲完，否则玩家在还没建立起"这家店有点不对"的感觉之前
/// 就已经被告知了全部真相。
/// </para>
/// <para>
/// <b>条件编排规则（三条，都别破坏）</b>：
/// <list type="number">
///   <item><b>任何两条的 <see cref="LoreEntry.Reveal"/> 必须不同。</b>同一个条件必然在同一瞬间
///   一起解锁——之前 50 条里有 23 条落在 10 个重复组里（最坏 3 条同时），
///   玩家看到的是"一口气弹出三段剧情"。下面每个数值都刻意错开，看着不圆整是故意的。</item>
///   <item><b>每条线用一条递增的"累计赚取"骨架定序。</b>建筑数 / 成就数 / 点击数的门槛
///   只是"顺手满足"的附加条件——它们一律在这个时间点<b>之前</b>就已经达到。
///   原因：成就与建筑类里程碑全在开局一小时内触发，而赚钱类要到几小时后，
///   把两者交替排列必然造成"后面的条目先亮、前面的还锁着"（实测 11 处倒挂）。</item>
///   <item><b>店休 / 转生类的条目只能放在线的尾部。</b>转生时机由玩家决定，
///   夹在中间就会因为玩家先攒钱后店休而倒挂。尾部内部再用转生等级递增定序。</item>
/// </list>
/// </para>
/// <para>
/// 条目在源码里的排列顺序即<b>阅读顺序</b>（<see cref="LoreEntry.Order"/>），
/// 所以 id 编号看起来是跳的——id 是稳定标识符，不承担顺序含义。
/// </para>
/// </summary>
internal static class Lore
{
    /// <summary>三条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        new()
        {
            Id = "door",
            Name = "两界之门",
            Theme = "咖啡馆为什么能通异世界；门越来越宽；结局。",
            Icon = "🚪",
            TotalEntries = 20,
        },
        new()
        {
            Id = "regular",
            Name = "常客们的记忆",
            Theme = "每个常客是一段人类记忆；他们为什么回来。",
            Icon = "🪑",
            TotalEntries = 16,
        },
        new()
        {
            Id = "supplier",
            Name = "异世界的供货商",
            Theme = "豆子从哪来；供货商的真实身份；这家店在九个世界里的位置。",
            Icon = "📦",
            TotalEntries = 15,
        },
    ];

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. DoorLine(),
        .. RegularLine(),
        .. SupplierLine(),
    ];

    // ---------------------------------------------------------------- 主线：两界之门

    private static IEnumerable<LoreEntry> DoorLine()
    {
        yield return Popup("door_01", 1, "门在厨房后面",
            "你以为是储藏间。推开门的时候，风是从另一边吹来的。",
            UnlockCondition.ClicksAtLeast(1));

        yield return Log("door_02", 2, "第一只自己走进来的猫",
            "她没有敲门。她只是坐在吧台上，等你把牛奶打完。",
            UnlockCondition.TotalBuildingsAtLeast(1));

        yield return Log("door_03", 3, "门缝里的光",
            "你数过：今天的门缝比昨天宽了一点。宽了大概一只猫的厚度。",
            UnlockCondition.TotalBuildingsAtLeast(5));

        yield return Log("door_04", 4, "她不说自己从哪来",
            "你问过一次。她指了指门，然后开始舔爪子——这是她表示“换个话题”的方式。",
            UnlockCondition.EarnedThisRunAtLeast(1e7));

        yield return Codex("door_05", 5, "第三棵树下",
            "门的另一边有一片没有边的草地。供货单上写的地址，就在那棵树的下面。",
            UnlockCondition.EarnedThisRunAtLeast(5e7));

        yield return Popup("door_06", 6, "门记住了你",
            "今天推门的时候，门没有发出声音。它认得你了——这不太像是好事。",
            UnlockCondition.EarnedThisRunAtLeast(2e9));

        yield return Log("door_07", 7, "打烊之后",
            "打烊后门还开着。她坐在门槛上，一半身子在这边，一半在那边。",
            UnlockCondition.EarnedThisRunAtLeast(4e9));

        yield return Log("door_08", 8, "两边的钟不一样快",
            "你在店里待了八小时，门外只过了一小时。她对此毫不意外。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e9),
                UnlockCondition.AchievementsAtLeast(8)));

        yield return Log("door_09", 9, "门开始往外漏东西",
            "昨天漏进来一片羽毛，今天是一张车票。票上的日期是三十年前。",
            UnlockCondition.EarnedThisRunAtLeast(1e10));

        yield return Log("door_10", 10, "第一块招牌",
            "你终于把招牌挂上了。她盯着看了很久，说：“这样它们就找得到了。”",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e10),
                UnlockCondition.TotalBuildingsAtLeast(150)));

        yield return Codex("door_11", 11, "她数过一次",
            "她说门那边有九个世界。你问第九个是什么样，她说她就是从那里来的。"
            + "她还说，那九个世界里，有一个正在看着这一个。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e10),
                UnlockCondition.AchievementsAtLeast(12)));

        yield return Log("door_12", 12, "门框在变粗",
            "新的门框比旧的重三倍。你请的木匠问：“这扇门到底通向哪儿？”你说不知道。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e10),
                UnlockCondition.TotalBuildingsAtLeast(180)));

        yield return Log("door_14", 13, "另一边的天气",
            "下雨的时候，门缝里会渗出水。水是温的，还有一点点甜。",
            UnlockCondition.EarnedThisRunAtLeast(1e11));

        yield return Log("door_15", 14, "门牌上的名字",
            "门牌上原本写着“储藏间”。某天早上它变成了一个你认不出的字。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e11),
                UnlockCondition.AchievementsAtLeast(14)));

        yield return Codex("door_16", 15, "两边的人",
            "你终于看清了：门外站着的不是猫，是很多年前走进这家店、然后再也没出去的人。",
            UnlockCondition.EarnedThisRunAtLeast(1.2e12));

        yield return Popup("door_18", 16, "整面墙都是门",
            "你拆掉一堵墙，发现墙后还是一扇门。这一扇更大，门缝里能看见星星。",
            UnlockCondition.EarnedThisRunAtLeast(4e12));

        yield return Log("door_19", 17, "她把钥匙给你",
            "一把很轻的钥匙。她说：“以后不用敲了。你本来就在这边。”",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.1e13),
                UnlockCondition.AchievementsAtLeast(20)));

        // ---- 店休 / 转生分支。放在末尾不是偷懒：转生时机由玩家决定，
        // 夹在赚钱类条目中间必然倒挂。尾部内部再按转生等级递增。
        yield return Popup("door_13", 18, "第一次店休",
            "你把灯关了。第二天回来，门还在，但她看你的眼神多了一点什么——像是确认。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e13),
                UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Log("door_17", 19, "门学会了等待",
            "你连续三天没来。第四天推门时，门自己开了——它一直在等。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.2e13),
                UnlockCondition.PrestigeLevelAtLeast(2)));

        yield return Popup("door_20", 20, "两界桥梁",
            "你把两张桌子搬到门的两侧，中间架了一块木板。客人从人这边走过来，"
            + "从猫那边走回去。没有人掉下去。桥就是这么造出来的。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e13),
                UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 支线：常客们的记忆

    private static IEnumerable<LoreEntry> RegularLine()
    {
        yield return Log("regular_01", 1, "周三的老先生",
            "他每周三来，坐同一张桌子，点同一杯。他说这里让他想起什么，但想不起来具体是什么。",
            UnlockCondition.EarnedThisRunAtLeast(2e7));

        yield return Log("regular_02", 2, "第二杯总是凉的",
            "他说第二杯不用热。你后来发现，他从来不喝第二杯——只是放在那儿看着。",
            UnlockCondition.EarnedThisRunAtLeast(1e8));

        yield return Log("regular_03", 3, "带小孩的母亲",
            "小孩一进门就直奔那只最懒的猫。母亲说：“他小时候也这样。”——她说的不是这只猫。",
            UnlockCondition.EarnedThisRunAtLeast(5e8));

        yield return Codex("regular_04", 4, "常客名单",
            "你在本子上记下他们的名字。写完你才发现，本子上的字迹有一半不是你写的。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(9e8),
                UnlockCondition.AchievementsAtLeast(9)));

        yield return Log("regular_06", 5, "每天换一种点法",
            "那位客人每次都点不同的东西，但每次都皱眉。他说他在找一个已经不存在的味道。",
            UnlockCondition.EarnedThisRunAtLeast(8e9));

        yield return Log("regular_07", 6, "她带着一本书来",
            "书里夹着一张照片。照片上的咖啡馆和这家一模一样，只是招牌上的字反着。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e10),
                UnlockCondition.AchievementsAtLeast(10)));

        yield return Codex("regular_08", 7, "空着的第四张桌",
            "第四张桌永远空着。她每天都会擦一遍，然后把糖罐摆正。你问过为什么，她说不为什么。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.5e10),
                UnlockCondition.TotalBuildingsAtLeast(160)));

        yield return Log("regular_09", 8, "熟客的暗号",
            "常客之间有个手势：把杯子转半圈。做这个动作的人，会被默默多加一份。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(4e10),
                UnlockCondition.TotalBuildingsAtLeast(200)));

        yield return Log("regular_10", 9, "他哭了",
            "有一次他喝完第一口就哭了。他说：“对不起，我只是很久没喝到热的了。”",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(7e10),
                UnlockCondition.ClicksAtLeast(1_000)));

        yield return Log("regular_12", 10, "有人留下了东西",
            "打烊后在第四张桌上发现一枚纽扣。你把它放进抽屉——里面已经有十几枚了。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.35e11),
                UnlockCondition.AchievementsAtLeast(13)));

        yield return Log("regular_13", 11, "门那边的家",
            "老先生说：“我家的厨房也有这个味道。”他说这话的时候，看着的是那扇门。",
            UnlockCondition.EarnedThisRunAtLeast(3e11));

        yield return Codex("regular_14", 12, "记忆的容量",
            "她解释过：一个人能记住的东西是有上限的。所以有些记忆被存到了别的地方。",
            UnlockCondition.EarnedThisRunAtLeast(1.6e12));

        yield return Log("regular_15", 13, "他们都记得",
            "今天所有人都到齐了。他们相互点头，像是认识很久——像是从同一个地方来的。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.5e12),
                UnlockCondition.AchievementsAtLeast(18)));

        // ---- 店休分支（同上：只能放尾部）。
        yield return Log("regular_05", 14, "他记得你的名字",
            "店休了一个月。推门进来的时候他说：“还开着啊。”——他记得你，所以你也还记得自己。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e12),
                UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Popup("regular_11", 15, "他们开始记得彼此",
            "周三的老先生和那位母亲第一次搭上话。他们聊了很久，然后同时安静下来。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.3e13),
                UnlockCondition.PrestigeLevelAtLeast(2)));

        yield return Popup("regular_16", 16, "本子写满了",
            "常客名单写到了最后一页。你翻回第一页，发现第一个名字是你自己的。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.6e13),
                UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 支线：异世界的供货商

    private static IEnumerable<LoreEntry> SupplierLine()
    {
        yield return Log("supplier_01", 1, "豆子是从门那边来的",
            "供货单上的地址写着“门的另一边，第三棵树下”。你决定不去确认。",
            UnlockCondition.EarnedThisRunAtLeast(7e7));

        yield return Codex("supplier_02", 2, "只收一种货币",
            "供货商不收钱。他收的是“别人说谢谢的次数”，每次记账都记得很认真。",
            UnlockCondition.EarnedThisRunAtLeast(2e8));

        yield return Log("supplier_03", 3, "箱子上的字",
            "每个箱子上都有一行小字。你拼了很久才认出：“给还没醒的那一个。”",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e8),
                UnlockCondition.TotalBuildingsAtLeast(170)));

        yield return Log("supplier_04", 4, "他从来不进门",
            "供货商把箱子放在门口就走。有一次你追出去，走廊是空的，箱子已经在了。",
            UnlockCondition.EarnedThisRunAtLeast(3e9));

        yield return Log("supplier_05", 5, "豆子会自己变味道",
            "同一批豆子，周一烘是苦的，周日烘是甜的。他说这不关他的事，是豆子自己的心情。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6.5e9),
                UnlockCondition.AchievementsAtLeast(9)));

        yield return Codex("supplier_06", 6, "账单背面",
            "账单背面画着一张地图。地图上标记了九个点，其中一个写着“你在这里”。"
            + "旁边还有一行小字：“第一个已经开张了。”",
            UnlockCondition.EarnedThisRunAtLeast(1.6e10));

        yield return Log("supplier_07", 7, "他也会累",
            "有一次他坐在门槛上，没有立刻走。他说：“送了很多年了。”然后就没再说别的。",
            UnlockCondition.EarnedThisRunAtLeast(2.2e10));

        yield return Log("supplier_08", 8, "缺货的那一周",
            "他失踪了七天。第八天箱子照常出现，里面的豆子比平时多了一倍，还附了一张纸条：抱歉。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3.5e10),
                UnlockCondition.TotalBuildingsAtLeast(220)));

        yield return Popup("supplier_09", 9, "他的脸",
            "这次他终于抬头了。你看见他的脸——很普通，普通到你第二天就想不起来。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e10),
                UnlockCondition.AchievementsAtLeast(15)));

        yield return Log("supplier_10", 10, "供货商的规矩",
            "他说过三条规矩：不问来处、不赊账、不让猫等。第三条他从来不解释。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.4e11),
                UnlockCondition.AchievementsAtLeast(16)));

        yield return Log("supplier_11", 11, "他也是一只猫",
            "你终于看清了箱底的字：“我也曾经被人摸过头。”落款是一个爪印。",
            UnlockCondition.EarnedThisRunAtLeast(1.8e12));

        yield return Codex("supplier_12", 12, "最后一批货",
            "他提前送了三个月的量。你说太多了，他说：“以后就送不了了。”",
            UnlockCondition.EarnedThisRunAtLeast(4.5e12));

        // ---- 店休分支 + 尾声。
        yield return Log("supplier_13", 13, "门口的脚印",
            "从那以后，门口每天早上都有一串湿脚印，通向门的方向，然后消失。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(8e12),
                UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Popup("supplier_14", 14, "现在是你在送",
            "今天早上，箱子上放着一张新的供货单——收件人地址写着“门的另一边，第三棵树下”。",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.8e13),
                UnlockCondition.PrestigeLevelAtLeast(2)));

        // 尾声：把这家店接回共享的神话体系。刻意放在全包最后——
        // 它不是"又一条供货商的怪癖"，而是"你一直以为自己在开店"的答案。
        yield return Popup("supplier_15", 15, "第一个世界",
            "你终于把那张地图摊平了。九个点，第一个被反复描过，纸都破了——"
            + "描的是一个你认得的位置：这里。图纸边角有一行极小的字，笔迹和账单一样："
            + "“我把他放在第一个，因为他最笨，最容易心软，最像我自己。”",
            UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.8e13),
                UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 辅助

    private static LoreEntry Log(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Log);

    private static LoreEntry Popup(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Popup);

    private static LoreEntry Codex(string id, int order, string title, string body, UnlockCondition reveal)
        => Make(id, order, title, body, reveal, LoreChannel.Codex);

    private static LoreEntry Make(
        string id, int order, string title, string body, UnlockCondition reveal, LoreChannel channel)
    {
        // id 前缀即剧情线：door_/regular_/supplier_ 与 Storylines 里的 id 对应。
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
        "door" => "🚪",
        "regular" => "🪑",
        "supplier" => "📦",
        _ => "📖",
    };
}
