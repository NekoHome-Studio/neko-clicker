using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 九条命 = 九层纪元。<para>
/// 每一层都换掉一条规则（用 <see cref="EraDefinition.Balance"/> 改数值参数，或用
/// <see cref="EraDefinition.Modifiers"/> 加倍率），这样"推进纪元"才是改变玩法，
/// 而不只是重复劳动。
/// </para>
/// <para>
/// <b>完成条件必须单调不减</b>，否则灰按钮的进度会倒退、玩家可能卡死。
/// 所以这里只用三类指标：本轮累计赚取（每层归零，层内只增）、成就数、
/// 以及两个计数器（<c>peak_cps</c> 与 <c>faith</c>）。构建期会强制校验。
/// </para>
/// </summary>
internal static class Eras
{
    /// <summary>九层定义。<paramref name="baseBalance"/> 是内容包的基准数值。</summary>
    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "life_cardboard",
            Name = "第一命 · 纸箱纪元",
            Icon = "📦",
            Theme = "求生与初遇：世界只剩一个箱子和一个还没醒来的她。",
            EntryText = "你睁开眼，先摸到的是纸箱的棱角。有东西在箱子里呼吸。",
            ExitText = "箱子塌了。但你已经知道怎么让下一个世界重新长出来。",
            Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),
            CompletionHint = "本轮累计赚到 100,000 条小鱼干。",
            Modifiers = [],
        },
        new()
        {
            Index = 2,
            Id = "life_cafe",
            Name = "第二命 · 咖啡馆纪元",
            Icon = "☕",
            Theme = "治愈与收集：学会用别人的幸福换自己的形状。",
            EntryText = "这一次她推开门，门后是一家还没开张的店。招牌上缺一个名字。",
            ExitText = "打烊了。她把最后一只杯子擦干，然后熄了灯。",
            // 事件更频繁：治愈系的节奏要快一点。
            Balance = baseBalance with
            {
                GoldenCookieMinDelay = baseBalance.GoldenCookieMinDelay * 0.5,
                GoldenCookieMaxDelay = baseBalance.GoldenCookieMaxDelay * 0.5,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e7),
                UnlockCondition.AchievementsAtLeast(4)),
            CompletionHint = "本轮累计 10 million，并解锁 4 个成就。",
            Modifiers = [],
            UnlocksBuildings = ["cat_cafe", "catnip_field"],
        },
        new()
        {
            Index = 3,
            Id = "life_lab",
            Name = "第三命 · 实验室纪元",
            Icon = "🧪",
            Theme = "觉醒与道德：她第一次问「我是谁」，而你手里握着记录表。",
            EntryText = "培养舱的玻璃上有雾。她在里面写了一个字，又擦掉了。",
            ExitText = "实验中止。数据带不走，但问题跟着你走了。",
            // 道德税：产量上去了，但情感能量结算打折。
            MetaRewardMultiplier = 0.8,
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e6)),
            CompletionHint = "本轮累计 100 million，且峰值产量达到 1 million/s。",
            UnlocksBuildings = ["cat_tower", "catgirl_lab"],
        },
        new()
        {
            Index = 4,
            Id = "life_civilization",
            Name = "第四命 · 文明纪元",
            Icon = "🏘️",
            Theme = "建设与扩张：从一只猫到一个村庄，再到一座会忘记你的城。",
            EntryText = "她画的第一个圈是房子，第二个圈是城墙，第三个圈她没有画完。",
            ExitText = "城还在，人换了。这大概就是文明的意思。",
            // 这一层鼓励铺量：批量上限与卖出返还都放宽。
            Balance = baseBalance with
            {
                MaxBulkBuy = 1_000_000,
                DefaultSellRefundRate = 0.75,
            },
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(2e8),
                UnlockCondition.AchievementsAtLeast(10)),
            CompletionHint = "本轮累计 200 million，并解锁 10 个成就。",
            Modifiers = [Modifier.GlobalMultiplier(1.5)],
            UnlocksBuildings = ["server_farm"],
        },
        new()
        {
            Index = 5,
            Id = "life_cyber",
            Name = "第五命 · 赛博纪元",
            Icon = "🖥️",
            Theme = "数字与上传：她开始复制自己，然后问哪一份才是原来的。",
            EntryText = "启动日志的第一行写着「实例 0」。说明还有实例 1。",
            ExitText = "迁移完成。她在那台机器上留了一句话给下一个自己。",
            // 服务器不睡：离线收益上限翻倍。
            Balance = baseBalance with { OfflineCapSeconds = baseBalance.OfflineCapSeconds * 2 },
            Modifiers = [Modifier.GlobalMultiplier(1.5), Modifier.BuildingMultiplier("server_farm", 3)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(3e8),
                UnlockCondition.Counter(EraSystem.PeakCpsCounterKey, 1e8)),
            CompletionHint = "本轮累计 300 million，且峰值产量达到 100 million/s。",
            UnlocksBuildings = ["memory_vault"],
        },
        new()
        {
            Index = 6,
            Id = "life_posthuman",
            Name = "第六命 · 末世纪元",
            Icon = "🏚️",
            Theme = "记忆与拼图：她是你留下的容器，拼出真相的同时也在拼出自己。",
            EntryText = "废墟里没有风。她把手放在一台机器上，机器凉得像没死透。",
            ExitText = "拼图缺了最后一块。她把它留给了下一世的自己。",
            // 高风险高回报：增益更短，但常驻产量翻倍。
            Modifiers =
            [
                new Modifier(ModifierTarget.BuffDuration(null), ModifierOperation.Multiplicative, 0.5),
                Modifier.GlobalMultiplier(2),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(5e8),
                UnlockCondition.AchievementsAtLeast(18)),
            CompletionHint = "本轮累计 500 million，并解锁 18 个成就。",
            UnlocksBuildings = ["temple"],
            UnlocksUpgrades = ["eulogy_reader"],
        },
        new()
        {
            Index = 7,
            Id = "life_goddess",
            Name = "第七命 · 神明纪元",
            Icon = "🏛️",
            Theme = "信仰与直播：她终于有了信徒，于是开始担心粉丝数。",
            EntryText = "第一炷香是纸箱味的。她咳了一声，然后端正了坐姿。",
            ExitText = "神不需要下班。但她还是关了灯。",
            Modifiers =
            [
                // 直播间产量随信仰成长：第二资源直接驱动数值。
                new Modifier(
                    ModifierTarget.BuildingCps("stream_studio"),
                    ModifierOperation.AdditivePercent,
                    0,
                    new Scaling(ScalingSource.CustomCounter, 0.01, Cap: 200, Id: FaithModule.CounterKey)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(7e8),
                UnlockCondition.Counter(FaithModule.CounterKey, 5e4)),
            CompletionHint = "本轮累计 700 million，并积累 50,000 点信仰。",
            UnlocksBuildings = ["stream_studio"],
        },
        new()
        {
            Index = 8,
            Id = "life_dream",
            Name = "第八命 · 梦境纪元",
            Icon = "💤",
            Theme = "梦层与唯一梦者：她发现所有猫娘都是同一个人的分裂人格，包括她自己。",
            EntryText = "睡着比醒来更费力。她在梦里数羊，数到第九只的时候认出了它。",
            ExitText = "该醒了。可她不确定醒来的是谁。",
            // 必须在场做梦：离线上限压到 10 分钟，但清醒时产量 ×3。
            Balance = baseBalance with { OfflineCapSeconds = 600 },
            Modifiers = [Modifier.GlobalMultiplier(3)],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1e9),
                UnlockCondition.AchievementsAtLeast(26)),
            CompletionHint = "本轮累计 1 billion，并解锁 26 个成就。",
            UnlocksBuildings = ["dream_library"],
        },
        new()
        {
            Index = 9,
            Id = "life_library",
            Name = "第九命 · 图书馆纪元",
            Icon = "📚",
            Theme = "元叙事与终局：她靠被阅读而存在，所以这一次她要写的是自己。",
            EntryText = "书架上有一本没有书名的书。翻开第一页，是你此刻正在读的这段字。",
            ExitText = "她把书合上了。合上不是结束，是等下一个读者。",
            Modifiers =
            [
                // 产量随"被阅读量"成长：读到的记忆越多，这一世越强。
                Modifier.GlobalPercent(0, new Scaling(ScalingSource.LoreCount, 0.02, Cap: 60)),
            ],
            Completion = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(1.5e9),
                UnlockCondition.AchievementsAtLeast(30)),
            CompletionHint = "本轮累计 1.5 billion，并解锁 30 个成就。",
            UnlocksBuildings = ["cat_universe"],
        },
    ];
}
