using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core.Content;
using NekoClicker.Core.Persistence;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 存档的<b>导出与导入</b>：一份能被人复制粘贴的文本，以及"导进来一份坏东西不许弄坏能用的存档"这条不变量。<para>
/// 这些用例守的是三件事，每件都对应一种**会静默发生**的事故：
/// </para>
/// <list type="number">
///   <item><b>往返必须一模一样</b>：导出再导入之后，存档本体逐字节相同。
///         差一个字段不会当场炸，只会在下一次读档时表现为"我的进度少了点什么"。</item>
///   <item><b>每一种坏输入都要响亮地拒绝，而且一个字节都不许动</b>。
///         今天的写入链路（<c>FileStorage.Write</c>）已经保证"坏内容不会成为当前存档"，
///         但它<b>拦不住</b>"解析成功却玩不了"（<c>NaN</c>）与"别的包的存档"——
///         那两类只有导入这道闸能拦，而它们恰恰是最容易被误认为成功的一类。</item>
///   <item><b>校验和是"这份文本还是原来那份吗"的唯一答案</b>，而它必须容忍
///         "用格式化工具重排过一遍"、不容忍"改了一个数字"。这两条各自一条用例。</item>
/// </list>
/// <para>
/// 全部用临时目录（<see cref="NewTempDir"/>），跑完即删。仓库里的 <c>saves/</c> 是真人正在玩的进度
/// ——而且此刻<b>有一个宿主正在写它</b>——这些用例一个字节都不碰它。
/// </para>
/// </summary>
public static class SaveTransferTests
{
    private const string Slot = "slot.json";
    private const string BackupPath = Slot + ".bak";
    private const string MiniPack = "mini";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// 往返性质：状态 → 导出 → 导入 → <b>逐字节相同</b>的存档本体。<para>
    /// 比较 <c>engine.Save()</c> 的原文而不是逐字段比对：它同时覆盖了字典顺序、数字写法与
    /// PRNG 状态字（<c>RandomState0/1</c> 是 <c>ulong</c>，只要中间过一趟双精度就会在这里红）。
    /// 两台引擎的手动时钟都从 2024-01-01 起（<c>ManualClock</c> 的默认值），所以
    /// <c>LastSavedAt</c> 也落在同一刻——这条相等是<b>严格</b>的，不是"差不多"。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RoundTripsStateExactly()
    {
        using var source = NewSession(packId: "nine", content: TestGame.NineLives);
        FillRichState(source.Engine);

        string exported = source.Manager.Export();

        using var target = NewSession(packId: "nine", content: TestGame.NineLives);
        SaveTransferResult result = target.Manager.Import(exported);

        Check.True(result.Ok, $"这份自己导出来的文本必须收得下：{result.Message}");
        Check.Equal(SaveTransferKind.Ok, result.Kind, "成功的类别应当是 Ok。");
        Check.Equal(0, result.UnknownIdCount, "往返用的 id 全是从这个包里取出来的，不该有一个被报成不认识。");
        CheckSameSave(
            source.Engine.Save(),
            target.Engine.Save(),
            "导出再导入之后，存档本体必须逐字节相同（含 PRNG 状态字）");
    }

