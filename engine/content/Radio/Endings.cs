using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 两个结局（C1《猫娘电台》）：被听到最后 / 无人再听。<para>
/// 这个包没有立场轴，只有一个<b>会掉</b>的指标：收听率。所以"被听到最后"不是"你攒够了"，
/// 而是<b>「你停播的时候，还有人正在听」</b>：条件里含一个收听率门槛，而它在每次开新频段时清零、
/// 没人听的时候衰减、点击还会掉。这是这个包唯一一个"必须持续维持才成立"的结局。
/// </para>
/// <para>
/// 兜底结局 <c>end_static</c> 只依赖主线完成，不含取反、不依赖收听率——构建期强制要求存在这样一个结局，
/// 玩家回避不了它，也就不可能卡住。它也是"虚无化"的终局形态：节目播完了，但停播的时候没有人在听。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>），否则玩家一进第 1 频段兜底结局就会立刻成立，
/// 而这一频段的听众还没养起来（实验室包踩过的坑）。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>本包的 text.json：结局文案与其它分区共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>两个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_heard_to_the_end",
            Name = Prose.Text("endings", "end_heard_to_the_end", "name"),
            Icon = Prose.Text("endings", "end_heard_to_the_end", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(BroadcastModule.CounterKey, ListenershipForEnding)),
            Text = Prose.Text("endings", "end_heard_to_the_end", "text"),
        },
        new()
        {
            Id = "end_static",
            Name = Prose.Text("endings", "end_static", "name"),
            Icon = Prose.Text("endings", "end_static", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_static", "text"),
        },
    ];

    /// <summary>结局成就：自己声明 Unlock = EndingReached(...)，走常规成就路径解锁。</summary>
    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_heard_to_the_end", "end_heard_to_the_end"),
        Ending("ach_end_static", "end_static"),
    ];

    /// <summary>
    /// 「停播的时候还有人正在听」的门槛。必须<b>够得着</b>——收听率会衰减、每频段开局是 0，
    /// 门槛一旦高于真实曲线养得起的听众量，那个结局就永远拿不到。
    /// </summary>
    public const double ListenershipForEnding = 60_000;

    /// <summary>末层主线完成：走到第 2 频段且完成条件成立。复用 Eras.FinalCompletion，不抄数值。</summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(2),
            Eras.FinalCompletion);

    private static AchievementDefinition Ending(string id, string endingId) => new()
    {
        Id = id,
        Name = Prose.Text("achievements", id, "name"),
        Icon = Prose.Text("achievements", id, "icon"),
        Description = Prose.Text("achievements", id, "description"),
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
