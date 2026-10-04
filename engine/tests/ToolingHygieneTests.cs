using System.Diagnostics;
using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 工具链卫生：<b>仓库自己的</b> <c>.ps1</c> 必须带 UTF-8 BOM。<para>
/// <b>为什么需要这条守卫</b>：仓库的 <c>.ps1</c> 是中文注释、由 <b>Windows PowerShell 5.1</b> 执行
/// （<c>start.cmd</c> 用 <c>-ExecutionPolicy Bypass</c> 调进来）。5.1 对<b>没有 BOM</b> 的文件按 GBK 解码，
/// 中文注释变成乱码，解析在到达第一行代码之前就失败（实测 <c>TerminatorExpectedAtEndOfString</c> /
/// <c>RedirectionNotSupported</c>）——整个脚本<b>一项都不跑</b>。而这个失效有一半是沉默的：
/// <c>pwsh</c>（PowerShell 7）默认按 UTF-8 读无 BOM 文件，所以同一份文件在开发机上"看起来是好的"。
/// </para>
/// <para>
/// 这件事在 2026-10-04 一天里咬了至少三次，最后一次直接落在 <c>main</c> 上：<c>tools/build.ps1</c> 与
/// <c>tools/api-test.ps1</c> 的 BOM 被编辑工具剥掉，两个脚本都 parse error（提交 <c>04c67e0</c> 补回）。
/// 在此之前它<b>只靠人记得</b>——<c>edit</c> 一类的工具保存 <c>.ps1</c> 时会把 BOM 丢掉。
/// </para>
/// <para>
/// <b>为什么是这条 C# 用例，而不是 <c>build.ps1</c> 里加一段、或者放进 <c>web-smoke.mjs</c></b>：
/// ① 它在 <c>tools\build.ps1 -Strict</c> 这条闸门里<b>无条件</b>跑——<c>web-smoke.mjs</c> 可以被
/// <c>-SkipWebSmoke</c> 显式跳过，闸门会漏，而 C# 那一段没有跳过开关；
/// ② 它在<b>所有 <c>.ps1</c> 之外</b>执行，所以连 <c>build.ps1</c> 自己被剥了 BOM 都抓得到
/// ——写进 <c>build.ps1</c> 里的守卫恰恰抓不到 <c>build.ps1</c> 本身；
/// ③ 代价是一条被 SDK 自动收进来的测试文件：不新增工程、不新增脚本、不动公开 API。
/// </para>
/// <para>
/// <b>射程：枚举 <c>git ls-files '*.ps1'</c>（仓库追踪的脚本），<u>不是</u>扫工作目录。</b>
/// 这不是实现细节，是这条守卫的判据本身。第一版扫的是<b>文件系统</b>（从仓库根递归 <c>*.ps1</c>，
/// 只跳过 <c>.git/.tmp/bin/obj/artifacts/node_modules</c>），于是它的结果取决于
/// <b>别人留在工作区里的未追踪文件</b>：2026-10-04 主树上有三个更早的代理留下的、已被
/// <c>.gitignore</c> 忽略的临时脚本（<c>.probe/fixcounts.ps1</c>、<c>.probe/inject.ps1</c>、
/// <c>.probe/injectb.ps1</c>，开头三字节 <c>24 45 72</c>），闸门当场变红
/// （`3/14`、`564 通过 / 1 失败`），而写这条守卫的那棵 worktree 里没有 <c>.probe/</c>，
/// 作者量到的是 <c>565/565</c>。<b>一条结果依赖未追踪本地文件的守卫不是确定性的，
/// 而且它会把此后每一次 <c>-Strict</c> 都判红——包括与这段代码无关的那些提交。</b>
/// 判据换成 git 之后：别人丢在工作区里的草稿脚本与这条守卫无关（有用例钉住这一点），
/// 而<b>追踪</b>的脚本少一个 BOM 仍然必红。
/// </para>
/// <para>
/// <b>这条射程的代价（写出来，不藏）</b>：它需要 git 元数据。没有 git（或没有 git 命令）时
/// 这条用例会红，并说清"为什么它答不了"。"这个树里哪些 <c>.ps1</c> 是仓库自己的"这个问题
/// 在没有 git 的环境里没有答案——退化成"扫当前目录"就是上面那个缺陷本身，
/// 所以这里选择<b>响亮地失败</b>，而不是换一条判据悄悄通过。（CI 与开发机都有 git。）
/// </para>
/// </summary>
public static class ToolingHygieneTests
{
    /// <summary>UTF-8 BOM：<c>EF BB BF</c>。</summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// 仓库追踪的每个 <c>.ps1</c> 的前三个字节必须是 <c>EF BB BF</c>。<para>
    /// 失败信息点名到具体文件与它实际的开头字节——"哪一份、坏在哪"是这条守卫唯一有用的输出。
    /// </para>
    /// </summary>
    [Test]
    public static void PowerShellScripts_CarryTheUtf8Bom()
    {
        string? root = RepositoryPaths.Find();
        Check.NotNull(root, "这条用例必须在仓库里跑：它要从仓库根找 .ps1（没找到 NekoClicker.sln）。");

        List<string> scripts = TrackedPowerShellScripts(root!);
        scripts.Sort(StringComparer.Ordinal);

        // 假绿防线：tools/ 今天就有 11 个 .ps1。枚举出 0 个（或明显变少）说明是这条用例自己写错了，
        // 而不是"仓库恰好干净"——这类卫生守卫最坏的失效形态就是它静默地什么都不检查。
        Check.AtLeast(
            scripts.Count,
            10,
            $"从 <{root}> 的 git 索引里只找到 {scripts.Count} 个 .ps1（tools/ 今天就有 11 个）：" +
            "这是枚举写错了，不是仓库干净。");

        List<string> offenders = [];
        foreach (string script in scripts)
        {
            byte[] head = ReadHead(script, Utf8Bom.Length);
            if (head.AsSpan().SequenceEqual(Utf8Bom)) continue;

            offenders.Add($"    {Path.GetRelativePath(root!, script).Replace('\\', '/')}（开头是 {Describe(head)}）");
        }

        Check.Equal(
            0,
            offenders.Count,
            $"仓库追踪的 .ps1 必须带 UTF-8 BOM（开头三个字节 EF BB BF），这几个没有（{offenders.Count}/{scripts.Count}）：" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine +
            "为什么：这些脚本由 Windows PowerShell 5.1 执行（start.cmd 用 -ExecutionPolicy Bypass 调进来），" +
            "5.1 把无 BOM 文件按 GBK 解码 ⇒ 中文注释变乱码 ⇒ 整脚本 parse error" +
            "（TerminatorExpectedAtEndOfString / RedirectionNotSupported）⇒ 一项都不跑；" +
            "而 pwsh 7 默认按 UTF-8 读，所以同一份文件在开发机上看起来是好的。" +
            "2026-10-04 一天咬了三次，最后一次落在 main 上（04c67e0）。" +
            "修法：把这三个字节补回文件最前面——凡是拿 edit / write 一类工具改过 .ps1，都必须重新补一次：" +
            "$p='tools/xxx.ps1'; $b=[System.IO.File]::ReadAllBytes($p); " +
            "[System.IO.File]::WriteAllBytes($p, [byte[]](0xEF,0xBB,0xBF) + $b)");
    }

