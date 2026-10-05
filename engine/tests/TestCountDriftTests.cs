using System.Reflection;
using System.Text.RegularExpressions;
using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 守「文档里写的**当前**用例数」与**真实用例数**一致（登记册 **W1**）。<para>
/// <b>为什么需要它</b>：这个数以散文形式散在十来个文件里，**没有一处是机器校验的**。
/// 于是每加一条守卫都要人工再扫一遍——仓库里已经手工扫过五轮，而 2026-10-04 那轮
/// 只更新了 <c>VERSIONING</c> §5 一个槽位，其余九处（README ×3、engine/README、games/README、
/// STATUS ×2、<c>ci.yml</c>、<c>api-test.ps1</c>、<c>ROADMAP</c>）全漏在 <c>564</c> 上，
/// 而真值当时已经是 <c>567</c>。<b>手工清扫不是判据，这条用例才是。</b>
/// </para>
/// <para>
/// <b>它怎么算"真值"</b>：用与 <see cref="TestRunner"/> 完全相同的发现规则
/// （程序集里所有带 <see cref="TestAttribute"/> 的方法，静态或实例都算）数一遍——
/// 所以它数的就是闸门跑的那个数，不是另抄一份。
/// </para>
/// <para>
/// <b>射程只覆盖"引擎用例数"</b>：前端冒烟条数（<c>web-smoke.mjs</c>）与端到端检查点数
/// （<c>api-test.ps1</c>）各自有别的出处，且各自会自己打印总数；把它们也塞进这条用例
/// 只会让它依赖那两个脚本的内部结构。这一条先把漂移最频繁、且真的能自算的那个数钉住。
/// </para>
/// <para>
/// <b>哪些槽位不在射程内（故意的）</b>：带日期的实测快照与历史表格一律不查——
/// 例如 <c>README.md</c> 里那句「564 个测试全部通过（2026-10-04 实测；这个数会随守卫增长）」，
/// 它记的是那一刻的事实，按仓库的规矩（<c>RELEASING</c> §2「只改'现在时'，不改'历史'」）永久冻结。
/// 判据只有一条：**读起来是"现在就是这样"的断言才进这张表**。
/// </para>
/// </summary>
public static class TestCountDriftTests
{
    /// <summary>
    /// 一处「当前用例数」槽位：<paramref name="File"/> 是仓库相对路径，
    /// <paramref name="Pattern"/> 里必须有**恰好一个**捕获组，捕到的就是那个数。
    /// <para>
    /// 模式**刻意不含数字本身**：含了就变成"钉住某个值"，文档一更新这条用例反而先红。
    /// 它锚的是数字周围的措辞，所以同一行把 564 改成 568 时，这条用例只会看"改对没有"。
    /// </para>
    /// </summary>
    private sealed record Slot(string File, string Pattern, string What);

    private static readonly Slot[] Slots =
    [
        new("README.md", @"构建 \+ 跑测试（(\d+) 个用例", "快速开始那一节的第一条命令"),
        new("README.md", @"(\d+) 个测试 \+ 自研迷你测试运行器", "仓库结构树里的 engine/tests 一行"),
        new("README.md", @"引擎 \+ 内容 \+ (\d+) 条用例 \+ Web 宿主 \+ 前端冒烟", "命令表里的 build.ps1 -Strict"),
        new("engine/README.md", @"\| (\d+) 个用例 \+ 自研迷你测试运行器", "结构表里的 tests/ 一行"),
        new("games/README.md", @"引擎 \+ 内容 \+ (\d+) 条用例 \+ Web 宿主 \+ 前端冒烟", "命令表里的 build.ps1 -Strict"),
        new("STATUS.md", @"(\d+) 个用例 \+ 自研迷你运行器", "仓库结构树里的 engine/tests 一行"),
        new("STATUS.md", @"引擎 \+ 内容 \+ (\d+) 用例 \+ Web 宿主 \+ 前端冒烟", "「现在能做什么」里的 build.ps1 -Strict"),
        new(".github/workflows/ci.yml", @"0 警告 \+ (\d+) 用例 \+ 主/Web 两条 sln", "build-and-test 作业的说明注释"),
        new("tools/api-test.ps1", @"守引擎（(\d+) 个用例", "脚本头部与单元测试的分工那一段"),
        new("engine/docs/VERSIONING.md", @"全部用例通过（当前 \*\*(\d+)\*\* 个", "§5 发布检查清单"),
        new("games/docs/ROADMAP.md", @"既有测试全绿（当前 (\d+) 个）", "G6 验收目标"),
    ];

