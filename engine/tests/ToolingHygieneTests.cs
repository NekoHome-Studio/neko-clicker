using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 工具链卫生：仓库里的 <c>.ps1</c> 必须带 UTF-8 BOM。<para>
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
/// <b>射程</b>：从仓库根往下扫全部 <c>*.ps1</c>，只跳过版本控制内部与构建/暂存目录
/// （<c>.git</c> / <c>.tmp</c> / <c>bin</c> / <c>obj</c> / <c>artifacts</c> / <c>node_modules</c>）——
/// 今天命中的 10 个全在 <c>tools/</c>，但写成"整个仓库"是为了让下一个 <c>.ps1</c>
/// 无论放在哪里都自动被扫到，而不是留下一处"新位置没人守"的盲区。
/// </para>
/// </summary>
public static class ToolingHygieneTests
{
    /// <summary>UTF-8 BOM：<c>EF BB BF</c>。</summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>枚举时跳过的目录名（相对仓库根；绝对路径里的同名目录不受影响）。</summary>
    private static readonly string[] SkippedDirectoryNames =
        [".git", ".tmp", "bin", "obj", "artifacts", "node_modules"];

    /// <summary>
    /// 仓库里每个 <c>.ps1</c> 的前三个字节必须是 <c>EF BB BF</c>。<para>
    /// 失败信息点名到具体文件与它实际的开头字节——"哪一份、坏在哪"是这条守卫唯一有用的输出。
    /// </para>
    /// </summary>
    [Test]
    public static void PowerShellScripts_CarryTheUtf8Bom()
    {
        string? root = RepositoryPaths.Find();
        Check.NotNull(root, "这条用例必须在仓库里跑：它要从仓库根找 .ps1（没找到 NekoClicker.sln）。");

        List<string> scripts = FindPowerShellScripts(root!);
        scripts.Sort(StringComparer.Ordinal);

        // 假绿防线：tools/ 今天就有 10 个 .ps1。枚举出 0 个（或明显变少）说明是这条用例自己写错了，
        // 而不是"仓库恰好干净"——这类卫生守卫最坏的失效形态就是它静默地什么都不检查。
        Check.AtLeast(
            scripts.Count,
            10,
            $"从 <{root}> 往下只找到 {scripts.Count} 个 .ps1（tools/ 今天就有 10 个）：这是枚举写错了，不是仓库干净。");

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
            $"仓库里的 .ps1 必须带 UTF-8 BOM（开头三个字节 EF BB BF），这几个没有（{offenders.Count}/{scripts.Count}）：" +
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

    /// <summary>递归收集 <c>*.ps1</c>，跳过 <see cref="SkippedDirectoryNames"/> 里的目录名。</summary>
    private static List<string> FindPowerShellScripts(string directory)
    {
        List<string> found = [.. Directory.EnumerateFiles(directory, "*.ps1")];

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectoryNames.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)) continue;
            found.AddRange(FindPowerShellScripts(child));
        }

        return found;
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