    /// <summary>
    /// 未追踪的（本地的、被 <c>.gitignore</c> 忽略的）<c>.ps1</c> <b>不在射程内</b>。<para>
    /// 这是上面那个缺陷的回归用例，而且它<b>自己造</b>证据：在仓库根写一个无 BOM 的 <c>.ps1</c>
    /// （仓库根不在任何"跳过目录"里，所以旧的"递归扫工作目录"实现<b>一定</b>会把它算成违规），
    /// 断言枚举结果里没有它，然后删掉。跑完不留东西。
    /// </para>
    /// </summary>
    [Test]
    public static void UntrackedPowerShellScripts_AreNotScanned()
    {
        string? root = RepositoryPaths.Find();
        Check.NotNull(root, "这条用例必须在仓库里跑：它要从仓库根找 .ps1（没找到 NekoClicker.sln）。");

        List<string> scripts = TrackedPowerShellScripts(root!);

        // 名字里带一个随机后缀：并行跑到这里时不会与别的实例（或人）撞名。
        // 刻意用 `<仓库根>/...` 而不是 `.tmp/` 或 `artifacts/`：那两个目录旧实现本来就会跳过，
        // 放在那里就证明不了"是 git 说的，不是目录名说的"。
        string probe = Path.Combine(root!, $".bom-guard-probe-{Guid.NewGuid():N}.ps1");

        try
        {
            File.WriteAllBytes(probe, "# 这条用例的探针：刻意不带 BOM。\n"u8.ToArray());

            // 探针自己得先是"一条会被守卫判红的 .ps1"，否则这条用例没有判别力。
            byte[] head = ReadHead(probe, Utf8Bom.Length);
            Check.False(
                head.AsSpan().SequenceEqual(Utf8Bom),
                $"这条用例自己写错了：探针 <{probe}> 不该带 BOM（开头是 {Describe(head)}）。");

            Check.False(
                scripts.Exists(script => string.Equals(Path.GetFileName(script), Path.GetFileName(probe), StringComparison.OrdinalIgnoreCase)),
                $"未追踪的 .ps1 不该被扫到——它是别人留在工作区里的本地脚本，与「仓库里的脚本有没有 BOM」无关：" +
                $"<{Path.GetFileName(probe)}>。这就是 2026-10-04 那次红的形状（.probe/fixcounts.ps1 等三个未追踪脚本）。");
        }
        finally
        {
            if (File.Exists(probe)) File.Delete(probe);
        }

        Check.False(File.Exists(probe), $"探针 <{probe}> 没被删掉：用例必须自己收拾干净，不许留临时文件。");
    }

