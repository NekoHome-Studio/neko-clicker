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
string? filter = args.Length > 0 ? args[0] : null;
TestSummary summary = TestRunner.RunAll(filter: filter);
return summary.AllPassed ? 0 : 1;
