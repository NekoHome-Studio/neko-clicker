using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 两个结局：设计文档给图书馆定的两个（被读到最后 / 无人再读）。<para>
/// 这个包既没有立场轴，也没有"记忆量"那种单向累积的资源——它只有一个<b>会掉</b>的指标：
/// 被阅读度。所以"被读到最后"不是"你攒够了"，而是<b>「你合上书的时候，还有人正在读」</b>：
/// 条件里含一个被阅读度门槛，而它在每次开新书时清零、在没人读的时候衰减。
/// 这是这个包唯一一个"必须持续维持才成立"的结局。
/// </para>
/// <para>
/// 兜底结局 <c>end_no_one_reads</c> 只依赖主线完成，不含取反、不依赖被阅读度——
/// 构建期强制要求存在这样一个结局，玩家回避不了它，也就不可能卡住。
/// 它同时也是"虚无化"的终局形态：书写完了，但合上的时候没有人在读。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 本兜底结局就会立刻成立，
/// 而这一本的读者还没养起来。实验室包踩过这个坑，这个包把顺序写进条件里。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>两个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_read_to_the_end",
            Name = "被读到最后",
            Icon = "🕯️",
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(ReadershipModule.CounterKey, ReadershipForEnding)),
            Text = "最后一本写完那天，闭馆时间早就过了，阅览室还亮着两盏灯："
                   + "一盏是她开的，一盏是那个读到最后一页的人开的。"
                   + "她没有过去，也没有催，只是在门口站了一会儿，"
                   + "然后转身去做了一件事——她给自己也办了一张借阅卡，"
                   + "编号是最后一个，名字那一栏写的是「读者」。",
        },
        new()
        {
            Id = "end_no_one_reads",
            Name = "无人再读",
            Icon = "🌑",
            Priority = 100,
            Condition = Finished,
            Text = "五本书都写完了，书架上排得整整齐齐。合上最后一页的那天晚上，"
                   + "阅览室一个人也没有——不是没人来，是她把灯关早了。"
                   + "后来那些书一直在那儿，借阅卡上最晚的一个日期停在很多年以前。"
                   + "有人问她后不后悔，她说：「我写的时候，它们是真的。」"
                   + "说完她把灯又关了，这次是最后一盏。",
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
        Ending("ach_end_read_to_the_end", "被读到最后", "end_read_to_the_end", "🕯️"),
        Ending("ach_end_no_one_reads", "无人再读", "end_no_one_reads", "🌑"),
    ];

    /// <summary>
    /// 「合上书的时候还有人正在读」的门槛。<para>
    /// 单独暴露出来是因为它必须<b>够得着</b>——被阅读度会衰减、每本书开局还是 0，
    /// 门槛一旦高于这个包在真实曲线里养得起的读者量，那个结局就永远拿不到。
    /// 它由 <c>LibraryContentTests.ReadershipAtTheEnd_ExceedsTheEndingThreshold</c> 守着。
    /// </para>
    /// </summary>
    public const double ReadershipForEnding = 60_000;

    /// <summary>
    /// 末层主线完成：走到第 5 本，且第 5 本的完成条件成立。<para>
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
