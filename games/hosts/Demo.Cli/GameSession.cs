using System.Text;
using NekoClicker.Core;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Persistence;
using NekoClicker.Core.Views;
using NekoClicker.Hosts;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 一笔正在等玩家输入的"窗口"。<para>
/// <b>终端里的"导出/导入窗口"就是这个。</b>这是一个逐帧重绘的字符界面，没有弹窗、
/// 没有文本框；诚实的等价物是<b>一次文件路径提示</b>：一行提示 + 一个可编辑的路径。
/// （方案：<c>engine/docs/SAVE_TRANSFER_PLAN.md</c> §4.2。）
/// </para>
/// </summary>
internal enum TransferPromptKind
{
    /// <summary>把当前存档导出到某个文件。</summary>
    Export,

    /// <summary>从某个文件导入一份导出文本。</summary>
    Import,
}

/// <summary>
/// 一次路径提示的可编辑状态：<b>哪种操作</b> + <b>此刻那一行里写着什么</b>。<para>
/// 它刻意不是一个"输入框控件"：终端这边按键是宿主读的（<see cref="InteractiveLoop.HandleKey"/>），
/// 会话只负责"往这一行里加一个字 / 删一个字 / 清空 / 确认 / 取消"。
/// 于是提示的**内容**可以脱离终端被测试，而按键与终端的耦合留在宿主那一侧。
/// </para>
/// </summary>
internal sealed class TransferPrompt
{
    /// <summary>开一次提示。</summary>
    /// <param name="kind">操作种类。</param>
    /// <param name="path">预填的路径（玩家可以直接回车，也可以先编辑）。</param>
    public TransferPrompt(TransferPromptKind kind, string path)
    {
        Kind = kind;
        Text = path;
    }

    /// <summary>操作种类。</summary>
    public TransferPromptKind Kind { get; }

    /// <summary>这一行里现在写着的路径。</summary>
    public string Text { get; private set; }

    /// <summary>追加一个字符；控制字符（回车 / Esc / 退格那些）一律不进来。</summary>
    /// <returns>真的加进去了为 <c>true</c>。</returns>
    public bool Append(char c)
    {
        if (char.IsControl(c)) return false;
        Text += c;
        return true;
    }

    /// <summary>删掉最后一个字符（已是空行时什么都不做）。</summary>
    public void Backspace()
    {
        if (Text.Length > 0) Text = Text[..^1];
    }

    /// <summary>清空（Ctrl+U）。换一个文件时比按二十次退格现实。</summary>
    public void Clear() => Text = string.Empty;
}

/// <summary>当前获得键盘焦点的面板。</summary>
internal enum PanelFocus
{
    /// <summary>建筑面板。</summary>
    Buildings,

    /// <summary>升级面板。</summary>
    Upgrades,

    /// <summary>成就面板。</summary>
    Achievements,

    /// <summary>图鉴（叙事条目）面板；内容包没有叙事时该面板为空。</summary>
    Codex,

    /// <summary>表态（待答选择与立场）面板；内容包没有选择时该面板为空。</summary>
    Choices,
}

/// <summary>表态面板的一行：某次待答选择的一个选项。</summary>
/// <param name="Choice">所属选择。</param>
/// <param name="Option">该选项。</param>
internal readonly record struct ChoiceRow(ChoiceView Choice, ChoiceOptionView Option);

/// <summary>
/// 会话：把引擎、存档和"界面状态"绑在一起。<para>
/// 所有玩家输入最终都变成对 <see cref="GameEngine"/> 的一次调用，会话自己<b>不做任何规则判断</b>——
/// 买不起、没解锁、已买满这些都由引擎返回结果，会话只负责把消息显示出来。
/// 这正是框架分层的价值：换成 Web 前端时，这一层的逻辑可以整段复用。
/// </para>
/// </summary>
internal sealed class GameSession : IDisposable
{
    private const int MaxLogLines = 200;

