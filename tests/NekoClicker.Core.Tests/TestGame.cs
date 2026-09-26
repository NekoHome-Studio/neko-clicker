using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Content.Apocalypse;
using NekoClicker.Content.Cafe;
using NekoClicker.Content.Civ;
using NekoClicker.Content.Cyber;
using NekoClicker.Content.God;
using NekoClicker.Content.Library;
using NekoClicker.Content.Company;
using NekoClicker.Content.Neko;
using NekoClicker.Content.NineLives;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>测试用的引擎工厂。</summary>
public static class TestGame
{
    private static readonly Lazy<GameContent> NekoContentCache = new(NekoClicker.Content.Neko.NekoContent.Build);
    private static readonly Lazy<GameContent> CafeContentCache = new(NekoClicker.Content.Cafe.CafeContent.Build);
    private static readonly Lazy<GameContent> NineLivesContentCache = new(NineLivesContent.Build);
    private static readonly Lazy<GameContent> LabContentCache = new(NekoClicker.Content.Lab.LabContent.Build);
    private static readonly Lazy<GameContent> CompanyContentCache = new(CompanyContent.Build);
    private static readonly Lazy<GameContent> ApocalypseContentCache = new(ApocalypseContent.Build);
    private static readonly Lazy<GameContent> LibraryContentCache = new(LibraryContent.Build);
    private static readonly Lazy<GameContent> GodContentCache = new(GodContent.Build);
    private static readonly Lazy<GameContent> CivContentCache = new(CivContent.Build);
    private static readonly Lazy<GameContent> CyberContentCache = new(CyberContent.Build);

    /// <summary>示例内容包（不可变，可安全共享）。</summary>
    public static GameContent NekoContent => NekoContentCache.Value;

    /// <summary>内容包 #1《猫娘咖啡馆》（含幸福感模块；不可变，可安全共享）。</summary>
    public static GameContent CafeContent => CafeContentCache.Value;

    /// <summary>内容包 #2《九命轮回》（九层纪元；不可变，可安全共享）。</summary>
    public static GameContent NineLives => NineLivesContentCache.Value;

    /// <summary>内容包 #3《猫娘实验室》（七批次 + 伦理值 + 道德轴；不可变，可安全共享）。</summary>
    public static GameContent Lab => LabContentCache.Value;

    /// <summary>内容包 #10《猫娘公司》（三轮重组 + 士气 + 劳资轴；不可变，可安全共享）。</summary>
    public static GameContent Company => CompanyContentCache.Value;

    /// <summary>内容包 #6《猫娘末世》（五次重启 + 记忆残片 + 跨转生继承；不可变，可安全共享）。</summary>
    public static GameContent Apocalypse => ApocalypseContentCache.Value;

    /// <summary>创建《猫娘末世》的引擎。</summary>
    public static GameEngine CreateApocalypse(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Apocalypse, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>内容包 #9《猫娘图书馆》（五本书 + 被阅读度 + 虚无化；不可变，可安全共享）。</summary>
    public static GameContent Library => LibraryContentCache.Value;

    /// <summary>创建《猫娘图书馆》的引擎。</summary>
    public static GameEngine CreateLibrary(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Library, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>内容包 #7《猫娘神明》（五套神话体系 + 信仰 + 三个结局；不可变，可安全共享）。</summary>
    public static GameContent God => GodContentCache.Value;

    /// <summary>创建《猫娘神明》的引擎。</summary>
    public static GameEngine CreateGod(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(God, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>内容包 #4《猫娘文明》（五个时代 + 文化 + 三个结局；不可变，可安全共享）。</summary>
    public static GameContent Civ => CivContentCache.Value;

    /// <summary>内容包 #5《赛博猫娘》（五层数字层 + 算力 + 两个结局；不可变，可安全共享）。</summary>
    public static GameContent Cyber => CyberContentCache.Value;

    /// <summary>创建《猫娘文明》的引擎。</summary>
    public static GameEngine CreateCiv(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Civ, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>创建《赛博猫娘》的引擎。</summary>
    public static GameEngine CreateCyber(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Cyber, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>创建《猫娘公司》的引擎。</summary>
    public static GameEngine CreateCompany(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Company, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>创建《猫娘实验室》的引擎。</summary>
    public static GameEngine CreateLab(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(Lab, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>创建《九命轮回》的引擎。</summary>
    public static GameEngine CreateNineLives(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(NineLives, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
        });
    }

    /// <summary>
    /// 创建《九命轮回》的引擎，并把第 1 层的完成条件直接置为达成，
    /// 以便测试"舍命之后发生了什么"而不用真的玩十万小鱼干。
    /// </summary>
    public static GameEngine CreateNineLivesFunded(out ManualClock clock, double era1Earnings = 2e5, ulong seed = 12345)
    {
        GameEngine engine = CreateNineLives(out clock, seed);
        engine.State.Cookies = 1e9;
        engine.State.CookiesEarnedThisRun = era1Earnings;
        engine.MarkDirty();
        return engine;
    }

    /// <summary>
    /// 贪心策略：先买得起的升级（贵的优先），再买最贵的买得起的建筑。<para>
    /// 模拟一个正常玩家：不追求最优，只求推进。长跑测试与"全程可达"机器人共用它。
    /// </para>
    /// </summary>
    public static void BuyGreedily(GameEngine engine)
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

    /// <summary>
    /// 全部内容包（含显示名）。<para>
    /// 给"横扫每一个包"的通用守卫用：这类守卫只写一份、自动覆盖后续新增的包，
    /// 比在每个包的专项用例里各写一遍可靠得多——阶段 4B 的两条真缺陷都是这么抓出来的。
    /// </para>
    /// </summary>
    public static (string Name, GameContent Content)[] AllContentPacks() =>
    [
        ("猫咖物语", NekoContent),
        ("#1 咖啡馆", CafeContent),
        ("#2 九命", NineLives),
        ("#3 实验室", Lab),
        ("#10 公司", Company),
        ("#6 末世", Apocalypse),
        ("#9 图书馆", Library),
        ("#7 神明", God),
        ("#4 文明", Civ),
        ("#5 赛博", Cyber),
    ];

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

    /// <summary>创建使用《猫娘咖啡馆》内容包的引擎，时间由 <see cref="ManualClock"/> 控制。</summary>
    public static GameEngine CreateCafe(out ManualClock clock, ulong seed = 12345, bool grantOffline = true)
    {
        clock = new ManualClock();
        return new GameEngine(CafeContent, new GameEngineOptions
        {
            Clock = clock,
            Seed = seed,
            GrantOfflineProgress = grantOffline,
            MaxNotifications = 64,
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
