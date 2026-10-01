using System.Text;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 剧情文本外部化的守卫。<para>
/// 这些用例守的是<b>外部化必然引入的那类沉默失败</b>：文本一旦离开代码，
/// id 与散文就只剩"字符串相等"这层关系，于是"id 打错→界面空白"、
/// "代码删了文件没删→孤儿条目"、"id 重复→静默覆盖"都不会让任何别的东西变红。
/// 所以每一条都必须在这里<b>被喂一份坏文件、然后确认它真的抛</b>。
/// </para>
/// </summary>
public static class ContentTextTests
{
    /// <summary>正常文件：能取到值，能通过孤儿检查。</summary>
    [Test]
    public static void ValidFile_ResolvesTextAndPassesTheOrphanCheck()
    {
        using var fixture = new Fixture("""
            {
              "lore": { "a": { "title": "标题", "body": "正文" } },
              "endings": { "e": { "name": "名", "text": "正文" } },
              "choices": { "c": { "prompt": "问", "options": { "x": { "label": "选项" } } } }
            }
            """);

        ContentText text = fixture.Load();
        Check.Equal("标题", text.Text("lore", "a", "title"));
        Check.Equal("正文", text.Text("lore", "a", "body"));
        Check.Equal("选项", text.Text("choices", "c/x", "label"));

        text.EnsureNoOrphans((kind, id) =>
            kind is "lore" or "endings" or "choices");   // 全部视为已用
    }

    /// <summary>文件不存在 → 抛。丢文件不能静默降级成"没有剧情"。</summary>
    [Test]
    public static void MissingFile_ThrowsInsteadOfSilentlyHavingNoStory()
    {
        using var fixture = new Fixture(files: false);
        Check.Throws<InvalidOperationException>(() => fixture.Load());
    }

    /// <summary>id 打错一个字母 → 抛，而不是界面上出现空白。</summary>
    [Test]
    public static void MissingId_Throws()
    {
        using var fixture = new Fixture("""{ "lore": { "abc": { "title": "t", "body": "b" } } }""");
        ContentText text = fixture.Load();

        InvalidOperationException ex =
            Check.Throws<InvalidOperationException>(() => text.Text("lore", "abd", "body"));
        Check.Contains(ex.Message, "abd");
    }

    /// <summary>字段漏写 → 抛。缺 body 与缺 id 是两种不同的错，都要拦。</summary>
    [Test]
    public static void MissingField_Throws()
    {
        using var fixture = new Fixture("""{ "lore": { "abc": { "title": "t" } } }""");
        ContentText text = fixture.Load();

        InvalidOperationException ex =
            Check.Throws<InvalidOperationException>(() => text.Text("lore", "abc", "body"));
        Check.Contains(ex.Message, "body");
    }

    /// <summary>分区不存在 / 选项不存在 → 抛。</summary>
    [Test]
    public static void MissingSectionOrOption_Throws()
    {
        using var fixture = new Fixture("""{ "choices": { "c": { "options": { "x": { "label": "l" } } } } }""");
        ContentText text = fixture.Load();

        Check.Throws<InvalidOperationException>(() => text.Text("endings", "e", "text"));
        Check.Throws<InvalidOperationException>(() => text.Text("choices", "c/y", "label"));
        Check.Throws<InvalidOperationException>(() => text.Text("choices", "nope/x", "label"));
    }

    /// <summary>孤儿条目 → 抛，并把是哪几条列出来。</summary>
    [Test]
    public static void OrphanEntry_ThrowsAndNamesItself()
    {
        using var fixture = new Fixture("""
            {
              "lore": {
                "used": { "title": "t", "body": "b" },
                "dead": { "title": "t", "body": "b" }
              }
            }
            """);

        ContentText text = fixture.Load();
        Check.Equal("b", text.Text("lore", "used", "body"));

        InvalidOperationException ex =
            Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
        Check.Contains(ex.Message, "dead");
        Check.False(ex.Message.Contains("used", StringComparison.Ordinal), "被取用过的条目不该出现在孤儿名单里。");
    }

    /// <summary>选项那一层的孤儿也要能查出来（它是两层结构，最容易漏）。</summary>
    [Test]
    public static void OrphanChoiceOption_Throws()
    {
        using var fixture = new Fixture("""
            { "choices": { "c": { "prompt": "问", "options": { "a": { "label": "A" }, "b": { "label": "B" } } } } }
            """);

        ContentText text = fixture.Load();
        text.Text("choices", "c", "prompt");
        _ = text.Text("choices", "c/a", "label");

        InvalidOperationException ex =
            Check.Throws<InvalidOperationException>(() => text.EnsureNoOrphans());
        Check.Contains(ex.Message, "c/b");
    }

