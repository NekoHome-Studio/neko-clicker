using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Persistence;

/// <summary>
/// 一次导入的结果类别。<para>
/// 它存在的理由是"每一种失败都要能被单独断言"：把这些情况压成一个 <c>false</c>，
/// 守卫就只能钉住"失败了"，而钉不住"失败得对"——而"别的包的存档被静默灌进来"
/// 与"粘贴的文本被截断了"需要完全不同的处理（前者要换文件，后者要重新复制）。
/// </para>
/// </summary>
public enum SaveTransferKind
{
    /// <summary>成功：已落盘并应用到当前会话。</summary>
    Ok,

    /// <summary>输入超过 <see cref="SaveTransfer.MaxTransferChars"/>，在解析之前就被拒。</summary>
    TooLarge,

    /// <summary>不是一份带信封头的导出文本（空 / 不是 JSON / 根不是对象 / 没有 <c>Format</c> 标签）。</summary>
    NotAnEnvelope,

    /// <summary>是信封，但自身不完整或不自洽（缺必填字段、字段类型不对、声明与内容对不上）。</summary>
    CorruptEnvelope,

    /// <summary>信封格式比本版本新（<c>FormatVersion</c> &gt; <see cref="SaveTransfer.FormatVersion"/>）。</summary>
    NewerFormat,

    /// <summary>存档格式比本版本新（<c>SaveVersion</c> &gt; <see cref="SaveSerializer.CurrentVersion"/>）。</summary>
    NewerSave,

    /// <summary>校验和对不上：文本被改过或截断过。</summary>
    ChecksumMismatch,

    /// <summary>这份存档属于另一个内容包。</summary>
    ForeignPack,

    /// <summary>存档本体读不回来（合法 JSON 但结构对不上、版本号不是整数等）。</summary>
    CorruptSave,

    /// <summary>存档读得回来，但里面有 <c>NaN</c> / <c>±Infinity</c> 这类不可能出现的数字。</summary>
    NonFiniteNumbers,

    /// <summary>存储写不进去（磁盘满 / 权限不足 / 被占着）。此时当前会话与旧存档都没动。</summary>
    WriteFailed,

    /// <summary>存档已经写进去了，但应用到当前会话时抛了异常（见 <see cref="SaveManager.Import"/> 的诚实边界）。</summary>
    ApplyFailed,
}

/// <summary>
/// 导入的结果。<para>
/// <b>它不是一个布尔值</b>：导入这件事的失败面很宽（§见 <see cref="SaveTransferKind"/>），
/// 而每一种都要能对玩家说清"哪里不对、该怎么办"。<see cref="Message"/> 就是那句话，
/// 它由引擎写好、宿主原样呈现——宿主不该再翻译一遍（与
/// <c>GameHost.FromResult</c> 那条"用引擎自己的 Message"的约定同源）。
/// </para>
/// </summary>
/// <param name="Ok">是否成功（已落盘并已应用）。</param>
/// <param name="Kind">结果类别。</param>
/// <param name="Message">给人看的一句话；失败时带原因与下一步。</param>
/// <param name="PackId">这份输入声明的包 id；没有信封或没写时为 <c>null</c>。</param>
/// <param name="SaveVersion">这份输入声明的存档格式版本；没读到时为 <c>-1</c>。</param>
/// <param name="UnknownIdCount">存档里有几个 id 是本内容包不认识的（**只报告，不拦**）。</param>
/// <param name="UnknownIds">上面那些 id 的前若干个（诊断用，最多 <see cref="SaveTransfer.MaxReportedUnknownIds"/> 个）。</param>
public sealed record SaveTransferResult(
    bool Ok,
    SaveTransferKind Kind,
    string Message,
    string? PackId,
    int SaveVersion,
    int UnknownIdCount,
    IReadOnlyList<string> UnknownIds);

