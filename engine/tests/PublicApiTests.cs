using System.Reflection;
using System.Text;
using NekoClicker.Core;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 公开 API 的版本契约守卫。<para>
/// 这个仓库的核心主张是"一套可以被任意宿主引用的框架"。主张要成立，光有 API 不够，
/// 还需要一句能兑现的话：<b>你的代码不会因为我发了个新版本就编不过</b>。
/// 这条用例就是那句话的执行手段——它把 <c>NekoClicker.Core</c> 的公开表面与
/// 提交在仓库里的快照 <c>engine/core/PublicApi.txt</c> 逐项对比。
/// </para>
/// <para>
/// <b>它红了怎么办</b>（两种可能，处理方式完全不同，不要一律"重新生成快照"）：
/// <list type="number">
///   <item>你<b>没打算</b>改公开 API → 这是误伤，多半是改了签名或删了成员，改回去。</item>
///   <item>你<b>打算</b>改 → 那就按 <c>engine/docs/VERSIONING.md</c> 走完整流程：
///     只增不改就升 minor、有不兼容改动就升 major，更新 <c>Directory.Build.props</c> 的
///     <c>Version</c>、补 <c>CHANGELOG.md</c>，最后用 <c>tools\public-api.ps1</c> 重新生成快照。</item>
/// </list>
/// 换句话说：<b>重新生成快照是流程的最后一步，不是第一步。</b>
/// 先重新生成会让这条守卫变成橡皮图章。
/// </para>
/// </summary>
public static class PublicApiTests
{
    /// <summary>核心：当前公开 API 必须与提交的快照逐项一致。</summary>
    [Test]
    public static void PublicApi_MatchesTheCommittedSnapshot()
    {
        string[] saved = PublicApiSnapshot.ReadLines();
        string[] current = PublicApiSurface.Dump(typeof(GameEngine).Assembly);

        // 防止把一份空快照 / 读不到资源当成"通过"——那会让守卫静默失效。
        Check.AtLeast(saved.Length, 100, "快照文件行数过少，多半是读错了资源或文件被清空。");
        Check.AtLeast(current.Length, 100, "当前公开 API 行数过少，多半是程序集加载错了。");

        Check.True(
            saved[0].StartsWith(PublicApiSurface.HeaderPrefix, StringComparison.Ordinal),
            $"快照首行不是预期的头部：<{saved[0]}>。");

        string? report = PublicApiSurface.Compare(saved, current);
        if (report is null) return;

        Check.Fail(
            report +
            "\n  （若这是有意改动：按 engine/docs/VERSIONING.md 先改版本号与 CHANGELOG，再跑 tools\\public-api.ps1 更新快照。）");
    }

    /// <summary>
    /// 快照描述的版本必须就是当前版本——这是"改了 API 必须同时升版本"的执法点。<para>
    /// 少了它，下面这条路是通的：改 API → 重新生成快照 → 用例全绿，而版本号一直停在 1.0.0，
    /// 于是没有任何人知道这个 1.0.0 已经不是当初发布的那份 1.0.0 了。
    /// </para>
    /// </summary>
    [Test]
    public static void PublicApi_SnapshotRecordsTheCurrentVersion()
    {
        string[] snapshot = PublicApiSnapshot.ReadLines();
        string? recorded = PublicApiSurface.VersionInHeader(snapshot[0]);
        string current = ApiVersion.Current;

        Check.NotNull(recorded, $"快照首行里没有 version= 字段：<{snapshot[0]}>。");
        Check.Equal(
            current,
            recorded!,
            $"快照记录的版本与当前程序集版本不一致：快照说 {recorded}，程序集是 {current}。" +
            "改了公开 API 就要同时升版本号（Directory.Build.props）并重新生成快照。");
    }

