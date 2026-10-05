using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Persistence;
using NekoClicker.Hosts;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// URL 上那段加密载荷（跳层令牌 + 分享链接）的守卫，见 <c>engine/docs/SHARE_LINK_PLAN.md</c>。<para>
/// 这一份测验的是<b>两件很容易被"看着像成功了"骗过去的事</b>：
/// </para>
/// <list type="number">
///   <item><b>密码学那几道闸真的拦得住</b>：密码错、密文被改、<b>头被改</b>（AAD 真的在起作用）、
///         版本/用途不符、迭代数越界、链接与载荷超限、口令太短——每一种都要有<b>自己的</b>类别，
///         而不是一句"失败了"。</item>
///   <item><b>坏链接一个字节都不许动</b>：解密失败发生在 <c>SaveManager.Import</c> <b>之前</b>，
///         所以磁盘上那份存档、它的 <c>.bak</c>、以及内存里的会话都必须原封不动。</item>
/// </list>
/// <para>
/// 里面那个 <see cref="CraftLink"/> 是<b>刻意手写的第二份线格式</b>：只有它能造出
/// <c>Protect</c> 自己拒绝产出的东西（超上限的载荷、迭代数为 1 的头、被改过的字节），
/// 而"压缩炸弹"那条守卫没有它就根本触发不到。代价是它必须跟着线格式一起改——
/// 这个代价是值得的：它同时也是"格式漂移"的第二双眼睛。
/// </para>
/// <para>
/// 与 <see cref="SaveTransferHostTests"/> 同一条纪律：全部指向临时目录，
/// 仓库里真实的 <c>saves/</c> 与 <c>artifacts/latency.txt</c>（真人数据）一个字节都不碰。
/// </para>
/// </summary>
public static class ShareLinkTests
{
    private const string GoodPassword = "correct-horse-battery";

    // ------------------------------------------------------------------ 编解码本身

