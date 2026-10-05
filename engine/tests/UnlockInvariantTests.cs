using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 「拥有 ⇒ 已解锁」这条隐含不变量的**横扫版**守卫。<para>
/// <b>它守什么</b>：快照里任何 <c>Owned &gt; 0</c> 的建筑行都必须 <c>IsUnlocked == true</c>。
/// 这条性质只在一种情况下会被打破——<b>开继承</b>：解锁门若写成「本轮累计赚取」，
/// 舍命 / 转生把本轮累计清零之后，继承进来的建筑会显示成未解锁
/// （产量照算，但列表里看不到、也卖不掉）。规则见 <c>CONTENT_AUTHORING.md</c> §11.1。
/// </para>
/// <para>
/// <b>为什么是横扫而不是单包</b>：这条不变量与「哪些包开了继承」耦合，而按包写死的守卫
/// 「新包不会自动被扫到」。原先唯一的守卫是
/// <see cref="ApocalypseContentTests.OwnedBuildings_StayUnlockedAfterARestart"/>
/// 调用的私有辅助 <c>ApocalypseContentTests.SnapshotAndCheckUnlocked</c>，只对末世包生效。
/// 这里改成从 <see cref="TestGame.AllContentPacks"/> / <see cref="TestGame.AllEraPacks"/> 派生：
/// 新包只要进了那张表就自动被扫到（教训见 <c>PrestigeTests</c> 里那段「手写清单会让
/// 『通用守卫自动覆盖』这个假设悄悄不成立」的注释）。
/// </para>
/// <para>
/// <b>与末世那条的分工</b>：那条用真实内容里的具体 id（<c>relic_city</c> 7 座 → 1 座）
/// 钉住「继承比例本身真的生效」，是单包、带 id 的验收；这条只守判据（拥有 ⇒ 已解锁），
/// 横扫全部包、并让九个纪元包各真的重开一次，不重复验继承比例。
/// 两条都保留：删掉那条会丢掉「继承真的生效」的证据，删掉这条则会让下一个打开继承的包无人覆盖。
/// </para>
/// <para>
/// 登记册条目见 <c>engine/docs/STRUCTURE_OPTIMIZATION.md</c> §S10
/// （「拥有 ⇒ 已解锁」只有一个包的私有用例在守）。
/// </para>
/// </summary>
public static class UnlockInvariantTests
{
    /// <summary>每个包玩多少轮；每轮 30 模拟秒，于是预算是 15 游戏分钟。</summary>
    private const int PlayRounds = 30;

    /// <summary>
    /// 重开前给每座已解锁的建筑发多少座。<para>
    /// 取 8 是因为末世第 2 层继承 25%（<c>floor(8 × 0.25) = 2</c>），
    /// 保证重开之后真的还有建筑留在手里——否则这条守卫就没有观测对象。
    /// </para>
    /// </summary>
    private const int SeededCount = 8;

    /// <summary>逐个添 dummy 成就时的保护性上限（内容目前的纪元门槛最多要到 34 个）。</summary>
    private const int DummyAchievementCap = 1_000;

    /// <summary>
    /// 横扫十一个包：自然游玩一段之后，快照里 <c>Owned &gt; 0</c> 的每一行都必须已解锁。<para>
    /// 循环形状照 <c>ApocalypseContentTests.RobotWalksAllFiveRestarts</c> /
    /// <c>EraTests.G4_RobotWalksFromTheFirstEraToTheLast</c>：点击 → 贪心买 → 推进 30 模拟秒
    /// （时间由 <see cref="ManualClock"/> 之下的 <see cref="GameEngine.Simulate"/> 直接推进，
    /// 仓库里的长跑用例都是这个写法）。这里只要「手里确实有建筑」，不追求走完主线——
    /// 那是各包 <c>RobotWalks…</c> 的职责——所以预算固定 30 轮（见 <c>STRUCTURE_OPTIMIZATION</c> §W4）。
    /// </para>
    /// </summary>
    [Test]
    public static void OwnedBuildings_StayUnlockedInEveryPack()
    {
        var packs = TestGame.AllContentPacks();
        int packsWithBuildings = 0;
        int checkedRows = 0;

        foreach ((string name, GameContent content) in packs)
        {
            GameEngine engine = TestGame.Create(content);

            for (int round = 0; round < PlayRounds; round++)
            {
                for (int i = 0; i < 8; i++) engine.Click();
                TestGame.BuyGreedily(engine);
                engine.Simulate(30);
            }

            int rows = CheckOwnedAreUnlocked(name, engine, "自然游玩之后");
            if (rows > 0) packsWithBuildings++;
            checkedRows += rows;
        }

        Check.AtLeast(packsWithBuildings, 1, "没有一个包在预算内买到建筑——这条横扫就是空的。");
        Console.WriteLine(
            $"      有建筑的包：{packsWithBuildings} / {packs.Length}；"
            + $"检查了 {checkedRows} 行「拥有 ⇒ 已解锁」");
    }

