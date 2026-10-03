using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 三轮公司阶段 = 三层纪元。转生语义是<b>「重组」</b>：公司推倒重来，
/// 上一轮学到的经验变成「期权」（对应永久升级与跨层保留的计数器）。<para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取与成就数这两类指标：
/// 士气虽然更有本包特色，但它会被加班吃掉，放进完成条件会让灰按钮的进度倒退。
/// 构建期会强制校验。
/// </para>
/// <para>
/// 三轮的门槛刻意摊平：第 1 轮 1e5（第一笔订单）、第 2 轮 1e8（A 轮）、
/// 第 3 轮 5e8（上市）。末轮的完成条件由 <see cref="FinalCompletion"/> 单独暴露，
/// 终局判定复用它——结局必须等到"末层主线完成"才成立（详见 <c>CompanyEndingTests</c> 与
/// 实验室包的同类修复）。
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

    /// <summary>第一轮（车库）的完成条件。</summary>
    private static UnlockCondition GarageCompletion => UnlockCondition.EarnedThisRunAtLeast(1e5);

    /// <summary>第二轮（A 轮）的完成条件。</summary>
    private static UnlockCondition SeriesACompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e8),
        UnlockCondition.AchievementsAtLeast(6));

    /// <summary>三层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "garage",
            Name = Prose.Text("eras", "garage", "name"),
            Icon = Prose.Text("eras", "garage", "icon"),
            Theme = Prose.Text("eras", "garage", "theme"),
            EntryText = Prose.Text("eras", "garage", "entryText"),
            ExitText = Prose.Text("eras", "garage", "exitText"),
            Completion = GarageCompletion,
            CompletionHint = Prose.Text("eras", "garage", "completionHint"),
            Stages = StagesWithin(GarageCompletion),
        },
        new()
        {
            Index = 2,
            Id = "series_a",
            Name = Prose.Text("eras", "series_a", "name"),
            Icon = Prose.Text("eras", "series_a", "icon"),
            Theme = Prose.Text("eras", "series_a", "theme"),
            EntryText = Prose.Text("eras", "series_a", "entryText"),
            ExitText = Prose.Text("eras", "series_a", "exitText"),
            // A 轮之后全员加班：士气模块会额外扣一笔（MoraleModule.OvertimeDrainPerSecond）。
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            // 投资人消息多：事件来得更密。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.7,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.7,
            },
            Completion = SeriesACompletion,
            CompletionHint = Prose.Text("eras", "series_a", "completionHint"),
            Stages = StagesWithin(SeriesACompletion),
        },
        new()
        {
            Index = 3,
            Id = "ipo",
            Name = Prose.Text("eras", "ipo", "name"),
            Icon = Prose.Text("eras", "ipo", "icon"),
            Theme = Prose.Text("eras", "ipo", "theme"),
            EntryText = Prose.Text("eras", "ipo", "entryText"),
            ExitText = Prose.Text("eras", "ipo", "exitText"),
            // 上市冲刺：规模 ×1.5，且士气越高越快（每点士气 +1%，上限 +100%）。
            Modifiers =
            [
                Modifier.GlobalMultiplier(1.5),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.01, Cap: 100, Id: MoraleModule.CounterKey)),
            ],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "ipo", "completionHint"),
            Stages = StagesWithin(FinalCompletion),
        },
    ];

    /// <summary>
    /// 最后一轮（第 3 轮）的完成条件。<para>
    /// 单独暴露是给终局判定用的：结局必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 3 轮，兜底结局就会立刻触发，而这一轮里的两次表态
    /// （工会 / 敲钟）还没到手——实验室包踩过这个坑，这里从设计上就避开。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(5e8),
        UnlockCondition.AchievementsAtLeast(12));

    /// <summary>
    /// 本层的阶段边界 = 「这一层里又能买到一座新建筑」的那些门槛，按门槛升序。<para>
    /// <b>为什么是建筑解锁线</b>：<c>TUNING_ANALYSIS</c> §四量出来的"重走"痛点精确地是这个
    /// ——第 2、3 轮把第 1 轮那四座按<b>同一顺序</b>再买一遍，而"第一次买到上一轮没有的东西"
    /// 要等到本层的 24%~69%。所以"又有一座新的能买了"就是玩家真的感到这一轮往前走了的那一刻，
    /// 也是这一层里唯一值得当阶段的线。
    /// </para>
    /// <para>
    /// <b>阶段与解锁线是同一条线</b>：<see cref="EraStage.At"/> 直接取 <c>building.Unlock</c>
    /// 那个对象，不是照抄一个数字——于是改解锁门槛就等于改阶段，两者不可能各自漂移。
    /// 门槛<b>严格低于</b>本层完成门槛的才算：第 3 轮的 <c>headquarters</c> 解锁在 1e9，
    /// 而第 3 轮的完成门槛是 5e8，把它写进阶段等于承诺一个正常流程里到不了的阶段。
    /// </para>
    /// </summary>
    private static EraStage[] StagesWithin(UnlockCondition completion)
    {
        double ceiling = Ceiling(completion);

        return
        [
            .. Buildings.All
                .Select(building => (building, Threshold: Threshold(building.Unlock)))
                .Where(x => x.Threshold is { } threshold && threshold < ceiling)
                .OrderBy(x => x.Threshold)
                .Select(x => new EraStage
                {
                    Id = x.building.Id,
                    Name = x.building.Name,
                    Icon = x.building.Icon,
                    At = x.building.Unlock,
                }),
        ];
    }

    /// <summary>完成条件里的「本轮累计赚取」门槛；没有这条叶子时为 0（那就一个阶段都不声明）。</summary>
    private static double Ceiling(UnlockCondition completion)
        => completion.NumericLeaves()
            .Where(leaf => leaf.Metric == NumericMetric.CookiesEarnedThisRun)
            .Select(leaf => leaf.Target)
            .DefaultIfEmpty(0)
            .Min();

    /// <summary>解锁条件里的「本轮累计赚取」门槛；不是这一类（例如无条件）时为 <c>null</c>。</summary>
    private static double? Threshold(UnlockCondition unlock)
        => unlock.NumericLeaves()
            .Where(leaf => leaf.Metric == NumericMetric.CookiesEarnedThisRun)
            .Select(leaf => (double?)leaf.Target)
            .FirstOrDefault();
}
