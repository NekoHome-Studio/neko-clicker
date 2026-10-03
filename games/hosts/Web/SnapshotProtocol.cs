using System.Text.Json;
using System.Text.Json.Nodes;

namespace NekoClicker.Web;

/// <summary>
/// <see cref="NekoClicker.Core.Views.GameSnapshot"/> 的 JSON 协议层。<para>
/// 这是 Web 前端与引擎之间的**唯一**数据契约，因此刻意做成纯静态、无状态的，
/// 并且不依赖 HTTP、不依赖引擎实例——于是它可以在引擎的测试项目里被逐条断言
/// （见 <c>engine/tests/WebSnapshotProtocolTests.cs</c>）。
/// </para>
/// <para>
/// 协议形态是「<b>信封 + 变化字段</b>」而不是每帧推全量，理由是用实测数据定的：
/// 一份快照 43~73 KB，而挂机时每 tick 真正变化的只有 <c>PlayTimeSeconds</c> 与
/// <c>GoldenCookieCountdown</c> 两个字段（约 100 字节）。按 4 Hz 推全量是 ~280 KB/s，
/// 推变化字段是 ~0.4 KB/s。这跟引擎内部的脏标记驱动是同一个判断。
/// </para>
/// </summary>
public static class SnapshotProtocol
{
    /// <summary>
    /// 线上传输用的序列化选项。<para>
    /// <b>① <c>PropertyNamingPolicy = CamelCase</c> 不能省。</b>
    /// <c>System.Text.Json</c> 默认<u>不做</u>命名转换，会原样输出 <c>Cookies</c> / <c>Buildings</c>。
    /// 前端 JS 是按 <c>cookies</c> / <c>buildings</c> 取的，键名一错就全盘静默失效——
    /// 服务端一切正常、浏览器上什么都不动。这个错真的犯过一次。
    /// </para>
    /// <para>
    /// <b>② <c>WriteIndented = false</c></b>：缩进会让体积涨 20~30%，而这份 JSON 是给机器读的。
    /// </para>
    /// <para>
    /// <b>③ 中文不转义</b>：UTF-8 原样输出比 <c>\uXXXX</c> 转义更短。
    /// </para>
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把一个值序列化成 JSON 字符串（用本协议的选项）。</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>序列化成一棵可比较的 JSON 树。</summary>
    private static JsonObject ToObject<T>(T value)
        => JsonSerializer.SerializeToNode(value, Options) as JsonObject
           ?? throw new InvalidOperationException($"{typeof(T).Name} 不能序列化成 JSON 对象。");

