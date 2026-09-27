namespace NekoClicker.Demo.Cli;

/// <summary>
/// 宿主终端兼容性判定。<para>
/// <b>为什么需要它</b>：conhost（传统 Windows 控制台宿主——PowerShell 5.1 / cmd 直接打开的那个窗口）
/// 有一个已知缺陷：<b>备用屏幕缓冲区处于活动状态时调整窗口大小，会让 conhost 自己崩溃</b>
/// （microsoft/terminal#13037）。那是宿主进程崩，.NET 里抓不到异常、也不会有崩溃日志——
/// 用户看到的是"缩窗口就崩"。所以默认策略是：只在我们认得出的"现代宿主"里使用备用屏，
/// 其余情况退回主缓冲区渲染（画面依旧，退出时清屏）。
/// </para>
/// </summary>
internal static class TerminalHost
{
    /// <summary>按当前环境与用户策略，决定是否使用备用屏幕缓冲区。</summary>
    /// <param name="mode">自动 / 强制开 / 强制关。</param>
    public static bool UseAlternateScreen(AltScreenMode mode)
        => ShouldUseAlternateScreen(mode, OperatingSystem.IsWindows(), HasModernHostMarker());

    /// <summary>
    /// 判定逻辑的纯函数形式（方便测试）。<para>
    /// 非 Windows 的终端没有这个缺陷；Windows 上认不出宿主就按 conhost 处理——最安全。
    /// </para>
    /// </summary>
    /// <param name="mode">自动 / 强制开 / 强制关。</param>
    /// <param name="isWindows">是否运行在 Windows。</param>
    /// <param name="hasModernHostMarker">是否带有"现代终端"的宿主标记。</param>
    public static bool ShouldUseAlternateScreen(AltScreenMode mode, bool isWindows, bool hasModernHostMarker) => mode switch
    {
        AltScreenMode.On => true,
        AltScreenMode.Off => false,
        _ => !isWindows || hasModernHostMarker,
    };

    /// <summary>当前进程是否带有"现代终端"的宿主标记（Windows Terminal / VS Code / ConEmu / mintty 等）。</summary>
    public static bool HasModernHostMarker()
    {
        string[] markers = ["WT_SESSION", "TERM_PROGRAM", "TERM", "ConEmuANSI"];
        foreach (string marker in markers)
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(marker))) return true;

        return false;
    }

    /// <summary>给崩溃日志用的宿主描述。</summary>
    public static string Describe()
    {
        string wt = Env("WT_SESSION");
        string termProgram = Env("TERM_PROGRAM");
        string term = Env("TERM");
        string conEmu = Env("ConEmuANSI");

        return $"Windows={OperatingSystem.IsWindows()}｜WT_SESSION={wt}｜TERM_PROGRAM={termProgram}｜" +
               $"TERM={term}｜ConEmuANSI={conEmu}｜现代宿主={HasModernHostMarker()}";

        static string Env(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
