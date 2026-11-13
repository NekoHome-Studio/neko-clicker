using NekoClicker.Core;
using NekoClicker.Core.Content;
using NekoClicker.Core.Views;

namespace NekoClicker.Core.Tests;

/// <summary>
/// D1 回放带的地基：**确定性是一条被守卫的性质，不是一句主张**。<para>
/// 引擎是命令驱动的（<c>Click</c> / <c>BuyBuilding</c> / …），随机源是 <c>DeterministicRandom</c>、
/// RNG 状态已进存档。于是 **seed + 命令流 = 一整局的完整回放**——这是引擎能给出的最强 bug 报告格式
/// （"这是我的种子和命令日志，复现我的 bug"），也是挑战 / 速通验证的地基（创意池 D1 / A2 / E2）。
/// </para>
/// <para>
/// 这一份把那条性质钉死：
/// <list type="number">
///   <item>同 seed + 同命令序列 ⇒ <c>Save()</c> 逐字节相同（回放可行的前提）；</item>
///   <item>不同 seed ⇒ 必须分叉（否则上一条是橡皮图章——这是判别力证明）；</item>
///   <item>把命令流**录下来再回放**到一台全新引擎 ⇒ 逐字节相同（"seed + 命令流 = 回放"的直接演示）。</item>
/// </list>
/// </para>
/// <para>
/// 命令流刻意横扫**全部**影响状态的输入（点击 / 买建筑 / 买升级 / 金猫 / 表态作答 / 舍命 / 推进）——
/// 漏一个回放就会分叉，那正是 D1 风险注记里点名的坑。时间用 <see cref="ManualClock"/>（只在显式调用时前进），
/// 把"墙上时间"这个潜在非确定源也排除掉。全部不碰磁盘、不碰真人 <c>saves/</c>。
/// </para>
/// </summary>
public static class ReplayTests
{
    /// <summary>同 seed + 同命令序列 ⇒ 存档逐字节相同。这是回放 / 挑战 / bug 复现共同的地基。</summary>
    [Test]
    public static void SameSeedAndCommands_ProduceByteIdenticalSave()
    {
        string a = PlayScripted(seed: 20261113);
        string b = PlayScripted(seed: 20261113);
        Check.Equal(a, b, "同 seed + 同命令序列必须得到逐字节相同的存档（否则回放无从谈起）。");
    }

    /// <summary>
    /// 不同 seed 必须分叉。<para>
    /// 这一条是上一条的**判别力证明**：如果随便两个 seed 都"相同"，那上一条绿得毫无意义。
    /// 金猫时机 / 结果都走 RNG，不同 seed 必然走出不同的局。
    /// </para>
    /// </summary>
    [Test]
    public static void DifferentSeeds_Diverge()
    {
        string a = PlayScripted(seed: 20261113);
        string b = PlayScripted(seed: 999983);
        Check.NotEqual(a, b, "不同 seed 必须走出不同的局（否则「同 seed 相同」那条是橡皮图章）。");
    }

    /// <summary>
    /// 录命令流 → 回放到全新引擎 ⇒ 逐字节相同。<para>
    /// 这是"seed + 命令流 = 完整回放"的直接演示：第一台引擎边玩边把每条命令（含金猫 instanceId、
    /// 表态 choiceId/optionId 这些**状态相关**的输入）记进日志；第二台引擎只凭 seed + 这份日志重放，
    /// 不靠任何别的共享状态。两者存档逐字节相同，就证明命令流足以完整复现一局。
    /// </para>
    /// </summary>
    [Test]
    public static void RecordedCommandLog_ReplaysToByteIdenticalSave()
    {
        var log = new List<Cmd>();
        GameEngine original = TestGame.CreateCompany(out _, seed: 777);
        PlayScript(original, log);
        string expected = original.Save();

        Check.Greater(log.Count, 50, "前置条件：命令流得真的录到东西（含各类命令），否则回放无从验起。");

        GameEngine replayed = TestGame.CreateCompany(out _, seed: 777);
        foreach (Cmd cmd in log) Apply(replayed, cmd);
        string actual = replayed.Save();

        Check.Equal(expected, actual, "只凭 seed + 命令流回放，必须复现逐字节相同的存档。");
    }