    /// <summary>
    /// 横扫九个纪元包：每个包都**真的重开一次**，重开之后再断言一次「拥有 ⇒ 已解锁」。<para>
    /// 这是 §S10 指出的形状：解锁门用「本轮累计」的包一旦打开继承，重开就会把手里留着的建筑
    /// 显示成未解锁。没有继承的包重开之后建筑被清空，这一轮对它们只是顺手上一道锁
    /// （判据空成立，不算观测对象）——所以结尾要求「至少有一个包真的留下了建筑」。
    /// </para>
    /// <para>
    /// <b>为什么可以直造状态</b>：起点照 <c>ApocalypseContentTests.OwnedBuildings_StayUnlockedAfterARestart</c>
    /// 的造法——资金充裕 + 直接给已解锁的建筑置持有数（<c>BuildingCounts</c> 是公开的纯数据，
    /// 同 <c>ViewTests.BuildingRows_ReportTheUnitRateForEveryPack</c> / <c>ClickBridgeTests.FundedEngine</c> 的做法），
    /// 本层完成门槛也直接补满。理由是「这层门槛够不够得着」属于
    /// <c>PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun</c>（那条真的把九个包玩到结局），
    /// 这条只借「重开」这个动作观测重开之后的快照，于是九个包的重开都不需要任何长跑循环。
    /// </para>
    /// </summary>
    [Test]
    public static void OwnedBuildings_StayUnlockedAfterEveryPackRestarts()
    {
        var packs = TestGame.AllEraPacks();
        int restarted = 0;
        int rowsBeforeRestart = 0;
        int rowsAfterRestart = 0;

        foreach ((string name, GameEngine engine) in packs)
        {
            // ① 资金充裕的起点（同 TestGame.CreateNineLivesFunded 的造法）。
            engine.State.Cookies = 1e18;
            engine.State.CookiesEarnedThisRun = 1e18;
            engine.State.CookiesEarnedAllTime = 1e18;
            engine.MarkDirty();

            // ② 只给「此刻确实解锁」的建筑发数量：重开之前的状态必须自洽，
            //    否则重开后的红色会把「我造出来的矛盾」报成内容缺陷。
            int seeded = SeedUnlockedBuildings(engine);
            Check.AtLeast(seeded, 1, $"{name}：1e18 累计下仍然一座建筑都没解锁——这条用例的前提不成立。");

            // ③ 补满本层完成条件，好让这个包真的重开一次。认不出的指标会当场失败，不静默跳过。
            SatisfyCurrentEraCompletion(name, engine);
            Check.True(
                engine.EraGate.CanAdvance,
                $"{name}：第 {engine.State.Era} 层的完成条件没被补满"
                + $"（{engine.EraGate.BlockedReason}）——这条横扫的辅助需要跟上内容，而不是跳过这个包。");

            // ④ 起点自洽的证据：重开之前「拥有 ⇒ 已解锁」本来就成立，重开后的红色才只能归因于重开。
            rowsBeforeRestart += CheckOwnedAreUnlocked(name, engine, "重开之前");

            // ⑤ 重开——§S10 真正要观测的那个动作（"整个游戏唯一的重置入口"）。
            AscensionResult result = engine.Ascend();
            Check.True(result.Success, $"{name}：重开失败——{result.Message}");
            restarted++;

            // ⑥ 重开之后再查一遍。
            rowsAfterRestart += CheckOwnedAreUnlocked(name, engine, "重开之后");
        }

        Check.AtLeast(restarted, 1, "没有任何一个纪元包真的重开过——这条横扫就是空的。");
        Check.AtLeast(
            rowsAfterRestart,
            1,
            "重开之后没有任何一个包还留着建筑——继承若全被关掉，这条守卫就没有观测对象了"
            + "（末世是该走这条路的包，先确认它的 InheritBuildingRatio 还在）。");
        Console.WriteLine(
            $"      重开过的包：{restarted} / {packs.Length}；"
            + $"重开前持有 {rowsBeforeRestart} 行，重开后仍持有并检查 {rowsAfterRestart} 行");
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 取快照，断言「<c>Owned &gt; 0</c> 的每一行都 <c>IsUnlocked</c>」，返回检查过的行数。<para>
    /// 判据与失败措辞照 <c>ApocalypseContentTests.SnapshotAndCheckUnlocked</c>，
    /// 只多带两样诊断信息：哪个包、重开的哪一侧（单包版没有「包」这个概念）。
    /// </para>
    /// </summary>
    /// <param name="packName">内容包显示名（报错信息用）。</param>
    /// <param name="engine">待检查的引擎。</param>
    /// <param name="when">这一侧的人类可读说法，例如「重开之后」。</param>
    private static int CheckOwnedAreUnlocked(string packName, GameEngine engine, string when)
    {
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.BuyMax);
        int rows = 0;

        foreach (BuildingView building in snapshot.Buildings)
        {
            if (building.Owned <= 0) continue;
            rows++;
            Check.True(
                building.IsUnlocked,
                $"{packName}：{when}拥有 {building.Owned} 座「{building.Name}」，它在列表里却是未解锁的"
                + "——产量照算，但列表里看不到、也卖不掉（STRUCTURE_OPTIMIZATION §S10）。");
        }

        return rows;
    }

