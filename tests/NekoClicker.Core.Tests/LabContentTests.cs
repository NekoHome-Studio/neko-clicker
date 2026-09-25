using NekoClicker.Content.Lab;
using NekoClicker.Core.Content;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 内容包 #3《猫娘实验室》的验收（阶段 3C）。<para>
/// 这个包存在的意义有两层：
/// <list type="number">
///   <item><b>结构自洽</b>——表与表之间的引用、门槛、曲线都对得上。</item>
///   <item><b>证明"核心不认识内容"</b>——它同时用了 Era + Lore + Choice 三项能力，
///   而核心一行未改。这一条由 <c>ArchitectureTests</c> 守着（核心程序集里不出现内容 id）。</item>
/// </list>
/// </para>
/// </summary>
public static class LabContentTests
{
    [Test]
    public static void Structure_IsComplete()
    {
        GameContent content = TestGame.Lab;

        Check.Equal("猫娘实验室", content.Title);
        Check.Equal(9, content.Buildings.Count);
        Check.AtLeast(content.Upgrades.Count, 40);
        Check.AtLeast(content.Achievements.Count, 60);
        Check.AtLeast(content.Buffs.Count, 6);
        Check.AtLeast(content.GoldenCookieOutcomes.Count, 8);

        // 七批次，层号连续。
        Check.Equal(7, content.Eras.Count);
        for (int index = 1; index <= 7; index++) Check.Equal(index, content.Eras[index - 1].Index);

        // 道德轴：四条立场、六次表态、五个结局。
        Check.Equal(4, content.Stances.Count);
        Check.Equal(6, content.Choices.Count);
        Check.Equal(5, content.Endings.Count);
    }

    [Test]
    public static void EveryChoiceIsARealFork()
    {
        foreach (ChoiceDefinition choice in TestGame.Lab.Choices)
        {
            Check.Equal(2, choice.Options.Count, $"「{choice.Id}」应当正好两个选项。");

            List<string> stances = [.. choice.Options.Select(o => o.StanceId)];
            Check.Equal(
                stances.Count,
                stances.Distinct(StringComparer.Ordinal).Count(),
                $"「{choice.Id}」的两个选项指向同一条立场，不构成分叉。");

            foreach (ChoiceOption option in choice.Options)
            {
                Check.True(option.Modifiers.Count > 0, $"「{choice.Id}/{option.Id}」没有数值代价。");
            }
        }
    }

    [Test]
    public static void EveryStanceCanReachTheEndingThreshold()
    {
        GameContent content = TestGame.Lab;
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
        GameContent content = TestGame.Lab;

        foreach (EndingDefinition ending in content.Endings)
        {
            bool found = content.Achievements.Any(
                a => a.Unlock is OwnedCondition { Kind: OwnedKind.Ending } owned
                     && string.Equals(owned.Id, ending.Id, StringComparison.Ordinal));

            Check.True(found, $"结局「{ending.Id}」没有对应的成就。");
        }
    }

    [Test]
    public static void EthicsIsProducedOnlyByWatchers()
    {
        // 设计要点：伦理值不来自规模，只来自"有谁在看"。
        // 所以铺满样本农场不该产出任何伦理值，而放一台伦委会就该开始涨。
        GameEngine engine = TestGame.CreateLab(out _);

        engine.State.Cookies = 1e15;
        // 铺满样本农场——它不带 awake 标签，所以不产出任何伦理值。
        engine.State.BuildingCounts["sample_farm"] = 200;
        engine.MarkDirty();
        engine.Simulate(60);
        Check.Close(0, engine.State.GetCounter(LabContent.EthicsCounterKey), 1e-9);

        // 加入在场者（觉醒区 10 + 伦理委员会 10 = 20）后，伦理值应当开始累积。
        engine.State.BuildingCounts["ethics_board"] = 10;
        engine.State.BuildingCounts["awakening_zone"] = 10;
        engine.MarkDirty();
        engine.Simulate(60);

        double ethics = engine.State.GetCounter(LabContent.EthicsCounterKey);
        Check.True(ethics > 0, "有在场者之后伦理值必须开始增长。");
        Check.Close(20 * 60 / LabContent.WatchersPerPointPerSecond, ethics, 1e-6);
    }

    [Test]
    public static void RobotWalksAllSevenBatches()
    {
        // 与九命包的 G4 同构：证明这批门槛在真实曲线下**走得完**，
        // 而不只是"配置看起来合理"。第 2.7 阶段就是被这类测试救回来的。
        GameEngine engine = TestGame.CreateLab(out _, seed: 20240924);
        List<(int Batch, double Hours)> timeline = [];

        for (int round = 0; round < 6000 && engine.State.Era < 7; round++)
        {
            for (int i = 0; i < 8; i++) engine.Click();
            TestGame.BuyGreedily(engine);

            for (int i = engine.State.GoldenCookies.Count - 1; i >= 0; i--)
                engine.ClickGoldenCookie(engine.State.GoldenCookies[i].InstanceId);

            if (engine.EraGate.CanAdvance)
            {
                int before = engine.State.Era;
                engine.Ascend();
                timeline.Add((before, engine.State.PlayTimeSeconds / 3600));
            }

            engine.Simulate(30);
        }

        foreach ((int batch, double hours) in timeline)
            Console.WriteLine($"      第 {batch} 批结束于 {hours:F1} 游戏小时");

        Check.Equal(7, engine.State.Era, $"机器人只走到第 {engine.State.Era} 批——某批的完成条件可能不可达。");
        Check.Equal(6, timeline.Count, "应当正好舍命 6 次。");
    }
}
