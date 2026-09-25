namespace NekoClicker.Core.Content;

/// <summary>内容定义不合法时抛出。构建期就失败，避免运行时才发现引用不存在的 id。</summary>
public sealed class GameContentValidationException : Exception
{
    /// <summary>创建异常。</summary>
    /// <param name="errors">全部校验错误。</param>
    public GameContentValidationException(IReadOnlyList<string> errors)
        : base("内容定义校验失败：" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
    {
        Errors = errors;
    }

    /// <summary>全部校验错误。</summary>
    public IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// 一个游戏的完整内容定义：UI 文案 + 平衡参数 + 全部实体。<para>
/// <see cref="GameContent"/> 是不可变的，由 <see cref="GameContentBuilder"/> 构建并校验。
/// 引擎只持有它，从不修改它——所有运行时状态都在 <see cref="GameState"/> 里。
/// 这个划分让同一份内容可以服务多个存档/多个玩家，也让内容可以做成
/// "基础包 + 活动包"增量叠加。
/// </para>
/// </summary>
public sealed class GameContent
{
    /// <summary>游戏标题。</summary>
    public string Title { get; init; } = "Incremental Game";

    /// <summary>主货币名称（单数/复数同形使用）。</summary>
    public string CurrencyName { get; init; } = "cookies";

    /// <summary>主货币图标。</summary>
    public string CurrencyIcon { get; init; } = "🍪";

    /// <summary>点击动作的名称，例如"撸猫"。</summary>
    public string ClickActionName { get; init; } = "Click";

    /// <summary>转生货币名称。</summary>
    public string PrestigeCurrencyName { get; init; } = "prestige chips";

    /// <summary>转生货币图标。</summary>
    public string PrestigeCurrencyIcon { get; init; } = "🌟";

    /// <summary>全局平衡参数。</summary>
    public GameBalance Balance { get; init; } = new();

    /// <summary>建筑列表（保持声明顺序，UI 直接按此展示）。</summary>
    public IReadOnlyList<BuildingDefinition> Buildings { get; init; } = [];

    /// <summary>建筑索引。</summary>
    public IReadOnlyDictionary<string, BuildingDefinition> BuildingById { get; init; }
        = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);

    /// <summary>升级列表。</summary>
    public IReadOnlyList<UpgradeDefinition> Upgrades { get; init; } = [];

    /// <summary>升级索引。</summary>
    public IReadOnlyDictionary<string, UpgradeDefinition> UpgradeById { get; init; }
        = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);

    /// <summary>成就列表。</summary>
    public IReadOnlyList<AchievementDefinition> Achievements { get; init; } = [];

    /// <summary>成就索引。</summary>
    public IReadOnlyDictionary<string, AchievementDefinition> AchievementById { get; init; }
        = new Dictionary<string, AchievementDefinition>(StringComparer.Ordinal);

    /// <summary>增益列表。</summary>
    public IReadOnlyList<BuffDefinition> Buffs { get; init; } = [];

    /// <summary>增益索引。</summary>
    public IReadOnlyDictionary<string, BuffDefinition> BuffById { get; init; }
        = new Dictionary<string, BuffDefinition>(StringComparer.Ordinal);

    /// <summary>金猫结果表。</summary>
    public IReadOnlyList<GoldenCookieOutcome> GoldenCookieOutcomes { get; init; } = [];

    /// <summary>金猫结果表权重总和（预计算，抽取时用）。</summary>
    public double GoldenCookieWeightTotal { get; init; }

    /// <summary>随内容一起注册的模块；引擎创建时会自动挂载。</summary>
    public IReadOnlyList<IGameModule> Modules { get; init; } = [];

    /// <summary>
    /// 纪元（转生分层）列表，按 <see cref="EraDefinition.Index"/> 升序。<para>
    /// 为空表示这个包没有分层转生，转生行为完全等同于经典的单轴 <c>Ascend</c>。
    /// </para>
    /// </summary>
    public IReadOnlyList<EraDefinition> Eras { get; init; } = [];

    /// <summary>纪元索引。</summary>
    public IReadOnlyDictionary<int, EraDefinition> EraByIndex { get; init; }
        = new Dictionary<int, EraDefinition>();

    /// <summary>最大层号；没有分层时为 1。</summary>
    public int MaxEraIndex { get; init; } = 1;

    /// <summary>是否启用了分层转生。</summary>
    public bool HasEras => Eras.Count > 0;

    /// <summary>
    /// 取第 <paramref name="era"/> 层生效的平衡参数。<para>
    /// 这是 <c>Era</c> 进入引擎的接缝之一：引擎只问"这一层用什么数值"，
    /// 不认识任何具体层号，也不出现 <c>if (era == ...)</c>。
    /// </para>
    /// </summary>
    /// <param name="era">层号。</param>
    public GameBalance BalanceFor(int era)
        => EraByIndex.TryGetValue(era, out EraDefinition? definition) && definition.Balance is { } balance
            ? balance
            : Balance;

