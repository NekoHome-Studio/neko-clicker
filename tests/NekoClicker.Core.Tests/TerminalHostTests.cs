using NekoClicker.Demo.Cli;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 宿主兼容性判定（<see cref="TerminalHost"/>）。<para>
/// conhost（PowerShell 5.1 / cmd 直接打开的窗口）在"备用屏 + 缩放"组合下会自己崩
/// （宿主进程崩溃，.NET 抓不到异常），所以自动策略必须保守：
/// Windows 上认不出"现代宿主"就不用备用屏。
/// </para>
/// </summary>
public static class TerminalHostTests
{
    [Test]
    public static void Auto_AvoidsAlternateScreenOnLegacyWindowsConsole()
    {
        // 传统 conhost：Windows + 没有任何现代宿主标记 → 不用备用屏（避开崩溃）。
        Check.False(
            TerminalHost.ShouldUseAlternateScreen(AltScreenMode.Auto, isWindows: true, hasModernHostMarker: false),
            "认不出宿主时应当按 conhost 处理，退回主缓冲区渲染。");

        // Windows Terminal / VS Code 终端等：有标记 → 照常用备用屏（它们没有这个缺陷）。
        Check.True(
            TerminalHost.ShouldUseAlternateScreen(AltScreenMode.Auto, isWindows: true, hasModernHostMarker: true),
            "认得出的现代宿主应当使用备用屏（退出后能恢复原屏幕）。");

        // 非 Windows 的终端没有这个缺陷。
        Check.True(
            TerminalHost.ShouldUseAlternateScreen(AltScreenMode.Auto, isWindows: false, hasModernHostMarker: false));
    }

    [Test]
    public static void ExplicitModes_OverrideAuto()
    {
        Check.True(
            TerminalHost.ShouldUseAlternateScreen(AltScreenMode.On, isWindows: true, hasModernHostMarker: false),
            "--altscreen 应当能强制打开。");
        Check.False(
            TerminalHost.ShouldUseAlternateScreen(AltScreenMode.Off, isWindows: false, hasModernHostMarker: true),
            "--no-altscreen 应当能强制关闭。");
    }

    [Test]
    public static void CliFlags_ParseBothAltScreenModes()
    {
        Check.Equal(AltScreenMode.Auto, CliOptions.Parse([]).AltScreen, "默认应当是自动。");
        Check.Equal(AltScreenMode.On, CliOptions.Parse(["--altscreen"]).AltScreen);
        Check.Equal(AltScreenMode.Off, CliOptions.Parse(["--no-altscreen"]).AltScreen);
    }

    [Test]
    public static void Describe_MentionsHostMarkers()
    {
        string text = TerminalHost.Describe();

        Check.Contains(text, "Windows=", "崩溃日志里要有宿主描述。");
        Check.Contains(text, "WT_SESSION", "崩溃日志里要有现代宿主标记。");
    }
}