/// <summary>
/// 存档的导出/导入信封。<para>
/// <b>为什么要有这一层</b>：存档本体（<see cref="SaveData"/> 的 JSON）不自证身份——
/// 十一个内容包写出来的形状逐字段同构，内容里没有一处写着"我是哪个包的"。
/// 于是"把别的包的存档导进来"会一声不响地成功（未知 id 是刻意保留的合法状态，
/// 见 <c>ARCHITECTURE.md</c> 的「存档与迁移」）。信封把三件必须自己说得清的事补上：
/// <b>我是谁（<c>Format</c>）</b>、<b>我属于哪个包（<c>PackId</c>）</b>、
/// <b>我还是原来那份吗（<c>Checksum</c>）</b>。
/// </para>
/// <para>
/// 信封文本长这样（外壳缩进，便于人核对；<c>Save</c> 是一个<b>字符串</b>，里面装紧凑的存档 JSON）：
/// <code>
/// {
///   "Format": "neko-save",
///   "FormatVersion": 1,
///   "PackId": "neko",
///   "SaveVersion": 1,
///   "FrameworkVersion": "1.10.0",
///   "ExportedAt": "2026-10-04T02:11:33.4120000+00:00",
///   "Checksum": "sha256:...",
///   "Save": "{\"Version\":1,\"Cookies\":1234}"
/// }
/// </code>
/// </para>
/// <para>
/// <b>校验和为什么算在"字符串的字节"上，而不是算在嵌套对象的规范化形式上</b>：
/// 规范化要先定"数字怎么写"，而 <c>RandomState0</c> 是 <c>ulong</c>——走一趟双精度会
/// 悄悄毁掉 PRNG 状态。算字符串则**导出时那串字节一字不差**，且任何 JSON 格式化工具
/// 都不会改写字符串字面量内部，所以"格式化一下再导入"照样过，而"改一个数字"必红。
/// 详细取舍见 <c>engine/docs/SAVE_TRANSFER_PLAN.md</c> §2。
/// </para>
/// </summary>
public static class SaveTransfer
{
    /// <summary>信封的格式标签：文本里必须有这一个值，否则一律不算导出文本。</summary>
    public const string FormatTag = "neko-save";

    /// <summary>信封自身的格式版本。**与存档格式版本（<see cref="SaveSerializer.CurrentVersion"/>）是两条独立的轴。**</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// 允许导入的最大字符数（1 MiB）。<para>
    /// 仓库里最大的真实存档（<c>saves/ninelines.json</c>）是 <b>3718 字节</b>，
    /// 所以正常输入离这个上限有约 280 倍余量。它唯一的用途是让一次病态粘贴
    /// <b>在 JSON 解析之前</b>就被拒——解析本身才是吃内存的那一步。
    /// </para>
    /// </summary>
    public const int MaxTransferChars = 1 << 20;

    /// <summary>校验和的前缀（算法名写在值里，将来换算法时读得出来）。</summary>
    public const string ChecksumPrefix = "sha256:";

    /// <summary>诊断信息里最多列出几个不认识的 id（总数仍如实报告）。</summary>
    public const int MaxReportedUnknownIds = 20;

