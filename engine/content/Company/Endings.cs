using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 四个结局：设计文档给公司定的三个（上市 / 工会胜利 / 破产清算）+ 一个兜底。<para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>），而不是"进入第 3 轮"就算。
/// 终局判定每个检查周期都跑一次，若只要 <c>EraAtLeast(3)</c>，玩家一进第 3 轮兜底结局
/// 就会立刻成立；而这一轮里的两次表态（工会 / 敲钟）还没到手。实验室包踩过这个坑
/// （见其 3C-1 补丁），这个包把顺序写进条件里。
/// </para>
/// <para>
/// 互斥靠 <see cref="EndingDefinition.Priority"/>；兜底结局 <c>end_fade</c>「无疾而终」
/// 只依赖主线完成，不含取反、不依赖表态——构建期强制要求存在这样一个结局。
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

    /// <summary>四个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_ipo",
            Name = Prose.Text("endings", "end_ipo", "name"),
            Icon = Prose.Text("endings", "end_ipo", "icon"),
            Priority = 0,
            Condition = Committed(Stances.Ipo),
            Text = Prose.Text("endings", "end_ipo", "text"),
        },
        new()
        {
            Id = "end_union",
            Name = Prose.Text("endings", "end_union", "name"),
            Icon = Prose.Text("endings", "end_union", "icon"),
            Priority = 1,
            Condition = Committed(Stances.Union),
            Text = Prose.Text("endings", "end_union", "text"),
        },
        new()
        {
            Id = "end_liquidate",
            Name = Prose.Text("endings", "end_liquidate", "name"),
            Icon = Prose.Text("endings", "end_liquidate", "icon"),
            Priority = 2,
            Condition = Committed(Stances.Liquidate),
            Text = Prose.Text("endings", "end_liquidate", "text"),
        },
        new()
        {
            Id = "end_fade",
            Name = Prose.Text("endings", "end_fade", "name"),
            Icon = Prose.Text("endings", "end_fade", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_fade", "text"),
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
        Ending("ach_end_ipo", "end_ipo"),
        Ending("ach_end_union", "end_union"),
        Ending("ach_end_liquidate", "end_liquidate"),
        Ending("ach_end_fade", "end_fade"),
    ];

    /// <summary>
    /// 末层主线完成：走到第 3 轮，且第 3 轮的完成条件成立。<para>
    /// 复用 <see cref="Eras.FinalCompletion"/> 而不是抄一遍数值——两条门槛一旦分叉，
    /// "结局在末层完成后判定"这条规则就会悄悄失效。
    /// </para>
    /// </summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(3),
            Eras.FinalCompletion);

    /// <summary>一条路的完整条件：末层主线完成 + 在这条路上承诺过。</summary>
    private static UnlockCondition Committed(string stanceId)
        => UnlockCondition.All(
            Finished,
            UnlockCondition.StanceWeight(stanceId, Stances.EndingThreshold));

    private static AchievementDefinition Ending(string id, string endingId) => new()
    {
        Id = id,
        Name = Prose.Text("achievements", id, "name"),
        Icon = Prose.Text("achievements", id, "icon"),
        Description = Prose.Text("achievements", id, "description"),
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
