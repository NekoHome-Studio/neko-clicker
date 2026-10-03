using System.Text.Json.Nodes;
using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;
using NekoClicker.Web;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 建筑升级系统：<b>"这座升级属于哪座建筑"</b>这条关系（<c>Category = "building:&lt;id&gt;"</c>）。<para>
/// 这条约定从 1.4.0 起就写在 <see cref="UpgradeDefinition.Category"/> 的文档上、十一个包都在用它，
/// 而在这次改动之前<b>没有一处代码校验过它</b>：写成 <c>building:nope</c> 不会报错，
/// 只会让这条升级在"按建筑取升级"的界面上永远不出现——正是一类静默失效。
/// 下面这组用例守的就是"它现在会大声报错、而且真的被索引与推送"。
/// </para>
/// <para>
/// 与 <c>FrameworkTests.Modifier_UnknownBuildingId_IsRejected</c> 是同一类修复的两半：
/// 那一半守修饰符的目标，这一半守分类。两条都不许退化。
/// </para>
/// </summary>
public static class BuildingUpgradeTests
{
    private const string Prefix = UpgradeCategories.BuildingPrefix;

    /// <summary>取出快照 JSON 里的建筑数组。</summary>
    private static JsonArray Buildings(JsonObject snapshot)
        => snapshot["buildings"] as JsonArray ?? throw new AssertionException("快照 JSON 里没有 buildings 数组。");

    private static JsonObject ToJson(GameSnapshot snapshot)
        => JsonNode.Parse(SnapshotProtocol.Serialize(snapshot)) as JsonObject
           ?? throw new AssertionException("快照没能序列化成 JSON 对象。");

