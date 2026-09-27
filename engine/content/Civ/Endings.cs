using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 三个结局：设计文档给文明定的三个（星际文明 / 停滞 / 自我毁灭）。<para>
/// 这个包既没有立场轴，也没有"表态"这种玩家选择——所以三个结局全都由<b>走得够远</b>这件事区分，
/// 而区分它们的两个指标都是单调不减的：
/// <list type="number">
///   <item><b>星际文明</b>（承诺型 ①）：末层主线完成 <b>且文化 ≥ 100 万</b>。
///     她当然可以把整颗行星的生产力堆到天上，但"有人记得"是另一回事——
///     文化只由五座记录者建筑产出，所以这个结局问的是一个具体的问题：
///     <b>你建了多少座"把这件事记下来"的建筑？</b></item>
///   <item><b>自我毁灭</b>（承诺型 ②）：末层主线完成 <b>且解锁 40 个成就</b>——
///     也就是"走得一样远，但没把文化养到那个高度"。它刻画的不是"你失败了"，
///     而是<b>「她走得够远、也造出了足以把自己抹掉的东西」</b>：
///     陨石、瘟疫、城墙上的攻城术、戴森环——这个包里所有让产量暴涨的东西，
///     同时也都是能让它一夜归零的东西。走完了却没能把文化养成"记得住"的形状，
///     文明就落在自己的技术里。</item>
///   <item><b>停滞</b>（兜底）：只依赖末层主线完成，不含取反、不依赖任何可选行为。
///     主线走完、文化没养起来、成就也没堆到那么高——文明停在了它自己的长度上，
///     没有毁灭，也没有走出去。这是回避一切的玩家永远拿得到的那个。</item>
/// </list>
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，
/// 而这一层的文明还没铺开。实验室包踩过这个坑，这个包把顺序写进条件里。
/// </para>
/// <para>
/// 互斥靠 <see cref="EndingDefinition.Priority"/>：判定时按优先级升序取第一个满足条件的，
/// 记下之后就再也不判。三个条件的构造保证了两两互斥：
/// <b>文化 ≥ 100 万</b>（星际文明）只会由"把记录者建筑养起来"的玩家拿到，
/// 而那条路一旦走上，"没到 100 万"就再也不成立——所以两个承诺型结局不可能同时亮。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>三个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_interstellar",
            Name = "星际文明",
            Icon = "🌌",
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(CultureModule.CounterKey, CultureForInterstellar)),
            Text = "最后一艘船离港那天，她没有去送。她留在学院最下面那间屋子里，"
                   + "把从第一个时代起所有的记录按年份重新排了一遍——石堆上那道痕、"
                   + "第一条写在墙上的规矩、第一本册子、第一张星图。"
                   + "排完之后她发现有一件事很明显：这些东西没有一样是她一个人做的。"
                   + "「原来文明不是我造出来的，」她说，「是它自己长出来的，我只是没让它断掉。」"
                   + "她把这句写在了最后一页。写完，她关上灯，走出门去——外面天已经亮了，"
                   + "而这一次，抬头能看见船。",
        },
        new()
        {
            Id = "end_self_destruction",
            Name = "自我毁灭",
            Icon = "☄️",
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.AchievementsAtLeast(AchievementsForSelfDestruction)),
            Text = "这个文明什么都会造。它能围住一颗恒星，也能让一整条街在一夜之间安静下来——"
                   + "两件事用的是同一双手。她早就该看出来的：洪水冲掉的东西，"
                   + "和城墙挡住的东西，是同一批。"
                   + "最后一次事故没有幸存者名单，因为名单也在里面。"
                   + "到最后只剩下一块烧黑的地基，和上面一个很浅的坑——"
                   + "形状和她第一个时代围出来的那个一模一样。"
                   + "她留下的最后一句记录只有六个字：「下次别造这个。」",
        },
        new()
        {
            Id = "end_stagnation",
            Name = "停滞",
            Icon = "⏳",
            Priority = 100,
            Condition = Finished,
            Text = "文明走到了它自己的长度，然后就不再长了。"
                   + "城墙还是那三道，学院里还是那句「你不必相信我」，星港里停着一艘没造完的船。"
                   + "没有人毁灭它，也没有人再往前推它——它只是每天都和昨天一模一样。"
                   + "很多年以后有个小孩问她：「我们以后会去星星那里吗？」"
                   + "她想了一会儿，说：「我们本来是要去的。」"
                   + "「那为什么没去？」「因为有一天我发现，明天和今天一样，也挺好的。」"
                   + "小孩点点头跑开了。她站在城墙上又看了一会儿，天没有变。",
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
        Ending("ach_end_interstellar", "星际文明", "end_interstellar", "🌌"),
        Ending("ach_end_self_destruction", "自我毁灭", "end_self_destruction", "☄️"),
        Ending("ach_end_stagnation", "停滞", "end_stagnation", "⏳"),
    ];

    /// <summary>
    /// 「有人记得」的门槛。<para>
    /// 它必须<b>严格高于末层完成门槛</b>（<see cref="Eras.FinalCompletion"/> 里的 5 万文化）——
    /// 否则"走完主线"就等于"拿到星际文明"，这个承诺型结局就不再是一个选择，
    /// 而 <see cref="AchievementsForSelfDestruction"/> 那一支会变成结构上不可达的死内容。
    /// 反过来，门槛太高则一次自然游玩拿不到它；1e6 是按实测包络（末层文化到 2e7 上下）定的。
    /// </para>
    /// </summary>
    public const double CultureForInterstellar = 1e6;

    /// <summary>
    /// 「造出了足以抹掉自己的东西」的门槛：末层主线完成时解锁到这么多成就。<para>
    /// 与 <see cref="CultureForInterstellar"/> 互为对照——<b>走得一样远，但没把文化养到那个高度，
    /// 就是毁灭的形状</b>。两个门槛一高一低，所以两个承诺型结局在构造上互斥。
    /// </para>
    /// </summary>
    public const double AchievementsForSelfDestruction = 40;

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

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
