using NekoClicker.Core.Content;
using NekoClicker.Core.Events;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 金猫系统：原版里那个随机刷出、限时点击、给出强力奖励的金色饼干。<para>
/// 它的设计价值在于<b>打断被动挂机</b>：纯放置游戏的参与度会随时间衰减，
/// 定期的随机事件把玩家拉回来看一眼。这里把它做成内容驱动的加权结果表，
/// 引擎只负责计时、抽取与结算，不硬编码任何一种奖励。
/// </para>
/// </summary>
public static class GoldenCookieSystem
{
    /// <summary>把倒计时重置为一个新的随机间隔。</summary>
    public static void ResetSchedule(GameEngine engine)
    {
        engine.State.GoldenCookieCountdown = NextDelay(engine);
    }

    /// <summary>按平衡参数与修饰符算出下一次出现所需的秒数。</summary>
    public static double NextDelay(GameEngine engine)
    {
        GameBalance balance = engine.Balance;
        double min = Math.Max(1.0, balance.GoldenCookieMinDelay);
        double max = Math.Max(min, balance.GoldenCookieMaxDelay);
        double delay = engine.RandomSource.NextDouble(min, max);

        // 第一只金猫提前出现，让新玩家尽早接触到这个机制。
        if (!engine.State.GoldenCookieIntroduced)
            delay *= Math.Clamp(balance.FirstGoldenCookieDelayFactor, 0.01, 1.0);

        double frequency = Math.Max(0.01, engine.Modifiers.Multiplier(ModifierTarget.GoldenCookieFrequency));
        return delay / frequency;
    }

    /// <summary>推进倒计时、清理超时的金猫、按需刷新。</summary>
    public static void Tick(GameEngine engine, double deltaSeconds)
    {
        GameBalance balance = engine.Balance;
        if (!balance.GoldenCookiesEnabled) return;

        GameState state = engine.State;

        for (int i = state.GoldenCookies.Count - 1; i >= 0; i--)
        {
            GoldenCookieSpawn spawn = state.GoldenCookies[i];
            spawn.RemainingSeconds -= deltaSeconds;
            if (spawn.RemainingSeconds > 0) continue;

            state.GoldenCookies.RemoveAt(i);
            engine.Events.Publish(new GoldenCookieExpiredEvent(spawn.InstanceId));
        }

        state.GoldenCookieCountdown -= deltaSeconds;
        if (state.GoldenCookieCountdown > 0) return;

        if (state.GoldenCookies.Count < Math.Max(1, balance.MaxConcurrentGoldenCookies))
            Spawn(engine);

        ResetSchedule(engine);
    }

    /// <summary>立即刷出一只金猫（也用于调试/剧本）。</summary>
    public static GoldenCookieSpawn Spawn(GameEngine engine, string? forcedOutcomeId = null)
    {
        GameState state = engine.State;

        long serial = (long)state.GetCounter("goldenCookieSerial") + 1;
        state.Counters["goldenCookieSerial"] = serial;

        double lifetime = balanceLifetime(engine);

        var spawn = new GoldenCookieSpawn
        {
            InstanceId = "gc-" + serial.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RemainingSeconds = lifetime,
            LifetimeSeconds = lifetime,
            X = engine.RandomSource.NextDouble(0.05, 0.95),
            Y = engine.RandomSource.NextDouble(0.10, 0.90),
            ForcedOutcomeId = forcedOutcomeId,
        };

        state.GoldenCookies.Add(spawn);
        state.GoldenCookieIntroduced = true;

        engine.Events.Publish(new GoldenCookieSpawnedEvent(spawn.InstanceId, spawn.X, spawn.Y, lifetime));
        engine.Notify("一只金猫出现了！快点它！", NotificationKind.Rare, "🌟");
        return spawn;
    }

