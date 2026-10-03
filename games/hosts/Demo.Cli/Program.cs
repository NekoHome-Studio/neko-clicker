using System.Text;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 示例程序入口。<para>
/// 默认进入交互式全屏界面；<c>--simulate</c> / <c>--frame</c> 则走完全无头的路径，
/// 用于验收数值曲线、验证布局，以及在 CI 里当压力测试跑。
/// </para>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        CliOptions options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            Console.Error.WriteLine($"参数错误：{options.Error}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 2;
        }

        if (options.Mode == RunMode.Help)
        {
            Console.WriteLine(CliOptions.HelpText);
            return 0;
        }

        if (options.NoColor) Ansi.ColorEnabled = false;

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // 输出被重定向时无法改编码；中文仍会以 UTF-8 字节写出。
        }

        Console.CancelKeyPress += (_, e) =>
        {
            // Ctrl+C 也应走正常退出流程，避免把终端留在备用屏幕里。
            e.Cancel = false;
        };

        return options.Mode switch
        {
            RunMode.Simulate => HeadlessRunner.RunSimulation(options),
            RunMode.Frame => HeadlessRunner.RunFrame(options),
            _ => RunInteractive(options),
        };
    }

    private static int RunInteractive(CliOptions options)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("检测到标准输入被重定向，无法进入交互模式。");
            Console.Error.WriteLine("请改用：--simulate <秒> --auto   或   --frame 100x30");
            return 2;
        }

        // humanPlay: true —— 这是**真人**坐在终端前玩，也就是 start.cmd 默认走的那条路。
        // 埋点必须在这条路上落盘：它此前不落盘，于是"照默认方式玩一局"一个样本都不会留下
        // （artifacts/latency.txt：21 条 session start、0 条 S 行）。
        using var session = GameSession.FromOptions(options, humanPlay: true);
        return InteractiveLoop.Run(session, options.AltScreen);
    }
}
