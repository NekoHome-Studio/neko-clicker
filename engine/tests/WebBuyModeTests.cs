using System.Diagnostics;
using NekoClicker.Core;
using NekoClicker.Core.Views;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 「点一下买几个」由**会话档位**决定，而不是由命令里的数字决定。<para>
/// <b>为什么非要一条这样的用例</b>：快照里那张建筑卡片上的 <c>batchAmount</c> / <c>batchPrice</c>
/// 是按 <c>GameHost</c> 的档位算出来的，而前端点卡片发出去的命令<b>不带数量</b>
/// （<c>{"type":"buy","id":…}</c>，见 <c>app.js</c> 的 <c>send("buy", { id })</c>）——
/// 数量本就该由服务端定，这是"前端不重实现游戏逻辑"那条规矩的必然结果。
/// 于是"卡片上写的 10 个"与"这一下买几个"是同一个问题的两个面。它们曾经对不上：
/// <c>Program.cs</c> 的 buy 分支只认命令里明写的 <c>amount</c>，没写就退回引擎的默认值 <c>1</c>，
/// 会话档位<b>从来没被问过</b>——2026-10-10 人报的「×10 之后点一下只能买一个」就是它。
/// </para>
/// <para>
/// <b>两条判据缺一不可</b>：买到的<b>个数</b>（此前是 1）与<b>扣的钱</b>（此前是单价）。
/// 只盯个数的话，"买 10 个但按单价收钱"这种更糟的形态会被放过。
/// </para>
/// <para>
/// <b>为什么这条能在 C# 层验</b>：<c>GameHost.cs</c> 以<b>共享源码</b>的形式编进这个程序集
/// （见测试工程的 csproj），所以"宿主接线"这一段不必真起 HTTP 就能断言；
/// HTTP 那一面另有一条真链路的用例（<c>tools/api-test.ps1</c> 的「批量档位」一节）。
/// 两条都要：这一条钉的是会话对象自己认不认档位，那一条钉的是命令真的走到了这里。
/// </para>
/// <para>
/// 全部走 <c>saveRoot: null</c> / <c>latencyLogPath: null</c>：仓库里的 <c>saves/</c> 与
/// <c>artifacts/latency.txt</c> 都是真人数据，这些用例一个字节都不写它们。
/// </para>
/// </summary>
public static class WebBuyModeTests
{
    /// <summary>
    /// ×10：点一下真的买 10 个，而且扣的就是卡片上那个总价。<para>
    /// 命令**不带数量**——与前端逐字同形。数量若由用例自己给，这条用例就绕开了被测的那条路。
    /// </para>
    /// </summary>
    [Test]
    public static void BuyTenMode_BuysTenAndChargesTheCardPrice()
    {
        using var host = new Scope();
        host.Fund(1_000_000);
        host.SetMode(PurchaseMode.Buy10);

        Reading before = host.Take(PurchaseMode.Buy10);
        Check.Equal(10, before.Row.BatchAmount, "前置条件：×10 档位下这张卡片给出的整批数量");
        Check.True(before.Row.IsUnlocked, "前置条件：这座建筑开局就解锁（否则买不成）");

        var wall = Stopwatch.StartNew();
        CommandOutcome outcome = host.Buy(); // == 前端：send("buy", { id })（没有数量）
        wall.Stop();
        Reading after = host.Take(PurchaseMode.Buy10);

        Check.Equal(
            10,
            after.Row.Owned - before.Row.Owned,
            "×10 档位下点一下买到的个数——此前是 1（命令里没写 amount，引擎的默认值接管了）");
        Check.Contains(outcome.Message, "购买 10 个", "命令回话里的个数（引擎自己说的）");

        // 实付 = 买之前的钱 - 买之后的钱，再放掉这段时间的**自然产量**：引擎线程一直在跑，
        // 两次读之间它会按 Cps 进账。要用**买之后**的产量算容忍量——买完这一下产量才刚涨起来
        // （实测：买 10 座 curled_cat 之后 Cps 从 0 变成 1，紧接着的 30Hz 那一拍就进账 0.033）。
        // 多放一拍（1/30 s）是引擎的固定步长，进账只发生在整拍上。
        double spent = before.Cookies - after.Cookies;
        double peakCps = Math.Max(before.Cps, after.Cps);
        double tolerance = peakCps * (wall.Elapsed.TotalSeconds + 1.0 / 30.0) * 2 + before.Row.BatchPrice * 1e-9;

        Check.Close(
            before.Row.BatchPrice,
            spent,
            tolerance,
            $"实付必须是卡片上那个总价（batchPrice={before.Row.BatchPrice}），"
            + $"而不是单价（unitPrice={before.Row.UnitPrice}）；容忍 {tolerance} 的自然产量");
    }

    /// <summary>
    /// 买满：点一下买到的个数 = 卡片上那个 <c>batchAmount</c>（由预算算出来的那个数），
    /// 而且不是 1。这一条与 ×10 分开写，是因为它们的数量来自<b>两条不同的分支</b>
    /// （固定档位 vs <c>0</c> = 买到买不起为止）。
    /// </summary>
    [Test]
    public static void BuyMaxMode_BuysAsManyAsTheCardAdvertises()
    {
        using var host = new Scope();
        host.Fund(1_000_000);
        host.SetMode(PurchaseMode.BuyMax);

        Reading before = host.Take(PurchaseMode.BuyMax);
        Check.Greater(before.Row.BatchAmount, 1, "前置条件：买满档位下这张卡片给出的个数应当不止 1");

        host.Buy(); // 与前端同形：不带数量
        Reading after = host.Take(PurchaseMode.BuyMax);

        Check.Equal(
            before.Row.BatchAmount,
            after.Row.Owned - before.Row.Owned,
            "买满档位买到的个数必须等于卡片上写的那个数（此前是 1）");
    }

