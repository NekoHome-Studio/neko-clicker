using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using NekoClicker.Core;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Views;
using NekoClicker.Hosts;

namespace NekoClicker.Web;

/// <summary>
/// Web 前端宿主的入口。<para>
/// 职责边界与终端 Demo 完全一致：<b>只读 <see cref="GameSnapshot"/>、只发命令</b>，
/// 一行游戏规则都不实现。它额外承担的是 Web 特有的两件事——长连接推送，
/// 以及"引擎只有一个主人"（见 <see cref="GameHost"/>）。
/// </para>
/// </summary>
public static class Program
{
    private const string DefaultUrl = "http://127.0.0.1:5273";

    /// <summary>
    /// 调试门密钥所在的<b>环境变量名</b>。<para>
    /// 这里只有名字，没有值：仓库与发布产物里<b>不存在任何密钥</b>，所以"没设"时这道门根本不存在
    /// （见 <c>engine/docs/WEB_DEBUG_GATE_PLAN.md</c> §2）。想调试的人自己临时设一个：
    /// <c>$env:NEKO_DEBUG_KEY = '...'; start.cmd web</c>——用 <b>环境变量</b>而不是代码常量或配置文件，
    /// 是因为常量等于把密钥公开、配置文件会被提交也会被打进 zip。
    /// </para>
    /// </summary>
    private const string DebugKeyVariable = "NEKO_DEBUG_KEY";

    /// <summary>
    /// 令牌门密钥所在的<b>环境变量名</b>（与明文调试门<b>分开</b>的一把钥匙）。<para>
    /// 它用来解密 <c>?k=…</c> 里的那段密文（见 <c>engine/docs/SHARE_LINK_PLAN.md</c> §4）。
    /// 与 <see cref="DebugKeyVariable"/> 一样：这里只有名字、没有值，仓库与发布产物里不存在任何密钥，
    /// 所以"没设"时这扇门根本不存在。
    /// </para>
    /// <para>
    /// <b>为什么和调试门不是同一把</b>：一扇门该有自己的钥匙。共用一把会让"我只想让跳层令牌生效"
    /// 顺手把明文门也开了，而两扇门的射程并不一样（明文门泄漏的是可复用的钥匙，令牌泄漏的是一次跳转）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 名字的**唯一出处**在 <see cref="EraLinkMinter.KeyVariable"/>：铸造端那句
    /// "没有设置环境变量 NEKO_URL_KEY" 与这扇门读的那个名字必须是同一个常量，
    /// 否则改一处就会得到一条"照着提示设了变量、门却还是没开"的死路。
    /// </remarks>
    private const string UrlKeyVariable = EraLinkMinter.KeyVariable;

    /// <summary>跳层会跳过的账（响应正文必须说出来，见 plan §3）。</summary>
    private static readonly string[] DebugSkippedBookkeeping =
        ["era_inheritance", "era_history", "prestige_settlement"];