    /// <summary>
    /// 文档里那 11 处「当前用例数」必须都等于真实用例数。<para>
    /// 失败信息逐处点名（文件 + 它写的数 + 应有的数），因为"哪一处、写成了几"是这条用例唯一有用的输出。
    /// </para>
    /// </summary>
    [Test]
    public static void DocumentedCurrentTestCount_MatchesTheRealOne()
    {
        string? root = RepositoryPaths.Find();
        Check.NotNull(root, "这条用例必须在仓库里跑：它要读仓库根下的那几份文档（没找到 NekoClicker.sln）。");

        int actual = CountTestMethods();

        // 假绿防线：反射数出 0 个（或明显变少）说明是这条用例自己写错了，而不是"文档恰好对"。
        // 这类"核对型"守卫最坏的失效形态就是它静默地什么都不核对。
        Check.AtLeast(actual, 100, $"只反射出 {actual} 条用例——这是这条用例数错了，不是仓库变小了。");

        // 每条槽位都必须**恰好**命中一次：命中 0 次说明文档被改写（锚点失效，这条用例从此空转），
        // 命中多次说明锚点太松（会去核对另一句话里的数字）。两种都要点名报出来。
        List<string> missing = [];
        List<string> ambiguous = [];
        List<string> wrong = [];

        foreach (Slot slot in Slots)
        {
            string path = Path.Combine(root!, slot.File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                missing.Add($"{slot.File}（文件不存在）— {slot.What}");
                continue;
            }

            MatchCollection matches = Regex.Matches(File.ReadAllText(path), slot.Pattern);
            if (matches.Count == 0)
            {
                missing.Add($"{slot.File}（锚点没命中）— {slot.What}：模式 /{slot.Pattern}/");
                continue;
            }

            if (matches.Count > 1)
            {
                ambiguous.Add($"{slot.File}（命中 {matches.Count} 次）— {slot.What}：模式 /{slot.Pattern}/");
                continue;
            }

            string written = matches[0].Groups[1].Value;
            if (written != actual.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                wrong.Add($"{slot.File}：写着 {written}，真实是 {actual} — {slot.What}");
            }
        }

        string detail = string.Join(
            Environment.NewLine,
            wrong.Select(line => "    数字过期：" + line)
                .Concat(missing.Select(line => "    锚点失效：" + line))
                .Concat(ambiguous.Select(line => "    锚点歧义：" + line)));

        Check.Equal(
            0,
            wrong.Count + missing.Count + ambiguous.Count,
            $"文档里的「当前用例数」与真实用例数不一致（真实 {actual} 条，共 {Slots.Length} 处槽位）：" +
            Environment.NewLine + detail + Environment.NewLine +
            "怎么修：把上面点名的每一处改成真实数字。**只改「现在时」的断言**——" +
            "带日期的实测快照、CHANGELOG 的历史条目、OPEN_WORK 的各轮记录一律不动" +
            "（RELEASING §2：只改'现在时'，不改'历史'）。" +
            "这条用例的存在理由就是：这件事以前靠人工扫，扫过五轮仍然漏了九处。");

        Console.WriteLine($"      当前用例数：{actual} 条，11 处文档槽位一致。");
    }

    /// <summary>
    /// 真实用例数：与 <see cref="TestRunner"/> 用**同一条**发现规则数一遍。<para>
    /// 规则（照抄运行器）：程序集里每个类型，凡带 <see cref="TestAttribute"/> 的方法都算一条，
    /// <c>public</c> / <c>static</c> / <c>instance</c> 都算——<b>不是</b>另立一套口径，
    /// 否则这条守卫数出来的就不是闸门跑的那个数。
    /// </para>
    /// </summary>
    private static int CountTestMethods()
    {
        Assembly assembly = typeof(TestCountDriftTests).Assembly;
        int count = 0;

        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<TestAttribute>() is null) continue;
                count++;
            }
        }

        return count;
    }
}