    /// <summary>
    /// 往返：一份<b>真的</b>导出文本进链接、出链接，必须逐字节相同，
    /// 而且链接里<b>不许出现</b>存档里的字、也不许出现密码。<para>
    /// 「不许出现」不是洁癖：这是"密文不泄漏存档"这句话唯一能被机器验的形式。
    /// 用的标记是 <c>CookiesEarnedAllTime</c> 这种 20 个字符的长词——
    /// base64url 的字母表里虽然有字母，但 20 个字符偶然撞上的概率可以忽略。
    /// </para>
    /// </summary>
    [Test]
    public static void ShareLink_RoundTripsTheExportTextByteForByteAndLeaksNothing()
    {
        string root = NewTempDir();
        try
        {
            using var host = new WebHostScope(root);
            SetDeterministicState(host);

            CommandOutcome exported = host.Export();
            Check.True(exported.Ok, $"前置条件：导出必须成功（{exported.Message}）。");
            string text = exported.Text!;

            string link = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", text);

            ShareLinkOpenResult opened = ShareLinkCodec.Open(GoodPassword, link, ShareLinkPurpose.SaveShare, "neko");
            Check.True(opened.Ok, $"同一个密码必须解得开：{opened.Message}");
            Check.Equal(text, opened.Payload!.SaveText!, "解出来的必须是**那一份字节**，逐字符相同。");
            Check.Equal("neko", opened.Payload.PackId!, "包标识也要在载荷里。");

            Check.False(link.Contains("CookiesEarnedAllTime", StringComparison.Ordinal), "链接里不许出现存档里的字。");
            Check.False(link.Contains(GoodPassword, StringComparison.Ordinal), "链接里不许出现密码。");
            Check.False(link.Contains("neko-save", StringComparison.Ordinal), "链接里不许出现信封标签（连格式都不该露）。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 密码错与密文被改：两者都必须是 <see cref="ShareLinkFailure.WrongPasswordOrTampered"/>，
    /// 而且消息里必须把<b>两种可能都说出来</b>——AEAD 在原理上分不开它们，挑一个说就是编事实。
    /// </summary>
    [Test]
    public static void ShareLink_WrongPasswordAndTamperedCiphertextBothFailLoudly()
    {
        string link = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", "{\"Version\":1}");

        ShareLinkOpenResult wrong = ShareLinkCodec.Open("not-the-password", link, ShareLinkPurpose.SaveShare, "neko");
        Check.False(wrong.Ok, "密码不对必须被拒。");
        Check.Equal(ShareLinkFailure.WrongPasswordOrTampered, wrong.Failure, "类别必须是「打不开」。");
        Check.Contains(wrong.Message, "密码不对", "消息里要说清可能是密码错。");
        Check.Contains(wrong.Message, "改过", "消息里也要说清可能是链接被改过——这两件事分不开。");

        // 翻一个**密文**里的字节（头之后的第一个字节）。
        ShareLinkOpenResult tampered = ShareLinkCodec.Open(
            GoodPassword, Mutate(link, 40, 0x00), ShareLinkPurpose.SaveShare, "neko");
        Check.False(tampered.Ok, "改了密文必须被拒。");
        Check.Equal(ShareLinkFailure.WrongPasswordOrTampered, tampered.Failure, "tag 失败就是这一个类别。");
    }

    /// <summary>
    /// <b>AAD 真的在起作用</b>：把<b>头</b>里的字节改掉（用途、盐），认证必须失败；
    /// 把版本字节改掉，必须在解密之前就以 <see cref="ShareLinkFailure.UnsupportedVersion"/> 被拒。<para>
    /// 这条守卫的对手是"把 <c>AssociatedData</c> 传成 null 也照样全绿"——
    /// 没有它，AAD 就只是注释里的一句话（判别力证明见 SHARE_LINK_PLAN §11）。
    /// </para>
    /// </summary>
    [Test]
    public static void ShareLink_HeaderMetadataIsAuthenticated()
    {
        string link = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", "{\"Version\":1}");

        // ① 把用途字节从 's' 改成 'e'：于是"用令牌那道门去开它"不再被用途检查拦住，
        //    只剩 AAD 能拦——这正是要验的那一条。
        ShareLinkOpenResult kindFlipped = ShareLinkCodec.Open(
            GoodPassword, Mutate(link, 4, (byte)'e'), ShareLinkPurpose.EraToken, "neko");
        Check.False(kindFlipped.Ok, "用途字节被改过：必须被 AAD 拦下，而不是被当成一枚令牌解开。");
        Check.Equal(ShareLinkFailure.WrongPasswordOrTampered, kindFlipped.Failure, "拦住它的必须是 tag。");

        // ② 改一个盐的字节（盐参与 KDF，也参与 AAD）。
        ShareLinkOpenResult saltFlipped = ShareLinkCodec.Open(
            GoodPassword, Mutate(link, 9, 0x00), ShareLinkPurpose.SaveShare, "neko");
        Check.False(saltFlipped.Ok, "盐被改过必须被拒。");

        // ③ 版本字节：在解密之前就该被点名。
        ShareLinkOpenResult versionFlipped = ShareLinkCodec.Open(
            GoodPassword, Mutate(link, 3, (byte)'9'), ShareLinkPurpose.SaveShare, "neko");
        Check.Equal(ShareLinkFailure.UnsupportedVersion, versionFlipped.Failure, "版本不认识就要说「版本不认识」。");
        Check.Contains(versionFlipped.Message, "版本", "消息里要点出是版本的问题。");
    }

    /// <summary>
    /// 迭代数的上下界：下界拦"把 KDF 降到 1 轮"，上界拦"一条链接逼宿主跑几十秒"。<para>
    /// 两头都用<a>手写的线格式</a>造——<c>Protect</c> 自己拒绝产出这种链接，
    /// 所以只测 <c>Protect</c> 的话，<c>Open</c> 那两道闸根本不会被执行到。
    /// </para>
    /// </summary>
    [Test]
    public static void ShareLink_IterationFloorAndCeilingAreEnforced()
    {
        string payload = PayloadJson("{\"Version\":1}");

        ShareLinkOpenResult tooWeak = ShareLinkCodec.Open(
            GoodPassword, CraftLink(payload, GoodPassword, iterations: 1), ShareLinkPurpose.SaveShare, "neko");
        Check.Equal(ShareLinkFailure.IterationsOutOfRange, tooWeak.Failure, "1 轮 PBKDF2 等于没有拉伸，必须拒绝。");

        ShareLinkOpenResult tooSlow = ShareLinkCodec.Open(
            GoodPassword,
            CraftLink(payload, GoodPassword, iterations: ShareLinkCodec.MaxIterations + 1),
            ShareLinkPurpose.SaveShare,
            "neko");
        Check.Equal(ShareLinkFailure.IterationsOutOfRange, tooSlow.Failure, "上界之外必须拒绝（不然是一条拒绝服务）。");

        bool threw = false;
        try
        {
            ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", "{}", iterations: 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Check.True(threw, "产出端同样不许造出迭代数为 1 的链接。");
    }

    /// <summary>
    /// 尺寸闸：链接太长、载荷太大（压缩炸弹）都必须响亮拒绝。<para>
    /// 炸弹那一条只有手写线格式造得出来：<c>Protect</c> 在压缩之前就把超限载荷拒了，
    /// 所以"解压上限"那道闸在正常路径上永远走不到——它防的正是<b>别人手搓的</b>一段链接。
    /// </para>
    /// </summary>
    [Test]
    public static void ShareLink_RejectsOversizedLinksAndPayloads()
    {
        // ① 链接文本本身超限：在 base64 解码**之前**就该被拒。
        ShareLinkOpenResult huge = ShareLinkCodec.Open(
            GoodPassword, new string('A', ShareLinkCodec.MaxLinkChars + 1), ShareLinkPurpose.SaveShare, "neko");
        Check.Equal(ShareLinkFailure.TooLarge, huge.Failure, "超长链接要在解码之前就被拒。");
        Check.Contains(huge.Message, "超过上限", "消息里要报出上限这件事。");

        // ② 压缩炸弹：300 KB 的载荷压成几百字节，解压那一步必须停下来。
        string bomb = PayloadJson(new string('A', 300_000));
        ShareLinkOpenResult exploded = ShareLinkCodec.Open(
            GoodPassword, CraftLink(bomb, GoodPassword), ShareLinkPurpose.SaveShare, "neko");
        Check.Equal(ShareLinkFailure.TooLarge, exploded.Failure, "解压超过上限必须拒绝，而不是把内存吃掉。");

        // ③ 产出端：载荷本身就超限时，连链接都不该产出来。
        bool threw = false;
        try
        {
            ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", new string('A', 400_000));
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        Check.True(threw, "太大的存档：产出端必须拒绝，而不是产出一段注定被拒的链接。");
    }

    /// <summary>
    /// 口令闸：太短拒绝（空口令等于把存档明文发出去），偏短则**照样生成**但要在消息里说出来。
    /// </summary>
    [Test]
    public static void ShareLink_ShortPasswordsAreRefusedOrFlagged()
    {
        foreach (string weak in new[] { string.Empty, "abc" })
        {
            bool threw = false;
            try
            {
                ShareLinkCodec.Protect(weak, ShareLinkPurpose.SaveShare, "neko", "{}");
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Check.True(threw, $"口令「{weak}」太短，必须拒绝（最少 {ShareLinkCodec.MinPasswordChars} 个字符）。");
        }

        // 偏短但合规：链接照发，消息里必须带一句实话。
        string link = ShareLinkCodec.Protect("12345", ShareLinkPurpose.SaveShare, "neko", "{\"Version\":1}");
        ShareLinkOpenResult opened = ShareLinkCodec.Open("12345", link, ShareLinkPurpose.SaveShare, "neko");
        Check.True(opened.Ok, $"合规口令必须能解开：{opened.Message}");
        Check.Contains(opened.Message, "偏短", "短口令要在消息里被点出来，而不是静默放行。");
    }

    /// <summary>
    /// 聊天工具会折行：链接里的空白字符一律先去掉再解码（base64url 的字母表里没有空白，
    /// 所以这一步不会把坏输入救活，只会把好输入救回来）。
    /// </summary>
    [Test]
    public static void ShareLink_SurvivesLineWrapping()
    {
        string link = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "neko", "{\"Version\":1}");

        var wrapped = new StringBuilder();
        for (int i = 0; i < link.Length; i += 64)
        {
            wrapped.Append(link, i, Math.Min(64, link.Length - i)).Append('\n');
        }

        ShareLinkOpenResult opened = ShareLinkCodec.Open(
            GoodPassword, wrapped.ToString(), ShareLinkPurpose.SaveShare, "neko");
        Check.True(opened.Ok, $"折过行的链接必须照样能开：{opened.Message}");
    }

    /// <summary>
    /// 跳层令牌：放行与四种拒绝（密码错 / 包不符 / 过期 / 用途不符）各一条。<para>
    /// 注意这里<b>不验</b>"层号是否在这个包的范围内"——那一条归 <c>GameHost.JumpToEraAsync</c>
    /// （于是 400 里带的一定是这个包真实的合法范围），宿主那一层由 <see cref="WebHostScope"/> 的用例与
    /// <c>tools/api-test.ps1</c> 端到端验。
    /// </para>
    /// </summary>
    [Test]
    public static void EraToken_AuthorizesExactlyTheRightDoor()
    {
        DateTimeOffset now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        string token = ShareLinkCodec.Protect(
            "host-url-key",
            ShareLinkPurpose.EraToken,
            packId: "lab",
            era: 7,
            expiresAt: now.AddHours(24));

        ShareLinkOpenResult opened = ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.EraToken, "lab", now);
        Check.True(opened.Ok, $"对的钥匙 + 对的包 + 没过期：必须放行（{opened.Message}）。");
        Check.Equal(7, opened.Payload!.Era, "目标层号要原样带出来。");
        Check.Equal("lab", opened.Payload.PackId!, "包标识要原样带出来。");

        Check.Equal(
            ShareLinkFailure.WrongPasswordOrTampered,
            ShareLinkCodec.Open("wrong-key", token, ShareLinkPurpose.EraToken, "lab", now).Failure,
            "钥匙不对必须拒绝。");

        Check.Equal(
            ShareLinkFailure.ForeignPack,
            ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.EraToken, "cafe", now).Failure,
            "令牌只对铸它的那个包有效，别的包必须拒绝。");

        ShareLinkOpenResult expired =
            ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.EraToken, "lab", now.AddHours(25));
        Check.Equal(ShareLinkFailure.Expired, expired.Failure, "过期必须拒绝。");
        Check.Contains(expired.Message, "过期", "消息里要说清是过期，而不是「解不开」。");

        Check.Equal(
            ShareLinkFailure.UnexpectedPurpose,
            ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.SaveShare, "lab", now).Failure,
            "拿令牌当分享链接用必须被用途闸拦住。");

        bool threw = false;
        try
        {
            ShareLinkCodec.Protect("host-url-key", ShareLinkPurpose.EraToken, "lab", era: 0);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        Check.True(threw, "层号 < 1 的令牌不许铸出来（它一定解不开——范围校验在宿主那一步）。");
    }

    /// <summary>
    /// <b>典型存档装得下、大存档装不下而且要当场说出来</b>（SHARE_LINK_PLAN §2）。<para>
    /// 6203 个字符正是仓库里最大那份真实导出文本的实测长度
    /// （<c>SAVE_TRANSFER_PLAN.md</c> §2.4：<c>ninelines.json</c> 3718 → 6203）。
    /// 这里用一段等长的合成文本，是为了让这条断言<b>确定</b>：真实存档会被正在玩的宿主
    /// 每 60 秒重写一次，把它当夹具等于把守卫交给运气。
    /// </para>
    /// </summary>
    [Test]
    public static void ShareLink_FitsATypicalSaveAndRefusesAHugeOne()
    {
        string typical = SyntheticExportText(6203);
        string link = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "ninelines", typical);

        Console.WriteLine(
            $"      链接长度：导出文本 {typical.Length} 字符 → base64url {link.Length} 字符"
            + $"（上限 {ShareLinkCodec.MaxLinkChars}）。");

        Check.True(
            link.Length <= ShareLinkCodec.MaxLinkChars,
            $"最大那份真实存档（导出 {typical.Length} 字符）必须装得进链接，"
            + $"实际 {link.Length} 字符、上限 {ShareLinkCodec.MaxLinkChars}。");

        Check.True(
            ShareLinkCodec.Open(GoodPassword, link, ShareLinkPurpose.SaveShare, "ninelines").Ok,
            "这一段合成文本必须能原样解开（长度断言的前提）。");

        // 六倍大的、**压不动**的存档：装不下是物理事实，所以要拒绝，
        // 而不是产出一段注定被截断的链接（截断的链接看起来像"密码不对"）。
        bool threw = false;
        try
        {
            ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "ninelines", IncompressibleText(40_000));
        }
        catch (InvalidDataException ex)
        {
            threw = true;
            Check.Contains(ex.Message, "链接", "拒绝的理由要说「链接装不下」，而不是一句含糊的失败。");
            Check.Contains(ex.Message, "复制粘贴", "还要指出下一步：改用复制粘贴那条路。");
        }

        Check.True(threw, "装不下的存档必须当场拒绝——截断的链接看起来像「密码不对」，会把人带去查不存在的问题。");
    }

