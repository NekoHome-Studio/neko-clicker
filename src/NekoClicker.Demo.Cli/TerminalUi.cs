using System.Text;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Views;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 把 <see cref="GameSnapshot"/> 渲染成一组定宽文本行。<para>
/// 渲染器是纯函数（快照进、字符串数组出），所以可以脱离终端测试与截图：
/// <c>--frame</c> 模式用的就是它。所有对齐都走 <see cref="Ansi.DisplayWidth"/>，
/// 否则中文游戏名与 emoji 会把整个表格撑歪。
/// </para>
/// </summary>
internal static class TerminalUi
{
    private static readonly string[] SlotKeys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    /// <summary>界面布局尺寸。集中算一次，避免各面板各算一套导致列宽不一致。</summary>
    private readonly record struct Layout(int Width, int Height, int Inner, int Left, int Right)
    {
        public static Layout Create(int width, int height)
        {
            width = Math.Max(40, width);
            height = Math.Max(14, height);
            int inner = width - 2;
            int left = Math.Clamp((int)(inner * 0.46), 26, 54);
            return new Layout(width, height, inner, left, inner - left);
        }
    }

    /// <summary>渲染一帧。</summary>
    /// <param name="session">会话。</param>
    /// <param name="width">终端宽度（列）。</param>
    /// <param name="height">终端高度（行）。</param>
    /// <returns>恰好 <paramref name="height"/> 行，每行显示宽度恰好 <paramref name="width"/>。</returns>
    public static List<string> Render(GameSession session, int width, int height)
    {
        Layout layout = Layout.Create(width, height);
        GameSnapshot snap = session.Snapshot;

        // 行数预算：顶边+顶栏+状态+分隔 (4) + 主体 (bodyHeight) + 分隔+日志×2+键位+底边 (5)
        int bodyHeight = layout.Height - 9;
        int listRows = Math.Max(1, bodyHeight - 2);

        bool rightIsUpgrades = session.Focus != PanelFocus.Achievements;
        int leftCount = session.Buildings.Count;
        int rightCount = rightIsUpgrades ? session.Upgrades.Count : session.Achievements.Count;
        int leftSelected = session.Focus == PanelFocus.Buildings ? session.Selected : -1;
        int rightSelected = session.Focus == PanelFocus.Buildings ? -1 : session.Selected;

        (int bStart, int bCount) = Window(leftCount, listRows, leftSelected);
        (int rStart, int rCount) = Window(rightCount, listRows, rightSelected);

        var lines = new List<string>(layout.Height)
        {
            Border(layout, '╭', '╮', $" {snap.Title} · 增量游戏框架示例 "),
            Column(layout, HeaderLeft(snap), HeaderRight(session, snap)),
            Column(layout, StatusLeft(session, snap), StatusRight(snap)),
            Border(layout, '├', '┤', null),
        };

        if (session.ShowHelp)
        {
            RenderHelp(lines, layout, bodyHeight, session);
        }
        else
        {
            lines.Add(Column(layout,
                PanelHeader(BuildBuildingTitle(session, bStart, bCount, leftCount), layout.Left),
                PanelHeader(rightIsUpgrades
                    ? BuildUpgradeTitle(session.Upgrades.Count, rStart, rCount, rightCount)
                    : BuildAchievementTitle(snap, rStart, rCount, rightCount), layout.Right)));

            for (int i = 0; i < listRows; i++)
            {
                UiLine leftLine = UiLine.New();
                int buildingIndex = bStart + i;
                if (buildingIndex < bStart + bCount)
                    BuildingRow(leftLine, session, session.Buildings[buildingIndex], buildingIndex, layout.Left);

                UiLine rightLine = UiLine.New();
                int rightIndex = rStart + i;
                if (rightIndex < rStart + rCount)
                {
                    if (rightIsUpgrades) UpgradeRow(rightLine, session, session.Upgrades[rightIndex], rightIndex, layout.Right);
                    else AchievementRow(rightLine, session, session.Achievements[rightIndex], rightIndex, layout.Right);
                }

                lines.Add(Column(layout, leftLine, rightLine));
            }

            lines.Add(FullRow(layout, DetailLine(session, snap)));
        }

        lines.Add(Border(layout, '├', '┤', null));
        lines.Add(FullRow(layout, LogLine(session, 1)));
        lines.Add(FullRow(layout, LogLine(session, 0)));
        lines.Add(FullRow(layout, Footer(session)));
        lines.Add(Border(layout, '╰', '╯', null));

        // 兜底：终端尺寸变化或极端小窗口时保证行数精确匹配，避免界面"爬屏"。
        while (lines.Count > layout.Height) lines.RemoveAt(lines.Count - 1);
        while (lines.Count < layout.Height) lines.Add(new string(' ', layout.Width));

        return lines;
    }