    /// <summary>点中一只金猫并结算奖励。</summary>
    public static GoldenCookieResult Click(GameEngine engine, string instanceId)
    {
        GameState state = engine.State;

        GoldenCookieSpawn? spawn = null;
        for (int i = 0; i < state.GoldenCookies.Count; i++)
        {
            if (!string.Equals(state.GoldenCookies[i].InstanceId, instanceId, StringComparison.Ordinal)) continue;
            spawn = state.GoldenCookies[i];
            break;
        }
        if (spawn is null) return GoldenCookieResult.Fail("这只金猫已经不在了。");

        IReadOnlyList<GoldenCookieOutcome> outcomes = engine.Content.GoldenCookieOutcomes;
        if (outcomes.Count == 0) return GoldenCookieResult.Fail("内容包没有定义任何金猫结果。");

        GoldenCookieOutcome outcome = ResolveOutcome(engine, outcomes, spawn);

        // ---- 结算 ----
        double cps = engine.CookiesPerSecond;
        double bank = state.Cookies;
        double rewardMultiplier = Math.Max(0, engine.Modifiers.Multiplier(ModifierTarget.GoldenCookieReward));

        double gained = outcome.CookiesFlat + (cps * outcome.CookiesFromCpsSeconds);

        if (outcome.CookiesFromBankFraction > 0 && bank > 0)
        {
            double fromBank = bank * outcome.CookiesFromBankFraction;
            // 上限以"CPS 秒数"表达；未设上限时（+∞）不截断。
            if (!double.IsPositiveInfinity(outcome.CookiesFromBankFractionCapSecondsOfCps))
                fromBank = Math.Min(fromBank, cps * outcome.CookiesFromBankFractionCapSecondsOfCps);
            gained += fromBank;
        }

        gained *= rewardMultiplier;
        double stolen = bank * outcome.StealBankFraction;
        double net = gained - stolen;

        if (net >= 0)
        {
            state.Cookies = Num.SafeAdd(state.Cookies, net);
            state.CookiesEarnedThisRun = Num.SafeAdd(state.CookiesEarnedThisRun, net);
            state.CookiesEarnedAllTime = Num.SafeAdd(state.CookiesEarnedAllTime, net);
        }
        else
        {
            // 被偷走的钱不算"负收入"，只是存量减少（且不会变成负数）。
            state.Cookies = Math.Max(0, Num.SafeAdd(state.Cookies, net));
        }

        // ---- 附带增益 ----
        double buffSeconds = 0;
        if (outcome.BuffId is not null && engine.Content.FindBuff(outcome.BuffId) is { } buffDef)
        {
            buffSeconds = BuffSystem.ScaledDuration(engine.Modifiers, buffDef.Id, outcome.BuffSeconds);
            BuffSystem.Apply(state, engine.Events, buffDef, buffSeconds);
        }
        if (outcome.SecondaryBuffId is not null && engine.Content.FindBuff(outcome.SecondaryBuffId) is { } secondaryDef)
        {
            double secondarySeconds = BuffSystem.ScaledDuration(engine.Modifiers, secondaryDef.Id, outcome.SecondaryBuffSeconds);
            BuffSystem.Apply(state, engine.Events, secondaryDef, secondarySeconds);
        }

        // ---- 收尾 ----
        state.GoldenCookies.Remove(spawn);
        state.GoldenCookiesClicked += 1;
        engine.MarkDirty();

        string message = Describe(outcome, net, buffSeconds);
        engine.Events.Publish(new GoldenCookieClickedEvent(spawn.InstanceId, outcome.Id, outcome.Name, net, outcome.BuffId));
        engine.Notify(message, outcome.IsRare ? NotificationKind.Rare : NotificationKind.Success, outcome.Icon);

        return new GoldenCookieResult
        {
            Success = true,
            Message = message,
            OutcomeId = outcome.Id,
            OutcomeName = outcome.Name,
            Icon = outcome.Icon,
            CookiesGained = net,
            BuffId = outcome.BuffId,
            BuffSeconds = buffSeconds,
        };
    }

    private static GoldenCookieOutcome ResolveOutcome(
        GameEngine engine,
        IReadOnlyList<GoldenCookieOutcome> outcomes,
        GoldenCookieSpawn spawn)
    {
        if (spawn.ForcedOutcomeId is not null)
        {
            foreach (GoldenCookieOutcome candidate in outcomes)
                if (string.Equals(candidate.Id, spawn.ForcedOutcomeId, StringComparison.Ordinal))
                    return candidate;
        }
        return engine.RandomSource.PickWeighted(outcomes, o => o.Weight);
    }

    private static double balanceLifetime(GameEngine engine)
    {
        double multiplier = Math.Max(0.05, engine.Modifiers.Multiplier(ModifierTarget.GoldenCookieDuration));
        return Math.Max(1.0, engine.Balance.GoldenCookieLifetime * multiplier);
    }

    private static string Describe(GoldenCookieOutcome outcome, double net, double buffSeconds)
    {
        string template = string.IsNullOrWhiteSpace(outcome.Description) ? outcome.Name : outcome.Description;
        return template
            .Replace("{amount}", NumFormat.FormatLong(Math.Abs(net)), StringComparison.Ordinal)
            .Replace("{duration}", NumFormat.Duration(buffSeconds), StringComparison.Ordinal)
            .Replace("{name}", outcome.Name, StringComparison.Ordinal);
    }
}
