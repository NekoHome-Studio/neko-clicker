using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 六次表态：第 1 轮两次、第 2 轮两次、第 3 轮两次。<para>
/// 每次两个互斥选项、各指向一条不同的立场、权重都是 2——三条立场各有 4 次机会，
/// 所以每条路都要求"每一次都选它"（门槛 7 点 &gt; 3 次 ×2）。与九命 / 实验室同一个配方：
/// <b>结局是承诺，不是倾向</b>。
/// </para>
/// <para>
/// <b>两次末轮表态刻意放在完成门槛之下</b>（2e8 / 4e8 &lt; 5e8），
/// 而结局要求"末层主线完成"。于是玩家一定先遇到选择、再走到终局判定——
/// 实验室包在这里踩过坑（见其终局修复），这个包从条件数值上就把顺序钉死。
/// </para>
/// <para>
/// 触发条件的两条纪律与其它包相同：里程碑必须 ≤ 该轮的完成门槛（<c>EraId</c> 是硬门），
/// 且数值要全包唯一。构建期会校验前者。
/// </para>
/// </summary>
internal static class Choices
{
    /// <summary>全部六次表态。</summary>
    public static ChoiceDefinition[] All =>
    [
        // ---- 第 1 轮 · 车库创业：第一笔订单，接还是不接。
        new()
        {
            Id = "choice_first_order",
            Speaker = "她",
            Prompt = "第一笔订单来了，对方要三天交付。她拿着排期表问你：「接吗？三天的话，我今晚得睡在公司。」",
            EraId = "garage",
            Trigger = Within(1, 2e4),
            Options =
            [
                new ChoiceOption
                {
                    Id = "order_take",
                    Label = "接，通宵也要交。",
                    OutcomeText = "她抱着睡袋进了会议室。第三天早上，订单交付了，她的黑眼圈成了公司最早的 Logo。",
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "order_negotiate",
                    Label = "不接，先谈条件。",
                    OutcomeText = "你回了封邮件：「五天可以，三天不行。」对方沉默了半天，答应了。她长出一口气。",
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第 1 轮 · 车库创业：账上还剩多少钱。
        new()
        {
            Id = "choice_runway",
            Speaker = "合伙人",
            Prompt = "账上还剩六个月的钱。合伙人把两份预算推到你面前：「全投进增长，还是留一半？」",
            EraId = "garage",
            Trigger = Within(1, 6e4),
            Options =
            [
                new ChoiceOption
                {
                    Id = "runway_all_in",
                    Label = "全投增长。",
                    OutcomeText = "预算表上只剩一行字：「要么做大，要么没有。」",
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "runway_reserve",
                    Label = "留一半当遣散费。",
                    OutcomeText = "你在表格最后加了一栏「退路」。没人提它，但所有人都看见了。",
                    StanceId = Stances.Liquidate,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第 2 轮 · A 轮：投资人要你裁员。
        new()
        {
            Id = "choice_layoff",
            Speaker = "投资人",
            Prompt = "投资人把一份名单推过来：「把团队砍掉一半，数据会好看很多。你砍不砍？」",
            EraId = "series_a",
            Trigger = Within(2, 3e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "layoff_yes",
                    Label = "砍。数据优先。",
                    OutcomeText = "名单上的人当天下午就收拾好了东西。会议室空了一半，报表好看了不少。",
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
                new ChoiceOption
                {
                    Id = "layoff_no",
                    Label = "不砍。",
                    OutcomeText = "你把名单推了回去。投资人的脸色不太好看，但那天晚上，办公室的灯一直亮着。",
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
            ],
        },

        // ---- 第 2 轮 · A 轮：有人要买下公司。
        new()
        {
            Id = "choice_acquisition",
            Speaker = "她自己",
            Prompt = "收购报价摆在桌上，够每个人分一笔。她问你：「卖了，还是自己做下去？」",
            EraId = "series_a",
            Trigger = Within(2, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "acquisition_sell",
                    Label = "卖掉，套现离场。",
                    OutcomeText = "交割那天大家在楼下合影，照片里每个人都笑得很轻松。",
                    StanceId = Stances.Liquidate,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "acquisition_hold",
                    Label = "不卖，自己敲钟。",
                    OutcomeText = "你在报价单背面写了一行字：「再等等。」然后把它塞进了抽屉。",
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.03)],
                },
            ],
        },

        // ---- 第 3 轮 · 上市：工会成立大会。
        new()
        {
            Id = "choice_union",
            Speaker = "工会代表",
            Prompt = "工会成立大会定在周五。代表把章程放你桌上：「我们只是想要一个能签字的地方。你签不签？」",
            EraId = "ipo",
            Trigger = Within(3, 2e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "union_sign",
                    Label = "签。承认它。",
                    OutcomeText = "你签完字，会议室里第一次响起了掌声——不是给你的。",
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "union_refuse",
                    Label = "不签，准备清算。",
                    OutcomeText = "你把章程推了回去，同时让财务开始算另一笔账：如果现在关掉，每个人能分多少。",
                    StanceId = Stances.Liquidate,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第 3 轮 · 上市：敲钟前一天。
        new()
        {
            Id = "choice_ring",
            Speaker = "她自己",
            Prompt = "敲钟前一天，期权池只剩最后一格。她把文件递给你：「分给大家，还是你自己拿走？」",
            EraId = "ipo",
            Trigger = Within(3, 4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "ring_share",
                    Label = "分给所有人。",
                    OutcomeText = "名单上有前台、有实习生、有已经离职的人。她问：「离职的也分？」你说：「也分。」",
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "ring_keep",
                    Label = "自己拿走，套现。",
                    OutcomeText = "你在受让方那一栏签了自己的名字。钟声第二天照常响起。",
                    StanceId = Stances.Liquidate,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
            ],
        },
    ];

    /// <summary>
    /// 轮次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该轮的完成门槛，
    /// 否则配合 <see cref="ChoiceDefinition.EraId"/> 会让这个选择永远遇不到（构建期会拦）。
    /// </summary>
    private static UnlockCondition Within(int round, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(round),
            UnlockCondition.EarnedThisRunAtLeast(milestone));
}
