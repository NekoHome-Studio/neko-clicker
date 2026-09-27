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
    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "basement",
            Name = "第 1 次重启 · 地下室",
            Icon = "🕯️",
            Theme = "文明已经结束很久了。她从一个塌掉的地下室里醒来，第一件事是找水。",
            EntryText = "你醒了。没有电，没有信号，也没有别人。她把手在裤子上擦干净，说：「那就从今天算起。」",
            ExitText = "地下室的墙被雨水泡透了。她收拾好东西往地面走——这一次她什么都没有带走。",
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "本轮累计挖到 100,000 物资。",
        },
        new()
        {
            Index = 2,
            Id = "electric",
            Name = "第 2 次重启 · 电",
            Icon = "💡",
            Theme = "她记得怎么发电——上一次她也用过发电机，只是那时候没觉得这件事值得记。",
            EntryText = "这一次她先去东边那栋楼，二楼靠窗的位置。发电机还在原地，只是锈住了。",
            ExitText = "灯亮了整晚。她第一次看清地下室到底有多大，然后开始数还有多少面墙没刷。",
            // 上一次留下四分之一：还记得路，记得水在哪，记得哪块地板能踩。
            InheritBuildingRatio = 0.25,
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            UnlocksBuildings = ["generator"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = "本轮累计 20 million，并解锁 4 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "settlement",
            Name = "第 3 次重启 · 聚集地",
            Icon = "🏕️",
            Theme = "有人来了。她们说，是看到灯光找过来的。",
            EntryText = "天线上第一次收到人声。三公里外，有人在问：「那边是不是有电？」",
            ExitText = "聚集地有了名字。名字是她们自己起的，她没参与投票。",
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
            CompletionHint = "本轮累计 400 million，并解锁 10 个成就。",
        },
        new()
        {
            Index = 4,
            Id = "memory_net",
            Name = "第 4 次重启 · 记忆网",
            Icon = "🕸️",
            Theme = "她开始把记忆写下来——因为这一次她发现，重启真的会忘掉东西。",
            EntryText = "她在墙上写字，写完又擦掉，改成三个字：「挖出来」。",
            ExitText = "第一座档案馆封顶。她说：「下一轮就不用重新学一遍了。」",
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
            CompletionHint = "本轮累计 8 billion，且峰值产量达到 50 million/s。",
        },
        new()
        {
            Index = 5,
            Id = "last_ruin",
            Name = "第 5 次重启 · 最后的遗迹",
            Icon = "🏚️",
            Theme = "最后一批人类遗迹就在城墙外。她必须决定，要不要把它挖出来。",
            EntryText = "探地雷达上是一片一片的回波——城墙外埋着整座城市，完整地压在地下三米。",
            ExitText = "她站在城墙上，看着下面那些还没被挖出来的东西。风很大，她没说话。",
            // 七成：这一轮她几乎把整个文明搬了过来。
            InheritBuildingRatio = 0.7,
            InheritBuildings = ["ruins", "archive"],
            Modifiers = [Modifier.GlobalMultiplier(3)],
            UnlocksBuildings = ["relic_city"],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计 150 billion，并解锁 18 个成就。",
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
