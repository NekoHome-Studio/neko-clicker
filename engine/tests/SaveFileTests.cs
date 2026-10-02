using NekoClicker.Core.Events;
using NekoClicker.Core.Persistence;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 落盘存档的耐久性：<b>坏存档不许成为当前存档，好存档不许被无声地覆盖掉。</b><para>
/// 这几条不变量各自对应一件以前会<b>静默</b>发生的事故：
/// </para>
/// <list type="number">
///   <item><b>提交之前先反解一遍</b>：一份写出来读不回来的存档以前会直接盖掉当前存档。
///         代价不在写的那一刻出现，而在下一局启动——那时上一份好存档已经没了。</item>
///   <item><b>替换之前留下上一份</b>：<c>File.Move(temp, path, overwrite: true)</c> 之后，
///         "上一次存的那份"在磁盘上不再存在，出事时没有任何东西可以退回去。</item>
///   <item><b>备份必须是验证过的好存档</b>：只按"文件在不在"来认"上一份"，
///         会把一份读不回来的东西请进 <c>.bak</c>，让备份变成一句谎话——
///         而它恰恰是你唯一会去指望的那份文件。</item>
/// </list>
/// <para>
/// 全部用临时目录（<see cref="NewTempDir"/>），跑完即删。仓库里的 <c>saves/</c> 是真人正在玩的进度，
/// 这些用例一个字节都不碰它：写测试宿主自己起的进程之外，没有任何东西会写那里。
/// </para>
/// </summary>
public static class SaveFileTests
{
    private const string Slot = "slot.json";
    private const string BackupPath = Slot + ".bak";
    private const string TempPath = Slot + ".tmp";

