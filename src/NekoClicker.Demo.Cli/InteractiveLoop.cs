using System.Text;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 交互式主循环：读键盘 → 交给会话 → 重绘。<para>
/// 采用"进入备用屏幕 + 光标归位后整屏重写"的方式，而不是逐处增量刷新：
/// 增量刷新在终端尺寸变化、中文宽度判断出错时极易残留脏字符，整屏重写在 20fps 下
/// 也完全看不出闪烁（这正是 Cookie Clicker 那类全屏 TUI 的常见做法）。
/// </para>
/// </summary>
internal static class InteractiveLoop
{
    private const double TargetFps = 20;
    private const double MaxFrameDelta = 0.25;

    /// <summary>运行交互循环。</summary>
    /// <param name="session">会话。</param>
    /// <returns>进程退出码。</returns>
    public static int Run(GameSession session)
    {
        bool colorSupported = Ansi.TryEnableVirtualTerminal();
        Ansi.ColorEnabled = colorSupported;

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // 重定向输出时无法改编码，忽略即可。
        }

        Console.Write(Ansi.EnterAlternateScreen() + Ansi.Clear() + Ansi.HideCursor());

        try
        {
            Pump(session);
        }
        finally
        {
            // 无论怎么退出都要还原终端，否则会把用户的 shell 留在备用屏幕里。
            Console.Write(Ansi.ShowCursor() + Ansi.ExitAlternateScreen());
            Console.Out.Flush();
        }

        return 0;
    }

    private static void Pump(GameSession session)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double lastFrame = 0;
        double lastRender = double.NegativeInfinity;
        bool needsRender = true;

        while (!session.QuitRequested)
        {
            double now = stopwatch.Elapsed.TotalSeconds;
            double delta = Math.Min(MaxFrameDelta, Math.Max(0, now - lastFrame));
            lastFrame = now;

            session.Update(delta);

            while (Console.KeyAvailable)
            {
                HandleKey(session, Console.ReadKey(intercept: true));
                needsRender = true;
            }

            if (needsRender || now - lastRender >= 1.0 / TargetFps)
            {
                Render(session);
                lastRender = now;
                needsRender = false;
            }

            // 纯轮询键盘，10ms 的休眠足以让界面保持跟手，又不会把 CPU 跑满。
            Thread.Sleep(10);
        }
    }

    private static void Render(GameSession session)
    {
        (int width, int height) = MeasureViewport();
        List<string> lines = TerminalUi.Render(session, width, height);

        var builder = new StringBuilder();
        builder.Append(Ansi.Home());
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) builder.Append('\n');
            builder.Append(lines[i]);
        }
        builder.Append(Ansi.ClearToEnd());

        Console.Write(builder.ToString());
    }

    private static (int Width, int Height) MeasureViewport()
    {
        try
        {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            if (width > 20 && height > 8) return (width, height);
        }
        catch (IOException)
        {
            // 输出被重定向时取不到窗口尺寸。
        }

        return (100, 30);
    }

    private static void HandleKey(GameSession session, ConsoleKeyInfo key)
    {
        // 转生确认态优先：除了 Y 之外任何键都是取消。
        if (session.AwaitingAscendConfirm)
        {
            if (key.Key == ConsoleKey.Y) session.Ascend();
            else session.CancelPending();
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
            case ConsoleKey.C:
                session.Click();
                return;

            case ConsoleKey.Enter:
                session.Activate();
                return;

            case ConsoleKey.Tab:
                session.CycleFocus();
                return;

            case ConsoleKey.UpArrow:
                session.MoveSelection(-1);
                return;

            case ConsoleKey.DownArrow:
                session.MoveSelection(1);
                return;

            case ConsoleKey.X:
                session.CycleMode();
                return;

            case ConsoleKey.V:
                session.ToggleSell();
                return;

            case ConsoleKey.G:
                session.GrabGoldenCookie();
                return;

            case ConsoleKey.A:
                session.RequestAscend();
                return;

            case ConsoleKey.F5:
                session.Save();
                return;

            case ConsoleKey.H:
                session.ToggleHelp();
                return;

            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                session.Quit();
                return;
        }

        // 数字键：选中并立即执行（快速购买），未聚焦的面板不响应。
        if (key.KeyChar is >= '0' and <= '9')
        {
            session.SelectByKey(key.KeyChar);
            if (session.Focus != PanelFocus.Achievements) session.Activate();
        }
    }
}
