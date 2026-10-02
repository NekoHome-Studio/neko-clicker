using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// Web 前端的线上协议（<see cref="SnapshotProtocol"/>）契约。<para>
/// 这份协议是"引擎 → 浏览器"之间唯一的数据契约，而它的失效方式全是**沉默**的：
/// 少合并一个字段，前端就永远显示旧值；把 <c>null</c> 当成"没变化"跳过，
/// 纪元/图鉴/立场/结局面板就会在结束后一直挂着。所以这里逐条钉住。
/// </para>
/// <para>
/// <b>最要紧的是第一条</b>：它证明"用增量帧更新一份快照"与"直接取一份全量快照"
/// 逐字节等价。只要它还绿，前端就不可能在长时间的增量流里累积出偏差。
/// </para>
/// </summary>
public sealed class WebSnapshotProtocolTests
{
    /// <summary>把一份快照序列化成前端会持有的那种 JSON 对象。</summary>
    private static JsonObject ToJson(GameSnapshot snapshot)
        => JsonNode.Parse(SnapshotProtocol.Serialize(snapshot)) as JsonObject
           ?? throw new AssertionException("快照没能序列化成 JSON 对象。");

    /// <summary>
    /// 一帧增量合并进快照之后，得到的必须是**与全量逐字节相同**的 JSON。<para>
    /// 这是"前端可以只靠增量帧活下去"这条主张的可执行版本。
    /// </para>
    /// </summary>
    [Test]
    public void DeltaMergedIntoSnapshot_EqualsTheFullSnapshot()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        TestGame.BuyGreedily(engine);

        JsonObject held = ToJson(engine.Snapshot(PurchaseMode.Buy1));
        string prefix = SnapshotProtocol.Serialize(held);

        // 走一段真实游玩，每一步都按"只推变化字段"来更新前端手里的那份
        for (int i = 0; i < 60; i++)
        {
            GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
            engine.Update(1.0 / 30);
            GameSnapshot after = engine.Snapshot(PurchaseMode.Buy1);

            SnapshotProtocol.Delta step = SnapshotProtocol.Diff(before, after);
            string delta = SnapshotProtocol.DeltaEnvelope(i + 1, step);
            SnapshotProtocol.ApplyDelta(held, delta, step);
        }

        string rebuilt = SnapshotProtocol.Serialize(SnapshotProtocol.Comparable(held));
        string fresh = SnapshotProtocol.Serialize(SnapshotProtocol.Comparable(ToJson(engine.Snapshot(PurchaseMode.Buy1))));

