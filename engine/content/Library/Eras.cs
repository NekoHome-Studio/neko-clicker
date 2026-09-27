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
    /// <summary>五层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "book_1",
            Name = "第 1 本 · 空白之书",
            Icon = "📄",
            Theme = "书架上有一本一个字都没有的书。她翻开它的时候，第一句话是自己掉下来的。",
            EntryText = "她醒来的时候，手里握着一支笔，面前摊着一本空白的东西。她试着写了一个字，纸把它吃了。",
            ExitText = "第一本写完了。最后一个句号落下的时候，整本书开始变厚——它长出了下一页。",
            Modifiers = ReaderScaling,
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "本轮累计写到 100,000 页。",
        },
        new()
        {
            Index = 2,
            Id = "book_2",
            Name = "第 2 本 · 第一个世界",
            Icon = "🌍",
            Theme = "书里开始有人住了。他们不知道自己是写出来的。",
            EntryText = "第二个世界比第一个大。她这次先画了地图，再往里放人。",
            ExitText = "她把第一个世界合上，放回书架。合上的声音比想象中轻。",
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["reading_room", "copier"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = "本轮累计 15 million，并解锁 4 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "book_3",
            Name = "第 3 本 · 被禁的书",
            Icon = "🔒",
            Theme = "这一本被列进了禁书区。于是它成了唯一一本所有人都读过两遍的书。",
            EntryText = "书脊上被人用红笔划过一道。她把它放进了铁栅栏后面，然后站在外面听。",
            ExitText = "查禁的人来过三次，每次都带走一本，第三次之后书架反而空了——因为大家都藏了一本。",
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
            CompletionHint = "本轮累计 300 million，并解锁 10 个成就。",
        },
        new()
        {
            Index = 4,
            Id = "book_4",
            Name = "第 4 本 · 合订本",
            Icon = "📖",
            Theme = "前三本的人物在同一本书里碰面了。他们互相不认识，但都觉得对方眼熟。",
            EntryText = "她把三本书拆开，按时间顺序重新装订。装到一半她停手了——她发现自己也在里面。",
            ExitText = "合订本厚得拿不动。她说这本不借出去，谁也不借。",
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["index_tower", "printing_house", "world_workshop"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e9),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 4e7)),
            CompletionHint = "本轮累计 6 billion，且峰值产量达到 40 million/s。",
        },
        new()
        {
            Index = 5,
            Id = "book_5",
            Name = "第 5 本 · 最后一页",
            Icon = "🔖",
            Theme = "最后一页写完之后，书会自己合上。合上之后还有没有人读，是她唯一没法控制的事。",
            EntryText = "这一本她写得很慢。每写完一页就停下来摸一摸，像是在确认纸还在。",
            ExitText = "最后一页落笔。笔尖抬起来的那一刻，整座图书馆安静得能听见别人翻页。",
            Modifiers = ReaderScaling,
            UnlocksBuildings = ["endless_shelf"],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计 100 billion，并解锁 18 个成就。",
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
