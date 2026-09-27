using System.Runtime.InteropServices;
using System.Text;

namespace NekoClicker.Demo.Cli;

/// <summary>ANSI 样式片段。</summary>
internal static class Style
{
    public const string Reset = "\u001b[0m";
    public const string Bold = "\u001b[1m";
    public const string Dim = "\u001b[2m";
    public const string Inverse = "\u001b[7m";
    public const string Red = "\u001b[31m";
    public const string Green = "\u001b[32m";
    public const string Yellow = "\u001b[33m";
    public const string Blue = "\u001b[34m";
    public const string Magenta = "\u001b[35m";
    public const string Cyan = "\u001b[36m";
    public const string Gray = "\u001b[90m";
    public const string BrightGreen = "\u001b[92m";
    public const string BrightYellow = "\u001b[93m";
    public const string BrightCyan = "\u001b[96m";
    public const string BrightMagenta = "\u001b[95m";
}

/// <summary>
/// 终端控制与"显示宽度"计算。<para>
/// 后半个功能比看起来重要：中文与 emoji 在终端里占 <b>2 列</b>，用 <c>string.Length</c>
/// 对齐会让整个表格错位。这里按 Unicode East Asian Width 的常用区间做近似，
/// 并处理"基础字符 + U+FE0F 变体选择符"会变成双宽 emoji 的情况（例如 ⛓️ / 🛏️）。
/// </para>
/// </summary>
internal static class Ansi
{
    /// <summary>是否输出 ANSI 颜色。关闭后样式片段全部变成空串。</summary>
    public static bool ColorEnabled { get; set; } = true;

    /// <summary>取样式片段（关闭颜色时返回空串）。</summary>
    public static string S(string style) => ColorEnabled ? style : string.Empty;

    /// <summary>进入备用屏幕缓冲区（全屏界面，退出后恢复原终端内容）。</summary>
    public static string EnterAlternateScreen() => ColorEnabled ? "\u001b[?1049h" : string.Empty;

    /// <summary>离开备用屏幕缓冲区。</summary>
    public static string ExitAlternateScreen() => ColorEnabled ? "\u001b[?1049l" : string.Empty;

    /// <summary>隐藏光标。</summary>
    public static string HideCursor() => ColorEnabled ? "\u001b[?25l" : string.Empty;

    /// <summary>显示光标。</summary>
    public static string ShowCursor() => ColorEnabled ? "\u001b[?25h" : string.Empty;

    /// <summary>把光标移到左上角。</summary>
    public static string Home() => ColorEnabled ? "\u001b[H" : string.Empty;

    /// <summary>清屏。</summary>
    public static string Clear() => ColorEnabled ? "\u001b[2J" : string.Empty;

    /// <summary>
    /// 清空滚动缓冲（<c>ED 3</c>：erase saved lines）。<para>
    /// 传统 conhost 在"窗口宽度变化 + 换行重排"时有崩溃缺陷：缓冲区里任何一行被重排都可能踩到。
    /// 主缓冲区模式下先把进入游戏前的 shell 历史清掉，缓冲区里就只剩我们自己的帧，
    /// 重排的风险面小得多。不支持这条序列的终端会忽略它。
    /// </para>
    /// </summary>
    public static string ClearScrollback() => ColorEnabled ? "\u001b[3J" : string.Empty;

    /// <summary>清除光标之后的内容。</summary>
    public static string ClearToEnd() => ColorEnabled ? "\u001b[J" : string.Empty;

    /// <summary>把光标移到指定行的第 1 列（行号从 1 起）。增量刷新靠它定位。</summary>
    public static string MoveTo(int row) => ColorEnabled ? $"\u001b[{row};1H" : string.Empty;

    /// <summary>擦除光标到行尾（<c>EL</c>）。整行覆盖后补一发，帧变短时不留脏字符。</summary>
    public static string ClearLine() => ColorEnabled ? "\u001b[K" : string.Empty;

