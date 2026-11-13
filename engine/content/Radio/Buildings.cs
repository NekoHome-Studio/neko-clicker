using NekoClicker.Core.Content;

namespace NekoClicker.Content.Radio;

/// <summary>
/// 《猫娘电台》的建筑表（9 座，C1 一层 MVP）。<para>
/// 数值沿用已验证的曲线配方（相邻价格 ×5~20、产量 ×3~12，且从第 3 座起价格倍率 &gt; 产量倍率），
/// 所以曲线回归对全部包同时成立——<b>换包换的是叙事与核心动词，不是数值手感</b>。
/// </para>
/// <para>
/// 带 <c>broadcaster</c> 标签的建筑是唯一能带来「收听率」的建筑：它们把节目播出去，听众才留得住。
/// 它们同时也是主力产出——所以这个包里"多赚钱"和"有人听"是同一件事的两面。
/// 而<b>点击会打断播音、掉收听率</b>（见 <see cref="BroadcastModule"/>）：这是本包反转核心动词的落点。
/// </para>
/// <para>
/// 解锁条件用「历史累计」而不是「本轮累计」：转生之后本轮累计归零，而手上还有建筑——
/// 用本轮累计会让它们显示成「未解锁」（ROADMAP 4A 的不变量）。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>本包的 text.json：建筑文案与剧情散文共用同一份实例（孤儿检查按实例记取用）。</summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>产出「收听率」的建筑标签。</summary>
    public const string BroadcasterTag = BroadcastModule.BroadcasterTag;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "microphone",
            Name = Prose.Text("buildings", "microphone", "name"),
            Icon = Prose.Text("buildings", "microphone", "icon"),
            Description = Prose.Text("buildings", "microphone", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
        },
        new()
        {
            Id = "mixer",
            Name = Prose.Text("buildings", "mixer", "name"),
            Icon = Prose.Text("buildings", "mixer", "icon"),
            Description = Prose.Text("buildings", "mixer", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "transmitter",
            Name = Prose.Text("buildings", "transmitter", "name"),
            Icon = Prose.Text("buildings", "transmitter", "icon"),
            Description = Prose.Text("buildings", "transmitter", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(300),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "antenna",
            Name = Prose.Text("buildings", "antenna", "name"),
            Icon = Prose.Text("buildings", "antenna", "icon"),
            Description = Prose.Text("buildings", "antenna", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(3_000),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "studio",
            Name = Prose.Text("buildings", "studio", "name"),
            Icon = Prose.Text("buildings", "studio", "icon"),
            Description = Prose.Text("buildings", "studio", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30_000),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "satellite",
            Name = Prose.Text("buildings", "satellite", "name"),
            Icon = Prose.Text("buildings", "satellite", "icon"),
            Description = Prose.Text("buildings", "satellite", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(300_000),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "network",
            Name = Prose.Text("buildings", "network", "name"),
            Icon = Prose.Text("buildings", "network", "icon"),
            Description = Prose.Text("buildings", "network", "description"),
            BasePrice = 15_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(3_000_000),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "streaming",
            Name = Prose.Text("buildings", "streaming", "name"),
            Icon = Prose.Text("buildings", "streaming", "icon"),
            Description = Prose.Text("buildings", "streaming", "description"),
            BasePrice = 160_000_000,
            BaseCps = 43_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(30_000_000),
            Tags = [BroadcasterTag],
        },
        new()
        {
            Id = "orbital",
            Name = Prose.Text("buildings", "orbital", "name"),
            Icon = Prose.Text("buildings", "orbital", "icon"),
            Description = Prose.Text("buildings", "orbital", "description"),
            BasePrice = 1_700_000_000,
            BaseCps = 240_000,
            Unlock = UnlockCondition.EarnedAllTimeAtLeast(300_000_000),
            Tags = [BroadcasterTag],
        },
    ];
}
