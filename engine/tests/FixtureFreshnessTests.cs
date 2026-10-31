using System.Text.Json;
using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Hosts;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 夹具的新鲜度守卫：<c>tools/fixtures/web-snapshot.json</c> 必须跟上引擎"此刻"的线上形状
/// （W19 的机制化）。<para>
/// web-smoke §14/§23 把这份夹具当"线上形状"的唯一参照，但那两个套件手里<b>只有夹具</b>——
/// 引擎侧长出新字段（1.11.0 的 <c>channelName</c>）或新内容行（1.10.1 的 <c>shareholder_paws</c>）
/// 时它们不会红：夹具就这样静默过期了三周，直到一次人肉重抓（<c>OPEN_WORK</c> §0.42）才现形。
/// 这条守卫把方向反过来：<b>引擎此刻序列化出来的快照</b>是真相，夹具必须逐路径跟上——
/// 对不上就红，红字只给一个出路：重抓（<c>tools/capture-fixture.ps1</c>）。
/// </para>
/// <para>
/// 比对口径与 web-smoke §14 相同（形状，不比值）：键集合两边对齐、数组只走 [0]、
/// 标量只比类别（string / number / boolean）；任一边是 null 就放过（null 表示
/// "这个包 / 这个会话没有这一项"）。内容数组（建筑 / 升级 / 成就 / 图鉴条目…）
/// 在两边都非空时<b>钉条数</b>——1.10.1 加的那条升级就是这么漏的；会话性数组
/// （通知 / 增益 / 金猫 / 纸条 / 表态）条数跟会话走，不钉。
/// </para>
/// </summary>
public sealed class FixtureFreshnessTests
{
    /// <summary>
    /// 夹具是按<b>公司包</b>抓的（<c>capture-fixture.ps1</c> 的默认 <c>-Package company</c>——
    /// 它是第一个声明阶段的包，所以夹具同时是那六个阶段字段"真的在线上"的判据）。
    /// 换包重抓时这一行跟着改。
    /// </summary>
    private const string FixturePackage = "company";

    /// <summary>条数跟会话走（点击 / 增益 / 金猫 / 纸条 / 表态）的数组：不钉条数。</summary>
    private static readonly string[] SessionVaryingArrays =
    {
        "notifications", "buffs", "goldenCookies", "pendingLore", "pendingChoices",
    };

    [Test]
    public void Fixture_StillMatchesWhatTheEngineSerializesToday()
    {
        string? root = RepositoryPaths.Find();
        Check.True(root is not null, "找不到仓库根（NekoClicker.sln）——这条用例靠仓库里的夹具文件工作。");
        string fixturePath = Path.Combine(root!, "tools", "fixtures", "web-snapshot.json");
        Check.True(File.Exists(fixturePath), $"夹具不见了：{fixturePath}（重抓：pwsh -File tools/capture-fixture.ps1）");

        JsonObject fixture = JsonNode.Parse(File.ReadAllText(fixturePath)) as JsonObject
            ?? throw new AssertionException("夹具的顶层不是 JSON 对象——这不是可以当夹具的东西。");

        // 引擎此刻的快照：公司包、新会话（0 点击）、Buy1 档。会话状态（点击数 / 计时器 /
        // 通知条数）不影响形状——这里比的是形状与内容数组的条数，不是数值。
        GameEngine engine = TestGame.CreateCompany(out _);
        JsonObject live = JsonNode.Parse(SnapshotProtocol.Serialize(engine.Snapshot(PurchaseMode.Buy1))) as JsonObject
            ?? throw new AssertionException("引擎快照没能序列化成 JSON 对象。");

        List<string> diffs = new();
        Walk(fixture, live, "", diffs);

        Check.True(diffs.Count == 0,
            $"夹具与引擎此刻的线上形状对不上 {diffs.Count} 处——夹具过期了（W19 的那类失败）。"
            + $"重抓：pwsh -File tools/capture-fixture.ps1 -Package {FixturePackage}，然后跑 node tools/web-smoke.mjs。\n      · "
            + string.Join("\n      · ", diffs));
    }

    private static void Walk(JsonNode? fixture, JsonNode? live, string path, List<string> into)
    {
        // 与 web-smoke §14 同一条规则：任一边是 null 就放过——null 表示"这个包 / 这个会话没有这一项"。
        if (fixture is null || live is null) return;

        if (fixture is JsonObject fo && live is JsonObject lo)
        {
            foreach (string key in lo.Select(p => p.Key))
                if (!fo.ContainsKey(key))
                    into.Add($"{At(path, key)}：引擎现在给、夹具没有（新字段没进夹具）");
            foreach (string key in fo.Select(p => p.Key))
                if (!lo.ContainsKey(key))
                    into.Add($"{At(path, key)}：夹具有、引擎现在不给（字段被删了？）");
            foreach (string key in fo.Select(p => p.Key))
                if (lo.ContainsKey(key))
                    Walk(fo[key], lo[key], At(path, key), into);
            return;
        }

        if (fixture is JsonArray fa && live is JsonArray la)
        {
            bool sessionVarying = SessionVaryingArrays.Contains(path);
            if (fa.Count > 0 && la.Count > 0)
            {
                if (!sessionVarying && fa.Count != la.Count)
                    into.Add($"{path}：条数对不上（夹具 {fa.Count} / 引擎 {la.Count}）——内容变了，夹具过期");
                Walk(fa[0], la[0], $"{path}[0]", into);
            }
            else if (!sessionVarying && fa.Count != la.Count)
            {
                into.Add($"{path}：一条空一条非空（夹具 {fa.Count} / 引擎 {la.Count}）");
            }
            return;
        }

        if (fixture is JsonValue && live is JsonValue)
        {
            if (KindOf(fixture) != KindOf(live))
                into.Add($"{path}：标量类型变了（夹具 {KindOf(fixture)} / 引擎 {KindOf(live)}）");
            return;
        }

        into.Add($"{path}：两边的 JSON 类别不同（夹具 {KindOf(fixture)} / 引擎 {KindOf(live)}）");
    }

    private static string At(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    private static string KindOf(JsonNode node)
    {
        if (node is JsonObject) return "object";
        if (node is JsonArray) return "array";
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out _)) return "string";
            if (value.TryGetValue<bool>(out _)) return "boolean";
            if (value.TryGetValue<decimal>(out _)) return "number";
        }
        return "null";
    }
}
