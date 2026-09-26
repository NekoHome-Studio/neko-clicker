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
    /// <summary>三层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "garage",
            Name = "第 1 轮 · 车库创业",
            Icon = "🚲",
            Theme = "三个人，一台咖啡机，一个还没写进合同的承诺。",
            EntryText = "车库的门是卷帘的，早上要手动摇上去。她把工位收拾好，问：「今天有活吗？」",
            ExitText = "第一笔订单的钱到账了。你请所有人吃了顿火锅，然后开始想下一笔。",
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "完成第一笔订单：本轮累计赚到 100,000 营收。",
        },
        new()
        {
            Index = 2,
            Id = "series_a",
            Name = "第 2 轮 · A 轮",
            Icon = "🚀",
            Theme = "钱到了，人到了，加班也到了。",
            EntryText = "投资人把这张桌子搬进了新办公室，然后问：「你们能跑多快？」",
            ExitText = "第二轮结束。你签完了所有该签的字，包括几张你没细看的。",
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
            CompletionHint = "本轮累计 100 million，并解锁 6 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "ipo",
            Name = "第 3 轮 · 上市",
            Icon = "🔔",
            Theme = "敲钟之前，只剩最后一个问题：这家公司是谁的。",
            EntryText = "路演厅的椅子摆好了。她站在台上替你调话筒，台下的人开始进场。",
            ExitText = "钟声之后，一切都会写进财报——包括你没写进合同的那部分。",
            // 上市冲刺：规模 ×1.5，且士气越高越快（每点士气 +1%，上限 +100%）。
            Modifiers =
            [
                Modifier.GlobalMultiplier(1.5),
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.01, Cap: 100, Id: MoraleModule.CounterKey)),
            ],
            Completion = FinalCompletion,
            CompletionHint = "本轮累计 500 million，并解锁 12 个成就。",
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
