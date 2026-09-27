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
    /// <summary>四个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_ipo",
            Name = "上市",
            Icon = "🔔",
            Priority = 0,
            Condition = Committed(Stances.Ipo),
            Text = "钟声在九点三十分响起。你站在台上，身后是她的名字和整个团队的名单。"
                   + "闪光灯亮成一片的时候，你忽然想起车库那扇要手动摇起来的卷帘门——"
                   + "那扇门后来被拆掉了，没人记得是谁拆的。",
        },
        new()
        {
            Id = "end_union",
            Name = "工会胜利",
            Icon = "✊",
            Priority = 1,
            Condition = Committed(Stances.Union),
            Text = "上市材料里夹着一份章程，第一页写着：重大事项须经工会同意。"
                   + "投行的人问这是不是必须的，你说：「是。」"
                   + "那天下午，她在办公室里贴了张纸：「这家公司有主，主是我们。」",
        },
        new()
        {
            Id = "end_liquidate",
            Name = "破产清算",
            Icon = "🧳",
            Priority = 2,
            Condition = Committed(Stances.Liquidate),
            Text = "清算完成的那个下午，财务把最后一笔钱打给了每一个人，包括已经离职的。"
                   + "钥匙交还给房东，灯一盏一盏关掉。她最后一个走，出门前回头看了一眼，"
                   + "然后说：「其实这样也挺好——至少没有人是被扔下的。」",
        },
        new()
        {
            Id = "end_fade",
            Name = "无疾而终",
            Icon = "📄",
            Priority = 100,
            Condition = Finished,
            Text = "公司活了下来，不好也不坏。没有人问过你那些问题，所以你一个也没有回答。"
                   + "几年后的一次聚会上，有人提起这家公司，想了半天才说：「哦，他们啊，还在。」"
                   + "她坐在角落里笑了笑，没说话。",
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
        Ending("ach_end_ipo", "上市", "end_ipo", "🔔"),
        Ending("ach_end_union", "工会胜利", "end_union", "✊"),
        Ending("ach_end_liquidate", "破产清算", "end_liquidate", "🧳"),
        Ending("ach_end_fade", "无疾而终", "end_fade", "📄"),
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

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
