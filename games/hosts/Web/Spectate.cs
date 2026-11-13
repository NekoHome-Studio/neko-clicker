using NekoClicker.Core;
using NekoClicker.Core.Persistence;
using NekoClicker.Hosts;

namespace NekoClicker.Web;

/// <summary>
/// 只读一瞥（D4 观战 / D15 只读快照）。<para>
/// <b>这里的全部要点是"快照而不建会话"。</b>正常路径 <c>Sessions.TryGet</c> 会给那个包
/// 建一个 <see cref="GameHost"/>——起一条游戏线程、挂上每 60 秒一次的自动存档。于是
/// "拿一份快照看看"这个本该无害的动作，会顺手开一个持续写盘的会话（这正是 D15 登记的缺陷）。
/// </para>
/// <para>
/// 这个类提供另一条路：**临时造一个引擎 → 读档 → 出一帧快照 → 丢弃**。不建会话、不起线程、
/// 不挂自动存档，而且**一个字节都不写磁盘**。它复用引擎与存档那套既有机制，一行游戏规则都不重写：
/// <list type="bullet">
///   <item>读盘那一侧用 <see cref="GameEngine.Load"/>（只读载入，<c>GrantOfflineProgress=false</c>
///         保证不补离线——明信片要的是"存档里那个样子"，不是"接上这段时间"）；</item>
///   <item>分享链接那一侧用 <see cref="SaveManager.Import"/> 配 <see cref="MemoryStorage"/>：
///         九道闸（尺寸/信封/版本/校验和/归属/结构/有限数字）原样复用，而"落盘"那一步写进的是
///         **内存字典**，磁盘分毫未动。</item>
/// </list>
/// </para>
/// <para>
/// <b>为什么这是宿主侧而不是引擎侧</b>：引擎本就提供"造出来、读档、快照"这些无副作用的积木，
/// 缺的只是"别把它们接成会话"这一层宿主决策。所以本文件不碰 <c>engine/core</c>，公开 API 一行不动。
/// 它只用 BCL + Core + 同程序集的 <see cref="ShareLinkCodec"/> / <see cref="SnapshotProtocol"/>，
/// 于是能像 <c>GameHost.cs</c> 一样以共享源码的形式编进测试项目，被 C# 用例直接盯着。
/// </para>
/// </summary>
internal static class Spectate
{
    /// <summary>
    /// 只读地看一眼某个包**磁盘上的存档**（D15 的 <c>?readonly=1</c> 走这里）。<para>
    /// 不建会话、不写盘：没有存档时给一份全新开局的快照，存档损坏时 likewise 退回全新开局
    /// （与游戏读档失败时的兜底同形）。返回的快照与 <c>/api/snapshot</c> 同一套序列化口径。
    /// </para>
    /// </summary>
    /// <param name="package">要看的内容包。</param>
    /// <param name="saveRoot">存档根目录（只读）。</param>
    /// <returns>冻结快照的 JSON 文本。</returns>
    public static string PeekDisk(WebPackage package, string saveRoot)
    {
        GameEngine engine = NewFrozenEngine(package);

        string? json = new FileStorage(saveRoot).Read($"{package.Id}.json");
        if (json is not null)
        {
            try
            {
                engine.Load(json);
            }
            catch (Exception)
            {
                // 存档损坏：与游戏读档失败同形——退回全新开局，而不是把一份堆栈端给探针。
                // 磁盘上的坏存档原样留着（这里本来就一个字节都不写）。
            }
        }

        return SnapshotProtocol.Serialize(engine.Snapshot(PurchaseMode.Buy1));
    }

    /// <summary>
    /// 只读地打开一段**分享令牌**（D4 的 <c>?spectate=</c> 走这里）。<para>
    /// 与 <c>GameHost.ImportShareAsync</c> 用的是**同一个** <see cref="ShareLinkCodec"/> 与
    /// **同一套**导入闸，唯一的不同是：那边导入进一个会写盘的活会话，这边导入进一个
    /// <see cref="MemoryStorage"/> 垫底的临时引擎——于是"看看我的猫"成立，而"把你的存档给我"不成立。
    /// </para>
    /// <para>
    /// 失败时磁盘与内存一个字节都没动（解密在导入之前，导入的落盘又在内存里），
    /// 消息原样用编解码 / 引擎写好的那句（与 <c>ImportShareAsync</c> 同一条约定）。
    /// </para>
    /// </summary>
    /// <param name="package">当前会话声明的内容包（用来做归属闸）。</param>
    /// <param name="token">分享令牌（<c>?spectate=</c> 后面那一段，与 <c>?share=</c> 同一种密文）。</param>
    /// <param name="password">收件人输入的口令。</param>
    /// <param name="now">判定有效期用的当前时刻。</param>
    /// <returns>成功时 <c>Snapshot</c> 是冻结快照 JSON；失败时带精确原因，且什么都没动。</returns>
    public static (bool Ok, string? Snapshot, string Message) PeekShare(
        WebPackage package, string token, string password, DateTimeOffset now)
    {
        ShareLinkOpenResult opened = ShareLinkCodec.Open(
            password, token, ShareLinkPurpose.SaveShare, package.Id, now);
        if (!opened.Ok) return (false, null, opened.Message);

        GameEngine engine = NewFrozenEngine(package);
        using var saves = new SaveManager(engine, new MemoryStorage(), $"{package.Id}.json")
        {
            PackId = package.Id,
        };

        SaveTransferResult result = saves.Import(opened.Payload!.SaveText!);
        if (!result.Ok) return (false, null, result.Message);

        return (true, SnapshotProtocol.Serialize(engine.Snapshot(PurchaseMode.Buy1)), result.Message);
    }

    /// <summary>
    /// 造一个"冻结"的临时引擎：不补离线收益（明信片要存档里那个样子），其余与活会话同参。
    /// 构造 <see cref="GameEngine"/> 本身不起线程——线程是 <see cref="GameHost"/> 才起的，
    /// 所以这里造完、读完、快照完就丢，没有任何后台推进。
    /// </summary>
    private static GameEngine NewFrozenEngine(WebPackage package)
        => new(package.Build(), new GameEngineOptions
        {
            Clock = SystemClock.Instance,
            Seed = 0,
            GrantOfflineProgress = false,
            MaxNotifications = 64,
        });
}
