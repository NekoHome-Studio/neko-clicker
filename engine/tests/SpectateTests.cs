using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Core.Persistence;
using NekoClicker.Hosts;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 只读一瞥（D4 观战 / D15 只读快照）的宿主接线。<para>
/// <b>为什么非要一条这样的用例</b>：D15 登记的缺陷正是"拿快照当探针会静默改写真人进度"——
/// <c>Sessions.TryGet</c> 会给那个包建一个活会话，而会话每 60 秒整份重写 <c>saves/&lt;包&gt;.json</c>。
/// 修法是 <see cref="Spectate"/>：临时造引擎 → 读档 → 出一帧快照 → 丢弃，不建会话、不起线程、
/// 不写盘。这条性质（**读了但什么都没改**）此前没有任何东西钉着；HTTP 那一面另由
/// <c>tools/api-test.ps1</c> 的观战一节盯真链路，两条都要（与 WebBuyModeTests 同一套分工）。
/// </para>
/// <para>
/// 全部走临时目录：仓库里的 <c>saves/</c> 是真人正在玩的进度，这些用例一个字节都不碰它。
/// </para>
/// </summary>
public static class SpectateTests
{
    /// <summary>
    /// PeekDisk 真的读到了磁盘上那份存档，而且**没有动过它**。<para>
    /// 做法：用真 FileStorage 落一份有内容的存档（灌一笔钱 + 点几下），记下 sha256，
    /// 再 PeekDisk 取快照——断言快照里能看到那笔钱，而文件的哈希前后逐字节相同。
    /// "哈希不变"就是 D15 那句话（"拿快照当探针不该带起存档"）的可执行形态。
    /// </para>
    /// </summary>
    [Test]
    public static void PeekDisk_ReadsTheSaveAndTouchesNothingOnDisk()
    {
        string root = NewTempDir();
        try
        {
            // ① 先用一个活会话把存档落成"真的玩过一局"的样子（这是夹具，不是被测路径）。
            var pack = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
            var writer = new GameHost(pack, saveRoot: root, seed: 7, latencyLogPath: null);
            try
            {
                writer.ExecuteAsync(engine =>
                {
                    engine.State.Cookies = 123_456;
                    engine.State.CookiesEarnedAllTime = 123_456;
                    engine.MarkDirty();
                    return new CommandOutcome(true, string.Empty, 0);
                }).GetAwaiter().GetResult();
                writer.SaveAsync().GetAwaiter().GetResult();
            }
            finally
            {
                writer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            string slot = Path.Combine(root, "neko.json");
            Check.True(File.Exists(slot), "前置条件：夹具真的落了一份存档。");
            byte[] before = Convert.FromHexString(Sha256OfFile(slot));
            // 记下 peek **之前**目录里都有什么（夹具那次落盘会留下 neko.json 与它的 .bak）。
            var entriesBefore = new SortedSet<string>(Directory.GetFiles(root, "*", SearchOption.AllDirectories), StringComparer.Ordinal);

            // ② 只读一瞥：必须看见那笔钱。
            string json = Spectate.PeekDisk(pack, root);
            JsonObject? snapshot = JsonNode.Parse(json) as JsonObject;
            Check.NotNull(snapshot, "PeekDisk 必须回一份合法的快照 JSON。");
            double cookies = snapshot!["cookies"]?.GetValue<double>() ?? -1;
            Check.Close(123_456, cookies, 1e-9, "只读一瞥必须读到磁盘上那份存档的内容（而不是全新开局）。");

            // ③ 而且一个字节都没动——这正是 D15 要的那条性质。
            byte[] after = Convert.FromHexString(Sha256OfFile(slot));
            Check.Equal(Convert.ToHexString(before), Convert.ToHexString(after),
                "PeekDisk 之后存档文件必须逐字节相同：'拿快照当探针会静默改写真人进度'（D15）从此被钉住。");

            // ④ 也不许在盘上新增任何条目（.bak / .tmp 之类写盘的脚印）——按"peek 前后差集"判，
            //    而不是笼统禁止 .bak：夹具那次真落盘本来就会留下 .bak，那不是被测路径的锅。
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                Check.True(entriesBefore.Contains(file),
                    $"PeekDisk 不该新建文件：<>{file} 在 peek 之前不存在。");
            }
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 没有存档时 PeekDisk 给的是全新开局，而不是抛。<para>
    /// 探针打到空槽位是常态（比如刚打包解压、还没玩过），坏输入被说成内部错误是这个仓库最反对的形状。
    /// </para>
    /// </summary>
    [Test]
    public static void PeekDisk_OnAnEmptySlot_ReturnsAFreshSnapshotWithoutThrowing()
    {
        string root = NewTempDir();
        try
        {
            var pack = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
            string json = Spectate.PeekDisk(pack, root);
            JsonObject? snapshot = JsonNode.Parse(json) as JsonObject;
            Check.NotNull(snapshot, "空槽位也要回一份合法快照。");
            Check.Equal(0d, snapshot!["cookies"]?.GetValue<double>() ?? -1, "空槽位的快照应当是全新开局（0 货币）。");
            Check.Equal(0, Directory.GetFiles(root).Length, "peek 一个空槽位不许在盘上留下任何东西。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 坏存档不炸：退回全新开局，磁盘上那份坏的**原样留着**。<para>
    /// 与游戏本体"读档失败保留当前进度"那条兜底同形。这条防的是把"文件坏了"翻成一个 500。
    /// </summary>
    [Test]
    public static void PeekDisk_OnACorruptSave_FallsBackToFreshAndLeavesTheFileAlone()
    {
        string root = NewTempDir();
        try
        {
            var pack = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
            string slot = Path.Combine(root, "neko.json");
            File.WriteAllText(slot, "{ this is not a save ");

            string json = Spectate.PeekDisk(pack, root);
            JsonObject? snapshot = JsonNode.Parse(json) as JsonObject;
            Check.NotNull(snapshot, "坏存档也要回一份合法快照（全新开局），而不是抛出去。");
            Check.Contains(File.ReadAllText(slot), "not a save", "磁盘上那份坏存档必须原样留着——只读连'清理坏文件'都不许做。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// PeekShare：对密码打开分享令牌，得到的是**发件人那一刻**的冻结快照。<para>
    /// 密文用与宿主同一个 <see cref="ShareLinkCodec"/> 铸（purpose/packId 同一条路），
    /// 于是这里验的就是线上真正会吃到的那种链接。
    /// </para>
    /// </summary>
    [Test]
    public static void PeekShare_WithTheRightPassword_ReturnsTheSendersFrozenSnapshot()
    {
        var pack = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");

        // 铸一份"玩过的"导出文本：直接拿引擎 Save() 过信封（与 SaveManager.Export 同一条序列化）。
        GameEngine sender = TestGame.CreateNeko(out _, seed: 11);
        sender.State.Cookies = 987_654;
        sender.State.CookiesEarnedAllTime = 987_654;
        string envelope = SaveTransfer.Wrap(sender.Save(), "neko", ApiVersion.Current, DateTimeOffset.UnixEpoch);
        string token = ShareLinkCodec.Protect("correct horse", ShareLinkPurpose.SaveShare, "neko", envelope);

        (bool ok, string? snapshot, string message) =
            Spectate.PeekShare(pack, token, "correct horse", DateTimeOffset.UtcNow);

        Check.True(ok, $"对密码必须打得开：{message}");
        Check.NotNull(snapshot, "成功时必须带回快照。");
        JsonObject? view = JsonNode.Parse(snapshot!) as JsonObject;
        Check.Equal(987_654d, view!["cookies"]?.GetValue<double>() ?? -1,
            "观战看到的必须是发件人存档里那个数（冻结明信片，不是收件人的会话）。");
    }

    /// <summary>
    /// 错密码明确拒绝，并且什么都不产出。<para>
    /// 与导入那条线同口径：失败分类来自 ShareLinkCodec 原文，界面不许猜。
    /// </para>
    /// </summary>
    [Test]
    public static void PeekShare_WithTheWrongPassword_RefusesClearly()
    {
        var pack = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
        GameEngine sender = TestGame.CreateNeko(out _, seed: 11);
        string envelope = SaveTransfer.Wrap(sender.Save(), "neko", ApiVersion.Current, DateTimeOffset.UnixEpoch);
        string token = ShareLinkCodec.Protect("right password", ShareLinkPurpose.SaveShare, "neko", envelope);

        (bool ok, string? snapshot, string message) =
            Spectate.PeekShare(pack, token, "wrong password", DateTimeOffset.UtcNow);

        Check.False(ok, "错密码绝不能算打开成功。");
        Check.Null(snapshot, "被拒时不许有任何快照产物。");
        Check.True(message.Length > 0, "拒绝必须说一句人话。");
    }

    /// <summary>
    /// 别的包的令牌在本包上打不开：归属闸照旧拦得住（复用九道闸的意义就在这）。<para>
    /// 信封声明 cafe、当前会话是 neko ⇒ ForeignPack。若哪天有人给观战另写一套校验，
    /// 这条会红——那正是要防的分叉。
    /// </summary>
    [Test]
    public static void PeekShare_FromAnotherPack_IsRefusedByTheOwnershipGate()
    {
        var neko = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
        GameEngine sender = TestGame.CreateCafe(out ManualClock _, seed: 11);
        string envelope = SaveTransfer.Wrap(sender.Save(), "cafe", ApiVersion.Current, DateTimeOffset.UnixEpoch);
        string token = ShareLinkCodec.Protect("pw1234", ShareLinkPurpose.SaveShare, "cafe", envelope);

        (bool ok, string? snapshot, string message) =
            Spectate.PeekShare(neko, token, "pw1234", DateTimeOffset.UtcNow);

        Check.False(ok, "咖啡馆的令牌不许在 neko 的观战里打开。");
        Check.Null(snapshot, "跨包被拒时不许有快照。");
        Check.Contains(message, "cafe", "拒绝的话里要说出那是哪个包的链接（与导入同一条文案）。");
    }

    // ------------------------------------------------------------------ 工具

    private static string Sha256OfFile(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "neko-spectate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录没清掉不影响结论。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
