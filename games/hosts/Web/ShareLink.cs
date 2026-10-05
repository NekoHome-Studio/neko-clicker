using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NekoClicker.Hosts;

/// <summary>
/// 一段加密载荷的**用途**。<para>
/// 它进的是密文头（并且被 AAD 覆盖），所以"把分享链接当跳层令牌用"这种事后改装
/// 会在解密第一步就被拒——而不是解出一份形状对不上的东西再去猜。
/// </para>
/// </summary>
public enum ShareLinkPurpose
{
    /// <summary><c>?k=…</c>：授权一次纪元（era）跳转。</summary>
    EraToken,

    /// <summary><c>?share=…</c>：一份用密码保护的存档分享链接。</summary>
    SaveShare,
}

/// <summary>
/// 开一段载荷失败的原因。<para>
/// 与 <c>SaveTransferKind</c> 同一个理由：把失败压成一个 <c>false</c>，
/// 守卫就只能钉住"失败了"，钉不住"失败得对"——而"密码打错了"与"链接被截断了"
/// 需要完全不同的下一步动作。
/// </para>
/// </summary>
public enum ShareLinkFailure
{
    /// <summary>成功。</summary>
    None,

    /// <summary>不是一段链接（不是 base64url / 太短 / 魔数不对）。</summary>
    NotALink,

    /// <summary>线格式版本更高（或更低），本版本不认识。</summary>
    UnsupportedVersion,

    /// <summary>解出来的用途与调用方期望的不符（拿分享链接当令牌用，或反过来）。</summary>
    UnexpectedPurpose,

    /// <summary>头部声明的迭代数在允许区间之外（下界 = 抗弱化，上界 = 抗拒绝服务）。</summary>
    IterationsOutOfRange,

    /// <summary>密码太短，拒绝产出链接。</summary>
    PasswordTooShort,

    /// <summary>
    /// 认证失败：密码不对，<b>或者</b>密文/头部被改过、被截断过。<para>
    /// AEAD <b>在原理上分不开</b>这两种情况（tag 失败就是 tag 失败），
    /// 所以消息里必须把两种可能都说出来，不许挑一个说。
    /// </para>
    /// </summary>
    WrongPasswordOrTampered,

    /// <summary>链接文本本身太长（> <see cref="ShareLinkCodec.MaxLinkChars"/>），或解开的载荷太大。</summary>
    TooLarge,

    /// <summary>解开了，但里面的载荷不是一份合法载荷（不是 JSON 对象 / 缺字段 / 字段类型不对）。</summary>
    CorruptPayload,

    /// <summary>载荷带 <c>ExpiresAt</c>，而现在已经过了那个时刻。</summary>
    Expired,

    /// <summary>载荷声明的包 id 与调用方期望的不符。</summary>
    ForeignPack,

    /// <summary>载荷里的层号不可能有效（<c>&lt; 1</c>）。</summary>
    EraOutOfRange,
}

/// <summary>
/// 解开之后的载荷。<para>
/// 这些字段**全部**在密文里（因此全部被 GCM tag 覆盖）：改不动、也伪造不出。
/// </para>
/// </summary>
/// <param name="Purpose">用途（与头部一致——不一致时根本走不到这里）。</param>
/// <param name="PackId">声明的内容包 id；没写时为 <c>null</c>。</param>
/// <param name="Era">目标层号；仅 <see cref="ShareLinkPurpose.EraToken"/> 有意义。</param>
/// <param name="ExpiresAt">过期时刻；<c>null</c> = 不过期。</param>
/// <param name="SaveText">
/// 整份导出文本（<c>SaveManager.Export()</c> 的原文）；仅 <see cref="ShareLinkPurpose.SaveShare"/> 有。
/// <b>刻意搬原文而不是"把存档拆成字段再拼回来"</b>：消费侧于是只需要调一次既有的
/// <c>SaveManager.Import(text)</c>，一条新语义都没多（见 SHARE_LINK_PLAN §5.3）。
/// </param>
public sealed record ShareLinkPayload(
    ShareLinkPurpose Purpose,
    string? PackId,
    int Era,
    DateTimeOffset? ExpiresAt,
    string? SaveText);