    // ---------------------------------------------------------------- 顶栏与状态

    private static UiLine HeaderLeft(GameSnapshot snap) => UiLine.New()
        .Add(" ")
        .Add($"{snap.CurrencyIcon} ", Ansi.S(Style.BrightYellow))
        .Add(snap.CookiesText, Ansi.S(Style.Bold + Style.BrightYellow))
        .Add($" {snap.CurrencyName}", Ansi.S(Style.Gray))
        .Add("   每秒 ", Ansi.S(Style.Gray))
        .Add(snap.CpsText, Ansi.S(Style.Bold + Style.BrightCyan))
        .Add("   点击 ", Ansi.S(Style.Gray))
        .Add(snap.ClickPowerText, Ansi.S(Style.Bold + Style.Green));

    private static UiLine HeaderRight(GameSession session, GameSnapshot snap) => UiLine.New()
        .Add(" ")
        .Add($"{snap.PrestigeCurrencyIcon} ", Ansi.S(Style.Green))
        .Add(NumFormat.FormatPlain(snap.PrestigeChips), Ansi.S(Style.Bold + Style.Green))
        .Add($" {snap.PrestigeCurrencyName}", Ansi.S(Style.Gray))
        .Add($" Lv{snap.PrestigeLevel}", Ansi.S(Style.Bold + Style.Magenta))
        .Add($"  {session.Package.PrestigeActionName} {ProgressBar(snap.Prestige.Progress, 8)} ", Ansi.S(Style.Gray))
        .Add(NumFormat.Percent(snap.Prestige.Progress, 0), Ansi.S(Style.Gray))
        .Add($"  成就 {snap.AchievementCount}/{snap.AchievementTotal}", Ansi.S(Style.Gray))
        .Add($"  建筑 {NumFormat.FormatPlain(snap.TotalBuildings)}", Ansi.S(Style.Gray));

    private static UiLine StatusLeft(GameSession session, GameSnapshot snap)
    {
        var line = UiLine.New().Add(" ");

        if (session.AwaitingAscendConfirm)
        {
            return line.Add($"⚠ 确认{session.Package.PrestigeActionName}？", Ansi.S(Style.Bold + Style.Red))
                .Add(" 将清空本轮进度（建筑、普通升级、增益），换取 ", Ansi.S(Style.Yellow))
                .Add(NumFormat.FormatLong(snap.Prestige.ChipsOnAscend), Ansi.S(Style.Bold + Style.Green))
                .Add($" {snap.PrestigeCurrencyName}。按 ", Ansi.S(Style.Gray))
                .Add("Y", Ansi.S(Style.Bold + Style.BrightGreen))
                .Add(" 确认，其他键取消。", Ansi.S(Style.Gray));
        }

        if (snap.GoldenCookies.Count > 0)
        {
            GoldenCookieView cookie = snap.GoldenCookies[0];
            return line.Add($"🌟 {session.Package.GoldenCookieName}出现！", Ansi.S(Style.Bold + Style.BrightYellow))
                .Add($" 剩 {NumFormat.Duration(cookie.RemainingSeconds)}", Ansi.S(Style.BrightYellow))
                .Add($"  位置 ({(int)(cookie.X * 100)}%, {(int)(cookie.Y * 100)}%)  按 ", Ansi.S(Style.Gray))
                .Add("G", Ansi.S(Style.Bold + Style.BrightGreen))
                .Add(" 抓住", Ansi.S(Style.Gray));
        }

        return line.Add($"🌟 下一只{session.Package.GoldenCookieName} ", Ansi.S(Style.Gray))
            .Add(NumFormat.Duration(Math.Max(0, snap.GoldenCookieCountdown)), Ansi.S(Style.Gray))
            .Add($"   已抓 {NumFormat.FormatPlain(snap.GoldenCookiesClicked)} 只", Ansi.S(Style.Gray))
            .Add($"   游玩 {NumFormat.Duration(snap.PlayTimeSeconds)}", Ansi.S(Style.Gray));
    }

