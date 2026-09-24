using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Content.Neko;

namespace NekoClicker.Core.Tests;

/// <summary>测试用的引擎工厂。</summary>
public static class TestGame
{
    private static readonly Lazy<GameContent> NekoContentCache = new(NekoClicker.Content.Neko.NekoContent.Build);

    /// <summary>示例内容包（不可变，可安全共享）。</summary>
    public static GameContent NekoContent => NekoContentCache.Value;

    /// <summary>创建使用示例内容包的引擎，时间由 <see cref="ManualClock"/> 控制。</summary>
    public static GameEngine CreateNeko(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(NekoContent, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 32,
        });
    }

    /// <summary>
    /// 一个刻意做小的内容包，用于精确验证数值语义。<para>
    /// 只含一座建筑（10 元 / 1 产量）和四个升级：两个 +50%、两个 ×2。
    /// </para>
    /// </summary>
    public static GameContent MiniContent() => new GameContentBuilder("Mini")
        .WithCurrency("unit", "u")
        .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
        .Add(new UpgradeDefinition
        {
            Id = "pct1",
            Name = "Pct1",
            Price = 1,
            Modifiers = [Modifier.GlobalPercent(0.5)],
        })
        .Add(new UpgradeDefinition
        {
            Id = "pct2",
            Name = "Pct2",
            Price = 1,
            Modifiers = [Modifier.GlobalPercent(0.5)],
        })
        .Add(new UpgradeDefinition
        {
            Id = "mul1",
            Name = "Mul1",
            Price = 1,
            Modifiers = [Modifier.GlobalMultiplier(2)],
        })
        .Add(new UpgradeDefinition
        {
            Id = "mul2",
            Name = "Mul2",
            Price = 1,
            Modifiers = [Modifier.GlobalMultiplier(2)],
        })
        .Add(new UpgradeDefinition
        {
            Id = "click_flat",
            Name = "ClickFlat",
            Price = 1,
            Modifiers = [Modifier.ClickFlat(5)],
        })
        .Build();

    /// <summary>
    /// 创建"资金充裕"的引擎：把货币与本轮累计赚取都设成大数，
    /// 以便直接测试后期建筑/升级，而不必先真的模拟几小时。
    /// </summary>
    public static GameEngine CreateNekoFunded(
        out ManualClock clock,
        double cookies = 1e12,
        double earnedThisRun = 1e12,
        ulong seed = 12345)
    {
        GameEngine engine = CreateNeko(out clock, seed);
        engine.State.Cookies = cookies;
        engine.State.CookiesEarnedThisRun = earnedThisRun;
        engine.MarkDirty();
        return engine;
    }

    /// <summary>创建使用 <see cref="MiniContent"/> 的引擎。</summary>
    public static GameEngine CreateMini(out ManualClock clock, ulong seed = 7)
    {
        clock = new ManualClock();
        return new GameEngine(MiniContent(), new GameEngineOptions { Clock = clock, Seed = seed });
    }
}
