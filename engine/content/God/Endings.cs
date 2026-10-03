using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 三个结局：设计文档给神明包定的三个（成为主神 / 变成 meme / 被遗忘）。<para>
/// 这个包没有立场轴（设计矩阵里 #7 只需要 Era + Lore + 第二资源），所以结局不是
/// "你表过什么态"决定的——它由<b>你究竟读完了多少神话</b>决定，这一点与末世包同构
/// （那边挂在"记住了多少"上）：同一套 <see cref="EndingDefinition"/> 既能表达立场驱动的结局，
/// 也能表达"图鉴厚度"驱动的结局，这本身就是"结局是条件树，不是特判"的又一次检验。
/// </para>
/// <para>
/// <b>为什么不用信仰当区分轴</b>：信仰是只涨不花的累加量，而它的量级已经被末层的完成条件钉死了
/// （在线人数 ≥ 2e5 反推出信仰 ≈ 8e6），任何"信仰够不够"的门槛要么恒真、要么让自然跑图
/// 落到兜底——两种都是坏内容。真正区分玩法的是<b>读了多少</b>：一路冲关的人只记得几个梗，
/// 把图鉴读完的人才认得出她是谁。三个结局因此挂在图鉴厚度上，而且三档都够得着。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，而这一层的直播间还没开起来。
/// 实验室包踩过这个坑，九命 / 公司 / 末世 / 图书馆都把顺序写进了条件里。
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
            Id = "end_main_god",
            Name = Prose.Text("endings", "end_main_god", "name"),
            Icon = Prose.Text("endings", "end_main_god", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.LoreAtLeast(LoreForGod)),
            Text = Prose.Text("endings", "end_main_god", "text"),
        },
        new()
        {
            Id = "end_meme",
            Name = Prose.Text("endings", "end_meme", "name"),
            Icon = Prose.Text("endings", "end_meme", "icon"),
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.LoreAtLeast(LoreForMeme)),
            Text = Prose.Text("endings", "end_meme", "text"),
        },
        new()
        {
            Id = "end_forgotten",
            Name = Prose.Text("endings", "end_forgotten", "name"),
            Icon = Prose.Text("endings", "end_forgotten", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_forgotten", "text"),
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
        Ending("ach_end_main_god", "end_main_god"),
        Ending("ach_end_meme", "end_meme"),
        Ending("ach_end_forgotten", "end_forgotten"),
    ];

    /// <summary>
    /// 「成为主神」要求的图鉴条数——把五套神话读全，才有资格谈"主神"这个头衔。<para>
    /// 单独暴露是因为它必须<b>够得着</b>：门槛一旦高于自然游玩读得到的条数，
    /// 这个结局就永远拿不到（阶段 4B 的死内容就是这么来的）。
    /// 它由 <c>GodContentTests.NaturalRunLandsOnThePromiseEnding</c> 守着。
    /// </para>
    /// </summary>
    public const double LoreForGod = 30;

    /// <summary>「变成 meme」要求的图鉴条数——只记得几个梗也算火过。</summary>
    public const double LoreForMeme = 9;

    /// <summary>
    /// 末层主线完成：走到第 5 套神话，且第 5 层的完成条件成立。<para>
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