    private static readonly string[] BuildingKeys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    private readonly List<GameNotification> _log = [];
    private readonly IDisposable _notificationSubscription;
    private readonly IDisposable _choiceSubscription;
    private readonly IDisposable _stanceSubscription;
    private readonly IDisposable _endingSubscription;
    private readonly ChoiceLatencyLog _latency;
    private GameSnapshot _snapshot = null!;
    private BuildingView[] _buildingCache = [];
    private UpgradeView[] _upgradeCache = [];
    private AchievementView[] _achievementCache = [];
    private LoreView[] _codexCache = [];
    private ChoiceRow[] _choiceCache = [];

    /// <summary>创建会话。</summary>
    /// <param name="package">要玩的内容包（决定构建哪份 <c>GameContent</c>）。</param>
    /// <param name="savePath">存档路径；<c>null</c> 表示不落盘。</param>
    /// <param name="seed">随机种子。</param>
    /// <param name="endingGraceSeconds">终局宽限期（模拟秒）；<c>null</c> 表示用框架默认值。
    /// 该参数 <b>1.5.0 起已退役</b>（落定条件改成了条件：现在是"还有答得上的待答表态就不落定"，
    /// 见 <see cref="GameEngine.CheckEnding"/>），传非 <c>null</c> 只会让引擎发一条警告；
    /// 保留形参是为了不打断已有的调用与脚本。</param>
    /// <param name="latencyLogPath">作答延迟埋点文件路径（<b>只追加</b>）；<c>null</c> 表示不落盘。
    /// 默认值是 <c>null</c>（不是"仓库里的 artifacts"）是刻意的：**写不写真人数据文件必须由调用方
    /// 明确决定**——否则测试、单帧渲染、机器人跑一局都会往那份文件里塞行。真人玩的路径由
    /// <see cref="FromOptions"/>（<c>humanPlay: true</c>）接上。</param>
    public GameSession(
        ContentPackage package,
        string? savePath,
        ulong seed,
        double? endingGraceSeconds = null,
        string? latencyLogPath = null)
    {
        Package = package;
        SavePath = savePath;

        Engine = new GameEngine(package.Build(), new GameEngineOptions
        {
            Clock = SystemClock.Instance,
            Seed = seed,
            GrantOfflineProgress = true,
            MaxNotifications = 64,
            EndingGraceSeconds = endingGraceSeconds,
        });

        _notificationSubscription = Engine.Events.Subscribe<NotificationEvent>(OnNotification);

        // 这三件事都很稀有、而且玩家错过就没了，所以直接进日志，不靠面板自己去发现。
        _choiceSubscription = Engine.Events.Subscribe<ChoiceTriggeredEvent>(OnChoiceTriggered);
        _stanceSubscription = Engine.Events.Subscribe<DominantStanceChangedEvent>(OnDominantStanceChanged);
        _endingSubscription = Engine.Events.Subscribe<EndingReachedEvent>(OnEndingReached);

        // 作答延迟的量测：宽限期该定多少秒，只能由真人的实测数据回答。
        // 用的就是 Web 宿主那一份实现（共享源码），所以两个宿主写出来的行格式不可能分叉。
        _latency = new ChoiceLatencyLog(Engine, package.Id, LatencyLogFormat.CliHost, latencyLogPath);
        _latency.Answered += sample =>
            Log($"⏱ 表态「{sample.ChoiceId}」你想了 {NumFormat.Duration(sample.SimulatedSeconds)}才答。", "⏱");
        _latency.Unmeasured += (choiceId, reason) =>
            Log($"⏱ 表态「{choiceId}」这次量不出时长：{reason}。", "⏱");

        if (savePath is not null)
        {
            Storage = new FileStorage(Path.GetDirectoryName(Path.GetFullPath(savePath)) ?? ".");
            // PackId 由宿主告诉 SaveManager（**不许从文件名反推**，见 SaveManager.PackId 的注释）：
            // 这是导入时"拒绝别的包的存档"唯一判据。
            Saves = new SaveManager(Engine, Storage, Path.GetFileName(savePath)) { PackId = package.Id };
        }

        if (Saves is not null && Saves.HasSave() && Saves.Load())
        {
            Log($"已读取存档：{savePath}", "📂");
            if (Saves.LastOfflineProgress is { CookiesGained: > 0 } offline)
                Log($"离线 {NumFormat.Duration(offline.CreditedSeconds)}，店里替你赚了 {NumFormat.FormatLong(offline.CookiesGained)} 条{Engine.Content.CurrencyName}。", "🌙");
        }
        else
        {
            Log(package.Welcome, "🐱");
        }

        // 埋点写到哪去了必须当场说清楚，而不是等玩家去翻文档：这份文件是"真人从看到表态到
        // 作答有多久"唯一的来源，而 Web 宿主早就在启动时报出路径了（终端不报，就等于
        // "埋点在跑"与"埋点没接线"在现象上都是"没有数据"）。
        if (_latency.FilePath is { } probePath)
            Log($"作答延迟埋点：{probePath}（只追加；只有真人作答才会出现数据行）", "📏");

        RefreshCache();
    }