    /// <summary>
    /// 不算作"变化"的字段名。<para>
    /// <b><c>canAfford</c> 必须在这里。</b>它是纯粹的<b>派生量</b>（<c>price &lt;= cookies</c>），
    /// 而 <c>cookies</c> 本来就每帧都推。实测：不排除它的时候，48 条升级各自的
    /// <c>canAfford</c> 随金钱增长不停翻转，于是每次推送都带上整个 ~28 KB 的 <c>upgrades</c> 数组
    /// ——增量协议等于白做（全量 47 KB vs 增量 28 KB，只省 40%）。
    /// </para>
    /// <para>
    /// 排除之后由前端用"最新的 <c>cookies</c> + 未变过的 <c>price</c>"自己推一次。
    /// 代价是前端的可点击状态可能比服务端<b>早一个推送周期</b>点亮（4 Hz 里的一格，250ms）——
    /// 这是可接受的：服务端仍然校验每一次购买，早点亮只会得到一句"钱不够"，
    /// 不会产生任何非法状态。而换来的是增量帧真的小。
    /// </para>
    /// </summary>
    /// <summary>
    /// 不进增量帧的字段名。<para>
    /// <b>这些全是派生量</b>——它们都能由同一帧里别的字段推出来，所以推它们只是重复付费：
    /// </para>
    /// <list type="bullet">
    ///   <item><c>canAfford</c>：<c>price &lt;= cookies</c>。不排除的话，48 条升级各自的
    ///     「买得起」随金钱增长不停翻转，每次推送都带上整个 ~28 KB 的 <c>upgrades</c> 数组。</item>
    ///   <item><c>progressText</c>：格式化的进度文本（<c>"12 / 500"</c>）。它的<b>数值</b>由量化后的
    ///     <c>progress</c> 承担；把文本也推一遍，等于让 12 KB 的 <c>achievements</c> 和
    ///     18 KB 的 <c>codex</c> 每帧重发——实测这一条就占了 22 KB。</item>
    ///   <item><c>effectSummary</c>：升级效果摘要（<c>"所有建筑产量 +4%；每个成就 +1%"</c>）。
    ///     它带成就数，成就一解锁就变。</item>
    /// </list>
    /// <para>
    /// 前端拿这些字段的方式与 <c>canAfford</c> 一致：**全量帧给权威值，两帧之间不更新**。
    /// 对 <c>canAfford</c> 而言是"自己推一次"（见 <c>app.js</c>），对两个文本字段就是"先不动"
    /// ——进度条本身由量化后的 <c>progress</c> 驱动，照样在动。
    /// </para>
    /// </summary>
    private static readonly HashSet<string> ExcludedFromDelta = new(StringComparer.OrdinalIgnoreCase)
    {
        "canAfford", "progressText", "effectSummary",
        // 阶段门槛的文本（`4.5 万 / 600 万（1%）`）与 `progressText` 完全同类：数值由
        // `stageProgress` 承担，文本只在全量帧里给权威值。不排除它，`era` 会在**每一帧**
        // 因为格式化后的钱数变动而整体重发。
        "stageProgressText",
    };

