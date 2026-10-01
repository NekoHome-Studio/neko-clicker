using NekoClicker.Core.Content;

namespace NekoClicker.Content.Civ;

/// <summary>
/// 限时增益表（9 条）。<para>
/// 天灾带来的东西不全是坏事：丰收年能让整片地里的产能翻倍，技术突破能让她跳一整代，
/// 但洪水、瘟疫、长冬、陨石也很真实。数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比——<b>换包换的是叙事，不是手感</b>。
/// </para>
/// <para>
/// 第 3 层（城墙 / 帝国）的规则是"天灾间隔更稀"，所以那一层里每条增益的相对价值更高；
/// 第 5 层的间隔最密，于是增益在这层几乎连成一片——这是刻意的：末层的规则就是"什么都快"。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "harvest_year",
            Name = "丰收年",
            Icon = "🌾",
            Description = "雨来得正好，一整个季节什么都没坏。仓里的东西多到要新盖一间。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "all_nighter",
            Name = "通宵",
            Icon = "🌙",
            Description = "她一个人干到天亮。爪子上的茧裂了两次，但没有一块石头是随便放的。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "breakthrough",
            Name = "技术突破",
            Icon = "💡",
            Description = "她盯着一块烧过的石头看了三天，然后明白了。那一年之后所有的活都快了一截。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "golden_age",
            Name = "黄金时代",
            Icon = "✨",
            Description = "整整一代人没打过仗、没饿过肚子。她站在城墙上往下看，第一次觉得这些东西真的留得住。全部产量 ×30。",
            Duration = 45,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(30)],
        },
        new()
        {
            Id = "flood",
            Name = "洪水",
            Icon = "🌊",
            Description = "水从低处涨上来，第一层的东西全泡了。她站在屋顶上看着，没有下水去捞。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "plague",
            Name = "瘟疫",
            Icon = "🦠",
            Description = "集市先静下来，然后是整条街。她把「不要聚在一起」写在了墙上，那时候字还没几个人认得。全部产量 ×0.6。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "long_winter",
            Name = "长冬",
            Icon = "❄️",
            Description = "冬天长到所有人都开始怀疑还有没有春天。她把最后一批种子分成了三份。全部产量 ×0.7。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "meteor",
            Name = "陨石",
            Icon = "☄️",
            Description = "天上掉下来一块，砸掉了半个广场。她是最先跑过去的人——因为那块石头能用。全部产量 ×0.75。",
            Duration = 54,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.75)],
            IsDebuff = true,
        },
        new()
        {
            Id = "night_shift",
            Name = "夜里的学院",
            Icon = "🕯️",
            Description = "灯一盏都没熄，有几个学生算到了天亮。学院产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("academy", 30)],
        },
    ];
}