    /// <summary>
    /// 开始"同步输出"（DEC 私有模式 2026）。<para>
    /// 这才是终端上真正意义上的双缓冲：夹在 <see cref="SyncStart"/> 与 <see cref="SyncEnd"/>
    /// 之间的写入被终端攒成一批，等这一帧写完了才整体上屏。没有它，终端完全可能
    /// 在一帧只写到一半时就开始绘制，于是看到撕裂/闪烁。不支持的终端会忽略这两个序列。
    /// </para>
    /// </summary>
    public static string SyncStart() => ColorEnabled ? "\u001b[?2026h" : string.Empty;

    /// <summary>结束同步输出，让终端把攒下的这一帧整体呈现。</summary>
    public static string SyncEnd() => ColorEnabled ? "\u001b[?2026l" : string.Empty;

    /// <summary>按终端显示宽度计算字符串占用的列数。</summary>
    public static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int width = 0;
        foreach (Rune rune in Enumerate(text)) width += rune.Width;
        return width;
    }

    /// <summary>按显示宽度左侧补空格到 <paramref name="width"/> 列。</summary>
    public static string PadRight(string text, int width)
    {
        int current = DisplayWidth(text);
        return current >= width ? text : text + new string(' ', width - current);
    }

    /// <summary>按显示宽度右侧补空格（数字右对齐用）。</summary>
    public static string PadLeft(string text, int width)
    {
        int current = DisplayWidth(text);
        return current >= width ? text : new string(' ', width - current) + text;
    }

    /// <summary>
    /// 按显示宽度截断，超出时以两个 ASCII 点 <c>..</c> 结尾。<para>
    /// 刻意不用 <c>…</c>：它是 East Asian Ambiguous 宽度字符——在中文 Windows 上常被
    /// 字体回退按 2 列渲染，在拉丁等宽字体里却是 1 列。截断符会出现在大量行里，
    /// 宽度算错一次就整行错位（右边的分隔线会跟着偏）。
    /// </para>
    /// </summary>
    public static string Truncate(string text, int maxWidth)
    {
        if (maxWidth <= 0) return string.Empty;
        if (DisplayWidth(text) <= maxWidth) return text;

        // 太窄时连两个点都放不下：能放几个点就放几个。
        if (maxWidth <= 2) return new string('.', maxWidth);

        var builder = new StringBuilder();
        int width = 0;
        foreach (Rune rune in Enumerate(text))
        {
            if (width + rune.Width > maxWidth - 2) return builder.Append("..").ToString();
            builder.Append(rune.Text);
            width += rune.Width;
        }
        return builder.ToString();
    }

    /// <summary>按显示宽度截取前缀（不追加省略号，用于硬裁列宽）。</summary>
    public static string Slice(string text, int maxWidth)
    {
        if (maxWidth <= 0) return string.Empty;
        if (DisplayWidth(text) <= maxWidth) return text;

        var builder = new StringBuilder();
        int width = 0;
        foreach (Rune rune in Enumerate(text))
        {
            if (width + rune.Width > maxWidth) break;
            builder.Append(rune.Text);
            width += rune.Width;
        }
        return builder.ToString();
    }

    /// <summary>重复一个字符到指定显示宽度。</summary>
    public static string Repeat(string unit, int width)
    {
        if (width <= 0) return string.Empty;

        var builder = new StringBuilder();
        int current = 0;
        int unitWidth = Math.Max(1, DisplayWidth(unit));
        while (current < width)
        {
            builder.Append(unit);
            current += unitWidth;
        }
        return builder.ToString();
    }

    /// <summary>
    /// 在 Windows 上启用 VT 转义序列处理。<para>
    /// 现代 Windows Terminal 默认支持，但 conhost 需要显式打开 ENABLE_VIRTUAL_TERMINAL_PROCESSING，
    /// 否则会看到一堆 <c>←[0m</c> 字面量。失败时返回 <c>false</c>，调用方应降级为纯文本。
    /// </para>
    /// </summary>
    public static bool TryEnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            nint handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (handle == 0 || handle == -1) return false;
            if (!GetConsoleMode(handle, out uint mode)) return false;

            const uint enableVirtualTerminalProcessing = 0x0004;
            if ((mode & enableVirtualTerminalProcessing) != 0) return true;

            return SetConsoleMode(handle, mode | enableVirtualTerminalProcessing);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>一个码位及其显示宽度与原始文本切片。</summary>
    private readonly record struct Rune(string Text, int Width);

    private static IEnumerable<Rune> Enumerate(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            // ANSI 转义序列：可见宽度为 0，但必须原样保留。
            // ProgressBar 这类"已经上好色的字符串"会作为普通文本传给 Slice / DisplayWidth，
            // 若把 ESC[32m 里的 '[' '3' '2' 'm' 当可见字符计数，排版预算会被吃掉，
            // 进度条会整段被裁掉、行也跟着变短——首版的"选项消失"就是这么来的。
            if (text[i] == '\u001b')
            {
                int escapeLength = EscapeLength(text, i);
                yield return new Rune(text.Substring(i, escapeLength), 0);
                i += escapeLength - 1;
                continue;
            }

            int codePoint = text[i];
            int charLength = 1;

            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                charLength = 2;
            }

            int width = CodePointWidth(codePoint);

            // "基础字符 + U+FE0F" 表示强制 emoji 呈现，终端会按双宽绘制
            // （⛓️ 这类字符的基础码位本身是窄的，只看基础码位会算错）。
            if (i + charLength < text.Length && text[i + charLength] == '\uFE0F' && width < 2)
            {
                width = 2;
                charLength++;
            }

            // ZWJ 序列（例如 ❤️‍🔥）：终端把它当**一个** emoji 画，宽 2 列。
            // 逐码位相加会算成 4 列，让整行短 2 列——右边的分隔线会往左偏。
            if (i + charLength < text.Length && text[i + charLength] == '\u200D')
            {
                int end = i + charLength;
                while (end < text.Length && text[end] == '\u200D')
                {
                    end++; // ZWJ 本身零宽
                    if (end >= text.Length) break;

                    end += char.IsHighSurrogate(text[end]) && end + 1 < text.Length
                           && char.IsLowSurrogate(text[end + 1]) ? 2 : 1;
                    if (end < text.Length && text[end] == '\uFE0F') end++;
                }

                charLength = end - i;
                width = 2;
            }

            yield return new Rune(text.Substring(i, charLength), width);
            i += charLength - 1;
        }
    }

    /// <summary>从 <paramref name="start"/>（一个 ESC 的位置）起算整条转义序列的长度。</summary>
    private static int EscapeLength(string text, int start)
    {
        int i = start + 1;
        if (i >= text.Length) return 1;

        // 两字符转义（ESC + 单字符），例如 ESC ( B。
        if (text[i] != '[') return 2;

        // CSI：ESC [ 参数 中间字节 终止字节（0x40~0x7E）。
        i++;
        while (i < text.Length && !(text[i] >= '@' && text[i] <= '~')) i++;
        return Math.Min(i + 1, text.Length) - start;
    }

    private static int CodePointWidth(int codePoint)
    {
        if (codePoint == 0) return 0;
        if (codePoint < 32) return 0;

        // 变体选择符与组合记号不占宽度
        if (codePoint is 0xFE0E or 0xFE0F) return 0;
        if (codePoint is >= 0x0300 and <= 0x036F) return 0;
        if (codePoint is >= 0x200B and <= 0x200F) return 0; // 零宽字符

        if (codePoint < 0x1100) return 1;

        return codePoint switch
        {
            // 默认为 Emoji 呈现、East Asian Width = Wide 的符号区
            0x231A or 0x231B => 2,
            >= 0x23E9 and <= 0x23EC => 2,
            0x23F0 or 0x23F3 => 2,
            >= 0x25FD and <= 0x25FE => 2,
            >= 0x2614 and <= 0x2615 => 2,
            >= 0x2648 and <= 0x2653 => 2,
            0x267F or 0x2693 => 2,
            >= 0x26AA and <= 0x26AB => 2,
            >= 0x26BD and <= 0x26BE => 2,
            >= 0x26C4 and <= 0x26C5 => 2,
            0x26CE or 0x26D4 or 0x26EA => 2,
            >= 0x26F2 and <= 0x26F3 => 2,
            0x26F5 or 0x26FA or 0x26FD => 2,
            0x2705 => 2,
            >= 0x270A and <= 0x270B => 2,
            0x2728 => 2,
            0x274C or 0x274E => 2,
            >= 0x2753 and <= 0x2755 => 2,
            0x2757 => 2,
            >= 0x2795 and <= 0x2797 => 2,
            0x27B0 or 0x27BF => 2,
            >= 0x2B1B and <= 0x2B1C => 2,
            0x2B50 or 0x2B55 => 2,

            // 中日韩与全角
            >= 0x1100 and <= 0x115F => 2, // 韩文字母
            >= 0x2E80 and <= 0x303E => 2, // 中日韩部首
            >= 0x3041 and <= 0x33FF => 2, // 平假名/片假名/注音
            >= 0x3400 and <= 0x4DBF => 2, // 中日韩扩展 A
            >= 0x4E00 and <= 0x9FFF => 2, // 中日韩统一表意文字
            >= 0xA000 and <= 0xA4CF => 2, // 彝文
            >= 0xAC00 and <= 0xD7A3 => 2, // 韩文音节
            >= 0xF900 and <= 0xFAFF => 2, // 中日韩兼容表意文字
            >= 0xFE30 and <= 0xFE6F => 2, // 中日韩兼容形式
            >= 0xFF00 and <= 0xFF60 => 2, // 全角形式
            >= 0xFFE0 and <= 0xFFE6 => 2, // 全角符号

            // emoji 主区
            >= 0x1F300 and <= 0x1F64F => 2,
            >= 0x1F680 and <= 0x1F6FF => 2,
            >= 0x1F900 and <= 0x1F9FF => 2,
            >= 0x1FA70 and <= 0x1FAFF => 2,
            >= 0x20000 and <= 0x3FFFD => 2, // 中日韩扩展 B 及以上

            _ => 1,
        };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}

