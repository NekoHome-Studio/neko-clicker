using System.Diagnostics;
using System.Text;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 交互式主循环：读键盘 → 交给会话 → 重绘。<para>
/// 采用"进入备用屏幕 + 光标归位后逐行覆盖"的方式，<b>不做整屏重写</b>：
/// 整屏重写在下述意义上是有害的——它让终端每帧都要重画整个窗口，
/// 内容没变时纯属浪费，观感就是"画面在闪"。
/// </para>
/// </summary>
internal static class InteractiveLoop
{
    private const double TargetFps = 20;
    private const double MaxFrameDelta = 0.25;

    /// <summary>运行交互循环。</summary>
    /// <param name="session">会话。</param>
    /// <param name="altScreenMode">备用屏策略；默认自动（conhost 上自动不用）。</param>
    /// <returns>进程退出码。</returns>
    public static int Run(GameSession session, AltScreenMode altScreenMode = AltScreenMode.Auto)
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

        // conhost 在"备用屏 + 缩放"组合下会自己崩（宿主进程崩，抓不到异常）：
        // 认不出现代宿主就别用备用屏。
        bool altScreen = TerminalHost.UseAlternateScreen(altScreenMode);

        TryWrite((altScreen ? Ansi.EnterAlternateScreen() : string.Empty)
                 + Ansi.Clear() + Ansi.Home() + Ansi.HideCursor());

        Exception? crash = null;
        try
        {
            Pump(session);
        }
        catch (Exception ex)
        {
            // 交互层是"环境相关崩溃"的重灾区（窗口缩放、字体、宿主差异），
            // 不该把栈直接糊在玩家脸上：先记一份带环境信息的日志，最后给一句人话。
            crash = ex;
        }
        finally
        {
            // 用备用屏时退出即可恢复原屏幕；不用备用屏时（conhost），退出前把画面清掉，
            // 别把一屏游戏界面留在 shell 的滚动缓冲里。
            // 先补一发 SyncEnd：万一是在同步输出窗口内异常退出，终端会一直攒着不上屏。
            // 还原本身也可能失败（窗口正在关闭）——用 TryWrite，不能让收尾再抛一次。
            TryWrite(Ansi.SyncEnd() + Ansi.ShowCursor()
                     + (altScreen ? Ansi.ExitAlternateScreen() : Ansi.Home() + Ansi.Clear()));
        }

        if (crash is null) return 0;

        string? logPath = CrashLog.Write(crash);