    /// <summary>
    /// 导出的文本必须自证身份：格式标签、包 id、存档版本、校验和，四样都要在，
    /// 而且校验和必须恰好等于对 <c>Save</c> 字段求出来的值。
    /// 顺带量一次体积（外壳与转义到底占多少）——这个数字要写进方案文档，不该靠估。
    /// </summary>
    [Test]
    public static void SaveTransfer_ExportedTextIsSelfDescribing()
    {
        using var session = NewSession(packId: MiniPack);
        string text = session.Manager.Export();
        var envelope = (JsonObject)JsonNode.Parse(text)!;

        Check.Equal(SaveTransfer.FormatTag, envelope["Format"]?.GetValue<string>(), "导出文本必须带格式标签。");
        Check.Equal(SaveTransfer.FormatVersion, envelope["FormatVersion"]!.GetValue<int>(), "必须带信封格式版本。");
        Check.Equal(MiniPack, envelope["PackId"]?.GetValue<string>(), "必须带包 id——导入端就靠它拒绝别的包。");
        Check.Equal(
            SaveSerializer.CurrentVersion,
            envelope["SaveVersion"]!.GetValue<int>(),
            "信封声明的存档版本必须与当前存档格式一致。");

        string body = envelope["Save"]!.GetValue<string>();
        Check.Equal(SaveTransfer.Checksum(body), envelope["Checksum"]!.GetValue<string>(), "校验和必须对得上 Save 字段。");
        Check.True(text.Contains('\n'), "外壳要缩进——它是唯一一处给人核对身份的地方。");
        Check.False(body.Contains('\n'), "存档本体要紧凑：塞进字符串之后缩进只会变成一排 \\n。");

        // 体积（实测，写进 SAVE_TRANSFER_PLAN §2.4）：外壳 + 字符串转义在真实存档上是多少。
        Console.WriteLine(
            $"      [实测] 存档本体 {body.Length} 字符 → 导出文本 {text.Length} 字符"
            + $"（外壳与转义 +{text.Length - body.Length} 字符，+{(text.Length - body.Length) * 100.0 / body.Length:0.0}%）。");
    }

    /// <summary>
    /// 重排空白不许让校验和变红。<para>
    /// 这是"校验和算在 <c>Save</c> <b>字符串的字节</b>上、而不是算在嵌套对象的规范化形式上"
    /// 这个选择的**唯一理由**：任何格式化工具都会重排外壳的空白，但<b>不会改写字符串字面量内部</b>。
    /// 反过来，见下一条用例：改一个数字必红。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_ReformattingTheEnvelopeDoesNotBreakTheChecksum()
    {
        using var source = NewSession(packId: MiniPack);
        string exported = source.Manager.Export();

        // 走一趟"解析成对象再紧凑输出"——等价于用 jq / 一个格式化工具重排一次空白。
        string reformatted = ((JsonObject)JsonNode.Parse(exported)!).ToJsonString();
        Check.NotEqual(exported, reformatted, "前置条件：这次重排真的改变了文本（否则这条用例什么都没验）。");

        using var target = NewSession(packId: MiniPack);
        SaveTransferResult result = target.Manager.Import(reformatted);

        Check.True(result.Ok, $"重排空白不该让导入失败：{result.Message}");
        CheckSameSave(source.Engine.Save(), target.Engine.Save(), "重排之后的文本导进来，内容必须还是那份。");
    }

    /// <summary>改一个数字必红：校验和要发现的正是"解析得出来、但已经不是原来那份"。</summary>
    [Test]
    public static void SaveTransfer_EditedTextFailsTheChecksum()
    {
        using var session = NewSession(packId: MiniPack);
        string text = session.Manager.Export();
        var envelope = (JsonObject)JsonNode.Parse(text)!;
        string body = envelope["Save"]!.GetValue<string>();

        // 只改字符串里的一个数字，校验和保持原样——这正是"手改存档"或"粘贴时丢了一位"的形状。
        Check.Contains(body, "600", "前置条件：盘上那份的货币数要在存盘文本里看得见。");
        envelope["Save"] = body.Replace("600", "6000", StringComparison.Ordinal);

        CheckRejected(
            session,
            envelope.ToJsonString(Indented),
            SaveTransferKind.ChecksumMismatch,
            "校验和对不上",
            "内容被改过而校验和没跟着改");
    }

