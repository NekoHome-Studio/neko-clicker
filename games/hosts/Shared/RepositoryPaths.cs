namespace NekoClicker.Hosts;

/// <summary>
/// 仓库根的定位。<para>
/// 两个宿主都有要往仓库里写的东西：<b>存档目录</b>（<c>saves/</c>）与
/// <b>作答延迟埋点文件</b>（<c>artifacts/latency.txt</c>）。这些必须用同一条判据，
/// 否则"埋点写到哪去了"和"存档写到哪去了"会在不同的机器上分叉
/// ——更糟的是，终端宿主与 Web 宿主会各自在**不同的目录**里写出一个
/// <c>artifacts/latency.txt</c>，而"这份文件是唯一的人类数据来源"这句话就不成立了。
/// </para>
/// <para>
/// 判据是"这一层有没有 <c>NekoClicker.sln</c>"，<b>刻意不写死往上几层</b>——
/// 层数会随目标框架、Debug/Release、将来换输出布局而变（这与
/// <c>VersionTests.RepositoryRoot</c> 是同一个判据）。
/// 找不到时返回 <c>null</c>：调用方自己决定退化成什么，而不是在这里悄悄选一个目录。
/// </para>
/// <para>
/// <b>为什么在 <c>games/hosts/Shared/</c> 而不是某一个宿主里</b>：它原先住在
/// <c>Web/</c> 下（那时只有 Web 宿主需要它）。现在终端宿主也要用同一条判据，
/// 于是它跟着埋点一起搬到共享目录，而不是再抄一份。
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
