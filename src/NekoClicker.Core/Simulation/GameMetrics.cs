using NekoClicker.Core.Content;

namespace NekoClicker.Core;

/// <summary>
/// <see cref="IGameMetrics"/> 的默认实现：把引擎的运行时状态投影成只读视图。<para>
/// 内容层（解锁条件、成长曲线）拿到的是这个对象，因此永远无法通过它改动游戏状态。
/// </para>
/// </summary>
internal sealed class GameMetrics : IGameMetrics
{
    private readonly GameEngine _engine;

    public GameMetrics(GameEngine engine) => _engine = engine;

    private GameState S => _engine.State;

    /// <inheritdoc />
    public double Cookies => S.Cookies;

    /// <inheritdoc />
    // 读缓存值而不是 Production 属性：求解修饰符期间若回调到这里，不能触发重入计算。
    public double CookiesPerSecond => _engine.LastProduction.CookiesPerSecond;

    /// <inheritdoc />
    public double ClickPower => _engine.LastProduction.ClickPower;

    /// <inheritdoc />
    public double CookiesEarnedThisRun => S.CookiesEarnedThisRun;

    /// <inheritdoc />
    public double CookiesEarnedAllTime => S.CookiesEarnedAllTime;

    /// <inheritdoc />
    public double TotalClicks => S.TotalClicks;

    /// <inheritdoc />
    public double HandMadeCookies => S.HandMadeCookies;

    /// <inheritdoc />
    public double GoldenCookiesClicked => S.GoldenCookiesClicked;

    /// <inheritdoc />
    public int PrestigeLevel => S.PrestigeLevel;

    /// <inheritdoc />
    public double PrestigeChips => S.PrestigeChips;

    /// <inheritdoc />
    public int Ascensions => S.Ascensions;

    /// <inheritdoc />
    public double PlayTimeSeconds => S.PlayTimeSeconds;

    /// <inheritdoc />
    public int BuildingCount(string buildingId) => S.BuildingCount(buildingId);

    /// <inheritdoc />
    public double TotalBuildings => S.TotalBuildings();

    /// <inheritdoc />
    public int UpgradeCount(string upgradeId) => S.UpgradeCount(upgradeId);

    /// <inheritdoc />
    public bool HasUpgrade(string upgradeId) => S.UpgradeCount(upgradeId) > 0;

    /// <inheritdoc />
    public int PurchasedUpgradeCount => S.UpgradeCounts.Count;

    /// <inheritdoc />
    public bool HasAchievement(string achievementId) => S.Achievements.Contains(achievementId);

    /// <inheritdoc />
    public int AchievementCount => S.Achievements.Count;

    /// <inheritdoc />
    public int TaggedUpgradeCount(string tag)
    {
        int total = 0;
        foreach (UpgradeDefinition def in _engine.Content.Upgrades)
        {
            if (!def.Tags.Contains(tag, StringComparer.Ordinal)) continue;
            total += S.UpgradeCount(def.Id);
        }
        return total;
    }

    /// <inheritdoc />
    public int TaggedBuildingCount(string tag)
    {
        int total = 0;
        foreach (BuildingDefinition def in _engine.Content.Buildings)
        {
            if (!def.Tags.Contains(tag, StringComparer.Ordinal)) continue;
            total += S.BuildingCount(def.Id);
        }
        return total;
    }

    /// <inheritdoc />
    public double GetCounter(string key) => S.GetCounter(key);
}
