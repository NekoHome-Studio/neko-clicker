using System.Reflection;

namespace NekoClicker.Core;

/// <summary>
/// 框架自身的版本号。<para>
/// 这个值是 <b>NekoClicker.Core 对外承诺的公开 API 版本</b>，不是某个内容包或 Demo 的版本。
/// 宿主（终端 / Web / Unity）可以把它显示在"关于"里，也可以写进自己那份存档或日志，
/// 这样"用户报的那个 bug 是在哪个框架版本上出现的"就不需要靠猜。
/// </para>
/// <para>
/// 版本的语义（什么改动升哪一位）写在 <c>engine/docs/VERSIONING.md</c>；
/// 公开 API 的快照守卫是 <c>PublicApiTests</c>，快照文件是
/// <c>engine/core/PublicApi.txt</c>。
/// </para>
/// </summary>
public static class ApiVersion
{
    /// <summary>语义版本字符串，例如 <c>1.0.0</c>。由构建期的 <c>Version</c> 属性写入程序集。</summary>
    public static string Current { get; } = ReadInformationalVersion();

    /// <summary>程序集版本（四段式，例如 <c>1.0.0.0</c>）。</summary>
    public static Version AssemblyVersion { get; } = typeof(ApiVersion).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>主版本号。它变化意味着有不兼容的公开 API 改动。</summary>
    public static int Major => AssemblyVersion.Major;

    /// <summary>次版本号。它变化意味着公开 API 只增不改。</summary>
    public static int Minor => AssemblyVersion.Minor;

    /// <summary>修订号。它变化意味着公开 API 一行没动。</summary>
    public static int Patch => Math.Max(AssemblyVersion.Build, 0);

    private static string ReadInformationalVersion()
    {
        Assembly assembly = typeof(ApiVersion).Assembly;

        // InformationalVersion 在 .NET 8 上会被追加 "+<commit sha>"（SourceLink 风格）。
        // 版本号本身不该带构建元数据，这里按 SemVer 规范把 '+' 之后的部分去掉。
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        int plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
