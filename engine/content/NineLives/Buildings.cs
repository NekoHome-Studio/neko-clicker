using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 十二座建筑，跨九层纪元逐步揭示。<para>
/// 数值沿用框架里已经验证过的曲线（相邻价格 ×6.7~16.5、产量 ×5.4~10，
/// 第 3 座起价格倍率必须大于产量倍率），所以曲线回归测试不需要为这个包改参数。
/// </para>
/// <para>
/// 解锁条件 = 「本轮累计赚取够多」且「已经进入第 N 命」。
/// 两者都是单调不减的指标，且后者保证建筑按纪元逐层出现——
/// 这是"每换一条命，世界重新长出来一遍"的机制表达。
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

    /// <summary>全部建筑。</summary>
    public static BuildingDefinition[] All =>
    [
        Make("cardboard_box", "📦", 15, 0.1, era: 1),
        Make("cat_bed", "🛏️", 100, 1, era: 1),

        Make("cat_cafe", "☕", 1_100, 8, era: 2),
        Make("catnip_field", "🌿", 12_000, 47, era: 2),

        Make("cat_tower", "🗼", 130_000, 260, era: 3),
        Make("catgirl_lab", "🧪", 1_400_000, 1_400, era: 3),

        Make("server_farm", "🖥️", 20_000_000, 7_800, era: 4),
        Make("memory_vault", "🗄️", 330_000_000, 44_000, era: 5),

        Make("temple", "🏛️", 5_100_000_000, 260_000, era: 6),
        Make("stream_studio", "📺", 75_000_000_000, 1_600_000, era: 7),

        Make("dream_library", "📚", 1_200_000_000_000, 9_000_000, era: 8),
        Make("cat_universe", "🌌", 18_000_000_000_000, 54_000_000, era: 9),
    ];

    private static BuildingDefinition Make(
        string id, string icon, double price, double cps, int era) => new()
        {
            Id = id,
            Name = Prose.Text("buildings", id, "name"),
            Icon = icon,
            Description = Prose.Text("buildings", id, "description"),
            BasePrice = price,
            BaseCps = cps,
            Unlock = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(price * 0.3),
                UnlockCondition.EraAtLeast(era)),
            Category = "nine-lives",
            Tags = ["neko"],
        };
}
