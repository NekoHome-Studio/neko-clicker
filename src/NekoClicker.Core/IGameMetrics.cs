namespace NekoClicker.Core;

/// <summary>
/// 只读的游戏状态视图，供内容层（解锁条件、成长曲线）查询。<para>
/// 这是内容定义与引擎内部可变状态之间的<b>唯一</b>契约：内容包只能"看"，
/// 不能改，从而保证所有数值改动都经过引擎的购买/事件流程（可回放、可校验）。
/// </para>
/// </summary>
public interface IGameMetrics
{
    /// <summary>当前货币存量。</summary>
    double Cookies { get; }

    /// <summary>当前每秒产量。</summary>
    double CookiesPerSecond { get; }

    /// <summary>本次转生周期内累计赚取的货币。</summary>
    double CookiesEarnedThisRun { get; }

    /// <summary>含历次转生的累计赚取量（转生等级按它计算）。</summary>
    double CookiesEarnedAllTime { get; }

    /// <summary>手动点击一次的收益。</summary>
    double ClickPower { get; }

    /// <summary>累计点击数。</summary>
    double TotalClicks { get; }

    /// <summary>其中来自手动点击的货币量。</summary>
    double HandMadeCookies { get; }

    /// <summary>累计点击金猫次数。</summary>
    double GoldenCookiesClicked { get; }

    /// <summary>转生等级（= 累计应得的猫薄荷数）。</summary>
    int PrestigeLevel { get; }

    /// <summary>当前持有的转生货币。</summary>
    double PrestigeChips { get; }

    /// <summary>转生次数。</summary>
    int Ascensions { get; }

    /// <summary>累计游玩秒数（含离线收益折算）。</summary>
    double PlayTimeSeconds { get; }

    /// <summary>某建筑的持有数量；未解锁/不存在返回 0。</summary>
    int BuildingCount(string buildingId);

    /// <summary>所有建筑数量之和。</summary>
    double TotalBuildings { get; }

    /// <summary>某升级的已购次数。</summary>
    int UpgradeCount(string upgradeId);

    /// <summary>是否已购买某升级（次数 &gt; 0）。</summary>
    bool HasUpgrade(string upgradeId);

    /// <summary>已购买的升级种类数。</summary>
    int PurchasedUpgradeCount { get; }

    /// <summary>是否已解锁某成就。</summary>
    bool HasAchievement(string achievementId);

    /// <summary>已解锁成就数。</summary>
    int AchievementCount { get; }

    /// <summary>带指定标签的升级已购总次数（用于"每 X 个某类升级 +5%"这类成长）。</summary>
    int TaggedUpgradeCount(string tag);

    /// <summary>带指定标签的建筑持有总数。</summary>
    int TaggedBuildingCount(string tag);

    /// <summary>读取自定义统计计数器；不存在返回 0。</summary>
    double GetCounter(string key);

    /// <summary>当前纪元（转生层号）；没有分层的包恒为 1。</summary>
    int Era { get; }
}
