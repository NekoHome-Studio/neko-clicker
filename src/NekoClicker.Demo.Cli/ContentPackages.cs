using NekoClicker.Core.Content;
using NekoClicker.Content.Apocalypse;
using NekoClicker.Content.Cafe;
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
    ];

    /// <summary>默认内容包（未显式指定 <c>--package</c> 时使用）。</summary>
    public static ContentPackage Default => All[0];

    /// <summary>按 id 查找；大小写不敏感。</summary>
    public static ContentPackage? Find(string id)
        => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>全部 id，用于错误提示与帮助。</summary>
    public static string IdList => string.Join(" | ", All.Select(p => p.Id));
}