    /// <summary>
    /// 仓库追踪的 <c>*.ps1</c> 的绝对路径，来自 <c>git ls-files -z -- '*.ps1'</c>。<para>
    /// 用 <c>-z</c>：路径按 NUL 分隔，git 不做任何引号 / 转义处理，含空格与中文的名字也原样拿到。
    /// </para>
    /// </summary>
    private static List<string> TrackedPowerShellScripts(string root)
    {
        (int exitCode, string stdout, string stderr) = RunGit(root, ["ls-files", "-z", "--", "*.ps1"]);

        Check.Equal(
            0,
            exitCode,
            $"`git ls-files -z -- *.ps1` 在 <{root}> 里退出了 {exitCode}，所以这条守卫答不了" +
            "「哪些 .ps1 是仓库自己的」。" + Environment.NewLine +
            "这条守卫刻意**不**退化成「扫工作目录」：那个判据会把别人留在工作区里的未追踪脚本" +
            "算成违规（2026-10-04 实测 3/14 红），让每一次 -Strict 都取决于本地草稿。" +
            "修法：在带 git 元数据的检出里跑（本仓库的 CI 与开发机都有 git）。" + Environment.NewLine +
            $"stderr：{stderr.Trim()}");

        List<string> scripts = [];
        foreach (string relative in stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // git 一律报正斜杠（即使 Windows）；Path.GetFullPath 把它归一成本平台的写法，
            // 后面的 ReadHead / GetRelativePath 就都只有一种形状要处理。
            scripts.Add(Path.GetFullPath(Path.Combine(root, relative)));
        }

        return scripts;
    }

    /// <summary>
    /// 跑一次 <c>git</c>（工作目录 = 仓库根），返回退出码与两条流。<para>
    /// 用 <see cref="ProcessStartInfo.ArgumentList"/> 而不是拼命令行：路径里的空格 / 中文
    /// 不用经过任何一层 shell 的引号规则。找不到 git 命令时直接判红——原因与处置写在消息里。
    /// </para>
    /// </summary>
    private static (int ExitCode, string Stdout, string Stderr) RunGit(string root, string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(root);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidOperationException("Process.Start 返回了 null。");
        }
        catch (Exception ex)
        {
            Check.Fail(
                $"起不了 `git`（{ex.GetType().Name}：{ex.Message}）。这条守卫要问 git「哪些 .ps1 是仓库自己的」" +
                "——没有 git 元数据时这个问题没有答案，而退化成「扫工作目录」就是它被修掉的那个缺陷本身。");
            return (0, string.Empty, string.Empty); // 上面一定抛了；这行只为让编译器满意。
        }

        using (process)
        {
            // 两条流各读各的：不能让任何一条写满管道把对方堵住。
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            // 有超时：`git ls-files` 正常在几十毫秒内返回，卡住时宁可让这条守卫响，
            // 也不要让整个 -Strict 无声地挂在这里。
            if (!process.WaitForExit(60_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                Check.Fail("`git ls-files` 超过 60 秒没有返回——这条守卫不打算无限等下去。");
            }

            return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
    }

    /// <summary>读文件开头至多 <paramref name="count"/> 个字节（文件更短时返回实际长度）。</summary>
    private static byte[] ReadHead(string path, int count)
    {
        using FileStream stream = File.OpenRead(path);

        byte[] head = new byte[count];
        int read = stream.Read(head, 0, count);
        return read == count ? head : head[..read];
    }

    /// <summary>把开头几个字节写成 <c>EF BB BF</c> / <c>23 20 2E</c> / <c>空文件</c> 这样的可读形式。</summary>
    private static string Describe(byte[] head)
        => head.Length == 0 ? "空文件" : string.Join(" ", head.Select(value => value.ToString("X2")));
}
