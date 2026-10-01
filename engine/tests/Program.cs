using NekoClicker.Core;
using NekoClicker.Core.Tests;

// 快照再生成模式：打印 NekoClicker.Core 的当前公开 API。
//   dotnet exec <测试程序集> --public-api > engine/core/PublicApi.txt
// 只在"确实有意改动公开 API"时使用，之后必须同步更新版本号与 CHANGELOG（见 engine/docs/VERSIONING.md）。
if (args.Length > 0 && args[0] == "--public-api")
{
    Console.Write(PublicApiSurface.Render(PublicApiSurface.Dump(typeof(GameEngine).Assembly)));
    return 0;
}

// 支持 `dotnet run -- [过滤词]`，便于只跑某个主题的用例。
// `--timing [过滤词]` 额外逐条计时并打印最慢的一批——测试套件跑一遍要几分钟，
// 想砍耗时就只能看逐条的真实数字（按类名子串过滤估耗时已经错过两次）。
// `--serial` 关掉并行、`--jobs N` 指定并发度：并行是默认的，留这两个口子是为了
// 排查"某条用例在并行下才红"，以及量并行到底带来了多少。
bool timing = args.Contains("--timing", StringComparer.Ordinal);
bool serial = args.Contains("--serial", StringComparer.Ordinal);

int jobs = 0;
int jobsAt = Array.IndexOf(args, "--jobs");
if (jobsAt >= 0 && jobsAt + 1 < args.Length && int.TryParse(args[jobsAt + 1], out int parsed) && parsed > 0)
    jobs = parsed;
if (serial) jobs = 1;

// 过滤词 = 第一个不以 `--` 开头的参数。
string? filter = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

TestSummary summary = TestRunner.RunAll(filter: filter, timing: timing, jobs: jobs);
return summary.AllPassed ? 0 : 1;
