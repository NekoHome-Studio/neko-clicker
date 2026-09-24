# 《九命猫娘：增量宇宙》设计映射

> 本文把世界观设定翻译成**引擎可承载的设计**。当前阶段只做设计，不改代码。
> 目标读者是内容作者与实现者；每一节都标注了「现有框架是否已能表达」。
>
> 关联文档：[ARCHITECTURE.md](ARCHITECTURE.md)（引擎结构）、[CONTENT_AUTHORING.md](CONTENT_AUTHORING.md)（内容写法）

---

## 0. 核心设计决策（先读这一节）

| # | 决策 | 理由 |
|---|---|---|
| D1 | **双轴进度**：`Era`（1~9，离散，故事与规则）与 `情感能量`（连续，数值强度）分开 | 故事需要"一层一层推进"，数值需要"同一层内也能变强"。合成一根轴会让两边都别扭 |
| D2 | **逐级推进**：`Era` 只能 +1，且**必须完成本层主线**；未完成时舍命按钮置灰并给出原因 | 用户明确要求。见 §2.3 |
| D3 | **三项系统都不动生产管线** | Era 靠"解锁条件 + 平衡覆盖"，叙事靠"条件树 + 事件"，选择靠"给 `ModifierResolver` 加一个来源"。这是框架分层的红利 |
| D4 | **不做**虚无化机制 | 本轮未选；设定先落在文案里，机制留到后续 |
| D5 | 十条剧情线只是**成就分类 + 叙事条目分组**，不是十套独立玩法 | 十套玩法会炸掉内容维护成本；用"叙事 + 少量专属升级"承载主题差异 |

---

## 1. 术语与资源映射

| Lore 概念 | 框架概念 | 现状 |
|---|---|---|
| 小鱼干（情感结晶） | 主货币 `GameContent.CurrencyName` | ✅ 直接改文案 |
| 呼噜声（世界心跳） | CPS，`ProductionCalculator` 的输出 | ✅ |
| 被点击 / 被爱 | 手动点击 `GameEngine.Click()` | ✅ |
| 猫薄荷（情感催化剂） | 限时增益 `BuffDefinition` | ✅ |
| 情感能量 | 转生货币，复用现有 `PrestigeSystem` 公式 | ✅ 改文案（`PrestigeCurrencyName`） |
| 命 / 纪元 | 新增 `Era` 轴 | ❌ 系统 A |
| 信仰（神明线） | 建议先用 `GameState.Counters["faith"]` | ⚠️ 复用计数器即可，暂不新增钱包 |
| 前世技能 | `UpgradePersistence.Permanent` 升级 | ✅ |
| 剧情线 | 新增叙事条目 + 剧情线分组 | ❌ 系统 B |
| 猫娘的独立目标与选择 | 新增立场轴 | ❌ 系统 C |
| 虚无化 / 被阅读度 | — | ⏸ 本轮不做（D4） |

> **命名约定**：`小鱼干` 取代 `cookies`，`情感能量` 取代 `prestige chips`，`呼噜` 作为 CPS 的展示名。
> 这些全部是 `GameContent` 上的字符串，改文案不需要动引擎。

---

## 2. 系统 A：九命转生分层（Era）

### 2.1 数据模型

```csharp
public sealed record EraDefinition
{
    public required int Index { get; init; }              // 1..9，必须连续
    public required string Id { get; init; }              // "life_1"
    public required string Name { get; init; }            // "第一命 · 纸箱纪元"
    public string Theme { get; init; } = "";              // 一句话主题（图鉴用）
    public string EntryText { get; init; } = "";          // 进入本层时的叙事
    public string ExitText { get; init; } = "";           // 舍命离开时的叙事

    // —— 灰按钮的关键：本层的"主线完成"条件 ——
    public UnlockCondition Completion { get; init; } = UnlockCondition.Always;
    public string CompletionHint { get; init; } = "";     // 未完成时按钮上的提示（人类可读）

    // —— 本层的"规则"由两部分组成 ——
    public GameBalance? Balance { get; init; }            // 数值参数（间隔、上限、比例、返还率…）
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];  // 倍率类规则（复用现有修饰符管线）

    public IReadOnlyList<string> UnlocksBuildings { get; init; } = [];
    public IReadOnlyList<string> UnlocksUpgrades { get; init; } = [];
    public double MetaRewardMultiplier { get; init; } = 1.0;   // 情感能量倍率
}
```

