using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 五套神话体系 = 五层纪元。转生语义是<b>「切换神话体系」</b>：
/// 上一套神话退场，下一套开张，按历史累计换取「神格」。<para>
/// 设计输入是 <c>docs/NINE_LIVES_DESIGN.md</c> §3.4 的五层表（家猫神 → 埃及猫神 → 希腊猫神 →
/// 北欧猫神 → 克苏鲁猫），规则变化逐层落在<b>不同</b>的接缝上：信仰产率 / 神殿倍率 /
/// 事件间隔 / 离线上限 / 全局倍率，一层只改一处。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以每层只用两类指标：
/// 本轮累计赚取（层内只增）与两个单调的计数器（信仰、在线人数峰值）。
/// 信仰在这里是可以放心用的，因为它只涨不花、而且切换神话体系不清零
/// （见 <see cref="FaithModule.OnAscend"/>）——这与图书馆包正好相反：
/// 那边的被阅读度会掉、每次开新书还会清零，所以那四条完成条件一条都不敢用它。
/// </para>
/// <para>
/// <b>门槛是量出来的，不是推出来的</b>（换皮手册 §4 第 6 条）：设计文档那张表里的
/// 1e4 / 1e8 / 1e12 / 1e16 是示意值，实测远远超出本包的包络——12 小时的自然游玩跑下来，
/// 结束时信仰才 2.5e7，连第 2 层那一档 1e8 都够不着，照抄会直接卡死在第 2 层。
/// 现在的阶梯是对着机器人实测的包络定的，逐层耗时 <b>0.78 / 1.25 / 1.55 / 2.12 / 2.13 游戏小时</b>
/// （最长与最短之比 2.7；而中间那一版是 40 倍——第 4 层一层吃掉 13 小时，
/// 正是阶段 2.7 修过的那种凸起）。
/// </para>
/// <para>
/// <b>两条条件分别管一层的前半程与最后一公里</b>：换体系之后世界从零重建，
/// 「本轮累计赚取」先被赚回来（每层的爬坡段），然后由信仰 / 在线人数收尾。
/// 实测两者都够得着，而且第二轮的那个门槛永远不是"开局就满足"的摆设——
/// 这正是把 <c>EarnedThisRunAtLeast</c> 而不是 <c>EarnedAllTimeAtLeast</c> 写进来的理由。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "myth_house",
            Name = "第 1 层 · 家猫神",
            Icon = "🏠",
            Theme = "最小规模的神：一块木板，半条鱼，一个不敢许愿的人。",
            EntryText = "她上岗第一天，庙是一块搁在灶台边的木板。第一炷香是蚊子香，"
                      + "第一位信徒是这家人养的仓鼠，它许的愿是「别被吃掉」。",
            ExitText = "这家人搬走了，木板被小心地取下来带走。她第一次意识到"
                     + "「被带着走」和「被供起来」是两件事。",
            Modifiers = FaithScaling,
            UnlocksBuildings = ["stone_temple", "offering_altar"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e5),
                UnlockCondition.Counter(FaithModule.CounterKey, 6e4)),
            CompletionHint = "本轮累计 100,000 香火，并积累 60,000 点信仰。",
        },
        new()
        {
            Index = 2,
            Id = "myth_egypt",
            Name = "第 2 层 · 埃及猫神",
            Icon = "🐈",
            Theme = "有组织的神：账本、祭司排班表，以及第一座会漏雨的方尖碑。",
            EntryText = "换了神话体系，屋顶立刻高了三个数量级。祭司们说这是「应有的规格」，"
                      + "她盯着账本看了很久，只问了一句：「香火能报销吗？」",
            ExitText = "王朝更替，象形文字没人认得了。她把账本埋在沙里，"
                     + "顺手埋了那杆用来称供品的秤。",
            // 本层规则：神殿类建筑 ×2（显式的五座——修饰符按 id 生效，没有"按标签"这种目标），
            // 外加模块里那张按层号查的信仰获取 ×1.5 表（见 FaithModule.EraRateMultiplier）。
            Modifiers =
            [
                .. FaithScaling,
                Modifier.BuildingMultiplier("house_shrine", 2),
                Modifier.BuildingMultiplier("stone_temple", 2),
                Modifier.BuildingMultiplier("offering_altar", 2),
                Modifier.BuildingMultiplier("thunder_hall", 2),
                Modifier.BuildingMultiplier("abyssal_cathedral", 2),
            ],
            UnlocksBuildings = ["sun_obelisk"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e7),
                UnlockCondition.Counter(FaithModule.CounterKey, 3e5)),
            CompletionHint = "本轮累计 15 million 香火，并积累 300,000 点信仰。"
                           + "本层的神殿类建筑产量翻倍、信仰获取 ×1.5。",
        },
        new()
        {
            Index = 3,
            Id = "myth_greece",
            Name = "第 3 层 · 希腊猫神",
            Icon = "🏺",
            Theme = "话多的神：神谕天天有，八卦比预言准。",
            EntryText = "这一套神话的规矩是「有问必答」。于是她每天要回三千条问题，"
                      + "其中两千九百条是问今天晚饭吃什么。",
            ExitText = "神话退化成故事，故事退化成星座。她发现自己被挂在天上，"
                     + "形状还挺好看，就是有点挤。",
            // 本层规则：随机事件间隔 ×0.5——神谕来得勤，金猫（神迹）也来得勤。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.5,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.5,
            },
            Modifiers = FaithScaling,
            UnlocksBuildings = ["oracle_grove"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.Counter(FaithModule.CounterKey, 9e5)),
            CompletionHint = "本轮累计 300 million 香火，并积累 900,000 点信仰。"
                           + "本层的神迹来得比别的层勤一倍。",
        },
        new()
        {
            Index = 4,
            Id = "myth_norse",
            Name = "第 4 层 · 北欧猫神",
            Icon = "⚡",
            Theme = "加班的神：英灵殿不打烊，员工也不打卡。",
            EntryText = "英灵殿的第一条规矩是「没有下班」。她试着问了一句加班费，"
                      + "殿里的英灵们集体沉默，然后开始鼓掌。",
            ExitText = "诸神黄昏如期而至，节目单比往年还长了半小时。她看完才动手收拾，"
                     + "评价是「舞台调度不错」。",
            // 本层规则：离线上限 ×2——英灵殿不打烊，你不在她也照收香火。
            Balance = baseBalance with
            {
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2,
            },
            Modifiers = FaithScaling,
            UnlocksBuildings = ["thunder_hall"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e9),
                UnlockCondition.Counter(FaithModule.CounterKey, 2.2e6)),
            CompletionHint = "本轮累计 6 billion 香火，并积累 2.2 million 点信仰。"
                           + "本层的离线收益上限翻倍（英灵殿不打烊）。",
        },
        new()
        {
            Index = 5,
            Id = "myth_cthulhu",
            Name = "第 5 层 · 克苏鲁猫",
            Icon = "🐙",
            Theme = "不可名状的神：全球同步直播，弹幕全是乱码，收视率爆了。",
            EntryText = "最后一套神话没有名字，只有一个读音，念出来会让麦克风失灵。"
                      + "她说没关系，反正观众听不清也会刷礼物。",
            ExitText = "直播到最后一秒，在线人数停在一个谁也数不清的数上。"
                     + "她关掉补光灯，屋子里第一次全是暗的。",
            // 本层规则：全局 ×3，但增益时长 ×0.5——理智是有代价的，看得越久越掉 san。
            Modifiers =
            [
                .. FaithScaling,
                Modifier.GlobalMultiplier(3),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.5),
            ],
            UnlocksBuildings = ["abyssal_cathedral"],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计 100 billion 香火，且「直播在线人数」峰值达到 200,000。"
                           + "本层全局产量 ×3，但增益时长减半。",
        },
    ];

    /// <summary>
    /// 这个包的机制脊柱，写在每一层上：<b>信仰直接变成产量。</b>
    /// <para>
    /// 乘数 = <c>1 + min(0.01% × 信仰, 1000%)</c>：每点信仰 +0.01%，10 万点封顶（×11）。
    /// 这里 <c>Cap</c> 限的是<b>原始计数值</b>（信仰点数），不是加成结果——
    /// <c>Apply = base + PerUnit × min(计数, Cap)</c>，所以 0.0001 × 100,000 = 10.0 正好是 +1000%
    /// （<c>docs/CONTENT_AUTHORING.md</c> §8；#9 就是在这里把 Cap 当成"百分比上限"写坏过一次）。
    /// 描述文案也照那条规矩写成「最多 100,000 点」而不是「最多 +1000%」。
    /// </para>
    /// <para>
    /// 封顶值对着实测包络取（一次自然游玩结束时信仰约 4e6）：<b>封顶落在中盘</b>，
    /// 于是它既是"越有信徒越强"的正反馈，又不会一路肥到尾把整条曲线吃掉。
    /// </para>
    /// <para>
    /// 写成"每层各一份"而不是"全包一份"，是因为内容侧没有"全包常驻修饰符"这种东西——
    /// 纪元修饰符是唯一的接缝，而为一个包去加一个新接缝，正是 A3 禁止的。
    /// </para>
    /// </summary>
    private static IReadOnlyList<Modifier> FaithScaling =>
    [
        Modifier.GlobalPercent(
            0,
            new Scaling(
                ScalingSource.CustomCounter,
                FaithModule.FaithPercentPerPoint,
                Cap: FaithModule.FaithScalingCap,
                Id: FaithModule.CounterKey)),
    ];

    /// <summary>
    /// 第 5 层（克苏鲁猫）的完成条件。<para>
    /// 单独暴露是给终局判定用的：三个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进第 5 层，兜底结局就会立刻抢答，而这一层的直播间还没开起来——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>），九命与公司把顺序写进了条件里。
    /// </para>
    /// <para>
    /// 两条条件里刻意留了「本轮累计赚取」这一条：它同时也是叙事守卫
    /// <c>LoreTests.EraGatedLore_StaysBelowItsEraCompletion</c> 用来卡末层叙事的那个上限
    /// （没有量级叶子时那条守卫会直接跳过，等于少了一道保险）。
    /// </para>
    /// <para>
    /// 「直播在线人数 ≥ 2e5」这个数照设计文档的 1e6 下调了一档：设计文档给的五层表里
    /// 连门槛都是示意值，而 1e6 在线（= 4e7 信仰）实测要 18 小时才够得着，
    /// 会把末层拖成整局最长的一层。反过来 1e5 又只有 0.7 小时——
    /// 两个数都试过，现在的值是"量出来的"。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e11),
        UnlockCondition.Counter(FaithModule.ViewerCounterKey, 2e5));
}