    private static UiLine StatusRight(GameSnapshot snap)
    {
        var line = UiLine.New().Add(" ");

        if (snap.Buffs.Count == 0)
            return line.Add("无增益生效", Ansi.S(Style.Gray));

        for (int i = 0; i < snap.Buffs.Count && i < 3; i++)
        {
            BuffView buff = snap.Buffs[i];
            string style = buff.IsDebuff ? Ansi.S(Style.Red) : Ansi.S(Style.Magenta);
            if (i > 0) line.Add("  │  ", Ansi.S(Style.Gray));
            line.Add($"{buff.Icon} {buff.Name}", style)
                .Add($" {NumFormat.Duration(buff.RemainingSeconds)}", Ansi.S(Style.Gray));
            if (buff.Stacks > 1) line.Add($" ×{buff.Stacks}", style);
        }

        return line;
    }

    // ---------------------------------------------------------------- 面板标题

    private static UiLine PanelHeader(string text, int width)
        => UiLine.New().AddPadded(text, width - 1, Ansi.S(Style.Bold + Style.Gray));

    private static string BuildBuildingTitle(GameSession session, int start, int count, int total)
        => $" 建筑 [{session.Mode.Label()}]  {total} 项{Scroll(start, count, total)}";

    private static string BuildUpgradeTitle(int available, int start, int count, int total)
        => $" 升级  可买 {available} 项{Scroll(start, count, total)}";

    private static string BuildAchievementTitle(GameSnapshot snap, int start, int count, int total)
        => $" 成就  {snap.AchievementCount}/{snap.AchievementTotal}{Scroll(start, count, total)}";

    private static string Scroll(int start, int count, int total)
    {
        if (total <= count) return string.Empty;
        string up = start > 0 ? "↑" : string.Empty;
        string down = start + count < total ? "↓" : string.Empty;
        return $"  {up}{down} {start + 1}-{start + count}";
    }

    // ---------------------------------------------------------------- 列表行

    private static void BuildingRow(UiLine line, GameSession session, BuildingView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Buildings && index == session.Selected;

        string name = view.IsUnlocked ? view.Name : $"{view.Name}（未解锁）";
        string price = view.IsUnlocked ? NumFormat.Format(view.UnitPrice, NumberStyle.Short) : "—";

        // 结构：空格 ▸ 1 🐈 名称(可变) 数量(4) 单价(9) 尾空格 —— 合计 nameWidth + 23 列
        int nameWidth = Math.Max(6, width - 23);
        string row = $" {(selected ? '▸' : ' ')}{Slot(index)} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(view.Owned.ToString(), 4)} {Ansi.PadLeft(price, 9)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : !view.IsUnlocked || !view.CanAfford ? Ansi.S(Style.Gray) : string.Empty;

        line.Add(row, style);
    }

    private static void UpgradeRow(UiLine line, GameSession session, UpgradeView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Upgrades && index == session.Selected;

        string currency = view.Currency == UpgradeCurrency.PrestigeChips
            ? session.Snapshot.PrestigeCurrencyIcon
            : session.Snapshot.CurrencyIcon;
        string price = NumFormat.Format(view.Price, NumberStyle.Short) + currency;
        string name = view.Owned > 0 ? $"{view.Name} ×{view.Owned}" : view.Name;

        // 结构：空格 ▸ 1 🐈 名称(可变) 价格(10) 尾空格 —— 合计 nameWidth + 19 列
        int nameWidth = Math.Max(6, width - 19);
        string row = $" {(selected ? '▸' : ' ')}{Slot(index)} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(price, 10)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : view.CanAfford ? Ansi.S(Style.BrightCyan) : Ansi.S(Style.Gray);

        line.Add(row, style);
    }

    private static void AchievementRow(UiLine line, GameSession session, AchievementView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Achievements && index == session.Selected;

        // 结构：空格 ▸ ✓ 🐈 名称(可变) 进度(可变) 尾空格 —— 合计 nameWidth + progressWidth + 10 列
        int progressWidth = Math.Clamp(width / 4, 0, 12);
        int nameWidth = Math.Max(6, width - 10 - progressWidth);
        string row = $" {(selected ? '▸' : ' ')}{(view.Unlocked ? '✓' : '·')} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(view.Name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(Ansi.Truncate(view.ProgressText, progressWidth), progressWidth)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : view.Unlocked ? Ansi.S(Style.Green) : Ansi.S(Style.Gray);

        line.Add(row, style);
    }

