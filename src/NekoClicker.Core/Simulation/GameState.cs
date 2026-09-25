namespace NekoClicker.Core;

/// <summary>
/// 一个存档的全部可变状态。<para>
/// 刻意做成"纯数据 + 公开读写属性"：存档序列化、测试构造、调试注入都变得直接，
/// 不需要反射或私有字段访问。所有业务规则都在 <see cref="GameEngine"/> 与各 System 中，
/// 因此外部代码即使直接改了 <see cref="Cookies"/> 也只是改了数据，不会绕过事件与成就检查—— 
/// 需要走流程时请调用引擎的购买/点击 API。
/// </para>
/// </summary>
public sealed class GameState
{
    // ---------- 货币与统计 ----------

    /// <summary>当前持有的主货币。</summary>
    public double Cookies { get; set; }

    /// <summary>本次转生周期内累计赚取量。</summary>
    public double CookiesEarnedThisRun { get; set; }

    /// <summary>含历次转生的累计赚取量（转生等级由它决定，转生不清零）。</summary>
    public double CookiesEarnedAllTime { get; set; }

    /// <summary>鼠标点击累计产生的货币。</summary>
    public double HandMadeCookies { get; set; }

    /// <summary>累计点击次数。</summary>
    public double TotalClicks { get; set; }

    /// <summary>累计点中金猫的次数。</summary>
    public double GoldenCookiesClicked { get; set; }

    // ---------- 转生 ----------

    /// <summary>转生等级。</summary>
    public int PrestigeLevel { get; set; }

    /// <summary>当前持有的转生货币。</summary>
    public double PrestigeChips { get; set; }

    /// <summary>历史上花掉的转生货币。</summary>
    public double PrestigeChipsSpent { get; set; }

    /// <summary>转生次数。</summary>
    public int Ascensions { get; set; }

    // ---------- 纪元（转生分层） ----------

    /// <summary>当前纪元（层号）；没有分层的包恒为 1。</summary>
    public int Era { get; set; } = 1;

    /// <summary>已经完成（已舍命离开）的层号集合。</summary>
    public HashSet<int> EraCompleted { get; } = [];

    /// <summary>各层的完成记录（层号 → 记录），用于结算叙事与统计。</summary>
    public Dictionary<int, EraRecord> EraHistory { get; } = [];

    /// <summary>进入当前层时的累计游玩秒数，用于算"本层耗时"。</summary>
    public double EraEnteredPlayTimeSeconds { get; set; }

    // ---------- 叙事（图鉴） ----------

    /// <summary>已释放的叙事条目 id 集合。</summary>
    public HashSet<string> LoreUnlocked { get; } = new(StringComparer.Ordinal);

    /// <summary>已释放但玩家还没点掉的弹窗条目 id（按释放顺序）。</summary>
    public List<string> PendingLorePopups { get; } = [];

    // ---------- 选择与立场 ----------

    /// <summary>
    /// 已作答的选择：选择 id → 选中的选项 id。<para>
    /// 存"选了哪个"而不只是"答过了"——选项自带的修饰符要按它决定生效哪一个。
    /// 与成就同级：<b>跨舍命与转生保留</b>，它是"发生过的事"，不是本轮进度。
    /// </para>
    /// </summary>
    public Dictionary<string, string> ChoiceAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>已触发但玩家还没作答的选择 id（按触发顺序）。</summary>
    public List<string> PendingChoices { get; } = [];

    /// <summary>各立场的累计权重（立场 id → 权重）。主导立场 = 权重最高者。</summary>
    public Dictionary<string, int> StanceWeights { get; } = new(StringComparer.Ordinal);

    /// <summary>取某立场的当前权重。</summary>
    /// <param name="stanceId">立场 id。</param>
    public int StanceWeight(string stanceId)
        => StanceWeights.TryGetValue(stanceId, out int weight) ? weight : 0;

    /// <summary>是否已作答某次选择。</summary>
    /// <param name="choiceId">选择 id。</param>
    public bool HasChoice(string choiceId) => ChoiceAnswers.ContainsKey(choiceId);

    /// <summary>某次选择选中的选项 id；未作答返回 <c>null</c>。</summary>
    /// <param name="choiceId">选择 id。</param>
    public string? AnswerOf(string choiceId)
        => ChoiceAnswers.TryGetValue(choiceId, out string? optionId) ? optionId : null;

    // ---------- 时间 ----------

    /// <summary>存档创建时刻。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>上次存档时刻（离线收益由它与当前时间之差算出）。</summary>
    public DateTimeOffset LastSavedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>累计模拟时长（秒）。</summary>
    public double PlayTimeSeconds { get; set; }

    /// <summary>累计（含离线的）模拟帧数，用于诊断。</summary>
    public long TickCount { get; set; }

    // ---------- 金猫 ----------

    /// <summary>距离下一次金猫出现还剩的秒数。</summary>
    public double GoldenCookieCountdown { get; set; }

    /// <summary>是否已经历过第一次金猫（用于"新手更快见到"的加速）。</summary>
    public bool GoldenCookieIntroduced { get; set; }

