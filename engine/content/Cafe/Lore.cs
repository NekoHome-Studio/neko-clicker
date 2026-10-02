using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cafe;

/// <summary>
/// 叙事条目：51 条，分三条线。<para>
/// 节奏是刻意编排的——开场 10 分钟内只会放出 3 条，其余按进度慢慢渗出来。
/// 世界观不该一次讲完，否则玩家在还没建立起"这家店有点不对"的感觉之前
/// 就已经被告知了全部真相。
/// </para>
/// <para>
/// <b>条件编排规则（三条，都别破坏）</b>：
/// <list type="number">
///   <item><b>任何两条的 <see cref="LoreEntry.Reveal"/> 必须不同。</b>同一个条件必然在同一瞬间
///   一起解锁——之前 50 条里有 23 条落在 10 个重复组里（最坏 3 条同时），
///   玩家看到的是"一口气弹出三段剧情"。下面每个数值都刻意错开，看着不圆整是故意的。</item>
///   <item><b>每条线用一条递增的"累计赚取"骨架定序。</b>建筑数 / 成就数 / 点击数的门槛
///   只是"顺手满足"的附加条件——它们一律在这个时间点<b>之前</b>就已经达到。
///   原因：成就与建筑类里程碑全在开局一小时内触发，而赚钱类要到几小时后，
///   把两者交替排列必然造成"后面的条目先亮、前面的还锁着"（实测 11 处倒挂）。</item>
///   <item><b>店休 / 转生类的条目只能放在线的尾部。</b>转生时机由玩家决定，
///   夹在中间就会因为玩家先攒钱后店休而倒挂。尾部内部再用转生等级递增定序。</item>
/// </list>
/// </para>
/// <para>
/// 条目在源码里的排列顺序即<b>阅读顺序</b>（<see cref="LoreEntry.Order"/>），
/// 所以 id 编号看起来是跳的——id 是稳定标识符，不承担顺序含义。
/// </para>
/// </summary>
internal static class Lore
{
    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Cafe"));

    /// <summary>本包的散文文件（随包复制到 content/Cafe/text.json）。取不到就抛，绝不回退成空白。</summary>
    internal static ContentText Prose => ProseCache.Value;

    /// <summary>三条剧情线。</summary>
    public static StorylineDefinition[] Storylines =>
    [
        Line("door", 20),
        Line("regular", 16),
        Line("supplier", 15),
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
        .. DoorLine(),
        .. RegularLine(),
        .. SupplierLine(),
    ];

    // ---------------------------------------------------------------- 主线：两界之门

    private static IEnumerable<LoreEntry> DoorLine()
    {
        yield return Popup("door_01", 1, UnlockCondition.ClicksAtLeast(1));

        yield return Log("door_02", 2, UnlockCondition.TotalBuildingsAtLeast(1));

        yield return Log("door_03", 3, UnlockCondition.TotalBuildingsAtLeast(5));

        yield return Log("door_04", 4, UnlockCondition.EarnedThisRunAtLeast(1e7));

        yield return Codex("door_05", 5, UnlockCondition.EarnedThisRunAtLeast(5e7));

        yield return Popup("door_06", 6, UnlockCondition.EarnedThisRunAtLeast(2e9));

        yield return Log("door_07", 7, UnlockCondition.EarnedThisRunAtLeast(4e9));

        yield return Log("door_08", 8, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(6e9), UnlockCondition.AchievementsAtLeast(8)));

        yield return Log("door_09", 9, UnlockCondition.EarnedThisRunAtLeast(1e10));

