using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Persistence;
using NekoClicker.Demo.Cli;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 存档导出/导入的 <b>宿主接线</b>：两个宿主各一条路，验的是"引擎那道闸真的被接到了界面上"。<para>
/// 引擎侧（<c>SaveTransfer</c> + <c>SaveManager.Export/Import</c>）已经有 17 条用例
/// （<see cref="SaveTransferTests"/>）。这一份补的是它<b>外面</b>那一层，而那一层此前
/// <b>一条用例都没有</b>——那正是"界面上一个入口都没有"（缺口 D）的形状：引擎全绿，
/// 而玩家一个入口也没有。
/// </para>
/// <list type="number">
///   <item><b>Web 宿主</b>（<see cref="GameHost"/>）：包标识真的传给了 <c>SaveManager</c>
///         （<c>PackId = package.Id</c>）、<c>export</c> 真的带回一份信封文本、
///         导入真的<b>立刻</b>应用到当前会话、<b>坏输入真的一个字节都不写盘</b>。
///         这几条只能在这里验：HTTP 那一面（<c>tools/api-test.ps1</c>）验的是端点，
///         验不到"会话对象自己有没有把那个字段接上"。</item>
///   <item><b>终端宿主</b>（<see cref="GameSession"/>）：<c>E</c> / <c>I</c> 开出来的
///         路径提示就是它那边的「窗口」；提示开着时别的键不许抢键（<c>q</c> 是路径的一部分，
///         不是退出）；导出文件不带 BOM；导入走的是<b>同一个</b> <c>SaveManager.Import</c>。</item>
/// </list>
/// <para>
/// 全部用临时目录，跑完即删：仓库里的 <c>saves/</c> 是真人正在玩的进度，这些用例一个字节都不碰它。
/// 埋点也显式传 <c>latencyLogPath: null</c>——<c>artifacts/latency.txt</c> 同样是真人数据。
/// </para>
/// </summary>
public static class SaveTransferHostTests
{
    private const string ExportFileName = "neko-export.json";

    // ------------------------------------------------------------------ Web 宿主

    /// <summary>
    /// 导出必须自证身份，导入必须<b>立刻</b>换掉当前会话。<para>
    /// 「立刻」是有内容的：导入之后<b>不等下一帧</b>取快照，看到的就得是导入后的状态。
    /// 这不是顺手——导入换掉的是整局状态，而新客户端接入时拿的正是这份视图
    /// （见 <see cref="GameHost.ImportAsync"/> 里的 <c>Publish(forceFull: true)</c>）。
    /// </para>
    /// </summary>
    [Test]
    public static void WebHost_ExportNamesThePackageAndImportAppliesImmediately()
    {
        string rootA = NewTempDir();
        string rootB = NewTempDir();
        try
        {
            using var a = new WebHostScope(rootA);
            using var b = new WebHostScope(rootB);

            // 取一份**确定**的状态：TotalClicks 与「持有几座」都不随秒产量变，
            // 所以断言可以对到整数上，而不是「差不多」——引擎线程从构造那一刻就在跑，钱一直在涨。
            string buildingId = SetDeterministicState(a);

            CommandOutcome exported = a.Export();
            Check.True(exported.Ok, $"export 必须成功：{exported.Message}");
            Check.NotNull(exported.Text, "export 必须把文本带回（Text 字段）——那正是要复制走的东西。");

            var envelope = (JsonObject)JsonNode.Parse(exported.Text!)!;
            Check.Equal(SaveTransfer.FormatTag, envelope["Format"]!.GetValue<string>(), "导出文本要有信封头。");

            // 用 TryGetValue 读而不是 `!`：**接漏了的时候要红得读得懂**。写成 `envelope["PackId"]!`
            // 的话，少了包标识只会得到一句 NullReferenceException——那正是这条守卫要防的事，
            // 却正好说不清它防的是什么（实测：把接线摘掉就是这个形状）。
            string? declaredPack =
                envelope["PackId"] is JsonValue packValue && packValue.TryGetValue(out string? packId)
                    ? packId
                    : null;

            Check.Equal(
                "neko",
                declaredPack,
                "包标识必须真的接到了 SaveManager 上（GameHost.PackId = package.Id）："
                + "这一条红了，就等于「把咖啡馆的存档导进末世包」又拦不住了。");
            Check.Contains(exported.Message, "neko", "给人看的那句话里也要说出这是哪个包的导出物。");

            CommandOutcome imported = b.Import(exported.Text!);
            Check.True(imported.Ok, $"自己导出来的文本必须收得下：{imported.Message}");

            JsonObject view = b.View();
            Check.Equal(
                777.0,
                view["totalClicks"]!.GetValue<double>(),
                "导入之后**不等下一帧**取快照，就必须已经是导入后的状态。");
            Check.Equal(9, OwnedOf(view, buildingId), "建筑持有数也要换成导入的那一份。");
        }
        finally
        {
            Cleanup(rootA);
            Cleanup(rootB);
        }
    }