/// <summary>
/// 解开一段载荷的结果（与 <c>SaveTransferResult</c> 同一形状：<b>不是一个布尔值</b>）。
/// </summary>
/// <param name="Ok">是否解开并通过了全部检查。</param>
/// <param name="Failure">失败原因；成功时为 <see cref="ShareLinkFailure.None"/>。</param>
/// <param name="Message">给人看的一句话（失败时带原因与下一步）；<b>永远不含密码</b>。</param>
/// <param name="Payload">解开的载荷；失败时为 <c>null</c>。</param>
public sealed record ShareLinkOpenResult(bool Ok, ShareLinkFailure Failure, string Message, ShareLinkPayload? Payload);

/// <summary>
/// URL 上那段加密载荷的编解码（**只用 BCL**，见 <c>engine/docs/SHARE_LINK_PLAN.md</c> §3）。<para>
/// <b>三件刻意不做的事</b>：不发明密码、不用 ECB、不自己写 KDF/MAC。
/// 用的全是既有原语：PBKDF2（<see cref="Rfc2898DeriveBytes.Pbkdf2(string, byte[], int, HashAlgorithmName, int)"/>）
/// 把短口令拉慢，<see cref="AesGcm"/> 一条原语同时给机密性与完整性，gzip 在**加密之前**压缩
/// （明文全部是我们自己生成的，不与攻击者可控内容混压 ⇒ 没有压缩侧信道的前提）。
/// </para>
/// <para>
/// <b>线格式</b>（base64url 之前是定长头 + 密文 + tag）：
/// <code>
/// 0   4   magic "NKL" + 版本字节 '1'   ← 版本化：换方案就换这个字节
/// 4   1   用途：'e' 令牌 / 's' 分享链接
/// 5   4   迭代数（uint32 大端）
/// 9   16  盐
/// 25  12  nonce
/// 37  n   密文 ‖ 16 字节 GCM tag
/// </code>
/// 盐、nonce、迭代数**都在链接里、都是明文**——这是正常做法，安全性不依赖把它们藏起来；
/// 而 <c>AssociatedData</c> 取**整个头**（0~36 字节），于是"版本 / 用途 / 迭代数 / 盐 / nonce"
/// 也被 tag 覆盖，改任何一个都只会得到认证失败。
/// </para>
/// </summary>
public static class ShareLinkCodec
{
    /// <summary>载荷 JSON 的格式标签（与信封的 <c>neko-save</c> 各是各的）。</summary>
    public const string PayloadFormatTag = "neko-share";

    /// <summary>载荷 JSON 自己的格式版本（**与线格式版本、存档格式版本是三条独立的轴**）。</summary>
    public const int PayloadFormatVersion = 1;

    /// <summary>线格式版本（头里第 4 个字节）。将来换方案时它 +1，旧链接会被明确拒绝而不是解错。</summary>
    public const byte WireVersion = (byte)'1';

    /// <summary>PBKDF2 的默认迭代数（PBKDF2-HMAC-SHA256，OWASP 2023 的建议量级）。</summary>
    public const int DefaultIterations = 210_000;

    /// <summary>接受的最小迭代数：拦住"把 KDF 降到 1 轮"的链接。</summary>
    public const int MinIterations = 100_000;

    /// <summary>接受的最大迭代数：拦住"一条链接逼宿主跑几十秒 KDF"的拒绝服务形态。</summary>
    public const int MaxIterations = 4_000_000;

    /// <summary>口令的最短长度。链接里只有这一层保护，所以这一条是硬的。</summary>
    public const int MinPasswordChars = 4;

    /// <summary>短于这个长度的口令会**照样生成链接**，但消息里会明说它偏短（不替使用者做决定）。</summary>
    public const int WeakPasswordWarningChars = 12;

    /// <summary>链接文本的硬上限（约 8 KB，见 SHARE_LINK_PLAN §2.2）。超过就**不产出链接**。</summary>
    public const int MaxLinkChars = 8_000;

    /// <summary>载荷 JSON 的字符数上限（解压之后再查一次；压缩前后各有一道）。</summary>
    public const int MaxPayloadChars = 256 * 1024;

    /// <summary>压缩后密文的字节上限（防"压缩炸弹"：读满上限就判太大，不把内存吃掉）。</summary>
    public const int MaxCompressedBytes = 256 * 1024;

    private const int HeaderLength = 37;
    private const int TagLength = 16;
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int KeyLength = 32;

