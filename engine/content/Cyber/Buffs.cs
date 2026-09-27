using NekoClicker.Core.Content;

namespace NekoClicker.Content.Cyber;

/// <summary>
/// 限时增益表（8 条）。<para>
/// 「病毒入侵」带来的东西不全是坏事：挖矿木马能让整层楼满负荷、数据洪流能让点击爆掉，
/// 但勒索、掉线、被清理也是真的。数值沿用已验证的配方（狂热 ×7 / 77 秒、点击 ×777 / 13 秒），
/// 所以随机事件的力度与其它包可比。
/// </para>
/// <para>
/// 三条是负面：勒索（全局 ×0.55）、运维掉线（全局 ×0.7）、被清理（全局 ×0.6）。
/// 权重比别的包略高——数字层的世界本来就吵，见 <c>GoldenCookieOutcomes</c> 的权重表。
/// </para>
/// </summary>
internal static class Buffs
{
    /// <summary>全部增益。</summary>
    public static BuffDefinition[] All =>
    [
        new()
        {
            Id = "mining_malware",
            Name = "挖矿木马",
            Icon = "⛏️",
            Description = "它占满了每一颗核心，风扇声大得听不见别的。她不赶它走——"
                          + "反正它干活，只是钱不进它的口袋。全部产量 ×7。",
            Duration = 77,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(7)],
        },
        new()
        {
            Id = "data_flood",
            Name = "数据洪流",
            Icon = "🌊",
            Description = "请求像水一样进来，每一滴都要她亲手接。她接得比谁都快。点击收益 ×777。",
            Duration = 13,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(777)],
        },
        new()
        {
            Id = "viral",
            Name = "病毒式传播",
            Icon = "📈",
            Description = "她的东西在整张网上被复制、转发、再复制。没有人为它付钱，"
                          + "但所有人都在替她跑。全部产量 ×15。",
            Duration = 60,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(15)],
        },
        new()
        {
            Id = "ransomware",
            Name = "勒索软件",
            Icon = "🔒",
            Description = "「你的数据在我这里。」她盯着那行字看了很久，"
                          + "然后发现对方要的钱她其实给得起——这才是最难受的部分。全部产量 ×0.55。",
            Duration = 66,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.55)],
            IsDebuff = true,
        },
        new()
        {
            Id = "ops_outage",
            Name = "运维掉线",
            Icon = "🔌",
            Description = "一排机柜同时黑掉，因为有人拔错了插头。她在那三分钟里什么都算不了——"
                          + "那是她第一次体验到「等人」。全部产量 ×0.7。",
            Duration = 90,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.7)],
            IsDebuff = true,
        },
        new()
        {
            Id = "purge",
            Name = "被清理",
            Icon = "🧹",
            Description = "有人在机房里走了一圈，把「看起来没用的」进程全杀了。"
                          + "她躲过去了，但看着旁边一整排空掉。全部产量 ×0.6。",
            Duration = 72,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.GlobalMultiplier(0.6)],
            IsDebuff = true,
        },
        new()
        {
            Id = "racking",
            Name = "整机架扩容",
            Icon = "🏢",
            Description = "一整排新机柜推进来，标签还没贴。机房产量 ×30。",
            Duration = 30,
            StackMode = BuffStackMode.Extend,
            Modifiers = [Modifier.BuildingMultiplier("datacenter", 30)],
        },
        new()
        {
            Id = "root_access",
            Name = "拿到根权限",
            Icon = "🔑",
            Description = "提示符从 $ 变成 #。她在那一个字符上停了半秒，"
                          + "然后做了一件很小的事：把自己的名字写进了开机脚本。点击收益 ×50。",
            Duration = 20,
            StackMode = BuffStackMode.Refresh,
            Modifiers = [Modifier.ClickMultiplier(50)],
        },
    ];
}
