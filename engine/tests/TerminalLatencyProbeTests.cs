using NekoClicker.Demo.Cli;
using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 默认启动器的作答延迟埋点<b>必须真的落盘</b>。<para>
/// 这份用例存在的理由是一条量过的缺陷（<c>engine/docs/STRUCTURE_OPTIMIZATION.md</c> §S13）：
/// 埋点此前有两份实现——终端宿主那份只把样本攒在内存里，Web 宿主那份才写文件，
/// 而 <c>tools/start.ps1</c> 的<b>默认模式</b>跑的正是终端宿主。后果是可核对的：
/// <c>artifacts/latency.txt</c> 里 21 条 <c>session start</c>、<b>0 条 <c>S</c> 行</b>，
/// 也就是说"照默认方式玩一局"从来不会留下任何人类数据。
/// </para>
/// <para>
/// 这个缺陷<b>没有任何东西会去发现</b>：埋点的失效方式全是沉默的——文件里少一行，
/// 在现象上与"还没人答过"完全一样。所以这里钉三件事：
/// ① 默认模式的配置指向**两个宿主共用的那一个文件**，而且会把它透给埋点；
/// ② 机器人跑一局<b>不许</b>碰那份真人数据文件（它默认不落盘，要写得显式给路径）；
/// ③ 显式给出路径时，终端宿主确实会落下 <c>S</c> 行、并自报是哪个宿主写的。
/// </para>
/// <para>
/// <b>用例自己绝不往那份真人数据文件里写一个字</b>：凡是需要真的开会话的地方，
/// 都把埋点路径显式指到临时目录（第 ② 条连这条规矩本身也钉着）。
/// </para>
/// </summary>
internal sealed class TerminalLatencyProbeTests
{
    // ---------------------------------------------------------------- ① 默认配置

