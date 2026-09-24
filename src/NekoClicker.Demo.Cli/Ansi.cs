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

    /// <summary>清除光标之后的内容。</summary>
    public static string ClearToEnd() => ColorEnabled ? "\u001b[J" : string.Empty;

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

    /// <summary>按显示宽度截断，超出时以 "…" 结尾。</summary>
    public static string Truncate(string text, int maxWidth)
    {
        if (maxWidth <= 0) return string.Empty;
        if (DisplayWidth(text) <= maxWidth) return text;

        var builder = new StringBuilder();
        int width = 0;
        foreach (Rune rune in Enumerate(text))
        {
            if (width + rune.Width > maxWidth - 1) return builder.Append('…').ToString();
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

            yield return new Rune(text.Substring(i, charLength), width);
            i += charLength - 1;
        }
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
