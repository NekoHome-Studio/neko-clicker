using NekoClicker.Core.Content;

namespace NekoClicker.Content.Dream;

/// <summary>
/// 两个结局：叫醒梦者 / 永远留在梦里。<para>
/// 这个包既没有立场轴，也没有"记忆量"那种单向累积的资源，所以两个结局的分岔
/// 只压在一件事上：<b>她有没有攒够把人叫醒的力气</b>——而那由「梦境能量」度量。
/// 「叫醒梦者」是承诺型：它不是"你赢了"，而是"你在最深的一层里还愿意往外走"。
/// </para>
/// <para>
/// 兜底结局 <c>end_stay_forever</c> 只依赖主线完成，不含取反、不依赖任何可选行为，
/// <c>Priority</c> 最大（判定时取优先级最小的那个，所以它永远排最后）。
/// 构建期强制要求存在这样一个结局，玩家回避不了它，也就不可能卡住。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，
/// 而梦核还没养起来。实验室包踩过这个坑，这个包把顺序写进条件里。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>两个结局，按 Priority 升序（小的先判定）。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_wake_her",
            Name = "叫醒梦者",
            Icon = "🌅",
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.Counter(DreamEnergyModule.CounterKey, DreamEnergyForEnding)),
            Text = "她攒够了力气。不是用来推开某一扇门的力气，是那种"
                   + "「我知道这是梦，而且我还是要出去」的力气。"
                   + "她一层一层往上走：梦核、嵌套塔、清醒区、失眠走廊——每一层都在留她，"
                   + "每一层她都回一次头。最上面那层是浅眠，枕头还是温的。"
                   + "她睁开眼的时候，天刚亮，窗外有人在收摊。她躺了很久，"
                   + "然后坐起来，对着那个还在睡的人说：「该起了。」",
        },
        new()
        {
            Id = "end_stay_forever",
            Name = "永远留在梦里",
            Icon = "🌙",
            Priority = 100,
            Condition = Finished,
            Text = "她没有上去。梦核旁边很暖，而且这里的时间是她说了算的——"
                   + "她可以让这一晚长到不像一晚。"
                   + "后来她在最里面又搭了一层，再往里又搭了一层，"
                   + "每一层都比外面大一点、软一点、好一点。"
                   + "有一天她试着往上走了走，走到浅眠那一层就停住了："
                   + "上面那层梦里有个枕头，枕头是凉的。"
                   + "她想了想，转过身，往下走回去了。她说：「再睡五分钟。」",
        },
    ];

    /// <summary>
    /// 结局成就。<para>
    /// 它们自己声明 <c>Unlock = EndingReached(...)</c>，走常规成就路径解锁——
    /// 结局不需要知道"谁是它的成就"，引擎也不需要针对结局加特判。
    /// </para>
    /// </summary>
    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_wake_her", "叫醒梦者", "end_wake_her", "🌅"),
        Ending("ach_end_stay_forever", "永远留在梦里", "end_stay_forever", "🌙"),
    ];

    /// <summary>
    /// 「攒够叫醒她的力气」的门槛。<para>
    /// 单独暴露出来是因为它必须<b>够得着</b>——梦境能量只涨不落，但它的产率贴着建筑线走，
    /// 门槛一旦高于这个包在真实曲线里养得出的量，那个结局就永远拿不到。
    /// 它由 <c>DreamContentTests.DreamEnergyAtTheEnd_ExceedsTheEndingThreshold</c> 守着，
    /// 数值本身是<b>先量包络再设值</b>定的（手册 §12.2）：实测这一个包 12 游戏小时的自然包络是
    /// 1.2e10 点（1440 轮贪心机器人），门槛取其中的约四成；而末层主线（含 6 小时的时长门槛）
    /// 落在第 6~7 小时、届时手上已经有 6e9 左右——所以<b>自然游玩会先拿到承诺型的那个</b>，
    /// 只有刻意攒梦却不肯等的路线才会落到兜底上。
    /// </para>
    /// </summary>
    public const double DreamEnergyForEnding = 4.5e9;

    /// <summary>
    /// 末层主线完成：走到第 5 层，且第 5 层的完成条件成立。<para>
    /// 复用 <see cref="Eras.FinalCompletion"/> 而不是抄一遍数值——两条门槛一旦分叉，
    /// "结局在末层完成后判定"这条规则就会悄悄失效。
    /// </para>
    /// </summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(5),
            Eras.FinalCompletion);

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
