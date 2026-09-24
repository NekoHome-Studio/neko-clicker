using NekoClicker.Core.Tests;

// 支持 `dotnet run -- [过滤词]`，便于只跑某个主题的用例。
string? filter = args.Length > 0 ? args[0] : null;
TestSummary summary = TestRunner.RunAll(filter: filter);
return summary.AllPassed ? 0 : 1;
