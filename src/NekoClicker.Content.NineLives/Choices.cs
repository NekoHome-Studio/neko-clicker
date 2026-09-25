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
    /// <summary>全部六次表态。</summary>
    public static ChoiceDefinition[] All =>
    [
        // ---- 第二命 · 咖啡馆纪元：她还是个没有名字的东西。
        new()
        {
            Id = "choice_name",
            Speaker = "她",
            Prompt = "她坐在吧台上，看你把最后一块招牌留白。她问：“要不要给我起个名字？”",
            EraId = "life_cafe",
            Trigger = Within(2, 3.6e6),
            Options =
            [
                new ChoiceOption
                {
                    Id = "name_write",
                    Label = "写上去。",
                    OutcomeText = "你写了一个名字。她念了两遍，说：“这个好像本来就是我的。”",
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "name_blank",
                    Label = "先空着。",
                    OutcomeText = "她把爪子按在招牌上，留下一个印子。她说：“这样也行。”",
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
            Speaker = "记录员",
            Prompt = "培养舱的玻璃上有雾，她正在写那个字。记录员问你：“要让她看记录表吗？上面写着她是什么。”",
            EraId = "life_lab",
            Trigger = Within(3, 2.4e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "records_show",
                    Label = "给她看。",
                    OutcomeText = "她读了很久，读到第三页停住了。她说：“原来我有个编号。”",
                    StanceId = Stances.Human,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "records_hide",
                    Label = "收起来。",
                    OutcomeText = "你把它折进口袋。她在玻璃后面看了你一会儿，然后开始舔爪子。",
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
            Speaker = "城里的人",
            Prompt = "有人画好了图纸：广场中央，一座她的石像。他们问你：“立吗？”",
            EraId = "life_civilization",
            Trigger = Within(4, 6e7),
            Options =
            [
                new ChoiceOption
                {
                    Id = "statue_build",
                    Label = "立。",
                    OutcomeText = "石像立起来那天，她绕着走了三圈，说：“这个比我完整。”",
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "statue_refuse",
                    Label = "不立。",
                    OutcomeText = "你把图纸退了回去。她说：“对，站着的东西不会晒太阳。”",
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
            Speaker = "另一份她",
            Prompt = "屏幕上，实例 1 在等你的答复：“要不要把实例 0 也留着？”",
            EraId = "life_cyber",
            Trigger = Within(5, 1.1e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "instance_both",
                    Label = "都留着。",
                    OutcomeText = "两份她互相看了一眼。她说：“有两个我，就有一个不用害怕忘记。”",
                    StanceId = Stances.Divine,
                    Weight = 2,
                    Modifiers = [Modifier.GlobalMultiplier(1.05)],
                },
                new ChoiceOption
                {
                    Id = "instance_one",
                    Label = "只留一份。",
                    OutcomeText = "你关掉实例 1。她没有反对，只说：“那这一份要好好活。”",
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
            Speaker = "废墟里的机器",
            Prompt = "托盘上躺着最后一块拼图。机器问：“要装回去吗？”",
            EraId = "life_posthuman",
            Trigger = Within(6, 1.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "puzzle_restore",
                    Label = "装回去。",
                    OutcomeText = "拼图归位的一瞬间，她咳了一声——那是她第一次像一个会咳嗽的东西。",
                    StanceId = Stances.Human,
                    Weight = 2,
                    Modifiers = [Modifier.ClickMultiplier(1.1)],
                },
                new ChoiceOption
                {
                    Id = "puzzle_leave",
                    Label = "留在托盘上。",
                    OutcomeText = "你合上盖子。她说：“缺一块也挺好，至少还知道缺什么。”",
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
            Speaker = "寐娅",
            Prompt = "她的声音第一次这么近：“要不要叫醒她？”",
            EraId = "life_dream",
            Trigger = Within(8, 3.4e8),
            Options =
            [
                new ChoiceOption
                {
                    Id = "wake_later",
                    Label = "让她再睡一会儿。",
                    OutcomeText = "你没有叫她。梦里她翻了个身，尾巴扫过你的手背。",
                    StanceId = Stances.Cat,
                    Weight = 2,
                    Modifiers = [Modifier.GoldenCookieReward(1.05)],
                },
                new ChoiceOption
                {
                    Id = "wake_now",
                    Label = "叫醒她。",
                    OutcomeText = "她睁开眼。梦碎得很安静，像一层灰。她说：“我知道会有这一天。”",
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