    /// <summary>
    /// 三种坏输入都必须<b>响亮地拒绝，而且磁盘上一个字节都不许动</b>。<para>
    /// 这是需求里「绝不损坏能用的存档」在<b>宿主这一层</b>的形态：引擎那 17 条验的是
    /// <c>SaveManager.Import</c> 自己；这一条验的是"经过 <c>GameHost</c> 与 HTTP 命令那条路
    /// 之后依然成立"（顺序、线程、以及宿主有没有在别处顺手写盘）。
    /// </para>
    /// </summary>
    [Test]
    public static void WebHost_BadImportsAreLoudAndLeaveTheSaveFileByteIdentical()
    {
        string root = NewTempDir();
        try
        {
            using var host = new WebHostScope(root);
            string buildingId = SetDeterministicState(host);

            Check.True(host.Save().Ok, "前置条件：先存一次盘，才有一份「能用的存档」可被弄坏。");

            string savePath = Path.Combine(root, "neko.json");
            byte[] before = File.ReadAllBytes(savePath);

            // 「内存里的会话」用**引擎自己的值**，不用快照：快照是每 250ms 推一次的一帧，
            // 拿它当基准会把"视图还没追上"读成"会话被改了"（这一条第一次跑就踩了这个坑）。
            double clicksBefore = host.EngineTotalClicks();
            Check.Equal(777.0, clicksBefore, "前提：状态已经灌进引擎了。");

            CommandOutcome exported = host.Export();
            Check.True(exported.Ok, $"导出失败就没法造下面这些坏输入了：{exported.Message}");

            // 三种坏输入，各自的类别由引擎判、宿主只负责把那句话原样带回来：
            string garbage = "这不是存档，只是一句话。";
            string tampered = exported.Text!.Replace("777", "778", StringComparison.Ordinal);
            string foreign = ForeignPack(exported.Text!, "cafe");

            foreach ((string what, string payload, string fragment) in new[]
                     {
                         ("粘贴了一句人话", garbage, "不是本游戏导出"),
                         ("把正文里一个数字改了", tampered, "校验和"),
                         ("把信封里的包标识改成了咖啡馆", foreign, "cafe"),
                     })
            {
                CommandOutcome result = host.Import(payload);

                Check.False(result.Ok, $"{what}：必须被拒绝，实际却收下了（{result.Message}）。");
                Check.Contains(result.Message, fragment, $"{what}：拒绝的理由要说清是哪一种（{result.Message}）。");
                Check.True(
                    File.ReadAllBytes(savePath).AsSpan().SequenceEqual(before),
                    $"{what}：被拒绝的导入不许动磁盘上那份能用的存档。");
                Check.Equal(
                    clicksBefore,
                    host.EngineTotalClicks(),
                    $"{what}：被拒绝的导入也不许动内存里的会话。");
            }

            // 反面：换回一份**好的**文本，同一个入口必须收下——否则上面三条「拒绝了」
            // 可能只是因为这条路根本不通，那种假绿比红更坏。
            CommandOutcome good = host.Import(exported.Text!);
            Check.True(good.Ok, $"同一份好文本必须收得下（否则上面三条拒绝什么都证明不了）：{good.Message}");
            Check.Contains(good.Message, "已导入", "成功那句话要说得出「导进来了」。");
            Check.Equal(9, OwnedOf(host.View(), buildingId), "好文本导进来之后状态要真的换掉。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>不落盘的会话（没给 <c>--save-root</c>）：导出/导入要说「做不到」，而不是崩。</summary>
    [Test]
    public static void WebHost_WithoutASaveRootSaysSoInsteadOfCrashing()
    {
        var package = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
        var host = new GameHost(package, saveRoot: null, seed: 7);

        try
        {
            Check.Null(host.Slot, "前提：这个会话没有存档槽位。");

            CommandOutcome export = host.ExportAsync().GetAwaiter().GetResult();
            Check.False(export.Ok, "不落盘时导出必须明确失败。");
            Check.Contains(export.Message, "不落盘", "理由要写出来——是「不落盘」，不是「出错了」。");
            Check.Null(export.Text, "失败时不该带回半份文本。");

            CommandOutcome import = host.ImportAsync("{}").GetAwaiter().GetResult();
            Check.False(import.Ok, "不落盘时导入必须明确失败。");
            Check.Contains(import.Message, "不落盘", "同上。");
        }
        finally
        {
            host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // ------------------------------------------------------------------ 终端宿主

    /// <summary>
    /// <c>E</c> 导出的文件，另一个会话 <c>I</c> 能吃下去。<para>
    /// 这条路把 W14 的三件事一次走完：提示真的开出来了（<c>E</c> 键接到了
    /// <see cref="InteractiveLoop.HandleKey"/> 上）、文件真的写出来了（<b>且不带 BOM</b>）、
    /// 导入走的是<b>同一个</b> <c>SaveManager.Import</c>（不是终端自己再解析一遍）。
    /// </para>
    /// </summary>
    [Test]
    public static void Terminal_PromptExportsAFileThatAnotherSessionCanImport()
    {
        string dirA = NewTempDir();
        string dirB = NewTempDir();
        try
        {
            using (var a = new GameSession(ContentPackages.Default, Path.Combine(dirA, "neko.json"), seed: 7))
            {
                a.Engine.State.Cookies = 1234.5;
                a.Engine.State.TotalClicks = 137;
                a.Engine.MarkDirty();
                Check.True(a.Saves!.Save(), "前置条件：先存一次盘。");

                // 走**按键**这条路，而不是直接调 ExportTo：接线的价值全在这里。
                InteractiveLoop.HandleKey(a, Typed('E'));
                Check.NotNull(a.Prompt, "E 必须开出一行提示（那就是终端里的「窗口」）。");
                Check.Equal(TransferPromptKind.Export, a.Prompt!.Kind, "E 开的是导出。");
                Check.Contains(a.Prompt.Text, ExportFileName, "默认路径要预填到存档旁边，回车即可。");

                InteractiveLoop.HandleKey(a, Key(ConsoleKey.Enter));
                Check.Null(a.Prompt, "回车之后提示要收掉（否则玩家会以为卡住了）。");
            }

            string exportedPath = Path.Combine(dirA, ExportFileName);
            Check.True(File.Exists(exportedPath), $"导出文件应当落在 {exportedPath}。");

            byte[] bytes = File.ReadAllBytes(exportedPath);
            Check.False(
                bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "导出文件**不许带 BOM**：开头多一个 U+FEFF，JSON 解析会在第一个字符上就失败，"
                + "而报出来的那句话（「连合法 JSON 都不是」）完全指不到 BOM 上。");

            var envelope = (JsonObject)JsonNode.Parse(File.ReadAllText(exportedPath))!;
            Check.Equal(SaveTransfer.FormatTag, envelope["Format"]!.GetValue<string>(), "写出来的必须是一封信封。");

            using var b = new GameSession(ContentPackages.Default, Path.Combine(dirB, "neko.json"), seed: 7);
            InteractiveLoop.HandleKey(b, Typed('I'));
            Check.NotNull(b.Prompt, "I 必须开出一行提示。");
            Check.Equal(TransferPromptKind.Import, b.Prompt!.Kind, "I 开的是导入。");

            // 换一个文件：先清空，再把路径一个字一个字打进去（这就是玩家会做的事）。
            InteractiveLoop.HandleKey(b, Ctrl(ConsoleKey.U));
            Check.Equal(string.Empty, b.Prompt!.Text, "Ctrl+U 清空。");
            foreach (char c in exportedPath) InteractiveLoop.HandleKey(b, Typed(c));

            InteractiveLoop.HandleKey(b, Key(ConsoleKey.Enter));
            Check.Null(b.Prompt, "回车之后提示要收掉。");

            Check.Equal(1234.5, b.Engine.State.Cookies, "导入之后货币要换成导出那一刻的值。");
            Check.Equal(137.0, b.Engine.State.TotalClicks, "计数器也要一起过来。");
        }
        finally
        {
            Cleanup(dirA);
            Cleanup(dirB);
        }
    }

    /// <summary>
    /// 提示开着时，<b>键的意义只剩「编辑这一行路径」</b>。<para>
    /// 这是这次接线里最容易悄悄坏掉的地方：<c>q</c> / <c>x</c> / <c>E</c> 在别处都是有含义的键，
    /// 而它们现在都要能打进一个路径里。判据分两半——开着时是字符，关掉之后（Esc）原来的含义
    /// 必须<b>回来</b>，否则就成了「为了编辑路径把退出键废掉」。
    /// </para>
    /// </summary>
    [Test]
    public static void Terminal_KeysWhileThePromptIsOpenOnlyEditThePath()
    {
        string dir = NewTempDir();
        try
        {
            using var session = new GameSession(ContentPackages.Default, Path.Combine(dir, "neko.json"), seed: 7);
            InteractiveLoop.HandleKey(session, Typed('E'));

            string start = session.Prompt!.Text;
            Check.Greater(start.Length, 0, "前提：提示预填了一个默认路径。");

            // 'q' 是退出键、'x' 是批量键、'E' 是「再开一层提示」——提示开着时它们都只是字符。
            string expected = start;
            foreach (char c in "qxE")
            {
                expected += c;
                InteractiveLoop.HandleKey(session, Typed(c));
                Check.Equal(
                    expected,
                    session.Prompt!.Text,
                    $"提示开着时 '{c}' 应当是路径的一部分，而不是别的动作。");
            }

            Check.False(session.QuitRequested, "提示开着时 'q' 不许退出。");
            Check.Equal(PurchaseMode.Buy1, session.Mode, "提示开着时 'x' 不许换档位。");

            InteractiveLoop.HandleKey(session, Key(ConsoleKey.Backspace));
            Check.Equal(start + "qx", session.Prompt!.Text, "退格删的是最后一个字符。");

            InteractiveLoop.HandleKey(session, Ctrl(ConsoleKey.U));
            Check.Equal(string.Empty, session.Prompt!.Text, "Ctrl+U 清空整行。");

            // 大写 U 不是 Ctrl+U：它得能打进路径里（有的路径真的有大写 U）。
            InteractiveLoop.HandleKey(session, Typed('U'));
            Check.Equal("U", session.Prompt!.Text, "大写 U 是普通字符。");

            InteractiveLoop.HandleKey(session, Key(ConsoleKey.UpArrow));
            Check.Equal("U", session.Prompt!.Text, "方向键没有字符可打，不该在路径里留下垃圾。");

            // Esc 取消：什么都不做、什么都不写。
            InteractiveLoop.HandleKey(session, Key(ConsoleKey.Escape));
            Check.Null(session.Prompt, "Esc 收起提示。");
            Check.False(File.Exists(Path.Combine(dir, ExportFileName)), "取消掉的导出不许留下文件。");

            // 关掉之后原来的含义必须回来——这是这条用例的阴性对照。
            InteractiveLoop.HandleKey(session, Typed('q'));
            Check.True(session.QuitRequested, "提示关掉之后 'q' 要重新是退出。");
        }
        finally
        {
            Cleanup(dir);
        }
    }

    /// <summary>别的包的存档不许导进来，而且拒绝之后那份能用的存档<b>一个字节都不许动</b>。</summary>
    [Test]
    public static void Terminal_ImportFromAForeignPackIsRefusedAndLeavesTheSaveAlone()
    {
        string dirA = NewTempDir();
        string dirB = NewTempDir();
        try
        {
            string exportedPath = Path.Combine(dirA, ExportFileName);
            using (var a = new GameSession(ContentPackages.Default, Path.Combine(dirA, "neko.json"), seed: 7))
            {
                a.Engine.State.Cookies = 4242.25;
                a.Engine.MarkDirty();
                Check.NotNull(a.ExportTo(exportedPath), "前置条件：猫咖包导出一份文本。");
            }

            ContentPackage cafe = ContentPackages.Find("cafe")!;
            string cafeSave = Path.Combine(dirB, "cafe.json");
            using var b = new GameSession(cafe, cafeSave, seed: 7);

            Check.True(b.Saves!.Save(), "前置条件：咖啡馆这一局先存一次盘。");
            byte[] before = File.ReadAllBytes(cafeSave);

            SaveTransferResult? result = b.ImportFrom(exportedPath);

            Check.NotNull(result, "文件读得到，就该有结果（读不到是另一条路）。");
            Check.False(result!.Ok, "别的包的存档必须被拒绝。");
            Check.Equal(SaveTransferKind.ForeignPack, result.Kind, "拒绝的类别应当是 ForeignPack。");
            Check.Contains(result.Message, "neko", "要说清对方是哪个包。");
            Check.Contains(result.Message, "cafe", "也要说清本会话是哪个包。");
            Check.True(
                File.ReadAllBytes(cafeSave).AsSpan().SequenceEqual(before),
                "被拒绝的导入不许动咖啡馆那一局的存档。");
            Check.Equal(0.0, b.Engine.State.Cookies, "也不许动内存里的会话。");
        }
        finally
        {
            Cleanup(dirA);
            Cleanup(dirB);
        }
    }

    /// <summary>
    /// 路径提示画出来的那一帧，<b>仍然满足「每行恰好 width 列、整帧恰好 height 行」</b>。<para>
    /// 既有的 <c>FrameRenderTests</c> 只渲染过「没有提示」的帧（它枚举面板与帮助浮层，
    /// 一次都没让提示开着）——而提示行是新加的一行内容，它自己也可能算错宽度、
    /// 把右边的分隔线顶出去。这几条把那个空白补上。
    /// </para>
    /// </summary>
    [Test]
    public static void Terminal_PromptFrameKeepsTheWidthAndHeightInvariants()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);
        InteractiveLoop.HandleKey(session, Typed('E'));
        session.PromptClear();
        foreach (char c in "saves/neko-export.json") session.PromptAppend(c);

        foreach ((int width, int height) in new[] { (40, 14), (60, 20), (100, 30), (118, 32) })
        {
            List<string> lines = TerminalUi.Render(session, width, height);

            Check.Equal(height, lines.Count, $"提示帧 {width}×{height}：行数必须恰好等于终端高度。");
            for (int i = 0; i < lines.Count; i++)
            {
                Check.Equal(
                    width,
                    Ansi.DisplayWidth(lines[i]),
                    $"提示帧 {width}×{height}：第 {i + 1} 行宽度不是 {width} 列"
                    + "（右边那些 │ 会错位、行尾会折到下一行）。");
            }
        }

        string frame = string.Join("\n", TerminalUi.Render(session, 100, 30));
        Check.Contains(frame, "saves/neko-export.json", "这一行提示要真的画出来（否则上面那些「宽度对」没有意义）。");
        Check.Contains(frame, "取消", "键位行要换成这一件事的键位（提示开着时别的键都不响应）。");
        Check.Contains(frame, "Ctrl+U", "清空这个键要说出来，否则没人知道它存在。");
        Check.False(
            frame.Contains("帮助", StringComparison.Ordinal),
            "提示开着时游戏那一行键位要让位——继续挂着「H 帮助」「Q 退出」就是在说谎。");

        // 路径长过一行时**从左边截**：正在敲进去的是尾部，而终端里没有横向滚动条。
        session.PromptClear();
        foreach (char c in new string('x', 200) + "TAIL.json") session.PromptAppend(c);
        string longFrame = string.Join("\n", TerminalUi.Render(session, 60, 20));
        Check.Contains(longFrame, "TAIL.json", "过长的路径要保住尾部，否则等于让人盲打。");

        // 提示收掉之后，原来那一行要回来（别把「最后一帧是提示」留在屏幕上）。
        InteractiveLoop.HandleKey(session, Key(ConsoleKey.Escape));
        string normal = string.Join("\n", TerminalUi.Render(session, 100, 30));
        Check.False(normal.Contains("TAIL.json", StringComparison.Ordinal), "提示关掉之后那一行就不该还在。");
    }

    /// <summary>不落盘的会话（<c>--no-save</c>）：导出/导入要说「做不到」，而不是静默什么都不做。</summary>
    [Test]
    public static void Terminal_WithoutASaveSaysExportAndImportAreUnavailable()
    {
        using var session = new GameSession(ContentPackages.Default, savePath: null, seed: 7);

        InteractiveLoop.HandleKey(session, Typed('E'));
        InteractiveLoop.HandleKey(session, Key(ConsoleKey.Enter));

        Check.Null(session.Prompt, "提示照样要收掉。");
        Check.Null(session.Saves, "前提：这个会话没有存档管理器。");
        Check.Contains(
            session.LogLines[^1].Message,
            "未启用存档",
            "不落盘时导出必须明说做不到：静默什么都不做，玩家会以为导出成功了。");

        InteractiveLoop.HandleKey(session, Typed('I'));
        InteractiveLoop.HandleKey(session, Key(ConsoleKey.Enter));
        Check.Contains(session.LogLines[^1].Message, "未启用存档", "导入同理。");
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>一台跑着的 Web 会话 + 它的临时存档根。</summary>
    private sealed class WebHostScope : IDisposable
    {
        private readonly GameHost _host;

        public WebHostScope(string root)
        {
            var package = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
            // latencyLogPath: null —— 这些用例一个字节都不该写进 artifacts/latency.txt（那是真人数据）。
            _host = new GameHost(package, root, seed: 7, latencyLogPath: null);
        }

        public CommandOutcome Export() => _host.ExportAsync().GetAwaiter().GetResult();

        public CommandOutcome Import(string text) => _host.ImportAsync(text).GetAwaiter().GetResult();

        public CommandOutcome Save() => _host.SaveAsync().GetAwaiter().GetResult();

        /// <summary>取当前快照（导入成功之后不用等下一帧：<c>ImportAsync</c> 自己推了一份全量）。</summary>
        public JsonObject View() => _host.ViewSnapshot();

        /// <summary>在游戏线程上改一次状态（引擎只有一个主人，改状态也要走那条线程）。</summary>
        public void Edit(Action<GameEngine> action)
            => _host.ExecuteAsync(engine =>
            {
                action(engine);
                return new CommandOutcome(true, string.Empty, 0);
            }).GetAwaiter().GetResult();

        /// <summary>读引擎里的计数器（不是快照：快照是 250ms 一帧，会慢半拍）。</summary>
        public double EngineTotalClicks()
        {
            double clicks = -1;
            Edit(engine => clicks = engine.State.TotalClicks);
            return clicks;
        }

        public void Dispose() => _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 灌一份<b>确定</b>的状态：<c>TotalClicks</c> 与建筑持有数都与秒产量无关，
    /// 所以断言可以对到整数上——引擎线程一直在跑，钱每毫秒都在涨。
    /// </summary>
    /// <returns>动过的那座建筑的 id（断言要用它去快照里找那一行）。</returns>
    private static string SetDeterministicState(WebHostScope host)
    {
        string buildingId = string.Empty;

        host.Edit(engine =>
        {
            buildingId = engine.Content.BuildingById.Keys.First();
            engine.State.TotalClicks = 777;
            engine.State.Cookies = 1_000_000;
            engine.State.BuildingCounts[buildingId] = 9;
            engine.MarkDirty();
        });

        Check.Greater(buildingId.Length, 0, "前置条件：这个包至少有一座建筑。");
        return buildingId;
    }

    /// <summary>
    /// 把导出文本里的包标识改成另一个包。<para>
    /// <b>这一改仍然过得了校验闸</b>（校验和只算 <c>Save</c> 字段，包标识在外壳上）——
    /// 于是它验的正是归属闸本身。这也是那份诚实边界：包标识是<b>声明</b>，不是证明。
    /// </para>
    /// </summary>
    private static string ForeignPack(string text, string packId)
    {
        var envelope = (JsonObject)JsonNode.Parse(text)!;
        envelope["PackId"] = packId;
        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static int OwnedOf(JsonObject view, string buildingId)
    {
        foreach (JsonNode? row in view["buildings"]!.AsArray())
        {
            if (row?["id"]?.GetValue<string>() == buildingId) return row["owned"]!.GetValue<int>();
        }

        throw new AssertionException($"快照里没有建筑「{buildingId}」这一行。");
    }

    /// <summary>按下一个字符键（字母映射到对应的 <see cref="ConsoleKey"/>，与真终端一致）。</summary>
    private static ConsoleKeyInfo Typed(char c)
    {
        ConsoleKey key = char.IsAsciiLetter(c)
            ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(c).ToString())
            : ConsoleKey.NoName;

        return new ConsoleKeyInfo(c, key, shift: false, alt: false, control: false);
    }

    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    private static ConsoleKeyInfo Ctrl(ConsoleKey key) => new('\u0015', key, false, false, true);

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "neko-save-transfer-host-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录没清掉不影响结论。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
        }
    }
}
