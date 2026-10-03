using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 七批样本 = 七层纪元。转生语义是<b>「开新批次」</b>：实验推倒重来，
/// 但上一批的<b>残留记忆</b>留在档案里（对应永久升级与跨层保留的计数器）。<para>
/// <b>完成条件必须单调不减</b>，否则"舍命"按钮的进度会倒退。所以只用三类指标：
/// 本轮累计赚取（每层归零，层内只增）、成就数、以及两个计数器（<c>peak_cps</c> 与 <c>ethics</c>）。
/// 构建期会强制校验。
/// </para>
/// <para>
/// <b>门槛节奏刻意摊平</b>（这是从内容包 #2 学到的）：那里只有第 3、5 层有产量门槛，
/// 结果 1000 倍的产量爬坡全压在第 5 层，那一层实测要 19.2 小时，邻居只要 1.8 小时。
/// 这里<b>只留一个产量门槛</b>，其余用赚取 / 成就 / 伦理值错开。
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

    /// <summary>七层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "batch_1",
            Name = Prose.Text("eras", "batch_1", "name"),
            Icon = Prose.Text("eras", "batch_1", "icon"),
            Theme = Prose.Text("eras", "batch_1", "theme"),
            EntryText = Prose.Text("eras", "batch_1", "entryText"),
            ExitText = Prose.Text("eras", "batch_1", "exitText"),
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "batch_1", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "batch_2",
            Name = Prose.Text("eras", "batch_2", "name"),
            Icon = Prose.Text("eras", "batch_2", "icon"),
            Theme = Prose.Text("eras", "batch_2", "theme"),
            EntryText = Prose.Text("eras", "batch_2", "entryText"),
            ExitText = Prose.Text("eras", "batch_2", "exitText"),
            // 观测效率提升：这一层整体快 20%。
            Modifiers = [Modifier.GlobalMultiplier(1.2)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = Prose.Text("eras", "batch_2", "completionHint"),
        },
        new()
        {
            Index = 3,
            Id = "batch_3",
            Name = Prose.Text("eras", "batch_3", "name"),
            Icon = Prose.Text("eras", "batch_3", "icon"),
            Theme = Prose.Text("eras", "batch_3", "theme"),
            EntryText = Prose.Text("eras", "batch_3", "entryText"),
            ExitText = Prose.Text("eras", "batch_3", "exitText"),
            // 事故更频繁：基因编辑让世界变得不那么稳定。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.6,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.6,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e6)),
            CompletionHint = Prose.Text("eras", "batch_3", "completionHint"),
        },
        new()
        {
            Index = 4,
            Id = "batch_4",
            Name = Prose.Text("eras", "batch_4", "name"),
            Icon = Prose.Text("eras", "batch_4", "icon"),
            Theme = Prose.Text("eras", "batch_4", "theme"),
            EntryText = Prose.Text("eras", "batch_4", "entryText"),
            ExitText = Prose.Text("eras", "batch_4", "exitText"),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = Prose.Text("eras", "batch_4", "completionHint"),
        },
        new()
        {
            Index = 5,
            Id = "batch_5",
            Name = Prose.Text("eras", "batch_5", "name"),
            Icon = Prose.Text("eras", "batch_5", "icon"),
            Theme = Prose.Text("eras", "batch_5", "theme"),
            EntryText = Prose.Text("eras", "batch_5", "entryText"),
            ExitText = Prose.Text("eras", "batch_5", "exitText"),
            // 伦理值驱动产量：有人看着的时候，实验才做得下去。
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.005, Cap: 100, Id: EthicsModule.CounterKey)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.Counter(EthicsModule.CounterKey, 200)),
            CompletionHint = Prose.Text("eras", "batch_5", "completionHint"),
        },
        new()
        {
            Index = 6,
            Id = "batch_6",
            Name = Prose.Text("eras", "batch_6", "name"),
            Icon = Prose.Text("eras", "batch_6", "icon"),
            Theme = Prose.Text("eras", "batch_6", "theme"),
            EntryText = Prose.Text("eras", "batch_6", "entryText"),
            ExitText = Prose.Text("eras", "batch_6", "exitText"),
            // 记忆手术的代价：情感结算打折，但产量翻倍。
            MetaRewardMultiplier = 0.9,
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e8),
                UnlockCondition.AchievementsAtLeast(16)),
            CompletionHint = Prose.Text("eras", "batch_6", "completionHint"),
        },
        new()
        {
            Index = 7,
            Id = "batch_7",
            Name = Prose.Text("eras", "batch_7", "name"),
            Icon = Prose.Text("eras", "batch_7", "icon"),
            Theme = Prose.Text("eras", "batch_7", "theme"),
            EntryText = Prose.Text("eras", "batch_7", "entryText"),
            ExitText = Prose.Text("eras", "batch_7", "exitText"),
            // 归档是加速的：所有残留记忆同时生效，但增益持续时间减半（高潮也更短）。
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.5),
            ],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "batch_7", "completionHint"),
        },
    ];

    /// <summary>
    /// 最后一批（第 7 批）的完成条件。<para>
    /// 单独暴露出来是给终局判定用的：结局必须等到<b>末层主线完成之后</b>才成立。
    /// 否则玩家一进入第 7 批，兜底结局就立刻触发，而乌托邦 / 共存这两个立场的
    /// 第三次表态机会在第 7 批里——那两个结局会永远拿不到（详见 <c>LabEndingTests</c>）。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.All(
        UnlockCondition.EarnedThisRunAtLeast(1e9),
        UnlockCondition.Counter(EthicsModule.CounterKey, 2000));
}
