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

    /// <summary>
    /// 「玩家被展示过那批待答表态」这份<b>诊断</b>记录由渲染器回答
    /// （<see cref="TerminalUi.ShowsChoicesPanel"/> 的判据，宿主据此调
    /// <see cref="GameEngine.MarkPendingChoicesShown"/>）。<para>
    /// <b>1.5.0 到 1.6.0 之间它是结局能否落定的唯一条件；1.6.0 起判定改看「答没答」</b>，
    /// 这条记录只剩诊断价值（回答"这条表态到底露过面没有"）。判据本身仍然必须准确：
    /// 它现在决定的是"这份记录可不可信"，而宿主不该在没画出来时报"画出来了"。
    /// </para>
    /// <para>
    /// 这条钉住三处会<b>悄悄</b>改变结论的地方：焦点不在表态面板、窗口小到只剩兜底帧、
    /// 帮助浮层盖住了整个主体——三处都会让"没看到"被记成"看到过"。
    /// </para>
    /// </summary>
    [Test]
    public static void ChoicesPanelCountsAsShownOnlyWhenItIsActuallyOnScreen()
    {
        using var session = new GameSession(ContentPackages.Find("lab")!, savePath: null, seed: 7);

        // 直接塞一条待答表态：这一层测的是"画没画出来"，不是触发条件（触发由引擎的用例管）。
        session.Engine.State.PendingChoices.Add("choice_archive");
        session.Refresh();
        Check.AtLeast(session.ChoiceRows.Count, 1, "前提：待答表态应当已经进了面板的行缓存。");

        Check.False(
            TerminalUi.ShowsChoicesPanel(session, 100, 30),
            "焦点还在建筑面板上时「表态」面板并没有画出来——不许算成展示过。");

        session.SetFocus(PanelFocus.Choices);
        Check.True(
            TerminalUi.ShowsChoicesPanel(session, 100, 30),
            "焦点在表态面板、而且窗口画得下时，才算真的展示过。");

        Check.False(
            TerminalUi.ShowsChoicesPanel(session, 20, 5),
            "窗口小到只画「终端太小」那一帧时，面板没有画出来（过小窗口的兜底帧不算展示）。");

        session.ToggleHelp();
        Check.False(
            TerminalUi.ShowsChoicesPanel(session, 100, 30),
            "帮助浮层盖住了整个主体时，面板没有画出来。");
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

    [Test]
    public static void RenderNeverThrowsForAnyWindowSize()
    {
        // 交互循环每帧都调 Render，任何 (宽, 高) 组合抛一次就是一次崩溃——
        // 用户缩放窗口时正好踩到过。窗口尺寸的唯一上限是终端给的数，下限是 1。
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);

        foreach (PanelFocus panel in Enum.GetValues<PanelFocus>())
        {
            session.SetFocus(panel);
            for (int height = 1; height <= 40; height++)
                for (int width = 1; width <= 120; width++)
                    TerminalUi.Render(session, width, height);
        }

        session.ToggleHelp();
        for (int height = 1; height <= 40; height++)
            for (int width = 1; width <= 120; width++)
                TerminalUi.Render(session, width, height);
    }

    [Test]
    public static void RenderNeverThrowsWithAFullGameState()
    {
        // 开局状态的列表都很短，索引边界与真实游玩完全不同。这里先跑出一段真实进度
        //（成就 / 图鉴 / 待答选择都攒起来），再按各种高度渲染一遍。
        using var session = new GameSession(ContentPackages.Find("company")!, savePath: null, seed: 20240924);

        for (int round = 0; round < 300; round++)
        {
            for (int i = 0; i < 8; i++) session.Engine.Click();
            TestGame.BuyGreedily(session.Engine);
            if (session.Engine.EraGate.CanAdvance) session.Engine.Ascend();
            session.Engine.Simulate(18);
        }
        session.Refresh();

        foreach (PanelFocus panel in Enum.GetValues<PanelFocus>())
        {
            session.SetFocus(panel);
            for (int height = 1; height <= 40; height++)
                for (int width = 1; width <= 80; width++)
                    TerminalUi.Render(session, width, height);
        }
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
