using System.Reflection;
using NekoClicker.Core.Content;

namespace NekoClicker.Web;

/// <summary>
/// 一个可玩的内容包。宿主只认这四样东西——它不知道"咖啡馆"有什么建筑，
/// 也不需要知道：<c>GameSnapshot</c> 里已经把该显示的都算好了。
/// </summary>
/// <param name="Id">URL 参数值（<c>?package=&lt;id&gt;</c>）。</param>
/// <param name="Name">显示名（选择页用）。</param>
/// <param name="Build">内容构建函数（宿主调用它，而不是内容包自己注册）。</param>
/// <param name="Welcome">开局欢迎语。</param>
public sealed record WebPackage(string Id, string Name, Func<GameContent> Build, string Welcome);

/// <summary>
/// 内容包目录。<para>
/// <b>运行时扫描发现，不是硬编码清单。</b>这一点与 <c>Demo.Cli</c> 那边刻意不同：
/// 那边有一张手写的 <c>ContentPackages</c> 表，每条还带着该包语气的文案
/// （欢迎语、帮助要点、无头报告提示）。宿主这边**不该有那些东西**——
/// Web 前端要显示的所有文字都在 <c>GameSnapshot</c> 里，所以这里只需要"怎么把一个包造出来"，
/// 于是它可以完全由运行时发现得到：加一个新内容包不需要改这个宿主。
/// </para>
/// <para>
/// 发现规则：输出目录里每个 <c>NekoClicker.Content.*.dll</c>，找出公开静态类上的
/// <c>public static GameContent Build()</c> 方法。id 由程序集名推出
/// （<c>NekoClicker.Content.NineLives</c> → <c>ninelives</c>）。
/// </para>
/// </summary>
public static class PackageCatalog
{
    private static readonly Lazy<IReadOnlyList<WebPackage>> Cached = new(Discover);

    /// <summary>全部可玩的内容包，按 id 排序。</summary>
    public static IReadOnlyList<WebPackage> All => Cached.Value;

    /// <summary>按 id 找一个包（忽略大小写）；找不到返回 <c>null</c>。</summary>
    public static WebPackage? Find(string? id)
        => id is null ? null : All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<WebPackage> Discover()
    {
        var packages = new List<WebPackage>();

        foreach (string path in Directory.EnumerateFiles(AppContext.BaseDirectory, "NekoClicker.Content.*.dll"))
        {
            foreach (Type type in LoadTypes(path))
            {
                MethodInfo? build = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
                if (build is null || !typeof(GameContent).IsAssignableFrom(build.ReturnType)) continue;

                string id = DeriveId(path);
                string title = ReadTitle(build) ?? id;

                packages.Add(new WebPackage(
                    Id: id,
                    Name: title,
                    Build: () => (GameContent)build.Invoke(null, null)!,
                    Welcome: $"已载入《{title}》。"));
            }
        }

        packages.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return packages;
    }

    private static IEnumerable<Type> LoadTypes(string path)
    {
        Type[] types;
        try
        {
            types = Assembly.LoadFrom(path).GetExportedTypes();
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or TypeLoadException)
        {
            yield break; // 不是本仓库的内容程序集：跳过，别把宿主拖垮
        }

        foreach (Type type in types) yield return type;
    }

    /// <summary>从程序集名推出 URL 里的 id：<c>NekoClicker.Content.NineLives</c> → <c>ninelives</c>。</summary>
    private static string DeriveId(string assemblyPath)
    {
        string name = Path.GetFileNameWithoutExtension(assemblyPath);
        const string prefix = "NekoClicker.Content.";
        return (name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name).ToLowerInvariant();
    }

    /// <summary>读内容的标题。构建失败时退化成程序集名，不让一个坏包拖垮整个目录。</summary>
    private static string? ReadTitle(MethodInfo build)
    {
        try
        {
            return (build.Invoke(null, null) as GameContent)?.Title;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