    /// <summary>按层号查纪元定义。</summary>
    public EraDefinition? FindEra(int index) => EraByIndex.TryGetValue(index, out EraDefinition? d) ? d : null;

    /// <summary>全部叙事条目（按剧情线与序号排序）。</summary>
    public IReadOnlyList<LoreEntry> LoreEntries { get; init; } = [];

    /// <summary>叙事条目索引。</summary>
    public IReadOnlyDictionary<string, LoreEntry> LoreById { get; init; }
        = new Dictionary<string, LoreEntry>(StringComparer.Ordinal);

    /// <summary>剧情线列表。</summary>
    public IReadOnlyList<StorylineDefinition> Storylines { get; init; } = [];

    /// <summary>剧情线索引。</summary>
    public IReadOnlyDictionary<string, StorylineDefinition> StorylineById { get; init; }
        = new Dictionary<string, StorylineDefinition>(StringComparer.Ordinal);

    /// <summary>按 id 查叙事条目。</summary>
    public LoreEntry? FindLore(string id) => LoreById.TryGetValue(id, out LoreEntry? d) ? d : null;

    /// <summary>按 id 查剧情线。</summary>
    public StorylineDefinition? FindStoryline(string id)
        => StorylineById.TryGetValue(id, out StorylineDefinition? d) ? d : null;

    /// <summary>
    /// 立场列表（价值取向轴）。<para>
    /// 为空表示这个包没有立场玩法，选择只会产生各自的修饰符，不累加任何轴。
    /// </para>
    /// </summary>
    public IReadOnlyList<StanceDefinition> Stances { get; init; } = [];

    /// <summary>立场索引。</summary>
    public IReadOnlyDictionary<string, StanceDefinition> StanceById { get; init; }
        = new Dictionary<string, StanceDefinition>(StringComparer.Ordinal);

    /// <summary>选择列表（按声明顺序）。</summary>
    public IReadOnlyList<ChoiceDefinition> Choices { get; init; } = [];

    /// <summary>选择索引。</summary>
    public IReadOnlyDictionary<string, ChoiceDefinition> ChoiceById { get; init; }
        = new Dictionary<string, ChoiceDefinition>(StringComparer.Ordinal);

    /// <summary>是否启用了立场轴。</summary>
    public bool HasStances => Stances.Count > 0;

    /// <summary>按 id 查选择。</summary>
    public ChoiceDefinition? FindChoice(string id) => ChoiceById.TryGetValue(id, out ChoiceDefinition? d) ? d : null;

    /// <summary>按 id 查立场。</summary>
    public StanceDefinition? FindStance(string id) => StanceById.TryGetValue(id, out StanceDefinition? d) ? d : null;

    /// <summary>结局列表（按声明顺序；判定时按 <see cref="EndingDefinition.Priority"/> 排）。</summary>
    public IReadOnlyList<EndingDefinition> Endings { get; init; } = [];

    /// <summary>结局索引。</summary>
    public IReadOnlyDictionary<string, EndingDefinition> EndingById { get; init; }
        = new Dictionary<string, EndingDefinition>(StringComparer.Ordinal);

    /// <summary>按 id 查结局。</summary>
    public EndingDefinition? FindEnding(string id) => EndingById.TryGetValue(id, out EndingDefinition? d) ? d : null;

    /// <summary>某条剧情线下的全部条目，按序号升序。</summary>
    public IReadOnlyList<LoreEntry> LoreOf(string storylineId)
        => [.. LoreEntries.Where(e => string.Equals(e.StorylineId, storylineId, StringComparison.Ordinal)).OrderBy(e => e.Order)];

    /// <summary>空内容（测试与骨架用）。</summary>
    public static GameContent Empty { get; } = new();

    /// <summary>按 id 查建筑。</summary>
    public BuildingDefinition? FindBuilding(string id) => BuildingById.TryGetValue(id, out BuildingDefinition? d) ? d : null;

    /// <summary>按 id 查升级。</summary>
    public UpgradeDefinition? FindUpgrade(string id) => UpgradeById.TryGetValue(id, out UpgradeDefinition? d) ? d : null;

    /// <summary>按 id 查成就。</summary>
    public AchievementDefinition? FindAchievement(string id) => AchievementById.TryGetValue(id, out AchievementDefinition? d) ? d : null;

    /// <summary>按 id 查增益。</summary>
    public BuffDefinition? FindBuff(string id) => BuffById.TryGetValue(id, out BuffDefinition? d) ? d : null;
}
