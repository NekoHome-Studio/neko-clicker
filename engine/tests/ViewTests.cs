using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>UI 只读视图层。</summary>
public static class ViewTests
{
    [Test]
    public static void Snapshot_ContainsAllContentRows()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        GameSnapshot snapshot = engine.Snapshot(PurchaseMode.Buy10);

        Check.Equal(engine.Content.Buildings.Count, snapshot.Buildings.Count);
        Check.Equal(engine.Content.Upgrades.Count, snapshot.Upgrades.Count);
        Check.Equal(engine.Content.Achievements.Count, snapshot.Achievements.Count);
        Check.Equal("猫咖物语", snapshot.Title);
        Check.Equal("小鱼干", snapshot.CurrencyName);
        Check.Equal("撸猫", snapshot.ClickActionName);
        Check.Equal(PurchaseMode.Buy10, snapshot.Mode);
    }

    [Test]
    public static void Snapshot_FormatsNumbersForDisplay()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 2_500_000;
        engine.MarkDirty();

        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal("2.5 million", snapshot.CookiesText);
        Check.Equal("0", snapshot.CpsText);
        Check.Equal("1", snapshot.ClickPowerText);
    }

    [Test]
    public static void Snapshot_ReportsNextMilestone()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        GameSnapshot snapshot = engine.Snapshot();

        BuildingView cat = snapshot.Buildings.First(b => b.Id == "curled_cat");

        Check.Equal(0, cat.Owned);
        Check.True(cat.IsUnlocked);
        Check.Equal(1, cat.NextMilestoneAt, "0 个时应指向第一个里程碑（需要 1 个）。");
        Check.NotNull(cat.NextMilestoneName);
    }

    [Test]
    public static void Snapshot_MilestoneAdvancesWithOwnership()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);

        BuildingView cat = engine.Snapshot().Buildings.First(b => b.Id == "curled_cat");

        Check.Equal(25, cat.NextMilestoneAt, "已有 10 个时应指向 25 个的里程碑。");
    }

    [Test]
    public static void Snapshot_BuyMaxReportsAffordableBatch()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 100;
        engine.MarkDirty();

        BuildingView cat = engine.Snapshot(PurchaseMode.BuyMax).Buildings.First(b => b.Id == "curled_cat");

        Check.True(cat.CanAfford);
        Check.AtLeast(cat.BatchAmount, 1);
        Check.AtMost(cat.BatchPrice, 100 + 1e-9);
    }

    /// <summary>
    /// 批量档位下 <see cref="BuildingView.BatchPrice"/> 是**整批的总价**，不是单价。<para>
    /// 它钉的是一个"两个宿主都赖以为生、却一直没有用例正面说过"的事实：Web 建筑卡片上那个数
    /// （`batchPrice`，批量档位下还写明「总价」，见 `tools/web-smoke.mjs` §27）与终端详情面板的
    /// 「本次花费」（`TerminalUi.DetailLine`）都是服务端算好的这**一个**数——两个前端一个乘号都不做。
    /// 它错了的话，两个宿主会一起把"单价"说成"总价"，而今天没有任何东西会因此变红。
    /// </para>
    /// <para>
    /// 对法用的是**逐个累加未来 N 个的单价**：与 <see cref="Pricing.BulkPrice"/> 的闭式解
    /// （等比数列求和）是两条独立的路，顺带把"整批价 **不是** 单价 ×N"也钉住了——
    /// 价格曲线是等比增长，第 10 个比第 1 个贵。
    /// </para>
    /// </summary>
    [Test]
    public static void Snapshot_BatchModePricesTheWholeBatch()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 3); // 持有数 > 0，价格曲线才拉得开

        BuildingView one = engine.Snapshot(PurchaseMode.Buy1).Buildings.First(b => b.Id == "curled_cat");
        BuildingView ten = engine.Snapshot(PurchaseMode.Buy10).Buildings.First(b => b.Id == "curled_cat");
        BuildingView hundred = engine.Snapshot(PurchaseMode.Buy100).Buildings.First(b => b.Id == "curled_cat");

        Check.Equal(1, one.BatchAmount);
        Check.Equal(10, ten.BatchAmount);
        Check.Equal(100, hundred.BatchAmount);

        // 单价不随档位变：跟着档位变的是"这一批要几个、一共多少钱"。
        Check.CloseRelative(one.UnitPrice, ten.UnitPrice, 1e-12, "单价不该跟着档位变。");
        Check.CloseRelative(one.UnitPrice, hundred.UnitPrice, 1e-12, "单价不该跟着档位变。");

        BuildingDefinition definition = engine.Content.BuildingById["curled_cat"];
        double multiplier = one.UnitPrice / Pricing.UnitPrice(definition, one.Owned, 1.0);

        double byHand = 0;
        for (int i = 0; i < 10; i++) byHand += Pricing.UnitPrice(definition, one.Owned + i, multiplier);
        Check.CloseRelative(byHand, ten.BatchPrice, 1e-9, "×10 的批价不是这 10 个各自单价之和。");

        Check.Greater(ten.BatchPrice, one.UnitPrice * 10, "×10 的批价不该等于（或小于）单价 ×10。");
        Check.Greater(hundred.BatchPrice, ten.BatchPrice * 10, "×100 的批价不该等于（或小于）×10 的十倍。");
    }

    [Test]
    public static void Snapshot_SellModeReportsRefund()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 5);

        BuildingView cat = engine.Snapshot(PurchaseMode.Sell1).Buildings.First(b => b.Id == "curled_cat");

        Check.True(cat.CanAfford, "出售模式下 CanAfford 表示有货可卖。");
        Check.Equal(1, cat.BatchAmount);
        Check.Greater(cat.BatchPrice, 0);
        Check.CloseRelative(15 * Math.Pow(1.15, 4) * 0.5, cat.BatchPrice, 1e-9);
    }

    [Test]
    public static void Snapshot_UpgradeRowsExposeAffordabilityAndEffects()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        for (int i = 0; i < 10; i++) engine.Click();
        engine.State.Cookies = 1_000;
        engine.MarkDirty();

        UpgradeView warmer = engine.Snapshot().Upgrades.First(u => u.Id == "warmer_hands");

        Check.True(warmer.IsUnlocked);
        Check.True(warmer.CanAfford);
        Check.False(warmer.IsMaxed);
        Check.Contains(warmer.EffectSummary, "点击收益");
    }

    [Test]
    public static void Snapshot_BuildingSharesSumToOne()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 10);
        engine.BuyBuilding("scratching_post", 10);
        engine.BuyBuilding("cat_bed", 10);

        GameSnapshot snapshot = engine.Snapshot();
        double totalShare = snapshot.Buildings.Sum(b => b.CpsShare);

        Check.Close(1.0, totalShare, 1e-9);
    }

    [Test]
    public static void Snapshot_BuffRowsCarryProgress()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.ApplyBuff("frenzy", 100);
        engine.Simulate(25);

        BuffView buff = engine.Snapshot().Buffs.Single();

        Check.Equal("frenzy", buff.Id);
        Check.Close(75, buff.RemainingSeconds, 1e-6);
        Check.True(buff.Progress is > 0.7 and < 0.8, $"进度应约为 75%，实际 {buff.Progress}");
        Check.False(buff.IsDebuff);
    }

    [Test]
    public static void Snapshot_AchievementProgressIsQuantified()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.BuyBuilding("curled_cat", 5);

        AchievementView next = engine.Snapshot().Achievements.First(a => a.Id == "curled_cat_x25");

        Check.False(next.Unlocked);
        Check.Close(0.2, next.Progress, 1e-6); // 5 / 25
        Check.Contains(next.ProgressText, "/ 25");
    }

    [Test]
    public static void Snapshot_PrestigePreviewIsPresent()
    {
        GameEngine engine = TestGame.CreateNeko(out _);
        engine.State.CookiesEarnedAllTime = 4e12;
        engine.MarkDirty();

        GameSnapshot snapshot = engine.Snapshot();

        Check.Equal(0, snapshot.Prestige.CurrentLevel);
        Check.Equal(1, snapshot.Prestige.NextLevel);
        Check.True(snapshot.Prestige.CanAscend);
    }

    /// <summary>
    /// 升级行必须自己说清"用哪个钱包付钱、货币叫什么、图标是哪个"。<para>
    /// 这条守的是一个**静默的**失败：前端原本靠 <c>currency === 1</c>（枚举序数）来选钱包，
    /// 而序数一错，表现是"买得起的行灰着、买不起的行亮着"，两边都不报错、测试也不会红。
    /// 现在权威值是服务端给的旗子与文本，这里逐项钉住它们与内容包一致。
    /// </para>
    /// </summary>
    [Test]
    public static void Snapshot_UpgradeRowsCarryCurrencySemantics()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);
        engine.State.PrestigeChips = 100; // 永久线按"持有多少转生货币"逐条解锁
        engine.MarkDirty();

        GameSnapshot snapshot = engine.Snapshot();

        List<UpgradeView> permanent = [.. snapshot.Upgrades.Where(u => u.IsPermanent)];
        Check.AtLeast(permanent.Count, 1, "示例包应当有一条永久升级线——否则下面几条断言是空的。");

        foreach (UpgradeView row in permanent)
        {
            Check.True(row.UsesPrestigeCurrency, $"永久升级「{row.Id}」应当花转生货币。");
            Check.Equal(snapshot.PrestigeCurrencyIcon, row.CurrencyIcon, $"「{row.Id}」的货币图标应取转生货币。");
            Check.Equal(snapshot.PrestigeCurrencyName, row.CurrencyName, $"「{row.Id}」的货币名应取转生货币。");
            Check.True(row.IsUnlocked, $"持有 100 点转生货币时「{row.Id}」应当已解锁（门槛最高 30）。");
            Check.True(row.CanAfford, $"持有 100 点时「{row.Id}」应当买得起。");
        }

        UpgradeView ordinary = snapshot.Upgrades.First(u => !u.IsPermanent);
        Check.False(ordinary.UsesPrestigeCurrency, $"普通升级「{ordinary.Id}」不该花转生货币。");
        Check.Equal(snapshot.CurrencyIcon, ordinary.CurrencyIcon, $"「{ordinary.Id}」的货币图标应取普通货币。");
        Check.Equal(snapshot.CurrencyName, ordinary.CurrencyName, $"「{ordinary.Id}」的货币名应取普通货币。");
    }

    /// <summary>
    /// 横扫十一个包：每一行的货币语义都必须与内容包自洽（不给前端留下"要自己猜"的字段）。<para>
    /// 与 <see cref="PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun"/> 的分工：
    /// 那条守**数值能不能买到**（内容平衡），这条守**前端能不能认出来**（视图契约）。
    /// 后者是 Web 上那条「永久」面板存在的全部依据。
    /// </para>
    /// </summary>
    [Test]
    public static void UpgradeRows_ReportTheRightWalletForEveryPack()
    {
        int packsWithPermanentLine = 0;

        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            GameEngine engine = TestGame.Create(content);
            engine.State.PrestigeChips = 1000;
            engine.MarkDirty();
            GameSnapshot snapshot = engine.Snapshot();

            List<UpgradeView> permanent = [.. snapshot.Upgrades.Where(u => u.IsPermanent)];
            if (permanent.Count > 0) packsWithPermanentLine++;

            foreach (UpgradeView row in snapshot.Upgrades)
            {
                // ① 旗子必须与内容包的计价货币一致（前端只认旗子，不认序数）
                Check.Equal(
                    row.Currency == UpgradeCurrency.PrestigeChips,
                    row.UsesPrestigeCurrency,
                    $"{name}：「{row.Id}」的 UsesPrestigeCurrency 与 Currency 不一致。");

                // ② 货币名与图标必须非空，且等于它所该取的那一对
                string expectedIcon = row.UsesPrestigeCurrency ? snapshot.PrestigeCurrencyIcon : snapshot.CurrencyIcon;
                string expectedName = row.UsesPrestigeCurrency ? snapshot.PrestigeCurrencyName : snapshot.CurrencyName;
                Check.Equal(expectedIcon, row.CurrencyIcon, $"{name}：「{row.Id}」的货币图标不对。");
                Check.Equal(expectedName, row.CurrencyName, $"{name}：「{row.Id}」的货币名不对。");
            }

            // ③ 永久线必须全是转生货币（内容构建期已强制，这里是"前端看得见的那一面"的复核）
            foreach (UpgradeView row in permanent)
            {
                Check.True(row.UsesPrestigeCurrency, $"{name}：永久升级「{row.Id}」居然用普通货币计价。");
            }
        }

        Check.AtLeast(packsWithPermanentLine, 1, "一个带永久线的包都没有——这条横扫就是空的。");
        Console.WriteLine($"      带永久升级线的包：{packsWithPermanentLine} / {TestGame.AllContentPacks().Length}");
    }

    [Test]
    public static void PurchaseModeHelpers_BehaveConsistently()
    {
        Check.True(PurchaseMode.Sell10.IsSell());
        Check.False(PurchaseMode.Buy10.IsSell());
        Check.Equal(0, PurchaseMode.BuyMax.RequestedAmount());
        Check.Equal(10, PurchaseMode.Buy10.RequestedAmount());
        Check.Equal(PurchaseMode.Buy100, PurchaseMode.Buy10.NextBuy());
        Check.Equal(PurchaseMode.Buy1, PurchaseMode.BuyMax.NextBuy());
        Check.Equal(PurchaseMode.Sell1, PurchaseMode.Buy1.ToggleSell());
        Check.Equal(PurchaseMode.Buy10, PurchaseMode.Sell10.ToggleSell());
    }
}
