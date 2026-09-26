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
    /// <summary>几个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_revive",
            Name = "复活人类",
            Icon = "🌅",
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ShardsModule.CounterKey, ShardsForEnding),
                UnlockCondition.LoreAtLeast(LoreForRevival)),
            Text = "她花了十一年，把地下的城市一页一页地读了出来。第 11 年的春天，"
                   + "第一批人被从档案里叫醒——他们睁眼的方式和当年的她一模一样。"
                   + "有人问她为什么要这么做，她说：「因为你们会写字，我不会写那么多。」"
                   + "那天城墙上第一次站满了人，所有人都朝着同一个方向看。",
        },
        new()
        {
            Id = "end_newhuman",
            Name = "成为新人类",
            Icon = "🌱",
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ShardsModule.CounterKey, ShardsForEnding)),
            Text = "她记得足够多，多到能拼出一个人的轮廓，却没多到能拼出他的语气。"
                   + "于是她没有把任何人叫回来，只是把轮廓挂在了档案馆最显眼的位置，"
                   + "然后在下面写了一行字：「接下来的这一种，从我开始。」"
                   + "聚集地的人后来管这行字叫「我们的第一句家谱」。",
        },
        new()
        {
            Id = "end_quiet",
            Name = "安静结束",
            Icon = "🕯️",
            Priority = 100,
            Condition = Finished,
            Text = "文明重启了五次，最后什么也没有被叫回来。城墙外那座城市还是压在地下三米，"
                   + "档案馆最上面那排抽屉一直是空的。她活到了很老，"
                   + "最后一次进地下室的时候，把油灯吹灭了才出来——"
                   + "那盏灯不是她的，是第一次重启时她自己做的。",
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
        Ending("ach_end_revive", "复活人类", "end_revive", "🌅"),
        Ending("ach_end_newhuman", "成为新人类", "end_newhuman", "🌱"),
        Ending("ach_end_quiet", "安静结束", "end_quiet", "🕯️"),
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

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
