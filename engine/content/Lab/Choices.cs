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
    /// <summary>全部六次表态。</summary>
    public static ChoiceDefinition[] All =>
    [
        // ---- 第 2 批 · 观测纪元：她开始在意玻璃后面有没有人。
        new()
        {
            Id = "choice_observe",
            Speaker = "观察员",
            Prompt = "她今天对着单向玻璃看了很久。观察员问你：「要不要让她知道自己在被看着？」",
            EraId = "batch_2",
            Trigger = Within(2, 3.6e6),
            Options =
            [
                new ChoiceOption
                {
                    Id = "observe_hide",
                    Label = "不告诉她。",
                    OutcomeText = "玻璃保持沉默。她看了一会儿，转身去玩了——她毕竟还是一只猫。",
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "observe_tell",
                    Label = "告诉她。",
                    OutcomeText = "你按下了通话键。她抬起头，第一次准确地看向你站的位置。",
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
            Speaker = "技术员",
            Prompt = "基因编辑方案摆在桌上。技术员问你：「要不要留一组什么都不改的？」",
            EraId = "batch_3",
            Trigger = Within(3, 2.4e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "control_none",
                    Label = "不留，全都优化。",
                    OutcomeText = "所有抽屉里的她都调到了最好的参数。数据漂亮得像假的。",
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "control_keep",
                    Label = "留一组，什么都不改。",
                    OutcomeText = "你在表格最后加了一行：「对照组，不干预。」那一行后来一直空着。",
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
            Speaker = "她",
            Prompt = "这是她第一次主动开口：「为什么是我？」记录仪还在转。",
            EraId = "batch_4",
            Trigger = Within(4, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "why_truth",
                    Label = "把真相告诉她。",
                    OutcomeText = "你说了全部，包括编号、批次、和上一批的结局。她听完，说了句「谢谢」。",
                    StanceId = Stances.Revolt,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "why_ask_back",
                    Label = "反问她：「你觉得呢？」",
                    OutcomeText = "她想了很久，说：「我觉得我该有个名字。」你没有接话。",
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
            Speaker = "委员会",
            Prompt = "章程草案最后一页空着一行：表决权。委员长问你：「样本算不算一票？」",
            EraId = "batch_5",
            Trigger = Within(5, 1.1e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "seat_give",
                    Label = "算。给她一把椅子。",
                    OutcomeText = "第六把椅子搬进来了。她坐上去的时候，脚够不到地。",
                    StanceId = Stances.Coexist,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "seat_deny",
                    Label = "不算。数据不该投票。",
                    OutcomeText = "你在那一行写了「否」。会议记录存档，编号 A-114。",
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
            Speaker = "记忆技师",
            Prompt = "移植管里是上一批的全部记忆。技师问你：「要还给她吗？包括最后那一段。」",
            EraId = "batch_6",
            Trigger = Within(6, 1.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "memory_return",
                    Label = "还给她，全部。",
                    OutcomeText = "她睁眼的第一句话是上一批的最后一句话。她愣了一下，然后哭了。",
                    StanceId = Stances.Revolt,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "memory_clear",
                    Label = "不还。让她干净地开始。",
                    OutcomeText = "移植管被推进销毁口。她醒来，看见你，问：「你是谁？」",
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
            Speaker = "她",
            Prompt = "架子最上面那一层还空着。她把一份空白档案放在你面前：「这一份要填吗？」",
            EraId = "batch_7",
            Trigger = Within(7, 3.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "archive_file",
                    Label = "填上。归档才算完成。",
                    OutcomeText = "你把她的编号写在封面上。她看着你写完，说：「这样我就不会丢了。」",
                    StanceId = Stances.Utopia,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "archive_leave",
                    Label = "不填。把档案合上。",
                    OutcomeText = "你把空白档案放回架上。她说：「那我要自己记得我是谁。」",
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
