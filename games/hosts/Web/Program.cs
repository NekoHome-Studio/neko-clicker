using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using NekoClicker.Core;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Views;

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

    /// <summary>启动宿主。</summary>
    /// <param name="args">
    /// <c>--urls &lt;地址&gt;</c> 换监听地址；<c>--save-root &lt;目录&gt;</c> 换存档根目录。
    /// </param>
    /// <returns>进程退出码。</returns>
    public static int Main(string[] args)
    {
        string url = ReadOption(args, "--urls") ?? ReadOption(args, "--url") ?? DefaultUrl;
        string saveRoot = Path.GetFullPath(ReadOption(args, "--save-root") ?? DefaultSaveRoot());

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(url);
        builder.Services.AddSingleton(new HostOptions(saveRoot));

        WebApplication app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        MapApi(app);

        // 关停时必须存一次盘：自动存档是每 60 秒一次，正常退出不存的话
        // 那最多 60 秒的进度连"离线收益"都拿不到（MinimumOfflineSeconds 恰好也是 60）。
        IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(Sessions.StopAll);

        Console.WriteLine($"NekoClicker Web 宿主已启动：{url}");
        Console.WriteLine($"框架版本 {ApiVersion.Current}｜内容包 {PackageCatalog.All.Count} 个｜存档目录 {saveRoot}");
        Console.WriteLine($"打开 {url}/ 开始玩；换包用 {url}/?package=<id>。Ctrl+C 退出。");

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
            return Results.Json(new { ok = outcome.Ok, message = outcome.Message, seq = outcome.Seq }, SnapshotProtocol.Options);
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
                    return new CommandOutcome(ok, ok ? "已表态。" : "这次表态不成立（可能已经答过，或那层已经过去了）。", host.Seq);
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

            case "hardReset":
                await host.SaveAsync().ConfigureAwait(false);
                return await host.ExecuteAsync(engine =>
                {
                    engine.HardReset();
                    return new CommandOutcome(true, "已重置，存档也已覆盖。", host.Seq);
                }).ConfigureAwait(false);

            default:
                return new CommandOutcome(false, $"不认识的命令：{type}", host.Seq);
        }

        static CommandOutcome Missing(string what) => new(false, $"这条命令缺少参数 {what}。", 0);
    }

    /// <summary>
    /// 存档根目录：从程序集所在目录往上找仓库根（有 <c>NekoClicker.sln</c> 的那一层）。<para>
    /// 刻意用与 <c>VersionTests.RepositoryRoot</c> 相同的判据、不写死"往上几层"——
    /// 层数会随目标框架、Debug/Release、将来换输出布局而变。
    /// 找不到时退化成当前目录下的 <c>saves/</c>，至少不会写到别处去。
    /// </para>
    /// </summary>
    private static string DefaultSaveRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NekoClicker.sln")))
            {
                return Path.Combine(directory.FullName, "saves");
            }

            directory = directory.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "saves");
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
public sealed record HostOptions(string SaveRoot);

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

            var host = new GameHost(package, options.SaveRoot);
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