    // ------------------------------------------------------------------ 铸造端（--mint-link 的校验）

    /// <summary>
    /// <c>--mint-link</c> 的拒绝面：没设钥匙 / 包不存在 / 没有层 / 层号越界（下界与上界各一条）/
    /// 用法错。<para>
    /// <b>这一段原先一条守卫都没有</b>：校验整个长在 <c>games/hosts/Web/Program.cs</c> 里，
    /// 而那个文件碰 ASP.NET、不在共享源码清单里（<c>SHARE_LINK_PLAN.md</c> §13 末尾自己记下了这条缺口，
    /// "要验它就得真起宿主"）。校验搬进 <see cref="EraLinkMinter.Mint"/> 之后，这里才钉得住它——
    /// 而命令行那一侧（真的 <c>--mint-link</c> 进程）由 <c>tools/api-test.ps1</c> 那一段常驻地验。
    /// </para>
    /// <para>
    /// 上界用的是<b>这个包真实的上界</b>（7），而不是一个随便挑的大数：
    /// "越界"必须是"对着真实层数查出来的"，否则这条守卫只证明"有个数被拒了"。
    /// </para>
    /// </summary>
    [Test]
    public static void EraLinkMint_RefusesMissingKeyUnknownPackAndOutOfRangeEras()
    {
        // ① 钥匙装置本身不在：拒绝铸造，而且要说清是哪个环境变量。
        EraLinkMintResult noKey = EraLinkMinter.Mint(MintRequest(key: null), MintPackIds, MintMaxEra);
        Check.Equal(EraLinkMinter.FailureExitCode, noKey.ExitCode, "没设钥匙必须拒绝铸造。");
        Check.True(noKey.Url is null, "拒绝时不许有 URL——「宿主一定解不开的链接」正是最坏的一种成功报告。");
        Check.Contains(noKey.Message, EraLinkMinter.KeyVariable, "那句话里要说清是哪个环境变量没设。");

        // 钥匙也缺、参数也错：先说钥匙。反过来的话，人会去改一条本来就写对的命令。
        EraLinkMintResult bothWrong =
            EraLinkMinter.Mint(MintRequest(key: string.Empty, package: null, era: "七"), MintPackIds, MintMaxEra);
        Check.Contains(bothWrong.Message, EraLinkMinter.KeyVariable, "钥匙没设与参数写错同时出现时，先说钥匙。");

        // ② 不存在的包：报出它、并且报出可用的那些。
        EraLinkMintResult unknownPack = EraLinkMinter.Mint(MintRequest(package: "no-such-pack"), MintPackIds, MintMaxEra);
        Check.Equal(EraLinkMinter.FailureExitCode, unknownPack.ExitCode, "不存在的包必须拒绝。");
        Check.Contains(unknownPack.Message, "no-such-pack", "拒绝的话里要点出是哪个包不存在。");
        Check.Contains(unknownPack.Message, "lab", "还要报出可用的包 id（否则使用者只能猜）。");

        // ③ 这个包存在、但没有分层转生：命令没写错，是它没有层可跳。理由必须与「越界」分开。
        EraLinkMintResult noEras = EraLinkMinter.Mint(MintRequest(package: "cafe"), MintPackIds, MintMaxEra);
        Check.Contains(noEras.Message, "没有分层转生", "没有层的包要说「没有层可跳」，而不是「层号越界」。");

        // ④ 下界：第 0 层（以及一切 < 1 的东西）越界，消息里带真实范围。
        EraLinkMintResult below = EraLinkMinter.Mint(MintRequest(era: "0"), MintPackIds, MintMaxEra);
        Check.Contains(below.Message, "越界", "第 0 层必须被范围闸拒绝。");
        Check.Contains(below.Message, "1..7", "拒绝的话里要带这个包真实的合法范围。");

        // ⑤ 上界：8 > 这个包真实的 7。
        EraLinkMintResult above = EraLinkMinter.Mint(MintRequest(era: "8"), MintPackIds, MintMaxEra);
        Check.Equal(EraLinkMinter.FailureExitCode, above.ExitCode, "超过真实层数必须拒绝。");
        Check.Contains(above.Message, "只有 7 层", "拒绝的话里要点出这个包真实有几层。");
        Check.True(above.Url is null, "越界时同样不许产出 URL（否则浏览器十分钟后才知道）。");

        // ⑥ 用法错不是「越界」：层号不是个数 / 少了 --era / 少了 --package，三处都必须是用法那一句。
        Check.Contains(
            EraLinkMinter.Mint(MintRequest(era: "七"), MintPackIds, MintMaxEra).Message,
            "用法",
            "层号不是个数属于用法错，消息必须是用法那一句。");
        Check.Contains(
            EraLinkMinter.Mint(MintRequest(era: null), MintPackIds, MintMaxEra).Message,
            "用法",
            "少了 --era 也是用法错。");
        Check.Contains(
            EraLinkMinter.Mint(MintRequest(package: null), MintPackIds, MintMaxEra).Message,
            "用法",
            "少了 --package 也是用法错。");
    }

