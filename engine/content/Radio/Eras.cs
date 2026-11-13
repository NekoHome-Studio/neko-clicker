using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 一层纪元（C1《猫娘电台》MVP）。转生语义是<b>「开新频段」</b>：上一频段停播，新的频段开始，
/// 按历史累计换取「频段执照」。<para>
/// <b>每一层都带同两条修饰符，因为它们是这个包的核心机制</b>：
/// <c>GlobalMultiplier(0.5)</c>（没人收听的电台等于没播）配上
/// <c>GlobalPercent(Scaling(CustomCounter, 0.00005, Cap: 60000, listenership))</c>
/// （收听率 2 万把乘数拉回 ×1.0、6 万到 ×2.0 封顶）。写到每层而不是"全局规则"，
/// 是因为 <c>EraDefinition.Modifiers</c> 是"本层常驻倍率"的唯一接缝。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>（ROADMAP R3），所以只用累计赚取。收听率<b>不能</b>进来：
/// 它会衰减、点击会掉、每次开新频段还会清零，放进灰按钮会让进度倒退到 0。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>本包的 text.json：纪元文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>两层定义。<paramref name="baseBalance"/> 是内容包的基准数值。<para>
    /// 刻意做两层而不是一层：转生结算要求"有下一层"（<c>Era &lt; MaxEraIndex</c>），
    /// 一层包永远舍不了命、执照恒为 0，永久线就结构性买不起。band_1 → band_2 给机器人
    /// 一次真实的舍命结算，这是"点击是干扰"之外必须保留的转生骨架。
    /// </para></summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "band_1",
            Name = Prose.Text("eras", "band_1", "name"),
            Icon = Prose.Text("eras", "band_1", "icon"),
            Theme = Prose.Text("eras", "band_1", "theme"),
            EntryText = Prose.Text("eras", "band_1", "entryText"),
            ExitText = Prose.Text("eras", "band_1", "exitText"),
            Modifiers = ListenerScaling,
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e9),
            CompletionHint = Prose.Text("eras", "band_1", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "band_2",
            Name = Prose.Text("eras", "band_2", "name"),
            Icon = Prose.Text("eras", "band_2", "icon"),
            Theme = Prose.Text("eras", "band_2", "theme"),
            EntryText = Prose.Text("eras", "band_2", "entryText"),
            ExitText = Prose.Text("eras", "band_2", "exitText"),
            Modifiers = ListenerScaling,
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "band_2", "completionHint"),
        },
    ];

    /// <summary>
    /// 这个包的核心机制，写在每一层上：<b>没人收听的电台等于没播，但永远不至于归零。</b><para>
    /// 乘数 = <c>0.5 × (1 + min(0.005% × 收听率, 300%))</c>：
    /// 收听率 0 → ×0.5（下限，不会归零到死锁）、2 万 → ×1.0、6 万 → ×2.0 封顶。
    /// </para>
    /// </summary>
    private static IReadOnlyList<Modifier> ListenerScaling =>
    [
        Modifier.GlobalMultiplier(0.5),
        Modifier.GlobalPercent(
            0,
            new Scaling(ScalingSource.CustomCounter, 0.00005, Cap: 60_000, Id: BroadcastModule.CounterKey)),
    ];

    /// <summary>
    /// 末层主线完成（第 2 频段的完成条件）。<para>
    /// 单独暴露是给终局判定用的：两个结局都必须等到<b>末层主线完成之后</b>才成立，
    /// 否则玩家一进入末层兜底结局就会立刻触发，而这一频段的听众还没养起来。
    /// 只用累计赚取（单调），不用收听率（会掉）。
    /// </para>
    /// </summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.EarnedThisRunAtLeast(1e9);
}
