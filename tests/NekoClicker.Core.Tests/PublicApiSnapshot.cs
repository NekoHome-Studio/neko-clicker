using System.Reflection;
using System.Text;
using NekoClicker.Core;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 读取"提交在仓库里的那份公开 API 快照"。<para>
/// 快照被嵌进 <c>NekoClicker.Core.dll</c>（见 Core 的 csproj），所以它随程序集一起走：
/// 任何拿到这个 dll 的宿主都能自己断言"这份二进制的公开 API 与我预期的一致"，
/// 而不需要把本仓库的测试代码也带走。
/// </para>
/// </summary>
public static class PublicApiSnapshot
{
    /// <summary>嵌入资源的逻辑名（在 Core 的 csproj 里显式指定）。</summary>
    public const string ResourceName = "NekoClicker.Core.PublicApi.txt";

    /// <summary>快照所在的源文件路径，用于在报错信息里告诉人该改哪个文件。</summary>
    public const string SourcePath = @"src\NekoClicker.Core\PublicApi.txt";

    /// <summary>读出快照全文。读不到时抛 <see cref="AssertionException"/>（而不是返回空串）。</summary>
    public static string ReadText()
    {
        Assembly core = typeof(GameEngine).Assembly;

        using Stream? stream = core.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            string[] available = [.. core.GetManifestResourceNames()];
            throw new AssertionException(
                $"读不到嵌入资源 <{ResourceName}>。程序集里现有：{string.Join("、", available)}" +
                "（检查 src/NekoClicker.Core/NekoClicker.Core.csproj 的 EmbeddedResource 是否还在）。");
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>读出快照并拆成行。</summary>
    public static string[] ReadLines() => PublicApiSurface.Parse(ReadText());
}
