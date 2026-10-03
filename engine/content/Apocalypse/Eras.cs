using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 五次重启 = 五层纪元。转生语义是<b>「重启文明」</b>：世界推倒重来，
/// 但上一轮的建筑按比例留下来——<b>每一次重启，她能多记住一点</b>
/// （继承比例 0 → 25% → 40% → 55% → 70%）。<para>
/// 这是全项目第一个真正使用 <see cref="EraDefinition.InheritBuildingRatio"/> 的包；
/// 前四个包的该字段全是默认值 0（全清）。<b>注意语义</b>：继承比例写在<b>目标层</b>上，
/// 表达的是"这一层允许带进来什么"，不是"离开上一层时带走什么"。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取、成就数、峰值产量
/// 这三类只会涨的指标。记忆残片虽然也单调，但它在第 1 轮恒为 0（没有东西可继承），
/// 所以同样不能进完成条件——否则第 1 轮永远走不出去。构建期会强制校验。
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

    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "basement",
            Name = Prose.Text("eras", "basement", "name"),
            Icon = Prose.Text("eras", "basement", "icon"),
            Theme = Prose.Text("eras", "basement", "theme"),
            EntryText = Prose.Text("eras", "basement", "entryText"),
            ExitText = Prose.Text("eras", "basement", "exitText"),
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "basement", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "electric",
            Name = Prose.Text("eras", "electric", "name"),
            Icon = Prose.Text("eras", "electric", "icon"),
            Theme = Prose.Text("eras", "electric", "theme"),
            EntryText = Prose.Text("eras", "electric", "entryText"),
            ExitText = Prose.Text("eras", "electric", "exitText"),
            // 上一次留下四分之一：还记得路，记得水在哪，记得哪块地板能踩。
            InheritBuildingRatio = 0.25,
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            UnlocksBuildings = ["generator"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = Prose.Text("eras", "electric", "completionHint"),
        },
        new()
        {
            Index = 3,
            Id = "settlement",
            Name = Prose.Text("eras", "settlement", "name"),
            Icon = Prose.Text("eras", "settlement", "icon"),
            Theme = Prose.Text("eras", "settlement", "theme"),
            EntryText = Prose.Text("eras", "settlement", "entryText"),
            ExitText = Prose.Text("eras", "settlement", "exitText"),
            // 四成留了下来；发电机是保底的——她无论如何先去找那台发电机。
            InheritBuildingRatio = 0.4,
            InheritBuildings = ["generator"],
            // 有人守着，夜里也在转：离线时长上限 ×1.5。
            Balance = baseBalance with
            {
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 1.5,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(4e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = Prose.Text("eras", "settlement", "completionHint"),
        },
        new()
        {
            Index = 4,
            Id = "memory_net",
            Name = Prose.Text("eras", "memory_net", "name"),
            Icon = Prose.Text("eras", "memory_net", "icon"),
            Theme = Prose.Text("eras", "memory_net", "theme"),
            EntryText = Prose.Text("eras", "memory_net", "entryText"),
            ExitText = Prose.Text("eras", "memory_net", "exitText"),
            // 五成五：她要开始赌"记得住"这件事本身。
            InheritBuildingRatio = 0.55,
            // 记忆开始反过来喂产量：越记得住，活着越容易。
            Modifiers =
            [
                Modifier.GlobalMultiplier(2),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 60_000, Id: ShardsModule.CounterKey)),
            ],
            UnlocksBuildings = ["data_tower", "archive"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(8e9),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 5e7)),
            CompletionHint = Prose.Text("eras", "memory_net", "completionHint"),
        },
        new()
        {
            Index = 5,
            Id = "last_ruin",
            Name = Prose.Text("eras", "last_ruin", "name"),
            Icon = Prose.Text("eras", "last_ruin", "icon"),
            Theme = Prose.Text("eras", "last_ruin", "theme"),
            EntryText = Prose.Text("eras", "last_ruin", "entryText"),
            ExitText = Prose.Text("eras", "last_ruin", "exitText"),
            // 七成：这一轮她几乎把整个文明搬了过来。
            InheritBuildingRatio = 0.7,
            InheritBuildings = ["ruins", "archive"],
            Modifiers = [Modifier.GlobalMultiplier(3)],
            UnlocksBuildings = ["relic_city"],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "last_ruin", "completionHint"),
        },
    ];

    /// <summary>
    /// 最后一次重启（第 5 层）的完成条件。<para>
    /// 单独暴露是给终局判定用的：三个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 5 层，兜底结局就会立刻触发，而这一层的叙事与记忆还没长出来——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>），九命与公司包把这条写进了设计。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1.5e11),
        UnlockCondition.AchievementsAtLeast(18));
}