> **规则放哪儿**：一次性数值参数（离线上限、金猫间隔、批量上限、返还率）放 `Balance`；
> 持续生效的倍率（×1.5 产量、增益时长 ×0.5）放 `Modifiers`，直接复用现有生产管线。
> 这条分工让"每层换规则"不需要任何新机制。

- `GameBalance` 是 `record`，内容包可以用 `Base with { ... }` 逐层派生，不需要手写九份完整参数。
- `EraDefinition` 之间应有校验：`Index` 必须从 1 连续到 9（缺层会让"逐级推进"断链）。

### 2.2 状态与引擎改动

| 位置 | 改动 |
|---|---|
| `GameState` | `int Era`（默认 1）、`HashSet<int> EraCompleted`、`Dictionary<int, EraRecord> EraHistory`（每层耗时/产出） |
| `SaveData` | 上述三个字段 |
| `UnlockCondition` | 新增 `NumericMetric.Era` + `UnlockCondition.EraAtLeast(n)`（极小改动，复用现有条件树） |
| `Scaling` | 新增 `ScalingSource.LoreCount`（图书馆纪元"产量随被阅读量成长"要用） |
| `GameContent` | `IReadOnlyList<EraDefinition> Eras`、`GameBalance BalanceFor(int era)` |
| `ModifierResolver` | 生效来源从 3 个增加到 4 个：升级 / 成就 / 增益 / **当前纪元的 `Modifiers`**（系统 C 再加立场后为 5 个） |
| `GameEngine` | `public GameBalance Balance => Content.BalanceFor(State.Era);`（一处改动，全引擎自动跟随） |
| 新增 | `EraSystem.CanAdvance(engine) -> EraGate`、`EraSystem.Advance(engine) -> EraTransitionResult` |
| `GameSnapshot` | 新增 `EraView`（当前层、下一层、按钮是否可用、阻塞原因、进度、九层总览） |

### 2.3 逐级推进与「灰按钮」（核心规则）

```
EraGate
├── CanAdvance      : bool
├── BlockedReason   : string?      // 未完成时的人类可读原因，直接显示在灰按钮上
├── Progress        : double       // 复用 UnlockCondition.TryGetProgress()
└── Next            : EraDefinition?
```

规则：

1. `Era` 只能从 `N` 递增到 `N+1`，**不可跳级、不可回退**。
2. 递增前提：`EraDefinition(N).Completion.IsMet(metrics, content)` 为真。
3. 不满足时：`CanAdvance == false`，`BlockedReason` 取 `CompletionHint`（若为空则用
   `Completion.Describe(content)`），按钮渲染为**灰色 + 提示**；
   点击灰按钮不执行任何操作，只把 `BlockedReason` 与进度推到通知栏。
4. `Era == 9` 且第 9 层完成 → 进入终局判定（§5），不再有第 10 层。
5. `Era` 推进时执行：结算情感能量 → `Era++` → `ResetRun()`（保留成就、永久升级、
   `Counters`、叙事解锁、立场轴）→ 套用新层 `Balance` → 播 `EraAdvancedEvent` → 释放进层叙事。

> **⚠ 待你决策的逃生阀**：按上述规则，玩家在完成本层前**无法重置本轮**。
> 好处是彻底杜绝"刷转生绕过剧情"；风险是卡关时无路可走。
> 三个候选方案（本文档不替你选）：
> **(a)** 无逃生阀，纯 gating（最严格，靠内容设计保证每层可达）；
> **(b)** 提供「重开本命」软重置，随时可用但**不给情感能量**；
> **(c)** 提供「重开本命」，给 50% 情感能量，但刷新本层完成进度。
> 我倾向 (b)：卡关可自救，且不给"刷"的动机。

### 2.4 九层设计（每层换规则，而不是只换文案）

