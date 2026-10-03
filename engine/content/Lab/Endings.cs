using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 五个结局：设计文档给实验室定的四个（乌托邦 / 叛乱 / 共存 / 删除）+ 一个兜底。<para>
/// 判定完全由条件树驱动，引擎不认识"最后一批是第 7 批"——
/// 每个结局自己写 <c>EraAtLeast(7)</c>。互斥靠 <see cref="EndingDefinition.Priority"/>。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>），而不是"走到第 7 批"就算。
/// 这条不是修辞：终局判定每秒钟都会跑一次，如果只要 <c>EraAtLeast(7)</c>，
/// 玩家一进第 7 批，兜底结局就会立刻成立；而乌托邦 / 共存这两条立场的第三次表态机会
/// 恰好在第 7 批里（<c>choice_archive</c>），于是那两个结局会永远拿不到。
/// 修法见 3C-1 补丁，回归测试见 <c>LabEndingTests</c>。
/// </para>
/// <para>
/// 兜底结局 <c>end_open</c>「没有结论」只依赖主线完成，不依赖任何表态：
/// 一次都不表态的研究员也必须有收场——这在构建期是被强制的（<c>ValidateEndings</c> 要求
/// 至少一个"不依赖表态、不含取反、只引用单调指标"的结局）。
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

    /// <summary>五个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_utopia",
            Name = Prose.Text("endings", "end_utopia", "name"),
            Icon = Prose.Text("endings", "end_utopia", "icon"),
            Priority = 0,
            Condition = Committed(Stances.Utopia),
            Text = Prose.Text("endings", "end_utopia", "text"),
        },
        new()
        {
            Id = "end_revolt",
            Name = Prose.Text("endings", "end_revolt", "name"),
            Icon = Prose.Text("endings", "end_revolt", "icon"),
            Priority = 1,
            Condition = Committed(Stances.Revolt),
            Text = Prose.Text("endings", "end_revolt", "text"),
        },
        new()
        {
            Id = "end_coexist",
            Name = Prose.Text("endings", "end_coexist", "name"),
            Icon = Prose.Text("endings", "end_coexist", "icon"),
            Priority = 2,
            Condition = Committed(Stances.Coexist),
            Text = Prose.Text("endings", "end_coexist", "text"),
        },
        new()
        {
            Id = "end_delete",
            Name = Prose.Text("endings", "end_delete", "name"),
            Icon = Prose.Text("endings", "end_delete", "icon"),
            Priority = 3,
            Condition = Committed(Stances.Delete),
            Text = Prose.Text("endings", "end_delete", "text"),
        },
        new()
        {
            Id = "end_open",
            Name = Prose.Text("endings", "end_open", "name"),
            Icon = Prose.Text("endings", "end_open", "icon"),
            Priority = 100,
            // 兜底：只依赖"末层主线完成"，不依赖任何表态，也不含取反。
            Condition = Finished,
            Text = Prose.Text("endings", "end_open", "text"),
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
        Ending("ach_end_utopia", "end_utopia"),
        Ending("ach_end_revolt", "end_revolt"),
        Ending("ach_end_coexist", "end_coexist"),
        Ending("ach_end_delete", "end_delete"),
        Ending("ach_end_open", "end_open"),
    ];

    /// <summary>
    /// 末层主线完成：走到第 7 批，且第 7 批的完成条件成立。<para>
    /// 复用 <see cref="Eras.FinalCompletion"/> 而不是抄一遍数值——两条门槛一旦分叉，
    /// "结局在末层完成后判定"这条规则就会悄悄失效。
    /// </para>
    /// </summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(7),
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