    /// <summary>
    /// 按命令行选项开会话——<b>两个运行模式都走这一条</b>。<para>
    /// <paramref name="humanPlay"/> 决定要不要把埋点接上那个真实文件：交互模式（默认启动器跑的
    /// 就是它）是<b>真人</b>在玩，样本是这份文件唯一的合法来源；而无头模拟是<b>机器人</b>作答，
    /// 它的样本不是人类数据，默认不许写进去（要写必须显式 <c>--latency-log</c>）。
    /// </para>
    /// <para>
    /// 之所以把这件事收成一个工厂而不是两处各写一遍 <c>new GameSession(...)</c>：
    /// §S13 那个缺陷的形态正是"默认模式那条路忘了把埋点接上文件"——两处各写一遍时，
    /// 没有任何测试或守卫能看见这条差别。收成一处之后，"默认模式接没接上"变成可以断言的事实。
    /// </para>
    /// </summary>
    public static GameSession FromOptions(CliOptions options, bool humanPlay)
        => new(
            options.Package,
            options.SavePath,
            options.Seed,
            options.EndingGraceSeconds,
            humanPlay || options.LatencyLogExplicit ? options.LatencyLogPath : null);

    /// <summary>当前内容包。</summary>
    public ContentPackage Package { get; }

    /// <summary>本次运行的存档文件路径（<c>null</c> = 不落盘）。导出提示的默认路径由它推出来。</summary>
    public string? SavePath { get; }

    /// <summary>引擎。</summary>
    public GameEngine Engine { get; }

    /// <summary>存储介质（未启用存档时为 <c>null</c>）。</summary>
    public IStorage? Storage { get; }

    /// <summary>存档管理器（未启用存档时为 <c>null</c>）。</summary>
    public SaveManager? Saves { get; }

    /// <summary>当前批量档位。</summary>
    public PurchaseMode Mode { get; private set; } = PurchaseMode.Buy1;

    /// <summary>当前焦点面板。</summary>
    public PanelFocus Focus { get; private set; } = PanelFocus.Buildings;

    /// <summary>当前选中行。</summary>
    public int Selected { get; private set; }

    /// <summary>待确认的转生 / 店休。</summary>
    public bool AwaitingAscendConfirm { get; private set; }

    /// <summary>是否显示帮助浮层。</summary>
    public bool ShowHelp { get; private set; }

    /// <summary>已退出标记。</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>退出前那次存档的错误；没失败则为 <c>null</c>。</summary>
    public Exception? ExitSaveError { get; private set; }

    /// <summary>最近一帧的快照（渲染与选择都基于它，避免每帧重复生成）。</summary>
    public GameSnapshot Snapshot => _snapshot;

    /// <summary>最近的消息（新的在后）。</summary>
    public IReadOnlyList<GameNotification> LogLines => _log;

    /// <summary>推进一帧。</summary>
    public void Update(double deltaSeconds)
    {
        Engine.Update(deltaSeconds);
        RefreshCache();
    }

    /// <summary>
    /// 重新生成快照与列表缓存。<para>
    /// 直接操作引擎（例如无头模拟）之后必须调用它，否则界面读到的还是上一帧的缓存。
    /// </para>
    /// </summary>
    public void Refresh() => RefreshCache();

    // ---------------------------------------------------------------- 输入

