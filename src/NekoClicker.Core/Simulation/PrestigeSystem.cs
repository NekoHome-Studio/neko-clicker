using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 转生系统。<para>
/// 原版公式：<c>等级 = floor((历史累计赚取 / 1e12) ^ (1/3))</c>——赚到 1 兆得 1 点，
/// 1 京（1e15）得 10 点。这意味着收益递增但需要指数级的产出，从而把"重开"变成
/// 一个有意义的选择而不是单纯的重复劳动。
/// </para>
/// <para>
/// 转生只重置"本轮"的东西：货币、建筑、非永久升级、增益、场上金猫。
/// 成就、累计统计、转生货币与永久升级都会保留。
/// </para>
/// </summary>
public static class PrestigeSystem
{
    /// <summary>由历史累计赚取量算出应得的转生等级。</summary>
    public static int LevelFor(double allTimeEarned, GameBalance balance)
    {
        if (allTimeEarned <= 0) return 0;

        double divisor = Math.Max(double.Epsilon, balance.PrestigeDivisor);
        double scaled = allTimeEarned / divisor;
        if (scaled <= 0) return 0;

        double raw = Math.Pow(scaled, balance.PrestigeExponent);
        if (double.IsNaN(raw) || raw <= 0) return 0;
        if (raw >= int.MaxValue) return int.MaxValue;

        int level = (int)Math.Floor(raw);

        // Math.Pow(1000, 1.0/3.0) 会算出 9.999999999999998，直接取整会把 10 级错判成 9 级。
        // 用反函数（整数幂，无精度损失）双向校正，最多几步就能收敛。
        int guard = 0;
        while (level > 0 && CookiesForLevel(level, balance) > allTimeEarned && guard++ < 8) level--;
        while (CookiesForLevel(level + 1, balance) <= allTimeEarned && guard++ < 8) level++;

        return level;
    }

    /// <summary>达到指定等级所需的历史累计赚取量（<see cref="LevelFor"/> 的反函数）。</summary>
    public static double CookiesForLevel(int level, GameBalance balance)
    {
        if (level <= 0) return 0;

        double exponent = balance.PrestigeExponent;
        if (exponent <= 0) return double.MaxValue;

        double inverse = 1.0 / exponent;

        // 1.0/(1.0/3.0) 在浮点下是 3.0000000000000004，会让 10^3 变成 1000.0000000000002，
        // 进而破坏 LevelFor 的校正循环。把"接近整数"的指数吸附到整数上。
        double rounded = Math.Round(inverse);
        if (Math.Abs(inverse - rounded) <= 1e-9 * Math.Max(1.0, rounded)) inverse = rounded;

        double scaled = Math.Pow(level, inverse);
        return double.IsFinite(scaled) ? scaled * balance.PrestigeDivisor : double.MaxValue;
    }

    /// <summary>生成转生预览，供 UI 在按钮旁展示。</summary>
    public static PrestigePreview Preview(GameEngine engine)
    {
        GameState state = engine.State;
        GameBalance balance = engine.Balance;

        int current = state.PrestigeLevel;
        int next = LevelFor(state.CookiesEarnedAllTime, balance);
        if (next < current) next = current;

        double chips = Math.Max(0, next - current) * balance.PrestigeChipsPerLevel;

        double requiredForNext = CookiesForLevel(next + 1, balance);
        double requiredForCurrent = CookiesForLevel(next, balance);
        double span = requiredForNext - requiredForCurrent;
        double progress = span > 0
            ? Math.Clamp((state.CookiesEarnedAllTime - requiredForCurrent) / span, 0, 1)
            : 0;

        return new PrestigePreview(current, next, chips, requiredForNext, progress, next > current);
    }

    /// <summary>执行转生。</summary>
    public static AscensionResult Ascend(GameEngine engine)
    {
        GameState state = engine.State;
        GameBalance balance = engine.Balance;

        int previous = state.PrestigeLevel;
        int next = LevelFor(state.CookiesEarnedAllTime, balance);

        if (next <= previous)
        {
            double needed = CookiesForLevel(previous + 1, balance);
            return AscensionResult.Fail(
                $"历史累计赚取不足：需要 {NumFormat.FormatLong(needed)} 才能把转生等级提升到 {previous + 1}。");
        }

        double gained = (next - previous) * balance.PrestigeChipsPerLevel;

        state.PrestigeLevel = next;
        state.PrestigeChips += gained;
        state.Ascensions++;

        ResetRun(engine, balance.KeepAchievementsOnAscend);
        engine.MarkDirty();
        engine.Events.Publish(new AscendedEvent(previous, next, gained, state.Ascensions));
        engine.Notify(
            $"转生完成！等级 {previous} → {next}，获得 {NumFormat.FormatLong(gained)} {engine.Content.PrestigeCurrencyName}。",
            NotificationKind.Rare,
            engine.Content.PrestigeCurrencyIcon);

        foreach (IGameModule module in engine.Modules) module.OnAscend(engine);

        return new AscensionResult
        {
            Success = true,
            Message = $"转生完成：等级 {next}，+{NumFormat.FormatLong(gained)} {engine.Content.PrestigeCurrencyName}。",
            PreviousLevel = previous,
            NewLevel = next,
            ChipsGained = gained,
            Ascensions = state.Ascensions,
        };
    }

    /// <summary>
    /// 重置"本轮"进度。永久升级（<see cref="UpgradePersistence.Permanent"/>）与转生货币不受影响。
    /// </summary>
    public static void ResetRun(GameEngine engine, bool keepAchievements)
    {
        GameState state = engine.State;

        state.Cookies = 0;
        state.CookiesEarnedThisRun = 0;
        state.BuildingCounts.Clear();
        state.Buffs.Clear();
        state.GoldenCookies.Clear();

        List<string>? toRemove = null;
        foreach (string id in state.UpgradeCounts.Keys)
        {
            bool keep = engine.Content.UpgradeById.TryGetValue(id, out UpgradeDefinition? def)
                        && def.Persistence == UpgradePersistence.Permanent;
            if (!keep) (toRemove ??= []).Add(id);
        }
        if (toRemove is not null)
            foreach (string id in toRemove) state.UpgradeCounts.Remove(id);

        if (!keepAchievements) state.Achievements.Clear();

        GoldenCookieSystem.ResetSchedule(engine);
    }
}
