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
/// <para>
/// <b>第四轮加的四个分区也在这张表里</b>：<c>endings</c>（<c>Name</c> / <c>Icon</c> /
/// <c>Text</c>）、<c>stances</c>（<c>Name</c> / <c>Theme</c> / <c>Icon</c> / <c>CostText</c>）、
/// <c>choices</c>（<c>Speaker</c> / <c>Prompt</c> / 每个选项的 <c>Label</c> 与
/// <c>OutcomeText</c>）与 <c>achievements</c>（<c>Name</c> / <c>Icon</c> / <c>Description</c>）。
/// 与纪元同一条理由：<b>没有这一类内容的包期望值写 0</b>，并要求文件里也没有那个分区。
/// 成就与其它三类不同——<b>十一个包都有</b>，包括没有纪元、没有表态的示例包。
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
    /// 每个包应当有几个结局。<para>
    /// 与 <see cref="ExpectedBuildings"/> / <see cref="ExpectedEras"/> 同一条理由：
    /// 双向比对只能发现"两边不一致"，<b>代码与文件同时少一个结局</b>只有这张写死的表看得见——
    /// 而少一个结局意味着一条完整的故事线永远走不到。
    /// </para>
    /// <para><b>十一个包全在这张表里</b>：没有结局的包写 0（并额外要求文件里没有 <c>endings</c> 分区）。</para>
    /// </summary>
    private static readonly (string Pack, int Count)[] ExpectedEndings =
    [
        ("Apocalypse", 3),
        ("Cafe", 0),
        ("Civ", 3),
        ("Company", 4),
        ("Cyber", 2),
        ("Dream", 2),
        ("God", 3),
        ("Lab", 5),
        ("Library", 2),
        ("Neko", 0),
        ("NineLives", 5),
    ];

    /// <summary>每个包应当有几条立场。<b>只有三个包有道德轴</b>，其余八个写 0。</summary>
    private static readonly (string Pack, int Count)[] ExpectedStances =
    [
        ("Apocalypse", 0),
        ("Cafe", 0),
        ("Civ", 0),
        ("Company", 3),
        ("Cyber", 0),
        ("Dream", 0),
        ("God", 0),
        ("Lab", 4),
        ("Library", 0),
        ("Neko", 0),
        ("NineLives", 4),
    ];

    /// <summary>每个包应当有几次表态。<b>只有三个包有表态</b>，其余八个写 0。</summary>
    private static readonly (string Pack, int Count)[] ExpectedChoices =
    [
        ("Apocalypse", 0),
        ("Cafe", 0),
        ("Civ", 0),
        ("Company", 6),
        ("Cyber", 0),
        ("Dream", 0),
        ("God", 0),
        ("Lab", 6),
        ("Library", 0),
        ("Neko", 0),
        ("NineLives", 6),
    ];

    /// <summary>
    /// 每个包应当有几条成就。<para>
    /// 成就的条数是这一类里唯一<b>由生成器写出来</b>的：一个包的成就表通常由"每座建筑三档"
    /// 之类的循环铺开，所以"代码与文件同时少一条"完全可以由一次改动造成，而双向比对看不见。
    /// </para>
    /// <para>十一个包全都有成就，包括没有纪元、没有表态的示例包。</para>
    /// </summary>
    private static readonly (string Pack, int Count)[] ExpectedAchievements =
    [
        ("Apocalypse", 67),
        ("Cafe", 45),
        ("Civ", 65),
        ("Company", 66),
        ("Cyber", 67),
        ("Dream", 65),
        ("God", 71),
        ("Lab", 66),
        ("Library", 66),
        ("Neko", 66),
        ("NineLives", 68),
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

    /// <summary>结局条数表的对应性守卫——与 <see cref="EraCountTable_CoversExactlyTheGuardTable"/> 同构。</summary>
    [Test]
    public static void EndingCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedEndings.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "结局条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
    }

    /// <summary>立场条数表的对应性守卫——与 <see cref="EraCountTable_CoversExactlyTheGuardTable"/> 同构。</summary>
    [Test]
    public static void StanceCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedStances.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "立场条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
    }

    /// <summary>表态条数表的对应性守卫——与 <see cref="EraCountTable_CoversExactlyTheGuardTable"/> 同构。</summary>
    [Test]
    public static void ChoiceCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedChoices.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "表态条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
    }

    /// <summary>成就条数表的对应性守卫——与 <see cref="EraCountTable_CoversExactlyTheGuardTable"/> 同构。</summary>
    [Test]
    public static void AchievementCountTable_CoversExactlyTheGuardTable()
    {
        Check.Equal(
            string.Join("、", Externalized.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            string.Join("、", ExpectedAchievements.Select(p => p.Pack).OrderBy(name => name, StringComparer.Ordinal)),
            "成就条数表与守卫表里的包不一致——加一个包就要在两处各加一行。");
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
    /// <c>EndingDefinition</c> 的三个面向玩家的字段（<c>Name</c> / <c>Icon</c> / <c>Text</c>）
    /// 都从文件的 <c>endings</c> 分区读，条数与写死的期望值一致。<para>
    /// 与纪元那条同构：代码 ↔ 文件<b>两个方向</b>都查，再叠一张写死的条数表。
    /// <b>没有结局的包要求文件里也没有这个分区</b>——反过来（文件有、代码不读）
    /// 会由孤儿检查抓住，这里先把"根本不该有"说清楚。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryEnding_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedEndings.Single(entry => entry.Pack == pack).Count;

            Check.Equal(expected, content.Endings.Count, $"{pack}: 代码里的结局数与期望值对不上。");

            if (expected == 0)
            {
                Check.False(file["endings"] is JsonObject, $"{pack}: 代码里没有结局，text.json 里却有 endings 分区。");
                continue;
            }

            Check.True(file["endings"] is JsonObject, $"{pack}: text.json 里没有 endings 分区。");
            JsonObject endings = (JsonObject)file["endings"]!;
            Check.Equal(expected, endings.Count, $"{pack}: 文件里的结局数与期望值对不上。");

            foreach (EndingDefinition ending in content.Endings)
            {
                Check.True(
                    endings.ContainsKey(ending.Id),
                    $"{pack}: text.json 的 endings 里没有「{ending.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)endings[ending.Id]!;
                Check.Equal((string?)row["name"], ending.Name, $"{pack}: 结局「{ending.Id}」的名称不是从文件里读出来的。");
                Check.Equal((string?)row["icon"], ending.Icon, $"{pack}: 结局「{ending.Id}」的图标不是从文件里读出来的。");
                Check.Equal((string?)row["text"], ending.Text, $"{pack}: 结局「{ending.Id}」的终局文本不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(ending.Name), $"{pack}: 结局「{ending.Id}」的名称是空的。");
                Check.False(string.IsNullOrWhiteSpace(ending.Icon), $"{pack}: 结局「{ending.Id}」的图标是空的。");
                Check.False(string.IsNullOrWhiteSpace(ending.Text), $"{pack}: 结局「{ending.Id}」的终局文本是空的。");
            }
        }
    }

    /// <summary>
    /// <c>StanceDefinition</c> 的四个面向玩家的字段（<c>Name</c> / <c>Theme</c> / <c>Icon</c> /
    /// <c>CostText</c>）都从文件的 <c>stances</c> 分区读，条数与写死的期望值一致。
    /// </summary>
    [Test]
    public static void EveryStance_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedStances.Single(entry => entry.Pack == pack).Count;

            Check.Equal(expected, content.Stances.Count, $"{pack}: 代码里的立场数与期望值对不上。");

            if (expected == 0)
            {
                Check.False(file["stances"] is JsonObject, $"{pack}: 代码里没有立场，text.json 里却有 stances 分区。");
                continue;
            }

            Check.True(file["stances"] is JsonObject, $"{pack}: text.json 里没有 stances 分区。");
            JsonObject stances = (JsonObject)file["stances"]!;
            Check.Equal(expected, stances.Count, $"{pack}: 文件里的立场数与期望值对不上。");

            foreach (StanceDefinition stance in content.Stances)
            {
                Check.True(
                    stances.ContainsKey(stance.Id),
                    $"{pack}: text.json 的 stances 里没有「{stance.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)stances[stance.Id]!;
                Check.Equal((string?)row["name"], stance.Name, $"{pack}: 立场「{stance.Id}」的名称不是从文件里读出来的。");
                Check.Equal((string?)row["theme"], stance.Theme, $"{pack}: 立场「{stance.Id}」的主题不是从文件里读出来的。");
                Check.Equal((string?)row["icon"], stance.Icon, $"{pack}: 立场「{stance.Id}」的图标不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["costText"],
                    stance.CostText,
                    $"{pack}: 立场「{stance.Id}」的代价文本不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(stance.Name), $"{pack}: 立场「{stance.Id}」的名称是空的。");
                Check.False(string.IsNullOrWhiteSpace(stance.Theme), $"{pack}: 立场「{stance.Id}」的主题是空的。");
                Check.False(string.IsNullOrWhiteSpace(stance.Icon), $"{pack}: 立场「{stance.Id}」的图标是空的。");
                Check.False(string.IsNullOrWhiteSpace(stance.CostText), $"{pack}: 立场「{stance.Id}」的代价文本是空的。");
            }
        }
    }

    /// <summary>
    /// <c>ChoiceDefinition</c> 的问句与每个 <c>ChoiceOption</c> 的按钮文字 / 结果文本都从文件的
    /// <c>choices</c> 分区读。<para>
    /// 这一类的 id 是<b>两层</b>（<c>选择id/选项id</c>），所以这里是四张表一起查：
    /// 问题（<c>speaker</c> / <c>prompt</c>）与选项（<c>label</c> / <c>outcomeText</c>）。
    /// <b>选项 id / 立场 id / 权重 / 触发条件留在代码里</b>——它们是"这题怎么算分"，不是文案。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryChoice_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedChoices.Single(entry => entry.Pack == pack).Count;

            Check.Equal(expected, content.Choices.Count, $"{pack}: 代码里的表态数与期望值对不上。");

            if (expected == 0)
            {
                Check.False(file["choices"] is JsonObject, $"{pack}: 代码里没有表态，text.json 里却有 choices 分区。");
                continue;
            }

            Check.True(file["choices"] is JsonObject, $"{pack}: text.json 里没有 choices 分区。");
            JsonObject choices = (JsonObject)file["choices"]!;
            Check.Equal(expected, choices.Count, $"{pack}: 文件里的表态数与期望值对不上。");

            foreach (ChoiceDefinition choice in content.Choices)
            {
                Check.True(
                    choices.ContainsKey(choice.Id),
                    $"{pack}: text.json 的 choices 里没有「{choice.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)choices[choice.Id]!;
                Check.Equal((string?)row["speaker"], choice.Speaker, $"{pack}: 表态「{choice.Id}」的说话人不是从文件里读出来的。");
                Check.Equal((string?)row["prompt"], choice.Prompt, $"{pack}: 表态「{choice.Id}」的问句不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(choice.Speaker), $"{pack}: 表态「{choice.Id}」的说话人是空的。");
                Check.False(string.IsNullOrWhiteSpace(choice.Prompt), $"{pack}: 表态「{choice.Id}」的问句是空的。");

                Check.True(row["options"] is JsonObject, $"{pack}: 表态「{choice.Id}」在文件里没有 options 分区。");
                JsonObject options = (JsonObject)row["options"]!;
                Check.Equal(
                    choice.Options.Count,
                    options.Count,
                    $"{pack}: 表态「{choice.Id}」的选项数与文件里对不上（应当被孤儿检查挡住）。");

                foreach (ChoiceOption option in choice.Options)
                {
                    string composite = choice.Id + "/" + option.Id;
                    Check.True(options.ContainsKey(option.Id), $"{pack}: text.json 的 choices 里没有「{composite}」。");

                    JsonObject optionRow = (JsonObject)options[option.Id]!;
                    Check.Equal(
                        (string?)optionRow["label"],
                        option.Label,
                        $"{pack}: 选项「{composite}」的按钮文字不是从文件里读出来的。");
                    Check.Equal(
                        (string?)optionRow["outcomeText"],
                        option.OutcomeText,
                        $"{pack}: 选项「{composite}」的结果文本不是从文件里读出来的。");
                    Check.False(string.IsNullOrWhiteSpace(option.Label), $"{pack}: 选项「{composite}」的按钮文字是空的。");
                    Check.False(string.IsNullOrWhiteSpace(option.OutcomeText), $"{pack}: 选项「{composite}」的结果文本是空的。");
                }
            }
        }
    }

    /// <summary>
    /// <c>AchievementDefinition</c> 的三个面向玩家的字段（<c>Name</c> / <c>Icon</c> /
    /// <c>Description</c>）都从文件的 <c>achievements</c> 分区读，条数与写死的期望值一致。<para>
    /// <b>这一类与其它三类有一处本质区别</b>：成就的条目大多由生成器铺出来
    /// （"每座建筑三档"之类），文案里带着建筑名与数字，所以文件里存的是<b>按 id 展开后的成品</b>，
    /// 而不是模板。id / 解锁条件 / 修饰符 / 分组 / 档位 / 是否隐藏全部留在代码里。
    /// </para>
    /// </summary>
    [Test]
    public static void EveryAchievement_ResolvesItsTextFromTheFile()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            JsonObject file = ShippedJson(pack);
            GameContent content = build();
            int expected = ExpectedAchievements.Single(entry => entry.Pack == pack).Count;

            Check.Equal(expected, content.Achievements.Count, $"{pack}: 代码里的成就数与期望值对不上。");
            Check.True(file["achievements"] is JsonObject, $"{pack}: text.json 里没有 achievements 分区。");
            JsonObject achievements = (JsonObject)file["achievements"]!;
            Check.Equal(expected, achievements.Count, $"{pack}: 文件里的成就数与期望值对不上。");

            foreach (AchievementDefinition achievement in content.Achievements)
            {
                Check.True(
                    achievements.ContainsKey(achievement.Id),
                    $"{pack}: text.json 的 achievements 里没有「{achievement.Id}」——它会在构建内容时抛。");

                JsonObject row = (JsonObject)achievements[achievement.Id]!;
                Check.Equal(
                    (string?)row["name"],
                    achievement.Name,
                    $"{pack}: 成就「{achievement.Id}」的名称不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["icon"],
                    achievement.Icon,
                    $"{pack}: 成就「{achievement.Id}」的图标不是从文件里读出来的。");
                Check.Equal(
                    (string?)row["description"],
                    achievement.Description,
                    $"{pack}: 成就「{achievement.Id}」的说明不是从文件里读出来的。");
                Check.False(string.IsNullOrWhiteSpace(achievement.Name), $"{pack}: 成就「{achievement.Id}」的名称是空的。");
                Check.False(string.IsNullOrWhiteSpace(achievement.Icon), $"{pack}: 成就「{achievement.Id}」的图标是空的。");
                Check.False(
                    string.IsNullOrWhiteSpace(achievement.Description),
                    $"{pack}: 成就「{achievement.Id}」的说明是空的。");
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

                ReadAllExcept(text, content, "lore/" + victim);

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
                ReadAllExcept(text, content, "buildings/" + victim);

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
                ReadAllExcept(text, content, "eras/" + victim);

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

    /// <summary>
    /// 结局文案被改坏的两种形态，每个有结局的包都要当场炸——与建筑 / 纪元那两条同构。<para>
    /// 结局的 id 表来自 <c>Endings.cs</c>，与其余三类各是一个源文件，所以各查一遍。
    /// 没有结局的包（<c>Neko</c> / <c>Cafe</c>）直接跳过，那件事由
    /// <see cref="EveryEnding_ResolvesItsTextFromTheFile"/> 的"期望 0"守着。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheEndingTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            GameContent content = build();
            if (content.Endings.Count == 0) continue;

            ProveCategoryFailsLoudly(
                (pack, build),
                "endings",
                "text",
                content.Endings[0].Id,
                "zz_orphan_ending",
                new JsonObject
                {
                    ["name"] = "无主结局",
                    ["icon"] = "❓",
                    ["text"] = "无主终局文本",
                });
        }
    }

    /// <summary>立场文案被改坏的两种形态，每个有立场的包都要当场炸。</summary>
    [Test]
    public static void EditingTheStanceTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            GameContent content = build();
            if (content.Stances.Count == 0) continue;

            ProveCategoryFailsLoudly(
                (pack, build),
                "stances",
                "costText",
                content.Stances[0].Id,
                "zz_orphan_stance",
                new JsonObject
                {
                    ["name"] = "无主立场",
                    ["theme"] = "无主主题",
                    ["icon"] = "❓",
                    ["costText"] = "无主代价",
                });
        }
    }

    /// <summary>
    /// 表态文案被改坏的两种形态，每个有表态的包都要当场炸。<para>
    /// 这一类的"少一条"删的是<b>整块</b>（问句 + 全部选项），所以跳过的是
    /// <c>choices/表态id</c>——选项那一层由同一个 <see cref="ReadChoices"/> 一并跳过。
    /// </para>
    /// </summary>
    [Test]
    public static void EditingTheChoiceTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            GameContent content = build();
            if (content.Choices.Count == 0) continue;

            ProveCategoryFailsLoudly(
                (pack, build),
                "choices",
                "prompt",
                content.Choices[0].Id,
                "zz_orphan_choice",
                new JsonObject
                {
                    ["speaker"] = "无主说话人",
                    ["prompt"] = "无主问句",
                });
        }
    }

    /// <summary>
    /// 成就文案被改坏的两种形态，<b>每个包</b>都要当场炸——包括没有纪元、没有表态的示例包，
    /// 所以这条不能像上面三条那样"期望 0 就跳过"。
    /// </summary>
    [Test]
    public static void EditingTheAchievementTextWrongly_FailsLoudly()
    {
        foreach ((string pack, Func<GameContent> build) in Externalized)
        {
            GameContent content = build();
            Check.True(content.Achievements.Count > 0, $"{pack}: 这个包一条成就都没有，这条守卫就没有意义了。");

            ProveCategoryFailsLoudly(
                (pack, build),
                "achievements",
                "description",
                content.Achievements[0].Id,
                "zz_orphan_achievement",
                new JsonObject
                {
                    ["name"] = "无主成就",
                    ["icon"] = "❓",
                    ["description"] = "无主说明",
                });
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
    private static void ReadAll(ContentText text, GameContent content) => ReadAllExcept(text, content);

    /// <summary>
    /// 同上，但可以跳过若干条。<para>
    /// 跳过的写法是 <c>"分区/id"</c>（选项那层是 <c>"choices/选择id/选项id"</c>）；
    /// "文件里少了这一条"那几支要用它：先把其余条目全读过（这样孤儿检查不该红），
    /// 再单独去取被删掉的那一条，才能把"缺条目"与"多条目"两种错区分开。
    /// 注意必须是"<b>整份文件</b>都读过"，少读一个分区会立刻被孤儿检查抓成红——
    /// 这本身就是 <see cref="ContentText.EnsureNoOrphans"/> 覆盖整份文件的证据。
    /// </para>
    /// </summary>
    private static void ReadAllExcept(ContentText text, GameContent content, params string[] skip)
    {
        HashSet<string> skipped = [.. skip];

        ReadStorylines(text, content);
        ReadLore(text, content, skipped);
        ReadBuildings(text, content, skipped);
        ReadEras(text, content, skipped);
        ReadEndings(text, content, skipped);
        ReadStances(text, content, skipped);
        ReadChoices(text, content, skipped);
        ReadAchievements(text, content, skipped);
    }

    /// <summary>按代码里的图鉴表读标题与正文（跳过 <paramref name="skip"/> 里点到的那一条）。</summary>
    private static void ReadLore(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (LoreEntry entry in content.LoreEntries)
        {
            if (skip.Contains("lore/" + entry.Id)) continue;

            text.Text("lore", entry.Id, "title");
            text.Text("lore", entry.Id, "body");
        }
    }

    /// <summary>按代码里的建筑表读名字、说明与图标（跳过点到的那一座）。</summary>
    private static void ReadBuildings(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (BuildingDefinition building in content.Buildings)
        {
            if (skip.Contains("buildings/" + building.Id)) continue;

            text.Text("buildings", building.Id, "name");
            text.Text("buildings", building.Id, "description");
            text.Text("buildings", building.Id, "icon");
        }
    }

    /// <summary>按代码里的纪元表读六个面向玩家的字段（跳过点到的那一层）。</summary>
    private static void ReadEras(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (EraDefinition era in content.Eras)
        {
            if (skip.Contains("eras/" + era.Id)) continue;

            text.Text("eras", era.Id, "name");
            text.Text("eras", era.Id, "theme");
            text.Text("eras", era.Id, "icon");
            text.Text("eras", era.Id, "entryText");
            text.Text("eras", era.Id, "exitText");
            text.Text("eras", era.Id, "completionHint");
        }
    }

    /// <summary>按代码里的结局表读名称、图标与终局文本（跳过点到的那一个结局）。</summary>
    private static void ReadEndings(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (EndingDefinition ending in content.Endings)
        {
            if (skip.Contains("endings/" + ending.Id)) continue;

            text.Text("endings", ending.Id, "name");
            text.Text("endings", ending.Id, "icon");
            text.Text("endings", ending.Id, "text");
        }
    }

    /// <summary>按代码里的立场表读四个面向玩家的字段（跳过点到的那一条立场）。</summary>
    private static void ReadStances(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (StanceDefinition stance in content.Stances)
        {
            if (skip.Contains("stances/" + stance.Id)) continue;

            text.Text("stances", stance.Id, "name");
            text.Text("stances", stance.Id, "theme");
            text.Text("stances", stance.Id, "icon");
            text.Text("stances", stance.Id, "costText");
        }
    }

    /// <summary>
    /// 按代码里的表态表读问句与每个选项的按钮文字 / 结果文本。<para>
    /// 跳过一条表态就等于跳过它的全部选项（文件里那一条整块没了），
    /// 也可以只跳过一个选项（<c>"choices/选择id/选项id"</c>）用来单独验证选项那一层。
    /// </para>
    /// </summary>
    private static void ReadChoices(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (ChoiceDefinition choice in content.Choices)
        {
            if (skip.Contains("choices/" + choice.Id)) continue;

            text.Text("choices", choice.Id, "speaker");
            text.Text("choices", choice.Id, "prompt");

            foreach (ChoiceOption option in choice.Options)
            {
                string composite = choice.Id + "/" + option.Id;
                if (skip.Contains("choices/" + composite)) continue;

                text.Text("choices", composite, "label");
                text.Text("choices", composite, "outcomeText");
            }
        }
    }

    /// <summary>按代码里的成就表读名称、图标与说明（跳过点到的那一条成就）。</summary>
    private static void ReadAchievements(ContentText text, GameContent content, HashSet<string> skip)
    {
        foreach (AchievementDefinition achievement in content.Achievements)
        {
            if (skip.Contains("achievements/" + achievement.Id)) continue;

            text.Text("achievements", achievement.Id, "name");
            text.Text("achievements", achievement.Id, "icon");
            text.Text("achievements", achievement.Id, "description");
        }
    }

    /// <summary>
    /// 一类文案的两种坏法都必须当场炸：<b>少一条</b>（取它的那一刻抛，且点名是哪一条）与
    /// <b>多一条</b>（孤儿检查点名）。<para>
    /// 四个新分区（结局 / 立场 / 表态 / 成就）的 id 表各来自一个不同的源文件，
    /// 各有各的漏洞可能，所以每类各跑一遍——这里只是把共同的那套动作收成一个辅助方法。
    /// </para>
    /// </summary>
    private static void ProveCategoryFailsLoudly(
        (string Pack, Func<GameContent> Content) entry,
        string section,
        string probeField,
        string victim,
        string orphanId,
        JsonObject orphanRow)
    {
        string pack = entry.Pack;
        JsonObject real = ShippedJson(pack);
        GameContent content = entry.Content();

        // 基准：原样读一遍，一条都不该抛。
        using (var baseline = new Fixture(pack, real.ToJsonString()))
            ReadAll(baseline.Load(), content);

        // ① 少一条：读它的那一刻抛，且点名是哪一条。
        JsonObject missing = (JsonObject)real.DeepClone();
        ((JsonObject)missing[section]!).Remove(victim);

        using (var bad = new Fixture(pack, missing.ToJsonString()))
        {
            ContentText text = bad.Load();
            ReadAllExcept(text, content, section + "/" + victim);

            // 其余条目都读过了，孤儿检查不该红——把"缺条目"与"多条目"两种错区分开。
            text.EnsureNoOrphans();

            InvalidOperationException ex = Check.Throws<InvalidOperationException>(
                () => text.Text(section, victim, probeField));
            Check.Contains(ex.Message, victim);
        }

        // ② 多一条：孤儿检查点名把它报出来，而不是让它静静躺在文件里。
        JsonObject extra = (JsonObject)real.DeepClone();
        ((JsonObject)extra[section]!)[orphanId] = orphanRow;

        using (var bad = new Fixture(pack, extra.ToJsonString()))
        {
            ContentText text = bad.Load();
            ReadAll(text, content);

            InvalidOperationException ex =
                Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
            Check.Contains(ex.Message, orphanId);
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
