using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 两个结局：设计文档给赛博猫娘定的两个（互联网守护猫 / 找到主人的数据残影）。<para>
/// 这个包既没有立场轴，也没有"记忆量"那种单向累积的资源——它只有两个指标：
/// 走完的五层，和攒下的算力。<b>「找到主人的数据残影」是承诺型的</b>：
/// 它不要求你做任何表态，只要求你一直往上爬、一直算，直到算力足够把那段残影从噪声里捞出来。
/// </para>
/// <para>
/// <b>兜底结局「互联网守护猫」只依赖主线完成</b>：不含取反、不依赖任何可选行为，
/// <c>Priority</c> 最大。构建期强制要求存在这样一个结局，玩家回避不了它，也就不可能卡住。
/// 它的语义也成立：她没找到主人，但她把整张网看住了——这本身就是一种结局，
/// 而且是更常见的那一种。
/// </para>
/// <para>
/// <b>两条条件的几何关系（这个包最容易写坏的一处）</b>：
/// 终局判定按<b>优先级升序</b>取第一个满足条件的，<b>记下之后就再也不判</b>（手册 §13.2）。
/// 两个结局同挂在末层主线之上，而末层主线本身要算力——于是三条门槛是：
/// <list type="bullet">
///   <item>末层主线（<see cref="Eras.FinalCompletion"/>）：算力 ≥ 5e5；</item>
///   <item><b>承诺型</b>（Priority 0）：算力 ≥ <see cref="ComputeForEnding"/> = 6e6；</item>
///   <item><b>兜底型</b>（Priority 100）：游玩时长 ≥ <see cref="FallbackPlayTimeSeconds"/> = 7 小时。</item>
/// </list>
/// 实测（<c>EnvelopeProbe</c>）：第 5 层在 3.2 小时开启、主线在 4.0 小时完成，
/// 那一刻算力刚过 5e5——<b>承诺型还不成立</b>，而兜底的时长门槛也还没到。
/// 玩家继续算，6e6 在 7 小时前后到达，承诺型成立；自然游玩因此落在「找到主人的数据残影」。
/// </para>
/// <para>
/// <b>兜底刻意挂在另一个指标（时长）上，而不是"更高的算力门槛"。</b>
/// 理由是算力单调不减：若两条都挂在算力上、主线又要求算力，那主线开启时两条会同时成立，
/// 靠 Priority 分胜负——门槛低的那条永远赢，门槛高的那条<b>结构上不可达</b>。
/// 本包第一版正是这么写坏的：兜底写成"只依赖主线完成"（门槛最低、优先级最大），
/// 于是自然游玩永远落到「互联网守护猫」，承诺型静默地变成死内容。
/// 构建期完全看不出来（两个条件各自都合法、兜底也过 ValidateEndings 的稳定兜底检查），
/// 只有 <c>EnvelopeProbe</c> 的实测把它抓了出来。换成时长之后，"快"与"深"两条路才真的分得开。
/// </para>
/// <para>
/// 兜底仍然满足构建期的"稳定兜底"要求：条件是末层主线 + 一个单调不减的指标，
/// 不含取反、不依赖表态。任何走完主线的存档都必然撞上其中之一。
/// </para>
/// <para>/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，
/// 而这一层的故事还没走完。实验室包踩过这个坑，这个包把顺序写进条件里。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>两个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_owner_echo",
            Name = Prose.Text("endings", "end_owner_echo", "name"),
            Icon = Prose.Text("endings", "end_owner_echo", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ComputeModule.CounterKey, ComputeForEnding)),
            Text = Prose.Text("endings", "end_owner_echo", "text"),
        },
        new()
        {
            Id = "end_guardian_cat",
            Name = Prose.Text("endings", "end_guardian_cat", "name"),
            Icon = Prose.Text("endings", "end_guardian_cat", "icon"),
            Priority = 100,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.PlayTimeAtLeast(FallbackPlayTimeSeconds)),
            Text = Prose.Text("endings", "end_guardian_cat", "text"),
        },
    ];

    /// <summary>
    /// 结局成就。<para>
    /// 它们自己声明 <c>Unlock = EndingReached(...)</c>，走常规成就路径解锁——
    /// 结局不需要知道"谁是它的成就"，引擎也不需要针对结局加特判。
    /// </para>
    /// </summary>
    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_owner_echo", "end_owner_echo"),
        Ending("ach_end_guardian_cat", "end_guardian_cat"),
    ];

    /// <summary>
    /// 「算力够把残影从噪声里捞出来」的门槛。<para>
    /// 它必须<b>够得着</b>——算力虽然不衰减、迁服务器也不清零，但它的产率完全由建筑规模决定，
    /// 门槛一旦高于这个包在真实曲线里攒得出的量，那个结局就永远拿不到。
    /// 数值按<b>实测包络</b>定（BRIEF §6：先量包络再设值）：
    /// 第 5 层的 4.5 小时处算力到 1.2e6，5 小时 2.1e6，7 小时 6.4e6，所以 6e6 留了约两小时的余量。
    /// 它由 <c>CyberContentTests.ComputeAtTheEnd_ExceedsTheEndingThreshold</c> 守着。
    /// </para>
    /// </summary>
    public const double ComputeForEnding = 6e6;

    /// <summary>
    /// 兜底结局「互联网守护猫」的时长门槛（秒）。<para>
    /// <b>它刻意不是算力门槛</b>：算力单调不减、末层主线本身又要算力，
    /// 两条结局都挂在它上面的话，主线一开、算力又已经越过两条时两者会同时成立，
    /// 门槛低的那条永远赢、另一条结构上不可达（见类型注释）。
    /// 用一个不同的指标把"快"与"深"分开，两条路才各自可达。
    /// </para>
    /// <para>
    /// 7 小时是实测包络上的一个安全点：主线在 4.0 小时完成，承诺型的算力门槛在 7 小时前后到达
    /// （<c>EnvelopeProbe</c>：6.5 小时 5.3e6、7 小时 6.4e6）。所以算得深的玩家拿到承诺型，
    /// 主线走完就停手的玩家在 7 小时处被兜底接住——两边都不会卡住。
    /// </para>
    /// </summary>
    public const double FallbackPlayTimeSeconds = 7 * 3600;

    /// <summary>
    /// 末层主线完成：走到第 5 层，且第 5 层的完成条件成立。<para>
    /// 复用 <see cref="Eras.FinalCompletion"/> 而不是抄一遍数值——两条门槛一旦分叉，
    /// "结局在末层完成后判定"这条规则就会悄悄失效。
    /// </para>
    /// </summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(5),
            Eras.FinalCompletion);

    private static AchievementDefinition Ending(string id, string endingId) => new()
    {
        Id = id,
        Name = Prose.Text("achievements", id, "name"),
        Icon = Prose.Text("achievements", id, "icon"),
        Description = Prose.Text("achievements", id, "description"),
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
