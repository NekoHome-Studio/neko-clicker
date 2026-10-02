using System.Text.Json.Nodes;
using NekoClicker.Core.Events;
using NekoClicker.Core.Persistence;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>存档序列化、版本迁移、离线收益与自动存档。</summary>
public static class SaveTests
{
    [Test]
    public static void SaveLoad_RoundTripsCoreState()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        for (int i = 0; i < 12; i++) engine.Click();
        engine.BuyBuilding("curled_cat", 25);
        engine.BuyBuilding("cat_bed", 5);
        engine.BuyUpgrade("warmer_hands");
        engine.ApplyBuff("frenzy", 30);

        string json = engine.Save();

        GameEngine restored = TestGame.CreateNeko(out _);
        restored.Load(json);

        Check.CloseRelative(engine.State.Cookies, restored.State.Cookies, 1e-12);
        Check.Equal(engine.State.BuildingCount("curled_cat"), restored.State.BuildingCount("curled_cat"));
        Check.Equal(engine.State.BuildingCount("cat_bed"), restored.State.BuildingCount("cat_bed"));
        Check.Equal(engine.State.UpgradeCount("warmer_hands"), restored.State.UpgradeCount("warmer_hands"));
        Check.Equal(engine.State.Achievements.Count, restored.State.Achievements.Count);
        Check.CloseRelative(engine.CookiesPerSecond, restored.CookiesPerSecond, 1e-12);
        Check.CloseRelative(engine.ClickPower, restored.ClickPower, 1e-12);
        Check.Equal(1, restored.State.Buffs.Count);
    }

    [Test]
    public static void SaveLoad_PreservesPrestigeAndRng()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.PrestigeLevel = 7;
        engine.State.PrestigeChips = 12.5;
        engine.State.CookiesEarnedAllTime = 3.43e14;
        engine.State.Ascensions = 4;
        engine.MarkDirty();
        engine.SpawnGoldenCookie();

        string json = engine.Save();
        GameEngine restored = TestGame.CreateNeko(out _);
        restored.Load(json);

        Check.Equal(7, restored.State.PrestigeLevel);
        Check.Close(12.5, restored.State.PrestigeChips);
        Check.Equal(4, restored.State.Ascensions);
        Check.Close(3.43e14, restored.State.CookiesEarnedAllTime, 1);
        Check.Equal(engine.State.RandomState0, restored.State.RandomState0);
        Check.Equal(engine.State.RandomState1, restored.State.RandomState1);
    }

    [Test]
    public static void OfflineProgress_IsCappedAndCredited()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);

        double cps = engine.CookiesPerSecond;
        Check.Greater(cps, 0);

        string json = engine.Save();
        double cookiesAtSave = engine.State.Cookies;

        clock.Advance(4 * 3600); // 离线 4 小时，上限 3 小时
        OfflineProgress? maybeOffline = engine.Load(json);

        Check.True(maybeOffline.HasValue, "4 小时离线应结算收益。");
        if (maybeOffline is not { } offline) return;

        Check.Close(4 * 3600, offline.ElapsedSeconds, 1e-6);
        Check.Close(3 * 3600, offline.CreditedSeconds, 1e-6);
        Check.True(offline.WasCapped);
        Check.CloseRelative(cps * 3 * 3600, offline.CookiesGained, 1e-9);
        Check.CloseRelative(cookiesAtSave + (cps * 3 * 3600), engine.State.Cookies, 1e-9);
    }

    [Test]
    public static void OfflineProgress_IgnoredForShortAbsence()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);
        string json = engine.Save();

        clock.Advance(10); // 低于 MinimumOfflineSeconds

        Check.False(engine.Load(json).HasValue);
    }

    [Test]
    public static void OfflineProgress_IgnoresExpiredBuffs()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);
        double baseCps = engine.CookiesPerSecond;

        engine.ApplyBuff("frenzy", 600); // 带增益时保存
        Check.CloseRelative(baseCps * 7, engine.CookiesPerSecond, 1e-9);

        string json = engine.Save();
        clock.Advance(2 * 3600);
        OfflineProgress? maybeOffline = engine.Load(json);

        Check.True(maybeOffline.HasValue);
        if (maybeOffline is not { } offline) return;

        // 离线产量必须按"无增益"结算，否则会显著高估。
        Check.CloseRelative(baseCps * 2 * 3600, offline.CookiesGained, 1e-9);
    }

    [Test]
    public static void OfflineProgress_CanBeDisabledByOption()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock, grantOffline: false);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 4);
        string json = engine.Save();

        clock.Advance(4 * 3600);

        Check.False(engine.Load(json).HasValue);
    }

    /// <summary>
    /// 离线补发要能走到 UI：读档之后快照里带着它，宿主播报完（<c>DismissOfflineProgress</c>）
    /// 快照里就没有了。<para>
    /// 这一对"出现 → 消失"是本轮新增的语义（见 <c>GameSnapshot.Offline</c>）：少了前半段，
    /// 玩家永远看不到自己离线赚了多少；少了后半段，刷新一次页面就重复弹一次。
    /// </para>
    /// </summary>
    [Test]
    public static void OfflineReport_ReachesTheSnapshotUntilDismissed()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);

        Check.Null(engine.Snapshot().Offline, "新开局不该有待播报的离线收益。");

        string json = engine.Save();
        clock.Advance(4 * 3600);
        OfflineProgress? recorded = engine.Load(json);
        Check.True(recorded.HasValue, "4 小时离线应结算收益。");

        OfflineView? view = engine.Snapshot().Offline;
        Check.NotNull(view, "补发了收益就该有待播报的离线报告。");
        if (view is null || recorded is not { } offline) return;

        Check.Close(offline.ElapsedSeconds, view.ElapsedSeconds, 1e-9);
        Check.Close(offline.CreditedSeconds, view.CreditedSeconds, 1e-9);
        Check.Close(offline.CookiesGained, view.CookiesGained, 1e-9);
        Check.True(view.WasCapped, "4 小时超过 3 小时上限，应标记被截断。");
        Check.False(string.IsNullOrWhiteSpace(view.DurationText), "时长文本不该为空。");
        Check.False(string.IsNullOrWhiteSpace(view.CookiesText), "货币文本不该为空。");

        // 报告在"播报"之前一直有效：刷新页面（= 再取一次快照）不该让它消失，
        // 否则玩家一刷新就再也看不到这笔钱是怎么来的。
        Check.NotNull(engine.Snapshot().Offline, "没播报过的报告不该自己过期。");

        Check.True(engine.DismissOfflineProgress(), "第一次播报应报告 true。");
        Check.Null(engine.Snapshot().Offline, "播报之后快照里不该再有它。");
        Check.False(engine.DismissOfflineProgress(), "重复播报应报告 false（幂等，不是错误）。");
    }

    /// <summary>离线时长不到门槛时既没有补发、也没有报告——不然会弹一张"赚了 0"的卡。</summary>
    [Test]
    public static void OfflineReport_NotRaisedForShortAbsence()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 1);
        string json = engine.Save();

        clock.Advance(10); // 低于 MinimumOfflineSeconds

        Check.Null(engine.Load(json));
        Check.Null(engine.Snapshot().Offline, "没补发就不该有待播报的报告。");
    }

    /// <summary>重开一局时，上一局还没播报的离线收益要一起丢掉（那笔钱不属于这一局）。</summary>
    [Test]
    public static void OfflineReport_ClearedByHardReset()
    {
        GameEngine engine = TestGame.CreateNeko(out ManualClock clock);
        engine.State.Cookies = 1_000_000;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 10);
        string json = engine.Save();

        clock.Advance(4 * 3600);
        engine.Load(json);
        Check.NotNull(engine.Snapshot().Offline, "前置条件：读档后应有一份待播报的报告。");

        engine.HardReset();

        Check.Null(engine.Snapshot().Offline, "重开之后不该再播报上一局的离线收益。");
    }

    [Test]
    public static void ShareCode_RoundTripsThroughBase64()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 12_345;
        engine.MarkDirty();
        engine.BuyBuilding("curled_cat", 2);

        string code = SaveSerializer.SerializeToShareCode(engine);
        Check.False(code.StartsWith('{'), "分享码应是 base64 而不是裸 JSON。");

        SaveData data = SaveSerializer.Parse(code);
        Check.Close(12_345 - 15 - 17.25, data.Cookies, 1e-9);
        Check.Equal(2, data.Buildings["curled_cat"]);
    }

    [Test]
    public static void Parse_RejectsCorruptOrFutureSaves()
    {
        Check.Throws<InvalidDataException>(() => SaveSerializer.Parse(string.Empty));
        Check.Throws<InvalidDataException>(() => SaveSerializer.Parse("这不是 json，也不是 base64!!!"));
        Check.Throws<InvalidDataException>(() => SaveSerializer.Parse("""{"Version": 999}"""));
    }

    [Test]
    public static void Parse_ToleratesMissingFields()
    {
        // 只写了少数键的老存档：其余字段应取默认值而不是抛异常。
        SaveData data = SaveSerializer.Parse("""{"Version":1,"Cookies":42}""");

        Check.Close(42, data.Cookies);
        Check.Equal(0, data.Buildings.Count);
        Check.Equal(0, data.Achievements.Count);
        Check.NotNull(data.Counters);
    }

    [Test]
    public static void Migration_UpgradesLegacySaves()
    {
        SaveSerializer.Migrations.Add(new LegacyV0Migration());
        try
        {
            SaveData data = SaveSerializer.Parse("""{"Cookies":42,"Buildings":{"curled_cat":3}}""");

            Check.Equal(SaveSerializer.CurrentVersion, data.Version);
            Check.Close(1_042, data.Cookies, 1e-9, "迁移应已执行。");
            Check.Equal(3, data.Buildings["curled_cat"]);
        }
        finally
        {
            SaveSerializer.Migrations.Clear();
        }
    }

    [Test]
    public static void SaveManager_AutoSavesOnInterval()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        var storage = new MemoryStorage();
        using var manager = new SaveManager(engine, storage, "slot.json", autoSaveIntervalSeconds: 60);

        Check.False(manager.HasSave());

        engine.Simulate(30);
        Check.False(manager.HasSave(), "未到间隔不应存档。");

        engine.Simulate(31);
        Check.True(manager.HasSave(), "超过间隔应自动存档。");
        Check.True(storage.ListKeys().Contains("slot.json"));
    }

    [Test]
    public static void SaveManager_SaveLoadDelete()
    {
        var storage = new MemoryStorage();
        GameEngine engine = TestGame.CreateNeko(out _);
        using var manager = new SaveManager(engine, storage, "slot.json");

        engine.State.Cookies = 777;
        engine.MarkDirty();
        Check.True(manager.Save());

        GameEngine other = TestGame.CreateNeko(out _);
        using var otherManager = new SaveManager(other, storage, "slot.json");
        Check.True(otherManager.Load());
        Check.Close(777, other.State.Cookies, 1e-9);

        otherManager.Delete();
        Check.False(otherManager.HasSave());
    }

    [Test]
    public static void SaveManager_ReportsLoadFailureWithoutCrashing()
    {
        var storage = new MemoryStorage();
        storage.Write("slot.json", "{ 这不是合法 JSON");

        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 5;
        engine.MarkDirty();
        using var manager = new SaveManager(engine, storage, "slot.json");

        Check.False(manager.Load());
        Check.NotNull(manager.LastError);
        Check.Close(5, engine.State.Cookies, 1e-9, "读档失败不应影响当前进度。");
    }

    [Test]
    public static void SaveManager_RaisesSavedEvent()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        var storage = new MemoryStorage();
        using var manager = new SaveManager(engine, storage, "slot.json");

        string? savedKey = null;
        using IDisposable subscription = engine.Events.Subscribe<GameSavedEvent>(e => savedKey = e.Key);

        manager.Save();

        Check.Equal("slot.json", savedKey);
    }

    private sealed class LegacyV0Migration : ISaveMigration
    {
        public int FromVersion => 0;

        public string Description => "测试用：给版本 0 的老玩家补发 1000 货币。";

        public void Apply(JsonObject save)
        {
            double cookies = save["Cookies"]?.GetValue<double>() ?? 0;
            save["Cookies"] = cookies + 1000;
        }
    }
}
