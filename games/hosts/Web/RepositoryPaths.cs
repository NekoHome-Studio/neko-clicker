namespace NekoClicker.Web;

/// <summary>
/// 仓库根的定位。<para>
/// Web 宿主有两处要往仓库里写东西：<b>存档目录</b>（<c>saves/</c>）与
/// <b>作答延迟埋点文件</b>（<c>artifacts/latency.txt</c>）。两处必须用同一条判据，
/// 否则"埋点写到哪去了"和"存档写到哪去了"会在不同的机器上分叉。
/// </para>
/// <para>
/// 判据是"这一层有没有 <c>NekoClicker.sln</c>"，<b>刻意不写死往上几层</b>——
/// 层数会随目标框架、Debug/Release、将来换输出布局而变（这与
/// <c>VersionTests.RepositoryRoot</c> 是同一个判据）。
/// 找不到时返回 <c>null</c>：调用方自己决定退化成什么，而不是在这里悄悄选一个目录。
/// </para>
/// </summary>
internal static class RepositoryPaths
{
    /// <summary>从程序集所在目录一路往上找仓库根；找不到返回 <c>null</c>。</summary>
    public static string? Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NekoClicker.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }
}
