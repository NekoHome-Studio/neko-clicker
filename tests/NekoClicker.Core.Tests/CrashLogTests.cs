using NekoClicker.Demo.Cli;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 崩溃日志的验收。<para>
/// 交互层最容易出"环境相关、本地复现不了"的崩溃（窗口缩放、字体回退、宿主差异）。
/// 这份日志是唯一能把现场带回来的东西——它自己必须先靠谱：异常类型、消息、窗口尺寸
/// 都要落盘，且写日志失败时不能反过来再抛。
/// </para>
/// </summary>
public static class CrashLogTests
{
    [Test]
    public static void Write_PersistsExceptionAndEnvironment()
    {
        string path = Path.GetFullPath(CrashLog.FileName);
        string? backup = File.Exists(path) ? File.ReadAllText(path) : null;

        try
        {
            string? written = CrashLog.Write(new InvalidOperationException("测试用的崩溃：窗口在缩放"));

            Check.Equal(path, written, "崩溃日志应当写到固定路径，并把路径回报给调用方。");
            Check.True(File.Exists(path), "崩溃日志文件没写出来。");

            string text = File.ReadAllText(path);
            Check.Contains(text, "InvalidOperationException", "日志里要有异常类型。");
            Check.Contains(text, "测试用的崩溃：窗口在缩放", "日志里要有异常消息。");
            Check.Contains(text, "窗口", "日志里要有现场信息（窗口尺寸 / 宿主）。");
        }
        finally
        {
            // 测试不许在仓库里留下崩溃日志：写完就恢复/删除。
            if (backup is null) File.Delete(path);
            else File.WriteAllText(path, backup);
        }
    }
}
