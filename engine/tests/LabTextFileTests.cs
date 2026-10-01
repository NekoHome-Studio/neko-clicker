using System.Security.Cryptography;
using System.Text.Json.Nodes;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 剧情外部化的运行期守卫（试点包：Lab）。<para>
/// 与 <see cref="ContentTextTests"/> 的分工：那边喂假文件，守加载器本身的每条分支；
/// 这边拿<b>真的包、真的 <c>text.json</c></b> 守装配——文件有没有随包复制到输出目录、
/// 40 条是不是真的从文件里读出来、把文件改坏之后会不会当场炸。
/// </para>
/// <para>
/// <b>它替换掉了原来的"逐字比对"用例</b>（<c>LoreTextFixtureTests</c>）。那条用例比的是
/// <c>Lore.cs</c> 里的字面量与 JSON：搬完家之后源码里已经没有字面量可比，它会因"抽到 0 条"
/// 而明确失败——这正是它设计时的自毁开关。它的使命（证明机器搬运没抄错）在迁移那一步
/// 已经完成；留一条永远绿的橡皮图章比删掉它更糟，所以换成下面三条。
/// </para>
/// </summary>
public static class LabTextFileTests
{
    /// <summary>
    /// 文件必须真的到了输出目录，而且与仓库里那份逐字节相同。<para>
    /// 两个失败都会让剧情悄悄变成另一份：csproj 的复制规则失效（丢文件 → 启动即抛，
    /// 还算响），以及"产物是旧的"（沿用上一次的文本，不抛、也没人发现）。
    /// </para>
    /// </summary>
    [Test]
    public static void TextFile_IsCopiedToTheOutput_AndIsTheOneFromTheRepo()
    {
        string shipped = ShippedPath();
        Check.True(
            File.Exists(shipped),
            $"text.json 没有复制到输出目录：{shipped}。" +
            "内容包项目的 csproj 里要有一条 CopyToOutputDirectory（Lab 用 <None Include=\"text.json\"> + Link）。");

        string source = Path.Combine(RepositoryRoot(), "engine", "content", "Lab", "text.json");
        Check.True(File.Exists(source), $"仓库里找不到 {source}。");

        Check.Equal(
            Hash(source),
            Hash(shipped),
            "输出目录里的 text.json 与仓库里的不是同一份——复制规则失效，或者这个产物是旧的。");
    }

    /// <summary>
    /// 代码里的每一条都与文件里对应条目的散文<b>逐字相同</b>，且没有空标题 / 空正文。<para>
    /// 这是"文本确实是从文件读的"的正面证据：如果哪天有人把字面量塞回代码里，
    /// 这条会立刻红——因为文件改了、代码没跟着改。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryEntryAndStoryline_ResolvesItsProseFromTheFile()
    {
        JsonObject file = ShippedJson();
        GameContent content = TestGame.Lab;

        JsonObject lore = (JsonObject)file["lore"]!;
        Check.Equal(40, lore.Count, "text.json 的 lore 条数不是 40——条数变了，迁移脚本与验收口径都要跟着改。");
        Check.Equal(lore.Count, content.LoreEntries.Count, "代码里的条目数与文件里的对不上。");

        foreach (LoreEntry entry in content.LoreEntries)
        {
            Check.True(lore.ContainsKey(entry.Id), $"text.json 里没有「{entry.Id}」。");

            JsonObject row = (JsonObject)lore[entry.Id]!;
            Check.Equal((string?)row["title"], entry.Title, $"「{entry.Id}」的标题不是从文件里读出来的。");
            Check.Equal((string?)row["body"], entry.Body, $"「{entry.Id}」的正文不是从文件里读出来的。");
            Check.False(string.IsNullOrWhiteSpace(entry.Title), $"「{entry.Id}」的标题是空的。");
            Check.False(string.IsNullOrWhiteSpace(entry.Body), $"「{entry.Id}」的正文是空的。");
        }

        JsonObject lines = (JsonObject)file["storylines"]!;
        foreach (StorylineDefinition line in content.Storylines)
        {
            Check.True(lines.ContainsKey(line.Id), $"text.json 的 storylines 里没有「{line.Id}」。");

            JsonObject row = (JsonObject)lines[line.Id]!;
            Check.Equal((string?)row["name"], line.Name, $"剧情线「{line.Id}」的名称不是从文件里读出来的。");
            Check.Equal((string?)row["theme"], line.Theme, $"剧情线「{line.Id}」的主题不是从文件里读出来的。");
            Check.Equal((string?)row["icon"], line.Icon, $"剧情线「{line.Id}」的图标不是从文件里读出来的。");
        }
    }

