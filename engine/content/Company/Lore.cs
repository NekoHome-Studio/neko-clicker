using NekoClicker.Core.Content;

namespace NekoClicker.Content.Company;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>公司阶段</b>走——每重组一轮，故事往前推一段；开篇几条挂在很小的营收上，
/// 免得图鉴一开就是一整墙 ???。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1/#2/#3 相同）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现"第 3 条还锁着，第 4 条已亮"。</item>
///   <item>门槛一律 ≤ 该轮的完成门槛——否则玩家会在够条件前重组走人，这条永远读不到。</item>
/// </list>
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Company"));

    /// <summary>本包的散文文件（随包复制到 content/Company/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("her", 14),
        Line("boss", 10),
        Line("team", 8),
        Line("ledger", 8),
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
        .. HerLine(),
        .. FounderLine(),
        .. TeamLine(),
        .. LedgerLine(),
    ];

    // ---------------------------------------------------------------- 主线：她

    private static IEnumerable<LoreEntry> HerLine()
    {
        yield return Log("her_01", 1, Round(1, 120));

        yield return Log("her_02", 2, Round(1, 900));

        yield return Popup("her_03", 3, Round(1, 6_000));

        yield return Log("her_04", 4, Round(1, 25_000));

        yield return Codex("her_05", 5, Round(1, 70_000));

        yield return Log("her_06", 6, Round(2, 3e5));

        yield return Log("her_07", 7, Round(2, 3e6));

        yield return Popup("her_08", 8, Round(2, 1.5e7));

        yield return Codex("her_09", 9, Round(2, 4e7));

        yield return Log("her_10", 10, Round(2, 7.5e7));

        yield return Popup("her_11", 11, Round(3, 1.3e8));

        yield return Log("her_12", 12, Round(3, 2.2e8));

        yield return Log("her_13", 13, Round(3, 3.6e8));

        yield return Popup("her_14", 14, Round(3, 4.8e8));
    }

    // ---------------------------------------------------------------- 你

    private static IEnumerable<LoreEntry> FounderLine()
    {
        yield return Popup("boss_01", 1, Round(1, 2_500));

        yield return Codex("boss_02", 2, Round(1, 40_000));

        yield return Log("boss_03", 3, Round(2, 1e6));

        yield return Log("boss_04", 4, Round(2, 5e6));

        yield return Log("boss_05", 5, Round(2, 2e7));

        yield return Popup("boss_06", 6, Round(2, 5.5e7));

        yield return Codex("boss_07", 7, Round(2, 9e7));

        yield return Log("boss_08", 8, Round(3, 1.6e8));

        yield return Log("boss_09", 9, Round(3, 3e8));

        yield return Popup("boss_10", 10, Round(3, 4.2e8));
    }

    // ---------------------------------------------------------------- 同事

    private static IEnumerable<LoreEntry> TeamLine()
    {
        yield return Log("team_01", 1, Round(1, 1_500));

        yield return Log("team_02", 2, Round(1, 30_000));

        yield return Codex("team_03", 3, Round(2, 2e6));

        yield return Log("team_04", 4, Round(2, 9e6));

        yield return Log("team_05", 5, Round(2, 2.5e7));

        yield return Codex("team_06", 6, Round(2, 6.5e7));

        yield return Popup("team_07", 7, Round(3, 2e8));

        yield return Popup("team_08", 8, Round(3, 4.6e8));
    }

    // ---------------------------------------------------------------- 账本

    private static IEnumerable<LoreEntry> LedgerLine()
    {
        yield return Codex("ledger_01", 1, Round(1, 8_000));

        yield return Log("ledger_02", 2, Round(1, 50_000));

        yield return Codex("ledger_03", 3, Round(2, 5e5));

        yield return Log("ledger_04", 4, Round(2, 4e6));

        yield return Codex("ledger_05", 5, Round(2, 1e7));

        yield return Popup("ledger_06", 6, Round(2, 8e7));

        yield return Log("ledger_07", 7, Round(3, 1.4e8));

        yield return Codex("ledger_08", 8, Round(3, 3.1e8));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 轮次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该轮的完成门槛，
    /// 且全包唯一（同条件的条目会同时解锁，是沉默失败）。
    /// </summary>
    private static UnlockCondition Round(int round, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(round),
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
        "her" => "🐾",
        "boss" => "🕴️",
        "team" => "👥",
        "ledger" => "📒",
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
