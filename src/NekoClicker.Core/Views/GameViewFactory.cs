using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core.Views;

/// <summary>
/// 把引擎状态折算成 <see cref="GameSnapshot"/>。<para>
/// 所有"UI 需要但引擎不关心"的计算（价格、进度百分比、里程碑提示、格式化文本）都集中在这里，
/// 好处是不同前端拿到的数字必然一致——终端 demo 与将来的 Web 前端不会出现"两边显示不一样"。
/// </para>
/// </summary>
public static class GameViewFactory
{
    /// <summary>生成快照。</summary>
    public static GameSnapshot Create(GameEngine engine, PurchaseMode mode)
    {
        GameState state = engine.State;
        GameContent content = engine.Content;
        IGameMetrics metrics = engine.Metrics;
        ProductionBreakdown production = engine.Production;
        bool selling = mode.IsSell();
        int requested = mode.RequestedAmount();

        var buildings = new List<BuildingView>(content.Buildings.Count);
        foreach (BuildingDefinition definition in content.Buildings)
        {
            bool unlocked = definition.Unlock.IsMet(metrics, content);
            int owned = state.BuildingCount(definition.Id);
            double priceMultiplier = engine.GetPriceMultiplier(definition.Id);
            double unitPrice = Pricing.UnitPrice(definition, owned, priceMultiplier);
            double refundRate = definition.SellRefundRate ?? engine.Balance.DefaultSellRefundRate;

            int batch;
            double batchPrice;
            bool canAct;

            if (selling)
            {
                batch = requested <= 0 ? owned : Math.Min(requested, owned);
                batchPrice = Pricing.SellValue(definition, owned, batch, refundRate);
                canAct = batch > 0;
            }
            else
            {
                batch = requested <= 0
                    ? Pricing.MaxAffordable(definition, owned, state.Cookies, priceMultiplier, Math.Max(1, engine.Balance.MaxBulkBuy))
                    : requested;
                batchPrice = Pricing.BulkPrice(definition, owned, batch, priceMultiplier);
                // "买满"模式下数量已经按预算算好，此时必然买得起（除非为 0）。
                canAct = batch > 0 && batchPrice <= state.Cookies;
            }

            BuildingProduction bp = production.For(definition.Id);
            (int? milestone, string? milestoneName) = FindNextMilestone(engine, definition.Id, owned);

            buildings.Add(new BuildingView
            {
                Id = definition.Id,
                Name = definition.Name,
                Icon = definition.Icon,
                Description = definition.Description,
                Category = definition.Category,
                Owned = owned,
                IsUnlocked = unlocked,
                HiddenUntilUnlocked = definition.HiddenUntilUnlocked,
                UnlockHint = definition.Unlock.Describe(content),
                UnlockProgress = Progress(definition.Unlock, metrics),
                UnitPrice = unitPrice,
                BatchAmount = batch,
                BatchPrice = batchPrice,
                CanAfford = canAct,
                CpsEach = bp.Count > 0 ? bp.Cps / bp.Count : EffectiveUnitCps(bp, priceMultiplier, production),
                CpsContribution = bp.Cps,
                CpsShare = bp.Share,
                NextMilestoneAt = milestone,
                NextMilestoneName = milestoneName,
                SellRefundRate = refundRate,
            });
        }

        var upgrades = new List<UpgradeView>(content.Upgrades.Count);
        foreach (UpgradeDefinition definition in content.Upgrades)
        {
            bool unlocked = definition.Unlock.IsMet(metrics, content);
            int owned = state.UpgradeCount(definition.Id);
            double price = Pricing.UpgradePrice(definition, owned, 1);
            double wallet = definition.Currency == UpgradeCurrency.PrestigeChips ? state.PrestigeChips : state.Cookies;

            upgrades.Add(new UpgradeView
            {
                Id = definition.Id,
                Name = definition.Name,
                Icon = definition.Icon,
                Description = definition.Description,
                Price = price,
                Currency = definition.Currency,
                Owned = owned,
                MaxPurchases = definition.MaxPurchases,
                IsUnlocked = unlocked,
                HiddenUntilUnlocked = definition.HiddenUntilUnlocked,
                UnlockHint = definition.Unlock.Describe(content),
                UnlockProgress = Progress(definition.Unlock, metrics),
                CanAfford = wallet >= price,
                EffectSummary = Summarize(definition.Modifiers, content),
                Category = definition.Category,
                Tier = definition.Tier,
                IsPermanent = definition.Persistence == UpgradePersistence.Permanent,
            });
        }

        var achievements = new List<AchievementView>(content.Achievements.Count);
        foreach (AchievementDefinition definition in content.Achievements)
        {
            bool unlocked = state.Achievements.Contains(definition.Id);
            (double current, double target) = AchievementSystem.Progress(definition, metrics);
            bool quantifiable = target > 0;
            bool concealed = definition.Hidden && !unlocked;

            achievements.Add(new AchievementView
            {
                Id = definition.Id,
                Name = concealed ? "???" : definition.Name,
                Icon = concealed ? "🔒" : definition.Icon,
                Description = concealed ? "隐藏成就：达成后揭晓。" : definition.Description,
                Unlocked = unlocked,
                Hidden = definition.Hidden,
                Category = definition.Category,
                Progress = quantifiable ? Math.Clamp(current / target, 0, 1) : (unlocked ? 1 : 0),
                ProgressText = quantifiable && !unlocked
                    ? $"{NumFormat.FormatPlain(Math.Min(current, target))} / {NumFormat.FormatPlain(target)}"
                    : string.Empty,
            });
        }

        var buffs = new List<BuffView>(state.Buffs.Count);
        foreach (ActiveBuff buff in state.Buffs)
        {
            content.BuffById.TryGetValue(buff.Id, out BuffDefinition? definition);
            buffs.Add(new BuffView
            {
                Id = buff.Id,
                Name = definition?.Name ?? buff.Id,
                Icon = definition?.Icon ?? "✨",
                Description = definition?.Description ?? string.Empty,
                RemainingSeconds = buff.RemainingSeconds,
                TotalSeconds = buff.TotalSeconds,
                Progress = buff.Progress,
                Stacks = buff.Stacks,
                IsDebuff = definition?.IsDebuff ?? false,
            });
        }

        var goldenCookies = new List<GoldenCookieView>(state.GoldenCookies.Count);
        foreach (GoldenCookieSpawn spawn in state.GoldenCookies)
        {
            goldenCookies.Add(new GoldenCookieView
            {
                InstanceId = spawn.InstanceId,
                RemainingSeconds = spawn.RemainingSeconds,
                LifetimeSeconds = spawn.LifetimeSeconds,
                Progress = spawn.LifetimeSeconds <= 0 ? 0 : Math.Clamp(spawn.RemainingSeconds / spawn.LifetimeSeconds, 0, 1),
                X = spawn.X,
                Y = spawn.Y,
            });
        }

        return new GameSnapshot
        {
            Title = content.Title,
            CurrencyName = content.CurrencyName,
            CurrencyIcon = content.CurrencyIcon,
            ClickActionName = content.ClickActionName,
            PrestigeCurrencyName = content.PrestigeCurrencyName,
            PrestigeCurrencyIcon = content.PrestigeCurrencyIcon,
            Cookies = state.Cookies,
            CookiesPerSecond = production.CookiesPerSecond,
            ClickPower = production.ClickPower,
            CookiesText = NumFormat.FormatLong(state.Cookies),
            CpsText = NumFormat.FormatLong(production.CookiesPerSecond),
            ClickPowerText = NumFormat.FormatLong(production.ClickPower),
            CookiesEarnedThisRun = state.CookiesEarnedThisRun,
            CookiesEarnedAllTime = state.CookiesEarnedAllTime,
            HandMadeCookies = state.HandMadeCookies,
            TotalClicks = state.TotalClicks,
            GoldenCookiesClicked = state.GoldenCookiesClicked,
            PrestigeLevel = state.PrestigeLevel,
            PrestigeChips = state.PrestigeChips,
            Ascensions = state.Ascensions,
            PlayTimeSeconds = state.PlayTimeSeconds,
            AchievementCount = state.Achievements.Count,
            AchievementTotal = content.Achievements.Count,
            TotalBuildings = state.TotalBuildings(),
            PurchasedUpgrades = state.UpgradeCounts.Count,
            GoldenCookieCountdown = state.GoldenCookieCountdown,
            Mode = mode,
            Buildings = buildings,
            Upgrades = upgrades,
            Achievements = achievements,
            Buffs = buffs,
            GoldenCookies = goldenCookies,
            Notifications = [.. engine.Notifications],
            Prestige = PrestigeSystem.Preview(engine),
        };
    }

