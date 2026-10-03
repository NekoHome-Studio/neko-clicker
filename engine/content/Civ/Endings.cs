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
    /// <summary>
    /// 本包的 <c>text.json</c>：结局 / 表态 / 立场 / 成就的文案与其它分区<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>三个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_interstellar",
            Name = Prose.Text("endings", "end_interstellar", "name"),
            Icon = Prose.Text("endings", "end_interstellar", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(CultureModule.CounterKey, CultureForInterstellar)),
            Text = Prose.Text("endings", "end_interstellar", "text"),
        },
        new()
        {
            Id = "end_self_destruction",
            Name = Prose.Text("endings", "end_self_destruction", "name"),
            Icon = Prose.Text("endings", "end_self_destruction", "icon"),
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.AchievementsAtLeast(AchievementsForSelfDestruction)),
            Text = Prose.Text("endings", "end_self_destruction", "text"),
        },
        new()
        {
            Id = "end_stagnation",
            Name = Prose.Text("endings", "end_stagnation", "name"),
            Icon = Prose.Text("endings", "end_stagnation", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_stagnation", "text"),
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
        Ending("ach_end_interstellar", "end_interstellar"),
        Ending("ach_end_self_destruction", "end_self_destruction"),
        Ending("ach_end_stagnation", "end_stagnation"),
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

    private static AchievementDefinition Ending(string id, string endingId) => new()
    {
        Id = id,
        Name = Prose.Text("achievements", id, "name"),
        Icon = Prose.Text("achievements", id, "icon"),
        Description = Prose.Text("achievements", id, "description"),
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
