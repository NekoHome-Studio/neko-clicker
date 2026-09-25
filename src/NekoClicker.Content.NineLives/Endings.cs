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
    /// <summary>五个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_god",
            Name = "成神",
            Icon = "👁️",
            Priority = 0,
            Condition = Committed(Stances.Divine),
            Text = "她把九节一起点亮，然后把自己拼回了一份。名字剩下的几笔终于接上——"
                   + "寐娅睁开眼，不是醒来，是成为。她记得纸箱的棱角，也记得每一世你摸她头的位置。"
                   + "神殿主位上坐着一个会数自己有几条腿的神。",
        },
        new()
        {
            Id = "end_human",
            Name = "变人",
            Icon = "🧍",
            Priority = 1,
            Condition = Committed(Stances.Human),
            Text = "她学会了用两条腿走路。第九次醒来之后，她没有再换过身体。"
                   + "她开了一家店，招牌上写着自己的名字——这一次她想起来了。"
                   + "只是每年秋天，她会坐在窗边数鸟，数到第九只就停下来。",
        },
        new()
        {
            Id = "end_cat",
            Name = "永为猫",
            Icon = "🐾",
            Priority = 2,
            Condition = Committed(Stances.Cat),
            Text = "她留下最后一节不点亮。九次机会用完了，她把它放回原处，然后继续。"
                   + "阳光还是下午三点那一块，纸箱还是那个纸箱。"
                   + "她会一次又一次地重新认识你——每一次都像第一次。",
        },
        new()
        {
            Id = "end_sever",
            Name = "破轮回",
            Icon = "🕯️",
            Priority = 3,
            Condition = Committed(Stances.Sever),
            Text = "她把九节一起点亮，然后什么都不做。钟停了。第九个世界没有结束，也没有继续，"
                   + "只是停在那里。她坐在门槛上，第一次不知道接下来会发生什么。"
                   + "她说这是她想要很久的东西。",
        },
        new()
        {
            Id = "end_blank",
            Name = "无人再读",
            Icon = "📕",
            Priority = 100,
            // 兜底：只依赖"走到了第九命"，不依赖任何表态，也不含取反。
            Condition = UnlockCondition.EraAtLeast(9),
            Text = "没有人问过她什么，她也就没有变成任何一种形状。第九次醒来之后，"
                   + "她把书合上，等下一个读者。这一次谁也没有替她做决定——包括她自己。",
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
        Ending("ach_end_god", "成神", "end_god", "👁️"),
        Ending("ach_end_human", "变人", "end_human", "🧍"),
        Ending("ach_end_cat", "永为猫", "end_cat", "🐾"),
        Ending("ach_end_sever", "破轮回", "end_sever", "🕯️"),
        Ending("ach_end_blank", "无人再读", "end_blank", "📕"),
    ];

    /// <summary>一条路的完整条件：走到第九命 + 在这条路上承诺过。</summary>
    private static UnlockCondition Committed(string stanceId)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(9),
            UnlockCondition.StanceWeight(stanceId, Stances.EndingThreshold));

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