    /// <summary>
    /// 对照组：<b>合法层号必须铸得出来</b>，而且铸出来的那段密文要能被
    /// <see cref="ShareLinkCodec.Open"/> 按<b>宿主那一条路</b>打开（同一把钥匙、同一个包、同一个用途）。<para>
    /// 没有这一条，上面那一堆"拒绝了"可能是"这条路根本不通"——那正是假绿最经典的形状。
    /// </para>
    /// </summary>
    [Test]
    public static void EraLinkMint_MintsATokenTheHostGateOpens()
    {
        const string Prefix = "http://127.0.0.1:5273/?package=lab&k=";

        EraLinkMintResult minted = EraLinkMinter.Mint(MintRequest(era: "2"), MintPackIds, MintMaxEra);
        Check.True(minted.Ok, $"合法层号必须铸得出来：{minted.Message}");
        Check.True(
            minted.Url is not null && minted.Url.StartsWith(Prefix, StringComparison.Ordinal),
            $"URL 的形状必须是 <base>/?package=<id>&k=<密文>，实际 <{minted.Url ?? "null"}>。");
        Check.Contains(minted.Message, "第 2 层", "给人看的那句话要报出铸的是哪一层。");

        string token = minted.Url![Prefix.Length..];
        ShareLinkOpenResult opened = ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.EraToken, "lab");
        Check.True(opened.Ok, $"铸出来的令牌必须能被宿主那条路打开：{opened.Message}");
        Check.Equal(2, opened.Ok ? opened.Payload!.Era : -1, "层号要原样进密文。");
        Check.Equal("lab", opened.Ok ? opened.Payload!.PackId! : "—", "包标识要原样进密文。");
        Check.True(opened.Ok && opened.Payload!.ExpiresAt is null, "不传 --expires-hours 就是不过期。");
        Check.False(token.Contains("host-url-key", StringComparison.Ordinal), "链接里不许出现钥匙。");

