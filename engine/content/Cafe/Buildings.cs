using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 《猫娘咖啡馆》的建筑表（10 座）。<para>
/// 数值沿用已验证过的曲线（相邻价格 ×6.7~16.5、产量 ×5.4~10，第 3 座起价格倍率 &gt; 产量倍率），
/// 所以 <c>ContentTests</c> 的曲线区间回归对两个包同时成立。<br/>
/// 解锁条件统一用「本轮累计赚取」——玩家在快买得起时就能看到下一层，
/// 形成"再攒一点就解锁"的牵引感，转生后也会重新逐层揭示。
/// </para>
/// <para>
/// <b>说明文本就是叙事通道</b>（ROADMAP 决策 R10）：本阶段的 50 条叙事尚未有独立系统，
/// 先把画面感写进 <see cref="BuildingDefinition.Description"/>。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>
    /// 本包的 <c>text.json</c>：建筑文案与剧情散文<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "coffee_machine",
            Name = Prose.Text("buildings", "coffee_machine", "name"),
            Icon = "☕",
            Description = Prose.Text("buildings", "coffee_machine", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["drink"],
        },
        new()
        {
            Id = "bar_counter",
            Name = Prose.Text("buildings", "bar_counter", "name"),
            Icon = "🪑",
            Description = Prose.Text("buildings", "bar_counter", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["drink"],
        },
        new()
        {
            Id = "cat_tree",
            Name = Prose.Text("buildings", "cat_tree", "name"),
            Icon = "🐈",
            Description = Prose.Text("buildings", "cat_tree", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["cat"],
        },
        new()
        {
            Id = "window_seat",
            Name = Prose.Text("buildings", "window_seat", "name"),
            Icon = "🪟",
            Description = Prose.Text("buildings", "window_seat", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(3_600),
            Tags = ["place"],
        },
        new()
        {
            Id = "upstairs",
            Name = Prose.Text("buildings", "upstairs", "name"),
            Icon = "🪜",
            Description = Prose.Text("buildings", "upstairs", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(39_000),
            Tags = ["drink"],
        },
        new()
        {
            Id = "bakery",
            Name = Prose.Text("buildings", "bakery", "name"),
            Icon = "🥐",
            Description = Prose.Text("buildings", "bakery", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(420_000),
            Tags = ["place"],
        },
        new()
        {
            Id = "catgirl_staff",
            Name = Prose.Text("buildings", "catgirl_staff", "name"),
            Icon = "😺",
            Description = Prose.Text("buildings", "catgirl_staff", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["cat"],
        },
        new()
        {
            Id = "otherworld_door",
            Name = Prose.Text("buildings", "otherworld_door", "name"),
            Icon = "🌀",
            Description = Prose.Text("buildings", "otherworld_door", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(99_000_000),
            Tags = ["portal"],
        },
        new()
        {
            Id = "memory_roastery",
            Name = Prose.Text("buildings", "memory_roastery", "name"),
            Icon = "🫘",
            Description = Prose.Text("buildings", "memory_roastery", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_530_000_000),
            Tags = ["portal"],
        },
        new()
        {
            Id = "branch_store",
            Name = Prose.Text("buildings", "branch_store", "name"),
            Icon = "🏬",
            Description = Prose.Text("buildings", "branch_store", "description"),
            BasePrice = 75_000_000_000,
            BaseCps = 1_600_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(22_500_000_000),
            Tags = ["portal"],
        },
    ];
}