    /// <summary>
    /// 命令里明写的数量仍然优先于档位。<para>
    /// 这不是顺手加的：<c>amount</c> 是"买 N 个"这种直接请求的入口，
    /// <c>tools/api-test.ps1</c> 的钳位用例（<c>amount=100000</c>）就走它——
    /// 档位只能当**没写数量时的默认值**，不能反过来把明写的数压掉。
    /// </para>
    /// </summary>
    [Test]
    public static void ExplicitAmount_WinsOverTheMode()
    {
        using var host = new Scope();
        host.Fund(1_000_000);
        host.SetMode(PurchaseMode.Buy10);

        Reading before = host.Take(PurchaseMode.Buy10);
        host.Buy(amount: 3);
        Reading after = host.Take(PurchaseMode.Buy10);

        Check.Equal(3, after.Row.Owned - before.Row.Owned, "明写 3 个就买 3 个（档位是 ×10 也不改它）");
    }

    /// <summary>
    /// 卖档 + 买命令：仍然是 1 个（改动前的行为）。<para>
    /// 这是一条<b>形状守卫而不是活路径</b>：Web 界面的档位行只有四个<b>买</b>档
    /// （<c>app.js</c> 的 <c>renderBatch</c> 里那四个 token），所以这个组合今天到不了。
    /// 它守的是"别顺手把卖档也塞进买命令的语义里"——<c>SellMax.RequestedAmount()</c> 是 <c>0</c>，
    /// 那在买命令里会变成"买到买不起为止"，与"卖光"正好相反。
    /// </para>
    /// </summary>
    [Test]
    public static void SellMode_KeepsBuyingOne()
    {
        using var host = new Scope();
        host.Fund(1_000_000);
        host.SetMode(PurchaseMode.Sell10);

        Reading before = host.Take(PurchaseMode.Sell10);
        host.Buy();
        Reading after = host.Take(PurchaseMode.Sell10);

        Check.Equal(1, after.Row.Owned - before.Row.Owned, "卖档下的买命令仍然是 1 个（卖档该由卖命令去认）");
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>一轮里读下来的东西：那张卡片、钱、当前产量。</summary>
    private sealed record Reading(BuildingView Row, double Cookies, double Cps);

    /// <summary>
    /// 一台跑着的 Web 会话（不落盘）。<para>
    /// 断言尽量只落在<b>整数</b>上：引擎线程从构造那一刻就在推进，钱每毫秒都在变。
    /// </para>
    /// </summary>
    private sealed class Scope : IDisposable
    {
        private readonly GameHost _host;

        public Scope()
        {
            var package = new WebPackage("neko", "猫咖物语", () => TestGame.NekoContent, "已载入。");
            // saveRoot: null —— 不落盘；latencyLogPath: null —— artifacts/latency.txt 是真人数据。
            _host = new GameHost(package, saveRoot: null, seed: 7, latencyLogPath: null);
            BuildingId = OnEngine(engine => engine.Content.BuildingById.Keys.First());
            Check.Greater(BuildingId.Length, 0, "前置条件：这个包至少有一座建筑。");
        }

        public string BuildingId { get; }

        /// <summary>灌一笔钱，并把持有数清零（⇒ 产量 0，两次读之间钱不会自己涨）。</summary>
        public void Fund(double cookies)
            => Edit(engine =>
            {
                engine.State.BuildingCounts.Clear();
                engine.State.Cookies = cookies;
                engine.MarkDirty();
            });

        public void SetMode(PurchaseMode mode) => _host.SetModeAsync(mode).GetAwaiter().GetResult();

        /// <summary>发一条<b>与前端逐字同形</b>的买命令：只有 id，没有数量（除非用例明写）。</summary>
        public CommandOutcome Buy(int amount = 0)
            => _host.BuyAsync(BuildingId, amount).GetAwaiter().GetResult();

        /// <summary>在<b>同一轮</b>里读下卡片、钱与产量（分两轮读，中间会被 tick 插进来）。</summary>
        public Reading Take(PurchaseMode mode)
            => OnEngine(engine =>
            {
                GameSnapshot snapshot = engine.Snapshot(mode);
                BuildingView row = snapshot.Buildings.First(b => b.Id == BuildingId);
                return new Reading(row, snapshot.Cookies, snapshot.CookiesPerSecond);
            });

        public void Dispose() => _host.DisposeAsync().AsTask().GetAwaiter().GetResult();

        /// <summary>在游戏线程上读一次（引擎只有一个主人，读也要走那条线程）。</summary>
        private T OnEngine<T>(Func<GameEngine, T> read)
        {
            T value = default!;
            _host.ExecuteAsync(engine =>
            {
                value = read(engine);
                return new CommandOutcome(true, string.Empty, _host.Seq);
            }).GetAwaiter().GetResult();
            return value;
        }

        private void Edit(Action<GameEngine> action) => OnEngine(engine =>
        {
            action(engine);
            return true;
        });
    }
}
