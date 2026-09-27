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

    /// <summary>能画出完整界面的最小终端尺寸；更小的窗口只显示一行提示。</summary>
    private const int MinWidth = 40;
    private const int MinHeight = 14;

    /// <summary>界面布局尺寸。集中算一次，避免各面板各算一套导致列宽不一致。</summary>
    private readonly record struct Layout(int Width, int Height, int Inner, int Left, int Right)
    {
        public static Layout Create(int width, int height)
        {
            int inner = width - 2;
            int left = Math.Clamp((int)(inner * 0.46), 26, 54);

            // 两栏行 = "│" + 左栏 + "│" + 右栏 + "│"，分隔符吃掉 3 列；
            // 而 Inner 是给"整行只有左右两个边框"的 Border / FullRow 用的（吃掉 2 列）。
            // 所以右栏要再减 1：Left + Right + 3 == Width。
            // 首版按 inner - left 算，于是每条两栏行都比终端宽出 1 列——多出来的那一列会折到
            // 下一行，接着把整屏顶下去；在 conhost（PowerShell 的宿主）上表现为持续滚屏与错位。
            return new Layout(width, height, inner, left, inner - left - 1);
        }
    }

    /// <summary>渲染一帧。</summary>
    /// <param name="session">会话。</param>
    /// <param name="width">终端宽度（列）。</param>
    /// <param name="height">终端高度（行）。</param>
    /// <returns>恰好 <paramref name="height"/> 行，每行显示宽度恰好 <paramref name="width"/>。</returns>
    public static List<string> Render(GameSession session, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        // 窗口装不下完整界面时，给一个"恰好装得下"的提示帧。
        // 绝不能按最小尺寸硬渲染：那会让每一行都超宽、每一帧都滚屏——正是首版的另一个 bug。
        if (width < MinWidth || height < MinHeight) return TooSmallFrame(width, height);

        Layout layout = Layout.Create(width, height);
        GameSnapshot snap = session.Snapshot;

        // 行数预算：顶边+顶栏+状态+分隔 (4) + 主体 (bodyHeight) + 分隔+日志×2+键位+底边 (5)
        int bodyHeight = layout.Height - 9;
        int listRows = Math.Max(1, bodyHeight - 2);

        // 右栏内容跟随焦点：建筑聚焦时显示升级，否则显示焦点对应的面板。
        PanelFocus right = session.Focus == PanelFocus.Buildings ? PanelFocus.Upgrades : session.Focus;
        int leftCount = session.Buildings.Count;
        int rightCount = right switch
        {
            PanelFocus.Achievements => session.Achievements.Count,
            PanelFocus.Codex => session.Codex.Count,
            // 漏掉这一行就会用"升级数量"去索引选择行：升级比选择多时直接越界崩溃。
            PanelFocus.Choices => session.ChoiceRows.Count,
            _ => session.Upgrades.Count,
        };
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
                PanelHeader(BuildRightTitle(session, snap, right, rStart, rCount, rightCount), layout.Right)));

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
                    switch (right)
                    {
                        case PanelFocus.Achievements:
                            AchievementRow(rightLine, session, session.Achievements[rightIndex], rightIndex, layout.Right);
                            break;
                        case PanelFocus.Codex:
                            CodexRow(rightLine, session, session.Codex[rightIndex], rightIndex, layout.Right);
                            break;
                        case PanelFocus.Choices:
                            ChoiceOptionRow(rightLine, session, session.ChoiceRows[rightIndex], rightIndex, layout.Right);
                            break;
                        default:
                            UpgradeRow(rightLine, session, session.Upgrades[rightIndex], rightIndex, layout.Right);
                            break;
                    }
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

    // 立场轴刻意不放进顶栏：顶栏那一半本来就快占满了，塞进去会把"建筑"之类的尾部挤掉。
    // 完整立场轴在「表态」面板里展示，主导立场变化时也会进日志。

    private static UiLine StatusLeft(GameSession session, GameSnapshot snap)
    {
        var line = UiLine.New().Add(" ");

        if (session.AwaitingAscendConfirm)
        {
            return line.Add($"! 确认{session.Package.PrestigeActionName}？", Ansi.S(Style.Bold + Style.Red))
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

        if (snap.PendingChoices.Count > 0)
        {
            // 表态排在剧情前面：剧情放出来就一直读得到，而表态是**会错过的**
            // （EraId 是硬门，舍命走人就再也遇不到），所以它的提示优先级更高。
            // 注意长度：状态栏左半只有约 50 列，"（会错过）"这种补充说明留给面板标题。
            return line.Add($"🗣 {snap.PendingChoices.Count} 项待答", Ansi.S(Style.Bold + Style.BrightMagenta))
                .Add("　Tab 切「表态」，", Ansi.S(Style.Gray))
                .Add("Enter", Ansi.S(Style.Bold + Style.BrightGreen))
                .Add(" 作答", Ansi.S(Style.Gray));
        }

        if (snap.PendingLore.Count > 0)
        {
            return line.Add($"📖 有 {snap.PendingLore.Count} 段新剧情待读", Ansi.S(Style.Bold + Style.Cyan))
                .Add("　", Ansi.S(Style.Gray))
                .Add("Tab", Ansi.S(Style.Bold + Style.BrightGreen))
                .Add(" 切到图鉴，", Ansi.S(Style.Gray))
                .Add("Enter", Ansi.S(Style.Bold + Style.BrightGreen))
                .Add(" 阅读", Ansi.S(Style.Gray));
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

    private static string BuildRightTitle(
        GameSession session, GameSnapshot snap, PanelFocus panel, int start, int count, int total) => panel switch
        {
            PanelFocus.Achievements => BuildAchievementTitle(snap, start, count, total),
            PanelFocus.Codex => BuildCodexTitle(snap, start, count, total),
            PanelFocus.Choices => BuildChoiceTitle(session, start, count, total),
            _ => BuildUpgradeTitle(session.Upgrades.Count, start, count, total),
        };

    private static string BuildChoiceTitle(GameSession session, int start, int count, int total)
    {
        if (!session.HasStances && session.ChoiceRows.Count == 0) return " 表态  （本内容包没有表态机制）";

        int pending = session.Snapshot.PendingChoices.Count;
        if (pending == 0) return $" 表态  （没有待答）{Scroll(start, count, total)}";

        // "过了这层就遇不到"不是吓唬：选择挂了 EraId，是硬门。
        // 动作名用各包自己的叫法（舍命 / 开新批次 / 重组），别把九命的词焊到别的包上。
        return $" 表态  {pending} 项待答 !{session.Package.PrestigeActionName}后不再{Scroll(start, count, total)}";
    }

    private static string BuildCodexTitle(GameSnapshot snap, int start, int count, int total)
        => snap.Codex is { } codex
            ? $" 图鉴  {codex.TotalUnlocked}/{codex.TotalEntries}{Scroll(start, count, total)}"
            : " 图鉴  （本内容包没有剧情）";

    private static string Scroll(int start, int count, int total)
    {
        if (total <= count) return string.Empty;
        string up = start > 0 ? "^" : string.Empty;
        string down = start + count < total ? "v" : string.Empty;
        return $"  {up}{down} {start + 1}-{start + count}";
    }

    // ---------------------------------------------------------------- 列表行

    private static void BuildingRow(UiLine line, GameSession session, BuildingView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Buildings && index == session.Selected;

        string name = view.IsUnlocked ? view.Name : $"{view.Name}（未解锁）";
        string price = view.IsUnlocked ? NumFormat.Format(view.UnitPrice, NumberStyle.Short) : "—";

        // 结构：空格 > 1 🐈 名称(可变) 数量(4) 单价(9) 尾空格 —— 合计 nameWidth + 23 列
        int nameWidth = Math.Max(6, width - 23);
        string row = $" {(selected ? '>' : ' ')}{Slot(index)} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(view.Owned.ToString(), 4)} {Ansi.PadLeft(price, 9)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : !view.IsUnlocked || !view.CanAfford ? Ansi.S(Style.Gray) : string.Empty;

        line.Add(row, style);
    }

    private static void UpgradeRow(UiLine line, GameSession session, UpgradeView view, int index, int width)
    {        bool selected = session.Focus == PanelFocus.Upgrades && index == session.Selected;

        string currency = view.Currency == UpgradeCurrency.PrestigeChips
            ? session.Snapshot.PrestigeCurrencyIcon
            : session.Snapshot.CurrencyIcon;
        string price = NumFormat.Format(view.Price, NumberStyle.Short) + currency;
        string name = view.Owned > 0 ? $"{view.Name} ×{view.Owned}" : view.Name;

        // 结构：空格 > 1 🐈 名称(可变) 价格(10) 尾空格 —— 合计 nameWidth + 19 列
        int nameWidth = Math.Max(6, width - 19);
        string row = $" {(selected ? '>' : ' ')}{Slot(index)} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(price, 10)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : view.CanAfford ? Ansi.S(Style.BrightCyan) : Ansi.S(Style.Gray);

        line.Add(row, style);
    }

    /// <summary>表态面板的一行 = 某次待答选择的一个选项。</summary>
    private static void ChoiceOptionRow(UiLine line, GameSession session, ChoiceRow row, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Choices && index == session.Selected;
        ChoiceOptionView option = row.Option;

        string stance = option.StanceName.Length > 0
            ? $"{option.StanceIcon}{option.StanceName}+{option.Weight}"
            : "中立";

        // 结构：空格 > 1 标签(可变) 立场(约 6~12) 尾空格
        int labelWidth = Math.Max(6, width - 8 - Ansi.DisplayWidth(stance));
        string text = $" {(selected ? '>' : ' ')}{Slot(index)} " +
                      $"{Ansi.PadRight(Ansi.Truncate(option.Label, labelWidth), labelWidth)} {stance}";

        line.Add(text, selected ? Ansi.S(Style.Inverse) : Ansi.S(Style.BrightYellow));
    }

    private static void AchievementRow(UiLine line, GameSession session, AchievementView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Achievements && index == session.Selected;

        // 结构：空格 > ✓ 🐈 名称(可变) 进度(可变) 尾空格 —— 合计 nameWidth + progressWidth + 10 列
        int progressWidth = Math.Clamp(width / 4, 0, 12);
        int nameWidth = Math.Max(6, width - 10 - progressWidth);
        string row = $" {(selected ? '>' : ' ')}{(view.Unlocked ? 'v' : '-')} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(view.Name, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(Ansi.Truncate(view.ProgressText, progressWidth), progressWidth)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : view.Unlocked ? Ansi.S(Style.Green) : Ansi.S(Style.Gray);

        line.Add(row, style);
    }

    private static string Slot(int index) => index < SlotKeys.Length ? SlotKeys[index] : "+";

    /// <summary>图鉴的一行：已解锁显示剧情线名，未解锁显示条件进度。</summary>
    private static void CodexRow(UiLine line, GameSession session, LoreView view, int index, int width)
    {
        bool selected = session.Focus == PanelFocus.Codex && index == session.Selected;

        int tailWidth = Math.Clamp(width / 3, 0, 14);
        int nameWidth = Math.Max(6, width - 8 - tailWidth);
        // 未解锁时用百分比而不是 "16,276,467 / 20,000,000"——那一格放不下，
        // 完整条件留给下方的详情行。
        string tail = view.Unlocked ? view.StorylineName : NumFormat.Percent(view.Progress, 0);

        string row = $" {(selected ? '>' : ' ')}{(view.Unlocked ? 'v' : '-')} {view.Icon} " +
                     $"{Ansi.PadRight(Ansi.Truncate(view.Title, nameWidth), nameWidth)} " +
                     $"{Ansi.PadLeft(Ansi.Truncate(tail, tailWidth), tailWidth)}";

        string style = selected
            ? Ansi.S(Style.Inverse)
            : view.Unlocked ? Ansi.S(Style.Green) : Ansi.S(Style.Gray);

        line.Add(row, style);
    }

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

            case PanelFocus.Codex when session.Selected < session.Codex.Count:
            {
                LoreView view = session.Codex[session.Selected];

                if (!view.Unlocked)
                {
                    return line.Add("🔒 ", Ansi.S(Style.Gray))
                        .Add($"还没读到：{view.RevealHint}", Ansi.S(Style.Yellow))
                        .Add(view.ProgressText.Length > 0 ? $"　进度 {view.ProgressText}" : string.Empty, Ansi.S(Style.Gray));
                }

                line.Add($"{view.Icon} {view.Title}", Ansi.S(Style.Bold))
                    .Add($"　【{view.StorylineName}】", Ansi.S(Style.Magenta))
                    .Add($"　{view.Body}", Ansi.S(Style.Gray));
                return line;
            }

            case PanelFocus.Choices when session.ChoiceRows.Count > 0:
            {
                ChoiceRow row = session.ChoiceRows[Math.Clamp(session.Selected, 0, session.ChoiceRows.Count - 1)];

                line.Add($"🗣 {row.Choice.Speaker}：{row.Choice.Prompt}", Ansi.S(Style.Bold))
                    .Add("　选：", Ansi.S(Style.Gray));

                for (int i = 0; i < row.Choice.Options.Count; i++)
                {
                    ChoiceOptionView option = row.Choice.Options[i];
                    if (i > 0) line.Add("｜", Ansi.S(Style.Gray));

                    line.Add(option.Label, Ansi.S(Style.BrightYellow));
                    if (option.StanceName.Length > 0)
                    {
                        line.Add($" +{option.StanceName}{option.Weight}", Ansi.S(Style.Magenta));
                    }
                    if (option.EffectSummary.Length > 0)
                    {
                        line.Add($" {option.EffectSummary}", Ansi.S(Style.Cyan));
                    }
                }

                return line;
            }

            case PanelFocus.Choices:
                // 没有待答时，这一行改用来展示立场轴与结局——那是这个面板的另一半用处。
                return line.Add(StanceAxis(snap), Ansi.S(Style.Gray));

            default:
                _ = snap;
                return line.Add(StanceAxis(snap), Ansi.S(Style.Gray));
        }
    }

    /// <summary>立场轴一行文本；没有立场轴时给出通用操作提示。</summary>
    private static string StanceAxis(GameSnapshot snap)
    {
        if (snap.Stances is not { } stances) return "按 Tab 切换面板，^v 选择，Enter 执行。";

        var text = new System.Text.StringBuilder("⚖️ 立场　");
        foreach (StanceView stance in stances)
        {
            text.Append(stance.Icon).Append(stance.Name).Append(' ').Append(stance.Weight);
            if (stance.IsDominant) text.Append(">");
            text.Append("　");
        }

        if (snap.Ending is { } ending)
        {
            text.Append("｜ ").Append(ending.Icon).Append(" 结局「").Append(ending.Name).Append("」：").Append(ending.Text);
        }
        else
        {
            text.Append("｜ 主导立场决定产量加成；每次都选同一条才会成为主导。");
        }

        return text.ToString();
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
            ("Enter", session.Focus == PanelFocus.Choices ? "作答" : "执行"),
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
            "    ^ / v       移动选择    1-9 / 0 直接跳到第 1~10 项",
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

    /// <summary>
    /// 窗口小于 <see cref="MinWidth"/>×<see cref="MinHeight"/> 时的兜底帧。<para>
    /// 只有第一行有提示，其余填空；每一行都<b>恰好</b> width 列、总行数<b>恰好</b> height 行。
    /// 兜底帧自己也必须守这两条不变量——否则小窗口里照样滚屏，兜底就成了另一种坏。
    /// </para>
    /// </summary>
    private static List<string> TooSmallFrame(int width, int height)
    {
        string hint = $" 终端太小：{width}×{height}（至少 {MinWidth}×{MinHeight}）";
        var lines = new List<string>(height);

        for (int i = 0; i < height; i++)
        {
            string text = i == 0 ? Ansi.Truncate(hint, width) : string.Empty;
            lines.Add(Ansi.PadRight(text, width));
        }

        return lines;
    }

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
