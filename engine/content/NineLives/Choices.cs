using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 六次表态：第 2 / 3 / 4 / 5 / 6 / 8 命各一次。<para>
/// 每次只给两个选项，各自指向一条不同的立场，权重都是 2——因为每条立场一共只有
/// 三次机会，所以四条路都要求"每一次都选它"。这是刻意的：结局是承诺，不是倾向。
/// </para>
/// <para>
/// <b>每个选项都带一个小幅永久修饰符</b>（不超过 10%）。这是设计文档"三条件"里的
/// <b>有代价</b>：选择必须当场在数值上留痕，而不能只靠"主导立场"这种延迟结算——
/// 否则前五次表态在数值上完全无感，玩家做决定时没有分量。
/// 代价是双向的：站在某一边就拿不到另一边的加成。
/// </para>
/// <para>
/// <b>触发条件的两条纪律（都别破坏）</b>：
/// <list type="number">
///   <item>里程碑必须 <b>≤ 该层的完成门槛</b>。选择挂了 <see cref="ChoiceDefinition.EraId"/>，
///   是个硬门——层内没达到里程碑就舍命走人，这个选择就<b>永远</b>遇不到了。
///   构建期会按这条规则校验（见 <c>GameContentBuilder.ValidateChoices</c>）。</item>
///   <item>里程碑数值要全包唯一，理由与叙事条目相同：同条件的两个东西必然同时发生。</item>
/// </list>
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
        // ---- 第二命 · 咖啡馆纪元：她还是个没有名字的东西。
        new()
        {
            Id = "choice_name",
            Speaker = Prose.Text("choices", "choice_name", "speaker"),
            Prompt = Prose.Text("choices", "choice_name", "prompt"),
            EraId = "life_cafe",
            Trigger = Within(2, 3.6e6),
            Options =
            [
                new ChoiceOption
                {
                    Id = "name_write",
                    Label = Prose.Text("choices", "choice_name/name_write", "label"),
                    OutcomeText = Prose.Text("choices", "choice_name/name_write", "outcomeText"),
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "name_blank",
                    Label = Prose.Text("choices", "choice_name/name_blank", "label"),
                    OutcomeText = Prose.Text("choices", "choice_name/name_blank", "outcomeText"),
                    StanceId = Stances.Cat,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
            ],
        },

        // ---- 第三命 · 实验室纪元：记录表上写着她是什么。
        new()
        {
            Id = "choice_records",
            Speaker = Prose.Text("choices", "choice_records", "speaker"),
            Prompt = Prose.Text("choices", "choice_records", "prompt"),
            EraId = "life_lab",
            Trigger = Within(3, 2.4e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "records_show",
                    Label = Prose.Text("choices", "choice_records/records_show", "label"),
                    OutcomeText = Prose.Text("choices", "choice_records/records_show", "outcomeText"),
                    StanceId = Stances.Human,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "records_hide",
                    Label = Prose.Text("choices", "choice_records/records_hide", "label"),
                    OutcomeText = Prose.Text("choices", "choice_records/records_hide", "outcomeText"),
                    StanceId = Stances.Cat,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.03)],
                },
            ],
        },

        // ---- 第四命 · 文明纪元：他们想给她立一座像。
        new()
        {
            Id = "choice_statue",
            Speaker = Prose.Text("choices", "choice_statue", "speaker"),
            Prompt = Prose.Text("choices", "choice_statue", "prompt"),
            EraId = "life_civilization",
            Trigger = Within(4, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "statue_build",
                    Label = Prose.Text("choices", "choice_statue/statue_build", "label"),
                    OutcomeText = Prose.Text("choices", "choice_statue/statue_build", "outcomeText"),
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "statue_refuse",
                    Label = Prose.Text("choices", "choice_statue/statue_refuse", "label"),
                    OutcomeText = Prose.Text("choices", "choice_statue/statue_refuse", "outcomeText"),
                    StanceId = Stances.Sever,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第五命 · 赛博纪元：她开始复制自己。
        new()
        {
            Id = "choice_instance",
            Speaker = Prose.Text("choices", "choice_instance", "speaker"),
            Prompt = Prose.Text("choices", "choice_instance", "prompt"),
            EraId = "life_cyber",
            Trigger = Within(5, 1.1e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "instance_both",
                    Label = Prose.Text("choices", "choice_instance/instance_both", "label"),
                    OutcomeText = Prose.Text("choices", "choice_instance/instance_both", "outcomeText"),
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "instance_one",
                    Label = Prose.Text("choices", "choice_instance/instance_one", "label"),
                    OutcomeText = Prose.Text("choices", "choice_instance/instance_one", "outcomeText"),
                    StanceId = Stances.Human,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
            ],
        },

        // ---- 第六命 · 末世纪元：金库最后一层，最后一块拼图。
        new()
        {
            Id = "choice_puzzle",
            Speaker = Prose.Text("choices", "choice_puzzle", "speaker"),
            Prompt = Prose.Text("choices", "choice_puzzle", "prompt"),
            EraId = "life_posthuman",
            Trigger = Within(6, 1.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "puzzle_restore",
                    Label = Prose.Text("choices", "choice_puzzle/puzzle_restore", "label"),
                    OutcomeText = Prose.Text("choices", "choice_puzzle/puzzle_restore", "outcomeText"),
                    StanceId = Stances.Human,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "puzzle_leave",
                    Label = Prose.Text("choices", "choice_puzzle/puzzle_leave", "label"),
                    OutcomeText = Prose.Text("choices", "choice_puzzle/puzzle_leave", "outcomeText"),
                    StanceId = Stances.Sever,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },

        // ---- 第八命 · 梦境纪元：寐娅第一次靠得这么近。
        new()
        {
            Id = "choice_wake",
            Speaker = Prose.Text("choices", "choice_wake", "speaker"),
            Prompt = Prose.Text("choices", "choice_wake", "prompt"),
            EraId = "life_dream",
            Trigger = Within(8, 3.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "wake_later",
                    Label = Prose.Text("choices", "choice_wake/wake_later", "label"),
                    OutcomeText = Prose.Text("choices", "choice_wake/wake_later", "outcomeText"),
                    StanceId = Stances.Cat,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "wake_now",
                    Label = Prose.Text("choices", "choice_wake/wake_now", "label"),
                    OutcomeText = Prose.Text("choices", "choice_wake/wake_now", "outcomeText"),
                    StanceId = Stances.Sever,
                    Weight = 2,
                    Modifiers = [Modifier.PriceMultiplier(0.95)],
                },
            ],
        },
    ];

    /// <summary>
    /// 纪元门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该层的完成门槛，
    /// 否则配合 <see cref="ChoiceDefinition.EraId"/> 会让这个选择永远遇不到。
    /// </summary>
    private static UnlockCondition Within(int era, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(era),
            UnlockCondition.EarnedThisRunAtLeast(milestone));
}