    /// <summary>
    /// 只按"大致相等"比较的字段名——它们是**派生的显示量**，不需要逐位精确。<para>
    /// <b>不加这一组，增量协议就等于白做。</b>实测：10 项建筑的 <c>unlockProgress</c>、12 条成就的
    /// <c>progress</c>、图鉴各线的进度，全都随产量<b>每帧</b>小幅变化（0.12136363… → 0.12137373…），
    /// 于是每次推送都带上整个 <c>buildings</c> / <c>achievements</c> / <c>codex</c> 数组
    /// （实测全量 47 KB、增量 28 KB，只省 40%）。
    /// </para>
    /// <para>
    /// 量化到小数点后 <see cref="ProgressPrecision"/> 位再比较：进度条上 0.01% 的差别肉眼不可见，
    /// 但省掉的是一整个数组。这与 <c>ReadershipModule</c> 里"跨过一个量子才 <c>MarkDirty</c>"是
    /// 同一个思路——**量化之后再判断要不要动**。
    /// </para>
    /// <para>
    /// <c>prestige</c> 那三个字段单独说明：它们只在"钱够不够升级"的边界上跳，
    /// 而那个边界在早期可能是每几秒一次；量化之后它只在真的跨档时才进增量帧。
    /// </para>
    /// </summary>
    private static readonly HashSet<string> QuantizedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "progress", "unlockProgress",
        "cookiesForNextLevel", "cookiesToNextLevel", "cpsForNextLevel",
        // 纪元内的阶段进度条：与 `era.progress` 一样是 0~1 的比例，产量一起来就每个 tick 都动。
        // 不量化它，`era` 会因为这一条子字段在每一帧进增量帧。
        "stageProgress",
    };

    /// <summary>
    /// 量化到小数点后几位。<para>
    /// <b>2 位（1%）是量出来的，不是拍的。</b>这些字段是 0~1 的<b>比例</b>，而产量是连续增长的，
    /// 所以一个 tick 里比例就会动——实测在"经济已经推起来"的局面下，
    /// <c>codex.storylines[].entries[].progress</c> 有 38 条每帧都在变，一次推送就是 14 KB。
    /// 精度设成 4 位时这个漂移<b>照样超过阈值</b>（量级 1e-3），所以拦不住；
    /// 设成 2 位之后，只有当进度条真的跨过 1% 才会进增量帧。
    /// </para>
    /// <para>
    /// 代价是进度条以 1% 为单位跳动而不是平滑滑动——这比"每帧重发 14 KB"划算得多，
    /// 而且 1% 在一条几十像素宽的进度条上本来就看不出来。
    /// </para>
    /// </summary>
    private const int ProgressPrecision = 2;

    /// <summary>
    /// 一帧增量的内容。<para>
    /// <see cref="Changed"/> 是要发给浏览器的；<see cref="QuantizedPaths"/> 是**合并时必须一起
    /// 应用的量化**——服务端不测它，"累加出的快照 == 直接取的全量"这条不变量就会红。
    /// 两者必须成对使用，所以打包成一个类型，而不是让调用方自己记着传两个参数。
    /// </para>
    /// </summary>
    /// <param name="Changed">顶层字段名 → 新值。</param>
    /// <param name="QuantizedPaths">需要按 <see cref="ProgressPrecision"/> 量化的路径（<c>顶层.下标.字段</c>）。</param>
    public sealed record Delta(JsonObject Changed, IReadOnlyList<string> QuantizedPaths);

    /// <summary>
    /// 算出 <paramref name="current"/> 相对 <paramref name="previous"/> 变化的**顶层字段**。<para>
    /// 比较用的是每个字段的原始 JSON 文本，所以嵌套的列表（建筑 / 升级 / 成就 / 图鉴…）
    /// 只要有一项变了就会被整个算作"变了"——这正好是想要的粒度：行列表要么整体重发，
    /// 要么不动，不存在"只改了第 7 行"这种半截状态。
    /// </para>
    /// <para>
    /// 两处例外：<see cref="ExcludedFromDelta"/> 的字段从不进增量帧；
    /// <see cref="QuantizedFields"/> 的字段先量化再比较。将来再有"派生量在churn"的字段，
    /// 加进这两个集合即可，不需要改调用方。
    /// </para>
    /// </summary>
    /// <returns>要发的变化字段，以及合并时必须一起应用的量化路径。</returns>
    public static Delta Diff<T>(T previous, T current)
    {
        JsonObject before = ToObject(previous);
        JsonObject after = ToObject(current);
        var changed = new JsonObject();
        var quantized = new List<string>();

        foreach ((string key, JsonNode? value) in after)
        {
            if (before.TryGetPropertyValue(key, out JsonNode? old) && SameJson(Strip(old), Strip(value))) continue;

            // 派生字段从不进增量帧：前端自己推得出来，服务端只在全量帧里给权威值
            if (ExcludedFromDelta.Contains(key)) continue;

            changed[key] = value?.DeepClone();
            CollectQuantizedPaths(value, key, quantized);
        }

        return new Delta(changed, quantized);
    }

    /// <summary>收集需要量化的路径，并**原地把值改成量化后的值**（这样合并侧不需要再算一次）。</summary>
    private static void CollectQuantizedPaths(JsonNode? node, string path, List<string> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string key, JsonNode? child) in obj)
                {
                    if (QuantizedFields.Contains(key)) into.Add($"{path}.{key}");
                    CollectQuantizedPaths(child, $"{path}.{key}", into);
                }

                break;

            case JsonArray array:
                for (int i = 0; i < array.Count; i++) CollectQuantizedPaths(array[i], $"{path}[{i}]", into);
                break;
        }
    }

    /// <summary>
    /// 一份快照的"可比较形式"：剥掉派生字段、把量化字段按精度对齐。<para>
    /// 给"增量累积出的快照 == 直接取的全量"这条不变量用。**不能直接逐字节比原始 JSON**：
    /// 累积侧会按协议重算派生字段（<c>canAfford</c> 由前端推、<c>progressText</c> 保持旧的），
    /// 而全量侧是引擎的权威值，两者本来就不该逐字节相同——比的是"协议保证一致的那部分"。
    /// </para>
    /// <para>
    /// 这同时也是一份**协议保证的一致性边界**：这些字段之外的内容，累积与全量必须完全一致；
    /// 这些字段本身则由 <c>app.js</c> 按同一口径重算。
    /// </para>
    /// </summary>
    public static JsonObject Comparable(JsonObject snapshot)
        => Strip(snapshot) as JsonObject ?? new JsonObject();

    /// <summary>递归剥掉不进增量的字段，并把需要量化的字段换成量化值，用于比较。</summary>
    private static JsonNode? Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var copy = new JsonObject();
                foreach ((string key, JsonNode? child) in obj)
                {
                    if (ExcludedFromDelta.Contains(key)) continue;
                    copy[key] = QuantizedFields.Contains(key) ? Quantize(child) : Strip(child)?.DeepClone();
                }

                return copy;
            }

            case JsonArray array:
            {
                var copy = new JsonArray();
                foreach (JsonNode? item in array) copy.Add(Strip(item)?.DeepClone());
                return copy;
            }

            default:
                return node;
        }
    }

    /// <summary>把数值量化到 <see cref="ProgressPrecision"/> 位小数；非数值原样返回。</summary>
    private static JsonNode? Quantize(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue(out double number) && double.IsFinite(number))
        {
            return JsonValue.Create(Math.Round(number, ProgressPrecision));
        }

        return node?.DeepClone();
    }

    /// <summary>把变化字段合并进一份快照 JSON，得到新的快照 JSON（原地修改 <paramref name="snapshot"/>）。</summary>
    /// <returns>合并后这份快照里真正变过的字段名（camelCase）。</returns>
    public static IReadOnlyList<string> Merge(JsonObject snapshot, JsonObject changed)
    {
        var applied = new List<string>(changed.Count);

        foreach ((string key, JsonNode? value) in changed)
        {
            // null 也要写进去：某个可空字段（Era / Codex / Stances / Ending）从有变无时，
            // 值是 null，跳过它就等于前端一直显示着旧的纪元面板。
            snapshot[key] = value?.DeepClone();
            applied.Add(key);
        }

        return applied;
    }

    /// <summary>
    /// 合并一帧增量（含它带来的量化调整）。<para>
    /// 直接接收 <see cref="Diff"/> 的返回值，而不是让调用方自己拆包——
    /// 忘了传 <see cref="Delta.QuantizedPaths"/> 的话，"累加出的快照 == 直接取的全量"
    /// 这条不变量会红，而那是个很难从表象看出来的失败。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Merge(JsonObject snapshot, Delta delta)
    {
        IReadOnlyList<string> applied = Merge(snapshot, delta.Changed);

        foreach (string path in delta.QuantizedPaths) QuantizeAt(snapshot, path);

        return applied;
    }

    /// <summary>按 <c>顶层</c> / <c>顶层.字段…</c> / <c>顶层[0].字段…</c> 的路径定位并量化。</summary>
    private static void QuantizeAt(JsonObject snapshot, string path)
    {
        JsonNode? node = snapshot;
        int index = 0;

        while (node is not null && index < path.Length)
        {
            int dot = path.IndexOf('.', index);
            string segment = dot < 0 ? path[index..] : path[index..dot];
            int bracket = segment.IndexOf('[');

            if (bracket >= 0)
            {
                string name = segment[..bracket];
                int close = segment.IndexOf(']', bracket);
                if (!int.TryParse(segment[(bracket + 1)..close], out int position)) return;

                node = (node as JsonObject)?[name] as JsonArray is { } array && position < array.Count
                    ? array[position]
                    : null;
            }
            else if (dot < 0)
            {
                if (node is JsonObject owner && owner[segment] is { } leaf) owner[segment] = Quantize(leaf);
                return;
            }
            else
            {
                node = (node as JsonObject)?[segment];
            }

            index = dot < 0 ? path.Length : dot + 1;
        }
    }

    /// <summary>构建一帧"全量"信封。</summary>
    public static string FullEnvelope(long seq, object snapshot)
        => Serialize(new
        {
            kind = "full",
            seq,
            snapshot = JsonSerializer.SerializeToNode(snapshot, Options),
        });

    /// <summary>构建一帧"增量"信封。</summary>
    public static string DeltaEnvelope(long seq, Delta delta) => DeltaEnvelope(seq, delta.Changed);

    /// <summary>构建一帧"增量"信封（只要变化字段，不带量化路径）。</summary>
    /// <remarks>
    /// 线上的帧里**不带** <see cref="Delta.QuantizedPaths"/>：那是服务端自己的合并细节，
    /// 浏览器不需要也不该看到。前端要重算派生值只有一条规则，写在 <c>app.js</c> 里。
    /// </remarks>
    public static string DeltaEnvelope(long seq, JsonObject changed)
        => Serialize(new
        {
            kind = "delta",
            seq,
            changed,
        });

    /// <summary>构建一帧"事件"信封（日志/提示用，不参与快照状态）。</summary>
    public static string EventEnvelope(string name, object payload)
        => Serialize(new
        {
            kind = "event",
            name,
            payload = JsonSerializer.SerializeToNode(payload, Options),
        });

    /// <summary>
    /// 合并一帧增量信封里的变化字段，并把派生字段重算回权威值。<para>
    /// 这是"前端视角"的合并，供测试与将来的兼容性检查使用——它只知道线上帧里有的东西，
    /// 所以量化路径要从 <paramref name="delta"/> 里拿（服务端自己是从 <see cref="Diff"/> 直接拿的）。
    /// </para>
    /// </summary>
    /// <returns>合并后这份快照里真正被写过的字段名（camelCase）。</returns>
    public static IReadOnlyList<string> ApplyDelta(JsonObject snapshot, string deltaEnvelopeJson, Delta? delta = null)
    {
        using JsonDocument document = JsonDocument.Parse(deltaEnvelopeJson);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("changed", out JsonElement changed) ||
            changed.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("这帧不是增量信封（缺 changed 字段）。");
        }

        JsonObject patch = JsonNode.Parse(changed.GetRawText()) as JsonObject
                           ?? throw new InvalidOperationException("changed 不是一个 JSON 对象。");

        IReadOnlyList<string> applied = Merge(snapshot, patch);

        if (delta is not null)
        {
            foreach (string path in delta.QuantizedPaths) QuantizeAt(snapshot, path);
        }

        RecomputeDerived(snapshot);
        return applied;
    }

    /// <summary>
    /// 按服务端的口径重算派生字段。<para>
    /// 目前只有一条规则：<c>canAfford = price &lt;= 钱包</c>（升级看它自己的货币）。
    /// 前端 <c>app.js</c> 里有一份等价的实现（它要在不重绘整个列表的前提下更新按钮样式）——
    /// 两处必须同口径，所以这条规则写在这里当唯一说明。
    /// </para>
    /// </summary>
    public static void RecomputeDerived(JsonObject snapshot)
    {
        double wallet = snapshot["cookies"]?.GetValue<double>() ?? 0;
        double premium = snapshot["prestigeChips"]?.GetValue<double>() ?? 0;

        foreach (string key in new[] { "buildings", "upgrades" })
        {
            if (snapshot[key] is not JsonArray rows) continue;

            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject item) continue;

                bool unlocked = item["isUnlocked"]?.GetValue<bool>() ?? false;
                bool maxed = item["isMaxed"]?.GetValue<bool>() ?? false;

                // 升级用哪种货币：0 = 主货币，1 = 转生货币（与 UpgradeCurrency 枚举一致）
                double budget = item["currency"]?.GetValue<int>() == 1 ? premium : wallet;
                double price = item["price"]?.GetValue<double>()
                               ?? item["batchPrice"]?.GetValue<double>()
                               ?? double.PositiveInfinity;

                item["canAfford"] = unlocked && !maxed && price <= budget;
            }
        }
    }

    private static bool SameJson(JsonNode? a, JsonNode? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return string.Equals(a.ToJsonString(Options), b.ToJsonString(Options), StringComparison.Ordinal);
    }
}