    /// <summary>正常存档：经 FileStorage 存一次、再读回来，进度一致（改写替换原语没有碰坏普通路径）。</summary>
    [Test]
    public static void SaveFile_RoundTripsThroughFileStorage()
    {
        string root = NewTempDir();
        try
        {
            GameEngine engine = TestGame.CreateMini(out _);
            engine.State.Cookies = 1_234;
            engine.MarkDirty();

            using var manager = new SaveManager(engine, new FileStorage(root), Slot);
            Check.True(manager.Save(), "存档应当成功。");

            GameEngine restored = TestGame.CreateMini(out _);
            using var reader = new SaveManager(restored, new FileStorage(root), Slot);
            Check.True(reader.Load(), "刚存下去的那份必须读得回来。");
            Check.Close(1_234, restored.State.Cookies, 1e-9);
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 第一次存档：目标不存在，<c>File.Replace</c> 的前提不成立，走的必须是另一条路。<para>
    /// 同时钉住"没有上一份就不该造备份"——凭空造一个空/半截的 <c>.bak</c>，
    /// 会让"有备份"这件事本身失去意义。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveFile_FirstSaveWorksAndHasNoBackup()
    {
        string root = NewTempDir();
        try
        {
            var storage = new FileStorage(root);
            storage.Write(Slot, SaveJson(cookies: 111));

            Check.True(File.Exists(Path.Combine(root, Slot)), "第一次存档就该把文件写出来。");
            Check.False(
                File.Exists(Path.Combine(root, BackupPath)),
                "第一次存档没有更早的一份可留，不该凭空造一个备份出来。");
            Check.False(File.Exists(Path.Combine(root, TempPath)), "提交成功之后不该留下临时文件。");

            string? written = storage.Read(Slot);
            Check.NotNull(written, "写完要读得到。");
            if (written is null) return;
            Check.Close(111, SaveSerializer.Parse(written).Cookies, 1e-9, "写出来的必须反解得回去。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 覆盖既有存档：被换下来的那一份必须留在 <c>.bak</c> 里，而且它<b>自己也要读得回来</b>。<para>
    /// "文件还在"与"这份文件有用"是两件事——只验证前者，等于把备份的价值赌在没人动过它上面。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveFile_OverwriteKeepsThePreviousContentAsAVerifiedBackup()
    {
        string root = NewTempDir();
        try
        {
            var storage = new FileStorage(root);
            string first = SaveJson(cookies: 111);
            string second = SaveJson(cookies: 222_222);

            storage.Write(Slot, first);
            storage.Write(Slot, second);

            Check.Equal(second, storage.Read(Slot), "当前存档应当是新写的那份。");

            string? backup = storage.Read(BackupPath);
            Check.NotNull(backup, "覆盖之前必须把上一份留下来。");
            if (backup is null) return;

            Check.Equal(first, backup, "备份里应当是上一份的原文，而不是更早的、或半截的东西。");
            Check.Close(
                111,
                SaveSerializer.Parse(backup).Cookies,
                1e-9,
                "备份必须自己读得回来，否则它只是个占位的文件。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 备份跟着滚动：第三次存档之后，<c>.bak</c> 里是第二次那份，而不是停在最早那份。<para>
    /// 这一条同时钉住"<c>.bak</c> 已经存在时替换仍然要成功"——
    /// 实测 <c>File.Replace</c> 会覆盖已存在的备份文件，但如果哪天不是了，
    /// 症状会是"第二次之后的每次存档都失败"，那比丢掉备份严重得多。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveFile_BackupRollsForwardOnEverySave()
    {
        string root = NewTempDir();
        try
        {
            var storage = new FileStorage(root);
            string first = SaveJson(cookies: 111);
            string second = SaveJson(cookies: 222);
            string third = SaveJson(cookies: 333);

            storage.Write(Slot, first);
            storage.Write(Slot, second);
            storage.Write(Slot, third);

            Check.Equal(third, storage.Read(Slot), "当前存档应当是最新那份。");
            Check.Equal(second, storage.Read(BackupPath), "备份应当滚到上一次那份，而不是停在最早那份。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 写不回来的内容<b>不许成为当前存档</b>，而且失败必须响亮（抛 <see cref="InvalidDataException"/>）。<para>
    /// 载荷刻意分几档，因为它们会走到不同的判定上：不是 JSON、是 JSON 但不是对象、版本高于当前，
    /// 以及最后一档——<b>合法 JSON、版本也合法，但根本读不成 <see cref="SaveData"/></b>。
    /// 那一档最危险：任何只检查"是不是 JSON"的自检都会放它过去，而它恰恰是玩家下次启动时
    /// 唯一会看到的东西（一句读档失败）。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveFile_RefusesContentThatCannotBeParsedBack()
    {
        string[] payloads =
        [
            "{ 这不是合法 JSON",
            "[1, 2, 3]",
            """{"Version":999}""",
            """{"Version":1,"Cookies":"不是数字"}""",
            """{"Version":1,"Buildings":{"mini":"两只"}}""",
        ];

        foreach (string payload in payloads)
        {
            string root = NewTempDir();
            try
            {
                var storage = new FileStorage(root);
                string good = SaveJson(cookies: 777);
                storage.Write(Slot, good);
                byte[] before = File.ReadAllBytes(Path.Combine(root, Slot));

                Check.Throws<InvalidDataException>(
                    () => storage.Write(Slot, payload),
                    $"载荷 <{payload}> 写出来读不回来，必须被拒绝而不是被写下去。");

                Check.Equal(good, storage.Read(Slot), $"载荷 <{payload}> 被拒绝之后，当前存档必须一字未动。");
                Check.True(
                    before.SequenceEqual(File.ReadAllBytes(Path.Combine(root, Slot))),
                    $"载荷 <{payload}> 被拒绝之后，当前存档连一个字节都不该动。");
                Check.False(
                    File.Exists(Path.Combine(root, TempPath)),
                    $"载荷 <{payload}> 被拒绝之后不该把半成品留在存档目录里。");
                Check.False(
                    File.Exists(Path.Combine(root, BackupPath)),
                    "被拒绝的写入不该顺手造一个备份出来。");
            }
            finally
            {
                Cleanup(root);
            }
        }
    }

    /// <summary>
    /// 磁盘上那份读不回来时，它不配当"上一份好存档"。<para>
    /// 它当然可以被新的好存档覆盖（否则一个坏文件会把存档功能永久锁死），但<b>不许占用 <c>.bak</c></b>：
    /// 那里要么留着上一次真正验证过的那份，要么空着。按"文件在不在"来认备份，就会在这里说谎——
    /// 而且是在最需要备份的那一刻说谎。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveFile_DoesNotPromoteAnUnloadableSaveToBackup()
    {
        // ① 已经有一份更早的好备份：新的好存档覆盖坏文件之后，它必须原样活下来。
        string withBackup = NewTempDir();
        try
        {
            string older = SaveJson(cookies: 100);
            string newer = SaveJson(cookies: 200);
            File.WriteAllText(Path.Combine(withBackup, BackupPath), older);
            File.WriteAllText(Path.Combine(withBackup, Slot), "{ 坏掉的存档");

            new FileStorage(withBackup).Write(Slot, newer);

            Check.Equal(newer, File.ReadAllText(Path.Combine(withBackup, Slot)), "坏文件应当被新的好存档覆盖。");
            Check.Equal(
                older,
                File.ReadAllText(Path.Combine(withBackup, BackupPath)),
                "读不回来的那一份不许顶掉真正验证过的备份。");
        }
        finally
        {
            Cleanup(withBackup);
        }

        // ② 没有更早的备份：覆盖坏文件之后也不该凭空出现一个装着坏内容的 .bak。
        string withoutBackup = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(withoutBackup, Slot), "{ 坏掉的存档");

            new FileStorage(withoutBackup).Write(Slot, SaveJson(cookies: 300));

            Check.False(
                File.Exists(Path.Combine(withoutBackup, BackupPath)),
                "读不回来的上一份不许被请进备份里。");
        }
        finally
        {
            Cleanup(withoutBackup);
        }
    }

    /// <summary>
    /// 存档路径上的失败必须响亮，而且不许丢掉上一份好存档。<para>
    /// 这里走的是<b>导入</b>那条路（<c>WriteRaw</c>），它与自动存档共用同一个写入实现：
    /// 回报 false、留下 <see cref="SaveManager.LastError"/>、当前存档一字未动、
    /// 之后存档还能继续正常工作。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveManager_FailedWriteIsLoudAndLosesNothing()
    {
        string root = NewTempDir();
        try
        {
            GameEngine engine = TestGame.CreateMini(out _);
            engine.State.Cookies = 555;
            engine.MarkDirty();

            bool savedEvent = false;
            using IDisposable subscription = engine.Events.Subscribe<GameSavedEvent>(_ => savedEvent = true);

            using var manager = new SaveManager(engine, new FileStorage(root), Slot);
            Check.True(manager.Save(), "前置条件：第一次存档应当成功。");
            Check.True(savedEvent, "前置条件：成功存档应当发 GameSavedEvent。");

            savedEvent = false;
            Check.False(
                manager.WriteRaw("""{"Version":1,"Buildings":{"mini":"两只"}}"""),
                "写不进去必须回报 false，而不是回报成功。");
            Check.NotNull(manager.LastError, "失败必须留痕（LastError）。");
            Check.False(savedEvent, "被拒绝的写入不该发 GameSavedEvent。");

            GameEngine restored = TestGame.CreateMini(out _);
            using var reader = new SaveManager(restored, new FileStorage(root), Slot);
            Check.True(reader.Load(), "被拒绝的写入之后，上一次的存档必须仍然读得回来。");
            Check.Close(555, restored.State.Cookies, 1e-9, "读回来的必须是上一份好存档，不是失败的那份。");

            // 失败一次之后存档还得能继续正常工作，而且要把上一份（555）滚进备份。
            engine.State.Cookies = 666;
            engine.MarkDirty();
            Check.True(manager.Save(), "一次被拒绝的写入之后，存档必须还能用。");

            string? live = manager.ReadRaw();
            Check.NotNull(live, "重新存档之后应当读得到当前存档。");
            if (live is null) return;
            Check.Close(666, SaveSerializer.Parse(live).Cookies, 1e-9, "当前存档应当是新写的那份。");

            string backupPath = Path.Combine(root, BackupPath);
            Check.True(File.Exists(backupPath), "上一份（555）应当已经滚进备份里。");
            Check.Close(
                555,
                SaveSerializer.Parse(File.ReadAllText(backupPath)).Cookies,
                1e-9,
                "备份里应当是上一份（555），不是失败的那份。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 存储层直接抛（磁盘满 / 权限不足 / 被别的进程占着）时，<c>Save()</c> 也必须失败得响亮。<para>
    /// 自动存档是<b>无人值守</b>的：它静默失败的话，玩家要到下一次启动才发现自己丢了一整局。
    /// 所以这里钉的不是"抛不抛"，而是"有没有对外报告成功"——尤其是那条
    /// <c>GameSavedEvent</c>：发了它，宿主就会理直气壮地告诉玩家"已存档"。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveManager_StorageFailureIsReportedNotSwallowed()
    {
        GameEngine engine = TestGame.CreateMini(out _);
        using var manager = new SaveManager(engine, new ThrowingStorage(), Slot);

        bool savedEvent = false;
        using IDisposable subscription = engine.Events.Subscribe<GameSavedEvent>(_ => savedEvent = true);

        Check.False(manager.Save(), "存储写不进去时 Save() 必须回报失败。");
        Check.NotNull(manager.LastError, "失败必须留痕（LastError）。");
        Check.Contains(manager.LastError!.Message, "磁盘满了", "LastError 里要看得见真正的原因。");
        Check.False(savedEvent, "存档失败却发了 GameSavedEvent，正是静默失败最典型的形状。");
        Check.Null(manager.LastSaveAt, "没存上就不该记下存档时刻。");
    }

    /// <summary>
    /// 合法 JSON 但读不成 <see cref="SaveData"/>，也算存档损坏，也必须走
    /// <see cref="InvalidDataException"/>。<para>
    /// 这条 <b>不只是好看的错误类型</b>：写入前的自检就是按这个类型判定"这份内容读不回来"的，
    /// 漏出去的类型会让自检判成"我没见过这种异常"——那就不再是"拒绝写入"，而是别的什么东西。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveSerializer_ValidJsonThatIsNotSaveDataCountsAsCorrupt()
    {
        Check.Throws<InvalidDataException>(
            () => SaveSerializer.Parse("""{"Version":1,"Cookies":"不是数字"}"""),
            "字段类型对不上的存档必须报存档损坏。");
        Check.Throws<InvalidDataException>(
            () => SaveSerializer.Parse("""{"Version":"1"}"""),
            "版本号不是整数同样是存档损坏。");
        Check.Close(
            42,
            SaveSerializer.Parse("""{"Version":1,"Cookies":42}""").Cookies,
            1e-9,
            "正常的存档不受影响。");
    }

    /// <summary>一份由引擎真写出来的存档文本（前提是它自己读得回来，否则这些用例的前提就不成立）。</summary>
    private static string SaveJson(double cookies)
    {
        GameEngine engine = TestGame.CreateMini(out _);
        engine.State.Cookies = cookies;
        engine.MarkDirty();
        return engine.Save();
    }

    /// <summary>建一个独占的临时目录；用例之间互不影响（测试是并行跑的）。</summary>
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "neko-save-file-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>删掉临时目录；删不掉也不让用例红（不是被测对象），但也不假装删过。</summary>
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
            // 同上。
        }
    }

    /// <summary>一个永远写不进去的存储：模拟磁盘满 / 权限不足 / 被别的进程占着。</summary>
    private sealed class ThrowingStorage : IStorage
    {
        /// <inheritdoc />
        public bool Exists(string key) => false;

        /// <inheritdoc />
        public string? Read(string key) => null;

        /// <inheritdoc />
        public void Write(string key, string content) => throw new IOException("磁盘满了（测试用）。");

        /// <inheritdoc />
        public void Delete(string key)
        {
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ListKeys() => [];
    }
}
