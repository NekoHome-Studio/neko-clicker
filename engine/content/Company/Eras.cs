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
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "garage", "completionHint"),
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
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e8),
                UnlockCondition.AchievementsAtLeast(6)),
            CompletionHint = Prose.Text("eras", "series_a", "completionHint"),
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
}
