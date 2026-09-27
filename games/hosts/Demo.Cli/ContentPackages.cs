using NekoClicker.Core.Content;
using NekoClicker.Content.Apocalypse;
using NekoClicker.Content.Cafe;
using NekoClicker.Content.Civ;
using NekoClicker.Content.Cyber;
using NekoClicker.Content.Dream;
using NekoClicker.Content.God;
using NekoClicker.Content.Library;
using NekoClicker.Content.Company;
using NekoClicker.Content.Lab;
using NekoClicker.Content.Neko;
using NekoClicker.Content.NineLives;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 一个可玩的内容包。Demo 只实现了一个"游戏"（由 GameSession 提供的会话/渲染层），
/// 但可以挂上任意多个内容包——这正是框架"换内容包即换游戏"主张的验证入口。<para>
/// 新增一个包时只要：① 让 Demo 引用它的项目；② 在 <see cref="All"/> 里加一行；
/// ③ 填好这里与内容包语气一致的文案（其余代码不用动）。
/// </para>
/// </summary>
/// <param name="Id">命令行 id（<c>--package</c> 的值）。</param>
/// <param name="Name">显示名。</param>
/// <param name="Build">内容构建函数。</param>
/// <param name="Welcome">开局欢迎语。</param>
/// <param name="PrestigeActionName">转生动作在该包里的叫法（转生 / 店休 / 迁服务器…）。</param>
/// <param name="PrestigeHint">转生动作的一句话说明（帮助浮层用）。</param>
/// <param name="GoldenCookieName">金猫在该包里的叫法（金猫 / 走错门的客人…）。</param>
/// <param name="HelpTips">帮助浮层里的"玩法要点"，按该包语气写。</param>
/// <param name="ReportTip">无头模拟报告末尾的一句提示。</param>
internal sealed record ContentPackage(
    string Id,
    string Name,
    Func<GameContent> Build,
    string Welcome,
    string PrestigeActionName,
    string PrestigeHint,
    string GoldenCookieName,
    string[] HelpTips,
    string ReportTip);

