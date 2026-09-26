using NekoClicker.Content.Company;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #10《猫娘公司》的验收（阶段 3C-2）。<para>
/// 这个包存在的意义有三层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>第三套纪元叙事</b>——三轮重组（车库 / A 轮 / 上市）与九命、实验室共用同一套
///   <c>Era</c> 代码，这是 ROADMAP G3 要求的第三种叙事。</item>
///   <item><b>第二套立场轴</b>——劳资轴（上市 / 工会 / 清算）与实验室的道德轴完全不是一回事，
///   却共用同一套 <c>StanceDefinition</c>：这是"立场不是 enum"的第二次检验。</item>
/// </list>
/// </para>
/// </summary>
public static class CompanyContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Company;

        Check.Equal("猫娘公司", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 三轮重组，层号连续。
        Check.Equal(3, content.Eras.Count);
        for (int index = 1; index <= 3; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 劳资轴：三条立场、六次表态、四个结局（三 + 兜底）。
        Check.Equal(3, content.Stances.Count);
        Check.Equal(6, content.Choices.Count);
        Check.Equal(4, content.Endings.Count);
    }

    [Test]
    public static void LoreRevealsAreUnique()
    {
        // 同条件的条目必然同时解锁。别的包的这条规则由 LoreTests 守着，
        // 这里给公司包补一份——内容包不该靠"没人测"来通过。
        Dictionary<string, string> seen = new(StringComparer.Ordinal);

        foreach (LoreEntry entry in TestGame.Company.LoreEntries)
        {
            List<string> leaves =
            [
                .. entry.Reveal.NumericLeaves()
                    .Select(leaf => $"{leaf.Metric}:{leaf.Id}:{leaf.Target:R}")
                    .Order(StringComparer.Ordinal),
            ];

            string key = string.Join("|", leaves) + "|" + entry.Reveal.GetType().Name;
            Check.True(
                seen.TryAdd(key, entry.Id),
                $"{entry.Id} 与 {seen.GetValueOrDefault(key)} 的释放条件完全相同——它们会同时解锁。");
        }
    }

    [Test]
    public static void EveryChoiceIsARealFork()
    {
        foreach (ChoiceDefinition choice in TestGame.Company.Choices)
        {
            Check.Equal(2, choice.Options.Count, $"「{choice.Id}」应当正好两个选项。");

            List<string> stances = [.. choice.Options.Select(o => o.StanceId)];
            Check.Equal(
                stances.Count,
                stances.Distinct(StringComparer.Ordinal).Count(),
                $"「{choice.Id}」的两个选项指向同一立场，不构成分叉。");

            foreach (ChoiceOption option in choice.Options)
            {
                Check.True(option.Modifiers.Count > 0, $"「{choice.Id}/{option.Id}」没有数值代价。");
            }
        }
    }

    [Test]
    public static void EveryStanceCanReachTheEndingThreshold()
    {
        GameContent content = TestGame.Company;
        int needed = (Stances.EndingThreshold + 1) / 2;

        foreach (StanceDefinition stance in content.Stances)
        {
            int opportunities = content.Choices
                .SelectMany(c => c.Options)
                .Count(o => string.Equals(o.StanceId, stance.Id, StringComparison.Ordinal));

            Check.AtLeast(
                opportunities,
                needed,
                $"立场「{stance.Name}」只有 {opportunities} 次表态机会，攒不满 {Stances.EndingThreshold} 点。");
        }
    }

    [Test]
    public static void EveryEndingHasItsOwnAchievement()
    {
        GameContent content = TestGame.Company;

        foreach (EndingDefinition ending in content.Endings)
        {
            bool found = content.Achievements.Any(
                a => a.Unlock is OwnedCondition { Kind: OwnedKind.Ending } owned
                     && string.Equals(owned.Id, ending.Id, StringComparison.Ordinal));

            Check.True(found, $"结局「{ending.Id}」没有对应的成就。");
        }
    }

    [Test]
    public static void Morale_IsGrownByTeamsAndEatenByOvertime()
    {
        // 设计要点：士气由「团队」建筑养，被加班升级与 A 轮后的全员加班吃。
        GameEngine engine = TestGame.CreateCompany(out _);
        engine.State.BuildingCounts["desk"] = 20; // 20 个团队成员 → 2 点/秒
        engine.MarkDirty();

        engine.Simulate(60);
        Check.Close(
            20 * 60 / CompanyContent.TeamPerPointPerSecond,
            engine.State.GetCounter(CompanyContent.MoraleCounterKey),
            1e-6);

        // 买入一条加班升级：每秒多消耗 0.2 点。
        engine.State.UpgradeCounts["series_a_fuel"] = 1;
        engine.MarkDirty();
        double before = engine.State.GetCounter(CompanyContent.MoraleCounterKey);
        engine.Simulate(60);
        Check.Close(
            before + (2 - 0.2) * 60,
            engine.State.GetCounter(CompanyContent.MoraleCounterKey),
            1e-6);

        // 进入 A 轮：全员加班，再扣 1 点/秒。
        engine.State.Era = 2;
        engine.MarkDirty();
        before = engine.State.GetCounter(CompanyContent.MoraleCounterKey);
        engine.Simulate(60);
        Check.Close(
            before + (2 - 1.2) * 60,
            engine.State.GetCounter(CompanyContent.MoraleCounterKey),
            1e-6);

        // 反面：人少 + 加班 = 士气归零，且不会变成负数。
        GameEngine starved = TestGame.CreateCompany(out _);
        starved.State.BuildingCounts["desk"] = 1; // 0.1 点/秒
        starved.State.Era = 2;                     // 1 点/秒
        starved.MarkDirty();
        starved.Simulate(300);
        Check.Close(0, starved.State.GetCounter(CompanyContent.MoraleCounterKey), 1e-9);
    }

    [Test]
    public static void EveryChoiceTriggersDuringARealPlaythrough()
    {
        GameEngine engine = LoadFrom(PlayedSave.Value);

        Check.Equal(3, engine.State.Era, "机器人应当走到第 3 轮（上市）。");
        Check.Equal(
            engine.Content.Choices.Count,
            engine.State.PendingChoices.Count,
            $"只有 {engine.State.PendingChoices.Count} 次表态出现过，应当全部出现。");
        Check.Equal(0, engine.State.ChoiceAnswers.Count, "机器人从不作答。");
        Check.Null(engine.ReachedEnding, "还在末轮中途，不该已经有结局。");
    }

    [Test]
    public static void EveryCommittedEndingIsReachableAndExclusive()
    {
        Check.Equal("end_ipo", EndingAfterChoosing(PlayedSave.Value, Stances.Ipo));
        Check.Equal("end_union", EndingAfterChoosing(PlayedSave.Value, Stances.Union));
        Check.Equal("end_liquidate", EndingAfterChoosing(PlayedSave.Value, Stances.Liquidate));
    }

    [Test]
    public static void AvoidingEveryChoiceYieldsTheFallbackEnding()
    {
        GameEngine engine = LoadFrom(PlayedSave.Value);
        Check.Equal(0, engine.State.ChoiceAnswers.Count, "前提：一次都不答。");

        FinishLastRound(engine);
        Check.Equal("end_fade", engine.ReachedEnding?.Id);
    }

    // ---------------------------------------------------------------- 辅助

    /// <summary>
    /// 跑一次真实游玩，直到六次表态全部进入待答队列（发生在第 3 轮，4e8 &lt; 5e8 完成门槛）。<para>
    /// 末轮改用 0.25 秒细步长：表态门槛与完成门槛之间只差 1e8，粗步长可能一步跨过去，
    /// 那样测的是步长而不是内容。跑完把存档缓存起来，结局用例各读一份，互不影响。
    /// </para>
    /// </summary>
    private static readonly Lazy<string> PlayedSave = new(() => PlayUntilAllChoicesPending().Save());

    private static GameEngine PlayUntilAllChoicesPending()
    {
        GameEngine engine = TestGame.CreateCompany(out _, seed: 20240924);
        int total = engine.Content.Choices.Count;

        for (int round = 0; round < 40_000 && engine.State.PendingChoices.Count < total; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance) engine.Ascend();
            engine.Simulate(engine.State.Era >= 3 ? 0.25 : 30);
        }

        Check.Equal(
            total,
            engine.State.PendingChoices.Count,
            "机器人没能让六次表态全部出现——某个选择的门槛可能超过了它所在轮的完成门槛。");
        Check.AtLeast(engine.State.Achievements.Count, 12, "末轮完成条件要求 12 个成就。");
        return engine;
    }

    private static GameEngine LoadFrom(string save)
    {
        GameEngine engine = TestGame.CreateCompany(out _);
        engine.Load(save);
        return engine;
    }

    /// <summary>待答的每次表态都尽量押同一条立场（没有该立场的选项就取第一个），然后完成末轮主线。</summary>
    private static string? EndingAfterChoosing(string save, string stanceId)
    {
        GameEngine engine = LoadFrom(save);

        foreach (ChoiceDefinition choice in engine.Content.Choices)
        {
            if (!engine.State.PendingChoices.Contains(choice.Id)) continue;

            ChoiceOption pick = choice.Options.FirstOrDefault(
                o => string.Equals(o.StanceId, stanceId, StringComparison.Ordinal)) ?? choice.Options[0];

            Check.True(engine.AnswerChoice(choice.Id, pick.Id), $"作答 {choice.Id}/{pick.Id} 失败。");
        }

        Check.AtLeast(
            engine.State.StanceWeight(stanceId),
            Stances.EndingThreshold,
            $"押满「{stanceId}」之后权重应当够门槛。");

        // 互斥：押满一条，别的都够不到门槛（每条立场只有 4 次机会）。
        foreach (StanceDefinition other in engine.Content.Stances)
        {
            if (string.Equals(other.Id, stanceId, StringComparison.Ordinal)) continue;
            Check.AtMost(
                engine.State.StanceWeight(other.Id),
                Stances.EndingThreshold - 1,
                $"押满「{stanceId}」时「{other.Id}」不该也够到门槛，否则结局不互斥。");
        }

        FinishLastRound(engine);
        return engine.ReachedEnding?.Id;
    }

    /// <summary>
    /// 把末轮主线推到完成：直接把"本轮累计"顶过门槛（成就数在跑图里已经够了），
    /// 然后让引擎自己跑一拍做终局判定。终局必须在末层完成后才成立——
    /// 这是实验室包那个坑的修复方式，公司包从内容上就要求同一条。
    /// </summary>
    private static void FinishLastRound(GameEngine engine)
    {
        engine.State.CookiesEarnedThisRun = 5e8;
        engine.MarkDirty();
        engine.Simulate(60);
    }
}