    private static readonly JsonSerializerOptions EnvelopeOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>算一份存档文本的校验和（<c>sha256:</c> + 小写十六进制，对 UTF-8 字节）。</summary>
    /// <param name="saveJson">存档本体文本。</param>
    /// <returns>可写进信封的校验和字符串。</returns>
    public static string Checksum(string saveJson)
        => ChecksumPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(saveJson)))
            .ToLowerInvariant();

    /// <summary>把一份存档本体包成可复制粘贴的导出文本。</summary>
    /// <param name="saveJson">存档本体（<c>GameEngine.Save()</c> 的输出）。</param>
    /// <param name="packId">这份存档属于哪个内容包；<c>null</c> = 不声明（导入时也就不做跨包检查）。</param>
    /// <param name="frameworkVersion">框架版本，只做诊断；<c>null</c> 时读 <see cref="ApiVersion.Current"/>。</param>
    /// <param name="exportedAt">导出时刻，只做诊断；<c>default</c> 时不写这个字段。</param>
    /// <returns>信封文本（外壳缩进）。</returns>
    /// <exception cref="InvalidDataException"><paramref name="saveJson"/> 本身不是一份存档。</exception>
    public static string Wrap(
        string saveJson,
        string? packId,
        string? frameworkVersion = null,
        DateTimeOffset exportedAt = default)
    {
        if (string.IsNullOrWhiteSpace(saveJson))
            throw new InvalidDataException("存档本体为空，不能包成导出文本。");

        // 版本号从存档本体里读出来，而不是问调用方要：信封与内容必须是同一份事实的两种写法，
        // 两处各自填一个数字迟早会不一致，而那种不一致只有在导入时才会暴露。
        if (!TryReadSaveVersion(saveJson, out int saveVersion))
            throw new InvalidDataException("这份文本不是一份存档（不是 JSON 对象，或 Version 不是整数），不能包成导出文本。");

        var envelope = new JsonObject
        {
            ["Format"] = FormatTag,
            ["FormatVersion"] = FormatVersion,
            ["PackId"] = packId is null ? null : JsonValue.Create(packId),
            ["SaveVersion"] = saveVersion,
            ["FrameworkVersion"] = JsonValue.Create(frameworkVersion ?? ApiVersion.Current),
            ["ExportedAt"] = exportedAt == default
                ? null
                : JsonValue.Create(exportedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            ["Checksum"] = Checksum(saveJson),
            ["Save"] = saveJson,
        };

        return envelope.ToJsonString(EnvelopeOptions);
    }

    /// <summary>
    /// 拆一封导出文本：只做"信封自己"的检查（尺寸 / 格式标签 / 字段齐全 / 版本 / 校验和）。<para>
    /// <b>它不碰存档本体，也不碰引擎</b>——归属（包 id）与结构（<see cref="SaveSerializer.Parse"/>）
    /// 的闸在 <see cref="SaveManager.Import"/> 里，因为那两道闸需要"这一次会话是谁"这个上下文。
    /// </para>
    /// </summary>
    /// <param name="text">粘贴进来的文本。</param>
    /// <param name="envelope">拆出来的信封；失败时为 <c>null</c>。</param>
    /// <param name="failure">失败原因；成功时为 <c>null</c>。</param>
    /// <returns>拆成功为 <c>true</c>。</returns>
    internal static bool TryUnwrap(string? text, out Envelope? envelope, out SaveTransferResult? failure)
    {
        envelope = null;
        failure = null;

        // ① 尺寸闸必须在解析之前：吃内存的正是解析那一步。
        if (string.IsNullOrWhiteSpace(text))
        {
            failure = Reject(SaveTransferKind.NotAnEnvelope, "没有粘贴任何内容。", null, -1);
            return false;
        }

        if (text.Length > MaxTransferChars)
        {
            failure = Reject(
                SaveTransferKind.TooLarge,
                $"这份文本有 {text.Length} 个字符，超过上限 {MaxTransferChars}。"
                + "一份真实存档只有几 KB，所以这多半不是存档，或者粘贴时带进了别的东西。",
                null,
                -1);
            return false;
        }

        // ② 信封闸：是 JSON 对象、有本游戏的格式标签。
        JsonObject root;
        try
        {
            root = JsonNode.Parse(text) as JsonObject
                   ?? throw new InvalidDataException("根节点不是 JSON 对象。");
        }
        catch (JsonException ex)
        {
            failure = Reject(
                SaveTransferKind.NotAnEnvelope,
                $"这不是本游戏导出的存档文本：它连合法 JSON 都不是（{ex.Message}）。"
                + "请整份复制「导出」出来的内容，不要只复制其中一段。",
                null,
                -1);
            return false;
        }
        catch (InvalidDataException)
        {
            failure = Reject(
                SaveTransferKind.NotAnEnvelope,
                "这不是本游戏导出的存档文本：它的根节点不是 JSON 对象。",
                null,
                -1);
            return false;
        }

        string? format = ReadString(root, "Format");
        if (!string.Equals(format, FormatTag, StringComparison.Ordinal))
        {
            failure = Reject(
                SaveTransferKind.NotAnEnvelope,
                format is null
                    ? $"这不是本游戏导出的存档文本：没有 {FormatTag} 信封头。"
                      + "（裸的 saves/<包>.json 没有包标识，为了不把别的包的存档灌进来，导入只收整份导出文本。）"
                    : $"这不是本游戏导出的存档文本：格式标签是「{format}」，本游戏是「{FormatTag}」。",
                null,
                -1);
            return false;
        }

        if (!TryReadInt(root, "FormatVersion", out int formatVersion))
        {
            failure = Reject(SaveTransferKind.CorruptEnvelope, "导出文本缺少 FormatVersion 字段，或者它不是整数。", null, -1);
            return false;
        }

        if (formatVersion > FormatVersion)
        {
            failure = Reject(
                SaveTransferKind.NewerFormat,
                $"这份导出文本是更新的版本写的（信封格式 {formatVersion} 高于本版本支持的 {FormatVersion}），"
                + "请更新游戏之后再导入。",
                null,
                -1);
            return false;
        }

        if (formatVersion < 1)
        {
            failure = Reject(
                SaveTransferKind.CorruptEnvelope,
                $"导出文本的 FormatVersion 是 {formatVersion}，本游戏不认识这个值。",
                null,
                -1);
            return false;
        }

        if (!TryReadInt(root, "SaveVersion", out int saveVersion))
        {
            failure = Reject(SaveTransferKind.CorruptEnvelope, "导出文本缺少 SaveVersion 字段，或者它不是整数。", null, -1);
            return false;
        }

        // 版本闸放在解析存档之前：这样"这份存档比游戏新"与"这份存档坏了"是两句不同的话。
        if (saveVersion > SaveSerializer.CurrentVersion)
        {
            failure = Reject(
                SaveTransferKind.NewerSave,
                $"存档版本 {saveVersion} 高于当前游戏支持的 {SaveSerializer.CurrentVersion}，请更新游戏后再读取。",
                ReadString(root, "PackId"),
                saveVersion);
            return false;
        }

        if (saveVersion < 0)
        {
            failure = Reject(
                SaveTransferKind.CorruptEnvelope,
                $"导出文本的 SaveVersion 是 {saveVersion}，本游戏不认识这个值。",
                ReadString(root, "PackId"),
                saveVersion);
            return false;
        }

        string? storedChecksum = ReadString(root, "Checksum");
        if (string.IsNullOrWhiteSpace(storedChecksum))
        {
            failure = Reject(SaveTransferKind.CorruptEnvelope, "导出文本缺少 Checksum 字段（或它不是字符串）。", ReadString(root, "PackId"), saveVersion);
            return false;
        }

        string? save = ReadString(root, "Save");
        if (string.IsNullOrWhiteSpace(save))
        {
            failure = Reject(SaveTransferKind.CorruptEnvelope, "导出文本里的 Save 是空的（或者它不是字符串）。", ReadString(root, "PackId"), saveVersion);
            return false;
        }

        // ③ 校验闸：内容与信封是不是同一份事实。
        string actual = Checksum(save);
        if (!string.Equals(actual, storedChecksum.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            failure = Reject(
                SaveTransferKind.ChecksumMismatch,
                $"校验和对不上（信封里写的是 {Head(storedChecksum.Trim())}，"
                + $"这份内容的实际值是 {Head(actual)}）——文本被改动或被截断过。请整份重新复制一次。",
                ReadString(root, "PackId"),
                saveVersion);
            return false;
        }

        // ④ 信封声明的版本必须与存档本体自己声明的版本一致：
        //    不一致说明有人手改过信封，或者文本是拼起来的。
        //    读不出本体版本时不在这里报错——那是"存档坏了"，紧接着的 Parse 会说得比这里准。
        int innerVersion;
        if (TryReadSaveVersion(save, out innerVersion) && innerVersion != saveVersion)
        {
            failure = Reject(
                SaveTransferKind.CorruptEnvelope,
                $"导出文本自相矛盾：信封声明存档版本 {saveVersion}，里面的存档写的是 {innerVersion}。",
                ReadString(root, "PackId"),
                saveVersion);
            return false;
        }

        envelope = new Envelope(
            ReadString(root, "PackId"),
            saveVersion,
            ReadString(root, "FrameworkVersion"),
            ReadString(root, "ExportedAt"),
            actual,
            save);
        return true;
    }

    /// <summary>
    /// 读出存档本体声明的版本号。<para>
    /// <b>刻意不抛</b>：它在两条路上被调用——<see cref="Wrap"/> 收到一份非存档时要立刻响，
    /// 而拆信封时读不出本体版本只意味着"本体可能坏了"，那句话该由
    /// <see cref="SaveSerializer.Parse"/> 来说（它说得比这里准）。让这里抛异常会让
    /// "坏输入"从 <see cref="SaveManager.Import"/> 里漏出去变成一次崩溃。
    /// </para>
    /// </summary>
    /// <param name="saveJson">存档本体文本。</param>
    /// <param name="version">存档声明的版本；字段缺失按 0（最老的格式）。</param>
    /// <returns>读得出为 <c>true</c>。</returns>
    private static bool TryReadSaveVersion(string saveJson, out int version)
    {
        version = 0;
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(saveJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is not JsonObject root) return false;
        if (root["Version"] is not { } declared) return true;
        if (declared is JsonValue value && value.TryGetValue(out int parsed))
        {
            version = parsed;
            return true;
        }

        return false;
    }

    /// <summary>读一个字符串字段；缺失或类型不对返回 <c>null</c>（诊断字段用它，不做判定）。</summary>
    private static string? ReadString(JsonObject root, string name)
        => root[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>读一个整数字段；缺失或类型不对返回 <c>false</c>。</summary>
    private static bool TryReadInt(JsonObject root, string name, out int value)
    {
        value = 0;
        return root[name] is JsonValue node && node.TryGetValue(out value);
    }

    /// <summary>只要前几位就够分辨"是不是同一份"，整串抄进消息里没人读。</summary>
    private static string Head(string checksum)
        => checksum.Length > 19 ? checksum[..19] + "…" : checksum;

    /// <summary>造一条失败结果。</summary>
    private static SaveTransferResult Reject(SaveTransferKind kind, string message, string? packId, int saveVersion)
        => new(false, kind, message, packId, saveVersion, 0, []);

    /// <summary>拆出来的信封内容（内部形状，不对外承诺）。</summary>
    /// <param name="PackId">声明的包 id。</param>
    /// <param name="SaveVersion">声明的存档版本。</param>
    /// <param name="FrameworkVersion">导出时的框架版本（诊断）。</param>
    /// <param name="ExportedAt">导出时刻（诊断）。</param>
    /// <param name="Checksum">校验和（已核对通过）。</param>
    /// <param name="Save">存档本体文本。</param>
    internal sealed record Envelope(
        string? PackId,
        int SaveVersion,
        string? FrameworkVersion,
        string? ExportedAt,
        string Checksum,
        string Save);

    /// <summary>
    /// 内部一致性检查：<b>数字必须是有限的</b>。<para>
    /// 它必须单独存在，因为"写入之前先反解一遍"那道闸<b>抓不到这个</b>：
    /// <see cref="SaveSerializer"/> 打开了 <c>AllowNamedFloatingPointLiterals</c>，
    /// 于是 <c>"Cookies": "NaN"</c> 是<b>解析成功</b>的。一份 <c>NaN</c> 存档会通过全部
    /// 既有闸门、成为当前存档，然后把之后每一次结算都变成 <c>NaN</c>。
    /// </para>
    /// <para>
    /// <b>这张清单是有意写死的</b>：新增一个 <c>double</c> 字段时要同时加进来，
    /// 守卫 <c>SaveData_DoubleFieldsAreAllCoveredByTheFiniteCheck</c> 会因为清单与
    /// <see cref="SaveData"/> 的实际字段对不上而变红——它就是为了提醒这件事。
    /// </para>
    /// </summary>
    /// <param name="data">已解析的存档。</param>
    /// <returns>第一个非有限数字的字段路径；全都有限时为 <c>null</c>。</returns>
    internal static string? FindNonFinite(SaveData data)
    {
        string? bad =
            Check(nameof(SaveData.Cookies), data.Cookies)
            ?? Check(nameof(SaveData.CookiesEarnedThisRun), data.CookiesEarnedThisRun)
            ?? Check(nameof(SaveData.CookiesEarnedAllTime), data.CookiesEarnedAllTime)
            ?? Check(nameof(SaveData.HandMadeCookies), data.HandMadeCookies)
            ?? Check(nameof(SaveData.TotalClicks), data.TotalClicks)
            ?? Check(nameof(SaveData.GoldenCookiesClicked), data.GoldenCookiesClicked)
            ?? Check(nameof(SaveData.PrestigeChips), data.PrestigeChips)
            ?? Check(nameof(SaveData.PrestigeChipsSpent), data.PrestigeChipsSpent)
            ?? Check(nameof(SaveData.PlayTimeSeconds), data.PlayTimeSeconds)
            ?? Check(nameof(SaveData.EraEnteredPlayTimeSeconds), data.EraEnteredPlayTimeSeconds)
            ?? Check(nameof(SaveData.GoldenCookieCountdown), data.GoldenCookieCountdown);

        if (bad is not null) return bad;

        foreach ((string key, double value) in data.Counters)
        {
            bad = Check($"{nameof(SaveData.Counters)}[\"{key}\"]", value);
            if (bad is not null) return bad;
        }

        foreach (BuffSave buff in data.Buffs)
        {
            bad = Check($"{nameof(SaveData.Buffs)}[\"{buff.Id}\"].RemainingSeconds", buff.RemainingSeconds)
                  ?? Check($"{nameof(SaveData.Buffs)}[\"{buff.Id}\"].TotalSeconds", buff.TotalSeconds);
            if (bad is not null) return bad;
        }

        foreach (GoldenCookieSave spawn in data.GoldenCookies)
        {
            bad = Check($"{nameof(SaveData.GoldenCookies)}[\"{spawn.InstanceId}\"].RemainingSeconds", spawn.RemainingSeconds)
                  ?? Check($"{nameof(SaveData.GoldenCookies)}[\"{spawn.InstanceId}\"].LifetimeSeconds", spawn.LifetimeSeconds)
                  ?? Check($"{nameof(SaveData.GoldenCookies)}[\"{spawn.InstanceId}\"].X", spawn.X)
                  ?? Check($"{nameof(SaveData.GoldenCookies)}[\"{spawn.InstanceId}\"].Y", spawn.Y);
            if (bad is not null) return bad;
        }

        foreach ((int index, EraRecord record) in data.EraHistory)
        {
            bad = Check($"{nameof(SaveData.EraHistory)}[{index}].PlayTimeSeconds", record.PlayTimeSeconds)
                  ?? Check($"{nameof(SaveData.EraHistory)}[{index}].CookiesEarned", record.CookiesEarned)
                  ?? Check($"{nameof(SaveData.EraHistory)}[{index}].ChipsGained", record.ChipsGained);
            if (bad is not null) return bad;
        }

        return null;
    }

    private static string? Check(string path, double value)
        => double.IsFinite(value) ? null : $"{path} = {value.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// 找出存档里**本内容包不认识**的 id。<para>
    /// <b>只报告，不拒绝</b>：未知 id 是刻意保留的合法状态——内容包临时下线某个建筑，
    /// 存档依然无损（<c>ARCHITECTURE.md</c> 的「存档与迁移」）。但"导进来的存档里有这个包
    /// 答不上的东西"必须**说得出来**：这正是 <c>OPEN_WORK</c> 的 W9 记着的那种形状——
    /// 界面上一切正常，没有任何地方提过一句。
    /// </para>
    /// </summary>
    /// <param name="data">已解析的存档。</param>
    /// <param name="content">当前内容包。</param>
    /// <param name="count">不认识的 id 总数。</param>
    /// <param name="sample">其中前 <see cref="MaxReportedUnknownIds"/> 个。</param>
    internal static void ProbeUnknownIds(
        SaveData data,
        GameContent content,
        out int count,
        out IReadOnlyList<string> sample)
    {
        var found = new List<string>();
        int total = 0;

        // 每个类别只建一次索引；内容包是不可变的，这张表在一次导入里不会变。
        var buildings = new HashSet<string>(content.BuildingById.Keys, StringComparer.Ordinal);
        var upgrades = new HashSet<string>(content.UpgradeById.Keys, StringComparer.Ordinal);
        var achievements = new HashSet<string>(content.AchievementById.Keys, StringComparer.Ordinal);
        var lore = new HashSet<string>(content.LoreById.Keys, StringComparer.Ordinal);
        var choices = new HashSet<string>(content.ChoiceById.Keys, StringComparer.Ordinal);
        var endings = new HashSet<string>(content.EndingById.Keys, StringComparer.Ordinal);

        void Report(IEnumerable<string> ids, HashSet<string> known, string label)
        {
            foreach (string id in ids)
            {
                if (known.Contains(id)) continue;
                total++;
                if (found.Count < MaxReportedUnknownIds) found.Add($"{label}:{id}");
            }
        }

        Report(data.Buildings.Keys, buildings, "建筑");
        Report(data.Upgrades.Keys, upgrades, "升级");
        Report(data.Achievements, achievements, "成就");
        Report(data.LoreUnlocked, lore, "叙事");
        Report(data.PendingLorePopups, lore, "待播叙事");
        Report(data.ChoiceAnswers.Keys, choices, "表态");
        Report(data.PendingChoices, choices, "待答表态");
        Report(data.EndingsReached, endings, "结局");

        count = total;
        sample = found;
    }
}