    /// <summary>
    /// 把真文件改坏的三种形态喂给真实的 id 表，确认它们都会响。<para>
    /// 顺序很重要：先证明"原样读一遍一条都不抛"（反面对照），否则下面两条"抛了"可能
    /// 只是碰巧——一条永远会红的守卫与一条正确的守卫，看起来同样不可靠。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheFileWrongly_FailsLoudly()
    {
        JsonObject real = ShippedJson();
        GameContent content = TestGame.Lab;

        // 基准：原样读一遍，一条都不该抛。
        using (var baseline = new Fixture(real.ToJsonString()))
            ReadAll(baseline.Load(), content);

        string victim = content.LoreEntries[0].Id;

        // ① 少一条：读它的那一刻抛，且点名是哪一条。
        JsonObject missing = (JsonObject)real.DeepClone();
        ((JsonObject)missing["lore"]!).Remove(victim);

        using (var bad = new Fixture(missing.ToJsonString()))
        {
            ContentText text = bad.Load();

            foreach (LoreEntry entry in content.LoreEntries)
            {
                if (entry.Id == victim) continue;
                text.Text("lore", entry.Id, "title");
                text.Text("lore", entry.Id, "body");
            }

            ReadStorylines(text, content);

            // 剩下的都被读过了，孤儿检查不该红——把"缺条目"与"多条目"两种错区分开。
            text.EnsureNoOrphans();

            InvalidOperationException ex = Check.Throws<InvalidOperationException>(
                () => text.Text("lore", victim, "body"));
            Check.Contains(ex.Message, victim);
        }

        // ② 多一条：孤儿检查点名把它报出来，而不是让它静静躺在文件里。
        JsonObject extra = (JsonObject)real.DeepClone();
        ((JsonObject)extra["lore"]!)["zz_orphan"] = new JsonObject
        {
            ["title"] = "无主标题",
            ["body"] = "无主正文",
        };

        using (var bad = new Fixture(extra.ToJsonString()))
        {
            ContentText text = bad.Load();
            ReadAll(text, content);

            InvalidOperationException ex =
                Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
            Check.Contains(ex.Message, "zz_orphan");
        }
    }

    // ---------------------------------------------------------------- 辅助

    private static string ShippedPath() => Path.Combine(AppContext.BaseDirectory, "content", "Lab", "text.json");

    private static JsonObject ShippedJson() => (JsonObject)JsonNode.Parse(File.ReadAllText(ShippedPath()))!;

    /// <summary>按代码用到的 id 表把一份文本读一遍——即"包在启动时会走的那条路"。</summary>
    private static void ReadAll(ContentText text, GameContent content)
    {
        ReadStorylines(text, content);

        foreach (LoreEntry entry in content.LoreEntries)
        {
            text.Text("lore", entry.Id, "title");
            text.Text("lore", entry.Id, "body");
        }
    }

    private static void ReadStorylines(ContentText text, GameContent content)
    {
        foreach (StorylineDefinition line in content.Storylines)
        {
            text.Text("storylines", line.Id, "name");
            text.Text("storylines", line.Id, "theme");
            text.Text("storylines", line.Id, "icon");
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>从程序集所在目录往上找 <c>NekoClicker.sln</c>。</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NekoClicker.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new AssertionException($"从 {AppContext.BaseDirectory} 往上找不到 NekoClicker.sln。");
    }

    /// <summary>临时的 <c>content/Lab/text.json</c> 夹具：把改坏的文件喂给真实的加载路径。</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _root;

        public Fixture(string json)
        {
            _root = Path.Combine(AppContext.BaseDirectory, "lab-text-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "content", "Lab"));
            File.WriteAllText(Path.Combine(_root, "content", "Lab", "text.json"), json);
        }

        public ContentText Load() => ContentText.Load("Lab", _root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* 清理失败不影响断言 */ }
        }
    }
}