        // 闭区间：1（下界）与 7（这个包真实的上界）都要铸得出来。
        Check.True(EraLinkMinter.Mint(MintRequest(era: "1"), MintPackIds, MintMaxEra).Ok, "第 1 层在下界之内。");
        Check.True(EraLinkMinter.Mint(MintRequest(era: "7"), MintPackIds, MintMaxEra).Ok, "第 7 层正好是这个包的上界。");

        // 包 id 的比对忽略大小写（与 PackageCatalog.Find 同一口径），但链接里带出去的
        // 必须是**目录里的那个 id**：否则同一枚令牌会因为大小写而 ForeignPack。
        EraLinkMintResult upper = EraLinkMinter.Mint(MintRequest(package: "LAB"), MintPackIds, MintMaxEra);
        Check.True(
            upper.Url is not null && upper.Url.Contains("package=lab&", StringComparison.Ordinal),
            $"大小写不该改变链接里的包 id：<{upper.Url ?? "null"}>");

        // 换一个带尾斜杠的基地址：尾斜杠一律去掉，否则会铸出 `//?package=`。
        EraLinkMintResult slashed = EraLinkMinter.Mint(
            MintRequest(era: "2", baseUrl: "http://127.0.0.1:5300/"), MintPackIds, MintMaxEra);
        Check.True(
            slashed.Url is not null && slashed.Url.StartsWith("http://127.0.0.1:5300/?package=lab&k=", StringComparison.Ordinal),
            $"基地址的尾斜杠要去掉：<{slashed.Url ?? "null"}>");
    }

    /// <summary>
    /// 有效期是可选的，但给了就必须是个正数。<para>
    /// 「看不懂就当成没过期」会把一个明确的意图变成一个沉默的默认值——这正是这个仓库一路在消灭的形态
    /// （与明文门的 <c>--expires-hours</c> 同一条取舍）。
    /// </para>
    /// </summary>
    [Test]
    public static void EraLinkMint_ExpiryIsOptionalButMustBePositive()
    {
        const string Prefix = "http://127.0.0.1:5273/?package=lab&k=";

        DateTimeOffset before = DateTimeOffset.UtcNow;
        EraLinkMintResult tomorrow = EraLinkMinter.Mint(
            MintRequest(era: "2", expiresHours: "24"), MintPackIds, MintMaxEra);
        DateTimeOffset after = DateTimeOffset.UtcNow;
        Check.True(tomorrow.Ok, $"给了正数的小时数必须铸得出来：{tomorrow.Message}");

        string token = tomorrow.Url![Prefix.Length..];
        ShareLinkOpenResult opened = ShareLinkCodec.Open("host-url-key", token, ShareLinkPurpose.EraToken, "lab");
        DateTimeOffset? expires = opened.Ok ? opened.Payload!.ExpiresAt : null;
        Check.True(expires is not null, "有效期要在密文里（它不是一句注释：改不动才算数）。");
        Check.True(
            expires is { } at && at >= before.AddHours(24) && at <= after.AddHours(24),
            $"过期时刻应当是「现在 + 24 小时」，实际 <{expires?.ToString("u", CultureInfo.InvariantCulture) ?? "null"}>。");
        Check.Contains(tomorrow.Message, "过期", $"给人看的那句话要报出过期时刻：{tomorrow.Message}");

        foreach (string bad in new[] { "0", "-3", "abc", "" })
        {
            EraLinkMintResult refused = EraLinkMinter.Mint(
                MintRequest(era: "2", expiresHours: bad), MintPackIds, MintMaxEra);
            Check.Equal(EraLinkMinter.FailureExitCode, refused.ExitCode, $"--expires-hours「{bad}」必须被拒绝。");
            Check.Contains(refused.Message, "正数", $"拒绝的话里要说清它要的是正数（收到「{bad}」）。");
        }
    }

    /// <summary>造一份铸造请求（默认就是"合法的那一份"，逐条改坏由调用方做）。</summary>
    private static EraLinkMintRequest MintRequest(
        string? key = "host-url-key",
        string? package = "lab",
        string? era = "2",
        string? expiresHours = null,
        string baseUrl = "http://127.0.0.1:5273")
        => new(key, package, era, expiresHours, baseUrl);

    /// <summary>用例里的内容包目录：<c>lab</c> 有 7 层、<c>cafe</c> 一层都没有、<c>neko</c> 只有 1 层。</summary>
    private static readonly string[] MintPackIds = ["lab", "cafe", "neko"];

    /// <summary>假的「最多能跳到第几层」（真值在内容包里，测试不该把它抄第二份）。</summary>
    private static int? MintMaxEra(string id) => id switch
    {
        "lab" => 7,
        "cafe" => 0,
        "neko" => 1,
        _ => null,
    };

    // ------------------------------------------------------------------ 宿主那一层

    /// <summary>
    /// 宿主往返：A 生成链接 → B 用同一个密码导入，<b>走的是既有的九道闸</b>。<para>
    /// 断言落在两件与秒产量无关的东西上（点击总数与建筑持有数）：引擎线程从构造那一刻就在跑，
    /// 拿钱做断言只会得到"差不多"。
    /// </para>
    /// </summary>
    [Test]
    public static void WebHost_ShareLinkImportsThroughTheSameGate()
    {
        string rootA = NewTempDir();
        string rootB = NewTempDir();
        try
        {
            using var a = new WebHostScope(rootA);
            using var b = new WebHostScope(rootB);
            string buildingId = SetDeterministicState(a);

            CommandOutcome made = a.CreateShare(GoodPassword);
            Check.True(made.Ok, $"生成分享链接必须成功：{made.Message}");
            Check.NotNull(made.Text, "链接文本必须走 Text 字段回来（与导出的约定一致）。");
            Check.False(made.Text!.Contains(GoodPassword, StringComparison.Ordinal), "宿主回给前端的链接里不许出现密码。");
            Check.Contains(made.Message, "密码不在链接里", "那句话必须说出来——否则使用者只能猜。");

            CommandOutcome imported = b.ImportShare(made.Text!, GoodPassword);
            Check.True(imported.Ok, $"同一个密码必须导入成功：{imported.Message}");
            Check.Contains(imported.Message, "已导入", "成功那句话要来自既有的导入路径（引擎自己的文案）。");

            JsonObject view = b.View();
            Check.Equal(777.0, view["totalClicks"]!.GetValue<double>(), "导入之后立刻取快照就是导入后的状态。");
            Check.Equal(9, OwnedOf(view, buildingId), "建筑持有数也要换成导入的那一份。");
        }
        finally
        {
            Cleanup(rootA);
            Cleanup(rootB);
        }
    }

    /// <summary>
    /// <b>密码错时磁盘与内存一个字节都不动</b>——这是需求里"绝不损坏能用的存档"在分享链接这条路上的形态。<para>
    /// 判据刻意比"再读一次还能读出来"强：那份存档在用例前后被<b>逐字节</b>比对，
    /// 于是连"被换成另一份同样能读的存档"也拦得住（与 <see cref="SaveTransferHostTests"/> 同一口径）。
    /// </para>
    /// </summary>
    [Test]
    public static void WebHost_WrongSharePasswordTouchesNothing()
    {
        string root = NewTempDir();
        try
        {
            using var host = new WebHostScope(root);
            SetDeterministicState(host);

            Check.True(host.Save().Ok, "前置条件：先存一次盘，才有一份「能用的存档」可被弄坏。");
            string savePath = Path.Combine(root, "neko.json");
            byte[] before = File.ReadAllBytes(savePath);

            CommandOutcome made = host.CreateShare(GoodPassword);
            Check.True(made.Ok, $"前置条件：链接要先生成出来（{made.Message}）。");

            double clicksBefore = host.EngineTotalClicks();

            CommandOutcome denied = host.ImportShare(made.Text!, "wrong-password");
            Check.False(denied.Ok, "密码不对必须被拒绝。");
            Check.Contains(denied.Message, "密码不对", $"拒绝的理由要指向密码（{denied.Message}）。");
            Check.True(
                File.ReadAllBytes(savePath).AsSpan().SequenceEqual(before),
                "被拒绝的导入不许动磁盘上那份能用的存档。");
            Check.Equal(clicksBefore, host.EngineTotalClicks(), "被拒绝的导入也不许动内存里的会话。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    /// <summary>
    /// 调试跳层过的会话：<b>既不产链接、也不收链接</b>（与 <c>ExportAsync</c>/<c>ImportAsync</c> 同一条规矩）。<para>
    /// 这一条防的是"跳层留下的层号被一段链接发出去"：链接是耐久且会离开这台机器的东西，
    /// 而跳层只在内存里、退出即弃。
    /// </para>
    /// </summary>
    [Test]
    public static void WebHost_DebugSessionRefusesToShareOrReceive()
    {
        string root = NewTempDir();
        try
        {
            // 用有分层转生的包：没有层就跳不了，这条用例会变成"什么都没验"。
            using var host = new WebHostScope(root, id: "lab", content: TestGame.Lab);
            int max = host.MaxEra();
            Check.AtLeast(max, 1, "前提：这个包必须真的有层（不然这条用例什么都没验）。");

            DebugEraJump jump = host.Jump(Math.Min(2, max));
            Check.True(jump.Applied, $"前提：跳层要成功（{jump.Message}）。");

            CommandOutcome exported = host.Export();
            Check.False(exported.Ok, "前提：调试会话本来就不导出。");

            CommandOutcome made = host.CreateShare(GoodPassword);
            Check.False(made.Ok, "调试会话不许生成分享链接。");
            Check.Contains(made.Message, "调试模式", $"理由要说是调试模式（{made.Message}）。");

            // 一段**合规**的链接：拒绝它的理由只能是调试模式，不能是"链接坏了"。
            string token = ShareLinkCodec.Protect(GoodPassword, ShareLinkPurpose.SaveShare, "lab", SyntheticExportText(400));
            CommandOutcome denied = host.ImportShare(token, GoodPassword);
            Check.False(denied.Ok, "调试会话不许导入。");
            Check.Contains(denied.Message, "调试模式", $"理由要说是调试模式（{denied.Message}）。");
        }
        finally
        {
            Cleanup(root);
        }
    }

    // ------------------------------------------------------------------ 手写的第二份线格式

    /// <summary>
    /// 手工造一段链接（<b>刻意不复用 <c>ShareLinkCodec.Protect</c></b>）。<para>
    /// 只有它能造出产出端拒绝产出的东西：迭代数为 1 的头、超上限的载荷、被改过的字节。
    /// 它同时也是线格式的第二份实现——格式一旦漂移，这里和 <c>ShareLink.cs</c> 会有一边先红。
    /// </para>
    /// </summary>
    private static string CraftLink(
        string payloadJson,
        string password,
        int iterations = ShareLinkCodec.DefaultIterations,
        byte kind = (byte)'s',
        byte version = 0)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);

        byte[] gzipped;
        using (var output = new MemoryStream())
        {
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(Encoding.UTF8.GetBytes(payloadJson));
            }

            gzipped = output.ToArray();
        }

        byte[] head = new byte[37];
        head[0] = (byte)'N';
        head[1] = (byte)'K';
        head[2] = (byte)'L';
        head[3] = version == 0 ? ShareLinkCodec.WireVersion : version;
        head[4] = kind;
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(5, 4), (uint)iterations);
        salt.CopyTo(head, 9);
        nonce.CopyTo(head, 25);

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        byte[] cipher = new byte[gzipped.Length];
        byte[] tag = new byte[16];

        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, gzipped, cipher, tag, head);
        }

        byte[] all = [.. head, .. cipher, .. tag];
        return Convert.ToBase64String(all).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>改链接里第 <paramref name="index"/> 个字节（按 base64url 解出来之后的偏移）。</summary>
    private static string Mutate(string link, int index, byte value)
    {
        string padded = link.Replace('-', '+').Replace('_', '/');
        byte[] raw = Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
        raw[index] = value;
        return Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>一份合法的载荷 JSON（<c>Save</c> 里放一段指定长度的文本）。</summary>
    private static string PayloadJson(string saveText) => new JsonObject
    {
        ["Format"] = ShareLinkCodec.PayloadFormatTag,
        ["FormatVersion"] = ShareLinkCodec.PayloadFormatVersion,
        ["Kind"] = "save",
        ["PackId"] = "neko",
        ["Save"] = saveText,
    }.ToJsonString();

    /// <summary>
    /// 一段长度可控、<b>形状与压缩率都像真存档</b>的合成导出文本。<para>
    /// id 由自己写的 LCG 生成（不依赖 <see cref="Random"/> 的实现细节，因此每次跑都一样），
    /// 数值各不相同——<b>刻意不用"重复同一段"</b>：重复内容 gzip 之后只剩几百分之一，
    /// 那样量出来的是压缩器的上限，而不是真实存档的长度。
    /// </para>
    /// <para>
    /// 用它而不是读真实的 <c>saves/</c>：真实存档是真人正在玩的进度，会被宿主每 60 秒重写，
    /// 拿它当夹具等于让守卫的结果取决于别人在不在玩（真存档的实测走一次性探针，见 SHARE_LINK_PLAN §2）。
    /// </para>
    /// </summary>
    private static string SyntheticExportText(int length)
    {
        var builder = new StringBuilder(length + 64);
        builder.Append("{\"Format\":\"neko-save\",\"FormatVersion\":1,\"SaveVersion\":1,\"Save\":\"");
        uint state = 0x1234_5678;

        while (builder.Length < length - 2)
        {
            state = (state * 1664525u) + 1013904223u;
            builder.Append("{\"id\":\"b_");
            builder.Append(state.ToString("x8"));
            builder.Append("\",\"owned\":true,\"count\":");
            builder.Append(state % 97_777);
            builder.Append("},");
        }

        builder.Append("\"}");
        return builder.ToString(0, length);
    }

    /// <summary>
    /// 一段<b>压不动</b>的文本（十六进制伪随机）：它代表"存档大到装不进链接"的物理下限——
    /// 有它才验得到产出端那道长度闸（不然压缩率一高，40 KB 的载荷也照样塞得下）。
    /// </summary>
    private static string IncompressibleText(int length)
    {
        var builder = new StringBuilder(length + 8);
        uint state = 0xDEAD_BEEF;

        while (builder.Length < length)
        {
            state = (state * 1103515245u) + 12345u;
            builder.Append(state.ToString("x8"));
        }

        return builder.ToString(0, length);
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>一台跑着的 Web 会话 + 它的临时存档根（与 <c>SaveTransferHostTests</c> 同形）。</summary>
    private sealed class WebHostScope : IDisposable
    {
        private readonly GameHost _host;

        public WebHostScope(string root, string id = "neko", GameContent? content = null)
        {
            // latencyLogPath: null —— 这些用例一个字节都不该写进 artifacts/latency.txt（真人数据）。
            var package = new WebPackage(id, id, () => content ?? TestGame.NekoContent, "已载入。");
            _host = new GameHost(package, root, seed: 7, latencyLogPath: null);
        }

        public CommandOutcome Export() => _host.ExportAsync().GetAwaiter().GetResult();

        public CommandOutcome Import(string text) => _host.ImportAsync(text).GetAwaiter().GetResult();

        public CommandOutcome Save() => _host.SaveAsync().GetAwaiter().GetResult();

        public CommandOutcome CreateShare(string password)
            => _host.CreateShareLinkAsync(password).GetAwaiter().GetResult();

        public CommandOutcome ImportShare(string token, string password)
            => _host.ImportShareAsync(token, password).GetAwaiter().GetResult();

        public DebugEraJump Jump(int era) => _host.JumpToEraAsync(era.ToString()).GetAwaiter().GetResult();

        public JsonObject View() => _host.ViewSnapshot();

        /// <summary>这个包有几层（决定跳层用例往哪跳）。</summary>
        public int MaxEra()
        {
            int max = -1;
            Edit(engine => max = engine.Content.MaxEraIndex);
            return max;
        }

        /// <summary>在游戏线程上改一次状态（引擎只有一个主人，改状态也要走那条线程）。</summary>
        public void Edit(Action<GameEngine> action)
            => _host.ExecuteAsync(engine =>
            {
                action(engine);
                return new CommandOutcome(true, string.Empty, 0);
            }).GetAwaiter().GetResult();

        /// <summary>读引擎里的计数器（不是快照：快照是 250ms 一帧，会慢半拍）。</summary>
        public double EngineTotalClicks()
        {
            double clicks = -1;
            Edit(engine => clicks = engine.State.TotalClicks);
            return clicks;
        }

        public void Dispose() => _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>灌一份与秒产量无关的确定状态（<c>TotalClicks</c> 与建筑持有数）。</summary>
    private static string SetDeterministicState(WebHostScope host)
    {
        string buildingId = string.Empty;

        host.Edit(engine =>
        {
            buildingId = engine.Content.BuildingById.Keys.First();
            engine.State.TotalClicks = 777;
            engine.State.Cookies = 1_000_000;
            engine.State.BuildingCounts[buildingId] = 9;
            engine.MarkDirty();
        });

        Check.Greater(buildingId.Length, 0, "前置条件：这个包至少有一座建筑。");
        return buildingId;
    }

    private static int OwnedOf(JsonObject view, string buildingId)
    {
        foreach (JsonNode? row in view["buildings"]!.AsArray())
        {
            if (row?["id"]?.GetValue<string>() == buildingId) return row["owned"]!.GetValue<int>();
        }

        throw new AssertionException($"快照里没有建筑「{buildingId}」这一行。");
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "neko-share-link-tests", Guid.NewGuid().ToString("N"));
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
}