    /// <summary>
    /// 默认启动器（<c>start.cmd</c>，即交互模式）默认就带着一个真实的埋点文件路径，
    /// 而且那个路径与 Web 宿主的默认路径是<b>同一个文件</b>。<para>
    /// <b>这一条就是那个缺陷的守卫</b>：修复前终端宿主根本没有文件路径这个概念
    /// （<c>ChoiceLatencyLog</c> 只有 90 行、一个字节都不写），这里会是 <c>null</c>。
    /// </para>
    /// </summary>
    [Test]
    public static void DefaultLauncher_HandsTheHumanProbeARealFile()
    {
        // --no-save：这条用例不该碰存档（默认存档路径是相对当前目录的 saves/neko.json）。
        CliOptions options = CliOptions.Parse(["--no-save"]);

        Check.Equal(RunMode.Interactive, options.Mode, "默认启动器跑的是交互模式——整条用例的前提就是它。");
        Check.NotNull(options.LatencyLogPath, "默认就必须落盘：不落盘时，照默认方式玩一局不会留下任何样本。");
        Check.True(
            options.LatencyLogPath!.EndsWith(Path.Combine("artifacts", "latency.txt"), StringComparison.OrdinalIgnoreCase),
            $"默认路径应当是仓库的 artifacts/latency.txt，实际 <{options.LatencyLogPath}>。");

        // 两个宿主的默认路径必须是**同一个文件**：否则"全项目唯一的人类数据来源"会变成两份，
        // 而"终端那半 0 条样本"这种事就会以另一种形式再发生一次。
        string? root = RepositoryPaths.Find();
        Check.NotNull(root, "用例跑在仓库里，应当能定位到仓库根。");
        Check.Equal(
            Path.Combine(root!, "artifacts", "latency.txt"),
            options.LatencyLogPath,
            "终端宿主与 Web 宿主的默认埋点路径必须是同一个文件。");

        // 会话级的那一半：真人模式开会话时，这个路径必须真的被透给埋点。
        // **用临时路径做这件事**——拿默认路径开会话会往那份真人数据文件里追加 session 行，
        // 用例不允许那么做（这正是下面第 ② 条盯着的同一件事）。
        string directory = NewTempDirectory();
        try
        {
            string path = Path.Combine(directory, "latency.txt");
            CliOptions redirected = CliOptions.Parse(["--no-save", "--latency-log", path]);

            using var human = GameSession.FromOptions(redirected, humanPlay: true);
            Check.Equal(
                Path.GetFullPath(path),
                human.LatencyLogPath,
                "真人模式开会话时必须把埋点路径透下去——这条断言就是 §S13 的守卫。");
            Check.True(File.Exists(path), "真人模式下埋点必须当场把文件与表头建出来（否则'线接没接上'看不出来）。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 机器人（无头模拟 / 单帧渲染）默认<b>不落盘</b>：它的样本不是人类数据，
    /// 混进那份文件等于把"这份文件里只有真人的数据行"这句承诺作废。
    /// </summary>
    [Test]
    public static void RobotModes_DoNotClaimToBeHumanData()
    {
        CliOptions options = CliOptions.Parse(["--no-save"]);

        using var robot = GameSession.FromOptions(options, humanPlay: false);
        Check.Null(robot.LatencyLogPath, "机器人作答默认不许写真人数据文件（要写必须显式 --latency-log）。");
    }

    /// <summary>埋点路径可以显式重定向，也可以显式关掉。</summary>
    [Test]
    public static void LatencyLog_CanBeRedirectedAndSwitchedOff()
    {
        string custom = Path.Combine(Path.GetTempPath(), "neko-latency-probe.txt");

        CliOptions redirected = CliOptions.Parse(["--latency-log", custom]);
        Check.Null(redirected.Error, $"参数应当能解析：<{redirected.Error}>");
        Check.Equal(custom, redirected.LatencyLogPath, "显式给路径时必须用它。");
        Check.True(redirected.LatencyLogExplicit, "显式给了路径就要记下来（机器人模式据此决定写不写）。");

        CliOptions off = CliOptions.Parse(["--no-latency-log"]);
        Check.Null(off.LatencyLogPath, "--no-latency-log 应当关掉落盘。");
        Check.True(off.LatencyLogExplicit, "显式关掉也算显式。");

        CliOptions missingValue = CliOptions.Parse(["--latency-log"]);
        Check.NotNull(missingValue.Error, "--latency-log 缺参数时必须报错，而不是悄悄用默认值。");
    }

    // ---------------------------------------------------------------- ② 真人数据文件不许被碰

    /// <summary>
    /// 机器人跑一局、而且没有显式给埋点路径时，仓库里那份真人数据文件必须
    /// <b>字节与 mtime 都不变</b>（文件不存在时也不许凭空造出来）。<para>
    /// 这条守的是埋点最容易被悄悄作废的一侧：测试、压测、单帧渲染都在同一个工作区里跑，
    /// 只要默认路径被顺手接上，它们就会往那份只有真人数据才有意义的文件里塞行。
    /// </para>
    /// </summary>
    [Test]
    public static void SimulatedRun_WithoutAnExplicitPath_LeavesTheHumanFileAlone()
    {
        string realPath = CliOptions.Parse(["--no-save"]).LatencyLogPath!;
        bool existed = File.Exists(realPath);
        byte[]? before = existed ? File.ReadAllBytes(realPath) : null;
        DateTime? beforeWrite = existed ? File.GetLastWriteTimeUtc(realPath) : null;

        CliOptions options = CliOptions.Parse(["--simulate", "60", "--auto", "--no-save"]);
        Check.Null(options.Error, $"参数应当能解析：<{options.Error}>");
        Check.Equal(0, HeadlessRunner.RunSimulation(options), "无头模拟应当正常结束。");

        if (!existed)
        {
            Check.False(File.Exists(realPath), $"机器人跑一局不该凭空造出 <{realPath}>。");
            return;
        }

        Check.True(File.Exists(realPath), $"<{realPath}> 不该被删掉。");
        Check.True(
            File.ReadAllBytes(realPath).SequenceEqual(before!),
            $"机器人跑一局绝不许改写真人的埋点文件：<{realPath}>。");
        Check.Equal(
            beforeWrite,
            (DateTime?)File.GetLastWriteTimeUtc(realPath),
            "内容没变就不该被重写（mtime 是同一件事的另一半证据）。");
    }

    // ---------------------------------------------------------------- ③ 显式路径下真的落 S 行

    /// <summary>
    /// 无头模拟（<c>--simulate --auto</c>）里机器人会答掉待答表态；只要显式给了埋点路径，
    /// 终端宿主的这条路径就必须**落下 <c>S</c> 行**，并且自报 <c>host=cli</c>。<para>
    /// 这一条与 ① 是同一个机制的两端：① 保证"真人玩的那条路接上了文件"，
    /// 这一条保证"接上之后真的写得出来"（列序、表头、追加行为都在
    /// <see cref="WebChoiceLatencyTests"/> 里钉着，这里是端到端的那一下）。
    /// </para>
    /// </summary>
    [Test]
    public static void SimulatedRun_WithAnExplicitPath_WritesAnSLine()
    {
        string directory = NewTempDirectory();
        try
        {
            string path = Path.Combine(directory, "latency.txt");

            // 种子**必须固定**（12345，与仓库里那条测量机器人同一个种子），窗口也要足够长：
            // 默认种子是按时间随机的，而"第一次表态在 900 模拟秒里出不出得来"是**边缘**的——
            // 实测同一份代码在 900 秒 / 随机种子下时有时无（随机事件把早期收入抬高或压低了，
            // 而公司包第一次表态的门槛是"本轮累计 2e4"）。3600 秒 + 这个种子实测答掉 6 次表态，
            // 所以这条断言不是在跟运气赛跑——一条会偶发变红的守卫比没有守卫更糟。
            CliOptions options = CliOptions.Parse(
                ["--simulate", "3600", "--auto", "--package", "company", "--seed", "12345", "--no-save", "--latency-log", path]);
            Check.Null(options.Error, $"参数应当能解析：<{options.Error}>");
            Check.Equal(RunMode.Simulate, options.Mode);

            Check.Equal(0, HeadlessRunner.RunSimulation(options), "无头模拟应当正常结束。");
            Check.True(File.Exists(path), $"显式给了路径就必须建出文件：<{path}>。");

            string[] lines = File.ReadAllLines(path);
            Check.True(
                lines.Any(line => line.StartsWith($"S{LatencyLogFormat.Separator}", StringComparison.Ordinal)),
                $"机器人答掉表态时必须落下 S 行：<{Tail(lines)}>");
            Check.True(
                lines.Any(line => line.Contains("host=" + LatencyLogFormat.CliHost, StringComparison.Ordinal)),
                $"会话行必须写明这一段的样本来自哪个宿主：<{Tail(lines)}>");
            Check.True(
                lines.Any(line => line.StartsWith("# session end", StringComparison.Ordinal)
                                  && !line.Contains("samples=0", StringComparison.Ordinal)),
                $"会话结束时必须报出非零样本数：<{Tail(lines)}>");
            Check.True(
                lines[0].StartsWith(LatencyLogFormat.HeaderSignature, StringComparison.Ordinal),
                $"新文件的第一行必须是自述表头：<{lines[0]}>");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>把最后三行拼成一句话，断言失败时它就是现场。</summary>
    private static string Tail(string[] lines)
        => string.Join(" | ", lines.TakeLast(3));

    /// <summary>仓库内的临时目录（<c>artifacts/</c> 已被 .gitignore 忽略），跑完由用例自己删掉。</summary>
    private static string NewTempDirectory()
    {
        string root = RepositoryPaths.Find() ?? Path.GetTempPath();
        string directory = Path.Combine(root, "artifacts", "latency-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
