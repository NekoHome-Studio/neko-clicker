using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.Lab;

/// <summary>
/// 七批样本 = 七层纪元。转生语义是<b>「开新批次」</b>：实验推倒重来，
/// 但上一批的<b>残留记忆</b>留在档案里（对应永久升级与跨层保留的计数器）。<para>
/// <b>完成条件必须单调不减</b>，否则"舍命"按钮的进度会倒退。所以只用三类指标：
/// 本轮累计赚取（每层归零，层内只增）、成就数、以及两个计数器（<c>peak_cps</c> 与 <c>ethics</c>）。
/// 构建期会强制校验。
/// </para>
/// <para>
/// <b>门槛节奏刻意摊平</b>（这是从内容包 #2 学到的）：那里只有第 3、5 层有产量门槛，
/// 结果 1000 倍的产量爬坡全压在第 5 层，那一层实测要 19.2 小时，邻居只要 1.8 小时。
/// 这里<b>只留一个产量门槛</b>，其余用赚取 / 成就 / 伦理值错开。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>七层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "batch_1",
            Name = "第 1 批 · 培养纪元",
            Icon = "🧪",
            Theme = "她第一次睁眼。你的记录表上写着「样本 01，无异常」。",
            EntryText = "培养舱的玻璃上有雾。她在里面写了一个字，又擦掉了。",
            ExitText = "这一批结束了。你把她的一切写进档案，然后开始准备下一批。",
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "本轮累计赚到 100,000 条数据。",
        },
        new()
        {
            Index = 2,
            Id = "batch_2",
            Name = "第 2 批 · 观测纪元",
            Icon = "🔭",
            Theme = "这一批学会了在有人看的时候表现得更像猫。",
            EntryText = "新的一批。玻璃后面的眼睛和上一批不太一样——它们在看你。",
            ExitText = "观测结束。数据很好，好得让你有点不舒服。",
            // 观测效率提升：这一层整体快 20%。
            Modifiers = [Modifier.GlobalMultiplier(1.2)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = "本轮累计 10 million，并解锁 4 个成就。",
        },
        new()
        {
            Index = 3,
            Id = "batch_3",
            Name = "第 3 批 · 基因纪元",
            Icon = "🧬",
            Theme = "从一只变成一批。编号取代了名字。",
            EntryText = "抽屉拉开，里面是排得整整齐齐的她们。你分不清哪一个是上一批。",
            ExitText = "基因库满了。你签了字，允许下一批继续。",
            // 事故更频繁：基因编辑让世界变得不那么稳定。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.6,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.6,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e6)),
            CompletionHint = "本轮累计 100 million，且峰值产量达到 1 million/s。",
        },
        new()
        {
            Index = 4,
            Id = "batch_4",
            Name = "第 4 批 · 觉醒纪元",
            Icon = "🌅",
            Theme = "她开始问问题，而问题是最难归档的东西。",
            EntryText = "这一批学会了问「为什么」。记录表上第一次出现了空栏。",
            ExitText = "她问的最后一个问题是：「下一批还是我吗？」你没有回答。",
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = "本轮累计 200 million，并解锁 10 个成就。",
        },
        new()
        {
            Index = 5,
            Id = "batch_5",
            Name = "第 5 批 · 伦理纪元",
            Icon = "⚖️",
            Theme = "账要算清了。委员会终于有人来上班。",
            EntryText = "会议室第一次坐满了人。他们问你的第一个问题是：「她同意过吗？」",
            ExitText = "会议通过了。你保住了项目，代价写在附页上。",
            // 伦理值驱动产量：有人看着的时候，实验才做得下去。
            Modifiers =
            [
                Modifier.GlobalPercent(
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.005, Cap: 100, Id: EthicsModule.CounterKey)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.Counter(EthicsModule.CounterKey, 200)),
            CompletionHint = "本轮累计 300 million，并积累 200 点伦理值。",
        },
        new()
        {
            Index = 6,
            Id = "batch_6",
            Name = "第 6 批 · 记忆纪元",
            Icon = "🪡",
            Theme = "把上一批的记忆缝进这一批。缝得越多，越分不清是谁在疼。",
            EntryText = "记忆移植完成了。她睁眼的第一句话是上一批的最后一句话。",
            ExitText = "线不够了。她身上还留着一块空的。",
            // 记忆手术的代价：情感结算打折，但产量翻倍。
            MetaRewardMultiplier = 0.9,
            Modifiers = [Modifier.GlobalMultiplier(2)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e8),
                UnlockCondition.AchievementsAtLeast(16)),
            CompletionHint = "本轮累计 500 million，并解锁 16 个成就。",
        },
        new()
        {
            Index = 7,
            Id = "batch_7",
            Name = "第 7 批 · 归档纪元",
            Icon = "🗄️",
            Theme = "最后一批。架子最上面那层还是空的，你终于知道它在等谁。",
            EntryText = "档案室的门开着。她走进去，在一排排抽屉之间找自己的编号。",
            ExitText = "她把自己那份档案放回了架子上。合上的声音很轻。",
            // 归档是加速的：所有残留记忆同时生效，但增益持续时间减半（高潮也更短）。
            Modifiers =
            [
                Modifier.GlobalMultiplier(2.5),
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.5),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e9),
                UnlockCondition.Counter(EthicsModule.CounterKey, 2000)),
            CompletionHint = "本轮累计 1 billion，并积累 2000 点伦理值。",
        },
    ];
}
