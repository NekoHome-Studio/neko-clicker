using System.Security.Cryptography;
using System.Text.Json.Nodes;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 剧情外部化的运行期守卫（覆盖<b>全部</b>已外部化的内容包）。<para>
/// 与 <see cref="ContentTextTests"/> 的分工：那边喂假文件，守加载器本身的每条分支；
/// 这边拿<b>真的包、真的 <c>text.json</c></b> 守装配——文件有没有随包复制到输出目录、
/// 每条散文是不是真的从文件里读出来、把文件改坏之后会不会当场炸。
/// </para>
/// <para>
/// <b>从"只覆盖 Lab"泛化过来</b>：九个包迁移完之后，守卫不能再是"每个包各写一份"，
/// 否则新增一个包时"忘了加守卫"是沉默的。所以这里用一张表 + 一条覆盖度用例：
/// <c>engine/content/*/text.json</c> 有多少个，表里就必须有多少个，一个不多一个不少。
/// </para>
/// </summary>
public static class ContentTextFileTests
{
    /// <summary>已经外部化的包：目录名 → 构建入口。目录名就是 <c>ContentText.Load</c> 的参数。</summary>
    private static readonly (string Pack, Func<GameContent> Content)[] Externalized =
    [
        ("Cafe", () => TestGame.CafeContent),
        ("NineLives", () => TestGame.NineLives),
        ("Lab", () => TestGame.Lab),
        ("Company", () => TestGame.Company),
        ("Apocalypse", () => TestGame.Apocalypse),
        ("Library", () => TestGame.Library),
        ("God", () => TestGame.God),
        ("Civ", () => TestGame.Civ),
        ("Cyber", () => TestGame.Cyber),
        ("Dream", () => TestGame.Dream),
    ];

    /// <summary>
    /// 覆盖度：仓库里有几个 <c>text.json</c>，这张表就得有几个包。<para>
    /// 这条守的是"守卫自己瞎掉"——新增一个包只加了文件没加守卫时，它会红。
    /// </para>
    /// </summary>
    [Test]
    public static void Table_CoversExactlyThePacksThatHaveATextFile()
    {
        string[] withFile = [.. Directory
            .GetDirectories(Path.Combine(RepositoryRoot(), "engine", "content"))
            .Where(dir => File.Exists(Path.Combine(dir, "text.json")))
            .Select(dir => Path.GetFileName(dir)!)
            .OrderBy(name => name, StringComparer.Ordinal)];

        string[] inTable = [.. Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)];

