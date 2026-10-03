using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 三个结局：设计文档给末世定的三个（复活人类 / 成为新人类 / 安静结束）。<para>
/// <b>这个包没有立场轴</b>（设计矩阵里 #6 只需要 Era + Lore + 继承），所以结局不是
/// "你表过什么态"决定的，而是<b>你究竟记住了多少</b>决定的——记忆残片的数量与图鉴的厚度。
/// 这与其他四个包的结局来源完全不同，却共用同一套 <see cref="EndingDefinition"/>。
/// </para>
/// <para>
/// 互斥靠 <see cref="EndingDefinition.Priority"/>：记得够多且读得够全 → 复活人类；
/// 记得够多但没拼出全貌 → 成为新人类；什么都没能留下 → 安静结束。
/// 兜底结局 <c>end_quiet</c> 只依赖主线完成，不含取反、不依赖记忆量——
/// 构建期强制要求存在这样一个结局。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，
/// 而这一层的记忆还没长出来。实验室包踩过这个坑，九命与公司把顺序写进了条件里。
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

    /// <summary>几个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_revive",
            Name = Prose.Text("endings", "end_revive", "name"),
            Icon = Prose.Text("endings", "end_revive", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ShardsModule.CounterKey, ShardsForEnding),
                UnlockCondition.LoreAtLeast(LoreForRevival)),
            Text = Prose.Text("endings", "end_revive", "text"),
        },
        new()
        {
            Id = "end_newhuman",
            Name = Prose.Text("endings", "end_newhuman", "name"),
            Icon = Prose.Text("endings", "end_newhuman", "icon"),
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ShardsModule.CounterKey, ShardsForEnding)),
            Text = Prose.Text("endings", "end_newhuman", "text"),
        },
        new()
        {
            Id = "end_quiet",
            Name = Prose.Text("endings", "end_quiet", "name"),
            Icon = Prose.Text("endings", "end_quiet", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_quiet", "text"),
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
        Ending("ach_end_revive", "end_revive"),
        Ending("ach_end_newhuman", "end_newhuman"),
        Ending("ach_end_quiet", "end_quiet"),
    ];

    /// <summary>
    /// 「记忆够得上一个结局」的门槛。<para>
    /// 单独暴露出来是因为它同时被两个结局引用：它们的区别只在<b>图鉴读没读全</b>，
    /// 门槛一旦分叉就会变成"多玩一会儿的人反而拿到更差的那个"。
    /// </para>
    /// </summary>
    public const double ShardsForEnding = 130_000;

    /// <summary>「复活人类」额外要求的图鉴条数——拼出一个完整的人，光有碎片不够，还得读过他们怎么活。</summary>
    public const double LoreForRevival = 30;

    /// <summary>
    /// 末层主线完成：走到第 5 次重启，且第 5 层的完成条件成立。<para>
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
