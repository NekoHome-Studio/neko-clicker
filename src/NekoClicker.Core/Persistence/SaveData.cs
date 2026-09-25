using System.Text.Json.Nodes;

namespace NekoClicker.Core.Persistence;

/// <summary>
/// 存档的数据传输对象。<para>
/// 刻意与 <see cref="GameState"/> 分离：状态类可以自由重构内部结构，而存档格式
/// 一旦发布就必须向后兼容。所有字段都是可空/有默认值的简单类型，因此旧存档缺字段时
/// 反序列化不会失败，而是拿到默认值。
/// </para>
/// </summary>
public sealed class SaveData
{
    /// <summary>存档格式版本。</summary>
    public int Version { get; set; } = SaveSerializer.CurrentVersion;

    // ---- 货币与统计 ----
    /// <summary>当前存量。</summary>
    public double Cookies { get; set; }

    /// <summary>本轮累计赚取。</summary>
    public double CookiesEarnedThisRun { get; set; }

    /// <summary>历史累计赚取。</summary>
    public double CookiesEarnedAllTime { get; set; }

    /// <summary>点击累计赚取。</summary>
    public double HandMadeCookies { get; set; }

    /// <summary>累计点击次数。</summary>
    public double TotalClicks { get; set; }

    /// <summary>累计点中金猫次数。</summary>
    public double GoldenCookiesClicked { get; set; }

    // ---- 转生 ----
    /// <summary>转生等级。</summary>
    public int PrestigeLevel { get; set; }

    /// <summary>持有转生货币。</summary>
    public double PrestigeChips { get; set; }

    /// <summary>已花费转生货币。</summary>
    public double PrestigeChipsSpent { get; set; }

    /// <summary>转生次数。</summary>
    public int Ascensions { get; set; }

    // ---- 时间 ----
    /// <summary>建档时刻。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>上次存档时刻。</summary>
    public DateTimeOffset LastSavedAt { get; set; }

    /// <summary>累计游玩秒数。</summary>
    public double PlayTimeSeconds { get; set; }

    /// <summary>累计逻辑帧数。</summary>
    public long TickCount { get; set; }

    // ---- 纪元（转生分层） ----
    /// <summary>当前纪元层号；没有分层的包恒为 1。</summary>
    public int Era { get; set; } = 1;

    /// <summary>已完成（已舍命离开）的层号。</summary>
    public List<int> EraCompleted { get; set; } = [];

    /// <summary>各层的完成记录。</summary>
    public Dictionary<int, EraRecord> EraHistory { get; set; } = [];

    /// <summary>进入当前层时的累计游玩秒数。</summary>
    public double EraEnteredPlayTimeSeconds { get; set; }

    // ---- 金猫 ----
    /// <summary>下一次金猫倒计时。</summary>
    public double GoldenCookieCountdown { get; set; }

    /// <summary>是否已出现过金猫。</summary>
    public bool GoldenCookieIntroduced { get; set; }

    // ---- 随机数 ----
    /// <summary>PRNG 状态字 0。</summary>
    public ulong RandomState0 { get; set; }

    /// <summary>PRNG 状态字 1。</summary>
    public ulong RandomState1 { get; set; }

    // ---- 持有 ----
    /// <summary>建筑数量。</summary>
    public Dictionary<string, int> Buildings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>升级购买次数。</summary>
    public Dictionary<string, int> Upgrades { get; set; } = new(StringComparer.Ordinal);

    /// <summary>已解锁成就。</summary>
    public List<string> Achievements { get; set; } = [];

    /// <summary>生效中的增益。</summary>
    public List<BuffSave> Buffs { get; set; } = [];

    /// <summary>场上的金猫。</summary>
    public List<GoldenCookieSave> GoldenCookies { get; set; } = [];

    /// <summary>自定义计数器。</summary>
    public Dictionary<string, double> Counters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>自定义元数据。</summary>
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>增益的存档表示。</summary>
public sealed class BuffSave
{
    /// <summary>增益 id。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>剩余秒数。</summary>
    public double RemainingSeconds { get; set; }

    /// <summary>总时长。</summary>
    public double TotalSeconds { get; set; }

    /// <summary>层数。</summary>
    public int Stacks { get; set; } = 1;
}

/// <summary>金猫的存档表示。</summary>
public sealed class GoldenCookieSave
{
    /// <summary>实例 id。</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>剩余停留秒数。</summary>
    public double RemainingSeconds { get; set; }

    /// <summary>总停留秒数。</summary>
    public double LifetimeSeconds { get; set; }

    /// <summary>归一化横坐标。</summary>
    public double X { get; set; }

    /// <summary>归一化纵坐标。</summary>
    public double Y { get; set; }

    /// <summary>强制结果 id。</summary>
    public string? ForcedOutcomeId { get; set; }
}

/// <summary>
/// 存档版本迁移。<para>
/// 发布过存档格式的游戏一定会遇到"老存档打不开"的问题。约定：每个迁移负责把
/// <c>FromVersion</c> 的存档升级到 <c>FromVersion + 1</c>，直接改 JSON 树，
/// 由 <see cref="SaveSerializer"/> 负责串起来并在最后写入新版本号。
/// </para>
/// </summary>
public interface ISaveMigration
{
    /// <summary>适用于哪个版本（该迁移把此版本升级到下一版）。</summary>
    int FromVersion { get; }

    /// <summary>迁移描述（诊断用）。</summary>
    string Description { get; }

    /// <summary>就地修改存档 JSON。</summary>
    /// <param name="save">存档根对象。</param>
    void Apply(JsonObject save);
}
