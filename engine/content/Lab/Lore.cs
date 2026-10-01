using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 叙事条目：40 条，分四条线。<para>
/// 节奏跟着<b>批次</b>走——每开一批，故事往前推一段。开场 10 分钟内只放 3 条。
/// </para>
/// <para>
/// <b>散文在 <c>content/Lab/text.json</c>，代码里只留结构。</b>标题与正文从文本文件按 id 取，
/// <c>Reveal</c>（条件树）、<c>Order</c>、<c>Channel</c> 一律留在下面——它们是逻辑，不是散文。
/// id 对不上、文件缺条目、文件里有代码不读的孤儿，三种情况都会当场抛（见 <see cref="Prose"/>）。
/// 这是"剧情做成可读文件"的试点包。
/// </para>
/// <para>
/// <b>三条纪律</b>（与内容包 #1/#2 相同，都由测试或构建期守住）：
/// <list type="number">
///   <item>任何两条的 <c>Reveal</c> 不得相同——同条件的两个东西必然同时解锁。</item>
///   <item>同一条线内，门槛随 <c>Order</c> 单调递增——否则图鉴里会出现"第 3 条还锁着，第 4 条已亮"。</item>
///   <item>门槛一律取该批次完成门槛的固定百分比，且 ≤ 它——否则玩家会在够条件前舍命走人，这条永远读不到。</item>
/// </list>
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Lab"));

    /// <summary>
    /// 本包的散文文件（随包复制到 <c>content/Lab/text.json</c>）。<para>
    /// 用懒初始化而不是静态字段直接加载：文件坏掉时抛的是"哪一条对不上"，
    /// 而不是被包成 <c>TypeInitializationException</c> 的谜语。
    /// 用 <see cref="Lazy{T}"/> 而不是 <c>??=</c>：内容可能被多个线程同时首次构建
    /// （宿主扫包、测试并行跑），而这份文本里记着"哪些 id 已经取过"的可变状态，
    /// 不能有两个实例各记一半。
    /// </para>
    /// </summary>
    private static ContentText Prose => ProseCache.Value;

    /// <summary>四条剧情线。名称 / 主题 / 图标来自文本文件，条数留在代码里（它是纪律，不是散文）。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("sample", 14),
        Line("researcher", 10),
        Line("board", 8),
        Line("data", 8),
    ];

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
        .. SampleLine(),
        .. ResearcherLine(),
        .. BoardLine(),
        .. DataLine(),
    ];

    /// <summary>
    /// 文本文件里"有、但代码从不取用"的条目会在这里抛（孤儿文本）。<para>
    /// 调用点必须在 <see cref="Storylines"/> 与 <see cref="Entries"/> 都被取过之后——
    /// 现在只有 <c>LabContent.Build()</c> 末尾一处。孤儿必须炸的理由见 <see cref="ContentText"/>：
    /// 删了剧情却留着文本，运行、断言、界面都不会有任何反应，于是那段死文本会一直躺在文件里等人改。
    /// </para>
    /// </summary>
    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();

    // ---------------------------------------------------------------- 主线：样本

    private static IEnumerable<LoreEntry> SampleLine()
    {
        yield return Popup("sample_01", 1, Era(1, 2e4));

        yield return Log("sample_02", 2, Era(2, 1.2e6));

        yield return Log("sample_03", 3, Era(2, 2.6e6));

        yield return Codex("sample_04", 4, Era(3, 1.2e7));

        yield return Log("sample_05", 5, Era(3, 2.6e7));

        yield return Popup("sample_06", 6, Era(4, 3e7));

        yield return Log("sample_07", 7, Era(4, 7e7));

        yield return Log("sample_08", 8, Era(5, 5e7));

        yield return Codex("sample_09", 9, Era(5, 9e7));

        yield return Log("sample_10", 10, Era(6, 8e7));

        yield return Popup("sample_11", 11, Era(6, 1.5e8));

        yield return Log("sample_12", 12, Era(7, 1.2e8));

        yield return Codex("sample_13", 13, Era(7, 3e8));

        yield return Popup("sample_14", 14, Era(7, 6e8));
    }

    // ---------------------------------------------------------------- 支线：研究员

    private static IEnumerable<LoreEntry> ResearcherLine()
    {
        yield return Log("researcher_01", 1, Era(1, 4.5e4));

        yield return Log("researcher_02", 2, Era(2, 4.1e6));

        yield return Codex("researcher_03", 3, Era(2, 5.5e6));

        yield return Log("researcher_04", 4, Era(3, 4.1e7));

        yield return Popup("researcher_05", 5, Era(3, 5.5e7));

        yield return Log("researcher_06", 6, Era(4, 1.1e8));

        yield return Log("researcher_07", 7, Era(5, 1.4e8));

        yield return Codex("researcher_08", 8, Era(5, 1.9e8));

        yield return Log("researcher_09", 9, Era(6, 2.4e8));

        yield return Popup("researcher_10", 10, Era(7, 4.5e8));
    }

    // ---------------------------------------------------------------- 支线：委员会

    private static IEnumerable<LoreEntry> BoardLine()
    {
        yield return Log("board_01", 1, Era(2, 7e6));

        yield return Log("board_02", 2, Era(3, 6.5e7));

        yield return Codex("board_03", 3, Era(4, 1.5e8));

        yield return Popup("board_04", 4, Era(5, 1.9e8));

        yield return Log("board_05", 5, Era(5, 2.3e8));

        yield return Log("board_06", 6, Era(6, 3.2e8));

        yield return Log("board_07", 7, Era(7, 7.5e8));

        yield return Popup("board_08", 8, Era(7, 8.8e8));
    }

    // ---------------------------------------------------------------- 支线：数据

    private static IEnumerable<LoreEntry> DataLine()
    {
        yield return Log("data_01", 1, Era(1, 7e4));

        yield return Codex("data_02", 2, Era(2, 8.8e6));

        yield return Log("data_03", 3, Era(3, 8.8e7));

        yield return Log("data_04", 4, Era(4, 1.9e8));

        yield return Log("data_05", 5, Era(5, 2.6e8));

        yield return Codex("data_06", 6, Era(6, 3.9e8));

        yield return Log("data_07", 7, Era(6, 4.6e8));

        yield return Popup("data_08", 8, Era(7, 9.6e8));
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 批次门槛 + 层内里程碑。<paramref name="milestone"/> 必须 ≤ 该批次的完成门槛，
    /// 且全包唯一。
    /// </summary>
    private static UnlockCondition Era(int batch, double milestone)
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(batch),
            UnlockCondition.EarnedThisRunAtLeast(milestone));

    private static LoreEntry Popup(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Popup);

    private static LoreEntry Log(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Log);

    private static LoreEntry Codex(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Codex);

    private static LoreEntry Make(string id, int order, UnlockCondition reveal, LoreChannel channel)
    {
        int underscore = id.IndexOf('_');
        string storylineId = underscore > 0 ? id[..underscore] : id;

        return new LoreEntry
        {
            Id = id,
            // 散文在这里从文本文件取。取不到就抛，绝不回退成空白或 id 本身。
            Title = Prose.Text("lore", id, "title"),
            Body = Prose.Text("lore", id, "body"),
            Icon = IconFor(storylineId),
            StorylineId = storylineId,
            Order = order,
            Reveal = reveal,
            Channel = channel,
        };
    }

    /// <summary>剧情线的图标同样来自文本文件；缺一个就抛，不留"看起来还在跑"的静默回退。</summary>
    private static string IconFor(string storylineId) => Prose.Text("storylines", storylineId, "icon");
}
