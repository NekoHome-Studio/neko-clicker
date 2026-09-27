using System.Reflection;
using NekoClicker.Core;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 版本号守卫。<para>
/// 版本号只有在"它确实约束住什么东西"时才是承诺，否则只是一句愿望。
/// 这一组用例把 <c>Directory.Build.props</c> 里那个 <c>Version</c> 与三样东西锁在一起：
/// 程序集元数据、公开 API 快照、CHANGELOG。改版本号会同时牵动它们，绕不过去。
/// </para>
/// <para>
/// 版本的语义（什么改动升哪一位）写在 <c>engine/docs/VERSIONING.md</c>。
/// </para>
/// </summary>
public static class VersionTests
{
    /// <summary>框架已经发布了 1.0.0，版本号不该再退回 0.x（0.x 意味着"公开 API 随时会变"）。</summary>
    [Test]
    public static void Version_IsAtLeastOneZeroZero()
    {
        Version version = typeof(ApiVersion).Assembly.GetName().Version ?? new Version(0, 0);

        Check.AtLeast(version.Major, 1, $"框架已对外承诺 API 稳定，主版本号不该是 0（当前 {version}）。");
    }

    /// <summary>程序集版本必须由 <c>Directory.Build.props</c> 的 <c>Version</c> 推导出来，不能各说各话。</summary>
    [Test]
    public static void Version_AssemblyMetadataMatchesTheReportedVersion()
    {
        Version assemblyVersion = typeof(ApiVersion).Assembly.GetName().Version ?? new Version(0, 0);

        // 三段式比较：AssemblyVersion 是四段（1.0.0.0），语义版本是三段。
        Check.Equal(ApiVersion.Major, assemblyVersion.Major, "ApiVersion.Major 与程序集版本不一致。");
        Check.Equal(ApiVersion.Minor, assemblyVersion.Minor, "ApiVersion.Minor 与程序集版本不一致。");
        Check.Equal(ApiVersion.Patch, Math.Max(assemblyVersion.Build, 0), "ApiVersion.Patch 与程序集版本不一致。");

        Check.Equal(
            $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(assemblyVersion.Build, 0)}",
            ApiVersion.Current,
            "ApiVersion.Current 不是「主.次.修订」三段式。");
    }

    /// <summary>
    /// CHANGELOG 里必须有当前版本的条目。<para>
    /// 使用者升级前看的就是这份文件；版本号升了却没有条目，等于让人自己去 diff。
    /// </para>
    /// </summary>
    [Test]
    public static void Changelog_HasAnEntryForTheCurrentVersion()
    {
        string root = RepositoryRoot();
        string path = Path.Combine(root, "CHANGELOG.md");

        Check.True(File.Exists(path), $"找不到 CHANGELOG.md（期望在 <{path}>）。");

        string text = File.ReadAllText(path);

        // 只要形如 "## [1.0.0]" 的标题，避免 "1.0.0" 恰好出现在别的段落里也算过。
        Check.Contains(
            text,
            $"## [{ApiVersion.Current}]",
            $"CHANGELOG.md 里没有 {ApiVersion.Current} 的条目（需要一行形如 `## [{ApiVersion.Current}] - YYYY-MM-DD` 的标题）。");

        // 顺便要求它写了日期：没有日期的变更日志无法判断"我用的这版是不是这条"。
        string line = text
            .Split('\n')
            .FirstOrDefault(candidate => candidate.StartsWith($"## [{ApiVersion.Current}]", StringComparison.Ordinal))
            ?? string.Empty;

        Check.True(
            line.Contains(" - ", StringComparison.Ordinal) && line.Contains("20", StringComparison.Ordinal),
            $"CHANGELOG.md 里 {ApiVersion.Current} 的标题没有日期：<{line.Trim()}>。");
    }

    /// <summary>
    /// README 顶部写着的"当前版本"必须是真版本。<para>
    /// 这是使用者第一眼看到的东西：一个说自己落后于实际版本的 README，
    /// 比没有版本号更糟——它主动给出错误信息。锚定在第一个 <c>`x.y.z`</c> 反引号片段上，
    /// 因为 README 开头那句「**当前版本 `1.0.0`**」就是全文第一次出现版本号的地方。
    /// </para>
    /// </summary>
    [Test]
    public static void Readme_AdvertisesTheCurrentVersion()
    {
        string path = Path.Combine(RepositoryRoot(), "README.md");
        Check.True(File.Exists(path), $"找不到 README.md（期望在 <{path}>）。");

        string text = File.ReadAllText(path);

        // 找第一个形如 `1.0.0` 的反引号片段——即 README 里第一次出现的语义版本号。
        int start = -1;
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] != '`') continue;
            if (!char.IsDigit(text[i + 1])) continue;
            start = i + 1;
            break;
        }

        Check.AtLeast(start, 1, "README.md 里找不到形如 `1.0.0` 的版本号片段。");

        int end = text.IndexOf('`', start);
        Check.AtLeast(end, start, "README.md 里的版本号片段没有收尾反引号。");

        string advertised = text[start..end];
        Check.Equal(
            ApiVersion.Current,
            advertised,
            $"README.md 顶部写的当前版本是 <{advertised}>，但程序集版本是 <{ApiVersion.Current}>。升版本时请一并更新 README。");
    }

    /// <summary>
    /// 仓库根目录：从程序集所在目录往上找 <c>NekoClicker.sln</c>。<para>
    /// 刻意不写死"往上几层"——层数会随目标框架、Debug/Release、以及将来换输出布局而变，
    /// 写死的话这条用例会在某次无关的改动后悄悄指错地方。
    /// </para>
    /// </summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NekoClicker.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        Check.Fail(
            $"从 <{AppContext.BaseDirectory}> 一路往上都没找到 NekoClicker.sln，无法定位仓库根目录。");

        return string.Empty;
    }
}