    /// <summary>把一组修饰符压成一行摘要（最多 3 条，其余折叠成"+N 项"）。</summary>
    public static string Summarize(IReadOnlyList<Modifier> modifiers, GameContent? content = null)
    {
        if (modifiers.Count == 0) return string.Empty;
        int shown = Math.Min(3, modifiers.Count);
        string body = string.Join("；", modifiers.Take(shown).Select(m => m.Describe(content)));
        return modifiers.Count > shown ? $"{body}；等 {modifiers.Count} 项" : body;
    }

    private static double Progress(UnlockCondition condition, IGameMetrics metrics)
    {
        if (!condition.TryGetProgress(metrics, out double current, out double target) || target <= 0)
            return condition.IsMet(metrics, GameContent.Empty) ? 1 : 0;
        return Math.Clamp(current / target, 0, 1);
    }

    private static double EffectiveUnitCps(BuildingProduction production, double priceMultiplier, ProductionBreakdown breakdown)
        => production.Count > 0 ? production.Cps / production.Count : 0;

    private static (int? Threshold, string? Name) FindNextMilestone(GameEngine engine, string buildingId, int owned)
    {
        int? best = null;
        string? name = null;

        foreach (UpgradeDefinition upgrade in engine.Content.Upgrades)
        {
            if (engine.State.UpgradeCount(upgrade.Id) >= upgrade.MaxPurchases) continue;

            foreach (NumericCondition condition in upgrade.Unlock.NumericLeaves())
            {
                if (condition.Metric != NumericMetric.BuildingCount) continue;
                if (!string.Equals(condition.Id, buildingId, StringComparison.Ordinal)) continue;
                if (condition.Target <= owned) continue;

                int threshold = (int)Math.Min(condition.Target, int.MaxValue);
                if (best is null || threshold < best)
                {
                    best = threshold;
                    name = upgrade.Name;
                }
            }
        }

        return (best, name);
    }
}
