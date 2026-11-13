using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Content.Radio;

namespace NekoClicker.Core.Tests;

/// <summary>
/// C1《猫娘电台》的内容守卫。<para>
/// 这个包的灵魂是<b>核心动词的反转</b>：别的包点击是生产线，这里点击是干扰。
/// 所以守卫也围绕这件事立：① 点击真的会掉收听率；②「管住手」的自然游玩够得着好结局
/// （盲点机器人拿不到——它会把收听率点掉，这正是设计）；③ 两个结局互斥且都可达。
/// </para>
/// <para>
/// 与图书馆守卫的关键差异：图书馆的被阅读度<b>不</b>被点击掉，所以它的盲点机器人也能拿好结局；
/// 电台的收听率会被点击掉，验证好结局必须用<b>不点击</b>的机器人。这不是守卫的漏洞，是机制本身。
/// </para>
/// </summary>
public static class RadioContentTests
{
    /// <summary>核心反转：点一下，收听率必须真的掉一截（打断播音）。</summary>
    [Test]
    public static void Clicking_DropsListenership()
    {
        GameEngine engine = CreateRadio();
        engine.State.BuildingCounts["mixer"] = 50;
        engine.State.SetCounter(RadioContent.ListenershipCounterKey, 10_000);
        engine.MarkDirty();

        double before = engine.State.GetCounter(RadioContent.ListenershipCounterKey);
        engine.Click();
        double after = engine.State.GetCounter(RadioContent.ListenershipCounterKey);

        Check.True(
            after < before,
            $"点击应当打断播音、掉收听率：点前 {before:F0}，点后 {after:F0}——没掉就说明「点击是干扰」没接上。");
    }

    /// <summary>
    /// 好结局够得着：一个<b>管住手（全程不点击）</b>的机器人自然游玩，应当落到「被听到最后」。
    /// 盲点机器人拿不到这个结局（会把收听率点掉）——那正是本包的反转，不是缺陷。
    /// </summary>
    [Test]
    public static void ListenershipAtTheEnd_ExceedsTheEndingThreshold()
    {
        GameEngine engine = PlayToTheEndWithoutClicking();

        double listeners = engine.State.GetCounter(RadioContent.ListenershipCounterKey);
        Console.WriteLine(
            $"      播到最后时：收听率 {listeners:F0}（门槛 {RadioContent.ListenershipForEnding:F0}）；" +
            $"播音类建筑 {BroadcasterBuildings(engine)}；成就 {engine.State.Achievements.Count}");

        Check.Equal(
            "end_heard_to_the_end",
            engine.ReachedEnding?.Id,
            "管住手的自然游玩应当落到「被听到最后」——够不着就说明门槛高于真实曲线养得起的听众量。");
    }

    /// <summary>两个结局都可达、且同时成立时取 Priority 更小的那个。</summary>
    [Test]
    public static void EveryEndingIsReachableAndExclusive()
    {
        Check.Equal("end_heard_to_the_end", EndingWith(RadioContent.ListenershipForEnding * 2));
        Check.Equal("end_static", EndingWith(0));

        GameEngine both = CompletedWith(RadioContent.ListenershipForEnding * 2);
        Check.True(
            both.Content.EndingById["end_static"].Condition.IsMet(both.Metrics, both.Content),
            "前提：这个状态下兜底结局的条件也应当是成立的。");
        Check.Equal("end_heard_to_the_end", both.ReachedEnding?.Id, "两个条件同时成立时应当取 Priority 更小的那个。");
    }

    // ---------------------------------------------------------------- 辅助

    private static GameEngine CreateRadio()
        => new(TestGame.Radio, new GameEngineOptions
        {
            Clock = new ManualClock(),
            Seed = 20241113,
            GrantOfflineProgress = false,
            MaxNotifications = 64,
        });

    /// <summary>播音类建筑总数（诊断用）。</summary>
    private static int BroadcasterBuildings(GameEngine engine)
    {
        int total = 0;
        foreach (BuildingDefinition building in engine.Content.Buildings)
        {
            if (building.Tags.Contains(RadioContent.BroadcasterTag, StringComparer.Ordinal))
                total += engine.State.BuildingCount(building.Id);
        }
        return total;
    }

    /// <summary>
    /// 一次<b>管住手</b>的自然游玩，走到结局：开局点几下 bootstrap（买得起第一座建筑），
    /// 建筑堆起来之后就<b>不再点击</b>——建筑自动产出，点击此后只会打断播音。
    /// 这正是本包要玩家学会的"把节目排好、管住手"。
    /// </summary>
    private static GameEngine PlayToTheEndWithoutClicking()
    {
        GameEngine engine = CreateRadio();

        for (int round = 0; round < 200_000 && engine.ReachedEnding is null; round++)
        {
            // 只在开局（建筑还没堆起来）点击；一旦有了规模就管住手。
            // 转生后建筑清零会重新 bootstrap，但那时收听率也刚清零，点了不亏。
            if (TotalBuildings(engine) < 30)
                for (int i = 0; i < 8; i++) engine.Click();

            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(engine.State.Era >= 2 ? 0.25 : 30);
        }

        Check.NotNull(engine.ReachedEnding, "管住手的机器人没能播到最后——主线或终局不可达。");
        return engine;
    }

    /// <summary>持有建筑总数（bootstrap 判据用）。</summary>
    private static int TotalBuildings(GameEngine engine)
    {
        int total = 0;
        foreach (BuildingDefinition building in engine.Content.Buildings)
            total += engine.State.BuildingCount(building.Id);
        return total;
    }

    /// <summary>把状态摆成"末层主线已完成 + 给定收听率"，看落到哪个结局。</summary>
    private static GameEngine CompletedWith(double listenership)
    {
        GameEngine engine = CreateRadio();
        engine.State.Era = 2;
        engine.State.CookiesEarnedThisRun = 1e9;
        engine.State.SetCounter(RadioContent.ListenershipCounterKey, listenership);
        engine.MarkDirty();
        engine.Simulate(60);
        return engine;
    }

    private static string? EndingWith(double listenership)
    {
        GameEngine engine = CompletedWith(listenership);
        Check.NotNull(engine.ReachedEnding, "末层主线完成之后没有拿到任何结局。");
        return engine.ReachedEnding?.Id;
    }
}