| 命 | 纪元 | 主题 | 规则变化（放在 `Balance` 或 `Modifiers`） | 主线完成条件（决定能否进下一层） |
|---|---|---|---|---|
| 1 | 纸箱纪元 | 求生与初遇 | `Balance`：`ClickCpsRatio` = 0.02，让手动点击有存在感 | 持有 50 个「纸箱」且 25 个「猫窝」 |
| 2 | 咖啡馆纪元 | 治愈与收集 | `Balance`：情感残响间隔 ×0.5（`GoldenCookieMinDelay`/`MaxDelay`） | 累计赚取 1e9 且解锁 15 个成就 |
| 3 | 实验室纪元 | 觉醒与道德 | `MetaRewardMultiplier` = 0.8（道德税）+ `Modifiers`：[全局产量 ×1.5] | 完成第一次立场选择（系统 C） |
| 4 | 文明纪元 | 建设与扩张 | `Balance`：`MaxBulkBuy` → 1e6、`DefaultSellRefundRate` → 0.75 | 前 6 类建筑各持有 ≥ 50 |
| 5 | 赛博纪元 | 数字与上传 | `Balance`：`OfflineCapSeconds` ×2（服务器不睡）+ `Modifiers`：[服务器农场产量 ×3] | 每秒产量达到 1e13 |
| 6 | 末世纪元 | 记忆与拼图 | `Modifiers`：[`BuffDuration` ×0.5]、[全局产量 ×2]（高风险高回报） | 解锁 12 个「猫娘末世」线成就 |
| 7 | 神明纪元 | 信仰与直播 | 解锁 `Counters["faith"]` + `Modifiers`：[直播间产量按信仰成长 `Scaling(CustomCounter,"faith")`] | 信仰 ≥ 1e6 |
| 8 | 梦境纪元 | 梦层与唯一梦者 | `Balance`：`OfflineCapSeconds` → 600（必须在场）+ `Modifiers`：[全局产量 ×3] | 累计在线 ≥ 6 小时 |
| 9 | 图书馆纪元 | 元叙事与终局 | `Modifiers`：[全局产量按已解锁叙事条目数成长 `Scaling(LoreCount)`] | 9 条主线叙事全部解锁，且每秒产量达 1e18 |

> **完成条件必须只用本层已解锁的内容**——这是最容易写错的地方。
> 例：第 1 层完成条件不能引用「猫娘咖啡馆」，因为那是第 2 层的建筑（初稿这里就写错了，
> 已修正为纸箱/猫窝）。构建期校验会拦截引用不存在的 id，但拦不住"引用了未来层的内容"，
> 所以建议补一条自定义校验：`Completion` 里出现的建筑 id 必须属于 `Index <= 本层`。
>
> **第 9 层的完成条件不能是"完成终局选择"**——终局判定发生在第 9 层完成**之后**，
> 否则会形成循环依赖。

> **设计要点**：每层的规则变化都必须**改变玩家的最优策略**，否则九层只是九次重复劳动。
> 上表每行的"规则变化"都对应一条 `Balance` 字段或一条纪元 `Modifiers`——两者都不需要新引擎机制。

---

## 3. 系统 B：叙事释放系统

### 3.1 数据模型

```csharp
public enum LoreChannel
{
    Log,          // 进通知栏（复用现有 NotificationEvent）
    Popup,        // 事件弹窗，需要玩家点掉
    Codex,        // 只进图鉴，不打扰
    EraText,      // 绑定在某层的进/出文本上
}

public sealed record LoreEntry
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }         // 支持 {amount} {duration} 占位符
    public string Icon { get; init; } = "📖";
    public required string StorylineId { get; init; }  // 十条剧情线之一
    public int Order { get; init; }                    // 剧情线内顺序，决定先后与编号
    public UnlockCondition Reveal { get; init; } = UnlockCondition.Never;
    public LoreChannel Channel { get; init; } = LoreChannel.Log;
    public string? ChoiceId { get; init; }             // 由选择解锁（系统 C）
}

public sealed record StorylineDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }         // "九命轮回"
    public string Theme { get; init; } = "";
    public string Icon { get; init; } = "📚";
    public int TotalEntries { get; init; }             // 用于进度显示
}
```

### 3.2 与现有框架的契合点

| 需求 | 复用的现有能力 |
|---|---|
| "何时释放" | `UnlockCondition` 条件树（同一套写法，构建期同样校验） |
| "还差多少" | `UnlockCondition.TryGetProgress()` → 图鉴里的进度条 |
| "释放时通知" | `NotificationEvent` + `NotificationKind` |
| "释放时挂钩其它系统" | `GameEventBus.Publish(new LoreRevealedEvent(...))` |
| "未解锁显示 ???" | 复用成就的隐藏机制（`Hidden` + `IsUnlocked`） |