    /// <summary>
    /// 把一份载荷包成一段链接文本（base64url，无填充）。<para>
    /// 失败一律**抛</b>而不是返回一段坏链接：产出端拿不到"能发出去但解不开"的东西，
    /// 而消费端那道闸（<see cref="Open"/>）在原理上也分不出"密码错"与"链接坏"。
    /// </para>
    /// </summary>
    /// <param name="password">使用者自己定的口令。<b>绝不进链接</b>。</param>
    /// <param name="purpose">用途。</param>
    /// <param name="packId">内容包 id（令牌用它限定射程）。</param>
    /// <param name="saveText">整份导出文本；仅 <see cref="ShareLinkPurpose.SaveShare"/> 用。</param>
    /// <param name="era">目标层号；仅 <see cref="ShareLinkPurpose.EraToken"/> 用。</param>
    /// <param name="expiresAt">过期时刻（UTC）；<c>null</c> = 不过期。</param>
    /// <param name="iterations">PBKDF2 迭代数（默认 <see cref="DefaultIterations"/>）。</param>
    /// <returns>base64url 链接文本。</returns>
    /// <exception cref="ArgumentException">口令太短，或用途与参数对不上。</exception>
    /// <exception cref="InvalidDataException">载荷本身超限，或包出来的链接超过 <see cref="MaxLinkChars"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">迭代数在允许区间之外。</exception>
    public static string Protect(
        string password,
        ShareLinkPurpose purpose,
        string? packId = null,
        string? saveText = null,
        int era = 0,
        DateTimeOffset? expiresAt = null,
        int iterations = DefaultIterations)
    {
        // ① 口令闸：空 / 太短直接拒绝。消息里说明为什么这一条是硬的。
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordChars)
        {
            throw new ArgumentException(
                $"分享密码至少要 {MinPasswordChars} 个字符——链接里只有这一层保护，"
                + "一个空口令等于把存档明文发出去。",
                nameof(password));
        }

