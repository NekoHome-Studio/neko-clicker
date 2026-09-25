namespace NekoClicker.Core.Content;

/// <summary>
/// 一个"结局"。<para>
/// 终局判定复用条件树：<see cref="Condition"/> 成立即达成。这样引擎就不需要认识
/// "哪一层是最后一层"——想表达"走完主线"就写 <c>EraAtLeast(9)</c>，
/// 想表达"某个立场占了上风"就写 <c>StanceWeight("god", 3)</c>，
/// 想表达"某个关键真相没读到"就写 <c>Not(LoreAtLeast(n))</c>。
/// </para>
/// <para>
/// <b>互斥由 <see cref="Priority"/> 保证</b>：判定时按 Priority 从小到大取第一个满足条件的，
/// 记下之后就再也不判了。所以一份存档只会有一个结局——要探索其它结局得开新存档
/// （立场权重跨转生保留，这是刻意的：选择是"发生过的事"）。
/// </para>
/// </summary>
public sealed record EndingDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>终局文本。</summary>
    public required string Text { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "🌌";

    /// <summary>达成条件。</summary>
    public UnlockCondition Condition { get; init; } = UnlockCondition.Never;

    /// <summary>判定优先级，<b>小者优先</b>。同时满足时取小的那个。</summary>
    public int Priority { get; init; }
}