    // ---------- 随机数状态（保证存档后序列可复现） ----------

    /// <summary>PRNG 状态字 0。</summary>
    public ulong RandomState0 { get; set; }

    /// <summary>PRNG 状态字 1。</summary>
    public ulong RandomState1 { get; set; }

    // ---------- 内容持有 ----------

    /// <summary>建筑 id → 持有数量。</summary>
    public Dictionary<string, int> BuildingCounts { get; } = new(StringComparer.Ordinal);

    /// <summary>升级 id → 已购次数。</summary>
    public Dictionary<string, int> UpgradeCounts { get; } = new(StringComparer.Ordinal);

    /// <summary>已解锁成就 id 集合。</summary>
    public HashSet<string> Achievements { get; } = new(StringComparer.Ordinal);

    /// <summary>当前生效的增益。</summary>
    public List<ActiveBuff> Buffs { get; } = [];

    /// <summary>场上尚未被点掉的金猫。</summary>
    public List<GoldenCookieSpawn> GoldenCookies { get; } = [];

    /// <summary>自定义计数器，供内容/模块使用。</summary>
    public Dictionary<string, double> Counters { get; } = new(StringComparer.Ordinal);

    /// <summary>自定义字符串元数据（存档槽名、玩家备注等）。</summary>
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.Ordinal);

    // ---------- 便捷读取 ----------

    /// <summary>某建筑的持有数量。</summary>
    public int BuildingCount(string id) => BuildingCounts.TryGetValue(id, out int n) ? n : 0;

    /// <summary>某升级的已购次数。</summary>
    public int UpgradeCount(string id) => UpgradeCounts.TryGetValue(id, out int n) ? n : 0;

    /// <summary>读取自定义计数器。</summary>
    public double GetCounter(string key) => Counters.TryGetValue(key, out double v) ? v : 0;

    /// <summary>累加自定义计数器。</summary>
    public void AddCounter(string key, double delta) => Counters[key] = GetCounter(key) + delta;

    /// <summary>所有建筑数量之和。</summary>
    public double TotalBuildings()
    {
        double sum = 0;
        foreach (int n in BuildingCounts.Values) sum += n;
        return sum;
    }
}

/// <summary>一层的完成记录（舍命离开时写入）。</summary>
public sealed class EraRecord
{
    /// <summary>层号。</summary>
    public int Index { get; set; }

    /// <summary>本层实际耗费的游玩秒数。</summary>
    public double PlayTimeSeconds { get; set; }

    /// <summary>本层的累计赚取。</summary>
    public double CookiesEarned { get; set; }

    /// <summary>离开本层时结算到的转生货币。</summary>
    public double ChipsGained { get; set; }

    /// <summary>离开时刻。</summary>
    public DateTimeOffset CompletedAt { get; set; }
}

/// <summary>一个正在生效的增益实例。</summary>
public sealed class ActiveBuff
{
    /// <summary>增益定义的 id。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>剩余秒数。</summary>
    public double RemainingSeconds { get; set; }

    /// <summary>本次生效的总时长（用于画进度条）。</summary>
    public double TotalSeconds { get; set; }

    /// <summary>叠加层数。</summary>
    public int Stacks { get; set; } = 1;

    /// <summary>剩余时间比例 [0,1]。</summary>
    public double Progress => TotalSeconds <= 0 ? 0 : Math.Clamp(RemainingSeconds / TotalSeconds, 0, 1);
}

/// <summary>一只场上等待点击的金猫。</summary>
public sealed class GoldenCookieSpawn
{
    /// <summary>实例 id（同一只金猫在存档往返后仍是同一个 id）。</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>剩余停留秒数。</summary>
    public double RemainingSeconds { get; set; }

    /// <summary>总停留秒数。</summary>
    public double LifetimeSeconds { get; set; }

    /// <summary>归一化横坐标 [0,1]，UI 自行映射到屏幕。</summary>
    public double X { get; set; }

    /// <summary>归一化纵坐标 [0,1]。</summary>
    public double Y { get; set; }

    /// <summary>强制结果 id（调试/剧本用）；为空则按权重抽取。</summary>
    public string? ForcedOutcomeId { get; set; }
}

/// <summary>通知类型，UI 据此决定颜色/图标。</summary>
public enum NotificationKind
{
    /// <summary>普通信息。</summary>
    Info,

    /// <summary>正面（购买成功、成就解锁）。</summary>
    Success,

    /// <summary>警告（买不起、条件不足）。</summary>
    Warning,

    /// <summary>稀有事件（金猫、转生）。</summary>
    Rare,
}

/// <summary>一条给玩家看的消息（原版的 <c>Game.Notify</c>）。</summary>
/// <param name="Message">正文。</param>
/// <param name="Icon">图标。</param>
/// <param name="Kind">类型。</param>
/// <param name="Timestamp">产生时刻（模拟时钟秒数）。</param>
public sealed record GameNotification(string Message, string Icon, NotificationKind Kind, double Timestamp);