    /// <summary>一座建筑 + 一条挂在它名下的升级，用来问"这条分类合法吗"。</summary>
    private static GameContentBuilder WithCategorizedUpgrade(
        string upgradeId,
        string category,
        params Modifier[] modifiers)
        => new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "a", Name = "A", BasePrice = 10, BaseCps = 1 })
            .Add(new BuildingDefinition { Id = "b", Name = "B", BasePrice = 10, BaseCps = 1 })
            .Add(new UpgradeDefinition
            {
                Id = upgradeId,
                Name = "U",
                Price = 1,
                Category = category,
                Modifiers = modifiers,
            });

    // ---------------------------------------------------------------- 一致性与索引

    /// <summary>
    /// 十一个包逐条：<c>building:</c> 前缀必须指向真实建筑，索引必须与声明**完全一致**，
    /// 且轨内按 Tier 单调不减。<para>
    /// 这条不许只看"没抛异常"：它同时钉住<b>不空</b>（312 条建筑档 / 104 座建筑），
    /// 否则"把分类全删掉"也会让它绿。
    /// </para>
    /// </summary>
    [Test]
    public static void BuildingCategories_ResolveToRealBuildings_InEveryPack()
    {
        int categorized = 0, totalUpgrades = 0, buildingsWithTrack = 0, buildings = 0;
        var problems = new List<string>();

        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            var declared = new Dictionary<string, List<UpgradeDefinition>>(StringComparer.Ordinal);

            foreach (UpgradeDefinition u in content.Upgrades)
            {
                totalUpgrades++;
                if (!u.Category.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                categorized++;

                if (!UpgradeCategories.TryGetBuildingId(u.Category, out string buildingId))
                {
                    problems.Add($"{name}：升级「{u.Id}」的分类是「{u.Category}」，取不出建筑 id。");
                    continue;
                }

                if (!content.BuildingById.ContainsKey(buildingId))
                {
                    problems.Add($"{name}：升级「{u.Id}」指向不存在的建筑「{buildingId}」。");
                    continue;
                }

                if (!declared.TryGetValue(buildingId, out List<UpgradeDefinition>? list))
                    declared[buildingId] = list = [];
                list.Add(u);

                bool targets = u.Modifiers.Any(m =>
                    m.Target.Kind is ModifierTargetKind.BuildingCps or ModifierTargetKind.BuildingPrice
                    && string.Equals(m.Target.Id, buildingId, StringComparison.Ordinal));
                bool targetsAnother = u.Modifiers.Any(m =>
                    m.Target.Kind is ModifierTargetKind.BuildingCps or ModifierTargetKind.BuildingPrice
                    && m.Target.Id is not null
                    && !string.Equals(m.Target.Id, buildingId, StringComparison.Ordinal));
                if (!targets && targetsAnother)
                    problems.Add($"{name}：升级「{u.Id}」挂在「{buildingId}」名下，修饰符却作用在别的建筑上。");
            }

            buildings += content.Buildings.Count;
            buildingsWithTrack += content.UpgradesByBuilding.Count;

            // 索引 == 声明（逐条比 id，不只看条数）
            foreach (BuildingDefinition building in content.Buildings)
            {
                IReadOnlyList<UpgradeDefinition> track = content.UpgradesForBuilding(building.Id);
                declared.TryGetValue(building.Id, out List<UpgradeDefinition>? expected);

                string[] actualIds = [.. track.Select(u => u.Id)];
                string[] expectedIds = [.. (expected ?? []).Select(u => u.Id)];
                if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
                {
                    problems.Add(
                        $"{name}：建筑「{building.Id}」的轨道与声明不一致——"
                        + $"索引 [{string.Join('、', actualIds)}]，声明 [{string.Join('、', expectedIds)}]。");
                }

                for (int i = 1; i < track.Count; i++)
                {
                    if (track[i].Tier < track[i - 1].Tier)
                        problems.Add($"{name}：建筑「{building.Id}」的轨道没按 Tier 排序（第 {i} 条倒退）。");
                }
            }

            // 索引里不该有"建筑表里没有的键"
            foreach (string key in content.UpgradesByBuilding.Keys)
            {
                if (!content.BuildingById.ContainsKey(key))
                    problems.Add($"{name}：索引里出现了不存在的建筑「{key}」。");
            }
        }

        Check.Equal(0, problems.Count, string.Join("；", problems));
        Check.Equal(545, totalUpgrades, "升级总数（2026-10-03 实测 534 条 → 点击桥 +11，见 OPEN_WORK §0.16）");
        Check.Equal(312, categorized, "其中属于某座建筑的（2026-10-03 实测 312 条）");
        Check.Equal(104, buildingsWithTrack, "有升级轨的建筑数（真值是 104 座全都有）");
        Check.Equal(104, buildings, "建筑总数");
    }

    /// <summary>没有升级的建筑必须回答"没有"，而不是抛异常或返回 null。</summary>
    [Test]
    public static void BuildingWithoutATrack_AnswersWithAnEmptyList()
    {
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "lonely", Name = "L", BasePrice = 10, BaseCps = 1 })
            .Build();

        Check.Equal(0, content.UpgradesForBuilding("lonely").Count, "没有升级的建筑");
        Check.Equal(0, content.UpgradesForBuilding("ghost").Count, "压根不存在的建筑（查询不该抛）");
        Check.Equal(0, content.UpgradesByBuilding.Count, "索引里不该凭空多出条目");
    }

    // ---------------------------------------------------------------- 排序

    /// <summary>
    /// 轨内按 <see cref="UpgradeDefinition.Tier"/> 排序，而不是按声明顺序。<para>
    /// 用合成内容把两者**故意写反**：若实现退化成"照声明顺序抄一遍"，这条会红。
    /// </para>
    /// </summary>
    [Test]
    public static void Track_IsOrderedByTier_NotByDeclarationOrder()
    {
        GameContent content = new GameContentBuilder("X")
            .Add(new BuildingDefinition { Id = "a", Name = "A", BasePrice = 10, BaseCps = 1 })
            .Add(new UpgradeDefinition
            {
                Id = "late", Name = "L", Price = 1, Tier = 10, Category = Prefix + "a",
                Modifiers = [Modifier.BuildingMultiplier("a", 2)],
            })
            .Add(new UpgradeDefinition
            {
                Id = "early", Name = "E", Price = 1, Tier = 1, Category = Prefix + "a",
                Modifiers = [Modifier.BuildingMultiplier("a", 2)],
            })
            .Build();

        string[] ids = [.. content.UpgradesForBuilding("a").Select(u => u.Id)];
        Check.Equal("early,late", string.Join(',', ids), "轨内顺序（声明顺序是 late,early）");
    }

    // ---------------------------------------------------------------- 坏声明必须大声

    /// <summary>分类指向不存在的建筑 → 构建期就红，而且点名升级 id 与那个坏建筑 id。</summary>
    [Test]
    public static void BuildingCategory_WithAnUnknownBuilding_FailsLoudly()
    {
        GameContentValidationException error = Check.Throws<GameContentValidationException>(() =>
            WithCategorizedUpgrade("u", Prefix + "ghost", Modifier.GlobalPercent(0.5)).Build());

        Check.Contains(error.Message, "「u」", "错误信息里的升级 id");
        Check.Contains(error.Message, "「ghost」", "错误信息里的那个不存在的建筑 id");
    }

    /// <summary>前缀后面没有建筑 id（<c>"building:"</c>）→ 同样在构建期点名。</summary>
    [Test]
    public static void BuildingCategory_WithAnEmptyId_FailsLoudly()
    {
        GameContentValidationException error = Check.Throws<GameContentValidationException>(() =>
            WithCategorizedUpgrade("u", Prefix, Modifier.GlobalPercent(0.5)).Build());

        Check.Contains(error.Message, "「u」", "错误信息里的升级 id");
        Check.Contains(error.Message, Prefix, "错误信息应指出是哪个前缀缺了 id");
    }

    /// <summary>
    /// 分类挂在 A 名下、修饰符作用在 B 上 → 构建期红。<para>
    /// 这是那条静默失效的正脸：玩家会在 A 那里看到它，买到的却是 B 的效果，两端都不报错。
    /// </para>
    /// </summary>
    [Test]
    public static void BuildingCategory_WhoseModifiersIgnoreTheBuilding_FailsLoudly()
    {
        GameContentValidationException error = Check.Throws<GameContentValidationException>(() =>
            WithCategorizedUpgrade("u", Prefix + "a", Modifier.BuildingMultiplier("b", 2)).Build());

        Check.Contains(error.Message, "「u」", "错误信息里的升级 id");
        Check.Contains(error.Message, "「a」", "错误信息里被声称归属的建筑 id");
        Check.Contains(error.Message, "「b」", "错误信息里修饰符实际作用的建筑 id");
    }

    /// <summary>
    /// 阴性对照：挂在某座建筑名下、效果是**全局**的，是合法创作，不许被上面那条误伤。<para>
    /// 没有这条，"分类与效果对不上"那条检查就可能被收紧成"必须有指向本建筑的修饰符"，
    /// 而那会把"这座建筑的培训提升了所有人"这类内容一并拦掉。
    /// </para>
    /// </summary>
    [Test]
    public static void BuildingCategory_WithAGlobalEffect_IsAllowed()
    {
        GameContent content = WithCategorizedUpgrade("u", Prefix + "a", Modifier.GlobalPercent(0.5)).Build();
        Check.Equal(1, content.UpgradesForBuilding("a").Count, "全局效果的建筑档仍应进轨");
    }

    /// <summary>没写分类的升级不会进任何建筑的轨（"没有分类"与"分类是建筑"必须分得开）。</summary>
    [Test]
    public static void UpgradeWithoutABuildingCategory_StaysOutOfEveryTrack()
    {
        GameContent content = WithCategorizedUpgrade("u", "click", Modifier.ClickFlat(5)).Build();
        Check.Equal(0, content.UpgradesForBuilding("a").Count, "「a」的轨道");
        Check.Equal(0, content.UpgradesByBuilding.Count, "索引");
    }

    /// <summary>约定本身的往返与拒绝（<see cref="UpgradeCategories"/> 是公开 API，单独钉一遍）。</summary>
    [Test]
    public static void UpgradeCategories_RoundTripAndRejectTheWrongShapes()
    {
        Check.Equal("building:catnap", UpgradeCategories.ForBuilding("catnap"), "构造出来的分类");

        Check.True(UpgradeCategories.TryGetBuildingId("building:catnap", out string id), "正常值应当能取出来");
        Check.Equal("catnap", id, "取出来的建筑 id");

        foreach (string bad in new[] { "building:", "click", "building", "", "Building:catnap" })
            Check.False(UpgradeCategories.TryGetBuildingId(bad, out _), $"「{bad}」不该被当成建筑分类");

        Check.False(UpgradeCategories.TryGetBuildingId(null, out _), "null 不该被当成建筑分类");
    }

    // ---------------------------------------------------------------- 走到快照

    /// <summary>
    /// 每一座建筑的 <see cref="BuildingView.UpgradeIds"/> 必须等于内容里那条轨，逐座比。<para>
    /// 这条是"索引建对了、但没人推到线上"的唯一防线——索引对了而快照忘了填，
    /// 前端拿到的就是一片空轨，而引擎侧不会有任何东西变红。
    /// </para>
    /// </summary>
    [Test]
    public static void BuildingView_CarriesTheTrack_ForEveryBuilding()
    {
        int buildings = 0, withTrack = 0;
        var problems = new List<string>();

        foreach ((string name, GameContent content) in TestGame.AllContentPacks())
        {
            GameSnapshot snapshot = TestGame.Create(content).Snapshot(PurchaseMode.Buy1);

            foreach (BuildingView view in snapshot.Buildings)
            {
                buildings++;
                string[] expected = [.. content.UpgradesForBuilding(view.Id).Select(u => u.Id)];
                if (!view.UpgradeIds.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    problems.Add(
                        $"{name}：建筑「{view.Id}」的快照轨 [{string.Join('、', view.UpgradeIds)}] "
                        + $"!= 内容轨 [{string.Join('、', expected)}]。");
                }

                if (view.UpgradeIds.Count > 0) withTrack++;
            }
        }

        Check.Equal(0, problems.Count, string.Join("；", problems));
        Check.Equal(104, buildings, "建筑数（快照侧）");
        Check.Equal(104, withTrack, "快照里带轨的建筑数（104 座全都有）");
    }

    /// <summary>线上形状：<c>buildings[].upgradeIds</c> 是字符串数组（前端按它取行）。</summary>
    [Test]
    public static void WireShape_UpgradeIdsIsAnArrayOfStrings()
    {
        JsonObject json = ToJson(TestGame.CreateCafe(out _).Snapshot(PurchaseMode.Buy1));
        JsonArray buildings = Buildings(json);

        Check.AtLeast(buildings.Count, 1, "咖啡馆应当有建筑");

        foreach (JsonNode? row in buildings)
        {
            JsonNode? ids = row?["upgradeIds"];
            Check.NotNull(ids, "每一行都必须带 upgradeIds（缺了就会与「这座建筑没有升级」混成同一种形状）");
            if (ids is not JsonArray array)
                throw new AssertionException($"upgradeIds 不是数组：{ids?.ToJsonString()}");

            foreach (JsonNode? id in array)
            {
                Check.True(
                    id is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrEmpty(text),
                    $"upgradeIds 里有非字符串/空值：{id?.ToJsonString()}");
            }
        }
    }

    /// <summary>
    /// 这一列是**静态**的：两个相邻帧之间逐字节相同，且不会因为它的存在让增量帧变大。<para>
    /// 它只随内容变（购买、解锁、金钱都不改它），所以"挂机时每帧重发 buildings"
    /// 这类事故不该由它引起。把实测的两个数字打出来，供报告与 OPEN_WORK 引用。
    /// </para>
    /// </summary>
    [Test]
    public static void UpgradeIds_AreStaticBetweenFrames_AndTheIdleDeltaStaysSmall()
    {
        GameEngine engine = TestGame.CreateNineLives(out _);
        for (int round = 0; round < 20; round++)
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

        JsonObject beforeJson = ToJson(before);
        JsonObject afterJson = ToJson(after);

        JsonArray beforeRows = Buildings(beforeJson);
        JsonArray afterRows = Buildings(afterJson);
        Check.Equal(beforeRows.Count, afterRows.Count, "两帧之间的建筑数");

        for (int i = 0; i < beforeRows.Count; i++)
        {
            string a = beforeRows[i]?["upgradeIds"]?.ToJsonString() ?? "<缺失>";
            string b = afterRows[i]?["upgradeIds"]?.ToJsonString() ?? "<缺失>";
            Check.Equal(a, b, $"第 {i} 座建筑的 upgradeIds 在两个相邻帧之间变了——它是静态的");
        }

        SnapshotProtocol.Delta diff = SnapshotProtocol.Diff(before, after);
        int deltaBytes = SnapshotProtocol.Serialize(diff.Changed).Length;
        int fullBytes = SnapshotProtocol.Serialize(after).Length;

        Console.WriteLine(
            $"  [建筑升级] 九命：全量 {fullBytes} 字节 / 一 tick 增量 {deltaBytes} 字节"
            + $"（{deltaBytes * 100.0 / fullBytes:F1}%，判据是 <5%；带着的字段："
            + $"{string.Join('、', diff.Changed.Select(p => p.Key))}）");

        Check.False(
            diff.Changed.ContainsKey("buildings"),
            $"一个闲置 tick 里 buildings 进了增量帧（带着的字段：{string.Join('、', diff.Changed.Select(p => p.Key))}）。"
            + "upgradeIds 是静态的，不该由它引起；若确实是它在漂，说明有人把它做成了派生字段。");
    }
}
