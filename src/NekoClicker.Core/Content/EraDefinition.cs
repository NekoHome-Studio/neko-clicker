namespace NekoClicker.Core.Content;

/// <summary>
/// 一个"纪元"（= 一条命 / 一个批次 / 一个时代 / 一台服务器）的定义。<para>
/// 这是转生分层的全部数据：什么时候可以离开本层、离开后世界变成什么样、
/// 以及进入下一层时保留什么。引擎只认识这份定义，不认识任何具体层号 ——
/// 所有"第 N 层有什么不同"都通过下面这些字段表达（见 ROADMAP 的 A2 不变量）。
/// </para>
/// </summary>
public sealed record EraDefinition
{
    /// <summary>层号，从 1 开始且必须连续（缺层会让"逐级推进"断链，构建期会报错）。</summary>
    public required int Index { get; init; }

    /// <summary>唯一 id（用于存档与叙事引用）。</summary>
    public required string Id { get; init; }

    /// <summary>显示名，例如「第一命 · 纸箱纪元」。</summary>
    public required string Name { get; init; }

    /// <summary>一句话主题（图鉴 / 总览用）。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "🌙";

    /// <summary>进入本层时释放的叙事。</summary>
    public string EntryText { get; init; } = string.Empty;

    /// <summary>离开本层（舍命）时释放的叙事。</summary>
    public string ExitText { get; init; } = string.Empty;

    /// <summary>
    /// 本层的"主线完成"条件——决定舍命按钮是否可用。<para>
    /// <b>必须单调不减</b>（只引用累计赚取、成就数、点击数、时长、峰值算力这类只会涨的指标）。
    /// 原因：这个条件会被持续显示在按钮上，一旦指标可能下降，玩家就会看到进度倒退、
    /// 灰按钮闪烁。构建期会按白名单校验（见 <c>GameContentBuilder</c>）。
    /// </para>
    /// </summary>
    public UnlockCondition Completion { get; init; } = UnlockCondition.Always;

    /// <summary>未完成时显示在灰按钮上的提示；为空则回退用 <see cref="Completion"/> 的自动描述。</summary>
    public string CompletionHint { get; init; } = string.Empty;

    /// <summary>本层的数值规则；<c>null</c> 表示沿用内容包的基准 <see cref="GameContent.Balance"/>。</summary>
    public GameBalance? Balance { get; init; }

    /// <summary>本层常驻的倍率类规则（复用现有修饰符管线）。</summary>
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];

    /// <summary>离开本层时情感能量的倍率（例如实验室纪元 0.8 = 道德税）。</summary>
    public double MetaRewardMultiplier { get; init; } = 1.0;

    /// <summary>
    /// 进入本层时保留多少比例的建筑。<c>0</c>（默认）= 全部清空。<para>
    /// 注意语义：这是<b>进入本层</b>的规则，不是离开上一层的规则。
    /// "这一层世界允许带进来什么"由这一层自己决定——例如末世纪元的设定是
    /// "重启文明但保留上纪元的猫娘"，那条规则写在末世纪元上。
    /// </para>
    /// </summary>
    public double InheritBuildingRatio { get; init; }

    /// <summary>进入下一层时无条件保留的建筑 id 白名单；非空时优先于 <see cref="InheritBuildingRatio"/>。</summary>
    public IReadOnlyList<string> InheritBuildings { get; init; } = [];

    /// <summary>本层解锁的建筑 id（供内容作者自查与图鉴展示；真正的门控写在各自的 <c>Unlock</c> 里）。</summary>
    public IReadOnlyList<string> UnlocksBuildings { get; init; } = [];

    /// <summary>本层解锁的升级 id（同上）。</summary>
    public IReadOnlyList<string> UnlocksUpgrades { get; init; } = [];
}

/// <summary>
/// 舍命按钮的状态。<para>
/// UI 只需要读这个结构：<see cref="CanAdvance"/> 为假时按钮置灰，
/// 并把 <see cref="BlockedReason"/> 显示出来；<see cref="Progress"/> 直接驱动进度条。
/// </para>
/// </summary>
/// <param name="CanAdvance">现在是否可以舍命进入下一层。</param>
/// <param name="BlockedReason">不能舍命的原因（人类可读）；可以时为 <c>null</c>。</param>
/// <param name="Progress">本层主线进度 [0,1]；无法量化时为 0 或 1。</param>
/// <param name="CurrentIndex">当前层号。</param>
/// <param name="NextIndex">下一层号；已是最后一层时为 <c>null</c>。</param>
public readonly record struct EraGate(
    bool CanAdvance,
    string? BlockedReason,
    double Progress,
    int CurrentIndex,
    int? NextIndex);