        if (iterations < MinIterations || iterations > MaxIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterations),
                iterations,
                $"PBKDF2 迭代数必须在 {MinIterations}~{MaxIterations} 之间。");
        }

        // ② 载荷：用途与参数必须自洽（宁可产出端抛，也不产出一段"解出来是残的"链接）。
        if (purpose == ShareLinkPurpose.EraToken && era < 1)
        {
            throw new ArgumentException($"跳层令牌的层号必须 ≥ 1（收到 {era}）。", nameof(era));
        }

        if (purpose == ShareLinkPurpose.SaveShare && string.IsNullOrWhiteSpace(saveText))
        {
            throw new ArgumentException("分享链接必须带一份导出文本。", nameof(saveText));
        }

        var payload = new JsonObject
        {
            ["Format"] = PayloadFormatTag,
            ["FormatVersion"] = PayloadFormatVersion,
            ["Kind"] = KindToken(purpose),
            ["PackId"] = packId is null ? null : JsonValue.Create(packId),
        };

        if (purpose == ShareLinkPurpose.EraToken)
        {
            payload["Era"] = era;
            payload["ExpiresAt"] = expiresAt is null
                ? null
                : JsonValue.Create(expiresAt.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        else
        {
            payload["Save"] = saveText;
        }

        // 刻意用 System.Text.Json 的**默认转义**（而不是 UnsafeRelaxedJsonEscaping）：
        // 载荷虽然永不进 DOM，但导出文本里带 <、& 这类字符，保持"转义是默认"这一条
        // 与 SAVE_TRANSFER_PLAN §2.4 记下的取舍同源；gzip 会把重复的 \u0022 压回去（实测见 §2）。
        string json = payload.ToJsonString();

        if (json.Length > MaxPayloadChars)
        {
            throw new InvalidDataException(
                $"载荷有 {json.Length} 个字符，超过上限 {MaxPayloadChars}——这份存档太大了，链接装不下。");
        }

        byte[] compressed = Compress(Encoding.UTF8.GetBytes(json));
        if (compressed.Length > MaxCompressedBytes)
        {
            throw new InvalidDataException(
                $"载荷压缩后仍有 {compressed.Length} 字节，超过上限 {MaxCompressedBytes}。");
        }

        // ③ 每条链接各自一份盐与 nonce。**绝不复用**：GCM 下 nonce 复用是灾难性的，
        //    而盐每份都换意味着"同一个密码用在多条链接上"也各派生一把不同的密钥。
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceLength);

        byte[] head = new byte[HeaderLength];
        head[0] = (byte)'N';
        head[1] = (byte)'K';
        head[2] = (byte)'L';
        head[3] = WireVersion;
        head[4] = KindByte(purpose);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(5, 4), (uint)iterations);
        salt.CopyTo(head, 9);
        nonce.CopyTo(head, 25);

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeyLength);

        byte[] cipher = new byte[compressed.Length];
        byte[] tag = new byte[TagLength];

        using (var aes = new AesGcm(key, TagLength))
        {
            // AAD = 整个头：版本 / 用途 / 迭代数 / 盐 / nonce 全部被认证。
            aes.Encrypt(nonce, compressed, cipher, tag, head);
        }

        CryptographicOperations.ZeroMemory(key);

        string link = Encode(head, cipher, tag);

        // ④ **长度闸放在产出端**：装不下就当场拒绝，绝不产出一段注定被截断的链接。
        //    截断的链接解不开，而"解不开"看起来像"密码不对"——那会把人带去查一个不存在的问题
        //    （WEB_DEBUG_GATE_PLAN §5 要消灭的正是这种失败形态）。所以这里说清"改用复制粘贴"。
        if (link.Length > MaxLinkChars)
        {
            throw new InvalidDataException(
                $"这段链接有 {link.Length} 个字符，超过上限 {MaxLinkChars}——这份存档太大，链接装不下。"
                + "请改用「导出」的复制粘贴（或下载 .json）那条路：它没有长度限制。");
        }

        return link;
    }

    /// <summary>
    /// 解开一段链接。<b>不抛异常</b>：坏输入带着精确原因回来（与 <c>SaveManager.Import</c> 同一约定）。<para>
    /// 顺序是刻意的：长度闸 → base64url → 魔数/版本 → 用途 → 迭代数 → 解密（tag）→
    /// 解压（有上限）→ 载荷 JSON → 过期 / 归属 / 层号。**每一步都在下一步之前**，
    /// 所以"坏输入"这条路上不会有任何一步偷偷跑掉。
    /// </para>
    /// </summary>
    /// <param name="password">收件人输入的口令。</param>
    /// <param name="link">链接文本（<b>允许带换行/空格</b>：聊天工具会折行，而 base64url 里没有空白字符）。</param>
    /// <param name="expected">调用方期望的用途。</param>
    /// <param name="expectedPackId">调用方期望的包 id；<c>null</c> = 不做这项检查。</param>
    /// <param name="now">当前时刻（UTC 比较用；可注入，便于用例钉住过期）。</param>
    /// <returns>结果；失败时 <see cref="ShareLinkOpenResult.Message"/> 里是原因与下一步。</returns>
    public static ShareLinkOpenResult Open(
        string password,
        string? link,
        ShareLinkPurpose expected,
        string? expectedPackId = null,
        DateTimeOffset now = default)
    {
        // ⓪ 长度闸在**解码之前**：一条被粘贴得七零八落的超长文本不该让宿主先做几 MB 的 base64 解码。
        string text = StripWhitespace(link);

        if (text.Length == 0)
        {
            return Reject(ShareLinkFailure.NotALink, "这段链接是空的——整份复制，从第一个字符到最后。");
        }

        if (text.Length > MaxLinkChars)
        {
            return Reject(
                ShareLinkFailure.TooLarge,
                $"这段链接有 {text.Length} 个字符，超过上限 {MaxLinkChars}。"
                + "它可能被截断了，或者根本不是一段链接；请让对方重新发一次（不要手工拼接）。");
        }

        byte[] raw;
        try
        {
            raw = Decode(text);
        }
        catch (FormatException)
        {
            return Reject(
                ShareLinkFailure.NotALink,
                "这不是一段分享链接：它包含 base64url 之外的字符。请整份复制对方发来的那一段。");
        }

        if (raw.Length < HeaderLength + TagLength)
        {
            return Reject(
                ShareLinkFailure.NotALink,
                $"这段链接只有 {raw.Length} 字节，连一个头都装不满——它一定是被截断了。请让对方重新发一次。");
        }

        if (raw[0] != (byte)'N' || raw[1] != (byte)'K' || raw[2] != (byte)'L')
        {
            return Reject(ShareLinkFailure.NotALink, "这不是本游戏的分享链接（魔数不对）。");
        }

        if (raw[3] != WireVersion)
        {
            return Reject(
                ShareLinkFailure.UnsupportedVersion,
                $"这段链接的格式版本是 {(char)raw[3]}，本版本只认识 {(char)WireVersion}——请更新游戏，或让对方用当前版本重新生成。");
        }

        var purpose = ParseKind(raw[4]);
        if (purpose is null)
        {
            return Reject(ShareLinkFailure.CorruptPayload, $"这段链接声明的用途 {(char)raw[4]} 不认识。");
        }

        if (purpose != expected)
        {
            return Reject(
                ShareLinkFailure.UnexpectedPurpose,
                expected == ShareLinkPurpose.EraToken
                    ? "这是一段存档分享链接，不是跳层令牌。"
                    : "这是一枚跳层令牌，不是存档分享链接——它里面没有存档可导入。");
        }

        uint iterations = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(5, 4));
        if (iterations < MinIterations || iterations > MaxIterations)
        {
            return Reject(
                ShareLinkFailure.IterationsOutOfRange,
                $"这段链接要求跑 {iterations} 轮 PBKDF2，而允许区间是 {MinIterations}~{MaxIterations}。"
                + "它要么是旧工具生成的，要么被人改过。");
        }

        byte[] head = raw[..HeaderLength];
        byte[] nonce = raw[25..37];
        byte[] cipher = raw[HeaderLength..^TagLength];
        byte[] tag = raw[^TagLength..];

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, raw[9..25], (int)iterations, HashAlgorithmName.SHA256, KeyLength);
        byte[] plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, cipher, tag, plain, head);
        }
        catch (CryptographicException)
        {
            // **不区分**密码错与密文被改：AEAD 在原理上分不开，挑一个说就是编事实。
            return Reject(
                ShareLinkFailure.WrongPasswordOrTampered,
                "打不开：密码不对，或者这段链接被改过 / 被截断了。"
                + "（这两件事在密码学上分不开。）请核对密码，或让对方重新发一次链接。");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        // 解压：**有上限**，而且读满上限就判太大——防的是压缩炸弹。
        byte[] json;
        try
        {
            json = Decompress(plain, MaxPayloadChars);
        }
        catch (InvalidDataException ex)
        {
            return Reject(
                ShareLinkFailure.TooLarge,
                $"这段链接解出来的载荷超过了上限（{ex.Message}）。请让对方换用「导出」的复制粘贴那条路。");
        }

        try
        {
            return Finish(password, Encoding.UTF8.GetString(json), expected, expectedPackId, now);
        }
        catch (JsonException ex)
        {
            return Reject(ShareLinkFailure.CorruptPayload, $"载荷不是合法 JSON（{ex.Message}）。");
        }
    }

    /// <summary>载荷解出来了，做最后一组判定（格式标签 / 版本 / 过期 / 归属 / 层号）。</summary>
    private static ShareLinkOpenResult Finish(
        string password,
        string json,
        ShareLinkPurpose expected,
        string? expectedPackId,
        DateTimeOffset now)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
        {
            return Reject(ShareLinkFailure.CorruptPayload, "载荷的根节点不是 JSON 对象。");
        }

        if (!string.Equals(ReadString(root, "Format"), PayloadFormatTag, StringComparison.Ordinal))
        {
            return Reject(ShareLinkFailure.CorruptPayload, $"载荷没有 {PayloadFormatTag} 标签。");
        }

        if (!TryReadInt(root, "FormatVersion", out int version) || version < 1)
        {
            return Reject(ShareLinkFailure.CorruptPayload, "载荷缺少 FormatVersion，或者它不是正整数。");
        }

        if (version > PayloadFormatVersion)
        {
            return Reject(
                ShareLinkFailure.UnsupportedVersion,
                $"这段链接的载荷版本是 {version}，本版本只支持 {PayloadFormatVersion}——请更新游戏。");
        }

        string? packId = ReadString(root, "PackId");
        if (expectedPackId is not null && packId is not null
            && !string.Equals(expectedPackId, packId, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                ShareLinkFailure.ForeignPack,
                $"这段链接是内容包「{packId}」的，当前会话是「{expectedPackId}」——"
                + "换一个包打开它（链接上带 ?package=），或者让对方重新生成。");
        }

        DateTimeOffset? expiresAt = null;
        if (ReadString(root, "ExpiresAt") is { Length: > 0 } expiresText)
        {
            if (!DateTimeOffset.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
            {
                return Reject(ShareLinkFailure.CorruptPayload, $"载荷里的 ExpiresAt「{expiresText}」不是时刻。");
            }

            expiresAt = parsed;
        }

        if (expiresAt is not null && now != default && now.ToUniversalTime() > expiresAt.Value.ToUniversalTime())
        {
            return Reject(
                ShareLinkFailure.Expired,
                $"这段链接在 {expiresAt.Value.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)} 就过期了"
                + "（链接的有效期是加密在里面的，改不动）。请让对方重新生成一枚。");
        }

        if (expected == ShareLinkPurpose.EraToken)
        {
            if (!TryReadInt(root, "Era", out int era) || era < 1)
            {
                return Reject(ShareLinkFailure.EraOutOfRange, "载荷里的层号缺失或小于 1。");
            }

            return new ShareLinkOpenResult(
                true,
                ShareLinkFailure.None,
                $"令牌有效：内容包「{packId ?? "未声明"}」、目标第 {era} 层"
                + (expiresAt is null
                    ? "、不过期。"
                    : $"、{expiresAt.Value.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)} 过期。"),
                new ShareLinkPayload(expected, packId, era, expiresAt, null));
        }

        string? save = ReadString(root, "Save");
        if (string.IsNullOrWhiteSpace(save))
        {
            return Reject(ShareLinkFailure.CorruptPayload, "载荷里没有导出文本（Save 字段）。");
        }

        // 口令长度只作为一句提示出现，**不进日志、不进任何持久产物**。
        string hint = password.Length < WeakPasswordWarningChars
            ? $"（这个密码只有 {password.Length} 个字符，偏短——链接若落到别人手里可以被离线枚举。）"
            : string.Empty;

        return new ShareLinkOpenResult(
            true,
            ShareLinkFailure.None,
            $"已解开分享链接（内容包「{packId ?? "未声明"}」、{save.Length} 个字符的导出文本）{hint}",
            new ShareLinkPayload(expected, packId, 0, expiresAt, save));
    }

    /// <summary>造一条失败结果。</summary>
    private static ShareLinkOpenResult Reject(ShareLinkFailure failure, string message)
        => new(false, failure, message, null);

    /// <summary>去掉所有空白字符（聊天工具折行 / 复制时带进来的换行不该让一段好链接变坏）。</summary>
    private static string StripWhitespace(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c)) builder.Append(c);
        }

        return builder.ToString();
    }

    private static byte KindByte(ShareLinkPurpose purpose)
        => purpose == ShareLinkPurpose.EraToken ? (byte)'e' : (byte)'s';

    private static ShareLinkPurpose? ParseKind(byte value) => value switch
    {
        (byte)'e' => ShareLinkPurpose.EraToken,
        (byte)'s' => ShareLinkPurpose.SaveShare,
        _ => null,
    };

    private static string KindToken(ShareLinkPurpose purpose)
        => purpose == ShareLinkPurpose.EraToken ? "era" : "save";

    /// <summary>base64url（<c>-</c>/<c>_</c>，无填充）编码。</summary>
    private static string Encode(byte[] head, byte[] cipher, byte[] tag)
    {
        byte[] all = new byte[head.Length + cipher.Length + tag.Length];
        head.CopyTo(all, 0);
        cipher.CopyTo(all, head.Length);
        tag.CopyTo(all, head.Length + cipher.Length);

        return Convert.ToBase64String(all).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>base64url 解码（补回填充）。非法字符抛 <see cref="FormatException"/>。</summary>
    private static byte[] Decode(string text)
    {
        string padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// 解压，但**有上限**：读满 <paramref name="maxBytes"/>+1 就判太大。<para>
    /// 压缩炸弹防的就是"几 KB 的密文解出几百 MB"——不设上限的话，这一行就能把宿主的内存吃掉，
    /// 而它读的还是别人发来的一段文本。
    /// </para>
    /// </summary>
    private static byte[] Decompress(byte[] data, int maxBytes)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        byte[] buffer = new byte[8192];
        int total = 0;
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new InvalidDataException($"解压后超过 {maxBytes} 字节");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static string? ReadString(JsonObject root, string name)
        => root[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static bool TryReadInt(JsonObject root, string name, out int value)
    {
        value = 0;
        return root[name] is JsonValue node && node.TryGetValue(out value);
    }
}

/// <summary>
/// 一次 <c>--mint-link</c> 请求的**纯数据**形态（<c>Program.cs</c> 把命令行参数翻成它）。<para>
/// 为什么要这样一层：铸造端的校验（层号范围、包存不存在、钥匙设没设）原先整个长在
/// <c>Program.cs</c> 里，而那个文件**不在共享源码清单**里（它碰 ASP.NET），
/// 于是"层号越界会被拒绝"这句话在自动闸门里<b>一条守卫都没有</b>
/// （<c>SHARE_LINK_PLAN.md</c> §13 末尾自己记下了这条缺口）。
/// 参数一旦是可构造的纯数据，校验就能被 C# 用例直接钉住，而命令行那一侧只负责翻译。
/// </para>
/// </summary>
/// <param name="Key">钥匙；只从环境变量 <see cref="EraLinkMinter.KeyVariable"/> 来，没设时为 <c>null</c>。</param>
/// <param name="PackageId"><c>--package</c> 的值。</param>
/// <param name="EraText"><c>--era</c> 的原文（<b>不在这里解析</b>：解析失败要说"用法"而不是"越界"）。</param>
/// <param name="ExpiresHoursText"><c>--expires-hours</c> 的原文；<c>null</c> = 不设过期。</param>
/// <param name="BaseUrl">链接前缀（<c>--base-url</c> / <c>--urls</c> / 默认地址）。</param>
public sealed record EraLinkMintRequest(
    string? Key,
    string? PackageId,
    string? EraText,
    string? ExpiresHoursText,
    string BaseUrl);

/// <summary>
/// 一次铸造的结果。<b>不是</b>一个布尔值：与 <see cref="ShareLinkOpenResult"/> 同一条理由——
/// 脚本要能区分"铸出来了"（拿 stdout 那行 URL）与"拒绝了，且拒绝得对"（拿退出码与那句话）。
/// </summary>
/// <param name="ExitCode">进程退出码；成功为 <c>0</c>、拒绝为 <see cref="EraLinkMinter.FailureExitCode"/>。</param>
/// <param name="Url">铸出来的链接（**走 stdout**）；拒绝时为 <c>null</c>。</param>
/// <param name="Message">给人看的一句话（**走 stderr**）：成功时报出层号与字符数，拒绝时说清原因与下一步。</param>
public sealed record EraLinkMintResult(int ExitCode, string? Url, string Message)
{
    /// <summary>是否铸出来了。</summary>
    public bool Ok => ExitCode == 0;
}

/// <summary>
/// 跳层令牌的铸造端（<c>--mint-link</c>，见 <c>engine/docs/SHARE_LINK_PLAN.md</c> §4.2）。<para>
/// <b>它不启动宿主、不监听端口、不读命令行</b>：输入是 <see cref="EraLinkMintRequest"/> 与
/// 内容包目录的两件事（有哪些 id、某个 id 最多能跳到第几层），输出是
/// <see cref="EraLinkMintResult"/>。于是这一整段校验在 <c>engine/tests</c> 里有守卫，
/// 而 <c>Program.cs</c> 只剩"读环境变量、把参数翻过来、按分工打印"。
/// </para>
/// <para>
/// 判据与 <c>GameHost.JumpToEraAsync</c> 同源：<b>把错误留在铸造那一步</b>，
/// 而不是留给十分钟后的浏览器。所以这里对层号查的是这个包<b>真实的</b>上界。
/// </para>
/// </summary>
public static class EraLinkMinter
{
    /// <summary>
    /// 令牌门钥匙所在的<b>环境变量名</b>（与明文门的 <c>NEKO_DEBUG_KEY</c> 分开的一把）。<para>
    /// 这里只有名字、没有值：仓库与发布产物里不存在任何密钥，所以"没设"时这扇门根本不存在。
    /// 名字放在这里（而不是留在 <c>Program.cs</c>）是为了让"没设钥匙就拒绝铸造"那句话
    /// 与它提到的那个变量名在<b>同一份被测试的代码</b>里——否则用例只能去断言一句抄来的文案。
    /// </para>
    /// </summary>
    public const string KeyVariable = "NEKO_URL_KEY";

    /// <summary>拒绝铸造时的退出码（用法错、包不存在、层号越界、没设钥匙都用它）。</summary>
    public const int FailureExitCode = 2;

    /// <summary>
    /// 铸一枚跳层令牌。<b>不抛异常</b>：每一种拒绝都带着自己的那句话回来
    /// （与 <see cref="ShareLinkCodec.Open"/> 同一约定）。
    /// </summary>
    /// <param name="request">请求（纯数据）。</param>
    /// <param name="knownPackIds">内容包目录里的全部 id（用来在"没有这个包"时报出可用的那些）。</param>
    /// <param name="maxEraIndex">按 id 查"最多能跳到第几层"；包不存在时返回 <c>null</c>。</param>
    /// <returns>结果；拒绝时 <see cref="EraLinkMintResult.Url"/> 为 <c>null</c>。</returns>
    public static EraLinkMintResult Mint(
        EraLinkMintRequest request,
        IReadOnlyList<string> knownPackIds,
        Func<string, int?> maxEraIndex)
    {
        // ① 钥匙装置本身在不在。**排在最前面**：没设钥匙时"命令怎么写"是无关紧要的，
        //    而先报"用法错"会让人去改一条本来就写对的命令（`Program.cs` 原来的顺序就是这样）。
        if (string.IsNullOrEmpty(request.Key))
        {
            return Refuse(
                $"铸不出令牌：没有设置环境变量 {KeyVariable}。"
                + $"先临时设一把钥匙（$env:{KeyVariable} = '…'），再重跑这条命令。");
        }

        // ② 参数齐不齐、层号是不是个数。这里只说"用法"，不说"越界"——两件事的下一步完全不同。
        if (request.PackageId is null || request.EraText is null
            || !int.TryParse(request.EraText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int era))
        {
            return Refuse(
                "用法：--mint-link --package <包 id> --era <层号> [--expires-hours <小时>] [--base-url <地址>]");
        }

        // ③ 这个包在不在。id 的比对口径与 PackageCatalog.Find 一致（忽略大小写），
        //    而链接里带出去的必须是**目录里的那个 id**，不是使用者随手敲的大小写。
        string? packId = knownPackIds.FirstOrDefault(
            id => string.Equals(id, request.PackageId, StringComparison.OrdinalIgnoreCase));

        int? max = packId is null ? null : maxEraIndex(packId);
        if (packId is null || max is null)
        {
            return Refuse(
                $"没有内容包 <{request.PackageId}>。可用的是：{string.Join("、", knownPackIds)}");
        }

        if (max.Value < 1)
        {
            return Refuse($"内容包「{packId}」没有分层转生，没有层可跳。");
        }

        // ④ 层号范围：对着这个包**真实的**层数查一次。
        if (era < 1 || era > max.Value)
        {
            return Refuse($"第 {era} 层越界（内容包「{packId}」只有 {max.Value} 层）。合法范围：1..{max.Value}。");
        }

        DateTimeOffset? expiresAt = null;
        if (request.ExpiresHoursText is { } hoursText)
        {
            // 默认**不设过期**（与明文门一样是"本机调试用"）；给了这个参数就必须给一个正数，
            // 而不是"看不懂就当成没过期"——那会把一个明确的意图变成一个沉默的默认值。
            if (!double.TryParse(hoursText, NumberStyles.Float, CultureInfo.InvariantCulture, out double hours)
                || hours <= 0)
            {
                return Refuse($"--expires-hours 需要一个正数（收到「{hoursText}」）。");
            }

            expiresAt = DateTimeOffset.UtcNow.AddHours(hours);
        }

        try
        {
            string token = ShareLinkCodec.Protect(
                request.Key, ShareLinkPurpose.EraToken, packId, null, era, expiresAt);
            string baseUrl = request.BaseUrl.TrimEnd('/');

            return new EraLinkMintResult(
                0,
                $"{baseUrl}/?package={packId}&k={token}",
                $"已铸一枚跳层令牌：内容包「{packId}」第 {era} 层、{token.Length} 个字符"
                + (expiresAt is null
                    ? "、不过期。"
                    : $"、{expiresAt.Value.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)} 过期。"));
        }
        catch (Exception ex)
        {
            return Refuse($"铸不出令牌：{ex.Message}");
        }
    }

    /// <summary>造一条"拒绝铸造"的结果（URL 一定是 <c>null</c>——绝不产出宿主解不开的链接）。</summary>
    private static EraLinkMintResult Refuse(string message) => new(FailureExitCode, null, message);
}