    /// <summary>空输入 / 不是 JSON / 根不是对象 / 格式标签不对：全都归入"这不是导出文本"，并且四种都要点破是哪一种。</summary>
    [Test]
    public static void SaveTransfer_RejectsTextThatIsNotAnExport()
    {
        using var session = NewSession(packId: MiniPack);

        CheckRejected(session, "", SaveTransferKind.NotAnEnvelope, "没有粘贴任何内容", "空输入");
        CheckRejected(session, "   ", SaveTransferKind.NotAnEnvelope, "没有粘贴任何内容", "全空白");
        CheckRejected(session, "{ 坏掉的存档", SaveTransferKind.NotAnEnvelope, "连合法 JSON 都不是", "半截 JSON");
        CheckRejected(session, "[1,2,3]", SaveTransferKind.NotAnEnvelope, "根节点不是 JSON 对象", "根是数组");
        CheckRejected(session, """{"Version":1,"Cookies":42}""", SaveTransferKind.NotAnEnvelope, "没有 neko-save 信封头", "裸存档");
        CheckRejected(
            session,
            """{"Format":"other-game","FormatVersion":1,"SaveVersion":1,"Checksum":"sha256:x","Save":"{}"}""",
            SaveTransferKind.NotAnEnvelope,
            "other-game",
            "别的游戏的格式标签");

        // base64 分享码（ExportShareCode 的产物）也要被明确拒掉：它没有包标识、没有校验和。
        // 它被归到"连合法 JSON 都不是"那一支（base64 不以 `{` 开头），不是"没有信封头"——
        // 两支都是 NotAnEnvelope，消息里都写着"请整份复制导出出来的内容"。
        // 这条取舍写下来免得被当成"忘了支持分享码"（见 SAVE_TRANSFER_PLAN §7「明确不做」）。
        CheckRejected(
            session,
            SaveSerializer.SerializeToShareCode(TestGame.CreateMini(out _)),
            SaveTransferKind.NotAnEnvelope,
            "这不是本游戏导出的存档文本",
            "base64 分享码");
    }

    /// <summary>信封自身不完整：缺必填字段、类型不对——每一条都要点出**是哪一个字段**。</summary>
    [Test]
    public static void SaveTransfer_RejectsIncompleteEnvelope()
    {
        using var session = NewSession(packId: MiniPack);
        string good = session.Manager.Export();

        CheckRejected(session, Without(good, "Checksum"), SaveTransferKind.CorruptEnvelope, "Checksum", "缺 Checksum");
        CheckRejected(session, Without(good, "Save"), SaveTransferKind.CorruptEnvelope, "Save 是空的", "缺 Save");
        CheckRejected(session, Without(good, "FormatVersion"), SaveTransferKind.CorruptEnvelope, "FormatVersion", "缺 FormatVersion");
        CheckRejected(session, Without(good, "SaveVersion"), SaveTransferKind.CorruptEnvelope, "SaveVersion", "缺 SaveVersion");
        CheckRejected(
            session,
            With(good, "Checksum", 12345),
            SaveTransferKind.CorruptEnvelope,
            "Checksum",
            "Checksum 类型不对");
    }

    /// <summary>
    /// 版本闸：<b>更新</b>的信封格式与<b>更新</b>的存档格式各自要有一句话，而且都要在解析存档之前响。<para>
    /// 顺序也在这条用例里钉住了：<c>SaveVersion</c> 被改成 2 之后校验和必然对不上，
    /// 而这里期望的是 <see cref="SaveTransferKind.NewerSave"/>——也就是说版本闸确实排在校验闸前面。
    /// 这不是巧合：先说"去更新游戏"比先说"校验和对不上"有用得多。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsNewerVersions()
    {
        using var session = NewSession(packId: MiniPack);
        string good = session.Manager.Export();

        CheckRejected(
            session,
            With(good, "FormatVersion", SaveTransfer.FormatVersion + 1),
            SaveTransferKind.NewerFormat,
            "请更新游戏",
            "信封格式比本版本新");

        CheckRejected(
            session,
            With(good, "SaveVersion", SaveSerializer.CurrentVersion + 1),
            SaveTransferKind.NewerSave,
            "请更新游戏",
            "存档格式比本版本新");

        CheckRejected(
            session,
            With(good, "FormatVersion", 0),
            SaveTransferKind.CorruptEnvelope,
            "FormatVersion",
            "信封格式版本是 0（本游戏不认识）");
    }

