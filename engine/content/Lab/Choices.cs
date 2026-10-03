using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 六次表态：第 2~7 批各一次。<para>
/// 每次两个互斥选项、各指向一条不同的立场、权重都是 2——每条立场一共只有三次机会，
/// 所以四条路都要求"每一次都选它"。与九命包同一个配方：<b>结局是承诺，不是倾向</b>。
/// </para>
/// <para>
/// <b>可选选项的措辞是这个包的核心。</b> 四条路里没有一条是明显"错的"：
/// 乌托邦听起来像善意，删除听起来像负责。要让玩家在点下去之前犹豫，
/// 而不是在"好选项"和"坏选项"之间挑。
/// </para>
/// <para>
/// 触发条件的两条纪律与九命包相同：里程碑必须 ≤ 该批次的完成门槛（<c>EraId</c> 是硬门），
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
        // ---- 第 2 批 · 观测纪元：她开始在意玻璃后面有没有人。
        new()
        {
            Id = "choice_observe",
            Speaker = Prose.Text("choices", "choice_observe", "speaker"),
            Prompt = Prose.Text("choices", "choice_observe", "prompt"),
            EraId = "batch_2",
            Trigger = Within(2, 3.6e6),
            Options =
            [
                new ChoiceOption
                {
                    Id = "observe_hide",
                    Label = Prose.Text("choices", "choice_observe/observe_hide", "label"),
                    OutcomeText = Prose.Text("choices", "choice_observe/observe_hide", "outcomeText"),
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "observe_tell",
                    Label = Prose.Text("choices", "choice_observe/observe_tell", "label"),
                    OutcomeText = Prose.Text("choices", "choice_observe/observe_tell", "outcomeText"),
                    StanceId = Stances.Revolt,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
            ],
        },

        // ---- 第 3 批 · 基因纪元：可以优化，也可以留一组什么都不做的。
        new()
        {
            Id = "choice_control_group",
            Speaker = Prose.Text("choices", "choice_control_group", "speaker"),
            Prompt = Prose.Text("choices", "choice_control_group", "prompt"),
            EraId = "batch_3",
            Trigger = Within(3, 2.4e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "control_none",
                    Label = Prose.Text("choices", "choice_control_group/control_none", "label"),
                    OutcomeText = Prose.Text("choices", "choice_control_group/control_none", "outcomeText"),
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "control_keep",
                    Label = Prose.Text("choices", "choice_control_group/control_keep", "label"),
                    OutcomeText = Prose.Text("choices", "choice_control_group/control_keep", "outcomeText"),
                    StanceId = Stances.Delete,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第 4 批 · 觉醒纪元：她开始问问题。
        new()
        {
            Id = "choice_why",
            Speaker = Prose.Text("choices", "choice_why", "speaker"),
            Prompt = Prose.Text("choices", "choice_why", "prompt"),
            EraId = "batch_4",
            Trigger = Within(4, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "why_truth",
                    Label = Prose.Text("choices", "choice_why/why_truth", "label"),
                    OutcomeText = Prose.Text("choices", "choice_why/why_truth", "outcomeText"),
                    StanceId = Stances.Revolt,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "why_ask_back",
                    Label = Prose.Text("choices", "choice_why/why_ask_back", "label"),
                    OutcomeText = Prose.Text("choices", "choice_why/why_ask_back", "outcomeText"),
                    StanceId = Stances.Coexist,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.03)],
                },
            ],
        },

        // ---- 第 5 批 · 伦理纪元：委员会问该不该给她一票。
        new()
        {
            Id = "choice_seat",
            Speaker = Prose.Text("choices", "choice_seat", "speaker"),
            Prompt = Prose.Text("choices", "choice_seat", "prompt"),
            EraId = "batch_5",
            Trigger = Within(5, 1.1e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "seat_give",
                    Label = Prose.Text("choices", "choice_seat/seat_give", "label"),
                    OutcomeText = Prose.Text("choices", "choice_seat/seat_give", "outcomeText"),
                    StanceId = Stances.Coexist,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "seat_deny",
                    Label = Prose.Text("choices", "choice_seat/seat_deny", "label"),
                    OutcomeText = Prose.Text("choices", "choice_seat/seat_deny", "outcomeText"),
                    StanceId = Stances.Delete,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第 6 批 · 记忆纪元：上一批的记忆可以还给她。
        new()
        {
            Id = "choice_memory",
            Speaker = Prose.Text("choices", "choice_memory", "speaker"),
            Prompt = Prose.Text("choices", "choice_memory", "prompt"),
            EraId = "batch_6",
            Trigger = Within(6, 1.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "memory_return",
                    Label = Prose.Text("choices", "choice_memory/memory_return", "label"),
                    OutcomeText = Prose.Text("choices", "choice_memory/memory_return", "outcomeText"),
                    StanceId = Stances.Revolt,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "memory_clear",
                    Label = Prose.Text("choices", "choice_memory/memory_clear", "label"),
                    OutcomeText = Prose.Text("choices", "choice_memory/memory_clear", "outcomeText"),
                    StanceId = Stances.Delete,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.03)],
                },
            ],
        },

        // ---- 第 7 批 · 归档纪元：最后一批要不要也变成档案。
        new()
        {
            Id = "choice_archive",
            Speaker = Prose.Text("choices", "choice_archive", "speaker"),
            Prompt = Prose.Text("choices", "choice_archive", "prompt"),
            EraId = "batch_7",
            Trigger = Within(7, 3.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "archive_file",
                    Label = Prose.Text("choices", "choice_archive/archive_file", "label"),
                    OutcomeText = Prose.Text("choices", "choice_archive/archive_file", "outcomeText"),
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "archive_leave",
                    Label = Prose.Text("choices", "choice_archive/archive_leave", "label"),
                    OutcomeText = Prose.Text("choices", "choice_archive/archive_leave", "outcomeText"),
                    StanceId = Stances.Coexist,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
            ],
        },
    ];

    /// <summary>
    /// 批次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该批次的完成门槛，
    /// 否则配合 <see cref="ChoiceDefinition.EraId"/> 会让这个选择永远遇不到（构建期会拦）。
    /// </summary>
    private static UnlockCondition Within(int batch, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(batch),
            UnlockCondition.EarnedThisRunAtLeast(milestone));
}
