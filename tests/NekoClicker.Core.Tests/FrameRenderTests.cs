using NekoClicker.Core.Content;
using NekoClicker.Demo.Cli;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 终端帧渲染的尺寸不变量（Demo 层）。<para>
/// 这两条不变量是"不出洋相"的全部：
/// <list type="number">
///   <item>返回的行数必须<b>恰好等于</b>终端高度——多一行，终端就会滚屏，整块界面往上跳。</item>
///   <item>每一行的显示宽度必须<b>恰好等于</b>终端宽度——多一列，最后那一列会折到下一行，
///   把下面所有行顶歪；少一列则要靠 <c>EL</c> 擦尾巴。</item>
/// </list>
/// 首版就栽在第二条上：两栏行按 <c>inner = width - 2</c> 排版，却写了 3 个 <c>│</c> 分隔符，
/// 于是每一行都是 <c>width + 1</c> 列。118 列的窗口里有 24 行是 119 列——在宽松的终端上
/// 只是偶尔看到花屏，在 conhost（PowerShell 的宿主）上就是持续滚屏 + 选项错位。
/// </para>
/// </summary>
public static class FrameRenderTests
{
    private static readonly (int Width, int Height)[] Sizes =
    [
        (40, 14), (60, 20), (80, 24), (100, 30), (118, 32), (160, 40), (200, 50),
        // 小于最小可用尺寸：必须给一个"恰好装得下"的提示帧，绝不能溢出。
        (30, 9), (20, 5), (8, 3), (1, 1),
    ];

    [Test]
    public static void EveryFrameFitsTheViewportExactly()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);

        foreach ((int width, int height) in Sizes)
        {
            AssertFits(TerminalUi.Render(session, width, height), width, height, "默认视图");
        }
    }

    [Test]
    public static void EveryPanelFitsTheViewportExactly()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);

        foreach (PanelFocus panel in Enum.GetValues<PanelFocus>())
        {
            session.SetFocus(panel);
            foreach ((int width, int height) in Sizes)
            {
                AssertFits(TerminalUi.Render(session, width, height), width, height, $"面板 {panel}");
            }
        }
    }

    [Test]
    public static void HelpOverlayFitsTheViewportExactly()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);
        session.ToggleHelp();

        foreach ((int width, int height) in Sizes)
        {
            AssertFits(TerminalUi.Render(session, width, height), width, height, "帮助浮层");
        }
    }

    [Test]
    public static void CompanyPackFitsTheViewportExactly()
    {
        // 公司包的图标里有 ZWJ 序列（❤️‍🔥），是最容易算错宽度的一类字符，单独过一遍。
        using var session = new GameSession(ContentPackages.Find("company")!, savePath: null, seed: 7);
        session.SetFocus(PanelFocus.Choices);

        foreach ((int width, int height) in new[] { (40, 14), (80, 24), (118, 32) })
        {
            AssertFits(TerminalUi.Render(session, width, height), width, height, "公司包/表态面板");
        }
    }

    [Test]
    public static void TooSmallWindow_ShowsAFittingHint()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);
        List<string> lines = TerminalUi.Render(session, 30, 9);

        AssertFits(lines, 30, 9, "过小窗口");
        Check.Contains(
            lines[0],
            "终端太小",
            "过小窗口应当给一句人话提示，而不是硬渲染一屏放不下的界面（那会持续滚屏）。");
    }

    [Test]
    public static void FramesAvoidFontFallbackProneGlyphs()
    {
        // 这些字符是 Unicode East Asian Ambiguous：在中文 Windows 上常被字体回退按 2 列渲染，
        // 在拉丁等宽字体里却是 1 列。它们一旦出现在行内，宽度就算不准——右边的分隔线会偏、
        // 行尾会折到下一行被覆盖（"选项消失"）。布局里一律用确定性宽度的替代品：
        // 箭头 ^ / v、勾 v、叉 -、警告 !、省略号 ..
        // 制表符（│ ─ ╭…）与 · × █ ░ 不在此列：主流等宽字体自带单宽字形，实测也没有问题。
        char[] forbidden = ['▸', '✓', '⚠', '↑', '↓', '←', '…'];

        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);

        foreach (PanelFocus panel in Enum.GetValues<PanelFocus>())
        {
            session.SetFocus(panel);
            IReadOnlyList<string> lines = TerminalUi.Render(session, 118, 32);

            foreach (string line in lines)
            {
                foreach (char ch in forbidden)
                {
                    Check.False(
                        line.Contains(ch, StringComparison.Ordinal),
                        $"面板 {panel} 的帧里出现了宽度不可靠的字符「{ch}」（U+{(int)ch:X4}）：{line}");
                }
            }
        }
    }

    [Test]
    public static void WidthModel_HandlesEscapesWideCharsAndZwj()
    {
        // 转义序列是零宽：带样式的字符串必须和它的可见文本一样宽。
        Check.Equal(2, Ansi.DisplayWidth("\u001b[32m猫\u001b[0m"));
        Check.Equal(2, Ansi.DisplayWidth("猫"), "汉字按两列算。");
        Check.Equal(1, Ansi.DisplayWidth("a"));
        Check.Equal(2, Ansi.DisplayWidth("🐈"), "emoji 按两列算。");
        Check.Equal(2, Ansi.DisplayWidth("❤️‍🔥"), "ZWJ 序列是一个 emoji，不能逐码位相加（那会算成 4 列）。");

        // 截断后不能超宽（换行/滚屏的根源就是"截完还超"）。
        foreach (int limit in new[] { 1, 2, 3, 4, 6, 9 })
            Check.AtMost(Ansi.DisplayWidth(Ansi.Truncate("猫咪咖啡馆的招牌", limit)), limit);
    }

    private static void AssertFits(List<string> lines, int width, int height, string what)
    {
        Check.Equal(height, lines.Count, $"{what} {width}×{height}：行数不匹配，多出来的行会让终端滚屏。");

        for (int i = 0; i < lines.Count; i++)
        {
            Check.False(
                lines[i].Contains('\n', StringComparison.Ordinal),
                $"{what} {width}×{height}：第 {i + 1} 行里混进了换行符。");

            // 注意这里量的是带样式码的原始行：转义序列必须被算成零宽，
            // 否则"上过色的字符串"（例如进度条）会把排版预算算歪——那正是首版的 bug 之一。
            Check.Equal(
                width,
                Ansi.DisplayWidth(lines[i]),
                $"{what} {width}×{height}：第 {i + 1} 行宽度是 {Ansi.DisplayWidth(lines[i])}，"
                + "超宽会折行、偏窄会留脏字符。");
        }
    }
}