    /// <summary>
    /// 别的包的存档必须被拒。<para>
    /// 这是本次改动存在的**理由本身**：十一个包写出来的存档形状逐字段同构，而未知 id 是刻意
    /// 保留的合法状态，所以除了信封里那条声明，没有任何东西分得清"这份是咖啡馆的"。
    /// 消息里必须同时有对方的包 id 与自己的——只说"包不对"等于让人去猜。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsForeignPack()
    {
        using var foreign = NewSession(packId: "cafe", content: TestGame.CafeContent);
        string fromCafe = foreign.Manager.Export();

        using var mine = NewSession(packId: MiniPack);
        CheckRejected(mine, fromCafe, SaveTransferKind.ForeignPack, "cafe", "咖啡馆的存档导进 mini 会话");
        CheckRejected(mine, fromCafe, SaveTransferKind.ForeignPack, "mini", "拒绝理由里要说清自己是谁");
    }

    /// <summary>
    /// 存档本体读不回来 ⇒ 拒绝，而且是**存档损坏**这一类，不是信封坏。<para>
    /// 校验和是**重新算过**的：这模拟的是"文本自洽、但里面的存档本身不是存档"
    /// （比如把别的东西包成了信封）。走不到落盘那一步是这条用例的重点。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsUnreadableSaveBody()
    {
        using var session = NewSession(packId: MiniPack);
        var envelope = (JsonObject)JsonNode.Parse(session.Manager.Export())!;

        string body = "这不是存档";
        envelope["Save"] = body;
        envelope["Checksum"] = SaveTransfer.Checksum(body);

        CheckRejected(
            session,
            envelope.ToJsonString(Indented),
            SaveTransferKind.CorruptSave,
            "读不回来",
            "信封自洽、但里面的存档不是存档");
    }

    /// <summary>
    /// 信封自相矛盾：外壳声明的存档版本与本体里写的版本不是一个数。
    /// 校验和**跟着重算**（否则先撞上的是校验闸），所以这条真的在验那一处一致性检查。
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsEnvelopeThatContradictsItself()
    {
        using var session = NewSession(packId: MiniPack);
        var envelope = (JsonObject)JsonNode.Parse(session.Manager.Export())!;

        string body = ((JsonObject)JsonNode.Parse(envelope["Save"]!.GetValue<string>())!)
            .ToJsonString(); // Version 仍是 1
        envelope["Save"] = body;
        envelope["Checksum"] = SaveTransfer.Checksum(body);
        envelope["SaveVersion"] = 0; // 声明 0，本体里写着 1

        CheckRejected(
            session,
            envelope.ToJsonString(Indented),
            SaveTransferKind.CorruptEnvelope,
            "自相矛盾",
            "信封声明与本体内容对不上");
    }

    /// <summary>
    /// 尺寸闸：<b>在解析之前</b>就拒。<para>
    /// 一份 1 MiB + 1 的文本会被这一条挡住，而不会走到 JSON 解析那一步——解析才是吃内存的动作。
    /// 上限比最大的真实存档（3718 字节）大约 280 倍，所以正常输入永远碰不到它。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsOversizedInput()
    {
        using var session = NewSession(packId: MiniPack);
        string huge = new('x', SaveTransfer.MaxTransferChars + 1);

        CheckRejected(session, huge, SaveTransferKind.TooLarge, "超过上限", "病态的大粘贴");
    }