    /// <summary>
    /// 给「此刻确实解锁」的建筑各发 <see cref="SeededCount"/> 座，返回发了几个建筑。<para>
    /// 直接写 <c>BuildingCounts</c> 而不走购买：价格的指数增长会把「重开之后还剩多少」变成
    /// 与内容数值耦合的东西（新包加一座贵的建筑就可能悄悄变成 0 座），而这条守卫要观测的是
    /// 重开之后的快照，购买路径由 <see cref="OwnedBuildings_StayUnlockedInEveryPack"/> 那趟自然游玩覆盖。
    /// </para>
    /// </summary>
    /// <param name="engine">待播种的引擎。</param>
    private static int SeedUnlockedBuildings(GameEngine engine)
    {
        int seeded = 0;

        foreach (BuildingDefinition building in engine.Content.Buildings)
        {
            if (!engine.IsUnlocked(building.Unlock)) continue;
            engine.State.BuildingCounts[building.Id] = SeededCount;
            seeded++;
        }

        engine.MarkDirty();
        return seeded;
    }

    /// <summary>
    /// 把当前层的完成条件直接补满，好让调用方真的重开一次。<para>
    /// 只覆盖内容实际用到的那三类指标（本轮累计赚取 / 成就数 / 自定义计数器）；
    /// <b>认不出的指标当场失败</b>——静默跳过正是 §S10 要修的那个毛病（「新包不会自动被扫到」）。
    /// </para>
    /// </summary>
    /// <param name="packName">内容包显示名（报错信息用）。</param>
    /// <param name="engine">待补满的引擎。</param>
    private static void SatisfyCurrentEraCompletion(string packName, GameEngine engine)
    {
        GameState state = engine.State;
        EraDefinition current = engine.Content.EraByIndex[state.Era];

        foreach (NumericCondition leaf in current.Completion.NumericLeaves())
        {
            switch (leaf.Metric)
            {
                case NumericMetric.CookiesEarnedThisRun:
                    state.CookiesEarnedThisRun = Math.Max(state.CookiesEarnedThisRun, leaf.Target);
                    break;

                case NumericMetric.AchievementCount:
                    // 成就只能一个一个添（末世那条单包用例也是这么造 dummy 的）。
                    for (int i = state.Achievements.Count; i < leaf.Target && i < DummyAchievementCap; i++)
                        state.Achievements.Add($"__unlock_invariant_{i}");
                    break;

                case NumericMetric.Counter:
                    state.SetCounter(leaf.Id!, Math.Max(state.GetCounter(leaf.Id!), leaf.Target));
                    break;

                default:
                    Check.Fail(
                        $"{packName}：第 {state.Era} 层的完成条件引用了 {leaf.Metric}，"
                        + "这条横扫还不知道怎么满足它——请在 SatisfyCurrentEraCompletion 里补一条，"
                        + "而不是让这个包静默地不被扫到。");
                    break;
            }
        }

        engine.MarkDirty();
    }
}
