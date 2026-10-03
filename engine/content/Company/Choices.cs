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
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部六次表态。</summary>
    public static ChoiceDefinition[] All =>
    [
        // ---- 第 1 轮 · 车库创业：第一笔订单，接还是不接。
        new()
        {
            Id = "choice_first_order",
            Speaker = Prose.Text("choices", "choice_first_order", "speaker"),
            Prompt = Prose.Text("choices", "choice_first_order", "prompt"),
            EraId = "garage",
            Trigger = Within(1, 2e4),
            Options =
            [
                new ChoiceOption
                {
                    Id = "order_take",
                    Label = Prose.Text("choices", "choice_first_order/order_take", "label"),
                    OutcomeText = Prose.Text("choices", "choice_first_order/order_take", "outcomeText"),
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "order_negotiate",
                    Label = Prose.Text("choices", "choice_first_order/order_negotiate", "label"),
                    OutcomeText = Prose.Text("choices", "choice_first_order/order_negotiate", "outcomeText"),
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
            Speaker = Prose.Text("choices", "choice_runway", "speaker"),
            Prompt = Prose.Text("choices", "choice_runway", "prompt"),
            EraId = "garage",
            Trigger = Within(1, 6e4),
            Options =
            [
                new ChoiceOption
                {
                    Id = "runway_all_in",
                    Label = Prose.Text("choices", "choice_runway/runway_all_in", "label"),
                    OutcomeText = Prose.Text("choices", "choice_runway/runway_all_in", "outcomeText"),
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "runway_reserve",
                    Label = Prose.Text("choices", "choice_runway/runway_reserve", "label"),
                    OutcomeText = Prose.Text("choices", "choice_runway/runway_reserve", "outcomeText"),
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
            Speaker = Prose.Text("choices", "choice_layoff", "speaker"),
            Prompt = Prose.Text("choices", "choice_layoff", "prompt"),
            EraId = "series_a",
            Trigger = Within(2, 3e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "layoff_yes",
                    Label = Prose.Text("choices", "choice_layoff/layoff_yes", "label"),
                    OutcomeText = Prose.Text("choices", "choice_layoff/layoff_yes", "outcomeText"),
                    StanceId = Stances.Ipo,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
                new ChoiceOption
                {
                    Id = "layoff_no",
                    Label = Prose.Text("choices", "choice_layoff/layoff_no", "label"),
                    OutcomeText = Prose.Text("choices", "choice_layoff/layoff_no", "outcomeText"),
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
            Speaker = Prose.Text("choices", "choice_acquisition", "speaker"),
            Prompt = Prose.Text("choices", "choice_acquisition", "prompt"),
            EraId = "series_a",
            Trigger = Within(2, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "acquisition_sell",
                    Label = Prose.Text("choices", "choice_acquisition/acquisition_sell", "label"),
                    OutcomeText = Prose.Text("choices", "choice_acquisition/acquisition_sell", "outcomeText"),
                    StanceId = Stances.Liquidate,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "acquisition_hold",
                    Label = Prose.Text("choices", "choice_acquisition/acquisition_hold", "label"),
                    OutcomeText = Prose.Text("choices", "choice_acquisition/acquisition_hold", "outcomeText"),
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
            Speaker = Prose.Text("choices", "choice_union", "speaker"),
            Prompt = Prose.Text("choices", "choice_union", "prompt"),
            EraId = "ipo",
            Trigger = Within(3, 2e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "union_sign",
                    Label = Prose.Text("choices", "choice_union/union_sign", "label"),
                    OutcomeText = Prose.Text("choices", "choice_union/union_sign", "outcomeText"),
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "union_refuse",
                    Label = Prose.Text("choices", "choice_union/union_refuse", "label"),
                    OutcomeText = Prose.Text("choices", "choice_union/union_refuse", "outcomeText"),
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
            Speaker = Prose.Text("choices", "choice_ring", "speaker"),
            Prompt = Prose.Text("choices", "choice_ring", "prompt"),
            EraId = "ipo",
            Trigger = Within(3, 4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "ring_share",
                    Label = Prose.Text("choices", "choice_ring/ring_share", "label"),
                    OutcomeText = Prose.Text("choices", "choice_ring/ring_share", "outcomeText"),
                    StanceId = Stances.Union,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "ring_keep",
                    Label = Prose.Text("choices", "choice_ring/ring_keep", "label"),
                    OutcomeText = Prose.Text("choices", "choice_ring/ring_keep", "outcomeText"),
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
