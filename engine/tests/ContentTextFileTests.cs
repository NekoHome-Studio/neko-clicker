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
/// <para>
/// <b>建筑文案也在这张表里</b>：<c>BuildingDefinition.Name</c>/<c>Description</c>/<c>Icon</c>
/// 与剧情散文同住一份 <c>text.json</c>（<c>buildings</c> 分区），所以守卫是同一组。
/// 示例包「猫咖物语」没有图鉴，只有 <c>buildings</c> 一个分区——这也正是"守卫不能假设
/// 每个包都有 lore"的原因。
/// </para>
/// <para>
/// <b>纪元文案同样在这张表里</b>：<c>EraDefinition</c> 的六个面向玩家的字符串
/// （<c>Name</c> / <c>Theme</c> / <c>Icon</c> / <c>EntryText</c> / <c>ExitText</c> /
/// <c>CompletionHint</c>）住在 <c>eras</c> 分区。<b>十一个包里只有九个层纪元</b>
/// （<c>Neko</c> 与 <c>Cafe</c> 连 <c>Eras.cs</c> 都没有），所以这张表里那两个包的期望值是
/// <b>0</b>，而不是"没有这一行"——"没有这一行"是沉默的，"期望 0"是响的。
/// </para>
/// </summary>
public static class ContentTextFileTests
{
    /// <summary>已经外部化的包：目录名 → 构建入口。目录名就是 <c>ContentText.Load</c> 的参数。</summary>
    private static readonly (string Pack, Func<GameContent> Content)[] Externalized =
    [
        ("Neko", () => TestGame.NekoContent),
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
    /// 每个包应当有几座建筑。<para>
    /// 这张表与"文件条数 == 代码条数"守的是<b>两件不同的事</b>：后者只能发现两边不一致，
    /// 两边同时少一座（删建筑时顺手把 JSON 里的也删了）它不会响；而这里是写死的期望值，
    /// 少一座就红——曲线回归只关心价格与产量，少一座建筑它也不一定看得见。
    /// </para>
    /// </summary>
    private static readonly (string Pack, int Count)[] ExpectedBuildings =
    [
        ("Apocalypse", 9),
        ("Cafe", 10),
        ("Civ", 9),
        ("Company", 9),
        ("Cyber", 9),
        ("Dream", 9),
        ("God", 9),
        ("Lab", 9),
        ("Library", 9),
        ("Neko", 10),
        ("NineLives", 12),
    ];

    /// <summary>
    /// 每个包应当有几层纪元。<para>
    /// 与 <see cref="ExpectedBuildings"/> 同一条理由：它管的是"代码与文件<b>同时</b>少一层"——
    /// 两边一致时双向比对看不出任何异常，而纪元少一层意味着整段叙事与一道门槛凭空消失。
    /// </para>
    /// <para>
    /// <b>十一个包全在这张表里，没有纪元的两个写 0</b>：把 <c>Neko</c> / <c>Cafe</c> 漏掉，
    /// 就等于"给它们加一层纪元"这件事没有守卫——而这两个包连 <c>Eras.cs</c> 都没有。
    /// </para>
    /// </summary>
    private static readonly (string Pack, int Count)[] ExpectedEras =
    [
        ("Apocalypse", 5),
        ("Cafe", 0),
        ("Civ", 5),
        ("Company", 3),
        ("Cyber", 5),
        ("Dream", 5),
        ("God", 5),
        ("Lab", 7),
        ("Library", 5),
        ("Neko", 0),
        ("NineLives", 9),
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
    /// 建筑条数表必须与守卫表一一对应：新增一个包时"忘了登记期望条数"是沉默的，
    /// 而条数表本身正是"两边同时少一座"那条守卫的依据。
    /// </summary>
    [Test]
    public static void BuildingCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedBuildings.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "建筑条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
    }

    /// <summary>
    /// 纪元条数表的对应性守卫——与 <see cref="BuildingCountTable_CoversExactlyTheGuardTable"/> 同构。<para>
    /// 同样要覆盖<b>全部十一个包</b>（含两个期望 0 的）：纪元的期望值表漏掉一个包，
    /// 就等于那个包"多了或少了整整一层"没人管。
    /// </para>
    /// </summary>
    [Test]
    public static void EraCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedEras.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "纪元条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
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

            // 不是每个包都有图鉴（示例包「猫咖物语」只有 buildings），所以分区缺失时
            // 要反过来要求"代码里也没有"——而不是让 (JsonObject)null 抛一个看不出所以然的异常。
            if (file["lore"] is JsonObject lore)
            {
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
            }
            else
            {
                Check.Equal(0, content.LoreEntries.Count, $"{pack}: text.json 里没有 lore 分区，代码里却有图鉴条目。");
            }

            if (file["storylines"] is JsonObject lines)
            {
                foreach (StorylineDefinition line in content.Storylines)
                {
                    Check.True(lines.ContainsKey(line.Id), $"{pack}: text.json 的 storylines 里没有「{line.Id}」。");

                    JsonObject row = (JsonObject)lines[line.Id]!;
                    Check.Equal((string?)row["name"], line.Name, $"{pack}: 剧情线「{line.Id}」的名称不是从文件里读出来的。");
                    Check.Equal((string?)row["theme"], line.Theme, $"{pack}: 剧情线「{line.Id}」的主题不是从文件里读出来的。");
                    Check.Equal((string?)row["icon"], line.Icon, $"{pack}: 剧情线「{line.Id}」的图标不是从文件里读出来的。");
                }
            }
            else
            {
                Check.Equal(0, content.Storylines.Count, $"{pack}: text.json 里没有 storylines 分区，代码里却有剧情线。");
            }
        }
    }

    /// <summary>
    /// 建筑的名字与说明都从文件的 <c>buildings</c> 分区读，条数与期望值一致。<para>
    /// <b>两个方向都查</b>：代码里每座建筑都要在文件里有对应条目（缺一条 = 启动即抛），
    /// 文件里也不许有代码不读的条目（那是孤儿，由 <see cref="ContentText.EnsureNoOrphans"/> 兜底）。
    /// 再叠一张<b>写死的条数表</b>：它管的是"两边同时少一座"——上面两条都发现不了。
    /// </para>
    /// <para>
    /// 这条还有一层作用：若哪天有人把字面量塞回 <c>Buildings.cs</c>，而文件改了、
    /// 代码没跟着改，这里的"代码值 == 文件值"就会红。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryBuilding_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedBuildings.Single(entry => entry.Pack == pack).Count;

            Check.True(file["buildings"] is JsonObject, $"{pack}: text.json 里没有 buildings 分区。");
            JsonObject buildings = (JsonObject)file["buildings"]!;

            Check.Equal(expected, content.Buildings.Count, $"{pack}: 代码里的建筑数与期望值对不上。");
            Check.Equal(expected, buildings.Count, $"{pack}: 文件里的建筑数与期望值对不上。");

            foreach (BuildingDefinition building in content.Buildings)
            {
                Check.True(
                    buildings.ContainsKey(building.Id),
                    $"{pack}: text.json 的 buildings 里没有「{building.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)buildings[building.Id]!;
                Check.Equal((string?)row["name"], building.Name, $"{pack}:「{building.Id}」的名字不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["description"],
                    building.Description,
                    $"{pack}:「{building.Id}」的说明不是从文件里读出来的。");
                Check.Equal((string?)row["icon"], building.Icon, $"{pack}:「{building.Id}」的图标不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(building.Name), $"{pack}:「{building.Id}」的名字是空的。");
                Check.False(string.IsNullOrWhiteSpace(building.Description), $"{pack}:「{building.Id}」的说明是空的。");
                Check.False(string.IsNullOrWhiteSpace(building.Icon), $"{pack}:「{building.Id}」的图标是空的。");
            }
        }
    }

    /// <summary>
    /// <c>EraDefinition</c> 的六个面向玩家的字符串都从文件的 <c>eras</c> 分区读，
    /// 条数与写死的期望值一致。<para>
    /// 与建筑那条同构：代码 ↔ 文件<b>两个方向</b>都查，再叠一张写死的条数表
    /// （它管的是"两边同时少一层"）。<b>没有纪元的包要求文件里也没有这个分区</b>——
    /// 反过来（文件有、代码不读）会由孤儿检查抓住，这里先把"根本不该有"说清楚。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryEra_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedEras.Single(entry => entry.Pack == pack).Count;

            Check.Equal(expected, content.Eras.Count, $"{pack}: 代码里的纪元数与期望值对不上。");

            if (expected == 0)
            {
                Check.False(file["eras"] is JsonObject, $"{pack}: 代码里没有纪元，text.json 里却有 eras 分区。");
                continue;
            }

            Check.True(file["eras"] is JsonObject, $"{pack}: text.json 里没有 eras 分区。");
            JsonObject eras = (JsonObject)file["eras"]!;
            Check.Equal(expected, eras.Count, $"{pack}: 文件里的纪元数与期望值对不上。");

            foreach (EraDefinition era in content.Eras)
            {
                Check.True(
                    eras.ContainsKey(era.Id),
                    $"{pack}: text.json 的 eras 里没有「{era.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)eras[era.Id]!;
                Check.Equal((string?)row["name"], era.Name, $"{pack}: 纪元「{era.Id}」的名称不是从文件里读出来的。");
                Check.Equal((string?)row["theme"], era.Theme, $"{pack}: 纪元「{era.Id}」的主题不是从文件里读出来的。");
                Check.Equal((string?)row["icon"], era.Icon, $"{pack}: 纪元「{era.Id}」的图标不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["entryText"],
                    era.EntryText,
                    $"{pack}: 纪元「{era.Id}」的进入文本不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["exitText"],
                    era.ExitText,
                    $"{pack}: 纪元「{era.Id}」的离开文本不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["completionHint"],
                    era.CompletionHint,
                    $"{pack}: 纪元「{era.Id}」的完成提示不是从文件里读出来的。");

                Check.False(string.IsNullOrWhiteSpace(era.Name), $"{pack}: 纪元「{era.Id}」的名称是空的。");
                Check.False(string.IsNullOrWhiteSpace(era.Theme), $"{pack}: 纪元「{era.Id}」的主题是空的。");
                Check.False(string.IsNullOrWhiteSpace(era.Icon), $"{pack}: 纪元「{era.Id}」的图标是空的。");
                Check.False(string.IsNullOrWhiteSpace(era.EntryText), $"{pack}: 纪元「{era.Id}」的进入文本是空的。");
                Check.False(string.IsNullOrWhiteSpace(era.ExitText), $"{pack}: 纪元「{era.Id}」的离开文本是空的。");
                Check.False(
                    string.IsNullOrWhiteSpace(era.CompletionHint),
                    $"{pack}: 纪元「{era.Id}」的完成提示是空的——" +
                    "它为空时界面会静默回退成条件树的自动描述，所以这里必须拦住。");
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

            // 没有图鉴的包（示例包「猫咖物语」）在这里没有可改坏的剧情条目；
            // 它的建筑文案由下面那条独立的守卫覆盖。
            if (content.LoreEntries.Count == 0) continue;

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

                ReadAllExcept(text, content, skipLoreId: victim, skipBuildingId: null, skipEraId: null);

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

    /// <summary>
    /// 建筑文案被改坏的两种形态，每个包都要当场炸。<para>
    /// 与剧情那条同构，但独立成一条：建筑的 id 表来自 <c>Buildings.cs</c>，
    /// 剧情那条的 id 表来自 <c>Lore.cs</c>——两边各有各的漏洞可能，不能只测一边。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheBuildingTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject real = ShippedJson(pack);
            GameContent content = build();
            Check.True(content.Buildings.Count > 0, $"{pack}: 这个包一座建筑都没有，这条守卫就没有意义了。");

            // 基准：原样读一遍，一条都不该抛。
            using (var baseline = new Fixture(pack, real.ToJsonString()))
                ReadAll(baseline.Load(), content);

            string victim = content.Buildings[0].Id;

            // ① 少一座：取它的那一刻抛，且点名是哪一座。
            JsonObject missing = (JsonObject)real.DeepClone();
            ((JsonObject)missing["buildings"]!).Remove(victim);

            using (var bad = new Fixture(pack, missing.ToJsonString()))
            {
                ContentText text = bad.Load();
                ReadAllExcept(text, content, skipLoreId: null, skipBuildingId: victim, skipEraId: null);

                // 其余条目都读过了，孤儿检查不该红——把"缺条目"与"多条目"两种错区分开。
                text.EnsureNoOrphans();

                InvalidOperationException ex = Check.Throws<InvalidOperationException>(
                    () => text.Text("buildings", victim, "name"));
                Check.Contains(ex.Message, victim);
            }

            // ② 多一座：孤儿检查点名报出来，而不是让它静静躺在文件里。
            JsonObject extra = (JsonObject)real.DeepClone();
            ((JsonObject)extra["buildings"]!)["zz_orphan_building"] = new JsonObject
            {
                ["name"] = "无主建筑",
                ["description"] = "无主说明",
            };

            using (var bad = new Fixture(pack, extra.ToJsonString()))
            {
                ContentText text = bad.Load();
                ReadAll(text, content);

                InvalidOperationException ex =
                    Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
                Check.Contains(ex.Message, "zz_orphan_building");
            }
        }
    }

    /// <summary>
    /// 纪元文案被改坏的两种形态，每个有纪元的包都要当场炸。<para>
    /// 与建筑那条同构，但独立成一条：纪元的 id 表来自 <c>Eras.cs</c>，
    /// 剧情来自 <c>Lore.cs</c>、建筑来自 <c>Buildings.cs</c>——三边各有各的漏洞可能。
    /// 没有纪元的包（<c>Neko</c> / <c>Cafe</c>）直接跳过，那件事由
    /// <see cref="EveryEra_ResolvesItsTextFromTheFile"/> 的"期望 0"守着。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheEraTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject real = ShippedJson(pack);
            GameContent content = build();

            if (content.Eras.Count == 0) continue;

            // 基准：原样读一遍，一条都不该抛。
            using (var baseline = new Fixture(pack, real.ToJsonString()))
                ReadAll(baseline.Load(), content);

            string victim = content.Eras[0].Id;

            // ① 少一层：取它的那一刻抛，且点名是哪一层。
            JsonObject missing = (JsonObject)real.DeepClone();
            ((JsonObject)missing["eras"]!).Remove(victim);

            using (var bad = new Fixture(pack, missing.ToJsonString()))
            {
                ContentText text = bad.Load();
                ReadAllExcept(text, content, skipLoreId: null, skipBuildingId: null, skipEraId: victim);

                // 其余条目都读过了，孤儿检查不该红——把"缺条目"与"多条目"两种错区分开。
                text.EnsureNoOrphans();

                InvalidOperationException ex = Check.Throws<InvalidOperationException>(
                    () => text.Text("eras", victim, "completionHint"));
                Check.Contains(ex.Message, victim);
            }

            // ② 多一层：孤儿检查点名报出来，而不是让它静静躺在文件里。
            JsonObject extra = (JsonObject)real.DeepClone();
            ((JsonObject)extra["eras"]!)["zz_orphan_era"] = new JsonObject
            {
                ["name"] = "无主纪元",
                ["theme"] = "无主主题",
                ["icon"] = "❓",
                ["entryText"] = "无主进入文本",
                ["exitText"] = "无主离开文本",
                ["completionHint"] = "无主提示",
            };

            using (var bad = new Fixture(pack, extra.ToJsonString()))
            {
                ContentText text = bad.Load();
                ReadAll(text, content);

                InvalidOperationException ex =
                    Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
                Check.Contains(ex.Message, "zz_orphan_era");
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
    private static void ReadAll(ContentText text, GameContent content) =>
        ReadAllExcept(text, content, skipLoreId: null, skipBuildingId: null, skipEraId: null);

    /// <summary>
    /// 同上，但可以跳过一条剧情条目 / 一座建筑 / 一层纪元。<para>
    /// "文件里少了这一条"那几支要用它：先把其余条目全读过（这样孤儿检查不该红），
    /// 再单独去取被删掉的那一条，才能把"缺条目"与"多条目"两种错区分开。
    /// 注意必须是"整份文件都读过"，少读一个分区（例如纪元）会立刻被孤儿检查抓成红——
    /// 这本身就是 <see cref="ContentText.EnsureNoOrphans"/> 覆盖整份文件的证据。
    /// </para>
    /// </summary>
    private static void ReadAllExcept(
        ContentText text,
        GameContent content,
        string? skipLoreId,
        string? skipBuildingId,
        string? skipEraId)
    {
        ReadStorylines(text, content);
        ReadLore(text, content, skipLoreId);
        ReadBuildings(text, content, skipBuildingId);
        ReadEras(text, content, skipEraId);
    }

    /// <summary>按代码里的图鉴表读标题与正文（跳过 <paramref name="skipId"/> 那一条）。</summary>
    private static void ReadLore(ContentText text, GameContent content, string? skipId = null)
    {
        foreach (LoreEntry entry in content.LoreEntries)
        {
            if (entry.Id == skipId) continue;

            text.Text("lore", entry.Id, "title");
            text.Text("lore", entry.Id, "body");
        }
    }

    /// <summary>按代码里的建筑表读名字、说明与图标（跳过 <paramref name="skipId"/> 那一座）。</summary>
    private static void ReadBuildings(ContentText text, GameContent content, string? skipId = null)
    {
        foreach (BuildingDefinition building in content.Buildings)
        {
            if (building.Id == skipId) continue;

            text.Text("buildings", building.Id, "name");
            text.Text("buildings", building.Id, "description");
            text.Text("buildings", building.Id, "icon");
        }
    }

    /// <summary>按代码里的纪元表读六个面向玩家的字段（跳过 <paramref name="skipId"/> 那一层）。</summary>
    private static void ReadEras(ContentText text, GameContent content, string? skipId = null)
    {
        foreach (EraDefinition era in content.Eras)
        {
            if (era.Id == skipId) continue;

            text.Text("eras", era.Id, "name");
            text.Text("eras", era.Id, "theme");
            text.Text("eras", era.Id, "icon");
            text.Text("eras", era.Id, "entryText");
            text.Text("eras", era.Id, "exitText");
            text.Text("eras", era.Id, "completionHint");
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
