using NekoClicker.Core.Content;

namespace NekoClicker.Content.Neko;

/// <summary>
/// 示例内容包的建筑表。<para>
/// 数值节奏参考 Cookie Clicker：相邻建筑的<b>价格约 ×10~13</b>、<b>单产约 ×6~8</b>。
/// 这个比例决定了"每解锁一层新建筑，都会在几分钟内成为主力，然后被下一层取代"的循环；
/// 想让玩家在某一层多停留一会儿，就缩小价格倍率或拉大产量倍率。
/// </para>
/// <para>
/// 解锁条件统一用<b>本轮累计赚取量</b>（约为该建筑价格的 30%）：玩家在快要买得起时
/// 就能看到下一层建筑，形成"再攒一点就解锁"的牵引感；而转生后重新逐层揭示，
/// 也避免了开局就被一长串买不起的灰色条目淹没。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>
    /// 本包的 <c>text.json</c>（随包复制到 <c>content/Neko/text.json</c>）。<para>
    /// 用懒初始化而不是静态字段直接加载：文件坏掉时抛的是"哪一条对不上"，
    /// 而不是被包成 <c>TypeInitializationException</c> 的谜语。
    /// 用 <see cref="Lazy{T}"/> 而不是 <c>??=</c>：内容可能被多个线程同时首次构建
    /// （宿主扫包、测试并行跑），而这份文本里记着"哪些 id 已经取过"的可变状态，
    /// 不能有两个实例各记一半。
    /// </para>
    /// </summary>
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Neko"));

    /// <summary>文本文件；取不到就抛，绝不回退成空白。</summary>
    private static ContentText Prose => ProseCache.Value;

    /// <summary>
    /// 文本文件里"有、但代码从不取用"的条目会在这里抛（孤儿文本）。<para>
    /// 调用点必须在 <see cref="All"/> 已经取过之后——现在只有 <c>NekoContent.Build()</c> 末尾一处。
    /// 孤儿必须炸的理由见 <see cref="ContentText"/>：删了文案却留着文本，
    /// 运行、断言、界面都不会有任何反应。
    /// </para>
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

    /// <summary>全部建筑，顺序即 UI 展示顺序。</summary>
    public static BuildingDefinition[] All =>
    [
        new()
        {
            Id = "curled_cat",
            Name = Prose.Text("buildings", "curled_cat", "name"),
            Icon = Prose.Text("buildings", "curled_cat", "icon"),
            Description = Prose.Text("buildings", "curled_cat", "description"),
            BasePrice = 15,
            BaseCps = 0.1,
            Tags = ["cat", "warm"],
        },
        new()
        {
            Id = "scratching_post",
            Name = Prose.Text("buildings", "scratching_post", "name"),
            Icon = Prose.Text("buildings", "scratching_post", "icon"),
            Description = Prose.Text("buildings", "scratching_post", "description"),
            BasePrice = 100,
            BaseCps = 1,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(30),
            Tags = ["cat"],
        },
        new()
        {
            Id = "cat_bed",
            Name = Prose.Text("buildings", "cat_bed", "name"),
            Icon = Prose.Text("buildings", "cat_bed", "icon"),
            Description = Prose.Text("buildings", "cat_bed", "description"),
            BasePrice = 1_100,
            BaseCps = 8,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
            Tags = ["cat", "warm"],
        },
        new()
        {
            Id = "auto_feeder",
            Name = Prose.Text("buildings", "auto_feeder", "name"),
            Icon = Prose.Text("buildings", "auto_feeder", "icon"),
            Description = Prose.Text("buildings", "auto_feeder", "description"),
            BasePrice = 12_000,
            BaseCps = 47,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(3_600),
            Tags = ["machine"],
        },
        new()
        {
            Id = "cat_cafe",
            Name = Prose.Text("buildings", "cat_cafe", "name"),
            Icon = Prose.Text("buildings", "cat_cafe", "icon"),
            Description = Prose.Text("buildings", "cat_cafe", "description"),
            BasePrice = 130_000,
            BaseCps = 260,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(39_000),
            Tags = ["business"],
        },
        new()
        {
            Id = "catnip_field",
            Name = Prose.Text("buildings", "catnip_field", "name"),
            Icon = Prose.Text("buildings", "catnip_field", "icon"),
            Description = Prose.Text("buildings", "catnip_field", "description"),
            BasePrice = 1_400_000,
            BaseCps = 1_400,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(420_000),
            Tags = ["farm"],
        },
        new()
        {
            Id = "cat_portal",
            Name = Prose.Text("buildings", "cat_portal", "name"),
            Icon = Prose.Text("buildings", "cat_portal", "icon"),
            Description = Prose.Text("buildings", "cat_portal", "description"),
            BasePrice = 20_000_000,
            BaseCps = 7_800,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(6_000_000),
            Tags = ["exotic"],
        },
        new()
        {
            Id = "time_cat",
            Name = Prose.Text("buildings", "time_cat", "name"),
            Icon = Prose.Text("buildings", "time_cat", "icon"),
            Description = Prose.Text("buildings", "time_cat", "description"),
            BasePrice = 330_000_000,
            BaseCps = 44_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(99_000_000),
            Tags = ["exotic"],
        },
        new()
        {
            Id = "cat_temple",
            Name = Prose.Text("buildings", "cat_temple", "name"),
            Icon = Prose.Text("buildings", "cat_temple", "icon"),
            Description = Prose.Text("buildings", "cat_temple", "description"),
            BasePrice = 5_100_000_000,
            BaseCps = 260_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(1_530_000_000),
            Tags = ["exotic", "holy"],
        },
        new()
        {
            Id = "cat_universe",
            Name = Prose.Text("buildings", "cat_universe", "name"),
            Icon = Prose.Text("buildings", "cat_universe", "icon"),
            Description = Prose.Text("buildings", "cat_universe", "description"),
            BasePrice = 75_000_000_000,
            BaseCps = 1_600_000,
            Unlock = UnlockCondition.EarnedThisRunAtLeast(22_500_000_000),
            Tags = ["exotic", "cosmic"],
        },
    ];
}