    private static string Slot(int index) => index < SlotKeys.Length ? SlotKeys[index] : "·";

    // ---------------------------------------------------------------- 详情 / 日志 / 键位

    private static UiLine DetailLine(GameSession session, GameSnapshot snap)
    {
        var line = UiLine.New().Add(" ");

        switch (session.Focus)
        {
            case PanelFocus.Buildings when session.Selected < session.Buildings.Count:
            {
                BuildingView view = session.Buildings[session.Selected];

                if (!view.IsUnlocked)
                {
                    return line.Add("🔒 ", Ansi.S(Style.Gray))
                        .Add($"解锁条件：{view.UnlockHint}", Ansi.S(Style.Yellow))
                        .Add($"　进度 {NumFormat.Percent(view.UnlockProgress, 0)}", Ansi.S(Style.Gray));
                }

                line.Add($"{view.Icon} {view.Name}", Ansi.S(Style.Bold))
                    .Add($"　单个 {NumFormat.Format(view.CpsEach, NumberStyle.Short)}/s", Ansi.S(Style.Cyan))
                    .Add($"　合计 {NumFormat.Format(view.CpsContribution, NumberStyle.Short)}/s 占 {NumFormat.Percent(view.CpsShare, 1)}", Ansi.S(Style.Cyan))
                    .Add($"　本次{(session.Mode.IsSell() ? "返还" : "花费")} {NumFormat.Format(view.BatchPrice, NumberStyle.Short)}", Ansi.S(Style.Gray));

                if (view.NextMilestoneAt is { } milestone)
                {
                    int needed = Math.Max(0, milestone - view.Owned);
                    line.Add($"　再买 {needed} 个解锁「{view.NextMilestoneName}」", Ansi.S(Style.BrightYellow));
                }
                return line;
            }

            case PanelFocus.Upgrades when session.Selected < session.Upgrades.Count:
            {
                UpgradeView view = session.Upgrades[session.Selected];
                line.Add($"{view.Icon} {view.Name}", Ansi.S(Style.Bold))
                    .Add($"　{NumFormat.Format(view.Price, NumberStyle.Short)}", Ansi.S(Style.BrightYellow))
                    .Add($"　{view.EffectSummary}", Ansi.S(Style.Cyan));
                if (view.Owned > 0) line.Add($"　已购 {view.Owned}/{view.MaxPurchases}", Ansi.S(Style.Gray));
                return line;
            }

            case PanelFocus.Achievements when session.Selected < session.Achievements.Count:
            {
                AchievementView view = session.Achievements[session.Selected];
                line.Add($"{view.Icon} {view.Name}", Ansi.S(Style.Bold))
                    .Add($"　{view.Description}", Ansi.S(Style.Gray));
                if (view.ProgressText.Length > 0) line.Add($"　进度 {view.ProgressText}", Ansi.S(Style.BrightYellow));
                return line;
            }

            default:
                _ = snap;
                return line.Add("按 Tab 切换面板，↑↓ 选择，Enter 执行。", Ansi.S(Style.Gray));
        }
    }

    /// <summary>取倒数第 <paramref name="offsetFromEnd"/> 条日志（0 = 最新）。</summary>
    private static UiLine LogLine(GameSession session, int offsetFromEnd)
    {
        var line = UiLine.New();
        IReadOnlyList<GameNotification> log = session.LogLines;
        int index = log.Count - 1 - offsetFromEnd;
        if (index < 0) return line;

        GameNotification entry = log[index];
        string style = entry.Kind switch
        {
            NotificationKind.Success => Ansi.S(Style.Green),
            NotificationKind.Warning => Ansi.S(Style.Yellow),
            NotificationKind.Rare => Ansi.S(Style.BrightMagenta),
            _ => Ansi.S(Style.Gray),
        };

        line.Add(" ");
        if (entry.Icon.Length > 0) line.Add(entry.Icon + " ", style);
        return line.Add(entry.Message, offsetFromEnd == 0 ? style : Ansi.S(Style.Gray));
    }

