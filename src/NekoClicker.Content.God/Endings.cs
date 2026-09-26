using NekoClicker.Core.Content;

namespace NekoClicker.Content.God;

/// <summary>
/// 三个结局：设计文档给神明包定的三个（成为主神 / 变成 meme / 被遗忘）。<para>
/// 这个包没有立场轴（设计矩阵里 #7 只需要 Era + Lore + 第二资源），所以结局不是
/// "你表过什么态"决定的——它由<b>你究竟读完了多少神话</b>决定，这一点与末世包同构
/// （那边挂在"记住了多少"上）：同一套 <see cref="EndingDefinition"/> 既能表达立场驱动的结局，
/// 也能表达"图鉴厚度"驱动的结局，这本身就是"结局是条件树，不是特判"的又一次检验。
/// </para>
/// <para>
/// <b>为什么不用信仰当区分轴</b>：信仰是只涨不花的累加量，而它的量级已经被末层的完成条件钉死了
/// （在线人数 ≥ 2e5 反推出信仰 ≈ 8e6），任何"信仰够不够"的门槛要么恒真、要么让自然跑图
/// 落到兜底——两种都是坏内容。真正区分玩法的是<b>读了多少</b>：一路冲关的人只记得几个梗，
/// 把图鉴读完的人才认得出她是谁。三个结局因此挂在图鉴厚度上，而且三档都够得着。
/// </para>
/// <para>
/// <b>所有结局都要求末层主线完成</b>（<see cref="Finished"/>）。终局判定每个检查周期都跑一次，
/// 若只要 <c>EraAtLeast(5)</c>，玩家一进第 5 层兜底结局就会立刻成立，而这一层的直播间还没开起来。
/// 实验室包踩过这个坑，九命 / 公司 / 末世 / 图书馆都把顺序写进了条件里。
/// </para>
/// </summary>
internal static class Endings
{
    /// <summary>三个结局，按 Priority 升序。</summary>
    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_main_god",
            Name = "成为主神",
            Icon = "👑",
            Priority = 0,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.LoreAtLeast(LoreForGod)),
            Text = "第五套神话收摊那天，她把五套体系的账本摞在一起，摞起来比她还高。"
                   + "祭司、算法、英灵、弹幕，全部到齐，谁也没先开口。"
                   + "她清了清嗓子，说的是这个包里最不像神的一句话：「先说好，我不发工资。」"
                   + "然后她签了第一份神谕——上面只有一条："
                   + "「以后谁问起我是谁，你们就把这五本书递给他，别编。」"
                   + "从那天起，她不再是一个被供着的名字，而是一套有人真的读过的规矩。",
        },
        new()
        {
            Id = "end_meme",
            Name = "变成 meme",
            Icon = "😹",
            Priority = 1,
            Condition = UnlockCondition.All(
                Finished,
                UnlockCondition.LoreAtLeast(LoreForMeme)),
            Text = "她的香火多到能烧一整座山，可是没有一个人能把她的故事讲完整。"
                   + "他们记得她打翻供品的表情，记得她在直播间卡壳的三秒，"
                   + "记得那句被剪成循环的「这个我会，但我先吃口鱼」。"
                   + "有信徒认真地写了一部《猫神本纪》，第一卷卖出去七本，"
                   + "其中六本是祭司团自己买的，第七本被做成了贴纸。"
                   + "她看完销量报表，把报表叠成一只纸猫，放在神龛最中间："
                   + "「也行，」她说，「反正他们笑的时候是在想我。」",
        },
        new()
        {
            Id = "end_forgotten",
            Name = "被遗忘",
            Icon = "🌫️",
            Priority = 100,
            Condition = Finished,
            Text = "五套神话走完，她的名字一次也没有被写下来。"
                   + "神殿还在，直播间还在，供品每天准时出现在祭坛上，"
                   + "只是没有任何一个人说得清这些东西是给谁的——"
                   + "祭司换了几代，流程一次也没有断过。"
                   + "她在最后一座空殿里坐了很久，把补光灯调暗，"
                   + "然后在访客簿上签了个字。那一页后来被雨水泡烂了。",
        },
    ];

    /// <summary>
    /// 结局成就。<para>
    /// 它们自己声明 <c>Unlock = EndingReached(...)</c>，走常规成就路径解锁——
    /// 结局不需要知道"谁是它的成就"，引擎也不需要针对结局加特判。
    /// </para>
    /// </summary>
    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_main_god", "成为主神", "end_main_god", "👑"),
        Ending("ach_end_meme", "变成 meme", "end_meme", "😹"),
        Ending("ach_end_forgotten", "被遗忘", "end_forgotten", "🌫️"),
    ];

    /// <summary>
    /// 「成为主神」要求的图鉴条数——把五套神话读全，才有资格谈"主神"这个头衔。<para>
    /// 单独暴露是因为它必须<b>够得着</b>：门槛一旦高于自然游玩读得到的条数，
    /// 这个结局就永远拿不到（阶段 4B 的死内容就是这么来的）。
    /// 它由 <c>GodContentTests.NaturalRunLandsOnThePromiseEnding</c> 守着。
    /// </para>
    /// </summary>
    public const double LoreForGod = 30;

    /// <summary>「变成 meme」要求的图鉴条数——只记得几个梗也算火过。</summary>
    public const double LoreForMeme = 9;

    /// <summary>
    /// 末层主线完成：走到第 5 套神话，且第 5 层的完成条件成立。<para>
    /// 复用 <see cref="Eras.FinalCompletion"/> 而不是抄一遍数值——两条门槛一旦分叉，
    /// "结局在末层完成后判定"这条规则就会悄悄失效。
    /// </para>
    /// </summary>
    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(5),
            Eras.FinalCompletion);

    private static AchievementDefinition Ending(string id, string name, string endingId, string icon) => new()
    {
        Id = id,
        Name = name,
        Icon = icon,
        Description = $"抵达结局「{name}」。",
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