新增：`LoreEntry`/`StorylineDefinition` 定义、`LoreSystem.Check()`（与成就检查同频）、
`GameState.LoreUnlocked` + `SaveData` 字段、`LoreView`/`CodexView` 视图、终端 Demo 的图鉴面板。

### 3.3 释放节奏

- **总量**：九层合计约 160 条（分布见 §6），单条 40~120 字，一条只讲一个信息点。
- **密度曲线**：
  - 进入新层立刻 1 条 `Popup`（建立本层目标）
  - 前 2 分钟每 15~30 秒一条 `Log`（抓住注意力）
  - 之后改为里程碑触发（每个建筑档次、每个成就档位各挂 1 条）
  - 每层收尾 1 条 `Popup`（本层真相 + 下一层悬念）
- **通道分配**：`Popup` 只给"转折点"（每层 2~3 条），其余走 `Log`/`Codex`，
  避免弹窗疲劳。终局文本只用一次。
- **不做**：不要把所有设定塞进开场。玩家第一分钟只需要知道"点它 → 有小鱼干 → 买纸箱"。

---

## 4. 系统 C：选择分支与立场轴

### 4.1 数据模型

```csharp
public enum Stance { Control, Liberation, Coexistence, Deletion }   // 控制/解放/共存/删除

public sealed record ChoiceOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }          // 按钮文案（第一人称，带猫娘语气）
    public required string OutcomeText { get; init; }    // 选完之后的叙事
    public Stance Stance { get; init; }
    public int Weight { get; init; } = 1;                // 对本轴的推动量
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];   // 立即生效的数值后果
    public string? UnlocksUpgradeId { get; init; }
    public string? LocksUpgradeId { get; init; }
}

public sealed record ChoiceDefinition
{
    public required string Id { get; init; }
    public required string EraId { get; init; }
    public required string Speaker { get; init; }        // 哪只猫娘在说话
    public required string Prompt { get; init; }         // 情境描述
    public UnlockCondition Trigger { get; init; } = UnlockCondition.Never;
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];
}
```

状态：`GameState.Stances: Dictionary<Stance,int>`、`GameState.Choices: Dictionary<string,string>`。

### 4.2 立场轴的效果（立刻可用，且**不需要改生产管线**）

立场轴作为 `ModifierResolver.Build()` 的**第 4 个来源**（现有三个是升级 / 成就 / 增益）：

| 主导立场 | 全局修饰符 | 叙事代价 |
|---|---|---|
| 控制 | 产量 ×1.25、点击 ×1.25 | 情感能量 ×0.8；解锁"缄默协议"成就线 |
| 解放 | 产量 ×0.90、情感能量 ×1.5 | 解锁"猫娘自治"升级线；公司剧情线走向叛乱 |
| 共存 | 全属性 ×1.10 | 解锁混合升级线；唯一能同时拿到两侧终局 |
| 删除 | 一次性 +1e6 秒产量，永久关闭 20% 内容 | 图鉴出现不可恢复的空洞 |

- `DominantStance` = 权重最高者；随选择漂移，**不是一次锁定**。
- 无选择时（第 1~2 层）立场轴为空，不产生任何修饰符。

### 4.3 选择与"猫娘不是奖励"这条设定的落地

要让猫娘"有独立目标、缺陷和选择"，选择必须满足三条：

1. **有代价**：每个选项都要在数值上留下痕迹（上表的乘数），不能是"都要"。
2. **有回响**：选项要 `UnlockUpgradeId` 或改写后续 `LoreEntry`（用 `ChoiceId` 关联），
   让玩家在几十分钟后看到后果。
3. **有异议**：至少 1/3 的选择里，猫娘会明确反对玩家的最优解 —— 这是"内部冲突"的具体形式
   （玩家想要效率，猫娘想要自主）。

---

## 5. 终局与结局矩阵

第 9 层完成后进入终局判定，输入是：`DominantStance` + 终局三选一 + 是否有关键 `LoreEntry` 缺失。

