using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 叙事条目：50 条，分四条线。<para>
/// 这个包的叙事节奏天然由<b>纪元</b>驱动——每条线都跟着"第几次醒来"逐段揭开，
/// 于是九命本身就是叙事的分卷。开场 10 分钟内只会放出 3 条。
/// </para>
/// <para>
/// <b>节奏规则（每条都别破坏）</b>：
/// <list type="number">
///   <item>除开篇外，每条都用 <c>All(EraAtLeast(n), 层内里程碑)</c>。</item>
///   <item>里程碑一律取该层完成门槛的固定百分比（10%~96%），且<b>全包唯一</b>。</item>
///   <item>同一条线内，门槛必须随 <see cref="LoreEntry.Order"/> 单调递增。</item>
///   <item>每一层至多一个弹窗。</item>
/// </list>
/// </para>
/// <para>
/// 之所以能靠"纪元门槛 + 层内百分比"根除"进层瞬间炸一堆"：<c>EarnedThisRunAtLeast</c>
/// 在舍命进层时<b>归零</b>（见 <c>Eras.cs</c> 顶部说明），所以纪元门槛一成立，
/// 层内里程碑必定是从 0 重新往上爬——进层那一刻，本层没有任何条目够条件。
/// 而里程碑数值全包互不相同，于是任一瞬间至多释放一条。
/// </para>
/// <para>
/// 反过来说，<b>层内里程碑的百分比不能超过该层的完成门槛</b>，否则玩家会在够条件前就舍命走人，
/// 这条剧情就永远读不到了（门槛越高越危险）。percent 一律 ≤ 100。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("NineLives"));

    /// <summary>本包的散文文件（随包复制到 content/NineLives/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("nine", 20),
        Line("god", 12),
        Line("ruin", 10),
        Line("instinct", 8),
    ];

    /// <summary>剧情线的名称 / 主题 / 图标来自文本文件；条数留在代码里（它是纪律，不是散文）。</summary>
    private static StorylineDefinition Line(string id, int totalEntries) => new()
    {
        Id = id,
        Name = Prose.Text("storylines", id, "name"),
        Theme = Prose.Text("storylines", id, "theme"),
        Icon = Prose.Text("storylines", id, "icon"),
        TotalEntries = totalEntries,
    };

    /// <summary>全部条目。</summary>
    public static LoreEntry[] Entries =>
    [
        .. NineLine(),
        .. GodLine(),
        .. RuinLine(),
        .. InstinctLine(),
    ];

    // ---------------------------------------------------------------- 主线：九命

    private static IEnumerable<LoreEntry> NineLine()
    {
        yield return Popup("nine_01", 1, UnlockCondition.ClicksAtLeast(1));

        yield return Log("nine_02", 2, Era(1, 6e4));

        yield return Log("nine_03", 3, Era(2, 1.5e6));

        yield return Log("nine_04", 4, Era(2, 6.5e6));

        yield return Popup("nine_05", 5, Era(3, 1.5e7));

        yield return Log("nine_06", 6, Era(3, 4.8e7));

        yield return Log("nine_07", 7, Era(3, 8.2e7));

        yield return Codex("nine_08", 8, Era(4, 4e7));

        yield return Popup("nine_09", 9, Era(4, 1.2e8));

        yield return Log("nine_10", 10, Era(5, 6.6e7));

        yield return Log("nine_11", 11, Era(5, 1.38e8));

        yield return Log("nine_12", 12, Era(5, 2.1e8));

        yield return Popup("nine_13", 13, Era(6, 1.65e8));

        yield return Log("nine_14", 14, Era(7, 1.82e8));

        yield return Codex("nine_15", 15, Era(8, 2e8));

        yield return Popup("nine_16", 16, Era(9, 2.7e8));

        // ---- 四条终局伏笔。机制留到系统 C（选择 + 立场轴），但路先铺好：
        // 每一条都写明"她试过"，于是玩家在结局时看到的是一个已经想过很多次的选项，
        // 而不是四个突然冒出来的按钮。
        yield return Codex("nine_17", 17, Era(9, 5.7e8));

        yield return Codex("nine_18", 18, Era(9, 8.7e8));

        yield return Codex("nine_19", 19, Era(9, 1.17e9));

        yield return Popup("nine_20", 20, Era(9, 1.44e9));
    }

    // ---------------------------------------------------------------- 支线：猫神寐娅

    private static IEnumerable<LoreEntry> GodLine()
    {
        yield return Log("god_01", 1, UnlockCondition.ClicksAtLeast(25));

        yield return Log("god_02", 2, Era(2, 3.2e6));

        yield return Codex("god_03", 3, Era(3, 3.2e7));

        yield return Popup("god_04", 4, Era(4, 8e7));

        yield return Log("god_05", 5, Era(5, 3e7));

        yield return Log("god_06", 6, Era(5, 1.74e8));

        yield return Codex("god_07", 7, Era(6, 7.5e7));

        yield return Log("god_08", 8, Era(6, 3.4e8));

        yield return Popup("god_09", 9, Era(7, 8.4e7));

        yield return Log("god_10", 10, Era(7, 4.76e8));

        yield return Log("god_11", 11, Era(8, 4.5e8));

        yield return Popup("god_12", 12, Era(9, 4.2e8));
    }

    // ---------------------------------------------------------------- 支线：人类遗毒

    private static IEnumerable<LoreEntry> RuinLine()
    {
        yield return Log("ruin_01", 1, Era(2, 4.8e6));

        yield return Codex("ruin_02", 2, Era(3, 6.5e7));

        yield return Log("ruin_03", 3, Era(4, 1.6e8));

        yield return Log("ruin_04", 4, Era(5, 1.02e8));

        yield return Popup("ruin_05", 5, Era(6, 2.5e8));

        yield return Log("ruin_06", 6, Era(7, 2.8e8));

        yield return Log("ruin_07", 7, Era(7, 5.74e8));

        yield return Codex("ruin_08", 8, Era(8, 7e8));

        yield return Log("ruin_09", 9, Era(9, 1.2e8));

        yield return Popup("ruin_10", 10, Era(9, 1.32e9));
    }

    // ---------------------------------------------------------------- 支线：猫的本能

    private static IEnumerable<LoreEntry> InstinctLine()
    {
        yield return Log("instinct_01", 1, UnlockCondition.ClicksAtLeast(120));

        yield return Log("instinct_02", 2, Era(2, 8.2e6));

        yield return Log("instinct_03", 3, Era(2, 9.4e6));

        yield return Log("instinct_04", 4, Era(4, 1.86e8));

        yield return Log("instinct_05", 5, Era(5, 2.46e8));

        yield return Log("instinct_06", 6, Era(6, 4.25e8));

        yield return Log("instinct_07", 7, Era(7, 6.51e8));

        yield return Log("instinct_08", 8, Era(9, 1.02e9));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 纪元门槛 + 层内里程碑。<paramref name="milestone"/> 必须落在该层完成条件之下
    /// （否则玩家会在够条件前舍命走人，这条剧情就永远读不到），且全包唯一。
    /// </summary>
    private static UnlockCondition Era(int era, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(era),
            UnlockCondition.EarnedThisRunAtLeast(milestone));

    private static LoreEntry Popup(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Popup);

    private static LoreEntry Log(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Log);

    private static LoreEntry Codex(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Codex);

    private static LoreEntry Make(
        string id, int order, UnlockCondition reveal, LoreChannel channel)
    {
        int underscore = id.IndexOf('_');
        string storylineId = underscore > 0 ? id[..underscore] : id;

        return new LoreEntry
        {
            Id = id,
            Title = Prose.Text("lore", id, "title"),
            Body = Prose.Text("lore", id, "body"),
            Icon = IconFor(storylineId),
            StorylineId = storylineId,
            Order = order,
            Reveal = reveal,
            Channel = channel,
        };
    }

    private static string IconFor(string storylineId) => storylineId switch
    {
        "nine" => "🌙",
        "god" => "👁️",
        "ruin" => "🏚️",
        "instinct" => "🐾",
        _ => "📖",
    };

    /// <summary>
    /// 文本文件里“有、但代码从不取用”的条目会在这里抛（孤儿文本）。<para>
    /// 覆盖<b>整份 <c>text.json</c></b>：剧情条目与 <see cref="Buildings"/> 的建筑文案都算——
    /// 两者共用同一份 <see cref="Prose"/> 实例，所以调用点必须在两者都被取过之后
    /// （现在只有包入口的 <c>Build()</c> 末尾一处）。
    /// </para>
    /// <para>
    /// 孤儿必须炸的理由见 <see cref="ContentText"/>：删了剧情却留着文本，
    /// 运行、断言、界面都不会有任何反应，于是那段死文本会一直躺在文件里等人改。
    /// </para>
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

}
