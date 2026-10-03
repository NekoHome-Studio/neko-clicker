using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Library;

/// <summary>
/// 五本书 = 五层纪元。转生语义是<b>「开新书」</b>：上一本书合上，新的世界开始，
/// 按历史累计换取「书签」。<para>
/// <b>每一层都带同两条修饰符，因为它们是这个包的核心机制</b>：
/// <c>GlobalMultiplier(0.5)</c>（没人读的书等于没写）配上
/// <c>GlobalPercent(Scaling(CustomCounter, 0.005, Cap: 300, readership))</c>
/// （每 2 万被阅读度把乘数拉回 ×1.0，6 万到 ×2.0 封顶）。
/// 写到每层而不是写成"全局规则"，是因为 <c>EraDefinition.Modifiers</c> 是
/// "本层常驻倍率"的唯一接缝——内容侧没有"全包常驻修饰符"这种东西，
/// 而为一个包去加它，正是 A3 禁止的"为需求打补丁"。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取 / 成就数 / 峰值产量。
/// 被阅读度<b>不能</b>进来：它会衰减、而且每次开新书都会清零（<c>ReadershipModule.OnAscend</c>），
/// 放进灰按钮会让进度倒退到 0。构建期拦不住这一条（计数器在白名单里），靠内容自觉。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>
    /// 本包的 <c>text.json</c>：纪元文案与剧情散文、建筑文案<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "book_1",
            Name = Prose.Text("eras", "book_1", "name"),
            Icon = Prose.Text("eras", "book_1", "icon"),
            Theme = Prose.Text("eras", "book_1", "theme"),
            EntryText = Prose.Text("eras", "book_1", "entryText"),
            ExitText = Prose.Text("eras", "book_1", "exitText"),
            Modifiers = ReaderScaling,
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "book_1", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "book_2",
            Name = Prose.Text("eras", "book_2", "name"),
            Icon = Prose.Text("eras", "book_2", "icon"),
            Theme = Prose.Text("eras", "book_2", "theme"),
            EntryText = Prose.Text("eras", "book_2", "entryText"),
            ExitText = Prose.Text("eras", "book_2", "exitText"),
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["reading_room", "copier"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = Prose.Text("eras", "book_2", "completionHint"),
        },
        new()
        {
            Index = 3,
            Id = "book_3",
            Name = Prose.Text("eras", "book_3", "name"),
            Icon = Prose.Text("eras", "book_3", "icon"),
            Theme = Prose.Text("eras", "book_3", "theme"),
            EntryText = Prose.Text("eras", "book_3", "entryText"),
            ExitText = Prose.Text("eras", "book_3", "exitText"),
            // 查禁的年份：事件来得稀，但每次更狠。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 1.4,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 1.4,
            },
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["banned_section"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = Prose.Text("eras", "book_3", "completionHint"),
        },
        new()
        {
            Index = 4,
            Id = "book_4",
            Name = Prose.Text("eras", "book_4", "name"),
            Icon = Prose.Text("eras", "book_4", "icon"),
            Theme = Prose.Text("eras", "book_4", "theme"),
            EntryText = Prose.Text("eras", "book_4", "entryText"),
            ExitText = Prose.Text("eras", "book_4", "exitText"),
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["index_tower", "printing_house", "world_workshop"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e9),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 4e7)),
            CompletionHint = Prose.Text("eras", "book_4", "completionHint"),
        },
        new()
        {
            Index = 5,
            Id = "book_5",
            Name = Prose.Text("eras", "book_5", "name"),
            Icon = Prose.Text("eras", "book_5", "icon"),
            Theme = Prose.Text("eras", "book_5", "theme"),
            EntryText = Prose.Text("eras", "book_5", "entryText"),
            ExitText = Prose.Text("eras", "book_5", "exitText"),
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["endless_shelf"],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "book_5", "completionHint"),
        },
    ];

    /// <summary>
    /// 这个包的核心机制，写在每一层上：<b>没人读的书等于没写，但永远不至于归零。</b>
    /// <para>
    /// 乘数 = <c>0.5 × (1 + min(0.5% × 被阅读度, 300%))</c>：
    /// 被阅读度 0 → ×0.5（下限，验收 ② 要的"不会归零到死锁"）、2 万 → ×1.0、6 万 → ×2.0 封顶。
    /// </para>
    /// </summary>
    private static IReadOnlyList<Modifier> ReaderScaling =>
    [
        Modifier.GlobalMultiplier(0.5),
        Modifier.GlobalPercent(
            0,
            new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 60_000, Id: ReadershipModule.CounterKey)),
    ];

    /// <summary>
    /// 最后一本书（第 5 层）的完成条件。<para>
    /// 单独暴露是给终局判定用的：两个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 5 层，兜底结局就会立刻触发，而这一层的读者还没养起来——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>）。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e11),
        UnlockCondition.AchievementsAtLeast(18));
}
