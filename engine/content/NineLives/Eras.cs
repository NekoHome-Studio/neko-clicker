using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 九条命 = 九层纪元。<para>
/// 每一层都换掉一条规则（用 <see cref="EraDefinition.Balance"/> 改数值参数，或用
/// <see cref="EraDefinition.Modifiers"/> 加倍率），这样"推进纪元"才是改变玩法，
/// 而不只是重复劳动。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>，否则灰按钮的进度会倒退、玩家可能卡死。
/// 所以这里只用三类指标：本轮累计赚取（每层归零，层内只增）、成就数、
/// 以及两个计数器（<c>peak_cps</c> 与 <c>faith</c>）。构建期会强制校验。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>
    /// 本包的 <c>text.json</c>：纪元文案与剧情散文、建筑文案<b>共用同一份实例</b>（<see cref="Lore.Prose"/>）。<para>
    /// 必须共用：孤儿检查会遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的——
    /// 两个实例各记一半，就会把对方那些 id 全报成孤儿。
    /// </para>
    /// </summary>
    private static ContentText Prose => Lore.Prose;

    /// <summary>九层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "life_cardboard",
            Name = Prose.Text("eras", "life_cardboard", "name"),
            Icon = Prose.Text("eras", "life_cardboard", "icon"),
            Theme = Prose.Text("eras", "life_cardboard", "theme"),
            EntryText = Prose.Text("eras", "life_cardboard", "entryText"),
            ExitText = Prose.Text("eras", "life_cardboard", "exitText"),
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = Prose.Text("eras", "life_cardboard", "completionHint"),
            Modifiers = [],
        },
        new()
        {
            Index = 2,
            Id = "life_cafe",
            Name = Prose.Text("eras", "life_cafe", "name"),
            Icon = Prose.Text("eras", "life_cafe", "icon"),
            Theme = Prose.Text("eras", "life_cafe", "theme"),
            EntryText = Prose.Text("eras", "life_cafe", "entryText"),
            ExitText = Prose.Text("eras", "life_cafe", "exitText"),
            // 事件更频繁：治愈系的节奏要快一点。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.5,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.5,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = Prose.Text("eras", "life_cafe", "completionHint"),
            Modifiers = [],
            UnlocksBuildings = ["cat_cafe", "catnip_field"],
        },
        new()
        {
            Index = 3,
            Id = "life_lab",
            Name = Prose.Text("eras", "life_lab", "name"),
            Icon = Prose.Text("eras", "life_lab", "icon"),
            Theme = Prose.Text("eras", "life_lab", "theme"),
            EntryText = Prose.Text("eras", "life_lab", "entryText"),
            ExitText = Prose.Text("eras", "life_lab", "exitText"),
            // 道德税：产量上去了，但情感能量结算打折。
            MetaRewardMultiplier = 0.8,
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e6)),
            CompletionHint = Prose.Text("eras", "life_lab", "completionHint"),
            UnlocksBuildings = ["cat_tower", "catgirl_lab"],
        },
        new()
        {
            Index = 4,
            Id = "life_civilization",
            Name = Prose.Text("eras", "life_civilization", "name"),
            Icon = Prose.Text("eras", "life_civilization", "icon"),
            Theme = Prose.Text("eras", "life_civilization", "theme"),
            EntryText = Prose.Text("eras", "life_civilization", "entryText"),
            ExitText = Prose.Text("eras", "life_civilization", "exitText"),
            // 这一层鼓励铺量：批量上限与卖出返还都放宽。
            Balance = baseBalance with
            {
                MaxBulkBuy = 1_000_000,
                DefaultSellRefundRate = 0.75,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = Prose.Text("eras", "life_civilization", "completionHint"),
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            UnlocksBuildings = ["server_farm"],
        },
        new()
        {
            Index = 5,
            Id = "life_cyber",
            Name = Prose.Text("eras", "life_cyber", "name"),
            Icon = Prose.Text("eras", "life_cyber", "icon"),
            Theme = Prose.Text("eras", "life_cyber", "theme"),
            EntryText = Prose.Text("eras", "life_cyber", "entryText"),
            ExitText = Prose.Text("eras", "life_cyber", "exitText"),
            // 服务器不睡：离线收益上限翻倍。
            Balance = baseBalance with { OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2 },
            Modifiers = [Modifier.GlobalMultiplier(1.5), Modifier.BuildingMultiplier("server_farm", 3)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e8)),
            CompletionHint = Prose.Text("eras", "life_cyber", "completionHint"),
            UnlocksBuildings = ["memory_vault"],
        },
        new()
        {
            Index = 6,
            Id = "life_posthuman",
            Name = Prose.Text("eras", "life_posthuman", "name"),
            Icon = Prose.Text("eras", "life_posthuman", "icon"),
            Theme = Prose.Text("eras", "life_posthuman", "theme"),
            EntryText = Prose.Text("eras", "life_posthuman", "entryText"),
            ExitText = Prose.Text("eras", "life_posthuman", "exitText"),
            // 高风险高回报：增益更短，但常驻产量翻倍。
            Modifiers =
            [
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.5),
                Modifier.GlobalMultiplier(2),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e8),
                UnlockCondition.AchievementsAtLeast(18)),
            CompletionHint = Prose.Text("eras", "life_posthuman", "completionHint"),
            UnlocksBuildings = ["temple"],
            UnlocksUpgrades = ["eulogy_reader"],
        },
        new()
        {
            Index = 7,
            Id = "life_goddess",
            Name = Prose.Text("eras", "life_goddess", "name"),
            Icon = Prose.Text("eras", "life_goddess", "icon"),
            Theme = Prose.Text("eras", "life_goddess", "theme"),
            EntryText = Prose.Text("eras", "life_goddess", "entryText"),
            ExitText = Prose.Text("eras", "life_goddess", "exitText"),
            Modifiers =
            [
                // 直播间产量随信仰成长：第二资源直接驱动数值。
                new Modifier(
                    ModifierTarget.BuildingCps("stream_studio"),
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.01, Cap: 200, Id: FaithModule.CounterKey)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(7e8),
                UnlockCondition.Counter(FaithModule.CounterKey, 5e4)),
            CompletionHint = Prose.Text("eras", "life_goddess", "completionHint"),
            UnlocksBuildings = ["stream_studio"],
        },
        new()
        {
            Index = 8,
            Id = "life_dream",
            Name = Prose.Text("eras", "life_dream", "name"),
            Icon = Prose.Text("eras", "life_dream", "icon"),
            Theme = Prose.Text("eras", "life_dream", "theme"),
            EntryText = Prose.Text("eras", "life_dream", "entryText"),
            ExitText = Prose.Text("eras", "life_dream", "exitText"),
            // 必须在场做梦：离线上限压到 10 分钟，但清醒时产量 ×3。
            Balance = baseBalance with { OfflineCapSeconds = 600 },
            Modifiers = [Modifier.GlobalMultiplier(3)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e9),
                UnlockCondition.AchievementsAtLeast(26)),
            CompletionHint = Prose.Text("eras", "life_dream", "completionHint"),
            UnlocksBuildings = ["dream_library"],
        },
        new()
        {
            Index = 9,
            Id = "life_library",
            Name = Prose.Text("eras", "life_library", "name"),
            Icon = Prose.Text("eras", "life_library", "icon"),
            Theme = Prose.Text("eras", "life_library", "theme"),
            EntryText = Prose.Text("eras", "life_library", "entryText"),
            ExitText = Prose.Text("eras", "life_library", "exitText"),
            Modifiers =
            [
                // 产量随"被阅读量"成长：读到的记忆越多，这一世越强。
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.LoreCount, 0.02, Cap: 60)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e9),
                UnlockCondition.AchievementsAtLeast(30)),
            CompletionHint = Prose.Text("eras", "life_library", "completionHint"),
            UnlocksBuildings = ["cat_universe"],
        },
    ];
}
