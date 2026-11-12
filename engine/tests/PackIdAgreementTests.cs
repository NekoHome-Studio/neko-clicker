using NekoClicker.Content.NineLives;
using NekoClicker.Demo.Cli;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 两个宿主的内容包目录必须说<b>同一套 id</b>（D16）。<para>
/// D16 是这条守卫的出生证明：同一个包（《九命轮回》）在终端注册表里叫 <c>ninelines</c>、
/// 在 Web 目录的旧规则下却从程序集名 <c>NineLives</c> 推导出 <c>ninelives</c>——
/// 同一个包两个 id、两份存档、跨宿主导入/导出与分享链接全被"别的包"闸拦住。
/// 修复后的形状是：**内容包自己声明 id**（<see cref="NineLivesContent.PackageId"/>），
/// Web 目录声明优先、程序集名只做兜底，终端注册表直接引用声明。
/// </para>
/// <para>
/// 这份守卫验的是"两套目录不会再次分叉"：两边对每个包说出的 id 集合必须逐项相等。
/// 它红的时候，要么有人改了注册表忘了声明（或反之），要么某个包的程序集名与声明再次分叉。
/// </para>
/// <para>
/// 注：这条只能在 C# 层钉。Web 目录的"运行时扫描"发生在测试程序集里
/// （测试工程以共享源码编进 <see cref="PackageCatalog"/>，它扫描的是测试 bin
/// ——那里同样落着全部内容包 dll，见 csproj 的 ProjectReference），所以这里的
/// 目录内容与真宿主一致；HTTP 那一面（<c>tools/api-test.ps1</c> 的令牌节）另有一层。
/// </para>
/// </summary>
public static class PackIdAgreementTests
{
    /// <summary>
    /// 两个宿主目录的 id 集合必须逐项相等。<para>
    /// 这一条红的时候读得懂它防的事：D16 当天的形状就是终端有 <c>ninelines</c>、
    /// Web 有 <c>ninelives</c>——集合差一步就是两份存档。
    /// </para>
    /// </summary>
    [Test]
    public static void HostCatalogs_RegisterTheSameIdSet()
    {
        string[] terminal = [.. ContentPackages.All.Select(p => p.Id)];
        string[] web = [.. PackageCatalog.All.Select(p => p.Id)];

        Check.Equal(
            string.Join(",", terminal.OrderBy(x => x)),
            string.Join(",", web.OrderBy(x => x)),
            "两个宿主的内容包目录必须说同一套 id（D16：九命轮回曾一边 ninelines 一边 ninelives）");
    }

    /// <summary>
    /// 终端目录里的每个包，在 Web 目录里必须能按同一个 id 找到；反过来也一样。<para>
    /// 与上一条分开写，是因为"集合相等"与"能按 id 找回"是两件事——
    /// Find 走的是各自宿主的查找路径（大小写不敏感），分叉可以藏在查找那一层。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryPackIsFindableByTheSameIdOnBothHosts()
    {
        foreach (ContentPackage pack in ContentPackages.All)
        {
            WebPackage? onWeb = PackageCatalog.Find(pack.Id);
            Check.True(
                onWeb is not null,
                $"终端的包「{pack.Id}」在 Web 目录里必须按同一个 id 找得到（D16）");
        }

        foreach (WebPackage pack in PackageCatalog.All)
        {
            ContentPackage? onTerminal = ContentPackages.Find(pack.Id);
            Check.True(
                onTerminal is not null,
                $"Web 目录的包「{pack.Id}」在终端注册表里必须按同一个 id 找得到（D16）");
        }
    }

    /// <summary>
    /// 声明的 id 必须逐字节等于文档与注册表沿用的那个：<c>ninelines</c>。<para>
    /// 故意写死而不是引用 <see cref="NineLivesContent.PackageId"/>——
    /// 那样的话有人把声明和这一条一起改掉，守卫就跟着走了。它钉的是
    /// 「存档目录、文档、两个宿主都已沿用的那个字符串」，改动它必须是
    /// 一次显式的迁移决定（含存档迁移），不是顺手改个常量。
    /// </para>
    /// </summary>
    [Test]
    public static void NineLives_PackageId_IsTheCanonicalNinelines()
    {
        Check.Equal("ninelines", NineLivesContent.PackageId,
            "D16 拍板的统一 id 是 ninelines（终端注册表、Web 目录、文档与既有存档都叫它这个）。"
            + "改它等于改全部玩家的存档名——那是一次显式迁移，不是改常量。");
    }
}