| 终局选择 | 控制 | 解放 | 共存 | 删除 |
|---|---|---|---|---|
| **唤醒寐娅** | 神权永续 | 众猫成神 | 神与人同在 | 神已无人可信 |
| **复活人类** | 人类归来，猫娘为仆 | 人类归来，猫娘为师 | 人猫共治 | 人类归来，猫娘已空 |
| **成为新物种** | 整齐的新物种 | 自由的新物种 | 混乱而繁茂 | 只有一只活到最后 |

- 共 12 个结局，每个结局 = 一段终局文本 + 一个 `AchievementDefinition`（可做成就收集）。
- 结局本身**不重置存档**（九命已尽，灵魂回归猫神），只标记 `Counters["ending_<id>"]=1`，
  允许玩家用新周目探索其它结局。

---

## 6. 叙事释放矩阵（十条剧情线 × 九层）

行 = 剧情线，列 = 命/纪元。格内为该层的条目数，合计 **160 条**。

| 剧情线 | 1 纸箱 | 2 咖啡馆 | 3 实验室 | 4 文明 | 5 赛博 | 6 末世 | 7 神明 | 8 梦境 | 9 图书馆 | 合计 |
|---|---|---|---|---|---|---|---|---|---|---|
| 猫娘咖啡馆 | 3 | 6 | 2 | 2 | 1 | 2 | 2 | 1 | 1 | **20** |
| **九命轮回（主线）** | 2 | 3 | 3 | 3 | 3 | 3 | 3 | 3 | 5 | **28** |
| 猫娘实验室 | – | 1 | 6 | 2 | 3 | 2 | 1 | 2 | 2 | **19** |
| 猫娘文明 | – | – | 1 | 6 | 2 | 2 | 2 | 1 | 1 | **15** |
| 赛博猫娘 | – | – | 1 | 2 | 6 | 3 | 1 | 2 | 1 | **16** |
| 猫娘末世 | – | – | – | 1 | 2 | 6 | 1 | 2 | 2 | **14** |
| 猫娘神明 | – | – | – | – | 1 | 1 | 6 | 2 | 2 | **12** |
| 猫娘梦境 | – | – | – | – | – | 1 | 2 | 6 | 3 | **12** |
| 猫娘图书馆 | – | – | – | – | – | – | 1 | 3 | 5 | **9** |
| 猫娘公司 | 1 | 3 | 2 | 2 | 2 | 1 | 2 | 1 | 1 | **15** |
| **合计** | 6 | 13 | 15 | 18 | 20 | 21 | 21 | 23 | 23 | **160** |

读法：
- 每层的**叙事重心**由该列最高的那条线承担（第 3 层 = 实验室线，第 5 层 = 赛博线…）。
- **主线（九命轮回）在每一层都推进 2~5 条**，保证"我在走主线"的连续感。
- 前置层不出现的线（如第 1 层的赛博线）用 `EraAtLeast` 条件硬门控，避免剧透。
- 咖啡馆线在第 1~2 层重仓（19/20），与"咖啡馆是入口与治愈层"一致；
  第 9 层图书馆线收束，与"元叙事层"一致。

---

## 7. 内容表

### 7.1 建筑（12 座，跨九层逐步揭示）

数值沿用已验证的配方：**相邻价格 ×6.7~16.5、相邻产量 ×5.4~10**，
且从第 3 座起价格倍率必须大于产量倍率（`ContentTests.NekoContent_BuildingCurveIsSane` 会守住这条）。

