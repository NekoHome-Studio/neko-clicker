using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 「点击 × 建筑」的桥：十一个包各一条，是点击线上**唯一**用 <see cref="Scaling"/> 的形态。<para>
/// 这一组守卫钉三件事，每一件都对应一条**会静默失效**的写法：
/// <list type="number">
///   <item>形状与门槛——把 <c>ScalingSource</c> 换成别的、把门槛从"建筑总数"换回"点击次数"、
///         或者把 <c>Cap</c> 删掉（后期一个升级吃掉整条曲线，手册 §3(b)），构建期都不会红。</item>
///   <item>实际效果与上限——描述里写着"每座 +0.5%、最多 120 座"，而玩家读到的那句话
///         与代码算出来的数**没有任何机制保证一致**。</item>
///   <item>可达性——"一局自然游玩里够得着"只有跑一次机器人才算证明；
///         够不着时它不会报错，只会**一直不亮**（这正是永久线那条老缺陷的形态）。</item>
/// </list>
/// 第 3 条放在 <see cref="PrestigeTests"/> 那条已经跑遍全部纪元包的机器人里，不另起一次长跑。
/// </para>
/// </summary>
public static class ClickBridgeTests
{
    /// <summary>每座建筑带来的点击收益增量（+0.5%）——<c>text.json</c> 的描述必须说同一个数。</summary>
    private const double PerUnit = 0.005;

    /// <summary>成长量的上限（限的是**原始计数**：最多计入 120 座，不是"+120%"）。</summary>
    private const double Cap = 120;

    /// <summary>解锁门槛：持有建筑总数。</summary>
    private const double RequiredBuildings = 80;

    /// <summary>
    /// 金表：包 → 桥 id / 前置档 id / 价格。<para>
    /// 三列都**写死**是有意的：从内容里"推"出它们等于让守卫跟着实现一起漂（照镜子）。
    /// 门槛取 <see cref="RequiredBuildings"/> 的依据是实测包络——一局自然游玩里建筑总量的
    /// 下界是**公司包 133 座**（本仓库最小的那个），80 对它留了 1.6 倍余量；实测见 OPEN_WORK §0.16。
    /// </para>
    /// </summary>
    private static readonly (string Pack, string Bridge, string Gate, double Price)[] Golden =
    [
        ("猫咖物语", "paws_everywhere", "plush_gloves", 2_000_000_000),
        ("#1 咖啡馆", "every_paw_in_the_cafe", "latte_art", 2_000_000_000),
        ("#2 九命", "nine_lifetimes_of_practice", "both_hands", 2_000_000_000),
        ("#3 实验室", "distributed_haptics", "two_hands_log", 5_000_000),
        ("#10 公司", "shareholder_paws", "pitch_deck", 5_000_000),
        ("#6 末世", "hands_of_the_shelter", "metal_detector", 2_000_000_000),
        ("#9 图书馆", "marginal_hands", "writing_habit", 2_000_000_000),
        ("#7 神明", "ten_thousand_paws", "live_demo", 2_000_000_000),
        ("#4 文明", "hands_of_the_people", "stone_knife", 6e9),
        ("#5 赛博", "swarm_pointer", "autocomplete", 2_000_000_000),
        ("#8 梦境", "shared_dreaming", "half_asleep", 8e10),
    ];

    /// <summary>
    /// 每个包**恰好一条**桥，形状一致：点击 +N%、成长来源是"持有建筑总数"、
    /// 门槛压在建筑总量上而不是点击次数上，而且描述里的数与代码是同一个数。
    /// </summary>
    [Test]
    public static void EveryPackHasExactlyOneBridge_WithTheSameShape()
    {
        // 覆盖度：金表与内容包清单必须一一对应（新增一个包却忘了登记时这条会红）。
        Check.Equal(
            string.Join("、", TestGame.AllContentPacks().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)),
            string.Join("、", Golden.Select(r => r.Pack).OrderBy(n => n, StringComparer.Ordinal)),
            "内容包清单与这张金表不一致——加了包就要在金表里加一行。");