        Check.Equal(
            string.Join("、", withFile),
            string.Join("、", inTable),
            "有 text.json 的包与守卫表里的包不一致——加了包就要在这张表里加一行，删了包同理。");
    }

    /// <summary>
    /// 每个包的文件都必须真的到了输出目录，而且与仓库里那份逐字节相同。<para>
    /// 两种失败都会让剧情悄悄变成另一份：复制规则失效（启动即抛，还算响），
    /// 以及"产物是旧的"（沿用上一版文本，不抛、也没人发现）。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryTextFile_IsCopiedToTheOutput_AndMatchesTheRepo()
    {
        foreach ((string pack, Func<GameContent> _) in Externalized)
        {
            string shipped = ShippedPath(pack);
            Check.True(
                File.Exists(shipped),
                $"content/{pack}/text.json 没有复制到输出目录：{shipped}。"
                + "内容包目录里有 text.json 时，仓库根的 Directory.Build.props 会自动把它复制成 content/<包名>/text.json。");

            string source = RepoPath(pack);
            Check.True(File.Exists(source), $"仓库里找不到 {source}。");

            Check.Equal(
                Hash(source),
                Hash(shipped),
                $"{pack}: 输出目录里的 text.json 与仓库里的不是同一份——复制规则失效，或者产物是旧的。");
        }
    }

    /// <summary>
    /// 代码里的每一条都与文件里对应条目的散文<b>逐字相同</b>，且没有空标题 / 空正文。<para>
    /// 这是"文本确实是从文件读的"的正面证据：若哪天有人把字面量塞回代码里，
    /// 文件改了、代码没跟着改，这条就会红。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryEntryAndStoryline_ResolvesItsProseFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();

            JsonObject lore = (JsonObject)file["lore"]!;
            Check.Equal(
                lore.Count,
                content.LoreEntries.Count,
                $"{pack}: 代码里的条目数与文件里的对不上（应当被孤儿检查挡住）。");

            foreach (LoreEntry entry in content.LoreEntries)
            {
                Check.True(lore.ContainsKey(entry.Id), $"{pack}: text.json 里没有「{entry.Id}」。");

                JsonObject row = (JsonObject)lore[entry.Id]!;
                Check.Equal((string?)row["title"], entry.Title, $"{pack}:「{entry.Id}」的标题不是从文件里读出来的。");
                Check.Equal((string?)row["body"], entry.Body, $"{pack}:「{entry.Id}」的正文不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(entry.Title), $"{pack}:「{entry.Id}」的标题是空的。");
                Check.False(string.IsNullOrWhiteSpace(entry.Body), $"{pack}:「{entry.Id}」的正文是空的。");
            }

            JsonObject lines = (JsonObject)file["storylines"]!;
            foreach (StorylineDefinition line in content.Storylines)
            {
                Check.True(lines.ContainsKey(line.Id), $"{pack}: text.json 的 storylines 里没有「{line.Id}」。");

                JsonObject row = (JsonObject)lines[line.Id]!;
                Check.Equal((string?)row["name"], line.Name, $"{pack}: 剧情线「{line.Id}」的名称不是从文件里读出来的。");
                Check.Equal((string?)row["theme"], line.Theme, $"{pack}: 剧情线「{line.Id}」的主题不是从文件里读出来的。");
                Check.Equal((string?)row["icon"], line.Icon, $"{pack}: 剧情线「{line.Id}」的图标不是从文件里读出来的。");
            }
        }
    }

    /// <summary>
    /// 把真文件改坏的三种形态喂给每个包真实的 id 表，确认它们都会响。<para>
    /// 顺序很重要：先证明"原样读一遍一条都不抛"（反面对照），否则下面两条"抛了"可能只是碰巧。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheFileWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject real = ShippedJson(pack);
            GameContent content = build();

            // 基准：原样读一遍，一条都不该抛。
            using (var baseline = new Fixture(pack, real.ToJsonString()))
                ReadAll(baseline.Load(), content);

            string victim = content.LoreEntries[0].Id;

            // ① 少一条：读它的那一刻抛，且点名是哪一条。
            JsonObject missing = (JsonObject)real.DeepClone();
            ((JsonObject)missing["lore"]!).Remove(victim);

            using (var bad = new Fixture(pack, missing.ToJsonString()))
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

            using (var bad = new Fixture(pack, extra.ToJsonString()))
            {
                ContentText text = bad.Load();
                ReadAll(text, content);

                InvalidOperationException ex =
                    Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
                Check.Contains(ex.Message, "zz_orphan");
            }
        }
    }

    // ---------------------------------------------------------------- 辅助

    private static string ShippedPath(string pack) =>
        Path.Combine(AppContext.BaseDirectory, "content", pack, "text.json");

    private static string RepoPath(string pack) =>
        Path.Combine(RepositoryRoot(), "engine", "content", pack, "text.json");

    private static JsonObject ShippedJson(string pack) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(ShippedPath(pack)))!;

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

    /// <summary>临时的 <c>content/&lt;包名&gt;/text.json</c> 夹具：把改坏的文件喂给真实的加载路径。</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _root;
        private readonly string _pack;

        public Fixture(string pack, string json)
        {
            _pack = pack;
            _root = Path.Combine(AppContext.BaseDirectory, "text-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "content", pack));
            File.WriteAllText(Path.Combine(_root, "content", pack, "text.json"), json);
        }

        public ContentText Load() => ContentText.Load(_pack, _root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* 清理失败不影响断言 */ }
        }
    }
}
