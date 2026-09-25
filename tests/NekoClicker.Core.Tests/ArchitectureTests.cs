using System.Reflection;
using System.Text;
using NekoClicker.Content.Cafe;
using NekoClicker.Content.Neko;
using NekoClicker.Content.NineLives;
using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 架构不变量（ROADMAP §4 的 A1 / R7）。<para>
/// 这些用例存在的意义不是"证明今天是对的"，而是<b>在框架被侵蚀的第一次就报警</b>：
/// 一条内容需求如果只能靠"在核心里写一个 if"实现，这里会红。
/// </para>
/// <list type="bullet">
///   <item>A1：核心不认识内容——不引用任何 <c>NekoClicker.Content.*</c> 项目，且程序集里不出现内容 id。</item>
///   <item>R7：每个内容包一个 csproj——内容包之间不得互相引用。</item>
/// </list>
/// </summary>
public static class ArchitectureTests
{
    private const string ContentAssemblyPrefix = "NekoClicker.Content.";

    /// <summary>A1：核心程序集不得引用任何内容项目。</summary>
    [Test]
    public static void Architecture_CoreDoesNotReferenceContentProjects()
    {
        Assembly core = typeof(GameEngine).Assembly;

        string[] offenders =
        [
            .. core.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => name.StartsWith(ContentAssemblyPrefix, StringComparison.Ordinal)),
        ];

        Check.Equal(
            0,
            offenders.Length,
            "核心引用了内容项目：" + string.Join("、", offenders) + "（A1 不变量被破坏）。");
    }

    /// <summary>A1：核心程序集的字符串字面量里不得出现任何内容 id。</summary>
    [Test]
    public static void Architecture_CoreContainsNoContentIds()
    {
        HashSet<string> contentIds = [];
        CollectIds(contentIds, NekoContent.Build());
        CollectIds(contentIds, CafeContent.Build());
        CollectIds(contentIds, NineLivesContent.Build());

        Check.AtLeast(contentIds.Count, 100, "内容 id 集合不应为空（否则这条用例是假绿）。");

        byte[] coreImage = File.ReadAllBytes(typeof(GameEngine).Assembly.Location);
        List<string> found = [.. contentIds.Where(id => ContainsUtf16Token(coreImage, id))];

        Check.Equal(
            0,
            found.Count,
            "核心程序集里出现了内容 id：" + string.Join("、", found) + "（A1 不变量被破坏）。");
    }

    /// <summary>R7：内容包之间不得互相引用（换包必须能独立发布）。</summary>
    [Test]
    public static void Architecture_ContentPacksDoNotReferenceEachOther()
    {
        Assembly neko = typeof(NekoContent).Assembly;
        Assembly cafe = typeof(CafeContent).Assembly;
        Assembly nineLives = typeof(NineLivesContent).Assembly;

        foreach ((Assembly left, Assembly right) in new[]
        {
            (neko, cafe), (neko, nineLives), (cafe, nineLives), (cafe, neko), (nineLives, neko), (nineLives, cafe),
        })
        {
            Check.False(
                References(left, right),
                $"{left.GetName().Name} 引用了 {right.GetName().Name}：内容包之间必须彼此独立。");
        }
    }

    /// <summary>
    /// A2：<c>Era</c> 只能通过四个接缝进入引擎，引擎里不得出现针对具体层号的比较。<para>
    /// 这条测试防的是"为了让某一层有点特殊效果，在核心里写一个 if (era == 3)" ——
    /// 一旦开了这个头，十个内容包的差异最后都会堆回核心里。
    /// </para>
    /// </summary>
    [Test]
    public static void Architecture_CoreHasNoEraSpecificBranches()
    {
        Assembly core = typeof(GameEngine).Assembly;

        // 具体层号只能是 1（默认值），其余数字出现在 Era 相关的分支里都是可疑的。
        string[] forbidden =
        [
            "era ==", "era !=", "Era ==", "Era !=",
            "era >", "era <", "Era >", "Era <",
            "switch (era", "switch (Era",
        ];

        byte[] coreImage = File.ReadAllBytes(core.Location);
        List<string> found = [.. forbidden.Where(token => ContainsUtf16Token(coreImage, token))];

        Check.Equal(
            0,
            found.Count,
            "核心程序集里出现了针对层号的分支：" + string.Join("、", found) +
            "（A2 不变量被破坏：Era 只能通过 BalanceFor / 修饰符来源 / 解锁指标 / 继承参数四个接缝进入引擎）。");
    }

    /// <summary>收集一个内容包的全部 id（建筑 / 升级 / 成就 / 增益 / 随机事件）。</summary>
    private static void CollectIds(HashSet<string> ids, GameContent content)
    {
        foreach (BuildingDefinition building in content.Buildings) ids.Add(building.Id);
        foreach (UpgradeDefinition upgrade in content.Upgrades) ids.Add(upgrade.Id);
        foreach (AchievementDefinition achievement in content.Achievements) ids.Add(achievement.Id);
        foreach (BuffDefinition buff in content.Buffs) ids.Add(buff.Id);
        foreach (GoldenCookieOutcome outcome in content.GoldenCookieOutcomes) ids.Add(outcome.Id);
    }

    private static bool References(Assembly assembly, Assembly target)
        => assembly.GetReferencedAssemblies()
            .Any(reference => string.Equals(reference.Name, target.GetName().Name, StringComparison.Ordinal));

    /// <summary>
    /// 判断程序集镜像里是否存在以 UTF-16 存储的指定字符串字面量。<para>
    /// C# 字符串字面量在 ECMA-335 的 #US 堆里是 UTF-16，而类型/成员名是 UTF-8，
    /// 所以这个扫描只会命中字面量。前后各取一个字符做边界判断，避免命中更长的字符串。
    /// </para>
    /// </summary>
    private static bool ContainsUtf16Token(byte[] image, string token)
    {
        byte[] needle = Encoding.Unicode.GetBytes(token);
        if (needle.Length == 0 || needle.Length > image.Length) return false;

        for (int offset = 0; offset + needle.Length <= image.Length; offset += 2)
        {
            if (!image.AsSpan(offset, needle.Length).SequenceEqual(needle)) continue;

            char before = offset >= 2 ? ReadUtf16(image, offset - 2) : '\0';
            char after = offset + needle.Length + 1 < image.Length ? ReadUtf16(image, offset + needle.Length) : '\0';
            if (!IsWordChar(before) && !IsWordChar(after)) return true;
        }

        return false;
    }

    private static char ReadUtf16(byte[] image, int offset)
        => (char)(image[offset] | (image[offset + 1] << 8));

    private static bool IsWordChar(char value)
        => char.IsLetterOrDigit(value) || value == '_';
}