    /// <summary>切换焦点面板。</summary>
    public void CycleFocus()
    {
        Focus = Focus switch
        {
            PanelFocus.Buildings => PanelFocus.Upgrades,
            PanelFocus.Upgrades => PanelFocus.Achievements,
            PanelFocus.Achievements => PanelFocus.Codex,
            PanelFocus.Codex => PanelFocus.Choices,
            _ => PanelFocus.Buildings,
        };
        Selected = 0;
        RefreshCache();
    }

    /// <summary>直接指定焦点面板（用于 <c>--frame --panel codex</c> 这类验证场景）。</summary>
    /// <param name="focus">目标面板。</param>
    public void SetFocus(PanelFocus focus)
    {
        Focus = focus;
        Selected = 0;
        RefreshCache();
    }

    /// <summary>移动选择。</summary>
    public void MoveSelection(int delta)
    {
        int count = RowCount;
        if (count == 0)
        {
            Selected = 0;
            return;
        }
        Selected = ((Selected + delta) % count + count) % count;
    }

    /// <summary>按序号直接选中（<c>-1</c> 表示无效）。</summary>
    public void SelectByKey(char key)
    {
        int index = Array.IndexOf(BuildingKeys, key.ToString());
        if (index < 0 || index >= RowCount) return;
        Selected = index;
    }

    /// <summary>手动点击（撸猫 / 做咖啡）。</summary>
    public void Click()
    {
        ClickResult result = Engine.Click();
        // 点击很频繁，不写日志；数字本身在顶栏跳动就够了。
        _ = result;
    }

    /// <summary>抓住第一只金猫。</summary>
    public void GrabGoldenCookie()
    {
        if (Engine.State.GoldenCookies.Count == 0)
        {
            double min = Engine.Content.Balance.GoldenCookieMinDelay;
            double max = Engine.Content.Balance.GoldenCookieMaxDelay;
            Log(
                $"现在没有{Package.GoldenCookieName}。它们每 {NumFormat.Duration(min)}~{NumFormat.Duration(max)} 出现一次，出现时顶部会有提示。",
                "🌟");
            return;
        }

        string id = Engine.State.GoldenCookies[0].InstanceId;
        GoldenCookieResult result = Engine.ClickGoldenCookie(id);
        Log(result.Message, result.Success ? result.Icon : "!");
    }

    /// <summary>切换批量档位。</summary>
    public void CycleMode()
    {
        if (Mode.IsSell())
        {
            Mode = Mode switch
            {
                PurchaseMode.Sell1 => PurchaseMode.Sell10,
                PurchaseMode.Sell10 => PurchaseMode.SellMax,
                _ => PurchaseMode.Sell1,
            };
        }
        else
        {
            Mode = Mode.NextBuy();
        }
        RefreshCache();
    }

    /// <summary>在买/卖之间切换。</summary>
    public void ToggleSell()
    {
        Mode = Mode.ToggleSell();
        RefreshCache();
    }

