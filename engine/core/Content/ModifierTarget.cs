using NekoClicker.Core;

namespace NekoClicker.Core.Content;

/// <summary>
/// 修饰符的作用目标。<para>
/// 引擎只需要认识这几种目标，就可以把任意数量的升级/成就/增益叠加成一条生产管线，
/// 而不必为每个新升级写特判代码——这是 Cookie Clicker 里 <c>Game.getCps</c> 那段
/// 巨型乘数链的可维护版本。
/// </para>
/// </summary>
public enum ModifierTargetKind
{
    /// <summary>所有建筑的每秒产量。</summary>
    GlobalCps,

    /// <summary>指定建筑的每秒产量（<see cref="ModifierTarget.Id"/> = 建筑 id）。</summary>
    BuildingCps,

    /// <summary>手动点击收益。</summary>
    ClickPower,

    /// <summary>所有建筑的购买价格。</summary>
    GlobalPrice,

    /// <summary>指定建筑的购买价格（<see cref="ModifierTarget.Id"/> = 建筑 id）。</summary>
    BuildingPrice,

    /// <summary>金猫出现频率（乘数越大间隔越短）。</summary>
    GoldenCookieFrequency,

    /// <summary>金猫停留时间。</summary>
    GoldenCookieDuration,

    /// <summary>金猫奖励倍率。</summary>
    GoldenCookieReward,

    /// <summary>离线收益效率。</summary>
    OfflineEfficiency,

    /// <summary>增益持续时间（<see cref="ModifierTarget.Id"/> 为空表示全部增益）。</summary>
    BuffDuration,
}

/// <summary>修饰符目标：种类 + 可选的具体 id。</summary>
/// <param name="Kind">目标种类。</param>
/// <param name="Id">建筑 / 增益 id；为 <c>null</c> 表示"全部"。</param>
public sealed record ModifierTarget(ModifierTargetKind Kind, string? Id = null)
{
    /// <summary>所有建筑产量。</summary>
    public static readonly ModifierTarget GlobalCps = new(ModifierTargetKind.GlobalCps);

    /// <summary>点击收益。</summary>
    public static readonly ModifierTarget ClickPower = new(ModifierTargetKind.ClickPower);

    /// <summary>所有建筑价格。</summary>
    public static readonly ModifierTarget GlobalPrice = new(ModifierTargetKind.GlobalPrice);

    /// <summary>金猫出现频率。</summary>
    public static readonly ModifierTarget GoldenCookieFrequency = new(ModifierTargetKind.GoldenCookieFrequency);

    /// <summary>金猫停留时间。</summary>
    public static readonly ModifierTarget GoldenCookieDuration = new(ModifierTargetKind.GoldenCookieDuration);

    /// <summary>金猫奖励倍率。</summary>
    public static readonly ModifierTarget GoldenCookieReward = new(ModifierTargetKind.GoldenCookieReward);

    /// <summary>离线收益效率。</summary>
    public static readonly ModifierTarget OfflineEfficiency = new(ModifierTargetKind.OfflineEfficiency);

    /// <summary>指定建筑的产量。</summary>
    public static ModifierTarget BuildingCps(string buildingId) => new(ModifierTargetKind.BuildingCps, buildingId);

    /// <summary>指定建筑的价格。</summary>
    public static ModifierTarget BuildingPrice(string buildingId) => new(ModifierTargetKind.BuildingPrice, buildingId);

    /// <summary>增益持续时间；不传 id 表示所有增益。</summary>
    public static ModifierTarget BuffDuration(string? buffId = null) => new(ModifierTargetKind.BuffDuration, buffId);

    /// <summary>生成面向玩家的人类可读名称。</summary>
    public string Describe(GameContent? content = null)
    {
        string subject = Id is not null && content is not null && content.BuildingById.TryGetValue(Id, out BuildingDefinition? b)
            ? b.Name
            : Id ?? "所有建筑";

        return Kind switch
        {
            ModifierTargetKind.GlobalCps => "所有建筑产量",
            ModifierTargetKind.BuildingCps => $"{subject} 产量",
            ModifierTargetKind.ClickPower => "点击收益",
            ModifierTargetKind.GlobalPrice => "所有建筑价格",
            ModifierTargetKind.BuildingPrice => $"{subject} 价格",
            ModifierTargetKind.GoldenCookieFrequency => "金猫出现频率",
            ModifierTargetKind.GoldenCookieDuration => "金猫停留时间",
            ModifierTargetKind.GoldenCookieReward => "金猫奖励",
            ModifierTargetKind.OfflineEfficiency => "离线收益效率",
            ModifierTargetKind.BuffDuration => Id is null ? "增益持续时间" : $"{Id} 持续时间",
            _ => Kind.ToString(),
        };
    }

    /// <inheritdoc />
    public override string ToString() => Kind + (Id is null ? string.Empty : ":" + Id);
}
