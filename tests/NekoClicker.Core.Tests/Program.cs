using NekoClicker.Core;
using NekoClicker.Core.Tests;

// 快照再生成模式：打印 NekoClicker.Core 的当前公开 API。
//   dotnet exec <测试程序集> --public-api > src/NekoClicker.Core/PublicApi.txt
// 只在"确实有意改动公开 API"时使用，之后必须同步更新版本号与 CHANGELOG（见 docs/VERSIONING.md）。
if (args.Length > 0 && args[0] == "--public-api")
{
    Console.Write(PublicApiSurface.Render(PublicApiSurface.Dump(typeof(GameEngine).Assembly)));
    return 0;
}

// 支持 `dotnet run -- [过滤词]`，便于只跑某个主题的用例。
// `--timing [过滤词]` 额外逐条计时并打印最慢的一批——测试套件跑一遍要十分钟，
// 想砍耗时就只能看逐条的真实数字（按类名子串过滤估耗时已经错过一次）。
bool timing = args.Length > 0 && args[0] == "--timing";
string? filter = timing
    ? (args.Length > 1 ? args[1] : null)
    : (args.Length > 0 ? args[0] : null);

TestSummary summary = TestRunner.RunAll(filter: filter, timing: timing);
return summary.AllPassed ? 0 : 1;