/// <summary>一行 UI 内容：由若干带样式的片段组成，可按目标宽度渲染成定宽字符串。</summary>
internal sealed class UiLine
{
    private readonly List<(string Text, string Style)> _parts = [];

    /// <summary>追加一个片段。</summary>
    public UiLine Add(string text, string style = "")
    {
        if (text.Length > 0) _parts.Add((text, style));
        return this;
    }

    /// <summary>追加一段按显示宽度补齐到 <paramref name="width"/> 的文本。</summary>
    public UiLine AddPadded(string text, int width, string style = "")
        => Add(Ansi.PadRight(Ansi.Truncate(text, width), width), style);

    /// <summary>追加一个右对齐到 <paramref name="width"/> 列的片段。</summary>
    public UiLine AddRight(string text, int width, string style = "")
        => Add(Ansi.PadLeft(text, width), style);

    /// <summary>
    /// 渲染成恰好 <paramref name="width"/> 列宽的字符串。<para>
    /// 关键点：内容超出时必须<b>裁剪</b>而不是溢出——各面板宽度是算出来的，
    /// 一旦某行溢出，"│" 分隔线就会错位，整个界面看起来就坏了。
    /// </para>
    /// </summary>
    public string Render(int width)
    {
        var builder = new StringBuilder();
        int used = 0;

        foreach ((string text, string style) in _parts)
        {
            if (width > 0 && used >= width) break;

            string chunk = width > 0 ? Ansi.Slice(text, width - used) : text;
            if (chunk.Length == 0) continue;

            used += Ansi.DisplayWidth(chunk);
            if (style.Length == 0)
            {
                builder.Append(chunk);
                continue;
            }
            builder.Append(style).Append(chunk).Append(Ansi.S(Style.Reset));
        }

        if (width > 0 && used < width) builder.Append(' ', width - used);
        return builder.ToString();
    }

    /// <summary>新建一行。</summary>
    public static UiLine New() => new();
}
