using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 五个结局：四个"承诺"，一个兜底。<para>
/// 四条路分别对应 <c>nine_17</c>~<c>nine_20</c> 那四段"她试过"的伏笔——
/// 那四段写的是她曾经设想过什么，这里写的是她最后走了哪一条。
/// </para>
/// <para>
/// <b>判定完全由条件树驱动</b>，引擎不认识"最后一层是第 9 层"：
/// 每个结局自己写 <c>EraAtLeast(9)</c>。互斥靠 <see cref="EndingDefinition.Priority"/>——
/// 升序取第一个满足的。四条路各自要求 5 点权重、而每条只有 3 次表态机会，
/// 所以四个条件在构造上就<b>不可能同时成立</b>（每次表态都是在互斥的选项之间二选一）。
/// </para>
/// <para>
/// 兜底结局 <c>end_blank</c> 没有条件以外的依赖：玩家一次都不表态也能走到。
/// 构建期会强制"至少有一个这种结局"（<c>ValidateEndings</c>），
/// 否则回避表态的玩家会走完主线却没有结局。
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
            Id = "end_god",
            Name = Prose.Text("endings", "end_god", "name"),
            Icon = Prose.Text("endings", "end_god", "icon"),
            Priority = 0,
            Condition = Committed(Stances.Divine),
            Text = Prose.Text("endings", "end_god", "text"),
        },
        new()
        {
            Id = "end_human",
            Name = Prose.Text("endings", "end_human", "name"),
            Icon = Prose.Text("endings", "end_human", "icon"),
            Priority = 1,
            Condition = Committed(Stances.Human),
            Text = Prose.Text("endings", "end_human", "text"),
        },
        new()
        {
            Id = "end_cat",
            Name = Prose.Text("endings", "end_cat", "name"),
            Icon = Prose.Text("endings", "end_cat", "icon"),
            Priority = 2,
            Condition = Committed(Stances.Cat),
            Text = Prose.Text("endings", "end_cat", "text"),
        },
        new()
        {
            Id = "end_sever",
            Name = Prose.Text("endings", "end_sever", "name"),
            Icon = Prose.Text("endings", "end_sever", "icon"),
            Priority = 3,
            Condition = Committed(Stances.Sever),
            Text = Prose.Text("endings", "end_sever", "text"),
        },
        new()
        {
            Id = "end_blank",
            Name = Prose.Text("endings", "end_blank", "name"),
            Icon = Prose.Text("endings", "end_blank", "icon"),
            Priority = 100,
            // 兜底：只依赖"走到了第九命"，不依赖任何表态，也不含取反。
            Condition = UnlockCondition.EraAtLeast(9),
            Text = Prose.Text("endings", "end_blank", "text"),
        },
    ];

    /// <summary>
    /// 结局成就。<para>
    /// 它们自己声明 <c>Unlock = EndingReached(...)</c>，走常规的成就检查路径解锁——
    /// 结局不需要知道"谁是它的成就"，引擎也不需要针对结局加成就的特判。
    /// </para>
    /// </summary>
    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_god", "end_god"),
        Ending("ach_end_human", "end_human"),
        Ending("ach_end_cat", "end_cat"),
        Ending("ach_end_sever", "end_sever"),
        Ending("ach_end_blank", "end_blank"),
    ];

    /// <summary>一条路的完整条件：走到第九命 + 在这条路上承诺过。</summary>
    private static UnlockCondition Committed(string stanceId)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(9),
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
