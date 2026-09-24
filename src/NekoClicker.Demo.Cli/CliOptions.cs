namespace NekoClicker.Demo.Cli;

/// <summary>运行模式。</summary>
internal enum RunMode
{
    /// <summary>交互式全屏界面。</summary>
    Interactive,

    /// <summary>无头模拟：按真实内容曲线跑指定时长，然后打印报告。</summary>
    Simulate,

    /// <summary>只渲染一帧界面到标准输出后退出（用于验证布局 / 截图 / CI）。</summary>
    Frame,

    /// <summary>打印帮助。</summary>
    Help,
}

/// <summary>命令行参数。</summary>
internal sealed class CliOptions
{
    /// <summary>运行模式。</summary>
    public RunMode Mode { get; private set; } = RunMode.Interactive;

    /// <summary>模拟时长（秒）。</summary>
    public double SimulateSeconds { get; private set; } = 3600;

    /// <summary>模拟时是否启用自动购买策略。</summary>
    public bool AutoPlay { get; private set; }

    /// <summary>随机种子；0 表示按时间随机。</summary>
    public ulong Seed { get; private set; }

    /// <summary>存档文件路径；<c>null</c> 表示不落盘。</summary>
    public string? SavePath { get; private set; } = Path.Combine("saves", "neko.json");

    /// <summary>渲染宽度。</summary>
    public int Width { get; private set; } = 100;

    /// <summary>渲染高度。</summary>
    public int Height { get; private set; } = 30;

    /// <summary>是否强制关闭 ANSI 颜色。</summary>
    public bool NoColor { get; private set; }

    /// <summary>解析错误信息；非空时应当只打印错误。</summary>
    public string? Error { get; private set; }

    /// <summary>解析命令行。</summary>
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        bool frameRequested = false;
        bool simulateRequested = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help" or "-h" or "/?":
                    options.Mode = RunMode.Help;
                    break;

                case "--simulate" or "-s":
                    simulateRequested = true;
                    if (i + 1 < args.Length && double.TryParse(args[i + 1], out double seconds))
                    {
                        options.SimulateSeconds = seconds;
                        i++;
                    }
                    break;

                case "--auto" or "-a":
                    options.AutoPlay = true;
                    break;

                case "--seed":
                    if (i + 1 < args.Length && ulong.TryParse(args[i + 1], out ulong seed)) { options.Seed = seed; i++; }
                    else options.Error = "--seed 需要一个非负整数。";
                    break;

                case "--save":
                    if (i + 1 < args.Length) { options.SavePath = args[i + 1]; i++; }
                    else options.Error = "--save 需要一个文件路径。";
                    break;

                case "--no-save":
                    options.SavePath = null;
                    break;

                case "--frame":
                    frameRequested = true;
                    if (i + 1 < args.Length && TryParseSize(args[i + 1], out int w, out int h))
                    {
                        options.Width = w;
                        options.Height = h;
                        i++;
                    }
                    break;

                case "--size":
                    if (i + 1 < args.Length && TryParseSize(args[i + 1], out int sw, out int sh))
                    {
                        options.Width = sw;
                        options.Height = sh;
                        i++;
                    }
                    else options.Error = "--size 需要形如 100x30 的参数。";
                    break;

                case "--no-color":
                    options.NoColor = true;
                    break;

                default:
                    options.Error = $"未知参数：{arg}";
                    break;
            }

            if (options.Error is not null) break;
        }

        // 模式优先级：帮助 > 单帧渲染 > 无头模拟 > 交互。
        // 这样 `--simulate 600 --auto --frame`（用模拟预热后截图）无论参数顺序如何都成立。
        if (options.Mode != RunMode.Help)
        {
            options.Mode = frameRequested ? RunMode.Frame
                : simulateRequested ? RunMode.Simulate
                : RunMode.Interactive;
        }

        return options;
    }

    /// <summary>打印帮助。</summary>
    public static string HelpText => """
        猫咖物语 · Neko Clicker —— 增量游戏框架示例

        用法:
          neko-clicker [选项]

        选项:
          --simulate <秒>     无头模拟指定时长后打印报告（默认 3600 秒）
          --auto              模拟时启用自动购买策略（否则纯挂机）
          --seed <整数>       固定随机种子（同一存档的随机序列可复现）
          --save <路径>       存档文件（默认 saves/neko.json）
          --no-save           本次运行不读写存档
          --frame [宽x高]     渲染一帧界面到标准输出后退出（默认 100x30）
          --size <宽x高>      指定界面尺寸
          --no-color          关闭 ANSI 颜色
          -h, --help          显示本帮助

        游戏内按键:
          空格 / C    撸猫（手动点击）
          Tab         切换面板焦点（建筑 → 升级 → 成就）
          ↑ / ↓       移动选择
          1-9, 0      直接选中当前面板的第 1~10 项
          Enter       执行（买建筑 / 买升级）
          X           切换批量档位（×1 → ×10 → ×100 → 买满）
          V           在"买"与"卖"之间切换
          G           抓住金猫
          A           转生（需要二次确认，按 Y 确认）
          F5          手动存档
          H           帮助
          Q / Esc     存档并退出

        示例:
          neko-clicker                          # 开始玩
          neko-clicker --simulate 21600 --auto  # 自动玩 6 小时并打印报告
          neko-clicker --frame 120x34 --no-color > frame.txt
        """;

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = 0;
        height = 0;

        string[] parts = text.Split(['x', 'X', '*'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height)) return false;

        width = Math.Clamp(width, 40, 400);
        height = Math.Clamp(height, 12, 200);
        return true;
    }
}
