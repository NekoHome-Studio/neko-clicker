using System.Text;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 崩溃日志：交互模式里任何未处理异常都写一份带环境信息的日志。<para>
/// 终端渲染这条路最容易出"环境相关、本地复现不了"的崩溃（窗口缩放、字体回退、宿主差异），
/// 只看到一句 "Unhandled exception" 是没法定位的。把异常、窗口尺寸、宿主信息落到一个
/// 固定路径的文件里，用户可以直接把它发回来。
/// </para>
/// </summary>
internal static class CrashLog
{
    /// <summary>日志文件名（写在当前工作目录，与 <c>saves/</c> 同级）。</summary>
    public const string FileName = "neko-clicker-crash.log";

    /// <summary>写一份崩溃日志，返回文件的绝对路径；连日志都写不进去时返回 <c>null</c>。</summary>
    /// <param name="exception">未处理的异常。</param>
    public static string? Write(Exception exception)
    {
        try
        {
            string path = Path.GetFullPath(FileName);
            var text = new StringBuilder()
                .AppendLine($"时间   ：{DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"程序集 ：{typeof(CrashLog).Assembly.GetName().Name} " +
                            $"{typeof(CrashLog).Assembly.GetName().Version}")
                .AppendLine($"系统   ：{Environment.OSVersion}｜64 位进程：{Environment.Is64BitProcess}")
                .AppendLine($"命令行 ：{Environment.CommandLine}")
                .AppendLine($"窗口   ：{DescribeViewport()}")
                .AppendLine($"VT 转义：{Ansi.ColorEnabled}")
                .AppendLine($"宿主   ：{TerminalHost.Describe()}")
                .AppendLine()
                .AppendLine(exception.ToString());

            File.AppendAllText(path, text.ToString());
            return path;
        }
        catch
        {
            return null; // 记日志本身失败就算了，不能在崩溃处理里再抛一次。
        }
    }

    private static string DescribeViewport()
    {
        try
        {
            return $"{Console.WindowWidth}×{Console.WindowHeight}";
        }
        catch (Exception)
        {
            return "读不到（宿主不支持）";
        }
    }
}
