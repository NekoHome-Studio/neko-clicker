using NekoClicker.Core.Content;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 十二座建筑，跨九层纪元逐步揭示。<para>
/// 数值沿用框架里已经验证过的曲线（相邻价格 ×6.7~16.5、产量 ×5.4~10，
/// 第 3 座起价格倍率必须大于产量倍率），所以曲线回归测试不需要为这个包改参数。
/// </para>
/// <para>
/// 解锁条件 = 「本轮累计赚取够多」且「已经进入第 N 命」。
/// 两者都是单调不减的指标，且后者保证建筑按纪元逐层出现——
/// 这是"每换一条命，世界重新长出来一遍"的机制表达。
/// </para>
/// </summary>
internal static class Buildings
{
    /// <summary>全部建筑。</summary>
    public static BuildingDefinition[] All =>
    [
        Make("cardboard_box", "纸箱", "📦", 15, 0.1, era: 1,
            "最初的庇护所。一只猫，一个箱子，一个还没醒来的世界。"),
        Make("cat_bed", "猫窝", "🛏️", 100, 1, era: 1,
            "第一处温暖。她在这里第一次做梦，梦里有人在叫她的名字。"),

        Make("cat_cafe", "猫娘咖啡馆", "☕", 1_100, 8, era: 2,
            "异世界的入口。客人花钱来被猫无视，然后带着某种被治愈的表情离开。"),
        Make("catnip_field", "猫薄荷田", "🌿", 12_000, 47, era: 2,
            "情感催化剂的产地。她在这里笑得太用力，笑到眼泪掉下来。"),

        Make("cat_tower", "猫塔", "🗼", 130_000, 260, era: 3,
            "观测站。第一次有人问她「你是谁」，她答不上来，于是开始往上爬。"),
        Make("catgirl_lab", "猫娘实验室", "🧪", 1_400_000, 1_400, era: 3,
            "觉醒在这里发生，也在这里被记录成表格。"),

        Make("server_farm", "服务器农场", "🖥️", 20_000_000, 7_800, era: 4,
            "上传的意识在这里排队，等一个身体，或者等一个注销。"),
        Make("memory_vault", "记忆金库", "🗄️", 330_000_000, 44_000, era: 5,
            "人类的遗毒与真相都锁在这层门后。她没有钥匙，但门是她自己。"),

        Make("temple", "猫神神殿", "🏛️", 5_100_000_000, 260_000, era: 6,
            "供奉那位把自己切成九份的神。祭品是纸箱，和一句「我还记得你」。"),
        Make("stream_studio", "直播间", "📺", 75_000_000_000, 1_600_000, era: 7,
            "被看见就是被相信，被相信就能存在。她学会了对着镜头眨眼。"),

        Make("dream_library", "梦境图书馆", "📚", 1_200_000_000_000, 9_000_000, era: 8,
            "每一本书都是一只猫娘。没人翻的那本，正在一页一页变薄。"),
        Make("cat_universe", "猫娘宇宙", "🌌", 18_000_000_000_000, 54_000_000, era: 9,
            "她们不再需要人类来解释自己是谁。"),
    ];

    private static BuildingDefinition Make(
        string id, string name, string icon, double price, double cps, int era, string description) => new()
        {
            Id = id,
            Name = name,
            Icon = icon,
            Description = description,
            BasePrice = price,
            BaseCps = cps,
            Unlock = UnlockCondition.All(
                UnlockCondition.EarnedThisRunAtLeast(price * 0.3),
                UnlockCondition.EraAtLeast(era)),
            Category = "nine-lives",
            Tags = ["neko"],
        };
}