    /// <summary>可选字段用 TextOr：缺了走回退值，而且不算"已取用"之外的孤儿。</summary>
    [Test]
    public static void OptionalField_FallsBackAndStaysOutOfTheOrphanMath()
    {
        using var fixture = new Fixture("""{ "lore": { "a": { "title": "t", "body": "b" } } }""");
        ContentText text = fixture.Load();

        Check.Equal("📖", text.TextOr("lore", "a", "icon", "📖"));
        Check.Equal("b", text.Text("lore", "a", "body"));
        text.EnsureNoOrphans();
    }

    /// <summary>不是合法 JSON → 抛，且带上路径。</summary>
    [Test]
    public static void MalformedJson_Throws()
    {
        using var fixture = new Fixture("{ \"lore\": ");
        InvalidOperationException ex = Check.Throws<InvalidOperationException>(() => fixture.Load());
        Check.Contains(ex.Message, "text.json");
    }

    /// <summary>
    /// 同一份文本被多个线程同时读，不能把"已读过哪些 id"那张表弄坏。<para>
    /// <b>这不是假想的并发</b>：一个内容包只持有<b>一份</b> <see cref="ContentText"/>
    /// （静态懒加载），而 <c>Build()</c> 在同一个进程里有多个入口——测试里
    /// <c>ArchitectureTests</c> 与 <c>TestGame</c> 的缓存各建一次、Demo 的包目录也会直接建一次，
    /// 而测试是并行跑的。1.2.0 发布时这里没有锁：实测 40 线程 × 30 轮稳定复现
    /// 「集合在并发更新下损坏」（522 次失败），在并行测试里表现为 17 条用例同时变红，
    /// 而单条跑全绿——正是最难查的那种失败。
    /// </para>
    /// </summary>
    [Test]
    public static void ConcurrentReaders_DoNotCorruptTheUsedSet()
    {
        // 条数要够多：`HashSet` 的损坏发生在并发扩容上，两三个键是撞不出来的
        // （第一版守卫就只用了两个键，结果在"去掉锁"的故障注入下照样绿——它自己先成了橡皮图章）。
        const int entries = 200;
        const int threadsCount = 16;
        const int rounds = 30;

        StringBuilder json = new("{ \"lore\": {");
        for (int i = 0; i < entries; i++)
        {
            if (i > 0) json.Append(',');
            json.Append($"\"e{i:D3}\": {{ \"title\": \"t{i}\", \"body\": \"b{i}\" }}");
        }

        json.Append("} }");
        using var fixture = new Fixture(json.ToString());

        // 关键：所有线程共用**同一个**实例（每个线程各 Load 一次就复现不出来了）。
        ContentText text = fixture.Load();
        Exception?[] failures = new Exception?[threadsCount];
        var start = new Barrier(threadsCount);

        Thread[] threads = new Thread[threadsCount];
        for (int i = 0; i < threadsCount; i++)
        {
            int slot = i;
            threads[i] = new Thread(() =>
            {
                start.SignalAndWait();
                try
                {
                    for (int round = 0; round < rounds; round++)
                    {
                        // 每个线程都把这 200 条完整读一遍——真实包在 Build() 里就是这么读的。
                        // 谁先读完，集合就完整了；因此任何线程随后的孤儿检查都不该报孤儿。
                        for (int n = 0; n < entries; n++)
                            text.Text("lore", $"e{n:D3}", "title");

                        text.EnsureNoOrphans();
                    }
                }
                catch (Exception ex)
                {
                    failures[slot] = ex;
                }
            });
        }

        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();

        Exception? first = failures.FirstOrDefault(f => f is not null);
        Check.Null(
            first,
            $"并发读同一份文本时抛了异常：{first?.GetType().Name}: {first?.Message}");
    }

    /// <summary>临时的 content/&lt;包名&gt;/text.json 夹具，用完删掉。</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _root;
        private readonly string _packId = "fixture_pack";

        public Fixture(string json)
        {
            _root = Path.Combine(AppContext.BaseDirectory, "text-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "content", _packId));
            File.WriteAllText(Path.Combine(_root, "content", _packId, "text.json"), json);
        }

        public Fixture(bool files)
        {
            _ = files;
            _root = Path.Combine(AppContext.BaseDirectory, "text-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public ContentText Load() => ContentText.Load(_packId, _root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* 清理失败不影响断言 */ }
        }
    }
}
