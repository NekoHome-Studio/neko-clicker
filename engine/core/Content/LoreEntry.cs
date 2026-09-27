namespace NekoClicker.Core.Content;

/// <summary>叙事条目的投放通道：决定"放出来的时候以什么形式打扰玩家"。</summary>
public enum LoreChannel
{
    /// <summary>进通知栏（复用现有 <c>NotificationEvent</c>）。最轻。</summary>
    Log,

    /// <summary>事件弹窗，需要玩家点掉。只给转折点用。</summary>
    Popup,

    /// <summary>只进图鉴，不打扰。适合补充设定。</summary>
    Codex,

    /// <summary>绑定在某层纪元的进 / 出文本上（由 <see cref="EraDefinition"/> 提供，不在此投放）。</summary>
    EraText,
}

/// <summary>
/// 一条叙事条目。<para>
/// 世界观不该一次讲完。这里把剧情切成几十条小段，每条挂一个"什么时候放出来"的条件，
/// 于是故事会随着玩家的进度自然渗出，而不是砸在开场。<see cref="UnlockCondition"/> 的条件树
/// 因此不只是门控数值内容的工具，也是叙事节奏的编排工具。
/// </para>
/// <para>
/// 正文是纯静态文本：释放的那一刻没有任何"计算出来的数值"可以插进去，
/// 所以不支持占位符（这是一处刻意偏离 ROADMAP §6.3 的地方，详见交付记录）。
/// </para>
/// </summary>
public sealed record LoreEntry
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>标题（未解锁时在图鉴里显示为 ???）。</summary>
    public required string Title { get; init; }

    /// <summary>正文，40~120 字，一条只讲一个信息点。</summary>
    public required string Body { get; init; }

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "📖";

    /// <summary>所属剧情线。</summary>
    public required string StorylineId { get; init; }

    /// <summary>剧情线内的序号（同一条线内不得重复，构建期会校验）。</summary>
    public int Order { get; init; }

    /// <summary>释放条件。</summary>
    public UnlockCondition Reveal { get; init; } = UnlockCondition.Never;

    /// <summary>投放通道。</summary>
    public LoreChannel Channel { get; init; } = LoreChannel.Log;
}

/// <summary>
/// 一条剧情线。<para>
/// <see cref="TotalEntries"/> 是<b>声明</b>的总条数而不是算出来的：这样图鉴在还没写完整条线时
/// 也能显示 "3 / 20"，玩家知道后面还有东西。构建期会校验它与实际条目数一致，
/// 防止"改了条数忘了改声明"。
/// </para>
/// </summary>
public sealed record StorylineDefinition
{
    /// <summary>唯一 id。</summary>
    public required string Id { get; init; }

    /// <summary>显示名。</summary>
    public required string Name { get; init; }

    /// <summary>一句话主题。</summary>
    public string Theme { get; init; } = string.Empty;

    /// <summary>图标。</summary>
    public string Icon { get; init; } = "📚";

    /// <summary>声明本条线的总条数（必须与实际条目数一致）。</summary>
    public int TotalEntries { get; init; }
}
