using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 五层梦 = 五层纪元。转生语义是<b>「再往下睡一层」</b>：上一层的梦塌了，新的梦更大、也更难醒。<para>
/// <b>每一层都带同一条核心机制</b>（<see cref="DeepDream"/>）：全局产量按「梦境能量」成长。
/// 写到每层而不是写成"全局规则"，是因为 <c>EraDefinition.Modifiers</c> 是"本层常驻倍率"
/// 的唯一接缝——内容侧没有"全包常驻修饰符"这种东西，而为一个包去加它，
/// 正是 A3 禁止的"为需求打补丁"。
/// </para>
/// <para>
/// <b>「越深的梦越大」是怎么落地的</b>：不是把同一套倍率抄五遍，而是每层各加一条自己的规则——
/// 第 2 层把梦层类建筑抬起来（睡得更沉，离线也更长），第 3 层全局上调，
/// 第 4 层给梦境能量的成长换一条陡得多的曲线，第 5 层收尾。所以"层与层之间换手感"
/// 是五条不同的修饰符，而不是五个不同的数字。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取 / 成就数 / 峰值产量。
/// 梦境能量虽然是单调的，但完成条件里不引用它——那会让"梦浓不浓"变成硬门，
/// 而这一包的主题是"你可以一直睡下去"。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>梦境能量成长曲线的门槛值（公开给测试与文案引用）。</summary>
    public const double DreamEnergySoftCap = 80_000;

    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "sleep_1",
            Name = "第 1 层 · 浅眠",
            Icon = "🛏️",
            Theme = "半梦半醒：床垫的纹路还压在小腿上，手指一动就能碰到现实。",
            EntryText = "她侧过身，把脸埋进枕头。数到第七只猫的时候，楼下的街换成了她认得的字。",
            ExitText = "她翻了个身，枕头凉了半边。这一层梦太薄，薄得能看见床单。她决定再往下睡一点。",
            // 浅眠：一动就醒，所以点击强；梦还薄，所以梦层类建筑的倍率没起来。
            Modifiers = [.. DeepDream, Modifier.ClickFlat(10), Modifier.GlobalMultiplier(1.1)],
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "本轮累计睡出 100,000 点梦。",
        },
        new()
        {
            Index = 2,
            Id = "sleep_2",
            Name = "第 2 层 · 深眠",
            Icon = "😴",
            Theme = "睡得更沉：梦层开始自己长，身体却越来越沉，离线的收益更好。",
            EntryText = "第二层比第一层安静。她往下走的时候没有脚步声——梦里的地面不响。",
            ExitText = "她在这层待了很久，久到忘了上面还有一层。醒来时手背上有个印子，像是被什么压过。",
            // 深眠：梦层类建筑 ×1.6、全局 ×1.4，离线结算上限翻倍。
            // 离线是"睡得更沉"最直接的表达——原版的 3 小时在这里是 6 小时。
            Balance = baseBalance with
            {
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2,
            },
            Modifiers = [.. DeepDream, .. DreamLayerMultiplier(1.6), Modifier.GlobalMultiplier(1.4)],
            UnlocksBuildings = ["dream_layer", "dream_mirror"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2.4e9),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = "本轮累计 2.4 billion，并解锁 4 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "sleep_3",
            Name = "第 3 层 · 清明梦",
            Icon = "💡",
            Theme = "她意识到自己在做梦。从这一刻起，梦魇来得频繁——但梦里的一切都听她的。",
            EntryText = "她在梦里停住，抬手看了看自己的手指，然后说：「这是我的梦。」整层梦安静了一秒，接着开始按她说的长。",
            ExitText = "清明是有代价的：醒着的那部分她，再也没法完全睡过去。",
            // 清明梦：全局 ×1.6，梦魇（金猫）来得更频繁。
            // 频率修饰符乘的是**间隔**，所以「更频繁」要写成小于 1 的数（0.75 → 间隔缩到 3/4）。
            Modifiers =
            [
                .. DeepDream,
                .. DreamLayerMultiplier(1.6),
                Modifier.GlobalMultiplier(1.6),
                Modifier.GoldenCookieFrequency(0.75),
            ],
            UnlocksBuildings = ["lucid_zone"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.6e10),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = "本轮累计 16 billion，并解锁 10 个成就。",
        },
        new()
        {
            Index = 4,
            Id = "sleep_4",
            Name = "第 4 层 · 噩梦层",
            Icon = "🕷️",
            Theme = "梦的褶皱里全是没做完的坏事。但这里的梦最浓，浓到能拧出东西来。",
            EntryText = "她往下走的时候，墙壁开始变软。有东西在深处呼吸，节奏和她一样。",
            ExitText = "她从噩梦里爬出来，指甲断了半片。梦没赢，但也没输。",
            // 噩梦层：梦境能量的成长曲线在这里陡增，梦层类建筑再抬一档。
            // 这一层的规则变化就是"第二资源终于开始决定产量"。
            Modifiers =
            [
                .. DeepDream,
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00012, Cap: 150_000, Id: DreamEnergyModule.CounterKey)),
                .. DreamLayerMultiplier(1.5),
                Modifier.GlobalMultiplier(1.5),
            ],
            UnlocksBuildings = ["dream_weaver", "nesting_tower"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(4.5e11),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 4e7)),
            CompletionHint = "本轮累计 450 billion，且峰值产量达到 40 million/s。",
        },
        new()
        {
            Index = 5,
            Id = "sleep_5",
            Name = "第 5 层 · 梦核",
            Icon = "🔮",
            Theme = "所有梦层套着的那一颗芯。走到这里只有两件事可做：把它叫醒，或者留下来。",
            EntryText = "最里面没有房间，只有一颗慢慢转的东西。她把手放上去，整座梦认出了她。",
            ExitText = "梦核安静下来。现在整座梦都在等她决定：是醒，还是再往下。",
            // 梦核：全局 ×2.3，收尾。这一层不再有新的规则，只有"更大"。
            Modifiers = [.. DeepDream, Modifier.GlobalMultiplier(2.3)],
            UnlocksBuildings = ["dream_core"],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计 1 trillion，且睡满 6 小时。",
        },
    ];

    /// <summary>
    /// 这个包的核心机制，写在每一层上：<b>「越深的梦越大」= 全局产量按梦境能量成长。</b>
    /// <para>
    /// 乘数 = <c>1 + min(0.02% × 梦境能量, 200%)</c>：0 点 → ×1.0、
    /// 10,000 点 → ×3.0、<see cref="DreamEnergySoftCap"/> 点 → ×17.0 封顶。
    /// 第 4 层另外叠一条更陡的曲线（"梦最浓的那一层"）。
    /// </para>
    /// <para>
    /// <b>为什么是 ×1.0 起步而不是像图书馆那样给一个惩罚性的下限</b>：这个包没有"不做就变差"
    /// 的压力机制（手册 §4 第 15 条：会衰减的第二资源是 #9 的专利），
    /// 梦的能量只会攒起来，所以曲线只需要"越深越强"这一个方向。
    /// </para>
    /// </summary>
    private static IReadOnlyList<Modifier> DeepDream =>
    [
        Modifier.GlobalPercent(
            0,
            new Scaling(ScalingSource.CustomCounter, 0.0002, Cap: DreamEnergySoftCap, Id: DreamEnergyModule.CounterKey)),
    ];

    /// <summary>把梦层类建筑整体抬一档（逐建筑写，因为修饰符目标必须点名建筑）。</summary>
    /// <param name="factor">倍率。</param>
    private static IReadOnlyList<Modifier> DreamLayerMultiplier(double factor) =>
    [
        .. Buildings.All
            .Where(b => b.Tags.Contains(DreamEnergyModule.DreamLayerTag, StringComparer.Ordinal))
            .Select(b => Modifier.BuildingMultiplier(b.Id, factor)),
    ];

    /// <summary>
    /// 最后一层（第 5 层）的完成条件。<para>
    /// 单独暴露是给终局判定用的：两个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 5 层，兜底结局就会立刻触发，而这一层的梦核还没养起来——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>）。
    /// </para>
    /// <para>
    /// <b>为什么带一个 6 小时的时长门槛</b>：这是<b>先量包络再设值</b>的结论，不是难度调节。
    /// 实测这个包的量级门槛爬得太快（1e12 在第 40 分钟就到了），如果末层主线只要有量级就能完成，
    /// 兜底结局会在第 1 小时就落地——而结局一旦判定就再也不重判（<c>EndingSystem</c> 的规则），
    /// 于是「叫醒梦者」那条<b>依赖第二资源的承诺</b>就永远没有机会被满足，成了死内容。
    /// 时长是单调指标（构建期白名单允许），"梦要睡够才到最深"也正好是这个包的语义：
    /// 梦核不是买出来的，是熬出来的。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e12),
        UnlockCondition.PlayTimeAtLeast(6 * 3600));
}
