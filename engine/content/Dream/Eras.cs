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
    /// <summary>
    /// 本包的 <c>text.json</c>：纪元文案与剧情散文、建筑文案<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>梦境能量成长曲线的门槛值（公开给测试与文案引用）。</summary>
    public const double DreamEnergySoftCap = 80_000;

    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "sleep_1",
            Name = Prose.Text("eras", "sleep_1", "name"),
            Icon = Prose.Text("eras", "sleep_1", "icon"),
            Theme = Prose.Text("eras", "sleep_1", "theme"),
            EntryText = Prose.Text("eras", "sleep_1", "entryText"),
            ExitText = Prose.Text("eras", "sleep_1", "exitText"),
            // 浅眠：一动就醒，所以点击强；梦还薄，所以梦层类建筑的倍率没起来。
            Modifiers = [.. DeepDream, Modifier.ClickFlat(10), Modifier.GlobalMultiplier(1.1)],
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "sleep_1", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "sleep_2",
            Name = Prose.Text("eras", "sleep_2", "name"),
            Icon = Prose.Text("eras", "sleep_2", "icon"),
            Theme = Prose.Text("eras", "sleep_2", "theme"),
            EntryText = Prose.Text("eras", "sleep_2", "entryText"),
            ExitText = Prose.Text("eras", "sleep_2", "exitText"),
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
            CompletionHint = Prose.Text("eras", "sleep_2", "completionHint"),
        },
        new()
        {
            Index = 3,
            Id = "sleep_3",
            Name = Prose.Text("eras", "sleep_3", "name"),
            Icon = Prose.Text("eras", "sleep_3", "icon"),
            Theme = Prose.Text("eras", "sleep_3", "theme"),
            EntryText = Prose.Text("eras", "sleep_3", "entryText"),
            ExitText = Prose.Text("eras", "sleep_3", "exitText"),
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
            CompletionHint = Prose.Text("eras", "sleep_3", "completionHint"),
        },
        new()
        {
            Index = 4,
            Id = "sleep_4",
            Name = Prose.Text("eras", "sleep_4", "name"),
            Icon = Prose.Text("eras", "sleep_4", "icon"),
            Theme = Prose.Text("eras", "sleep_4", "theme"),
            EntryText = Prose.Text("eras", "sleep_4", "entryText"),
            ExitText = Prose.Text("eras", "sleep_4", "exitText"),
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
            CompletionHint = Prose.Text("eras", "sleep_4", "completionHint"),
        },
        new()
        {
            Index = 5,
            Id = "sleep_5",
            Name = Prose.Text("eras", "sleep_5", "name"),
            Icon = Prose.Text("eras", "sleep_5", "icon"),
            Theme = Prose.Text("eras", "sleep_5", "theme"),
            EntryText = Prose.Text("eras", "sleep_5", "entryText"),
            ExitText = Prose.Text("eras", "sleep_5", "exitText"),
            // 梦核：全局 ×2.3，收尾。这一层不再有新的规则，只有"更大"。
            Modifiers = [.. DeepDream, Modifier.GlobalMultiplier(2.3)],
            UnlocksBuildings = ["dream_core"],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "sleep_5", "completionHint"),
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