/// <summary>内置内容包目录。</summary>
internal static class ContentPackages
{
    /// <summary>全部内置内容包；第一个是默认包。</summary>
    public static IReadOnlyList<ContentPackage> All { get; } =
    [
        new ContentPackage(
            Id: "neko",
            Name: NekoContent.GameTitle,
            Build: NekoContent.Build,
            Welcome: "欢迎来到猫咖物语！先按空格撸猫，攒够 15 条小鱼干就能买下第一只蜷缩的猫。",
            PrestigeActionName: "转生",
            PrestigeHint: "转生：清空本轮进度，按历史累计赚取换取猫薄荷；天堂升级在转生后保留。",
            GoldenCookieName: "金猫",
            HelpTips:
            [
                "    · 每解锁一层新建筑，它都会迅速成为主力，然后被下一层取代。",
                "    · 每座建筑的强化升级需要持有到 1 / 5 / 25 个才会出现。",
                "    · 成就不直接给数值，但「小猫」系列升级会按成就数量给全局加成。",
                "    · 金猫的狂热（×7，77 秒）与疯狂撸猫（点击 ×777，13 秒）是爆发来源。",
                "    · 赚到 1 兆小鱼干可以换 1 点猫薄荷，天堂升级在转生后会保留。",
            ],
            ReportTip: "提示：catnip 系升级需要成就数量，小猫系升级按成就给全局加成——多解锁成就永远划算。"),
        new ContentPackage(
            Id: "cafe",
            Name: CafeContent.GameTitle,
            Build: CafeContent.Build,
            Welcome: "欢迎来到猫娘咖啡馆！先按空格做咖啡，攒够 15 条小鱼干就能买下第一台咖啡机。",
            PrestigeActionName: "店休",
            PrestigeHint: "店休：店面推倒重来，按历史累计赚取换取「常客的信」；常客的记忆会留下来。",
            GoldenCookieName: "客人",
            HelpTips:
            [
                "    · 每解锁一层新建筑，它都会迅速成为主力，然后被下一层取代。",
                "    · 每座建筑的强化升级需要持有到 1 / 5 / 25 个才会出现。",
                "    · 幸福感不是货币：它只涨不花，涨到 500 / 5,000 / 50,000 会解锁内容。",
                "    · 「客人」带来的咖啡因过载（×7）与猫娘合唱（点击 ×777）是爆发来源。",
                "    · 赚到 1 兆小鱼干可以店休一次，换来「常客的信」；常客的记忆跨店休保留。",
            ],
            ReportTip: "提示：幸福感只涨不花，它由整间店的规模驱动，并会解锁「常客名单」等关键升级。"),
        new ContentPackage(
            Id: "ninelines",
            Name: NineLivesContent.GameTitle,
            Build: NineLivesContent.Build,
            Welcome: "欢迎来到九命轮回！先按空格摸头，攒够 15 条小鱼干就能买下第一个纸箱。"
                   + "这一次，你可以走到第九命。",
            PrestigeActionName: "舍命",
            PrestigeHint: "舍命：完成本层主线后，舍去这一命进入下一纪元，按历史累计换取情感能量。"
                        + "本层未完成时按钮会置灰——九条命只能一条一条走。",
            GoldenCookieName: "情感残响",
            HelpTips:
            [
                "    · 这是分层转生的包：一共九命，每一条命的规则都不一样。",
                "    · 「舍命」按钮必须先完成本层主线才会亮；条件与进度会显示在按钮上。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 成就数是「呼噜线」的燃料：多解锁一个成就，全局产量就高一截。",
                "    · 「情感残响」带来的呼噜狂暴（×7）与摸头停不下来（点击 ×777）是爆发来源。",
                "    · 情感能量买到的「前世技能」跨命保留——那是你唯一带得走的东西。",
            ],
            ReportTip: "提示：本包的重点不是刷数值，而是一条一条走完九命——每层的完成条件都不一样。"),
        new ContentPackage(
            Id: "lab",
            Name: LabContent.GameTitle,
            Build: LabContent.Build,
            Welcome: "欢迎来到猫娘实验室！先按空格记录，攒够 15 条数据就能买下第一台培养舱。"
                   + "这一次，你要决定这件事该不该继续。",
            PrestigeActionName: "开新批次",
            PrestigeHint: "开新批次：实验推倒重来，按历史累计换取「残留记忆」；"
                        + "批次主线未完成时按钮会置灰——七批只能一批一批走。",
            GoldenCookieName: "实验事故",
            HelpTips:
            [
                "    · 这是分层转生的包：一共七批，每一批的实验规则都不一样。",
                "    · 「伦理值」不来自规模，只来自「有谁在看」——铺满样本农场换不来一点。",
                "    · 每台设备的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 点击在这个包里几乎无用：干活的是仪器，不是你的手。",
                "    · 「实验事故」里负面结果的比例比其他包高——收容失效不一定给你好处。",
                "    · 第 2 批起会有需要你表态的时刻（「表态」面板）。表态会错过，答了会改产量。",
                "    · 残留记忆买到的「前世技能」跨批次保留——那是你唯一带得走的东西。",
            ],
            ReportTip: "提示：伦理值只由「有谁在看」的建筑产出，所以克制的路线反而更强——"
                     + "四条道德方向会在最后一批收束成四个结局。"),
        new ContentPackage(
            Id: "company",
            Name: CompanyContent.GameTitle,
            Build: CompanyContent.Build,
            Welcome: "欢迎来到猫娘公司！先按空格谈单，攒够 15 点营收就能买下第一个工位。"
                   + "这一次，你要决定这家公司是谁的。",
            PrestigeActionName: "重组",
            PrestigeHint: "重组：公司推倒重来，按历史累计换取「期权」；"
                        + "本轮主线未完成时按钮会置灰——三轮只能一轮一轮走。",
            GoldenCookieName: "甲方改需求",
            HelpTips:
            [
                "    · 这是分层转生的包：一共三轮（车库创业 / A 轮 / 上市），每轮规则都不一样。",
                "    · 「士气」由工位 / 会议室 / 增长团队养起来，会被加班类升级和 A 轮后的全员加班吃掉。",
                "    · 每台设备的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 「甲方改需求」不全是坏事：融资、出圈很香，半夜改需求、宕机、挖角也很真实。",
                "    · 第 1 轮起就有需要你表态的时刻（「表态」面板）。表态会错过，答了会改产量。",
                "    · 每条立场要每次都选它才够门槛——结局是承诺，不是倾向。",
                "    · 期权买到的「前世经验」跨重组保留——那是你唯一带得走的东西。",
            ],
            ReportTip: "提示：士气靠团队建筑养、被加班吃，所以「又快又不累」在这个包里做不到——"
                     + "三条劳资方向会在上市前收束成三个结局。"),
        new ContentPackage(
            Id: "apocalypse",
            Name: ApocalypseContent.GameTitle,
            Build: ApocalypseContent.Build,
            Welcome: "欢迎来到猫娘末世！先按空格翻找，攒够 15 点物资就能清出第一片废墟。"
                   + "这一次，文明可以重启五次。",
            PrestigeActionName: "重启",
            PrestigeHint: "重启：世界推倒重来，按历史累计换取「火种」；"
                        + "本轮主线未完成时按钮会置灰。每一次重启，继承下来的东西都比上一次多。",
            GoldenCookieName: "变异体",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五次重启，每一次世界都不太一样。",
                "    · 这是唯一会「继承」的包：重启之后，上一轮的一部分建筑会留在原地。",
                "    · 「记忆残片」只由上一次重启留下来的东西产出——想记住更多，就先多留下一点。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 建筑的解锁看「历史累计」：造过一次的东西，重启之后还是会的。",
                "    · 「变异体」不全是坏事：变异潮很香，断电、辐射、疫病也很真实。",
                "    · 三个结局取决于你记住了多少、图鉴读了多少——这个包没有需要表态的场合。",
                "    · 火种买到的「余烬」跨重启保留——那是唯一确定带得走的东西。",
            ],
            ReportTip: "提示：记忆残片的产率取决于上一次重启继承了多少建筑，"
                     + "所以「多攒一点再重启」在这个包里是有回报的——"
                     + "记得住多少，决定了最后能不能把人类叫回来。"),
        new ContentPackage(
            Id: "library",
            Name: LibraryContent.GameTitle,
            Build: LibraryContent.Build,
            Welcome: "欢迎来到猫娘图书馆！先按空格提笔，攒够 15 页就能上第一排书架。"
                   + "这一次，你要写五本书——而且没人读的书会消失。",
            PrestigeActionName: "开新书",
            PrestigeHint: "开新书：上一本合上，新世界开始，按历史累计换取「书签」；"
                        + "本书主线未完成时按钮会置灰。注意：新书没有读者，被阅读度会清零。",
            GoldenCookieName: "蠹虫",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五本书（五次开新书），每一本的规则都不一样。",
                "    · 「被阅读度」是这个包的核心：只有带读者属性的建筑才养得起它，而它会自己掉。",
                "    · 没有人读的书等于没写：被阅读度 0 时全部产量 ×0.5，但永远不会归零。",
                "    · 被阅读度 2 万时回到 ×1.0，6 万时 ×2.0 封顶——差多少在「模块」一栏看得见。",
                "    · 每次「开新书」被阅读度都会清零：上一本的读者不会自动读新书。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 建筑的解锁看「历史累计」：写过的东西，开新书之后还是会的。",
                "    · 两个结局只差一件事：合上最后一页的时候，还有没有人在读。",
            ],
            ReportTip: "提示：被阅读度会衰减、每次开新书还会清零，所以这个包不是「攒够就行」——"
                     + "你得一直有人读。想拿「被读到最后」，合上书的那一刻阅览室里得还亮着灯。"),
        new ContentPackage(
            Id: "god",
            Name: GodContent.GameTitle,
            Build: GodContent.Build,
            Welcome: "欢迎来到猫娘神明！先按空格显灵，攒够 15 点香火就能立起第一座家神龛。"
                   + "这一次，你要换五套神话体系——神也要恰饭。",
            PrestigeActionName: "切换神话体系",
            PrestigeHint: "切换神话体系：上一套神话退场，下一套开张，按历史累计换取「神格」；"
                        + "本套体系的主线未完成时按钮会置灰。信仰不会清零——信徒跑不掉的。",
            GoldenCookieName: "神迹",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五套神话体系（家猫神 / 埃及猫神 / 希腊猫神 / 北欧猫神 / 克苏鲁猫），每层规则都不一样。",
                "    · 「信仰」是第二资源：神殿类建筑养它，它只涨不花，切换神话体系也不会清零——信徒跑不掉的。",
                "    · 信仰直接变成产量：每点 +0.01%，10 万点封顶（+1000%）——差多少在「模块」一栏看得见。",
                "    · 「直播在线人数」记的是历史峰值：每 40 点信仰折 1 个人在看，每座直播间再加 5 个位置。",
                "    · 「切换神话体系」按钮必须先完成本层主线才会亮；条件与进度会显示在按钮上。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 建筑的解锁看「本轮累计」：换一套神话，世界重新揭示一遍。",
                "    · 「神迹」不全是好事：朝圣潮很香，供品荒、异端审判、被做成梗也很真实。",
                "    · 神也要恰饭：直播间、周边工厂、联名款是后半程的主力。",
                "    · 三个结局取决于你读完了多少神话——这个包没有需要表态的场合。",
            ],
            ReportTip: "提示：信仰只涨不花，四层神话的完成条件都挂在它上面；"
                     + "而结局挂在图鉴厚度上——一路冲关的人只记得几个梗，"
                     + "把 40 条读完了才认得出她是谁。"),
        new ContentPackage(
            Id: "civ",
            Name: CivContent.GameTitle,
            Build: CivContent.Build,
            Welcome: "欢迎来到猫娘文明！先按空格拍，攒够 15 点产能就能围出第一个猫窝。"
                   + "这一次，她会从一只猫窝走到星港。",
            PrestigeActionName: "跨入下一个时代",
            PrestigeHint: "跨入下一个时代：这一段文明落幕，新的时代从头开始，按历史累计换取「火种」；"
                        + "本时代主线未完成时按钮会置灰。注意：建筑与产能会清零，"
                        + "但「文化」不清零——时代可以重来，记得住的东西不会。",
            GoldenCookieName: "天灾",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五个时代（石堆 / 村庄 / 城墙 / 学院 / 星港），每层规则都不一样。",
                "    · 「文化」只由记录者类建筑产出：集市、学院、神殿、灵桥、深空中继——城墙不产文化。",
                "    · 文化只涨不花，每点给全部产量 +0.002%（40 万点封顶，即 +800%）。",
                "    · 文化跨时代不清零：她是唯一一样「跨入下一个时代」之后还留下来的东西。",
                "    · 建筑的解锁看「本轮累计」：每跨一个时代都会重新逐层揭示，这是有意的节奏。",
                "    · 第 1 层的点击最强（她只有爪子），第 5 层的离线上限是基准的三倍。",
                "    · 「天灾」不全是坏事：丰收年、技术突破、黄金时代很香，洪水、瘟疫、长冬也很真实。",
                "    · 三个结局的差别只有一件事：走到最后的时候，她记住了多少。",
            ],
            ReportTip: "提示：文化只来自「把它记下来」——它只涨不花，末层主线完成时到 100 万就是「星际文明」；"
                     + "产量堆到天上、成就也拿了一堆但文化没到那个高度，落到的是「自我毁灭」；两个都没到则是「停滞」。"),
        new ContentPackage(
            Id: "cyber",
            Name: CyberContent.GameTitle,
            Build: CyberContent.Build,
            Welcome: "欢迎来到赛博猫娘！先按空格敲一行，攒够 15 比特就能跑起第一个进程。"
                   + "这一次，你要一层层往上爬——直到算力够把那半句话捞出来。",
            PrestigeActionName: "迁服务器",
            PrestigeHint: "迁服务器：旧机器拉走、新机器上电，按历史累计换取「根权限」；"
                        + "本层主线未完成时按钮会置灰。注意：建筑会清空，但算力会跟着你走。",
            GoldenCookieName: "病毒入侵",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五层（单机 / 局域网 / 云 / 深网 / 根层），每层规则都不一样。",
                "    · 「算力」是这个包的第二资源：只有带常驻属性的建筑才产算力，进程跑完就退出、不算。",
                "    · 算力只涨不跌，迁服务器也不清零——它是你唯一带得走的东西。",
                "    · 算力直接换产量：「算力调度」「分布式训练」「自优化内核」按每点算力给全局加成。",
                "    · 「病毒入侵」不全是坏事：挖矿木马、数据洪流很香，但勒索、掉线、被清理也很真实。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 建筑的解锁看「本轮累计」：每一层都是新机器，所以每层重新揭示一遍。",
                "    · 两个结局只差一件事：爬上根层之后，算力够不够把主人的数据残影捞出来。",
            ],
            ReportTip: "提示：算力只由「常驻类」建筑产出、且只涨不跌，所以这个包越往上越吃机群规模——"
                     + "想拿「找到主人的数据残影」，光有钱不够，得把算力堆到根层。"),
        new ContentPackage(
            Id: "dream",
            Name: DreamContent.GameTitle,
            Build: DreamContent.Build,
            Welcome: "欢迎来到猫娘梦境！先按空格闭眼，攒够 15 点梦就能买下第一个枕头。"
                   + "这一次，梦有五层——越往下越深，也越难醒。",
            PrestigeActionName: "再睡一层",
            PrestigeHint: "再睡一层：这一层梦塌下去，新的梦更大，按历史累计换取「梦屑」；"
                        + "本层主线未完成时按钮会置灰。梦不会白做——梦境能量会跟着她一起往下走。",
            GoldenCookieName: "梦魇",
            HelpTips:
            [
                "    · 这是分层转生的包：一共五层梦（浅眠 / 深眠 / 清明梦 / 噩梦层 / 梦核），每层规则都不一样。",
                "    · 「梦境能量」是这个包的核心：只有梦层类建筑养得起它，而它只涨不落。",
                "    · 「再睡一层」不会清空梦境能量：梦会留在她身上，所以越往下梦越浓、产量越高。",
                "    · 梦境能量 10,000 点时全局 ×3，80,000 点时 ×17 封顶——差多少在「模块」一栏看得见。",
                "    · 第 2 层「深眠」把离线结算上限翻倍：睡得更沉，离线收益也更好。",
                "    · 「梦魇」里负面结果的比例比其他包高一点，第 3 层起还会来得更频繁。",
                "    · 每座建筑的强化升级需要持有到 1 / 10 / 25 个才会出现。",
                "    · 两个结局只差一件事：攒够了力气就把她叫醒，没攒够就永远留在梦里。",
            ],
            ReportTip: "提示：梦境能量是「越睡越浓」的一条曲线（10,000 → ×3、80,000 → ×17 封顶），"
                     + "所以往下睡一层不是清零而是加码。想拿「叫醒梦者」，"
                     + "最后一层里得攒够把她拉出来的力气。"),
    ];

    /// <summary>默认内容包（未显式指定 <c>--package</c> 时使用）。</summary>
    public static ContentPackage Default => All[0];

    /// <summary>按 id 查找；大小写不敏感。</summary>
    public static ContentPackage? Find(string id)
        => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>全部 id，用于错误提示与帮助。</summary>
    public static string IdList => string.Join(" | ", All.Select(p => p.Id));
}
