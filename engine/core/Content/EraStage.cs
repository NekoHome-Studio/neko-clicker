namespace NekoClicker.Core.Content;

/// <summary>
/// 一个纪元<b>内部</b>的阶段边界。<para>
/// 纪元（<see cref="EraDefinition"/>）是"重开"的单位：过一层就清空本轮。
/// 而阶段是"重开<b>之内</b>的分段"：跨过它<b>什么都不清</b>，只是这一层里多了一样能买的东西
/// （或一条能读到的叙事）。所以阶段不是资源、不是货币、不是倍率，
/// 也<b>不占存档位</b>——它完全由单调指标派生（见 <see cref="EraSystem.Stage"/>）。
/// </para>
/// <para>
/// <b>为什么做成"内容声明的边界"而不是一个新的引擎状态机</b>：内容里<b>早就有</b>层内分段了
/// ——每一条挂在 <c>All(EraAtLeast(n), EarnedThisRunAtLeast(m))</c> 上的叙事 / 表态，
/// 以及每一座用「本轮累计」解锁的建筑，都是一条隐式的阶段线。缺的从来不是机制，
/// 而是<b>把它说出来</b>：玩家看得见的分段、构建期能校验的声明、守卫能断言的结构。
/// 于是这里选择的形式是"给已有的门槛起个名字"，而不是"再造一套进度"：
/// 老存档、老内容、8 个没声明阶段的包，行为<b>一个字节都不变</b>（<see cref="EraDefinition.Stages"/>
/// 为空时整套能力是休眠的）。
/// </para>
/// </summary>
public sealed record EraStage
{
    /// <summary>唯一 id（层内唯一；用于构建期报错定位与内容自查）。</summary>
    public required string Id { get; init; }

    /// <summary>显示名，例如「路演厅」。玩家在阶段提示里看到的就是它。</summary>
    public required string Name { get; init; }

    /// <summary>图标（通知用）。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// 跨过这条边界的条件。<b>必须单调不减</b>（与纪元完成条件同一张白名单，构建期校验）：
    /// 阶段只进不退，否则界面上的「第 k/n 阶段」会来回跳。
    /// <para>
    /// 默认值刻意是 <see cref="UnlockCondition.Never"/>（而不是 <c>Always</c>）：
    /// 忘了写条件的阶段<b>永远打不开</b>，而那是个静默的谎——所以构建期直接把它报成错误，
    /// 而不是让它在线上悄悄成立或悄悄永不成立。
    /// </para>
    /// </summary>
    public UnlockCondition At { get; init; } = UnlockCondition.Never;
}

/// <summary>
/// 当前纪元的阶段状态。<para>
/// <b>编号从 1 开始，且第 1 阶段是"刚进这一层"</b>——所以
/// <c>Count = 声明的边界数 + 1</c>，<c>Index ∈ [1, Count]</c>。
/// 这样内容侧只需要声明"在哪里分段"，不必再写一条 <c>At = Always</c> 的开场阶段
/// （那会是一份重复的声明，而且容易和层本身的门槛打架）。
/// </para>
/// <para>
/// 没有声明任何阶段的纪元返回 <c>Count = 0</c>（<see cref="HasStages"/> 为假），
/// 界面据此整块隐藏——旧内容包一个都不会因此多出一行。
/// </para>
/// </summary>
/// <param name="Index">当前阶段号（1 起）；没有阶段时为 0。</param>
/// <param name="Count">本层共几个阶段（含开场那个）；没有阶段时为 0。</param>
/// <param name="Current">当前阶段对应的边界；第 1 阶段（开场）为 <c>null</c>。</param>
/// <param name="Next">下一个阶段的边界；已在最后一个阶段时为 <c>null</c>。</param>
/// <param name="Progress">向下一个阶段推进的比例 [0,1]；已在最后一个阶段时为 1。</param>
public readonly record struct EraStageGate(
    int Index,
    int Count,
    EraStage? Current,
    EraStage? Next,
    double Progress)
{
    /// <summary>本层是否声明了阶段（没声明时界面整块隐藏）。</summary>
    public bool HasStages => Count > 0;

    /// <summary>当前阶段的显示名；开场阶段（无边界名）为空串。</summary>
    public string Name => Current?.Name ?? string.Empty;

    /// <summary>下一阶段的显示名；已在最后一个阶段时为空串。</summary>
    public string NextName => Next?.Name ?? string.Empty;
}
