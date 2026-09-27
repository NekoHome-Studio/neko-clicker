using System.Reflection;
using System.Text;
using System.Text.Json;
using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Web;

/// <summary>
/// Web 前端宿主的入口。
///
/// 这一版是**骨架**：它证明"引擎 → 浏览器"这条线是通的，并且把真正需要定的两个决策
/// （推快照的频率、HTTP 与 SSE 的边界）暴露出来，但刻意不替使用者决定。
/// 游戏规则一行都没有实现——宿主只做三件事：托管静态文件、报框架版本、报本机有哪些内容包。
/// </summary>
public static class Program
{
    private const string DefaultUrl = "http://127.0.0.1:5273";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // 中文文案在 JSON 里保持可读（引擎侧的 SaveSerializer 用默认转义，这里刻意不同：
        // 这是给人看的调试端点，不是存档格式）。
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>启动宿主。</summary>
    /// <param name="args">命令行参数：<c>--urls &lt;地址&gt;</c> 可换监听地址。</param>
    /// <returns>进程退出码。</returns>
    public static int Main(string[] args)
    {
        string url = ReadUrls(args) ?? DefaultUrl;

        // 刻意**不覆盖** ContentRootPath / WebRootPath：Web SDK 在开发期用 bin 里的
        // *.staticwebassets.endpoints.json 清单解析 wwwroot（源码目录），只有在 publish
        // 时才把 wwwroot 复制到输出目录。硬指到 AppContext.BaseDirectory 会把这个机制整个
        // 绕开，结果是 /api/* 一切正常而首页 404 —— 一个编译期完全看不出来的沉默失败。
        // 宿主因此用 `dotnet run --project`（或发布产物）启动，见 tools/web.ps1。
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // 只监听回环地址：这是一个单机宿主，不开局域网端口，也就不触发防火墙询问。
        builder.WebHost.UseUrls(url);

        WebApplication app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // ① 框架自证：宿主能在运行时报出引擎版本（阶段 6 加 ApiVersion 就是为了这件事）。
        app.MapGet("/api/ping", () => Results.Json(new
        {
            app = "neko-clicker-web",
            apiVersion = ApiVersion.Current,
            assemblyVersion = ApiVersion.AssemblyVersion,
            runtime = Environment.Version.ToString(),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        }, Json));

        // ② 内容包发现：宿主不认识内容，只在运行时看有哪些 Content.* 程序集在。
        app.MapGet("/api/packs", () => Results.Json(DiscoverPacks(), Json));

        Console.WriteLine($"NekoClicker Web 宿主已启动：{url}");
        Console.WriteLine($"框架版本 {ApiVersion.Current}｜内容包 {DiscoverPacks().Count} 个");
        Console.WriteLine("（骨架版：还没有游戏界面。Ctrl+C 退出。）");

        app.Run();
        return 0;
    }

    private static string? ReadUrls(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--urls" or "--url") return args[i + 1];
        }
        return null;
    }

    private static List<PackInfo> DiscoverPacks()
    {
        var packs = new List<PackInfo>();
        string dir = AppContext.BaseDirectory;

        foreach (string path in Directory.EnumerateFiles(dir, "NekoClicker.Content.*.dll"))
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                foreach (Type type in assembly.GetExportedTypes())
                {
                    // 内容包的约定形态：一个公开静态类，带 public static GameContent Build()。
                    MethodInfo? build = type.GetMethod(
                        "Build", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
                    if (build is null || !typeof(GameContent).IsAssignableFrom(build.ReturnType)) continue;

                    packs.Add(new PackInfo(type.FullName ?? type.Name, Path.GetFileNameWithoutExtension(path)));
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or TypeLoadException)
            {
                // 不是本仓库的内容程序集就跳过，不让它把宿主拖垮。
            }
        }

        packs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return packs;
    }

    private sealed record PackInfo(string Id, string Assembly);
}