    /// <summary>启动宿主。</summary>
    /// <param name="args">
    /// <c>--urls &lt;地址&gt;</c> 换监听地址；<c>--save-root &lt;目录&gt;</c> 换存档根目录；
    /// <c>--latency-log &lt;文件&gt;</c> 换作答延迟埋点文件（默认 <c>artifacts/latency.txt</c>）；
    /// <c>--mint-link --package &lt;id&gt; --era &lt;层号&gt; [--expires-hours &lt;小时&gt;] [--base-url &lt;地址&gt;]</c>
    /// 铸一枚跳层令牌并打印 URL 后退出（<b>不启动宿主</b>）。
    /// </param>
    /// <returns>进程退出码。</returns>
    public static int Main(string[] args)
    {
        // ---- 铸令牌模式：打印一段 URL 就退出，**不启动宿主、不监听端口**。
        // 放在最前面：铸造是"写一条链接"，顺手把宿主起起来只会让人以为它要一直开着。
        if (args.Contains("--mint-link", StringComparer.Ordinal)) return MintEraLink(args);

        string url = ReadOption(args, "--urls") ?? ReadOption(args, "--url") ?? DefaultUrl;
        string saveRoot = Path.GetFullPath(ReadOption(args, "--save-root") ?? DefaultSaveRoot());

        // 作答延迟埋点（见 ChoiceLatencyLog 的类注释）：默认落在仓库的 artifacts/ 里，
        // **不是**因为"顺手"，而是因为这是唯一一条能把数字交到不坐在这台终端前的人手里的路。
        string latencyLog = Path.GetFullPath(
            ReadOption(args, "--latency-log") ?? Path.Combine(DefaultArtifactsRoot(), "latency.txt"));

        // 内容根：能自己找就自己找，别依赖"启动时的工作目录"。
        // 实测（2026-10-02，打包后的产物）：在包根目录敲 `web\neko-clicker-web.exe`，
        // `/api/*` 全部正常而首页 404——ASP.NET Core 默认把**当前工作目录**当 ContentRoot，
        // 而 `wwwroot` 在 `web\` 下面；说明书里写的正是那条命令。
        // 但也不能无条件指向程序集目录：开发期 `bin` 里**没有** `wwwroot`（静态文件靠 SDK 写的
        // staticwebassets 清单解析到源码目录），内容根一改清单就找不到，首页照样 404。
        // 于是判据就是"程序集旁边有没有 wwwroot"：有（= 发布产物）用它，没有（= 开发期）保持默认。
        string? contentRoot = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
            ? AppContext.BaseDirectory
            : null;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = contentRoot,
        });
        builder.WebHost.UseUrls(url);
        builder.Services.AddSingleton(new HostOptions(saveRoot, latencyLog));

        WebApplication app = builder.Build();

        // ---- Web 调试门（仅宿主功能，见 engine/docs/WEB_DEBUG_GATE_PLAN.md）。
        // **必须排在静态文件之前**：'/' 会被 UseDefaultFiles 改写成 index.html、再由 UseStaticFiles
        // 直接端出去（实测日志就是这个顺序），排在后面的话带 epoch 的请求永远到不了这里。
        app.Use(async (context, next) =>
        {
            if (await TryHandleDebugGateAsync(context).ConfigureAwait(false)) return;
            await next(context).ConfigureAwait(false);
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();

        MapApi(app);

        // 关停时必须存一次盘：自动存档是每 60 秒一次，正常退出不存的话
        // 那最多 60 秒的进度连"离线收益"都拿不到（MinimumOfflineSeconds 恰好也是 60）。
        IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(Sessions.StopAll);

        Console.WriteLine($"NekoClicker Web 宿主已启动：{url}");
        Console.WriteLine($"框架版本 {ApiVersion.Current}｜内容包 {PackageCatalog.All.Count} 个｜存档目录 {saveRoot}");

        // 调试门的状态必须在启动时说清楚：变量设在了别的 shell 里的话，否则要等到 403 才知道
        // ——那正是"沉默的坑"。（这里只说变量设没设，永远不会打印密钥本身。）
        Console.WriteLine(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DebugKeyVariable))
            ? $"调试门：未启用（没有设置 {DebugKeyVariable}，带 epoch 的请求一律 403）。"
            : $"调试门：已启用（{DebugKeyVariable} 已设置）；用法 {url}/?package=<id>&password=<密钥>&epoch=<层号>。");

        // 令牌门（?k=）与明文门是**两扇独立的门**（各自的钥匙）。这里同样只说"设没设"，
        // 永远不会打印钥匙本身——打印出来的东西会进日志、进截图。
        Console.WriteLine(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(UrlKeyVariable))
            ? $"令牌门：未启用（没有设置 {UrlKeyVariable}，带 k 的请求一律 403）。"
            : $"令牌门：已启用（{UrlKeyVariable} 已设置）；铸一枚：--mint-link --package <id> --era <层号>。");

        Console.WriteLine($"打开 {url}/ 开始玩；换包用 {url}/?package=<id>。Ctrl+C 退出。");

        // 埋点写到哪去了必须当场说清楚：这个文件的读者常常不是启动它的人，
        // 而"埋点在跑"与"埋点没接线"在现象上都是"没有数据"——只有把路径报出来才分得清。
        // 表头同时在这里先落好：这样"埋点接没接上"在看任何页面之前就能被检查
        // （文件在 = 线接上了；文件不在 = 埋点根本没跑）。
        var latencyFile = new LatencyLogFile(latencyLog);
        if (!latencyFile.Ensure()) Console.Error.WriteLine($"[埋点] 写不进 <{latencyLog}>：{latencyFile.LastError}");
        Console.WriteLine($"作答延迟埋点：{latencyLog}（只追加；只有真人作答才会出现数据行）");

        app.Run();
        return 0;
    }

    private static void MapApi(WebApplication app)
    {
        // ---- 框架自证：宿主能在运行时报出引擎版本（阶段 6 的 ApiVersion 就是为这件事加的）
        app.MapGet("/api/ping", () => Results.Json(new
        {
            app = "neko-clicker-web",
            apiVersion = ApiVersion.Current,
            assemblyVersion = ApiVersion.AssemblyVersion,
            runtime = Environment.Version.ToString(),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            pushIntervalMs = 250,
            protocol = "snapshot-envelope-v1",
        }, SnapshotProtocol.Options));

        // ---- 内容包目录（运行时扫描出来的，宿主里没有包名字面量）
        app.MapGet("/api/packs", () => Results.Json(
            PackageCatalog.All.Select(p => new { id = p.Id, name = p.Name, welcome = p.Welcome }),
            SnapshotProtocol.Options));

        // ---- 当前快照：一份全量。客户端接 SSE 之前先拿它当起点，或页面刷新时兜底。
        app.MapGet("/api/snapshot", (string? package, HostOptions options) =>
        {
            GameHost? host = Sessions.TryGet(package, options);
            return host is null
                ? Results.Json(new { error = "unknown_package", package }, SnapshotProtocol.Options, statusCode: 404)
                : Results.Text(host.ViewJson, "application/json");
        });

        // ---- 推送流：SSE。第一帧一定是全量，之后是增量信封 + 每 30 秒一次全量对账。
        app.MapGet("/api/stream", async (HttpContext context, string? package, HostOptions options) =>
        {
            GameHost? host = Sessions.TryGet(package, options);
            if (host is null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"没有内容包 <{package}>。").ConfigureAwait(false);
                return;
            }

            context.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store";
            // 反向代理常见的缓冲会让 SSE 变成"攒一批再发"，本机直连也要写清楚
            context.Response.Headers["X-Accel-Buffering"] = "no";

            ChannelReader<string> reader = host.Subscribe();
            try
            {
                await foreach (string frame in reader.ReadAllAsync(context.RequestAborted).ConfigureAwait(false))
                {
                    // SSE 的一帧是 `data: <单行>`；我们的 JSON 是紧凑单行的，正好放得下
                    await context.Response.WriteAsync($"data: {frame}\n\n", context.RequestAborted).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 浏览器关页面 / 刷新：正常路径，不是错误
            }
            finally
            {
                host.Unsubscribe(reader);
            }
        });

        // ---- 命令：所有会改状态的动作都从这里进，由游戏线程串行执行
        app.MapPost("/api/command", async (HttpContext context, string? package, HostOptions options) =>
        {
            GameHost? host = Sessions.TryGet(package, options);
            if (host is null)
            {
                return Results.Json(new { ok = false, message = "没有这个内容包。" }, SnapshotProtocol.Options, statusCode: 404);
            }

            JsonNode? body;
            try
            {
                body = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.Json(new { ok = false, message = "请求体不是合法 JSON。" }, SnapshotProtocol.Options, statusCode: 400);
            }

            string type = body?["type"]?.GetValue<string>() ?? string.Empty;
            CommandOutcome outcome = await DispatchAsync(host, type, body).ConfigureAwait(false);
            // `text` 是命令**带回来的文本**（今天只有 export 用它，别的命令是 null）：
            // 导出是一份 1.5~6 KB 的机器数据，混进 `message` 那句给人看的话里，界面上就只能二选一。
            return Results.Json(
                new { ok = outcome.Ok, message = outcome.Message, seq = outcome.Seq, text = outcome.Text },
                SnapshotProtocol.Options);
        });
    }

    /// <summary>把一条命令派发到游戏线程上执行。</summary>
    private static async Task<CommandOutcome> DispatchAsync(GameHost host, string type, JsonNode? body)
    {
        string? id = body?["id"]?.GetValue<string>();
        string? optionId = body?["optionId"]?.GetValue<string>();
        int amount = body?["amount"]?.GetValue<int>() ?? 0;

        switch (type)
        {
            case "click":
                return await host.ExecuteAsync(engine => GameHost.FromResult(engine.Click(), host.Seq)).ConfigureAwait(false);

            case "buy":
                if (id is null) return Missing("id");
                return await host.ExecuteAsync(engine => GameHost.FromResult(
                    amount > 0 ? engine.BuyBuilding(id, amount) : engine.BuyBuilding(id), host.Seq)).ConfigureAwait(false);

            case "sell":
                if (id is null) return Missing("id");
                return await host.ExecuteAsync(engine => GameHost.FromResult(
                    amount > 0 ? engine.SellBuilding(id, amount) : engine.SellBuilding(id), host.Seq)).ConfigureAwait(false);

            case "upgrade":
                if (id is null) return Missing("id");
                return await host.ExecuteAsync(engine =>
                    GameHost.FromResult(engine.BuyUpgrade(id), host.Seq)).ConfigureAwait(false);

            case "grabGolden":
                if (id is null) return Missing("id");
                return await host.ExecuteAsync(engine =>
                    GameHost.FromResult(engine.ClickGoldenCookie(id), host.Seq)).ConfigureAwait(false);

            case "answer":
                if (id is null || optionId is null) return Missing("id / optionId");
                return await host.ExecuteAsync(engine =>
                {
                    bool ok = engine.AnswerChoice(id, optionId);
                    // 失败原因只有两种（见 ChoiceSystem.Answer）：已经答过，或这条 id 根本不在
                    // 待答队列里。**不要再写"那层已经过去了"**——实测（1.6.0）跨层之后待答表态
                    // 仍然答得上：舍命不碰 PendingChoices，作答也不重判 EraId。
                    // 引擎收不收下这次作答只取决于"它在不在待答清单里"。
                    return new CommandOutcome(ok, ok ? "已表态。" : "这次表态不成立（可能已经答过，或它不在待答清单里）。", host.Seq);
                }).ConfigureAwait(false);

            // "待答表态已经展示给玩家看过了"——**诊断信号，不参与判定**。
            // 1.6.0 起结局等的是玩家把表态**答完**（见 GameEngine.CheckEnding / EndingSystem.Check）；
            // 1.5.0 那会儿这一发才是落定的唯一条件，那条规则已经被换掉了。
            // 留着它是因为它记下的东西有用：**每条表态到底露过面没有**——新规则下"结局一直没落定"
            // 是真实可能的事，而"从没看到"与"看到了却一直没答"是两种完全不同的原因。
            // 它是一条**状态**而不是前端的记忆：刷新、换标签页、换设备之后，"玩家看没看过"
            // 必须仍是同一个答案，所以真相在引擎里，前端每次渲染只负责报告"我把它画出来了"。
            // 幂等：客户端每渲染一帧都可能发一次，第二发不是错误。
            case "choicesShown":
                return await host.ExecuteAsync(engine =>
                {
                    int marked = engine.MarkPendingChoicesShown();
                    return new CommandOutcome(
                        true,
                        marked > 0
                            ? $"已记下 {marked} 项表态的展示记录（诊断用；结局现在等的是作答）。"
                            : "这些表态已经记过了。",
                        host.Seq);
                }).ConfigureAwait(false);

            case "dismissLore":
                if (id is null) return Missing("id");
                return await host.ExecuteAsync(engine =>
                {
                    bool ok = engine.DismissLorePopup(id);
                    return new CommandOutcome(ok, ok ? string.Empty : "这条叙事已经点掉了。", host.Seq);
                }).ConfigureAwait(false);

            case "dismissAllLore":
                return await host.ExecuteAsync(engine =>
                {
                    int count = engine.DismissAllLorePopups();
                    return new CommandOutcome(true, $"点掉了 {count} 条。", host.Seq);
                }).ConfigureAwait(false);

            case "ascend":
                return await host.ExecuteAsync(engine =>
                    GameHost.FromResult(engine.Ascend(), host.Seq)).ConfigureAwait(false);

            case "mode":
                PurchaseMode? mode = GameHost.ParseMode(body?["mode"]?.GetValue<string>());
                return mode is null
                    ? new CommandOutcome(false, "无法识别的批量档位。", host.Seq)
                    : await host.SetModeAsync(mode.Value).ConfigureAwait(false);

            case "save":
                return await host.SaveAsync().ConfigureAwait(false);

            // 存档的导出 / 导入（W13）。两条命令都不做任何校验与解析：引擎那边
            // SaveManager.Export/Import 已经把七道闸排好了，宿主只负责搬运与回报。
            case "export":
                return await host.ExportAsync().ConfigureAwait(false);

            case "import":
            {
                // 类型不对时**当作没给**，而不是让 GetValue<string>() 抛成 500：
                // 前端拼错一个字段不该得到一份堆栈，而是一句"缺少参数"。
                string? text = ReadString(body, "text");
                if (text is null) return Missing("text");
                return await host.ImportAsync(text).ConfigureAwait(false);
            }

            // 分享链接（见 engine/docs/SHARE_LINK_PLAN.md §5）。与 export/import 同一条分工：
            // 宿主只搬运与回报，加解密与九道闸都在别处，一行校验都不在这里重写。
            // **密码走请求体、不走 URL**，而且宿主不日志它、不回显它——它只用来派生密钥。
            case "shareLink":
            {
                string? password = ReadString(body, "password");
                if (password is null) return Missing("password");
                return await host.CreateShareLinkAsync(password).ConfigureAwait(false);
            }

            case "importShare":
            {
                string? token = ReadString(body, "token");
                string? password = ReadString(body, "password");
                if (token is null) return Missing("token");
                if (password is null) return Missing("password");
                return await host.ImportShareAsync(token, password).ConfigureAwait(false);
            }

            // 离线收益弹窗的"看过了"。这是一条**状态**而不是前端的记忆：
            // 放在前端（localStorage / sessionStorage）就一定会错——两次离线补发完全可能
            // 数值一模一样（同样离线到上限、产量也没变），按数值当指纹去重会把第二次吃掉；
            // 按"每个标签页记一次"则刷新不弹、新开标签页又弹。状态在引擎里就只有一份真相。
            case "dismissOffline":
                return await host.ExecuteAsync(engine =>
                {
                    bool seen = engine.DismissOfflineProgress();
                    // 幂等：两个标签页都会发这条命令，第二发不是错误。
                    return new CommandOutcome(true, seen ? "已收下离线收益。" : "没有待播报的离线收益。", host.Seq);
                }).ConfigureAwait(false);

            case "hardReset":
            {
                // 先存一次（正常玩法下这是"重置前把当前进度落盘"），再重置。
                // 调试跳层的会话里 SaveAsync 会明确拒绝，所以这里的文案必须跟着变——
                // 不能一边没覆盖存档、一边告诉玩家"存档也已覆盖"。
                CommandOutcome overwritten = await host.SaveAsync().ConfigureAwait(false);
                return await host.ExecuteAsync(engine =>
                {
                    engine.HardReset();
                    return new CommandOutcome(true, overwritten.Ok
                        ? "已重置，存档也已覆盖。"
                        : $"已重置（只在内存里）；{overwritten.Message}", host.Seq);
                }).ConfigureAwait(false);
            }

            default:
                return new CommandOutcome(false, $"不认识的命令：{type}", host.Seq);
        }

        static CommandOutcome Missing(string what) => new(false, $"这条命令缺少参数 {what}。", 0);

        // 读一个字符串字段；缺失**或类型不对**都返回 null。用 TryGetValue 而不是
        // `GetValue<string>()`：后者对 `{"text": 5}` 会抛 InvalidOperationException，
        // 顺着请求管道冒出去就是一个 500 —— 那正是"坏输入被说成内部错误"的形状。
        static string? ReadString(JsonNode? node, string name)
            => node?[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    /// <summary>
    /// Web 调试门：<c>/?package=lab&amp;password=&lt;密钥&gt;&amp;epoch=7</c> 把这一局直接置到第 7 层。<para>
    /// <b>密钥只从环境变量来</b>（<see cref="DebugKeyVariable"/>），仓库里不存在任何密钥，
    /// 所以没设变量时这道门根本不存在——带了 <c>epoch</c> 也只会得到一句"门未启用"。
    /// </para>
    /// <para>
    /// <b>拒绝一律明确报错，绝不静默忽略</b>（plan §5）：静默忽略正是这个项目一路在消灭的失败形态
    /// ——玩家会以为自己参数名写错或版本不对，然后去查一个根本不存在的问题。
    /// </para>
    /// </summary>
    /// <param name="context">当前请求。</param>
    /// <returns>已经写过响应时返回 <c>true</c>（调用方不要再往下走）。</returns>
    private static async Task<bool> TryHandleDebugGateAsync(HttpContext context)
    {
        // 只认首页：这道门是"用 URL 跳层"，不是给 API 加的旁路
        if (!HttpMethods.IsGet(context.Request.Method)) return false;

        string? path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path)
            && path != "/"
            && !path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)) return false;

        IQueryCollection query = context.Request.Query;

        // 两扇门都不带参数就是普通请求：一字不改地走原来的路径（这是回归的底线，plan §7.5）
        bool hasEpoch = query.ContainsKey("epoch");
        bool hasToken = query.ContainsKey("k");

        if (!hasEpoch && !hasToken) return false;

        context.Response.Headers.CacheControl = "no-store";

        // 两扇门同时带参数：**明确拒绝，不挑一个执行**。静默挑一个正是这个项目一路在消灭的
        // 失败形态——请求方会以为自己要的那扇门生效了，然后去查一个不存在的问题。
        if (hasEpoch && hasToken)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
            {
                ok = false,
                message = "这次请求同时带了 epoch 与 k：它们两扇独立的门，一次只能用一扇。",
            }).ConfigureAwait(false);
            return true;
        }

        // 令牌门（?k=…，见 SHARE_LINK_PLAN §4）：先分流。
        // 下面明文调试门那一段**一个字节都没有改**。
        if (hasToken) return await TryHandleTokenGateAsync(context, query).ConfigureAwait(false);

        // ① 密钥装置本身在不在
        string? key = Environment.GetEnvironmentVariable(DebugKeyVariable);
        if (string.IsNullOrEmpty(key))
        {
            await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
            {
                ok = false,
                debug = true,
                message = $"调试门未启用：本机没有设置环境变量 {DebugKeyVariable}。",
            }).ConfigureAwait(false);
            return true;
        }

        // ② 密钥对不对（只区分"没配"与"配错"，不回显正确值）
        if (!string.Equals(query["password"].ToString(), key, StringComparison.Ordinal))
        {
            await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
            {
                ok = false,
                debug = true,
                message = "调试密钥不对。",
            }).ConfigureAwait(false);
            return true;
        }

        // ③ 密钥已经对了，这时才允许碰会话：被拒的请求不该顺手把内容包加载进内存
        string packageId = query["package"].ToString();
        HostOptions options = context.RequestServices.GetRequiredService<HostOptions>();
        GameHost? host = Sessions.TryGet(packageId.Length == 0 ? null : packageId, options);
        if (host is null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new
            {
                ok = false,
                debug = true,
                package = packageId,
                message = $"没有内容包 <{packageId}>。",
            }).ConfigureAwait(false);
            return true;
        }

        // ④ 解析与范围校验都在游戏线程上做，于是 400 里带的范围一定是这个包真实的那个
        DebugEraJump jump = await host.JumpToEraAsync(query["epoch"].ToString()).ConfigureAwait(false);

        if (!jump.Applied)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
            {
                ok = false,
                debug = true,
                package = host.Package.Id,
                from = jump.From,
                validRange = new { min = jump.Min, max = jump.Max },
                message = jump.Message,
            }).ConfigureAwait(false);
            return true;
        }

        // ⑤ 成功：说清楚跳到了哪、跳过了哪些账、以及本次不落盘
        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            ok = true,
            debug = true,
            package = host.Package.Id,
            from = jump.From,
            to = jump.To,
            validRange = new { min = jump.Min, max = jump.Max },
            skipped = DebugSkippedBookkeeping,
            autosave = "disabled",
            message = jump.Message,
        }).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 令牌门：<c>/?package=lab&amp;k=&lt;密文&gt;</c> 用 <see cref="UrlKeyVariable"/> 解密，
    /// 解出来的层号走<b>同一条</b> <see cref="GameHost.JumpToEraAsync"/>（见
    /// <c>engine/docs/SHARE_LINK_PLAN.md</c> §4）。<para>
    /// 于是"合法范围"与"跳过了哪些账"这两件事**不需要在这里重写一遍**：
    /// 400 里的范围一定是这个包真实的那个，跳层的局限也一定跟着既有的回报走。
    /// </para>
    /// <para>
    /// 拒绝一律明确报错，并且把 <see cref="ShareLinkFailure"/> 的名字放进正文
    /// （<c>failure</c> 字段）：脚本与用例于是能精确判别，而不必去解析一句中文。
    /// </para>
    /// <para>
    /// <b>这里不是权限系统</b>：钥匙在宿主的环境里，能读到它的人本来就能改这台机器上的存档
    /// （SHARE_LINK_PLAN §4.3 把这条威胁模型写全了）。它比明文门强的地方只有两处、但都是真的：
    /// URL 里不再是可复用的钥匙，而且范围与有效期被密文锁住。
    /// </para>
    /// </summary>
    /// <param name="context">当前请求。</param>
    /// <param name="query">已解析的查询串。</param>
    /// <returns>已经写过响应时返回 <c>true</c>。</returns>
    private static async Task<bool> TryHandleTokenGateAsync(HttpContext context, IQueryCollection query)
    {
        // ① 钥匙装置本身在不在
        string? key = Environment.GetEnvironmentVariable(UrlKeyVariable);
        if (string.IsNullOrEmpty(key))
        {
            await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
            {
                ok = false,
                gate = "token",
                failure = "KeyNotConfigured",
                message = $"令牌门未启用：本机没有设置环境变量 {UrlKeyVariable}。",
            }).ConfigureAwait(false);
            return true;
        }

        // ② 解令牌（在碰会话之前就要有答案：被拒的请求不该顺手把内容包加载进内存）
        string packageId = query["package"].ToString();
        HostOptions options = context.RequestServices.GetRequiredService<HostOptions>();
        GameHost? host = Sessions.TryGet(packageId.Length == 0 ? null : packageId, options);
        if (host is null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new
            {
                ok = false,
                gate = "token",
                package = packageId,
                message = $"没有内容包 <{packageId}>。",
            }).ConfigureAwait(false);
            return true;
        }

        ShareLinkOpenResult opened = ShareLinkCodec.Open(
            key,
            query["k"].ToString(),
            ShareLinkPurpose.EraToken,
            host.Package.Id,
            DateTimeOffset.UtcNow);

        if (!opened.Ok)
        {
            await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
            {
                ok = false,
                gate = "token",
                package = host.Package.Id,
                failure = opened.Failure.ToString(),
                message = opened.Message,
            }).ConfigureAwait(false);
            return true;
        }

        // ③ 与明文门**同一个**入口：范围校验在游戏线程上做，400 里带的是这个包真实的合法范围。
        DebugEraJump jump = await host
            .JumpToEraAsync(opened.Payload!.Era.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        if (!jump.Applied)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
            {
                ok = false,
                gate = "token",
                package = host.Package.Id,
                from = jump.From,
                validRange = new { min = jump.Min, max = jump.Max },
                message = jump.Message,
            }).ConfigureAwait(false);
            return true;
        }

        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            ok = true,
            gate = "token",
            package = host.Package.Id,
            from = jump.From,
            to = jump.To,
            validRange = new { min = jump.Min, max = jump.Max },
            skipped = DebugSkippedBookkeeping,
            autosave = "disabled",
            expiresAt = opened.Payload.ExpiresAt?.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture),
            message = jump.Message,
        }).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 铸一枚跳层令牌并打印 URL（<c>--mint-link</c>，见 <c>SHARE_LINK_PLAN.md</c> §4.2）。<para>
    /// 它<b>不启动宿主</b>：铸造是"写一条链接"，而链接的下一站是浏览器。
    /// 钥匙只从 <see cref="UrlKeyVariable"/> 来；没设就<b>拒绝铸造</b>——
    /// 绝不产出一段"宿主一定解不开"的链接（那正是最坏的一种成功报告）。
    /// </para>
    /// <para>
    /// 校验本身（钥匙 / 用法 / 包在不在 / 层号范围 / 有效期）**一行都不在这个文件里**：
    /// 它们全在 <see cref="EraLinkMinter.Mint"/>（<c>ShareLink.cs</c>，在共享源码清单里），
    /// 所以"层号越界会被拒绝"这句话有 C# 用例钉着——这个文件碰 ASP.NET，进不了测试项目，
    /// 原先长在这里的那段校验因此一条守卫都没有（<c>SHARE_LINK_PLAN.md</c> §13 末尾记过这条缺口）。
    /// 这里只剩三件事：读环境变量、把参数翻成纯数据、按分工把结果打到两个流上。
    /// </para>
    /// <para>
    /// 输出分工是刻意的：<b>URL 走 stdout</b>（脚本要能整行取走），
    /// 给人看的那两句走 stderr。
    /// </para>
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>退出码；成功为 <c>0</c>。</returns>
    private static int MintEraLink(string[] args)
    {
        string baseUrl = ReadOption(args, "--base-url")
                         ?? ReadOption(args, "--urls")
                         ?? ReadOption(args, "--url")
                         ?? DefaultUrl;

        // 目录只提供两样东西：有哪些 id、以及某个 id 最多能跳到第几层。
        // 后者是**按需**调用的（只给选中的那个包 Build 一次），坏包不该把整条命令拖垮。
        string[] packIds = [.. PackageCatalog.All.Select(p => p.Id)];

        EraLinkMintResult result = EraLinkMinter.Mint(
            new EraLinkMintRequest(
                Environment.GetEnvironmentVariable(UrlKeyVariable),
                ReadOption(args, "--package"),
                ReadOption(args, "--era"),
                ReadOption(args, "--expires-hours"),
                baseUrl),
            packIds,
            id => PackageCatalog.Find(id) is { } package ? package.Build().MaxEraIndex : null);

        if (result.Url is not null) Console.WriteLine(result.Url);
        Console.Error.WriteLine(result.Message);
        return result.ExitCode;
    }

    /// <summary>按线上协议的序列化选项写一份 JSON 响应。</summary>
    private static Task WriteJsonAsync(HttpContext context, int statusCode, object body)
        => Results.Json(body, SnapshotProtocol.Options, statusCode: statusCode).ExecuteAsync(context);

    /// <summary>
    /// 存档根目录：仓库根下的 <c>saves/</c>（仓库根由 <see cref="RepositoryPaths"/> 定位）。<para>
    /// 找不到仓库根时退化成当前目录下的 <c>saves/</c>，至少不会写到别处去。
    /// </para>
    /// </summary>
    private static string DefaultSaveRoot()
    {
        string? root = RepositoryPaths.Find();
        return root is null ? Path.Combine(Directory.GetCurrentDirectory(), "saves") : Path.Combine(root, "saves");
    }

    /// <summary>埋点目录：仓库根下的 <c>artifacts/</c>（<c>.gitignore</c> 已忽略）。</summary>
    private static string DefaultArtifactsRoot()
    {
        string? root = RepositoryPaths.Find();
        return Path.Combine(root ?? Directory.GetCurrentDirectory(), "artifacts");
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
        }

        return null;
    }
}