        foreach ((string pack, GameContent content) in TestGame.AllContentPacks())
        {
            List<UpgradeDefinition> bridges = [.. content.Upgrades.Where(IsBridge)];
            Check.Equal(1, bridges.Count, $"{pack} 应当恰好有一条『点击 × 建筑』的桥，实际 {bridges.Count} 条。");

            UpgradeDefinition bridge = bridges[0];
            (string _, string bridgeId, string gateId, double price) =
                Golden.Single(row => row.Pack == pack);

            Check.Equal(bridgeId, bridge.Id, $"{pack} 的桥 id 与金表对不上。");
            Check.NotNull(content.FindUpgrade(gateId), $"{pack} 的前置档「{gateId}」不存在（金表写错了？）");
            Check.Close(price, bridge.Price, 1e-6, $"{pack} 的桥「{bridge.Id}」价格与金表对不上。");

            Modifier effect = bridge.Modifiers.Single(m => m.Target.Kind == ModifierTargetKind.ClickPower);
            Check.Equal(ModifierOperation.AdditivePercent, effect.Operation, $"{pack}：桥必须是加法百分比。");
            Check.Close(0, effect.Value, 1e-12, $"{pack}：桥的基础值必须是 0——效果全部来自成长。");
            Check.NotNull(effect.Scaling, $"{pack}：桥没有成长曲线，那它就只是又一个固定倍率。");
            Check.Equal(
                ScalingSource.TotalBuildings,
                effect.Scaling!.Source,
                $"{pack}：桥的成长来源必须是「持有建筑总数」。");
            Check.Close(PerUnit, effect.Scaling.PerUnit, 1e-12, $"{pack}：桥的每单位增量与描述不符。");
            Check.Close(Cap, effect.Scaling.Cap, 1e-12, $"{pack}：桥的成长上限与描述不符。");
            Check.Null(effect.Scaling.Id, $"{pack}：TotalBuildings 不需要 id。");

            NumericCondition? gate = bridge.Unlock.NumericLeaves()
                .FirstOrDefault(c => c.Metric == NumericMetric.TotalBuildings);
            Check.NotNull(gate, $"{pack}：桥没有压在任何建筑总量门槛上。");
            Check.Close(RequiredBuildings, gate!.Target, 1e-12, $"{pack}：桥的建筑总量门槛与金表不符。");
            Check.False(
                bridge.Unlock.NumericLeaves().Any(c => c.Metric == NumericMetric.Clicks),
                $"{pack}：桥用了点击次数门槛——那它就不是「把点击接到建筑上」的那条线了。");
            Check.True(
                bridge.Tags.Contains("click") || bridge.Tags.Contains("touch"),
                $"{pack}：桥没有挂在点击线的标签上。");

            // 描述是一句**承诺**：玩家读到 "+0.5%" 与 "120 座 / +60%"，代码就得是这三个数。
            Check.Contains(bridge.Description, "0.5%", $"{pack}：桥的描述没写每座 +0.5%。");
            Check.Contains(bridge.Description, "120", $"{pack}：桥的描述没写「最多算 120 座」。");
            Check.Contains(bridge.Description, "+60%", $"{pack}：桥的描述没写「即 +60%」。");
        }
    }

    /// <summary>
    /// 桥确实把点击收益抬起来，抬到描述里写明的比例，而且**在上限处停住**。<para>
    /// 三个建筑总量各测一次：门槛（80 座 → +40%）、上限（120 座 → +60%）、
    /// 上限之上（360 座 → 仍然是 +60%）。最后那一档就是 <c>Cap</c> 的判别力——
    /// 没有它，"最多算 120 座"这句承诺没有任何东西守着。
    /// </para>
    /// </summary>
    [Test]
    public static void Bridge_RaisesClickPowerByTheClaimedShare_AndStopsAtTheCap()
    {
        foreach ((string pack, string bridgeId, string gateId, double _) in Golden)
        {
            GameContent content = FindContent(pack);

            CheckRatio(content, pack, bridgeId, gateId, RequiredBuildings);
            CheckRatio(content, pack, bridgeId, gateId, Cap);
            CheckRatio(content, pack, bridgeId, gateId, Cap * 3);
        }
    }

    /// <summary>
    /// <c>TotalBuildings</c> 这个成长来源**只归这条桥用**。<para>
    /// 它是本仓库此前零使用的来源（十一个包一条都没用过）；把它用到产能上等于另一种玩法改动，
    /// 那时候这条守卫会红，逼一次"这是有意的吗"的决定，而不是让它悄悄发生。
    /// </para>
    /// </summary>
    [Test]
    public static void TotalBuildings_IsOnlyUsedByTheBridge()
    {
        foreach ((string pack, GameContent content) in TestGame.AllContentPacks())
        {
            foreach (UpgradeDefinition upgrade in content.Upgrades)
            {
                foreach (Modifier modifier in upgrade.Modifiers)
                {
                    if (modifier.Scaling?.Source != ScalingSource.TotalBuildings) continue;
                    Check.Equal(
                        ModifierTargetKind.ClickPower,
                        modifier.Target.Kind,
                        $"{pack}：升级「{upgrade.Id}」把「持有建筑总数」用在了 {modifier.Target.Kind} 上——"
                        + $"这个来源今天只归点击桥用（见这条守卫的注释）。");
                }
            }
        }
    }

    private static void CheckRatio(GameContent content, string pack, string bridgeId, string gateId, double buildings)
    {
        GameEngine engine = FundedEngine(content, buildings);
        BuyClickLine(engine, bridgeId);

        Check.True(
            engine.State.UpgradeCount(gateId) > 0,
            $"{pack}：前置档「{gateId}」在 {buildings} 座建筑、十万次点击的状态下依然买不了。");

        double before = engine.ClickPower;
        PurchaseResult bought = engine.BuyUpgrade(bridgeId);
        Check.True(
            bought.Success,
            $"{pack}：{buildings} 座建筑时买不到桥「{bridgeId}」——{bought.Message}");
        double after = engine.ClickPower;

        Check.Greater(after, before, $"{pack}：买了桥「{bridgeId}」之后点击收益没有变大（{before} → {after}）。");
        Check.CloseRelative(
            1.0 + (PerUnit * Math.Min(buildings, Cap)),
            after / before,
            1e-9,
            $"{pack}：{buildings} 座建筑时桥带来的点击倍率不对。");
    }

    /// <summary>
    /// 一个"钱与点击都够、只有建筑数量受控"的状态。<para>
    /// 建筑数量用 <see cref="GameState.BuildingCounts"/> 直接置数（存档结构是公开的纯数据，
    /// 测试构造不需要绕购买流程）；点击次数给十万是为了让前置档的 <c>ClicksAtLeast</c> 成立。
    /// </para>
    /// </summary>
    private static GameEngine FundedEngine(GameContent content, double buildings)
    {
        GameEngine engine = TestGame.Create(content);
        engine.State.Cookies = 1e18;
        engine.State.CookiesEarnedThisRun = 1e18;
        engine.State.CookiesEarnedAllTime = 1e18;
        engine.State.TotalClicks = 100_000;
        engine.State.BuildingCounts[content.Buildings[0].Id] = (int)buildings;
        engine.MarkDirty();
        return engine;
    }

    /// <summary>
    /// 把这条包的点击线买到"只剩桥"为止——<b>只买加法与乘法档，跳过所有百分比档</b>。<para>
    /// 理由：要测的量是"桥带来的倍率" = <c>(1 + 桥的百分比) / (1 + 别的百分比)</c>，
    /// 所以别的百分比档必须恰好是 0，比值才是一个不含 <c>cps</c>、不含其它升级的干净数。
    /// 买不动的一律忽略（<c>BuyUpgrade</c> 自己会按解锁条件拒绝），最后只断言前置档到手。
    /// </para>
    /// </summary>
    private static void BuyClickLine(GameEngine engine, string bridgeId)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            foreach (UpgradeDefinition def in engine.Content.Upgrades)
            {
                if (def.Id == bridgeId) continue;
                if (!def.Modifiers.Any(m => m.Target.Kind == ModifierTargetKind.ClickPower)) continue;
                if (def.Modifiers.Any(m => m.Operation == ModifierOperation.AdditivePercent)) continue;
                engine.BuyUpgrade(def.Id);
            }
        }
    }

    private static bool IsBridge(UpgradeDefinition upgrade)
        => upgrade.Modifiers.Any(m => m.Target.Kind == ModifierTargetKind.ClickPower && m.Scaling is not null);

    private static GameContent FindContent(string pack)
        => TestGame.AllContentPacks().Single(p => p.Name == pack).Content;
}
