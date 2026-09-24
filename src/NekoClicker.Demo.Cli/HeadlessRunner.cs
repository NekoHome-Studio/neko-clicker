using System.Diagnostics;
using System.Text;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;
using NekoClicker.Core.Views;

namespace NekoClicker.Demo.Cli;

/// <summary>
/// 无头模式：把引擎当成一个纯计算库来跑。<para>
/// 这一层存在的意义是证明框架<b>真的可以不依赖 UI</b>：<c>--simulate --auto</c> 会用一个
/// 贪心策略替玩家玩上几小时并打印数值报告，既可以用来验收内容曲线，
/// 也可以在 CI 里当压力测试跑。
/// </para>
/// </summary>
internal static class HeadlessRunner
{
    /// <summary>运行模拟并打印报告。</summary>
    public static int RunSimulation(CliOptions options)
    {
        using var session = new GameSession(options.Package, options.SavePath, options.Seed);
        GameEngine engine = session.Engine;

        Console.WriteLine($"内容包：{engine.Content.Title}（--package {options.Package.Id}）");
        Console.WriteLine(
            $"目标时长：{NumFormat.Duration(options.SimulateSeconds)}" +
            $"｜策略：{(options.AutoPlay ? "自动购买" : "纯挂机（不购买）")}" +
            $"｜种子：{(options.Seed == 0 ? "随机" : options.Seed.ToString())}");
        Console.WriteLine();

        var stopwatch = Stopwatch.StartNew();
        Advance(engine, options.SimulateSeconds, options.AutoPlay, options.SimulateSeconds, options.Package);
        stopwatch.Stop();

        // 上面直接操作引擎，这里必须刷新一次，否则报告读到的还是开局快照。
        session.Refresh();
        PrintReport(session, stopwatch.Elapsed.TotalSeconds);
        return 0;
    }

    /// <summary>渲染一帧界面并打印（用于验证布局，可重定向到文件）。</summary>
    public static int RunFrame(CliOptions options)
    {
        using var session = new GameSession(options.Package, options.SavePath, options.Seed);

        if (options.AutoPlay && options.SimulateSeconds > 0)
            Advance(session.Engine, options.SimulateSeconds, autoPlay: true, options.SimulateSeconds, options.Package);

        session.Refresh();
        List<string> lines = TerminalUi.Render(session, options.Width, options.Height);
        foreach (string line in lines) Console.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// 推进模拟。<paramref name="autoPlay"/> 为真时用贪心策略替玩家操作。<para>
    /// 切片为 5 秒：随机事件最短只活 13 秒，切片太大就会一次次错过它。
    /// </para>
    /// </summary>
    private static void Advance(
        GameEngine engine,
        double seconds,
        bool autoPlay,
        double totalSeconds,
        ContentPackage package,
        double slice = 5)
    {
        double remaining = Math.Max(0, seconds);
        double sincePurchase = 0;
        double nextReport = 0;
        double reportEvery = Math.Max(1, totalSeconds / 10);

        while (remaining > 0)
        {
            double step = Math.Min(slice, remaining);
            engine.Simulate(step);
            remaining -= step;
            sincePurchase += step;

            if (!autoPlay) continue;

            // 模拟玩家的手速：早期靠点击启动，后期点击收益占比自然下降。
            for (int i = 0; i < 4; i++) engine.Click();

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (sincePurchase >= 10)
            {
                sincePurchase = 0;
                BuyGreedily(engine);
            }

            if (totalSeconds - remaining >= nextReport + reportEvery)
            {
                nextReport = totalSeconds - remaining;
                Console.WriteLine(
                    $"  [{NumFormat.Duration(totalSeconds - remaining),8}] " +
                    $"{NumFormat.FormatLong(engine.State.Cookies),12} {engine.Content.CurrencyName}" +
                    $"   产量 {NumFormat.FormatLong(engine.CookiesPerSecond),12}/s" +
                    $"   建筑 {NumFormat.FormatPlain(engine.State.TotalBuildings()),6}" +
                    $"   成就 {engine.State.Achievements.Count,3}" +
                    $"   {package.GoldenCookieName} {NumFormat.FormatPlain(engine.State.GoldenCookiesClicked),4}");
            }
        }

        if (autoPlay) BuyGreedily(engine);
    }

    /// <summary>贪心策略：先买得起的升级（贵的优先），再买最贵的买得起的建筑。</summary>
    private static void BuyGreedily(GameEngine engine)
    {
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.BuyMax);

        for (int i = snapshot.Upgrades.Count - 1; i >= 0; i--)
        {
            UpgradeView upgrade = snapshot.Upgrades[i];
            if (!upgrade.IsAvailable || !upgrade.CanAfford) continue;
            if (upgrade.Currency != UpgradeCurrency.Cookies) continue;
            engine.BuyUpgrade(upgrade.Id);
        }

        for (int i = snapshot.Buildings.Count - 1; i >= 0; i--)
        {
            BuildingView building = snapshot.Buildings[i];
            if (!building.IsUnlocked) continue;
            if (engine.BuyBuilding(building.Id, 0).Success) break;
        }
    }