/// <summary>宿主级选项。</summary>
/// <param name="SaveRoot">存档根目录。</param>
/// <param name="LatencyLog">作答延迟埋点文件路径（见 <see cref="ChoiceLatencyLog"/>）。</param>
public sealed record HostOptions(string SaveRoot, string LatencyLog);

/// <summary>
/// 已启动的会话表。<para>
/// 一个内容包一个会话，<b>按需懒启动</b>：玩家打开 <c>?package=cafe</c> 才会为咖啡馆建引擎，
/// 于是"打开页面"不会顺手把十一个包全加载进内存。会话一旦启动就一直跑
/// ——这正是"关掉浏览器也算挂机"的实现方式。
/// </para>
/// </summary>
internal static class Sessions
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, GameHost> Started = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取（必要时创建）某个内容包的会话；包 id 不认识时返回 <c>null</c>。</summary>
    /// <remarks>不给 <paramref name="packageId"/> 时用目录里的第一个包当默认。</remarks>
    public static GameHost? TryGet(string? packageId, HostOptions options)
    {
        WebPackage? package = packageId is null
            ? PackageCatalog.All.FirstOrDefault()
            : PackageCatalog.Find(packageId);

        if (package is null) return null;

        lock (Gate)
        {
            if (Started.TryGetValue(package.Id, out GameHost? existing)) return existing;

            var host = new GameHost(package, options.SaveRoot, latencyLogPath: options.LatencyLog);
            Started[package.Id] = host;

            if (host.OfflineOnLoad is { CookiesGained: > 0 } offline)
            {
                Console.WriteLine(
                    $"[{package.Id}] 离线 {NumFormat.Duration(offline.CreditedSeconds)}，" +
                    $"补发 {NumFormat.FormatLong(offline.CookiesGained)}。");
            }

            return host;
        }
    }

    /// <summary>关停全部会话（各自存档一次）。由 <c>ApplicationStopping</c> 调用。</summary>
    public static void StopAll()
    {
        GameHost[] hosts;
        lock (Gate)
        {
            hosts = [.. Started.Values];
            Started.Clear();
        }

        foreach (GameHost host in hosts)
        {
            try
            {
                // ApplicationStopping 是同步回调，所以这里同步等：StopAsync 内部的存档
                // 排在游戏线程上，正常情况几毫秒就回来了。
                host.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{host.Package.Id}] 关闭时出错：{ex.Message}");
            }
        }
    }
}