    // ------------------------------------------------------------------ 脚本与回放

    /// <summary>一条可序列化的回放命令（type + 参数）。"sim" 的 Seconds 是推进秒数。</summary>
    private sealed record Cmd(string Type, double Seconds = 0, string? Id = null, string? Option = null, int Amount = 0);

    /// <summary>把一条回放命令作用到引擎上——与宿主的命令派发同形，只是这里不经过 HTTP。</summary>
    private static void Apply(GameEngine e, Cmd c)
    {
        switch (c.Type)
        {
            case "sim": e.Simulate(c.Seconds); break;
            case "click": e.Click(); break;
            case "buy": e.BuyBuilding(c.Id!, c.Amount); break;
            case "upgrade": e.BuyUpgrade(c.Id!); break;
            case "golden": e.ClickGoldenCookie(c.Id!); break;
            case "answer": e.AnswerChoice(c.Id!, c.Option!); break;
            case "ascend": e.Ascend(); break;
            default: throw new InvalidOperationException($"未知回放命令：{c.Type}");
        }
    }

    /// <summary>跑一局固定脚本；<paramref name="log"/> 非空时把每条命令录下来（供回放）。</summary>
    private static string PlayScripted(ulong seed)
    {
        GameEngine engine = TestGame.CreateCompany(out _, seed);
        PlayScript(engine, log: null);
        return engine.Save();
    }

    /// <summary>
    /// 确定性脚本：推进 + 点击 + 金猫 + 表态 + 贪心购买 + 舍命，循环若干轮。<para>
    /// 选 company 是因为它**既有表态又有分层转生**，能把所有命令类型都跑到。每一轮的动作只依赖
    /// 引擎当前状态（而状态由 seed + 之前的命令唯一决定），所以整段脚本是确定性的。
    /// </para>
    /// </summary>
    private static void PlayScript(GameEngine e, List<Cmd>? log)
    {
        void Do(Cmd c) { Apply(e, c); log?.Add(c); }

        for (int round = 0; round < 80; round++)
        {
            Do(new Cmd("sim", Seconds: 45));
            for (int i = 0; i < 4; i++) Do(new Cmd("click"));

            // 金猫与表态的 id 是状态相关的——先快照再遍历，点击/作答会改集合。
            foreach (var gc in e.State.GoldenCookies.ToList())
                Do(new Cmd("golden", Id: gc.InstanceId));
            foreach (ChoiceDefinition choice in e.Content.Choices)
                if (e.State.PendingChoices.Contains(choice.Id))
                    Do(new Cmd("answer", Id: choice.Id, Option: choice.Options[0].Id));

            // 贪心购买：先买得起的升级（贵的优先），再买一座买得起的建筑（买满）。
            GameSnapshot snap = e.Snapshot(PurchaseMode.BuyMax);
            for (int i = snap.Upgrades.Count - 1; i >= 0; i--)
            {
                UpgradeView u = snap.Upgrades[i];
                if (u.IsAvailable && u.CanAfford && u.Currency == UpgradeCurrency.Cookies)
                    Do(new Cmd("upgrade", Id: u.Id));
            }
            snap = e.Snapshot(PurchaseMode.BuyMax);
            for (int i = snap.Buildings.Count - 1; i >= 0; i--)
            {
                BuildingView b = snap.Buildings[i];
                if (b.IsUnlocked && b.CanAfford)
                {
                    Do(new Cmd("buy", Id: b.Id, Amount: 0));
                    break;
                }
            }

            if (e.EraGate.CanAdvance) Do(new Cmd("ascend"));
        }
    }
}