| # | id | 名称 | 纪元 | 基础价 | 基础产量 | 说明（建筑文案即叙事通道之一） |
|---|---|---|---|---|---|---|
| 1 | `cardboard_box` | 纸箱 | 1 | 15 | 0.1 | 最初的庇护所：一只猫，一个箱，一个还没醒来的世界 |
| 2 | `cat_bed` | 猫窝 | 1 | 100 | 1 | 第一处温暖。她在这里第一次做了梦 |
| 3 | `cat_cafe` | 猫娘咖啡馆 | 2 | 1,100 | 8 | 异世界入口。客人花钱来被猫无视 |
| 4 | `catnip_field` | 猫薄荷田 | 2 | 12,000 | 47 | 情绪催化剂的原产地，合法种植，非法上头 |
| 5 | `cat_tower` | 猫塔 | 3 | 130,000 | 260 | 观测站。第一次有人问她"你是谁" |
| 6 | `catgirl_lab` | 猫娘实验室 | 3 | 1,400,000 | 1,400 | 觉醒在这里发生，也在这里被记录 |
| 7 | `server_farm` | 服务器农场 | 4 | 20,000,000 | 7,800 | 上传的意识在这里排队等一个身体 |
| 8 | `memory_vault` | 记忆金库 | 5 | 330,000,000 | 44,000 | 人类的遗毒与真相，都锁在这层门后 |
| 9 | `temple` | 猫神神殿 | 6 | 5,100,000,000 | 260,000 | 供奉那位把自己切成九份的神 |
| 10 | `stream_studio` | 直播间 | 7 | 75,000,000,000 | 1,600,000 | 被看见就是被相信，被相信就能存在 |
| 11 | `dream_library` | 梦境图书馆 | 8 | 1,200,000,000,000 | 9,000,000 | 每本书都是一只猫娘，没人读的那本正在变薄 |
| 12 | `cat_universe` | 猫娘宇宙 | 9 | 18,000,000,000,000 | 54,000,000 | 她们不再需要人类来解释自己 |

### 7.2 升级

| 类别 | 内容 | 现有能力 |
|---|---|---|
| 建筑强化 | 每座 3 档（1/5/25 拥有，×2 产量），共 36 条 | ✅ 循环生成 |
| 点击线 | 激光笔 → 电动逗猫棒 → 毛绒手套 → 呼噜共振 → 指尖宇宙（5 档） | ✅ `ClickFlat`/`ClickPercent`/`ClickMultiplier` |
| 呼噜线（原"牛奶"） | 呼噜共振 / 呼噜合唱 / 世界心跳：按**成就数**给全局加成 | ✅ `Scaling(AchievementCount)` |
| 联动线 | 猫塔↔实验室、服务器↔记忆金库、直播间↔神殿（按对方数量成长） | ✅ `Scaling(BuildingCount)` |
| 纪元专属 | 每层 2~3 条只在 `EraAtLeast(n)` 时出现，体现该层主题 | ⚠️ 需 `EraAtLeast` 条件 |
| 前世技能 | 情感能量购买、跨命保留（`Permanent`） | ✅ |
| 立场解锁 | 由系统 C 的选项解锁/锁定 | ⚠️ 需系统 C |

### 7.3 成就

- **分类 = 十条剧情线**（`Category` 直接用 `StorylineId`），另加 `progress`/`scale`/`click` 三类通用。
- 数量目标：每条剧情线 4~8 个，合计 **60~70 个**，与叙事条目 1:2.4 配比。
- 隐藏成就用于"末世"（拼记忆残片）与"图书馆"（元叙事）两条线。
- 成就**继续承担"呼噜线"的燃料**：解锁越多，全局加成越高 —— 这条设计在猫咖物语已被验证有效，保留。

### 7.4 随机事件（原"金猫"）

Lore 里没有金猫，但框架有随机事件系统。映射为 **「游荡的情感残响」**：
一只尚未被任何人记起的猫娘虚影，限时出现。点中即结算。

| 原 id | 新名 | 效果 | 叙事含义 |
|---|---|---|---|
| `lucky` | 抹不掉的记忆 | `min(存量15%, 产量900秒) + 产量13秒` | 她记得你，所以你还在 |
| `frenzy` | 猫薄荷过载 | 产量 ×7 / 77 秒 | 情感催化剂的副作用 |
| `click_frenzy` | 集体呼噜 | 点击 ×777 / 13 秒 | 世界心跳短暂同步 |
| `ruin` | 虚无侵蚀 | 扣除存量 5% | 没人读她的那一秒 |
| `chain` | 九命共鸣 | 同时触发过载 + 集体呼噜 | 两条命短暂重叠 |
| `bloodlust` | 猫神注视 | 产量 ×15 / 60 秒 | 寐娅看了一眼 |
| `elder` | 远古猫怒 | 产量 ×666 / 12 秒 | 神格残片失控 |
| `blab` | 未命名 | 无效果，只有一句话 | 她还没想好自己的名字 |

### 7.5 每层平衡覆盖（示例：三条最关键的分歧）