    /// <summary>
    /// <c>NaN</c> / 无穷必须被拒，而且是<b>逐字段</b>验的。<para>
    /// 这一类是既有的三道闸<b>结构性拦不住</b>的：<see cref="SaveSerializer"/> 打开了
    /// <c>AllowNamedFloatingPointLiterals</c>，于是 <c>"Cookies": "NaN"</c> 是<b>解析成功</b>的。
    /// 一份 NaN 存档会通过"写前反解一遍"成为当前存档，然后把之后每一次结算都变成 NaN。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_RejectsNonFiniteNumbers()
    {
        using var session = NewSession(packId: MiniPack);
        var original = (JsonObject)JsonNode.Parse(session.Manager.Export())!;
        string body = original["Save"]!.GetValue<string>();

        foreach (string field in DoubleFieldsOfSaveData)
        {
            var envelope = (JsonObject)JsonNode.Parse(original.ToJsonString())!;
            var edited = (JsonObject)JsonNode.Parse(body)!;
            edited[field] = "NaN";
            string editedBody = edited.ToJsonString();
            envelope["Save"] = editedBody;
            envelope["Checksum"] = SaveTransfer.Checksum(editedBody);

            // 前置条件：这份编辑真的是一座 NaN 存档（Parse 收得下），否则下面拒的就是别的东西。
            SaveData parsed = SaveSerializer.Parse(editedBody);
            double value = (double)typeof(SaveData).GetProperty(field)!.GetValue(parsed)!;
            Check.True(
                double.IsNaN(value),
                $"前置条件：{field} 应当被解析成 NaN——如果这里不成立，这条用例就没在验它以为在验的东西。");

            CheckRejected(
                session,
                envelope.ToJsonString(Indented),
                SaveTransferKind.NonFiniteNumbers,
                field,
                $"{field} = NaN");
        }
    }

    /// <summary>
    /// 未知 id <b>只报告、不拒绝</b>——而且报告完还得原样留着。<para>
    /// 未知 id 是**合法状态**（内容包临时下线某个建筑时存档依然无损，见 <c>ARCHITECTURE.md</c>），
    /// 所以拒绝它是错的；但"这个包答不上这些东西"必须说得出来——那正是 <c>OPEN_WORK</c> 的 W9
    /// 记着的那种形状：界面上一切正常，没有任何地方提过一句。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_ReportsUnknownIdsButKeepsThem()
    {
        using var source = NewSession(packId: MiniPack);
        var envelope = (JsonObject)JsonNode.Parse(source.Manager.Export())!;
        string body = envelope["Save"]!.GetValue<string>();

        var edited = (JsonObject)JsonNode.Parse(body)!;
        edited["Buildings"] = new JsonObject { ["b"] = 3, ["ghost_building"] = 7 };
        string editedBody = edited.ToJsonString();
        envelope["Save"] = editedBody;
        envelope["Checksum"] = SaveTransfer.Checksum(editedBody);
        string text = envelope.ToJsonString(Indented);

        using var target = NewSession(packId: MiniPack);
        SaveTransferResult result = target.Manager.Import(text);

        Check.True(result.Ok, $"未知 id 不该让导入失败——它是合法状态：{result.Message}");
        Check.Equal(1, result.UnknownIdCount, "只该报出 ghost_building 这一个不认识的 id。");
        Check.Equal("建筑:ghost_building", result.UnknownIds[0], "报告里要带上它是什么类别的 id。");
        Check.Contains(result.Message, "ghost_building", "消息里要把不认识的 id 列出来。");

        // 报告了还得留着：不能被"清洗"掉。这是 ARCHITECTURE「未知的内容 id 不会被丢弃」那条不变量。
        Check.Equal(7, target.Engine.State.BuildingCounts["ghost_building"], "未知 id 必须原样保留。");
        Check.Equal(3, target.Engine.State.BuildingCounts["b"], "认识的 id 也要在。");
    }

    /// <summary>
    /// 导入之后<b>自动存档不许把导入的成果盖掉</b>。<para>
    /// 这是本次改动要修的那个缺口本身：改之前唯一的"导入"路径（<c>WriteRaw</c>）只写文件、
    /// 不碰当前会话，于是 60 秒后自动存档就用<b>旧状态</b>把刚导入的文件盖了回去——
    /// 报告成功、然后自我销毁，全程没有一句话。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_ImportedStateSurvivesTheNextAutoSave()
    {
        using var source = NewSession(packId: MiniPack);
        source.Engine.State.Cookies = 1_234.5;
        source.Engine.MarkDirty();
        string exported = source.Manager.Export();

        using var target = NewSession(packId: MiniPack);
        Check.True(target.Manager.Import(exported).Ok, "前置条件：导入要成功。");
        Check.Close(1_234.5, target.Engine.State.Cookies, 1e-9, "前置条件：内存里已经是导入的那份。");

        // 手动把自动存档的计时走过头：这正是 60 秒后会发生的事。
        target.Manager.Tick(target.Manager.AutoSaveInterval + 1);

        string? onDisk = target.Manager.ReadRaw();
        Check.NotNull(onDisk, "自动存档之后盘上必须有存档。");
        if (onDisk is null) return;
        Check.Close(
            1_234.5,
            SaveSerializer.Parse(onDisk).Cookies,
            1e-9,
            "自动存档必须写的是导入进来的那份，而不是被导入盖掉之前的旧状态。");
    }