    /// <summary>
    /// 快照确实覆盖了该覆盖的东西——这是一条"守卫本身没瞎"的用例。<para>
    /// 如果哪天有人把 <c>GetExportedTypes</c> 换成 <c>GetTypes</c>、或者给成员过滤加了个过头的条件，
    /// 上一条用例会因为"两边都少"而继续绿；这一条会红。
    /// </para>
    /// </summary>
    [Test]
    public static void PublicApi_CoversEveryExportedTypeAndEveryPublicMember()
    {
        string[] snapshot = PublicApiSurface.Dump(typeof(GameEngine).Assembly);
        HashSet<string> lines = [.. snapshot];

        foreach (Type type in typeof(GameEngine).Assembly.GetExportedTypes())
        {
            string typeName = type.FullName ?? type.Name;

            // 声明行的形态是「修饰符 种类 类型名[<类型参数>] [: 基类/接口...]」，
            // 所以取冒号之前那段、再取最后一个空白之后的名字。
            string bareName = typeName.Split('<')[0];
            Check.True(
                snapshot.Any(line => !line.StartsWith(' ') && DeclaredTypeName(line) == bareName),
                $"快照里没有类型声明行：{typeName}。");

            // 每个会占一行的方法都必须真的在快照里。枚举规则与 Dumper 共用同一个判据
            // （IsRenderedAsMethodLine），避免两边各自漂移。
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!PublicApiSurface.IsRenderedAsMethodLine(method)) continue;

                // 定位方式：在 "方法名" 前留一个空格来界定名字边界（返回类型与名字之间就有空格），
                // 泛型方法则是 "方法名<"。这样同名重载必须各自都在快照里才算过。
                Check.True(
                    lines.Any(line => line.StartsWith("  method ", StringComparison.Ordinal)
                        && (line.Contains(" " + method.Name + "(", StringComparison.Ordinal)
                            || line.Contains(" " + method.Name + "<", StringComparison.Ordinal))),
                    $"快照里没有方法行：{typeName}.{method.Name}。");
            }
        }
    }

    /// <summary>
    /// 判别力证明（故障注入）。<para>
    /// 一条"永远不报警"的守卫与一条"正确"的守卫在测试输出里长得一模一样。
    /// 本项目的惯例（从阶段 2.7 起）是：<b>用一次真实的失败证明守卫会红</b>。
    /// 真实用例跑的是一整个程序集，没法在测试里塞一份坏程序集；
    /// 所以这里对比较逻辑本身做故障注入——它才是"红不红"的判官。
    /// </para>
    /// </summary>
    [Test]
    public static void PublicApiGuard_RejectsEveryKindOfBreakingChange()
    {
        string[] baseline =
        [
            "# 测试用快照 format=1 assembly=Demo",
            string.Empty,
            "public sealed class Demo.Engine",
            "  method System.Void Buy(System.String! id)",
            "  prop System.Double Cookies { get; }",
        ];

        Check.Null(PublicApiSurface.Compare(baseline, baseline), "同一份快照不该报差异。");

        // ① 删掉一个方法：调用方会编不过，必须判为不兼容。
        string[] removed =
        [
            "# 测试用快照 format=1 assembly=Demo",
            string.Empty,
            "public sealed class Demo.Engine",
            "  prop System.Double Cookies { get; }",
        ];
        string? removedReport = PublicApiSurface.Compare(baseline, removed);
        Check.NotNull(removedReport, "删掉公开方法必须报警。");
        Check.Contains(removedReport!, "不兼容");
        Check.Contains(removedReport!, "Buy");

        // ② 改签名（同名方法、参数类型变了）：源与二进制都不兼容。
        string[] changed = [.. baseline[..^2], "  method System.Void Buy(System.Int32! amount)", baseline[^1]];
        string? changedReport = PublicApiSurface.Compare(baseline, changed);
        Check.NotNull(changedReport, "改方法签名必须报警。");
        Check.Contains(changedReport!, "不兼容");

        // ③ 只新增一个成员：不破坏任何人，但必须被看见——否则"只增不改才升 minor"就没人执行。
        string[] added = [.. baseline, "  method System.Void Prestige()"];
        string? addedReport = PublicApiSurface.Compare(baseline, added);
        Check.NotNull(addedReport, "新增公开成员也必须报警（要有人去升 minor 并更新快照）。");
        Check.Contains(addedReport!, "新增");
        // 只有新增时不该出现"不兼容"字样，否则版本决策会被误导。
        Check.False(addedReport!.Contains("不兼容", StringComparison.Ordinal), "只新增成员不该被说成不兼容。");

        // ④ 可空标注收紧：把 string? 改成 string，调用方的 ?? 会开始警告——也算契约变化。
        string[] tightened = [.. baseline[..^2], "  method System.Void Buy(System.String id)", baseline[^1]];
        Check.NotNull(PublicApiSurface.Compare(baseline, tightened), "可空标注变化必须报警。");

        // ⑤ 纯顺序调整不算差异（否则快照会变成"改一行注释就红"的噪音源）。
        string[] reordered = [baseline[0], baseline[1], baseline[2], baseline[4], baseline[3]];
        Check.Null(PublicApiSurface.Compare(baseline, reordered), "成员顺序变化不该被当成 API 变化。");
    }

    /// <summary>从类型声明行里取出类型名（去掉修饰符、种类、类型参数与基类清单）。</summary>
    private static string DeclaredTypeName(string declarationLine)
    {
        string withoutBases = declarationLine.Split(':')[0].TrimEnd();
        return withoutBases.Split(' ')[^1].Split('<')[0];
    }
}