    private static UiLine Footer(GameSession session)
    {
        GameSnapshot snap = session.Snapshot;
        (string Key, string Text)[] hints =
        [
            ("空格", snap.ClickActionName),
            ("Tab", "切面板"),
            ("Enter", "执行"),
            ("X", "批量"),
            ("V", "买卖"),
            ("G", session.Package.GoldenCookieName),
            ("A", session.Package.PrestigeActionName),
            ("F5", "存档"),
            ("H", "帮助"),
            ("Q", "退出"),
        ];

        var line = UiLine.New().Add(" ");
        for (int i = 0; i < hints.Length; i++)
        {
            if (i > 0) line.Add("  ");
            line.Add(hints[i].Key, Ansi.S(Style.Bold + Style.BrightCyan))
                .Add(" " + hints[i].Text, Ansi.S(Style.Gray));
        }
        return line;
    }

    private static void RenderHelp(List<string> lines, Layout layout, int bodyHeight, GameSession session)
    {
        GameSnapshot snap = session.Snapshot;
        double sellRefund = session.Engine.Balance.DefaultSellRefundRate;
        double clickRatio = session.Engine.Balance.ClickCpsRatio;
        double cookieLifetime = session.Engine.Balance.GoldenCookieLifetime;

        string[] help =
        [
            "  按键",
            $"    空格 / C    {snap.ClickActionName}（手动点击，收益 = 1 + 当前每秒产量的 {NumFormat.Percent(clickRatio, 1)}）",
            "    Tab         切换面板焦点：建筑 → 升级 → 成就",
            "    ↑ / ↓       移动选择    1-9 / 0 直接跳到第 1~10 项",
            "    Enter       购买选中的建筑或升级",
            "    X           切换批量档位：×1 → ×10 → ×100 → 买满",
            $"    V           在「买」与「卖」之间切换（卖出返还 {NumFormat.Percent(sellRefund, 0)}）",
            $"    G           抓住{session.Package.GoldenCookieName}（只停留 {NumFormat.Duration(cookieLifetime)}，出现时顶部会提示）",
            $"    A           {session.Package.PrestigeHint}",
            "    F5          手动存档（默认每 60 秒自动存档一次）",
            "    H           关闭本帮助        Q / Esc  存档并退出",
            string.Empty,
            "  玩法要点",
            .. session.Package.HelpTips,
        ];

        int titleRows = 1;
        lines.Add(FullRow(layout, PanelHeader(" 帮助", layout.Inner)));

        int available = bodyHeight - titleRows;
        for (int i = 0; i < available; i++)
        {
            string text = i < help.Length ? help[i] : string.Empty;
            lines.Add(FullRow(layout, UiLine.New().AddPadded(text, layout.Inner - 1, Ansi.S(Style.Gray))));
        }
    }

    // ---------------------------------------------------------------- 布局工具

    private static string Border(Layout layout, char left, char right, string? label)
    {
        int inner = layout.Inner;
        if (label is null) return left + Ansi.Repeat("─", inner) + right;

        string text = Ansi.Truncate(label, inner - 1);
        int remaining = Math.Max(0, inner - Ansi.DisplayWidth(text));
        return left + Ansi.S(Style.Bold) + text + Ansi.S(Style.Reset) + Ansi.Repeat("─", remaining) + right;
    }

    private static string Column(Layout layout, UiLine left, UiLine right)
        => "│" + left.Render(layout.Left) + "│" + right.Render(layout.Right) + "│";

    private static string FullRow(Layout layout, UiLine content)
        => "│" + content.Render(layout.Inner) + "│";

    /// <summary>计算滚动窗口。选中项为 <c>-1</c>（面板未聚焦）时从头开始显示。</summary>
    private static (int Start, int Count) Window(int total, int visible, int selected)
    {
        if (total <= visible) return (0, total);
        if (selected < 0) return (0, visible);

        int start = Math.Clamp(selected - (visible / 2), 0, total - visible);
        return (start, visible);
    }

    private static string ProgressBar(double ratio, int width)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        int filled = (int)Math.Round(ratio * width);
        return new StringBuilder()
            .Append(Ansi.S(Style.Green)).Append('█', filled)
            .Append(Ansi.S(Style.Gray)).Append('░', width - filled)
            .Append(Ansi.S(Style.Reset))
            .ToString();
    }
}