        yield return Log("door_10", 10, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2e10), UnlockCondition.TotalBuildingsAtLeast(150)));

        yield return Codex("door_11", 11, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(3e10), UnlockCondition.AchievementsAtLeast(12)));

        yield return Log("door_12", 12, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(5e10), UnlockCondition.TotalBuildingsAtLeast(180)));

        yield return Log("door_14", 13, UnlockCondition.EarnedThisRunAtLeast(1e11));

        yield return Log("door_15", 14, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2e11), UnlockCondition.AchievementsAtLeast(14)));

        yield return Codex("door_16", 15, UnlockCondition.EarnedThisRunAtLeast(1.2e12));

        yield return Popup("door_18", 16, UnlockCondition.EarnedThisRunAtLeast(4e12));

        yield return Log("door_19", 17, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.1e13), UnlockCondition.AchievementsAtLeast(20)));

        // ---- 店休 / 转生分支。放在末尾不是偷懒：转生时机由玩家决定，
        // 夹在赚钱类条目中间必然倒挂。尾部内部再按转生等级递增。
        yield return Popup("door_13", 18, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.5e13), UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Log("door_17", 19, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2.2e13), UnlockCondition.PrestigeLevelAtLeast(2)));

        yield return Popup("door_20", 20, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(3e13), UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 支线：常客们的记忆

    private static IEnumerable<LoreEntry> RegularLine()
    {
        yield return Log("regular_01", 1, UnlockCondition.EarnedThisRunAtLeast(2e7));

        yield return Log("regular_02", 2, UnlockCondition.EarnedThisRunAtLeast(1e8));

        yield return Log("regular_03", 3, UnlockCondition.EarnedThisRunAtLeast(5e8));

        yield return Codex("regular_04", 4, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(9e8), UnlockCondition.AchievementsAtLeast(9)));

        yield return Log("regular_06", 5, UnlockCondition.EarnedThisRunAtLeast(8e9));

        yield return Log("regular_07", 6, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.5e10), UnlockCondition.AchievementsAtLeast(10)));

        yield return Codex("regular_08", 7, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2.5e10), UnlockCondition.TotalBuildingsAtLeast(160)));

        yield return Log("regular_09", 8, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(4e10), UnlockCondition.TotalBuildingsAtLeast(200)));

        yield return Log("regular_10", 9, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(7e10), UnlockCondition.ClicksAtLeast(1_000)));

        yield return Log("regular_12", 10, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.35e11), UnlockCondition.AchievementsAtLeast(13)));

        yield return Log("regular_13", 11, UnlockCondition.EarnedThisRunAtLeast(3e11));

        yield return Codex("regular_14", 12, UnlockCondition.EarnedThisRunAtLeast(1.6e12));

        yield return Log("regular_15", 13, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2.5e12), UnlockCondition.AchievementsAtLeast(18)));

        // ---- 店休分支（同上：只能放尾部）。
        yield return Log("regular_05", 14, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(5e12), UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Popup("regular_11", 15, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.3e13), UnlockCondition.PrestigeLevelAtLeast(2)));

        yield return Popup("regular_16", 16, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2.6e13), UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 支线：异世界的供货商

    private static IEnumerable<LoreEntry> SupplierLine()
    {
        yield return Log("supplier_01", 1, UnlockCondition.EarnedThisRunAtLeast(7e7));

        yield return Codex("supplier_02", 2, UnlockCondition.EarnedThisRunAtLeast(2e8));

        yield return Log("supplier_03", 3, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(6e8), UnlockCondition.TotalBuildingsAtLeast(170)));

        yield return Log("supplier_04", 4, UnlockCondition.EarnedThisRunAtLeast(3e9));

        yield return Log("supplier_05", 5, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(6.5e9), UnlockCondition.AchievementsAtLeast(9)));

        yield return Codex("supplier_06", 6, UnlockCondition.EarnedThisRunAtLeast(1.6e10));

        yield return Log("supplier_07", 7, UnlockCondition.EarnedThisRunAtLeast(2.2e10));

        yield return Log("supplier_08", 8, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(3.5e10), UnlockCondition.TotalBuildingsAtLeast(220)));

        yield return Popup("supplier_09", 9, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(6e10), UnlockCondition.AchievementsAtLeast(15)));

        yield return Log("supplier_10", 10, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.4e11), UnlockCondition.AchievementsAtLeast(16)));

        yield return Log("supplier_11", 11, UnlockCondition.EarnedThisRunAtLeast(1.8e12));

        yield return Codex("supplier_12", 12, UnlockCondition.EarnedThisRunAtLeast(4.5e12));

        // ---- 店休分支 + 尾声。
        yield return Log("supplier_13", 13, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(8e12), UnlockCondition.PrestigeLevelAtLeast(1)));

        yield return Popup("supplier_14", 14, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(1.8e13), UnlockCondition.PrestigeLevelAtLeast(2)));

        // 尾声：把这家店接回共享的神话体系。刻意放在全包最后——
        // 它不是"又一条供货商的怪癖"，而是"你一直以为自己在开店"的答案。
        yield return Popup("supplier_15", 15, UnlockCondition.All( UnlockCondition.EarnedThisRunAtLeast(2.8e13), UnlockCondition.PrestigeLevelAtLeast(3)));
    }

    // ---------------------------------------------------------------- 辅助

    private static LoreEntry Log(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Log);

    private static LoreEntry Popup(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Popup);

    private static LoreEntry Codex(string id, int order, UnlockCondition reveal)
        => Make(id, order, reveal, LoreChannel.Codex);

    private static LoreEntry Make(
        string id, int order, UnlockCondition reveal, LoreChannel channel)
    {
        // id 前缀即剧情线：door_/regular_/supplier_ 与 Storylines 里的 id 对应。
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
        "door" => "🚪",
        "regular" => "🪑",
        "supplier" => "📦",
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