    private static void PrintReport(GameSession session, double wallSeconds)
    {
        GameEngine engine = session.Engine;
        GameState state = engine.State;
        GameSnapshot snap = session.Snapshot;

        Section("总览");
        Field("游戏内时长", NumFormat.Duration(state.PlayTimeSeconds));
        Field("实际计算耗时", $"{wallSeconds:F2} 秒（约 {state.PlayTimeSeconds / Math.Max(0.001, wallSeconds):F0}× 实时）");
        Field("模拟逻辑帧", $"{state.TickCount:N0}");
        Field("货币", $"{snap.CookiesText} {snap.CurrencyName}");
        Field("每秒产量", $"{snap.CpsText}/s");
        Field("点击收益", snap.ClickPowerText);
        Field("本轮累计赚取", NumFormat.FormatLong(state.CookiesEarnedThisRun));
        Field("历史累计赚取", NumFormat.FormatLong(state.CookiesEarnedAllTime));
        Field("手动点击", $"{NumFormat.FormatPlain(state.TotalClicks)} 次，共 {NumFormat.FormatLong(state.HandMadeCookies)}");

        Section("建筑");
        foreach (BuildingView building in snap.Buildings)
        {
            if (building.Owned == 0 && !building.IsUnlocked) continue;
            Console.WriteLine(
                $"  {(building.IsUnlocked ? building.Icon : "🔒")} {Ansi.PadRight(building.Name, 14)}" +
                $" ×{building.Owned,-5}" +
                $" 单价 {Ansi.PadLeft(NumFormat.Format(building.UnitPrice, NumberStyle.Short), 10)}" +
                $" 产量 {Ansi.PadLeft(NumFormat.Format(building.CpsContribution, NumberStyle.Short), 10)}/s" +
                $" 占 {NumFormat.Percent(building.CpsShare, 1),6}");
        }

        Section($"升级 / 成就 / {session.Package.GoldenCookieName}");
        Field("已购升级", $"{state.UpgradeCounts.Count} 种");
        Field("成就", $"{state.Achievements.Count}/{snap.AchievementTotal}");
        Field(
            session.Package.GoldenCookieName,
            $"点中 {NumFormat.FormatPlain(state.GoldenCookiesClicked)} 只，场上还剩 {state.GoldenCookies.Count} 只");
        Field("生效增益", state.Buffs.Count == 0
            ? "无"
            : string.Join("、", state.Buffs.Select(b => $"{b.Id} {NumFormat.Duration(b.RemainingSeconds)}")));

        Section(session.Package.PrestigeActionName);
        Field("当前等级", snap.PrestigeLevel.ToString());
        Field(engine.Content.PrestigeCurrencyName, NumFormat.FormatPlain(state.PrestigeChips));
        Field($"若现在{session.Package.PrestigeActionName}", $"{snap.Prestige.NextLevel} 级（+{NumFormat.FormatPlain(snap.Prestige.ChipsOnAscend)} {engine.Content.PrestigeCurrencyName}）");
        Field("下一级所需", NumFormat.FormatLong(snap.Prestige.CookiesForNextLevel));
        Field($"{session.Package.PrestigeActionName}次数", state.Ascensions.ToString());

        Section("最近消息");
        foreach (GameNotification notification in session.LogLines.TakeLast(8))
            Console.WriteLine($"  {notification.Icon} {notification.Message}");

        if (engine.Content.Modules.Count > 0)
        {
            Section("模块");
            foreach (string counterKey in state.Counters.Keys.OrderBy(k => k, StringComparer.Ordinal))
                Field(counterKey, NumFormat.FormatPlain(state.Counters[counterKey]));
        }

        Console.WriteLine();
        Console.WriteLine(session.Package.ReportTip);
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"── {title} " + new string('─', Math.Max(0, 46 - Ansi.DisplayWidth(title))));
    }

    private static void Field(string label, string value)
        => Console.WriteLine($"  {Ansi.PadRight(label, 16)} {value}");
}
