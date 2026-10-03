using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Hosts;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 作答延迟埋点的**格式与算术**（<see cref="ChoiceLatencyLog"/>）。<para>
/// 这份实现现在由两个宿主共用（共享源码：<c>games/hosts/Shared/ChoiceLatencyLog.cs</c>），
/// 用例里出现的类型来自 Demo.Cli 那个程序集——而 Web 宿主编的是**同一份源码**，
/// 所以"测的是一份副本、线上跑的是另一份"这种最坏形态不存在。
/// </para>
/// <para>
/// 这些埋点用例存在的理由和别的守卫不太一样：埋点的失效方式<b>全是沉默的</b>——
/// 少记一条样本、把"量不出"当成"没作答"、写盘失败被吞掉、格式改了没人解析得动，
/// 这四种在现象上都只是"文件里没有那个数"。而这份文件是"真人从看到表态到作答要多久"
/// 唯一的来源（1.5.0 之前它服务的那个宽限期参数已经退役），
/// 一个安静失效的埋点会让人得出一个没有依据的结论。
/// </para>
/// <para>
/// 所以这里钉的是三件事：<b>算术</b>（模拟秒之差）、<b>记账</b>（量不出的必须留痕）、
/// <b>格式</b>（列数、列序、不变文化、无 BOM、表头只写一次）。
/// </para>
/// </summary>
internal sealed class WebChoiceLatencyTests
{
    // ---------------------------------------------------------------- ① 算术

    /// <summary>从"表态进队列"到"作答"的模拟秒差，就是这条埋点要量的数。</summary>
    [Test]
    public static void Probe_MeasuresSimulatedSecondsFromTriggerToAnswer()
    {
        GameEngine engine = NewEngine();

        using var probe = new ChoiceLatencyLog(engine, "latency-test", LatencyLogFormat.WebHost);

        engine.State.PlayTimeSeconds = 10;
        engine.Click();          // 满足 ClicksAtLeast(1)
        engine.CheckChoices();   // → ChoiceTriggeredEvent
        double shownAt = engine.PlayTimeSeconds;
        Check.Equal(0, probe.Samples.Count, "刚触发时还不该有样本——玩家还没作答。");

        engine.State.PlayTimeSeconds = shownAt + 32.5;
        Check.True(engine.AnswerChoice("c1", "yes"), "c1/yes 应当作答成功。");

        Check.Equal(1, probe.Samples.Count, "作答一次应当恰好产出一条样本。");
        LatencySample sample = probe.Samples[0];
        Check.Equal("c1", sample.ChoiceId);
        Check.Equal("yes", sample.OptionId);
        Check.Close(32.5, sample.SimulatedSeconds, 1e-9, "模拟秒必须是两次 PlayTimeSeconds 之差。");
        Check.True(probe.Summary().Contains("样本 1 条", StringComparison.Ordinal), $"汇总应当报出样本数：<{probe.Summary()}>");
    }

    /// <summary>
    /// 会话开始前就挂在待答队列里的表态（读档接手、宿主刚起来）<b>量不出</b>真实等待时长
    /// ——它必须被记账，不能被丢掉。<para>
    /// 这是 Web 宿主里最常见的一类作答：玩家打开页面时那条表态早就挂着了。
    /// 丢掉的话，最常见的情形反而一条样本都不产出，而"没有样本"会被读成"没人作答"。
    /// </para>
    /// </summary>
    [Test]
    public static void Probe_RecordsAlreadyPendingChoicesAsUnmeasurable()
    {
        GameEngine engine = NewEngine();

        // 手工把它放进待答队列，模拟"这次会话开始前就挂着"。
        engine.State.PendingChoices.Add("c1");

        using var probe = new ChoiceLatencyLog(engine, "latency-test", LatencyLogFormat.WebHost);
        engine.State.PlayTimeSeconds += 100;

        Check.True(engine.AnswerChoice("c1", "yes"), "待答队列里的选择应当能作答。");
        Check.Equal(0, probe.Samples.Count, "等待起点没被观测到，就不许凭空造一个样本。");
        Check.Equal(1, probe.UnmeasuredCount, "但它必须被如实记账。");
    }