    /// <summary>
    /// 会话没有包标识时，跨包检查<b>做不了</b>——这件事必须在消息里说出来。<para>
    /// 不说的话，"没检查"与"检查通过"在结果里长得一模一样，而它们是完全不同的两件事。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveTransfer_SaysSoWhenThePackCheckDidNotRun()
    {
        using var source = NewSession(packId: null);
        string exported = source.Manager.Export();
        Check.False(
            ((JsonObject)JsonNode.Parse(exported)!)["PackId"] is JsonValue,
            "没有包标识时，信封里的 PackId 必须是 null，而不是编一个出来。");

        using var target = NewSession(packId: null);
        SaveTransferResult result = target.Manager.Import(exported);

        Check.True(result.Ok, "没有包标识不该让导入失败——只是少了一道闸。");
        Check.Null(result.PackId, "没声明包 id 时结果里也应当是 null。");
        Check.Contains(result.Message, "没有做跨包检查", "消息里必须点明这次没有做跨包检查。");
    }

    /// <summary>
    /// <see cref="SaveData"/> 上每一个 <c>double</c> 字段都必须被"非有限数字"那道闸覆盖。<para>
    /// 那张清单是**手写**的（不反射，因为反射走 <c>ulong</c> 会把 PRNG 状态字毁掉），
    /// 所以需要这条用例在<b>有人给 SaveData 加了新数值字段</b>时变红——它是那份清单的守卫，
    /// 少了它，"新字段可以是 NaN"就会一直没人发现。
    /// </para>
    /// </summary>
    [Test]
    public static void SaveData_DoubleFieldsAreAllCoveredByTheFiniteCheck()
    {
        string[] actual = [.. typeof(SaveData)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(double))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)];

        string[] covered = [.. DoubleFieldsOfSaveData.OrderBy(n => n, StringComparer.Ordinal)];

        Check.Equal(
            string.Join("、", covered),
            string.Join("、", actual),
            "SaveData 的 double 字段变了：请同步更新 SaveTransfer.FindNonFinite 的清单与这条用例");
    }

    /// <summary><c>Wrap</c> 只接受一份真存档：不是存档就不许包成导出文本（否则会产出一份自己都不自洽的信封）。</summary>
    [Test]
    public static void SaveTransfer_WrapRefusesNonSaveInput()
    {
        Check.Throws<InvalidDataException>(
            () => SaveTransfer.Wrap(string.Empty, MiniPack),
            "空文本不能被包成导出文本。");
        Check.Throws<InvalidDataException>(
            () => SaveTransfer.Wrap("这不是 JSON", MiniPack),
            "不是 JSON 的文本不能被包成导出文本。");
        Check.Throws<InvalidDataException>(
            () => SaveTransfer.Wrap("""{"Version":"一"}""", MiniPack),
            "Version 不是整数的文本不能被包成导出文本。");
    }

    // ------------------------------------------------------------------ 工具

    /// <summary><see cref="SaveData"/> 上所有 <c>double</c> 字段的名字（与 <c>SaveTransfer.FindNonFinite</c> 的清单同源）。</summary>
    private static readonly string[] DoubleFieldsOfSaveData =
    [
        nameof(SaveData.Cookies),
        nameof(SaveData.CookiesEarnedThisRun),
        nameof(SaveData.CookiesEarnedAllTime),
        nameof(SaveData.HandMadeCookies),
        nameof(SaveData.TotalClicks),
        nameof(SaveData.GoldenCookiesClicked),
        nameof(SaveData.PrestigeChips),
        nameof(SaveData.PrestigeChipsSpent),
        nameof(SaveData.PlayTimeSeconds),
        nameof(SaveData.EraEnteredPlayTimeSeconds),
        nameof(SaveData.GoldenCookieCountdown),
    ];

    /// <summary>断言"这次导入必须被拒"，并且<b>磁盘与内存一个字节都没变</b>。</summary>
    /// <param name="session">已经存过两次盘的会话。</param>
    /// <param name="input">要导入的坏文本。</param>
    /// <param name="kind">期望的类别。</param>
    /// <param name="fragment">消息里必须出现的关键片段。</param>
    /// <param name="why">这条在验什么（失败信息里要看得见）。</param>
    private static void CheckRejected(Session session, string input, SaveTransferKind kind, string fragment, string why)
    {
        string saveBefore = session.ReadSave();
        string backupBefore = session.ReadBackup();

        SaveTransferResult result = session.Manager.Import(input);

        Check.False(result.Ok, $"{why}：必须拒绝，实际却收下了（{result.Message}）。");
        Check.Equal(kind, result.Kind, $"{why}：拒绝的类别不对（消息：{result.Message}）。");
        Check.Contains(result.Message, fragment, $"{why}：拒绝的理由里要说清是哪一种。");
        Check.Equal(saveBefore, session.ReadSave(), $"{why}：被拒绝的导入不许动当前存档。");
        Check.Equal(backupBefore, session.ReadBackup(), $"{why}：被拒绝的导入不许动备份。");
        Check.Close(600, session.Engine.State.Cookies, 1e-9, $"{why}：被拒绝的导入不许动内存里的会话。");
    }

    /// <summary>比两份存档文本；不一样就只报"第一处不同在第几个字符"，不把两万字节刷进日志。</summary>
    private static void CheckSameSave(string expected, string actual, string why)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal)) return;

        int at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at]) at++;

        string window = at < expected.Length
            ? expected[Math.Max(0, at - 20)..Math.Min(expected.Length, at + 20)]
            : expected;

        throw new AssertionException(
            $"{why}｜第一处不同在第 {at} 个字符：期望 …{window}…；长度 期望 {expected.Length} / 实际 {actual.Length}。");
    }

    /// <summary>把导出文本里的某个字段删掉（造"信封不完整"）。</summary>
    private static string Without(string text, string field)
    {
        var envelope = (JsonObject)JsonNode.Parse(text)!;
        envelope.Remove(field);
        return envelope.ToJsonString(Indented);
    }

    /// <summary>把导出文本里的某个字段改成别的值（造"字段类型不对 / 版本更新"）。</summary>
    private static string With(string text, string field, int value)
    {
        var envelope = (JsonObject)JsonNode.Parse(text)!;
        envelope[field] = value;
        return envelope.ToJsonString(Indented);
    }

    /// <summary>
    /// 灌一批有代表性的状态：货币、转生、纪元、叙事、表态、结局、持有、增益、场上的金猫、
    /// 计数器、元数据全都要有。<para>
    /// id 一律从<b>这个包自己的内容</b>里取（<c>BuildingById.Keys.First()</c> 这类），
    /// 不写死字符串：内容包改了 id，这条用例不该跟着坏；而写死的 id 会让"未知 id"与"真 id"
    /// 混在一起，把"往返保真"验成"未知 id 保真"。
    /// </para>
    /// </summary>
    private static void FillRichState(GameEngine engine)
    {
        GameState state = engine.State;
        GameContent content = engine.Content;

        state.Cookies = 1_234.5;
        state.CookiesEarnedThisRun = 987.25;
        state.CookiesEarnedAllTime = 8_765_432.125;
        state.HandMadeCookies = 42.5;
        state.TotalClicks = 137;
        state.GoldenCookiesClicked = 3;
        state.PrestigeLevel = 4;
        state.PrestigeChips = 12.5;
        state.PrestigeChipsSpent = 7;
        state.Ascensions = 2;
        state.PlayTimeSeconds = 3_600.5;
        state.TickCount = 108_000;
        state.GoldenCookieCountdown = 42.75;
        state.GoldenCookieIntroduced = true;

        state.Era = Math.Max(1, content.HasEras ? 2 : 1);
        state.EraEnteredPlayTimeSeconds = 900;
        state.EraCompleted.Add(1);
        state.EraHistory[1] = new EraRecord
        {
            Index = 1,
            PlayTimeSeconds = 600.5,
            CookiesEarned = 5_000.25,
            ChipsGained = 3.5,
            CompletedAt = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
        };

        state.BuildingCounts[content.BuildingById.Keys.First()] = 25;
        if (content.BuildingById.Count > 1) state.BuildingCounts[content.BuildingById.Keys.Last()] = 3;
        state.UpgradeCounts[content.UpgradeById.Keys.First()] = 1;
        state.Achievements.Add(content.AchievementById.Keys.First());

        state.LoreUnlocked.Add(content.LoreById.Keys.First());
        if (content.LoreById.Count > 1) state.PendingLorePopups.Add(content.LoreById.Keys.Last());

        if (content.ChoiceById.Count > 0) state.ChoiceAnswers[content.ChoiceById.Keys.First()] = "option";
        if (content.ChoiceById.Count > 1) state.PendingChoices.Add(content.ChoiceById.Keys.Last());
        state.StanceWeights["stance"] = 7;
        if (content.EndingById.Count > 0) state.EndingsReached.Add(content.EndingById.Keys.First());

        state.Buffs.Add(new ActiveBuff
        {
            Id = "buff_a",
            RemainingSeconds = 12.5,
            TotalSeconds = 30,
            Stacks = 2,
        });

        state.GoldenCookies.Add(new GoldenCookieSpawn
        {
            InstanceId = "golden_a",
            RemainingSeconds = 5.5,
            LifetimeSeconds = 13,
            X = 0.25,
            Y = 0.75,
            ForcedOutcomeId = "outcome_a",
        });

        state.Counters["counter_a"] = 3.5;
        state.Metadata["meta_a"] = "元数据的值";
        engine.MarkDirty();
    }

    /// <summary>一份"已经存过两次盘"的会话：盘上当前那份是 600，上一份（<c>.bak</c>）是 555。</summary>
    /// <param name="packId">包标识；<c>null</c> 表示这个会话不知道自己是哪个包。</param>
    /// <param name="content">内容包；默认是精确做小的 Mini，往返用例会换成真包。</param>
    private static Session NewSession(string? packId, GameContent? content = null) => new(packId, content);

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "neko-save-transfer-tests", Guid.NewGuid().ToString("N"));
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
            // 同上。
        }
    }

    /// <summary>一个临时目录里的会话：引擎 + FileStorage + SaveManager，已经存过两次盘。</summary>
    private sealed class Session : IDisposable
    {
        private readonly string _root;

        public Session(string? packId, GameContent? content)
        {
            _root = NewTempDir();
            Engine = content is null ? TestGame.CreateMini(out _) : TestGame.Create(content);
            Manager = new SaveManager(Engine, new FileStorage(_root), Slot) { PackId = packId };

            // 两次存档：第二次会把第一次那份滚进 .bak。"被拒绝的导入不许动备份"这句话
            // 只有备份真的存在时才验得了。
            Engine.State.Cookies = 555;
            Engine.MarkDirty();
            Check.True(Manager.Save(), "前置条件：第一次存档应当成功。");

            Engine.State.Cookies = 600;
            Engine.MarkDirty();
            Check.True(Manager.Save(), "前置条件：第二次存档应当成功。");
        }

        public GameEngine Engine { get; }

        public SaveManager Manager { get; }

        public string ReadSave() => File.ReadAllText(Path.Combine(_root, Slot));

        public string ReadBackup() => File.Exists(Path.Combine(_root, BackupPath))
            ? File.ReadAllText(Path.Combine(_root, BackupPath))
            : string.Empty;

        public void Dispose()
        {
            Manager.Dispose();
            Cleanup(_root);
        }
    }
}
