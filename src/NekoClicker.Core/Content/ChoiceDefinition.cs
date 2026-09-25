namespace NekoClicker.Core.Content;

/// <summary>
/// 一条"立场"（价值取向）。<para>
/// 立场是<b>内容定义</b>而不是引擎枚举——"控制/解放/共存/删除"是某个包的世界观词汇，
/// 不是引擎词汇。写成 <c>enum</c> 等于把某个包的世界观焊进核心，
/// 将来做别的包（道德、劳资、信仰）还得改引擎（A1：核心程序集里不出现内容 id）。
/// </para>
/// <para>
/// 与 <see cref="EraDefinition"/> / <see cref="StorylineDefinition"/> 同构：
/// 引擎只做"累加权重 + 取最高者 + 把它的修饰符计入管线"，不认识任何具体立场。
/// </para>
/// </summary>
public sealed record StanceDefinition
{
    /// <summary>唯一 id（供选择选项引用）。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>一句话主题。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "⚖️";

    /// <summary>
    /// 这一立场主导时生效的全局修饰符。<para>
    /// 这是立场进入引擎的接缝：<see cref="Simulation.ModifierResolver"/> 的第 5 个来源。
    /// </para>
    /// </summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];

    /// <summary>主导这一立场的代价（人类可读，UI 与结算展示用）。</summary>
    public string CostText { get; init; } = string.Empty;
}

/// <summary>
/// 选择的一个选项。<para>
/// <b>刻意没有 <c>UnlocksUpgradeId</c> / <c>LocksUpgradeId</c></b>：那两个字段是多余的。
/// 升级本身就有 <see cref="UpgradeDefinition.Unlock"/>，写成
/// <c>UnlockCondition.ChoiceMade(id)</c> 或 <c>Not(ChoiceMade(id))</c> 就能表达，
/// 而且可以继续组合（<c>All(ChoiceMade("c1"), AchievementsAtLeast(10))</c>）。
/// 少一套机制、少一处会写反的地方。
/// </para>
/// </summary>
public sealed record ChoiceOption
{
    /// <summary>选项 id（同一条选择内唯一）。</summary>
    public required string Id { get; init; }

    /// <summary>按钮文字。第一人称、带角色语气。</summary>
    public required string Label { get; init; }

    /// <summary>选完立刻看到的结果文本。</summary>
    public required string OutcomeText { get; init; }

    /// <summary>所属立场 id；为空表示这个选项不偏向任何立场（纯风味选项）。</summary>
    public string StanceId { get; init; } = string.Empty;

    /// <summary>选中后给该立场累加的权重。</summary>
    public int Weight { get; init; } = 1;

    /// <summary>选中后永久生效的修饰符。</summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];
}

/// <summary>
/// 一次选择（一次需要玩家表态的对话）。<para>
/// <see cref="Trigger"/> 达成后进入待答队列；玩家作答前<b>不产生任何效果</b>——
/// 未选中的选项不会有修饰符、不会累加立场（R6：选择不阻塞，可以放着不管）。
/// </para>
/// </summary>
public sealed record ChoiceDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>谁在说话。</summary>
    public required string Speaker { get; init; }

    /// <summary>问题 / 情境描述。</summary>
    public required string Prompt { get; init; }

    /// <summary>绑定在哪一层的纪元 id；为空表示不限定层。</summary>
    public string EraId { get; init; } = string.Empty;

    /// <summary>触发条件。</summary>
    public UnlockCondition Trigger { get; init; } = UnlockCondition.Never;

    /// <summary>可选项（至少两个，否则不构成选择）。</summary>
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];
}