    /// <summary>执行当前选中项。</summary>
    public void Activate()
    {
        if (AwaitingAscendConfirm)
        {
            Ascend();
            return;
        }

        switch (Focus)
        {
            case PanelFocus.Buildings:
                ActivateBuilding(Selected);
                break;
            case PanelFocus.Upgrades:
                ActivateUpgrade(Selected);
                break;
            case PanelFocus.Codex:
                ActivateLore(Selected);
                break;
            case PanelFocus.Choices:
                AnswerChoice(Selected);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 在图鉴里"读"一条：如果是待处理的弹窗就点掉它，已解锁的就在日志里重读一遍。
    /// </summary>
    /// <param name="index">条目下标。</param>
    public void ActivateLore(int index)
    {
        if (index < 0 || index >= _codexCache.Length) return;

        LoreView entry = _codexCache[index];

        if (Engine.DismissLorePopup(entry.Id))
        {
            Log($"「{entry.Title}」", entry.Icon);
            RefreshCache();
            return;
        }

        if (entry.Unlocked) Log($"「{entry.Title}」{entry.Body}", entry.Icon);
        else Log($"还没读到这一段。条件：{entry.RevealHint}", "🔒");
    }

    /// <summary>
    /// 作答表态面板里的一行。<para>
    /// 作答是<b>一次性</b>的：同一个选择答过之后就不再出现在面板里，
    /// 所以面板里每一行都是"还没答过的选项"。
    /// </para>
    /// </summary>
    /// <param name="index">行下标。</param>
    public void AnswerChoice(int index)
    {
        if (index < 0 || index >= _choiceCache.Length) return;

        ChoiceRow row = _choiceCache[index];
        if (!Engine.AnswerChoice(row.Choice.Id, row.Option.Id))
        {
            Log("这次表态已经不成立了。", "!");
            RefreshCache();
            return;
        }

        // 结果文本刻意不放进视图：作答前不该剧透后果。答完再从引擎取回来。
        string outcome = ChoiceSystem.AnswerOf(Engine.Content, Engine.State, row.Choice.Id)?.OutcomeText ?? string.Empty;
        Log($"{row.Option.Label}{outcome}", "🗣");
        RefreshCache();
    }

    /// <summary>
    /// 报告"待答表态已经画到屏幕上了"（见 <see cref="GameEngine.MarkPendingChoicesShown"/>）。<para>
    /// 由 <see cref="InteractiveLoop"/> 在<b>真的画出</b>「表态」面板的那一帧调用；
    /// 会话只负责转发，"这一帧到底画没画出表态"由渲染器回答
    /// （<see cref="TerminalUi.ShowsChoicesPanel"/>）——宿主这边再写一份近似，焦点规则、
    /// 过小窗口、帮助浮层任何一处变了自己这份就会悄悄过时。
    /// </para>
    /// <para>
    /// <b>1.6.0 起它是诊断信号，不再是落定条件</b>：结局等的是玩家把表态<b>答完</b>。
    /// 这份记录回答的是"这条表态到底露过面没有"——那正是"结局为什么一直没来"的两种
    /// 完全不同的原因之一（另一种是"看到了却一直没答"）。
    /// </para>
    /// <para>
    /// <b>状态栏那行「🗣 N 项待答」不算</b>：它只说有几项在等，选择本身（谁在问、问什么、
    /// 有哪些选项）只有「表态」面板里才看得到。所以"看到角标"不等于"看到表态"。
    /// </para>
    /// </summary>
    /// <returns>本次新标记的条数（幂等：都标记过时为 <c>0</c>）。</returns>
    public int MarkChoicesShown() => Engine.MarkPendingChoicesShown();

    /// <summary>执行指定序号的建筑（供数字快捷键使用）。</summary>
    /// <param name="index">建筑列表下标。</param>
    public void ActivateBuilding(int index)
    {
        if (index < 0 || index >= _buildingCache.Length) return;

        BuildingView view = _buildingCache[index];
        bool selling = Mode.IsSell();
        int amount = Mode.RequestedAmount();

        PurchaseResult result = selling
            ? Engine.SellBuilding(view.Id, amount)
            : Engine.BuyBuilding(view.Id, amount);

        Log(result.Message, result.Success ? (selling ? "💸" : "🛒") : "!");
        RefreshCache();
    }

    /// <summary>执行指定序号的升级。</summary>
    /// <param name="index">升级列表下标。</param>
    public void ActivateUpgrade(int index)
    {
        if (index < 0 || index >= _upgradeCache.Length) return;

        UpgradeView view = _upgradeCache[index];
        PurchaseResult result = Engine.BuyUpgrade(view.Id);
        Log(result.Message, result.Success ? "⬆" : "!");
        RefreshCache();
    }

    /// <summary>请求转生（首次调用进入确认态）。</summary>
    public void RequestAscend()
    {
        if (AwaitingAscendConfirm)
        {
            Ascend();
            return;
        }

        PrestigePreview preview = PrestigeSystem.Preview(Engine);
        if (!preview.CanAscend)
        {
            Log(
                $"还不能{Package.PrestigeActionName}：历史累计 {NumFormat.FormatLong(Engine.State.CookiesEarnedAllTime)} / " +
                $"{NumFormat.FormatLong(preview.CookiesForNextLevel)}。",
                "!");
            return;
        }

        AwaitingAscendConfirm = true;
        Log(
            $"确认{Package.PrestigeActionName}？将清空本轮进度，换取 {NumFormat.FormatLong(preview.ChipsOnAscend)} {Engine.Content.PrestigeCurrencyName}。按 Y 确认，其他键取消。",
            "🌿");
    }

    /// <summary>执行转生。</summary>
    public void Ascend()
    {
        AwaitingAscendConfirm = false;
        AscensionResult result = Engine.Ascend();
        Log(result.Message, result.Success ? "🌿" : "!");
        RefreshCache();
    }

    /// <summary>取消待确认状态。</summary>
    public void CancelPending() => AwaitingAscendConfirm = false;

    /// <summary>切换帮助浮层。</summary>
    public void ToggleHelp() => ShowHelp = !ShowHelp;

    /// <summary>手动存档。</summary>
    public void Save()
    {
        if (Saves is null)
        {
            Log("本次运行未启用存档（--no-save）。", "💾");
            return;
        }

        Log(Saves.Save() ? "已存档。" : $"存档失败：{Saves.LastError?.Message}", "💾");
    }

    /// <summary>请求退出。</summary>
    public void Quit()
    {
        // 退出前这一次存档失败必须被记下来、并且**在离开备用屏之后再喊一次**：
        // 循环下一轮就结束了，界面日志面板不会被重画，所以只往 _log 里塞一条等于静默
        // （备用屏退出时那行还会被整屏丢掉）。谁来喊见 <c>InteractiveLoop.Run</c> 的收尾。
        if (Saves is not null && !Saves.Save()) ExitSaveError = Saves.LastError;
        QuitRequested = true;
    }

    // ---------------------------------------------------------------- 导出 / 导入
    //
    // 终端没有弹窗，所以这两件事的"窗口"= **一次文件路径提示**（SAVE_TRANSFER_PLAN §4.2）。
    // 两边都走**同一个** SaveManager.Export/Import —— 终端绝不自己解析一遍导出文本：
    // "两个宿主两套语义"正是这一节要避免的事（Web 那边也是同一个入口）。

    /// <summary>此刻正在等玩家输入的提示；没有则为 <c>null</c>。</summary>
    public TransferPrompt? Prompt { get; private set; }

    /// <summary>开一次导出提示（<c>E</c> 键）。</summary>
    public void BeginExportPrompt() => BeginPrompt(TransferPromptKind.Export);

    /// <summary>开一次导入提示（<c>I</c> 键）。</summary>
    public void BeginImportPrompt() => BeginPrompt(TransferPromptKind.Import);

    /// <summary>往路径那一行追加一个字符。</summary>
    /// <param name="c">按键字符。</param>
    public void PromptAppend(char c) => Prompt?.Append(c);

    /// <summary>删掉路径最后一个字符。</summary>
    public void PromptBackspace() => Prompt?.Backspace();

    /// <summary>清空路径（Ctrl+U）。换一个文件时比按二十次退格现实。</summary>
    public void PromptClear() => Prompt?.Clear();

    /// <summary>取消这次提示（Esc）：什么都不做，什么都不写。</summary>
    public void CancelPrompt()
    {
        if (Prompt is null) return;
        Prompt = null;
        Log("已取消。", "·");
    }

    /// <summary>
    /// 确认这次提示（Enter）：导出写文件，或导入读文件。<para>
    /// 提示**先收掉再干活**：写文件可能慢、可能抛，而"提示去不掉"会让玩家以为卡住了。
    /// </para>
    /// </summary>
    public void AcceptPrompt()
    {
        if (Prompt is not { } prompt) return;

        string path = prompt.Text.Trim();
        TransferPromptKind kind = prompt.Kind;
        Prompt = null;

        if (Saves is null)
        {
            // 不落盘的会话没有槽位可导、也没有槽位可导进：明说，而不是静默什么都不做。
            Log("本次运行未启用存档（--no-save），导出与导入都做不了。", "!");
            return;
        }

        if (path.Length == 0)
        {
            Log("没有给路径。按 E / I 重新来一次。", "!");
            return;
        }

        if (kind == TransferPromptKind.Export) ExportTo(path);
        else ImportFrom(path);
    }

    /// <summary>
    /// 把当前存档导出成一个文件。<para>
    /// <b>为什么不走 <see cref="IStorage"/></b>：那三道闸（先写 .tmp → 反解一遍 → 原子替换 + .bak）
    /// 是**存档槽位**的保护，而导出物是另存的一份文本、不是槽位本身。真正的保护已经由
    /// <see cref="SaveManager.Export"/> 保证（状态读得出来才导得出），这里只需要"写不进去要响"。
    /// </para>
    /// </summary>
    /// <param name="path">目标文件路径（相对路径按当前工作目录）。</param>
    /// <returns>写入的完整路径；失败为 <c>null</c>。</returns>
    public string? ExportTo(string path)
    {
        if (Saves is null)
        {
            Log("本次运行未启用存档（--no-save），导出做不了。", "!");
            return null;
        }

        try
        {
            string text = Saves.Export();
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");

            // **不带 BOM**：这一行是导出格式的一部分，不是口味。导出文本的下一站是"被贴进
            // 另一个宿主的文本框"或"被别的工具读"——开头多一个 U+FEFF，JSON 解析会在
            // 第一个字符上就失败，而报出来的那句话（"连合法 JSON 都不是"）完全指不到 BOM 上。
            File.WriteAllText(full, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Log($"已导出到 {full}（{text.Length} 个字符）。整份复制走就能在别处导入。", "📤");
            return full;
        }
        catch (Exception ex)
        {
            // 写不进去（路径不存在 / 没权限 / 磁盘满）必须响亮：玩家会以为导出成功了，
            // 然后在另一台机器上发现文件不在——那是"报成功却什么都没发生"的经典形状。
            Log($"导出失败：{ex.Message}", "!");
            return null;
        }
    }

    /// <summary>
    /// 从一个文件导入一份导出文本。<b>校验与落盘全在 <see cref="SaveManager.Import"/> 里</b>
    /// （七道闸都在碰磁盘之前），这里只负责读文件与播报结果。
    /// </summary>
    /// <param name="path">源文件路径。</param>
    /// <returns>导入结果；文件都读不到时为 <c>null</c>。</returns>
    public SaveTransferResult? ImportFrom(string path)
    {
        if (Saves is null)
        {
            Log("本次运行未启用存档（--no-save），导入做不了。", "!");
            return null;
        }

        string text;
        try
        {
            // 刻意用 ReadAllText 的默认值（会自己剥掉 BOM）：别人用记事本存过的文件也读得进来。
            // 反过来说，**导出那一侧不许写 BOM** —— 见 ExportTo。
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Log($"读不到那个文件：{ex.Message}", "!");
            return null;
        }

        SaveTransferResult result = Saves.Import(text);
        // 成功与失败都**原样**播报引擎那句话：它已经写着"是哪个字段、期望什么、该怎么办"，
        // 宿主再翻译一遍只会两边慢慢说不到一起去（与 Web 宿主同一条约定）。
        Log(result.Message, result.Ok ? "📥" : "!");
        return result;
    }

    /// <summary>开一次提示，路径预填成默认值。</summary>
    private void BeginPrompt(TransferPromptKind kind) => Prompt = new TransferPrompt(kind, DefaultTransferPath());

    /// <summary>
    /// 导出/导入提示里的默认路径：**存档旁边**的 <c>&lt;包 id&gt;-export.json</c>。<para>
    /// 跟着存档走（而不是另起一个目录）：玩家自己传了 <c>--save</c> 时，导出物落在存档边上
    /// 才是可预期的地方。导入侧刻意用<b>同一个</b>默认值——最常见的用法就是"我刚导出，
    /// 现在想导回来"，于是回车即可；要换文件就先编辑这一行（Ctrl+U 清空重打）。
    /// </para>
    /// </summary>
    private string DefaultTransferPath()
    {
        string dir = SavePath is null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(SavePath)) ?? Directory.GetCurrentDirectory();

        return Path.Combine(dir, $"{Package.Id}-export.json");
    }

    // ---------------------------------------------------------------- 视图数据

    /// <summary>可见建筑（已解锁，或未解锁但不隐藏）。</summary>
    public IReadOnlyList<BuildingView> Buildings => _buildingCache;

    /// <summary>可购买升级（已解锁且未买满，按价格升序）。</summary>
    public IReadOnlyList<UpgradeView> Upgrades => _upgradeCache;

    /// <summary>可见成就。</summary>
    public IReadOnlyList<AchievementView> Achievements => _achievementCache;

    /// <summary>图鉴条目（按剧情线与序号排好）。</summary>
    public IReadOnlyList<LoreView> Codex => _codexCache;

    /// <summary>待表态的选项行（按选择触发顺序，每次选择的选项相邻）。</summary>
    public IReadOnlyList<ChoiceRow> ChoiceRows => _choiceCache;

    /// <summary>是否启用了立场轴。</summary>
    public bool HasStances => _snapshot.Stances is { Count: > 0 };

    /// <summary>当前焦点面板的行数。</summary>
    public int RowCount => Focus switch
    {
        PanelFocus.Buildings => _buildingCache.Length,
        PanelFocus.Upgrades => _upgradeCache.Length,
        PanelFocus.Codex => _codexCache.Length,
        PanelFocus.Choices => _choiceCache.Length,
        _ => _achievementCache.Length,
    };

    /// <summary>把引擎通知与会话日志写进统一的时间线。</summary>
    public void Log(string message, string icon = "·")
    {
        _log.Add(new GameNotification(message, icon, NotificationKind.Info, Engine.State.PlayTimeSeconds));
        while (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _notificationSubscription.Dispose();
        _choiceSubscription.Dispose();
        _stanceSubscription.Dispose();
        _endingSubscription.Dispose();
        _latency.Dispose();
        Saves?.Dispose();
    }

    private void OnNotification(NotificationEvent evt)
    {
        _log.Add(evt.Notification);
        while (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }

    /// <summary>作答延迟的汇总（宽限期该定多少秒的实测输入）。</summary>
    public string LatencySummary() => _latency.Summary();

    /// <summary>作答延迟埋点的落盘路径；本次不落盘时为 <c>null</c>。</summary>
    public string? LatencyLogPath => _latency.FilePath;

    private void OnChoiceTriggered(ChoiceTriggeredEvent evt)
        => Log($"{evt.Speaker}问你：「{evt.Prompt}」 —— 按 Tab 切到「表态」作答。", "🗣");

    private void OnEndingReached(EndingReachedEvent evt)
    {
        Log($"结局「{evt.Name}」：{evt.Text}", evt.Icon);

        // 终局是这次游玩唯一的"自然收尾"，把测量埋点攒到的样本在这儿交出来。
        Log(_latency.Summary(), "📏");
    }

    private void OnDominantStanceChanged(DominantStanceChangedEvent evt)
    {
        if (evt.CurrentStanceId is not { } id) return;
        if (Engine.Content.FindStance(id) is not { } stance) return;
        Log($"{stance.Name}开始主导。{stance.CostText}", stance.Icon);
    }

    private void RefreshCache()
    {
        _snapshot = Engine.Snapshot(Mode);

        _buildingCache = [.. _snapshot.Buildings.Where(b => b.IsVisible)];
        _upgradeCache = [.. _snapshot.Upgrades.Where(u => u.IsAvailable).OrderBy(u => u.Price)];
        _achievementCache = [.. _snapshot.Achievements.Where(a => a.Unlocked || !a.Hidden)];
        _codexCache = _snapshot.Codex is { } codex
            ? [.. codex.Storylines.SelectMany(s => s.Entries)]
            : [];

        // 行 = 待答选择的选项，按选择分组相邻排列（面板里一次表态的两个选项挨在一起）。
        _choiceCache =
        [
            .. _snapshot.PendingChoices.SelectMany(
                choice => choice.Options.Select(option => new ChoiceRow(choice, option))),
        ];

        int count = RowCount;
        if (Selected >= count) Selected = Math.Max(0, count - 1);
    }
}