        try
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"界面出错了：{crash.GetType().Name}：{crash.Message}");
            Console.Error.WriteLine(logPath is null
                ? "（崩溃日志写入失败）"
                : $"崩溃日志已写入：{logPath}——把它发给开发者即可定位。");
        }
        catch (IOException)
        {
            // 连 stderr 都写不进去时，只剩退出码可以表达"出错了"。
        }
        catch (ObjectDisposedException)
        {
        }

        return 1;
    }

    /// <summary>写终端；缩放 / 关闭窗口时写入可能失败，忽略即可——不能因为写不进去再崩一次。</summary>
    private static void TryWrite(string text)
    {
        try
        {
            Console.Write(text);
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Pump(GameSession session)
    {
        var stopwatch = Stopwatch.StartNew();
        var painter = new FramePainter();
        double lastFrame = 0;
        double lastRender = double.NegativeInfinity;
        bool needsRender = true;

        while (!session.QuitRequested)
        {
            double now = stopwatch.Elapsed.TotalSeconds;
            double delta = Math.Min(MaxFrameDelta, Math.Max(0, now - lastFrame));
            lastFrame = now;

            session.Update(delta);

            try
            {
                while (Console.KeyAvailable)
                {
                    HandleKey(session, Console.ReadKey(intercept: true));
                    needsRender = true;
                }
            }
            catch (InvalidOperationException)
            {
                // 输入句柄在缩放 / 切换焦点时可能短暂不可用：跳过这一轮轮询，下一轮再来。
            }
            catch (IOException)
            {
            }

            if (needsRender || now - lastRender >= 1.0 / TargetFps)
            {
                painter.Draw(session, now);
                lastRender = now;
                needsRender = false;
            }

            // 纯轮询键盘，10ms 的休眠足以让界面保持跟手，又不会把 CPU 跑满。
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// 帧绘制器：只重画内容真正变化的行。<para>
    /// 改成增量之前是每帧整屏重写。实测（118×30、60 秒 @20fps）：
    /// 整屏重写 <b>36,000 行 / 4,170 KB</b>（约 70KB/s），而真正变化的只有
    /// <b>1,229 行 / 137 KB</b>（约 2.3KB/s）——<b>行数 3.4%、流量 3.3%</b>，
    /// 也就是说过去 97% 的终端写入是在重画没变的东西，观感就是"没动却在闪"。
    /// </para>
    /// <para>
    /// 变化的行几乎全是时间驱动的（顶部每秒产量、金猫倒计时），所以"纯挂机"与
    /// "持续操作"的差异很小（1229 vs 1239 行）——静止时不写才是关键。
    /// </para>
    /// <para>
    /// 行级增量之所以安全（原本选整屏重写就是为了躲开"中文宽度算错留下脏字"）：
    /// 每一行都是<b>从第 1 列整行覆盖</b>，再补 <c>EL</c> 擦到行尾。
    /// 要么整行被正确替换，要么这行压根没动过——不存在"只改了几个格子、宽度判断出错就留残字"的窗口。
    /// </para>
    /// </summary>
    private sealed class FramePainter
    {
        /// <summary>每隔这么久整屏重画一次，兜底外部程序往终端里写过东西的情况。</summary>
        private const double FullRepaintSeconds = 5.0;

        private readonly List<string> _previous = [];
        private int _width;
        private int _height = -1;
        private double _lastFullRepaint = double.NegativeInfinity;

        /// <summary>按需绘制一帧；内容完全没变时一个字节都不写。</summary>
        public void Draw(GameSession session, double now)
        {
            (int Width, int Height)? viewport = MeasureViewport();
            if (viewport is not { } size)
            {
                // 缩放 / 最小化的瞬间读不到窗口尺寸：这一帧不画。
                // 千万不要拿一个假尺寸（比如 100×30）去写一个真实的小窗口——那正是滚屏的来源。
                return;
            }

            (int width, int height) = size;
            List<string> lines = TerminalUi.Render(session, width, height);

            // 终端不支持 VT 转义：没有定位 / 清屏 / 备用屏幕能力，全屏界面无从谈起。
            // 退化成"每次追加一整帧"的普通输出——绝不能走下面的增量路径：
            // 定位序列此时全是空串，所有行会被粘成一行。
            if (!Ansi.ColorEnabled)
            {
                TryWrite(string.Join('\n', lines) + "\n");
                return;
            }

            // 尺寸变了 / 到了兜底间隔 → 整屏重画。
            bool full = width != _width
                        || height != _height
                        || now - _lastFullRepaint >= FullRepaintSeconds;

            var builder = new StringBuilder();
            int written = 0;

            // 帧变矮时先把下边多出来的行擦掉（要在写新内容之前定位）。
            if (_previous.Count > lines.Count)
                builder.Append(Ansi.MoveTo(lines.Count + 1)).Append(Ansi.ClearToEnd());

            for (int i = 0; i < lines.Count; i++)
            {
                if (!full
                    && i < _previous.Count
                    && string.Equals(_previous[i], lines[i], StringComparison.Ordinal))
                {
                    continue;
                }

                // 定位一律用绝对坐标（CUP），不用 \n 推进行：
                // LF 在宿主终端里可能被解释成"光标已在最后一行 → 滚屏"，
                // conhost（PowerShell 的宿主）尤其明显——那正是随机的整屏跳动。
                // 绝对定位没有这个歧义，代价是每行多几个字节。
                builder.Append(Ansi.MoveTo(i + 1));
                builder.Append(lines[i]).Append(Ansi.ClearLine());
                written++;
            }

            _previous.Clear();
            _previous.AddRange(lines);
            _width = width;
            _height = height;

            if (written == 0) return; // 画面没变：不写，屏幕上就不该有任何动静

            if (full) _lastFullRepaint = now;

            // 整帧一次 Write（而不是逐行 Write-Host 那种 N 次刷新），
            // 并用同步输出把它作为一帧原子呈现。写失败（窗口正在关闭）就丢掉这一帧。
            TryWrite(Ansi.SyncStart() + builder.ToString() + Ansi.SyncEnd());
        }
    }

    /// <summary>
    /// 读取当前窗口尺寸；读不到时返回 <c>null</c>。<para>
    /// <b>拖拽缩放窗口的瞬间，Windows 控制台会让 GetConsoleScreenBufferInfo 失败</b>
    /// （<c>IOException</c> / <c>ArgumentOutOfRangeException</c> / 窗口最小化时读到 0 都见过）。
    /// 这里把"读尺寸"当成可能失败的操作：失败就这一帧不画，绝不让它把游戏带崩，
    /// 也不用假尺寸去写真实窗口（那会造成滚屏）。
    /// </para>
    /// </summary>
    private static (int Width, int Height)? MeasureViewport()
    {
        try
        {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            if (width > 0 && height > 0) return (width, height);
        }
        catch (Exception)
        {
            // 见方法注释：缩放竞态下什么都可能抛，一律按"读不到"处理。
        }

        return null;
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
