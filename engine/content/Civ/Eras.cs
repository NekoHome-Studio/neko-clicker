using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 五个时代 = 五层纪元。转生语义是<b>「跨入下一个时代」</b>：上一段文明落幕，
/// 新的时代从零开始，按历史累计换取「火种」。<para>
/// <b>每一层都带同一条修饰符，因为它是这个包的核心机制</b>：
/// <c>GlobalPercent(Scaling(CustomCounter, 0.00002, Cap: 400_000, culture))</c>
/// ——每点文化给全局 +0.002%，最多到 +800%。文化<b>跨时代不清零</b>，
/// 所以"她记得住的东西"是唯一一条不随时代重来的成长曲线。
/// </para>
/// <para>
/// <b>四层不同的手感不是换皮，是换规则</b>（都用 <c>EraDefinition</c> 已有的接缝，
/// 没有一处特判）：
/// <list type="bullet">
///   <item>第 1 层：点击的性价比最高（<c>ClickBasePower</c> 2.5）、天灾来得最勤（×0.7 间隔）。</item>
///   <item>第 2 层：集市类建筑 ×2.5，天灾再勤一点（×0.8）——交换与记录把文明拉起来。</item>
///   <item>第 3 层：全局 ×2.5，但天灾间隔拉长到 ×1.25（战争与瘟疫一次顶几次）。</item>
///   <item>第 4 层：离线上限 ×2（四个小时，学院夜里也在算），文化门槛第一次进完成条件。</item>
///   <item>第 5 层：全局 ×5.5、离线上限 ×3、天灾最频繁（×0.5）——末层的规则就是"什么都快"。</item>
/// </list>
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取 / 成就数 / 文化 / 峰值产量。
/// 文化是单调不减的（<see cref="CultureModule"/> 只累加、跨时代不清零），所以它可以进完成条件——
/// 这与 #9 图书馆那条"会掉的第二资源不能进完成条件"正好相反，差别在于单调性，
/// 而构建期只拦得住 <c>Cps</c> 那类明令禁止的指标，计数器靠内容自觉。
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
            Id = "stone",
            Name = Prose.Text("eras", "stone", "name"),
            Icon = Prose.Text("eras", "stone", "icon"),
            Theme = Prose.Text("eras", "stone", "theme"),
            EntryText = Prose.Text("eras", "stone", "entryText"),
            ExitText = Prose.Text("eras", "stone", "exitText"),
            // 这个时代只有爪子：点击的性价比必须够高，否则开局十分钟什么都推不动。
            Balance = baseBalance with
            {
                ClickBasePower = 2.5,
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.7,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.7,
            },
            Modifiers = CultureScaling,
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e5),
                UnlockCondition.AchievementsAtLeast(3)),
            CompletionHint = Prose.Text("eras", "stone", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "village",
            Name = Prose.Text("eras", "village", "name"),
            Icon = Prose.Text("eras", "village", "icon"),
            Theme = Prose.Text("eras", "village", "theme"),
            EntryText = Prose.Text("eras", "village", "entryText"),
            ExitText = Prose.Text("eras", "village", "exitText"),
            Balance = baseBalance with
            {
                ClickBasePower = 2.0,
                // 集市开张了：交换比什么都来得快，天灾也来得更勤。
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.8,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.8,
            },
            Modifiers = [.. CultureScaling, Modifier.BuildingMultiplier("market", 2.5)],
            UnlocksBuildings = ["village", "market"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e7),
                UnlockCondition.AchievementsAtLeast(5),
                UnlockCondition.Counter(CultureModule.CounterKey, 4e4)),
            CompletionHint = Prose.Text("eras", "village", "completionHint"),
        },
        new()
        {
            Index = 3,
            Id = "empire",
            Name = Prose.Text("eras", "empire", "name"),
            Icon = Prose.Text("eras", "empire", "icon"),
            Theme = Prose.Text("eras", "empire", "theme"),
            EntryText = Prose.Text("eras", "empire", "entryText"),
            ExitText = Prose.Text("eras", "empire", "exitText"),
            // 战争与瘟疫：事件来得稀，但每次更狠——它的负面权重也更高。
            Balance = baseBalance with
            {
                ClickBasePower = 1.0,
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 1.25,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 1.25,
            },
            Modifiers = [.. CultureScaling, Modifier.GlobalMultiplier(2.5)],
            UnlocksBuildings = ["city_wall"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.AchievementsAtLeast(8),
                // 文化第一次成为瓶颈：文明不只是「更大」，还得「记得住」。
                UnlockCondition.Counter(CultureModule.CounterKey, 6e5)),
            CompletionHint = Prose.Text("eras", "empire", "completionHint"),
        },
        new()
        {
            Index = 4,
            Id = "enlightenment",
            Name = Prose.Text("eras", "enlightenment", "name"),
            Icon = Prose.Text("eras", "enlightenment", "icon"),
            Theme = Prose.Text("eras", "enlightenment", "theme"),
            EntryText = Prose.Text("eras", "enlightenment", "entryText"),
            ExitText = Prose.Text("eras", "enlightenment", "exitText"),
            Balance = baseBalance with
            {
                ClickBasePower = 0.5,
                // 学院夜里也在算：离线收益的上限翻倍。
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2,
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 2,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 2,
            },
            Modifiers = [.. CultureScaling, Modifier.GlobalMultiplier(4)],
            UnlocksBuildings = ["academy", "temple"],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(6e9),
                UnlockCondition.Counter(CultureModule.CounterKey, 8e5),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 4e7)),
            CompletionHint = Prose.Text("eras", "enlightenment", "completionHint"),
        },
        new()
        {
            Index = 5,
            Id = "starport",
            Name = Prose.Text("eras", "starport", "name"),
            Icon = Prose.Text("eras", "starport", "icon"),
            Theme = Prose.Text("eras", "starport", "theme"),
            EntryText = Prose.Text("eras", "starport", "entryText"),
            ExitText = Prose.Text("eras", "starport", "exitText"),
            Balance = baseBalance with
            {
                ClickBasePower = 0.3,
                // 末层的规则就是"什么都快"：离线上限再 ×3（六小时），天灾最频繁。
                OfflineCapSeconds = baseBalance.OfflineCapSeconds * 3,
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.5,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.5,
            },
            Modifiers = [.. CultureScaling, Modifier.GlobalMultiplier(5.5)],
            UnlocksBuildings = ["star_port", "spirit_bridge", "deep_space_relay"],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "starport", "completionHint"),
        },
    ];

    /// <summary>
    /// 这个包的核心机制，写在每一层上：<b>她记得住的东西会变成产能。</b>
    /// <para>
    /// 全局产量 <c>+0.002% × min(文化, 400,000)</c>，最多 +800%。
    /// 文化跨时代不清零，所以这一条在第 1 层几乎看不出来（那时她什么都没有），
    /// 到了第 5 层则是整条曲线里最厚的一层复利——这正是"文明"该有的形状。
    /// </para>
    /// </summary>
    private static IReadOnlyList<Modifier> CultureScaling =>
    [
        Modifier.GlobalPercent(
            0,
            new Scaling(ScalingSource.CustomCounter, 0.00002, Cap: 400_000, Id: CultureModule.CounterKey)),
    ];

    /// <summary>
    /// 最后一个时代（第 5 层）的完成条件。<para>
    /// 单独暴露是给终局判定用的：三个结局都必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 5 层，兜底结局就会立刻触发，而这一层的文明还没铺开——
    /// 实验室包踩过这个坑（见 <c>LabEndingTests</c>）。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e11),
        UnlockCondition.Counter(CultureModule.CounterKey, 5e4),
        UnlockCondition.AchievementsAtLeast(20));
}
