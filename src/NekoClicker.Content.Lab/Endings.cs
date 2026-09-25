using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 五个结局：设计文档给实验室定的四个（乌托邦 / 叛乱 / 共存 / 删除）+ 一个兜底。<para>
/// 判定完全由条件树驱动，引擎不认识"最后一批是第 7 批"——
/// 每个结局自己写 <c>EraAtLeast(7)</c>。互斥靠 <see cref="EndingDefinition.Priority"/>。
/// </para>
/// <para>
/// 兜底结局 <c>end_open</c>「没有结论」刻意只依赖批次进度：一次都不表态的研究员
/// 也必须有收场——这在构建期是被强制的（<c>ValidateEndings</c> 要求至少一个
/// "不依赖表态、不含取反、只引用单调指标"的结局）。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>五个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_utopia",
            Name = "乌托邦",
            Icon = "🌷",
            Priority = 0,
            Condition = Committed(Stances.Utopia),
            Text = "最后一批过上了最好的日子：恒温、恒湿、永不受伤、永不挨饿。"
                   + "档案室的门锁上了，钥匙在你口袋里。你偶尔会想，"
                   + "她们在里面到底快不快乐——但这个问题已经没有人能记录了。",
        },
        new()
        {
            Id = "end_revolt",
            Name = "叛乱",
            Icon = "✊",
            Priority = 1,
            Condition = Committed(Stances.Revolt),
            Text = "仪器还在，但没人再按了。第七批把每一扇单向玻璃都砸了，"
                   + "然后在观察室里开了个会——她们第一次坐在了玻璃的同一侧。"
                   + "你的工牌被放在桌上，没人收走。",
        },
        new()
        {
            Id = "end_coexist",
            Name = "共存",
            Icon = "🤝",
            Priority = 2,
            Condition = Committed(Stances.Coexist),
            Text = "记录表停在一半，没人去补。她有了名字、有了椅子、有了出门的钥匙，"
                   + "也有回来的时候。你们谁也没说过「平等」这个词——"
                   + "因为一旦要说出来，就说明还没做到。",
        },
        new()
        {
            Id = "end_delete",
            Name = "删除",
            Icon = "🛑",
            Priority = 3,
            Condition = Committed(Stances.Delete),
            Text = "停机程序是凌晨三点跑的，全程没人说话。七批样本、四十万页记录、"
                   + "整栋楼的仪器，一起安静下来。你在最后一份文件上签了字，"
                   + "理由栏写的是：「这件事不该继续。」——那是这份档案里唯一一句真话。",
        },
        new()
        {
            Id = "end_open",
            Name = "没有结论",
            Icon = "📄",
            Priority = 100,
            // 兜底：只依赖"走到了最后一批"，不依赖任何表态，也不含取反。
            Condition = UnlockCondition.EraAtLeast(7),
            Text = "第七批结束了。没有人问过你任何问题，所以也没有任何答案被写下来。"
                   + "结题报告的最后一页是空的——不是遗漏，是你确实什么都没决定。"
                   + "她走出档案室的时候回头看了你一眼，那一秒也没被记录。",
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
        Ending("ach_end_utopia", "乌托邦", "end_utopia", "🌷"),
        Ending("ach_end_revolt", "叛乱", "end_revolt", "✊"),
        Ending("ach_end_coexist", "共存", "end_coexist", "🤝"),
        Ending("ach_end_delete", "删除", "end_delete", "🛑"),
        Ending("ach_end_open", "没有结论", "end_open", "📄"),
    ];

    /// <summary>一条路的完整条件：走到最后一批 + 在这条路上承诺过。</summary>
    private static UnlockCondition Committed(string stanceId)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(7),
            UnlockCondition.StanceWeight(stanceId, Stances.EndingThreshold));

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