    // ---------------------------------------------------------------- ② 文件格式

    /// <summary>表头只写一次（幂等），样本行是固定列序的制表符分隔文本，且文件不带 BOM。</summary>
    [Test]
    public static void LatencyFile_WritesHeaderOnceAndAppendsSamplesInAFixedFormat()
    {
        string directory = NewTempDirectory();
        try
        {
            string path = Path.Combine(directory, "latency.txt");
            var file = new LatencyLogFile(path);

            Check.True(file.Ensure(), "第一次应当能建出文件与表头。");
            string[] afterFirst = File.ReadAllLines(path);
            Check.True(afterFirst.Length >= 5, "表头必须自述列意（这份文件的读者常常不是启动宿主的人）。");
            Check.True(afterFirst[0].StartsWith("# neko-clicker", StringComparison.Ordinal), $"第一行应当是表头：<{afterFirst[0]}>");

            Check.True(file.Ensure(), "重复 Ensure 应当成功。");
            Check.Equal(afterFirst.Length, File.ReadAllLines(path).Length, "Ensure 必须幂等：不许重复写表头。");

            var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var sample = new LatencySample("c1", "yes", 12.5, 12.4, at);
            Check.True(file.Append(LatencyLogFormat.SampleLine("cafe", sample, 30, 1)));

            string line = File.ReadAllLines(path)[^1];
            string[] fields = line.Split(LatencyLogFormat.Separator);
            Check.Equal(9, fields.Length, $"样本行应当是 9 列：<{line}>");
            Check.Equal("S", fields[0]);
            Check.Equal("2026-01-02T03:04:05.000Z", fields[1]);
            Check.Equal("cafe", fields[2]);
            Check.Equal("c1", fields[3]);
            Check.Equal("yes", fields[4]);
            Check.Equal("12.5", fields[5], "模拟秒（不变文化的小数点写法）。");
            Check.Equal("12.4", fields[6], "真实秒。");
            Check.Equal("30", fields[7], "当时的宽限期参数（1.5.0 起已退役、不再参与判定，但样本行仍要带上它）。");
            Check.Equal("1", fields[8]);

            // 不可量的行也要能落在同一个文件里，列数与原因都得在。
            Check.True(file.Append(LatencyLogFormat.UnmeasuredLine("cafe", "c9", "no", "会话开始前就挂着", at)));
            string[] unmeasured = File.ReadAllLines(path)[^1].Split(LatencyLogFormat.Separator);
            Check.Equal(6, unmeasured.Length, "U 行应当是 6 列。");
            Check.Equal("U", unmeasured[0]);
            Check.Equal("c9", unmeasured[3]);

            // UTF-8 且**不带 BOM**：追加式文件里 BOM 只会在开头出现一次，容易被误读成数据。
            byte[] bytes = File.ReadAllBytes(path);
            Check.Equal((byte)'#', bytes[0], "文件不该以 UTF-8 BOM 开头。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 写盘失败既不能抛进玩法，也不能静默。<para>
    /// 这里刻意让"父目录"其实是一个文件，于是建目录必然失败（<c>IOException</c>）——
    /// 埋点必须照样把样本量出来，同时把失败次数与原因留下来。
    /// </para>
    /// </summary>
    [Test]
    public static void Probe_SurvivesAnUnwritableLogFileWithoutLosingTheMeasurement()
    {
        string directory = NewTempDirectory();
        try
        {
            string blocker = Path.Combine(directory, "blocker");
            File.WriteAllText(blocker, "这不是目录");
            string path = Path.Combine(blocker, "latency.txt");

            GameEngine engine = NewEngine();
            using var probe = new ChoiceLatencyLog(engine, "latency-test", LatencyLogFormat.WebHost, path);

            engine.State.PlayTimeSeconds = 5;
            engine.Click();
            engine.CheckChoices();
            engine.State.PlayTimeSeconds = 15;
            Check.True(engine.AnswerChoice("c1", "yes"), "作答本身不该受埋点写盘失败影响。");

            Check.Equal(1, probe.Samples.Count, "写盘失败不该让样本消失。");
            Check.True(probe.FileWriteFailures > 0, "写盘失败必须被记账（沉默失败正是要消灭的东西）。");
            Check.True(probe.FileError is not null, "失败原因必须留得下来。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 文件里已经有**别人**写的东西时：不覆盖它，但也不让新行落在一个解释不了它们的表头下面。<para>
    /// 这条不是假想出来的情形：<c>artifacts/latency.txt</c> 上真的躺着一份**旧埋点实现**留下的
    /// <c>heartbeat</c> 文件（2026-10-01）。覆盖它是毁掉别人的数据；沉默地混写则会让之后
    /// 读这个文件的人把两种格式当成一种——而这份文件是"真人从看到表态到作答有多久"唯一的来源。
    /// </para>
    /// </summary>
    [Test]
    public static void LatencyFile_KeepsForeignContentButStaysSelfDescribing()
    {
        string directory = NewTempDirectory();
        try
        {
            string path = Path.Combine(directory, "latency.txt");
            File.WriteAllText(
                path,
                "latency probe started\n[19:21:58] heartbeat pending=0 seen=0 answered=0\n",
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var file = new LatencyLogFile(path);
            Check.True(file.Ensure(), "文件里已有别人的内容时也应当能继续写。");

            string[] lines = File.ReadAllLines(path);
            Check.Equal("latency probe started", lines[0], "别人的内容必须原样留在最前面（不许覆盖）。");
            string text = File.ReadAllText(path);
            Check.True(
                text.Contains(LatencyLogFormat.ForeignContentNote, StringComparison.Ordinal),
                "必须有一段分隔说明，讲清上面那部分不是当前实现写的。");
            Check.True(
                text.Contains(LatencyLogFormat.HeaderSignature, StringComparison.Ordinal),
                "当前实现的表头必须出现，否则后面那些列没有列意。");

            Check.True(file.Ensure(), "第二次 Ensure 应当成功。");
            Check.Equal(lines.Length, File.ReadAllLines(path).Length, "幂等：说明与表头只补一次。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ---------------------------------------------------------------- 辅助

    private static GameEngine NewEngine()
    {
        var clock = new ManualClock();
        return new GameEngine(ChoiceContent(), new GameEngineOptions
        {
            Clock = clock,
            Seed = 7,
            GrantOfflineProgress = false,
            MaxNotifications = 8,
        });
    }

    /// <summary>一份最小的合成内容：一条表态，触发条件是"点过一下"。</summary>
    private static GameContent ChoiceContent() => new GameContentBuilder("LatencyProbe")
        .WithCurrency("单位", "u")
        .Add(new BuildingDefinition { Id = "b", Name = "工坊", BasePrice = 10, BaseCps = 1 })
        .AddChoices(new ChoiceDefinition
        {
            Id = "c1",
            Speaker = "她",
            Prompt = "要不要？",
            Trigger = UnlockCondition.ClicksAtLeast(1),
            Options =
            [
                new ChoiceOption { Id = "yes", Label = "要。", OutcomeText = "要了。", Weight = 0 },
                new ChoiceOption { Id = "no", Label = "不要。", OutcomeText = "没要。", Weight = 0 },
            ],
        })
        .Build();

    /// <summary>仓库内的临时目录（<c>artifacts/</c> 已被 .gitignore 忽略），跑完由用例自己删掉。</summary>
    private static string NewTempDirectory()
    {
        string root = RepositoryPaths.Find() ?? Path.GetTempPath();
        string directory = Path.Combine(root, "artifacts", "latency-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
