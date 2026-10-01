using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 抽取保真度：<c>tools/extract-lore-text.ps1</c> 从 <c>Lore.cs</c> 抽出来的散文，
/// 必须与源码里的字面量<b>逐字相同</b>。<para>
/// <b>为什么要有这条</b>：把 40 条中文散文搬到 JSON，是"机器搬运 + 人相信它没搬错"
/// 这件事。这个仓库已经在中文批量编辑上出过一次事故（脚本字面量被错误解码、
/// 把 ROADMAP 写坏 298 行），所以"相信"不够——要比对。
/// </para>
/// <para>
/// 而且比对要<b>双向</b>：源码里每条都要在 JSON 里有对应项（漏抽），
/// JSON 里每条也都要在源码里有对应项（多抽/抄重）。单看一边会漏掉另一半。
/// </para>
/// <para>
/// 这条用例在"代码改成从 JSON 取文本"之后会失去意义（那时源码里已经没有字面量了），
/// 届时它会因为抽不到东西而<b>明确失败</b>（见下面的断言），提醒把它换成运行期守卫——
/// 而不是悄悄变成一条永远绿的橡皮图章。
/// </para>
/// </summary>
public static class LoreTextFixtureTests
{
    [Test]
    public static void LabTextJson_MatchesTheCSharpLiteralsExactly()
    {
        string root = RepositoryRoot();
        string source = Path.Combine(root, "engine", "content", "Lab", "Lore.cs");
        string json = Path.Combine(root, "engine", "content", "Lab", "text.json");

        Check.True(File.Exists(source), $"找不到 {source}");
        Check.True(File.Exists(json), $"找不到 {json}");

        string code = File.ReadAllText(source);
        JsonNode? node = JsonNode.Parse(File.ReadAllText(json));
        Check.NotNull(node, "text.json 解析失败。");

        JsonObject lore = node!["lore"] as JsonObject ?? [];
        JsonObject storylines = node["storylines"] as JsonObject ?? [];

        // ---- 条目：与工厂方法的调用点逐一比对 ----
        MatchCollection calls = Regex.Matches(
            code,
            "(?:Popup|Log|Codex)\\(\"(?<id>[^\"]+)\"\\s*,\\s*(?<order>\\d+)\\s*,\\s*\"(?<title>[^\"]*)\"\\s*,\\s*\"(?<body>[^\"]*)\"");

        Check.AtLeast(calls.Count, 30, "源码里抽到的条目太少——调用点的写法可能变了，这条守卫会因此失去判别力。");
        Check.Equal(calls.Count, lore.Count, "JSON 的条数与源码调用点数量不一致。");

        foreach (Match call in calls)
        {
            string id = call.Groups["id"].Value;
            Check.True(lore.ContainsKey(id), $"JSON 里漏了源码中的条目：{id}");

            JsonObject entry = lore[id] as JsonObject ?? [];
            Check.Equal(call.Groups["title"].Value, (string?)entry["title"], $"「{id}」的标题与源码不一致。");
            Check.Equal(call.Groups["body"].Value, (string?)entry["body"], $"「{id}」的正文与源码不一致。");
        }

        // ---- 反方向：JSON 里不许有源码里没有的条目 ----
        foreach ((string id, JsonNode? _) in lore)
            Check.True(
                Regex.IsMatch(code, $"(?:Popup|Log|Codex)\\(\"{Regex.Escape(id)}\""),
                $"JSON 里的「{id}」在源码里找不到对应的调用点（多抽或 id 抄错）。");

        // ---- 剧情线 ----
        MatchCollection lines = Regex.Matches(
            code,
            "Id\\s*=\\s*\"(?<id>[^\"]*)\"\\s*,\\s*Name\\s*=\\s*\"(?<name>[^\"]*)\"\\s*,\\s*Theme\\s*=\\s*\"(?<theme>[^\"]*)\"\\s*,\\s*Icon\\s*=\\s*\"(?<icon>[^\"]*)\"");

        Check.Equal(lines.Count, storylines.Count, "剧情线的条数与源码不一致。");

        foreach (Match line in lines)
        {
            string id = line.Groups["id"].Value;
            JsonObject entry = storylines[id] as JsonObject
                ?? throw new AssertionException($"JSON 里漏了剧情线：{id}");

            Check.Equal(line.Groups["name"].Value, (string?)entry["name"], $"剧情线「{id}」的名称不一致。");
            Check.Equal(line.Groups["theme"].Value, (string?)entry["theme"], $"剧情线「{id}」的主题不一致。");
            Check.Equal(line.Groups["icon"].Value, (string?)entry["icon"], $"剧情线「{id}」的图标不一致。");
        }
    }

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
}
