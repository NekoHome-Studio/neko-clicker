using NekoClicker.Core.Content;

namespace NekoClicker.Content.Apocalypse;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 变异体带来的东西不全是坏事，但负面的比例是全项目最高的：辐射、疫病、断电各占一条。
/// 数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "mutation_wave",
            Name = "变异潮",
            Icon = "🧬",
            Description = "地下那层东西被水泡过之后开始长，长得比谁都快。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "scavenge_frenzy",
            Name = "翻找狂热",
            Icon = "⛏️",
            Description = "她今天不想停。手指划破了就换一只手。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "prewar_cache",
            Name = "战前缓存",
            Icon = "📦",
            Description = "撬开一扇封死的门，里面是整整齐齐码到天花板的箱子。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "blackout",
            Name = "全城断电",
            Icon = "🌑",
            Description = "负载跳了，所有灯同时灭。黑暗中只剩下她自己的呼吸。全部产量 ×0.5。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.5)],
            IsDebuff = true,
        },
        new()
        {
            Id = "radiation_cloud",
            Name = "辐射云",
            Icon = "☢️",
            Description = "风向变了，灰色的雾贴着地面过来。所有人下到地下二层。全部产量 ×0.6。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "plague",
            Name = "疫病",
            Icon = "🦠",
            Description = "从东边来的三个人带来了它。隔离、烧水、等人好。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "shelter_night",
            Name = "避难所之夜",
            Icon = "🛖",
            Description = "外面下着雨，所有人挤在地下二层分一锅汤。避难所产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("shelter", 30)],
        },
        new()
        {
            Id = "salvage_order",
            Name = "拆解有序",
            Icon = "🔧",
            Description = "她画了一张拆解顺序图，拆下来的东西第一次全部用上了。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