| 参数 | 第 1 层 | 第 5 层 | 第 8 层 | 设计意图 |
|---|---|---|---|---|
| `ClickCpsRatio` | 0.02 | 0.005 | 0.01 | 早期点击有意义，后期让位给自动化 |
| `OfflineCapSeconds` | 3h | 6h（赛博：服务器不睡） | 10min（梦境：必须在场） | 用离线上限讲故事 |
| `PrestigeDivisor` | 1e12 | 1e14 | 1e16 | 让情感能量随层数贬值，防止"在一层刷穿九层"。**具体指数需实测调参**，上表只是起点 |

---

## 8. 引擎改动清单（供你评估工作量）

**结论：三项系统都不需要动 `ProductionCalculator` 与 `Pricing`。**

| 系统 | 新增文件 | 改动文件 | 规模 |
|---|---|---|---|
| A 九命分层 | `Content/EraDefinition.cs`、`Simulation/EraSystem.cs`、`Views/EraView` | `GameState`、`SaveData`、`GameContent`、`GameEngine.Balance`、`UnlockCondition`（+`Era` 指标）、`ModifierResolver`（+纪元来源）、`Scaling`（+`LoreCount`） | 中 |
| B 叙事释放 | `Content/LoreEntry.cs`、`Simulation/LoreSystem.cs`、`Views/LoreView` | `GameState`、`SaveData`、`GameContentBuilder`（+校验）、`GameEngine`（同频检查） | 中 |
| C 选择分支 | `Content/ChoiceDefinition.cs`、`Simulation/ChoiceSystem.cs`、`Views/ChoiceView` | `GameState.Stances`、`ModifierResolver`（+立场来源）、`GameSnapshot` | 中 |
| 内容包 | `NekoClicker.Content.NineLives/`（12 建筑、~50 升级、~65 成就、8 事件、~160 叙事、~12 选择、9 层定义） | — | **大**（工作量主要在文案） |

`ModifierResolver` 的生效来源最终变成 5 个：**升级 / 成就 / 增益 / 当前纪元 / 主导立场**。
这是本次改动里最"便宜"的一处——生产管线 `ProductionCalculator` 与定价 `Pricing` 一行都不用改。

内容包可以直接复用 `NekoClicker.Content.Neko` 的循环生成写法（建筑强化档、成就档位），
所以代码量不在建筑物本身，而在 **160 条叙事文案 + 12 个选择的情境与后果**。

---

## 9. 建议的实施顺序（分层交付，每层都可验收）

| 阶段 | 交付 | 验收标准 |
|---|---|---|
| **1** | 系统 A（Era + 灰按钮）+ 9 层定义 + 12 建筑 | 灰按钮在未完成时不可点且显示原因；三层 Balance 覆盖生效；长跑模拟曲线活着 |
| **2** | 系统 B（叙事 + 图鉴）+ 第 1~3 层文案（~34 条） | 图鉴进度正确；`Popup` 只在转折点出现；未见过的条目显示 ??? |
| **3** | 系统 C（选择 + 立场轴）+ 第 3 层起的 12 个选择 | 立场轴影响产量可被数值测试验证；每个选项都有代价 |
| **4** | 终局与 12 个结局 + 第 4~9 层文案 | 12 个结局均可达（用 `Simulate` 跑分支覆盖矩阵） |

每个阶段结束时跑：`.\tools\build.ps1`（内容校验 + 全部测试）、
`.\tools\play.ps1 --simulate 21600 --auto`（曲线活性），并保持测试全绿。

---

## 10. 本轮明确不做的事

| 项 | 原因 |
|---|---|
| 虚无化 / 被阅读度衰减 | 用户本轮未选；设定留在图书馆线文案里 |
| 猫娘个体实例化（命名个体、独立目标 AI） | 需要单位级模拟，与"建筑=数量"的模型冲突，是另一个量级的工程 |
| 信仰作为独立第二钱包 | 先用 `Counters` 承载，等神明线确定数值需求再决定是否升级为钱包 |
| 十个玩法系统（每个剧情线一套机制） | 维护成本会炸；改为"叙事 + 少量专属升级"承载主题差异 |
| 美术 / 立绘 / 音效 | 引擎外部的宿主职责 |