        Check.Equal(fresh, rebuilt, "增量帧累积出的快照与直接取的全量快照不一致——前端会长期显示错的值。");
        Check.NotEqual(prefix, SnapshotProtocol.Serialize(held), "60 个 tick 之后快照居然一个字节都没变，这条用例就失去判别力了。");
    }

    /// <summary>挂机时每 tick 真正变化的顶层字段应当<b>极少</b>——这正是选增量协议的理由。</summary>
    [Test]
    public void IdleDelta_IsTiny()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out _);

        // 先玩一会儿再观测：开局第一 tick 什么都在变，量不出稳态
        for (int i = 0; i < 600; i++) engine.Update(1.0 / 30);
        Check.Greater(engine.State.PlayTimeSeconds, 0, "引擎没在推进，后面的观测没有意义。");

        GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
        engine.Update(1.0 / 30);
        GameSnapshot after = engine.Snapshot(PurchaseMode.Buy1);

        JsonObject changed = SnapshotProtocol.Diff(before, after).Changed;
        string deltaJson = SnapshotProtocol.DeltaEnvelope(1, changed);
        int fullBytes = SnapshotProtocol.Serialize(after).Length;

        Check.True(
            changed.ContainsKey("playTimeSeconds"),
            $"游玩时长没变，说明引擎没在推进（变化的字段：{string.Join('、', changed.Select(p => p.Key))}）。");

        // 稳态下一个 tick 只该动时间驱动的字段。给一点余量（成就检查、金猫刷新会偶尔带出别的）。
        Check.AtMost(
            changed.Count,
            4,
            $"挂机时一个 tick 变了 {changed.Count} 个顶层字段——增量协议的前提不成立了：" +
            $"{string.Join('、', changed.Select(p => p.Key))}");

        // 增量帧必须比全量小一个数量级以上，否则这套协议没有意义。
        Check.AtMost(
            deltaJson.Length * 10,
            fullBytes,
            $"增量帧 {deltaJson.Length} 字节、全量 {fullBytes} 字节，差距不足以支撑增量协议。");
    }

    /// <summary>字段名必须是 camelCase——前端 JS 直接按这个名字取值，改了就静默失效。</summary>
    [Test]
    public void FieldNames_AreCamelCase()
    {
        JsonObject json = ToJson(TestGame.CreateNineLives(out _).Snapshot(PurchaseMode.Buy1));

        // 前端真的会读的几个（缺一个就有一个面板永远空着）
        foreach (string key in new[]
                 {
                     "title", "currencyName", "currencyIcon", "clickActionName",
                     "cookies", "cookiesPerSecond", "clickPower",
                     "cookiesText", "cpsText", "clickPowerText",
                     "achievementCount", "achievementTotal",
                     "buildings", "upgrades", "achievements", "buffs", "goldenCookies",
                     "notifications", "prestige", "era", "codex", "pendingChoices",
                 })
        {
            Check.True(json.ContainsKey(key), $"快照 JSON 里没有 <{key}>，前端对应的那块会永远空着。");
        }

        // 序列化不能带缩进：那是给机器读的，缩进只会让体积涨两三成。
        Check.False(
            SnapshotProtocol.Serialize(json).Contains('\n'),
            "线上序列化带了换行——WriteIndented 应该是 false。");
    }

    /// <summary>
    /// 可空字段从"有"变"无"时，增量帧必须把它写成 <c>null</c>，
    /// 而不是当成"没变化"跳过——否则前端会把纪元/图鉴面板一直挂在那儿。
    /// </summary>
    [Test]
    public void NullPatch_IsAppliedRatherThanSkipped()
    {
        var held = new JsonObject { ["era"] = new JsonObject { ["index"] = 3 } };
        var changed = new JsonObject { ["era"] = null };

        IReadOnlyList<string> applied = SnapshotProtocol.Merge(held, changed);

        Check.Equal(1, applied.Count, "null 补丁应当被算作一次修改。");
        Check.True(held.ContainsKey("era"), "字段应当还在（值为 null），而不是被整个删掉。");
        Check.Null(held["era"]?.ToJsonString(), "era 应当是 null。");
    }

    /// <summary>
    /// 离线报告要真的走完"出现 → 播报 → 消失"这一整条协议路径。<para>
    /// 它是**一次性播报**的字段：播报之前必须一直待在全量帧里（前端一刷新就再取一份全量，
    /// 靠"推过一次就算数"是收不到的），播报之后必须以 <c>null</c> 补丁到前端——
    /// 中间任何一步缺失，玩家要么看不到自己离线赚了多少，要么那张卡永远关不掉。
    /// </para>
    /// </summary>
    [Test]
    public void OfflineReport_ArrivesThenLeavesAsANullPatch()
    {
        GameEngine engine = TestGame.CreateNineLivesFunded(out ManualClock clock);
        TestGame.BuyGreedily(engine);
        Check.Greater(engine.CookiesPerSecond, 0, "前置条件：产量为 0 就补发不出任何收益，这条用例会变成橡皮图章。");
        string json = engine.Save();

        clock.Advance(4 * 3600);
        engine.Load(json);

        GameSnapshot withReport = engine.Snapshot(PurchaseMode.Buy1);
        JsonObject full = ToJson(withReport);
        Check.True(full["offline"] is JsonObject, "读档补发之后，全量帧里应带上待播报的离线报告。");
        var report = full["offline"] as JsonObject;
        if (report is null) return;

        foreach (string field in new[] { "cookiesGained", "creditedSeconds", "elapsedSeconds", "wasCapped", "durationText", "cookiesText" })
        {
            Check.True(report.ContainsKey(field), $"离线报告缺字段 <{field}>（前端按 camelCase 取，缺了就静默显示不出来）。");
        }
        Check.Greater(report["cookiesGained"]!.GetValue<double>(), 0, "补发的货币应当大于 0。");

        // 播报之后：字段还在（值为 null），差别在于值
        engine.DismissOfflineProgress();
        GameSnapshot afterDismiss = engine.Snapshot(PurchaseMode.Buy1);
        Check.Null(ToJson(afterDismiss)["offline"], "播报之后全量帧里的 offline 应当是 null 而不是整个字段消失。");

        SnapshotProtocol.Delta delta = SnapshotProtocol.Diff(withReport, afterDismiss);
        Check.True(
            delta.Changed.ContainsKey("offline"),
            $"播报之后增量帧应当显式带上 offline——否则前端那张卡永远关不掉（实际带的字段：{string.Join('、', delta.Changed.Select(p => p.Key))}）。");
        Check.Null(delta.Changed["offline"], "offline 的补丁值应当是 null。");
    }

    /// <summary>增量帧里没提到的字段不能被改动——"没变化"与"变成空"必须区分得开。</summary>
    [Test]
    public void DeltaLeavesUnmentionedFieldsAlone()
    {
        var held = new JsonObject { ["cookies"] = 5.0, ["title"] = "猫咖物语" };
        var changed = new JsonObject { ["cookies"] = 9.0 };

        SnapshotProtocol.Merge(held, changed);

        Check.Equal(9d, held["cookies"]!.GetValue<double>(), "cookies 应当被更新。");
        Check.Equal("猫咖物语", held["title"]!.GetValue<string>(), "title 没在增量里，不该被动。");
    }

    /// <summary>
    /// 真的跨几层纪元走一遍，验证"结构变了"时增量帧依然能还原。
    /// 上面那条用的是同一层的连续 tick；这条覆盖跨层、列表长度变化、新面板出现
    /// （图鉴会长出新条目、通知会滚、纪元总览会长一格）。
    /// </summary>
    [Test]
    public void DeltaSurvivesEraAdvance()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        JsonObject held = ToJson(engine.Snapshot(PurchaseMode.Buy1));
        int erasAdvanced = 0;

        // 不靠"跑得够久"来撞纪元门槛：第 2 命的门槛是「累计 1000 万 **且**解锁 4 个成就」，
        // 而 AllCondition 的进度取的是最落后的子条件——纯挂机的机器人在成就数上要爬十几小时。
        // 这条用例要验的是**协议**，不是内容曲线，所以直接给钱把结构变化推出来。
        for (int era = 0; era < 2; era++)
        {
            engine.State.Cookies = 1e9;
            engine.State.CookiesEarnedThisRun = Math.Max(engine.State.CookiesEarnedThisRun, 1e9);
            engine.MarkDirty();

            for (int step = 0; step < 600 && !engine.EraGate.CanAdvance; step++)
            {
                GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
                engine.Simulate(5);
                TestGame.BuyGreedily(engine);
                SnapshotProtocol.Delta diff = SnapshotProtocol.Diff(before, engine.Snapshot(PurchaseMode.Buy1));
                SnapshotProtocol.ApplyDelta(held, SnapshotProtocol.DeltaEnvelope(step, diff), diff);
            }

            if (!engine.EraGate.CanAdvance) break;

            GameSnapshot preAscend = engine.Snapshot(PurchaseMode.Buy1);
            Check.True(engine.Ascend().Success, $"第 {era + 1} 次舍命没成功。");
            erasAdvanced++;
            SnapshotProtocol.Delta ascendDiff = SnapshotProtocol.Diff(preAscend, engine.Snapshot(PurchaseMode.Buy1));
            SnapshotProtocol.ApplyDelta(held, SnapshotProtocol.DeltaEnvelope(999, ascendDiff), ascendDiff);
        }

        // 只要求两层：这条用例要证明的是"跨纪元时增量帧能还原"（新纪元文字、图鉴长条目、
        // 纪元总览多一格、通知滚掉旧的），不是走完九命。层数定得越高，越是在测内容曲线
        // 而不是协议——那会让内容调参把它弄红。
        Check.AtLeast(erasAdvanced, 2, "这条用例需要真的推进两层纪元，但没走到。");

        string rebuilt = SnapshotProtocol.Serialize(SnapshotProtocol.Comparable(held));
        string fresh = SnapshotProtocol.Serialize(SnapshotProtocol.Comparable(ToJson(engine.Snapshot(PurchaseMode.Buy1))));
        Check.Equal(fresh, rebuilt, "跨纪元之后增量还原出的快照与全量不一致。");
    }

    /// <summary>
    /// "买得起 / 买不起"的翻转**不该**让整个行列表进增量帧。<para>
    /// 这不是一条优化建议，是增量协议能不能成立的分水岭：实测把 <c>canAfford</c> 算进比较时，
    /// 48 条升级各自的状态随金钱增长不停翻转，每次推送都带上整个 ~28 KB 的 <c>upgrades</c>，
    /// 于是"全量 47 KB vs 增量 28 KB"——只省 40%，协议等于白做。
    /// </para>
    /// <para>
    /// 判别力是构造出来的：故意先造出"只差一点点钱"的局面，让下一 tick 必然跨过门槛。
    /// </para>
    /// </summary>
    [Test]
    public void AffordabilityFlip_DoesNotResendTheWholeList()
    {
        GameEngine engine = TestGame.CreateNeko(out _);

        // 开局一条升级都没解锁，所以先把经济推起来：给钱 → 贪心买 → 再给钱，
        // 直到出现一条"用主货币计价、还没买满"的升级。
        UpgradeView? target = null;
        for (int round = 0; round < 40 && target is null; round++)
        {
            engine.State.Cookies = 1e6;
            engine.State.CookiesEarnedThisRun = 1e6;
            engine.MarkDirty();
            TestGame.BuyGreedily(engine);

            target = engine.Snapshot(PurchaseMode.Buy1).Upgrades
                .FirstOrDefault(u => u.IsUnlocked && !u.IsMaxed && u.Currency == UpgradeCurrency.Cookies);
        }

        Check.NotNull(target, "推了 40 轮经济还是没有可买的升级，这条用例没有判别力。");

        // 摆出"刚好差一点点钱"的局面，让下一 tick 必然跨过门槛
        engine.State.Cookies = target!.Price * 0.999;
        engine.MarkDirty();

        GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
        Check.False(
            before.Upgrades.First(u => u.Id == target.Id).CanAfford,
            "设定失败：这一刻它应当还买不起。");

        engine.State.Cookies = target.Price * 1.001;
        engine.MarkDirty();
        GameSnapshot after = engine.Snapshot(PurchaseMode.Buy1);
        Check.True(
            after.Upgrades.First(u => u.Id == target.Id).CanAfford,
            "设定失败：这一刻它应当已经买得起了（否则下面的断言测不到东西）。");

        JsonObject changed = SnapshotProtocol.Diff(before, after).Changed;

        Check.False(
            changed.ContainsKey("upgrades"),
            "只是「买得起」翻转了一下，整个 upgrades 数组就被重发了——" +
            "canAfford 是派生量（price <= cookies），不该进增量比较，见 SnapshotProtocol.ExcludedFromDelta。");

        // 全量帧里它仍然必须有权威值：前端首次渲染要靠它
        JsonObject full = ToJson(after);
        Check.True(
            (full["upgrades"] as JsonArray)!.Any(u => u!["canAfford"] is not null),
            "全量快照里必须带 canAfford——前端靠它做首次渲染。");
    }

    /// <summary>增量比较必须能识别<b>嵌套列表内部</b>的变化（建筑买了一个、成就解锁了一个）。</summary>
    [Test]
    public void DiffDetectsNestedListChanges()
    {
        GameEngine engine = TestGame.CreateNekoFunded(out _);

        GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
        engine.BuyBuilding(before.Buildings[0].Id);
        GameSnapshot after = engine.Snapshot(PurchaseMode.Buy1);

        JsonObject changed = SnapshotProtocol.Diff(before, after).Changed;
        Check.True(
            changed.ContainsKey("buildings"),
            $"买了一个建筑之后 buildings 没被判为变化（变了的是：{string.Join('、', changed.Select(p => p.Key))}）。");
        Check.True(changed.ContainsKey("cookies"), "买了建筑之后货币数量应当变化。");
    }

    /// <summary>
    /// 量化判定本身：第 5 位小数以后的差别不算变化，第 3 位的差别要算。<para>
    /// 先单独验这一条，是因为"派生进度每帧漂移导致整个数组重发"的修法全靠它——
    /// 如果量化没生效，后面那条按字节数的用例只会报"增量太大"，不会告诉你为什么。
    /// </para>
    /// </summary>
    [Test]
    public void QuantizedFields_IgnoreDriftBelowPrecision_ButCatchRealChanges()
    {
        var engine = TestGame.CreateNeko(out _);
        engine.State.Cookies = 1e6;
        engine.MarkDirty();

        GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
        JsonObject beforeJson = ToJson(before);

        // 造出"只有第 6 位小数不同"的局面：直接改快照 JSON 里的 unlockProgress，再逐项比较
        var tweaked = (JsonObject)beforeJson.DeepClone();
        var rows = (JsonArray)tweaked["buildings"]!;
        double original = rows[0]!["unlockProgress"]!.GetValue<double>();
        rows[0]!["unlockProgress"] = original + 0.000001; // 1e-6，远小于 4 位小数的精度

        JsonObject drift = SnapshotProtocol.Diff(beforeJson, tweaked).Changed;
        Check.False(
            drift.ContainsKey("buildings"),
            $"{original} → {original + 0.000001} 这种第 6 位小数的漂移不该让 buildings 进增量帧。");

        // 反过来：真正的变化必须抓到（否则前端永远不更新进度条）
        rows[0]!["unlockProgress"] = original + 0.5;
        JsonObject real = SnapshotProtocol.Diff(beforeJson, tweaked).Changed;
        Check.True(
            real.ContainsKey("buildings"),
            $"{original} → {original + 0.5} 是真实变化，必须进增量帧。");
    }

    /// <summary>
    /// <b>量的是字节数，不是字段个数。</b><para>
    /// 这条用例是为一个真实事故加的：<c>unlockProgress</c> / <c>progress</c> 这类派生进度
    /// 每帧都在小数点后第 5 位以后变化，于是整个 6.9 KB 的 <c>buildings</c>、12 KB 的
    /// <c>achievements</c>、18 KB 的 <c>codex</c> 每帧重发——全量 47 KB、增量 28 KB。
    /// 而当时的用例只断言"变化的字段数 ≤ 4"，<b>照样是绿的</b>。
    /// </para>
    /// <para>
    /// 所以判据必须是字节：增量帧一旦接近全量，增量协议就已经名存实亡了。
    /// </para>
    /// </summary>
    [Test]
    public void IdleDelta_StaysSmallInBytes_EvenWhileProgressDrifts()
    {
        GameEngine engine = TestGame.CreateCafe(out _);

        // 先推起经济，让建筑/成就/图鉴里都攒出"进度条"来——这正是会漂的东西
        for (int round = 0; round < 30; round++)
        {
            engine.State.Cookies = 1e6;
            engine.State.CookiesEarnedThisRun = 1e6;
            engine.MarkDirty();
            TestGame.BuyGreedily(engine);
        }
        for (int i = 0; i < 60; i++) engine.Update(1.0 / 30);

        GameSnapshot before = engine.Snapshot(PurchaseMode.Buy1);
        engine.Update(1.0 / 30);
        GameSnapshot after = engine.Snapshot(PurchaseMode.Buy1);

        SnapshotProtocol.Delta diff = SnapshotProtocol.Diff(before, after);

        // 量的是"真的会发出去的字节"，所以按协议自己的口径（剥掉不进增量的派生字段）比
        int deltaBytes = SnapshotProtocol.Serialize(diff.Changed).Length;
        int fullBytes = SnapshotProtocol.Serialize(after).Length;

        Check.True(diff.Changed.Count > 0, "一个 tick 什么都没变，这条用例测不到东西。");
        Check.True(
            deltaBytes * 20 < fullBytes,
            $"一个 tick 的增量是 {deltaBytes} 字节，全量才 {fullBytes} 字节（占 {deltaBytes * 100.0 / fullBytes:F0}%）。" +
            $"带着的字段：{string.Join('、', diff.Changed.Select(p => $"{p.Key}({SnapshotProtocol.Serialize(p.Value).Length}B)"))}。" +
            $"漂移的子字段：{DriftingFields(ToJson(before), ToJson(after))}。" +
            "多半是某个派生字段在每帧漂移，要把它加进 SnapshotProtocol.ExcludedFromDelta" +
            "（文本类）或 QuantizedFields（数值类）。");
    }

    /// <summary>列出两个快照之间真正发生差异的子字段路径，供上面的断言报错时定位。</summary>
    private static string DriftingFields(JsonObject before, JsonObject after)
    {
        var drift = new Dictionary<string, int>(StringComparer.Ordinal);

        void Walk(JsonNode? a, JsonNode? b, string path)
        {
            if (a is JsonObject ao && b is JsonObject bo)
            {
                foreach ((string key, JsonNode? value) in bo)
                {
                    Walk(ao[key], value, path.Length == 0 ? key : $"{path}.{key}");
                }

                return;
            }

            if (a is JsonArray aa && b is JsonArray ba)
            {
                for (int i = 0; i < Math.Min(aa.Count, ba.Count); i++) Walk(aa[i], ba[i], $"{path}[{i}]");
                return;
            }

            if (a?.ToJsonString() == b?.ToJsonString()) return;

            // 把路径里的下标折掉，便于按字段名归类
            string normalized = System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[]");
            drift[normalized] = drift.GetValueOrDefault(normalized) + 1;
        }

        Walk(before, after, string.Empty);

        return drift.Count == 0
            ? "（无）"
            : string.Join('、', drift.OrderByDescending(p => p.Value).Select(p => $"{p.Key}×{p.Value}"));
    }
}
