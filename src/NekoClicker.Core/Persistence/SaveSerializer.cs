using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Persistence;

/// <summary>
/// 存档序列化。<para>
/// 使用 <c>System.Text.Json</c>（BCL 自带，无第三方依赖）直接序列化 <see cref="SaveData"/>。
/// 版本迁移在反序列化<b>之前</b>对 JSON 树执行，因此迁移代码不需要认识旧的 C# 类型——
/// 这正是"删掉一个字段后老存档还能读"的关键。
/// </para>
/// </summary>
public static class SaveSerializer
{
    /// <summary>当前存档格式版本。</summary>
    public const int CurrentVersion = 1;

    /// <summary>可扩展的迁移注册表（游戏可自行 <c>Add</c>）。</summary>
    public static List<ISaveMigration> Migrations { get; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>把引擎状态序列化为 JSON 字符串。</summary>
    public static string Serialize(GameEngine engine) => JsonSerializer.Serialize(Capture(engine), JsonOptions);

    /// <summary>把引擎状态序列化为 base64 字符串（便于复制粘贴分享）。</summary>
    public static string SerializeToShareCode(GameEngine engine)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(engine)));

    /// <summary>从引擎抓取存档数据。</summary>
    public static SaveData Capture(GameEngine engine)
    {
        GameState state = engine.State;
        return new SaveData
        {
            Version = CurrentVersion,
            Cookies = state.Cookies,
            CookiesEarnedThisRun = state.CookiesEarnedThisRun,
            CookiesEarnedAllTime = state.CookiesEarnedAllTime,
            HandMadeCookies = state.HandMadeCookies,
            TotalClicks = state.TotalClicks,
            GoldenCookiesClicked = state.GoldenCookiesClicked,
            PrestigeLevel = state.PrestigeLevel,
            PrestigeChips = state.PrestigeChips,
            PrestigeChipsSpent = state.PrestigeChipsSpent,
            Ascensions = state.Ascensions,
            CreatedAt = state.CreatedAt,
            LastSavedAt = state.LastSavedAt,
            PlayTimeSeconds = state.PlayTimeSeconds,
            TickCount = state.TickCount,
            GoldenCookieCountdown = state.GoldenCookieCountdown,
            GoldenCookieIntroduced = state.GoldenCookieIntroduced,
            RandomState0 = state.RandomState0,
            RandomState1 = state.RandomState1,
            Buildings = new Dictionary<string, int>(state.BuildingCounts, StringComparer.Ordinal),
            Upgrades = new Dictionary<string, int>(state.UpgradeCounts, StringComparer.Ordinal),
            Achievements = [.. state.Achievements],
            Buffs = state.Buffs.Select(b => new BuffSave
            {
                Id = b.Id,
                RemainingSeconds = b.RemainingSeconds,
                TotalSeconds = b.TotalSeconds,
                Stacks = b.Stacks,
            }).ToList(),
            GoldenCookies = state.GoldenCookies.Select(g => new GoldenCookieSave
            {
                InstanceId = g.InstanceId,
                RemainingSeconds = g.RemainingSeconds,
                LifetimeSeconds = g.LifetimeSeconds,
                X = g.X,
                Y = g.Y,
                ForcedOutcomeId = g.ForcedOutcomeId,
            }).ToList(),
            Counters = new Dictionary<string, double>(state.Counters, StringComparer.Ordinal),
            Metadata = new Dictionary<string, string>(state.Metadata, StringComparer.Ordinal),
        };
    }

    /// <summary>把存档数据写进引擎（不结算离线收益）。</summary>
    public static void Apply(GameEngine engine, SaveData data)
    {
        var state = new GameState
        {
            Cookies = data.Cookies,
            CookiesEarnedThisRun = data.CookiesEarnedThisRun,
            CookiesEarnedAllTime = data.CookiesEarnedAllTime,
            HandMadeCookies = data.HandMadeCookies,
            TotalClicks = data.TotalClicks,
            GoldenCookiesClicked = data.GoldenCookiesClicked,
            PrestigeLevel = data.PrestigeLevel,
            PrestigeChips = data.PrestigeChips,
            PrestigeChipsSpent = data.PrestigeChipsSpent,
            Ascensions = data.Ascensions,
            CreatedAt = data.CreatedAt == default ? engine.Clock.UtcNow : data.CreatedAt,
            LastSavedAt = data.LastSavedAt,
            PlayTimeSeconds = data.PlayTimeSeconds,
            TickCount = data.TickCount,
            GoldenCookieCountdown = data.GoldenCookieCountdown,
            GoldenCookieIntroduced = data.GoldenCookieIntroduced,
            RandomState0 = data.RandomState0,
            RandomState1 = data.RandomState1,
        };

        foreach ((string id, int count) in data.Buildings) state.BuildingCounts[id] = count;
        foreach ((string id, int count) in data.Upgrades) state.UpgradeCounts[id] = count;
        foreach (string id in data.Achievements) state.Achievements.Add(id);
        foreach ((string key, double value) in data.Counters) state.Counters[key] = value;
        foreach ((string key, string value) in data.Metadata) state.Metadata[key] = value;

        foreach (BuffSave buff in data.Buffs)
        {
            if (buff.RemainingSeconds <= 0) continue; // 存档期间已过期
            state.Buffs.Add(new ActiveBuff
            {
                Id = buff.Id,
                RemainingSeconds = buff.RemainingSeconds,
                TotalSeconds = buff.TotalSeconds <= 0 ? buff.RemainingSeconds : buff.TotalSeconds,
                Stacks = Math.Max(1, buff.Stacks),
            });
        }

        foreach (GoldenCookieSave spawn in data.GoldenCookies)
        {
            if (spawn.RemainingSeconds <= 0) continue;
            state.GoldenCookies.Add(new GoldenCookieSpawn
            {
                InstanceId = spawn.InstanceId,
                RemainingSeconds = spawn.RemainingSeconds,
                LifetimeSeconds = spawn.LifetimeSeconds <= 0 ? spawn.RemainingSeconds : spawn.LifetimeSeconds,
                X = spawn.X,
                Y = spawn.Y,
                ForcedOutcomeId = spawn.ForcedOutcomeId,
            });
        }

        engine.ReplaceState(state);
    }

    /// <summary>
    /// 反序列化并应用到引擎，同时结算离线收益。
    /// </summary>
    /// <param name="engine">目标引擎。</param>
    /// <param name="json">JSON 或 base64 存档字符串。</param>
    /// <param name="migrations">自定义迁移；为 <c>null</c> 时使用 <see cref="Migrations"/>。</param>
    /// <returns>离线收益明细。</returns>
    /// <exception cref="InvalidDataException">存档损坏或来自更新的游戏版本。</exception>
    public static OfflineProgress? DeserializeInto(
        GameEngine engine,
        string json,
        IReadOnlyList<ISaveMigration>? migrations = null)
    {
        SaveData data = Parse(json, migrations);
        Apply(engine, data);

        // 以存档里的"上次保存时刻"为基准计算离线时长；时钟回拨时不做任何补发。
        TimeSpan elapsed = engine.Clock.UtcNow - data.LastSavedAt;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        OfflineProgress? offline = engine.ApplyOfflineProgress(elapsed);

        if (offline is { CookiesGained: > 0 })
        {
            engine.Notify(
                $"离线 {Numbers.NumFormat.Duration(offline.Value.CreditedSeconds)}，" +
                $"猫猫们替你赚了 {Numbers.NumFormat.FormatLong(offline.Value.CookiesGained)}！",
                NotificationKind.Rare,
                engine.Content.CurrencyIcon);
        }

        return offline;
    }

    /// <summary>解析（含 base64 自动识别与版本迁移），但不改动引擎。</summary>
    public static SaveData Parse(string json, IReadOnlyList<ISaveMigration>? migrations = null)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("存档为空。");

        string text = Normalize(json);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("存档不是合法的 JSON。", ex);
        }

        if (node is not JsonObject root) throw new InvalidDataException("存档根节点必须是 JSON 对象。");

        int version = root["Version"]?.GetValue<int>() ?? 0;
        if (version > CurrentVersion)
            throw new InvalidDataException(
                $"存档版本 {version} 高于当前游戏支持的 {CurrentVersion}，请更新游戏后再读取。");

        IReadOnlyList<ISaveMigration> list = migrations ?? Migrations;
        foreach (ISaveMigration migration in list.OrderBy(m => m.FromVersion))
        {
            if (version != migration.FromVersion) continue;
            migration.Apply(root);
            version++;
            root["Version"] = version;
        }

        SaveData? data = root.Deserialize<SaveData>(JsonOptions);
        if (data is null) throw new InvalidDataException("存档反序列化失败。");

        // 集合属性在 JSON 中为 null 时兜底，避免后续到处判空。
        data.Buildings ??= new Dictionary<string, int>(StringComparer.Ordinal);
        data.Upgrades ??= new Dictionary<string, int>(StringComparer.Ordinal);
        data.Achievements ??= [];
        data.Buffs ??= [];
        data.GoldenCookies ??= [];
        data.Counters ??= new Dictionary<string, double>(StringComparer.Ordinal);
        data.Metadata ??= new Dictionary<string, string>(StringComparer.Ordinal);

        return data;
    }

    private static string Normalize(string json)
    {
        string trimmed = json.Trim();
        if (trimmed.StartsWith('{')) return trimmed;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("存档既不是 JSON 也不是合法的 base64。", ex);
        }
    }
}
