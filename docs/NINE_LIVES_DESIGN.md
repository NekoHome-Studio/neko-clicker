# 《九命猫娘：增量宇宙》设计映射

> 本文把世界观设定翻译成**引擎可承载的设计**。当前阶段只做设计，不改代码。
>
> 关联文档：[ARCHITECTURE.md](ARCHITECTURE.md)（引擎结构）、[CONTENT_AUTHORING.md](CONTENT_AUTHORING.md)（内容写法）
>
> **要动手时的入口是 [ROADMAP.md](ROADMAP.md)**：那里有全部已定决策、架构不变量、
> 能力依赖图、接口契约与阶段验收。本文只负责"设定 → 设计"的翻译。

---

## 0. 结构：一个核心 + 十个内容包

世界观有**双层结构**，这一点决定了整个工程的组织方式：

```
共用核心（机械词汇表）
  九命 = 转生      呼噜 = 自动产出    小鱼干 = 货币
  纸箱 = 建筑      猫薄荷 = buff      激光笔 = 点击
        │
        └── 十个内容包：同一套机制，十种转生语义、十种结局、十种语气
```

**十个「剧情 idea」不是同一游戏里的十条剧情线，而是十个独立内容包**：
每一个都有自己的 `转生=` 释义（重装修 / 耗命 / 新批次 / 时代更替 / 迁服务器 /
重启文明 / 换神话体系 / 梦醒 / 写新书 / 公司重组）和自己的 `结局=`。

这与框架的核心主张完全对齐——**换内容包即换游戏**。所以本设计的目标不是"做一个游戏"，
而是**做一套能让十个游戏共用的引擎能力，然后逐个上内容包**。

### 核心设计决策

| # | 决策 | 理由 |
|---|---|---|
| D1 | **双轴进度**：`Era`（离散，管故事与规则）与 `情感能量`（连续，管数值强度）分开 | 故事要一层层推进，数值要同层内也能变强；合成一根轴两边都别扭 |
| D2 | **逐级推进**：`Era` 只能 +1，且必须完成本层主线；未完成时按钮置灰并显示原因 | 用户明确要求 |
| D3 | **十个包共用同一套机制**，差异靠内容包 + **转生语义** + 语气承载，不做十套玩法 | 十套玩法会炸掉维护成本 |
| D4 | **`Era` 是唯一的必经之路** | 十个包里 9 个都需要它（见 §2 矩阵），且它同时是叙事分层与"逐级推进"的载体 |
| D5 | **先做零引擎改动的包（咖啡馆）验证"换内容包即换游戏"，再投入引擎能力** | 先证明框架主张，再扩大投入 |
| D6 | 三项系统都不动生产管线 | Era 靠"解锁条件 + 平衡覆盖 + 纪元 Modifiers"，叙事靠"条件树 + 事件"，选择靠"给 `ModifierResolver` 加来源" |

---

## 1. 术语与资源映射（共享核心）

| Lore 概念 | 框架概念 | 现状 |
|---|---|---|
| 小鱼干 | 主货币 `GameContent.CurrencyName` | ✅ 改文案 |
| 呼噜声 | CPS，`ProductionCalculator` 的输出 | ✅ |
| 激光笔 / 点击 | 手动点击 `GameEngine.Click()` | ✅ |
| 猫薄荷 | 限时增益 `BuffDefinition` | ✅ |
| 情感能量 | 转生货币，复用 `PrestigeSystem` 公式 | ✅ 改文案 |
| 纸箱 / 猫窝 / 猫塔… | `BuildingDefinition`，按 `Era` 分层揭示 | ✅ |
| 命 / 轮回 / 批次 / 时代 / 服务器 | **`Era`（同一机制的十种叙事包装）** | ❌ 系统 A |
| 前世技能 / 常客记忆 / 残留记忆 | `UpgradePersistence.Permanent` 升级 | ✅ |
| 幸福感 / 信仰 / 士气 | `GameState.Counters[...]` | ✅ 复用计数器 |
| 剧情释放（日志/弹窗/图鉴） | 叙事条目 + 剧情线分组 | ❌ 系统 B |
| 独立目标与选择 | 立场轴 | ❌ 系统 C |
| 被阅读度 / 虚无化 | 衰减机制 | ❌ 系统 D（仅图书馆包需要） |

> **十个包的差异，主要就是上表"改文案"与"换 `Era` 语义"的组合。**
> 真正需要新写引擎代码的只有 A/B/C/D 四项能力。

---

## 2. 十个内容包 × 引擎能力矩阵

| # | 内容包 | 语气 | 转生语义 | 需要 Era | 需要 Lore | 需要 Choice | 还需别的能力 | 用今天的框架能做吗 |
|---|---|---|---|---|---|---|---|---|
| 1 | 猫娘咖啡馆 | 轻松治愈 | 重装修，保留常客记忆 | – | 可选 | – | — | ✅ **能**（**已落地**：`src/NekoClicker.Content.Cafe/`） |
| 2 | 九命轮回 | 主线·最契合转生 | 耗一命，保留记忆 | ✅ | ✅ | 可选 | — | ✅ **已落地**：`src/NekoClicker.Content.NineLives/` |
| 3 | 猫娘实验室 | 道德黑残深 | 新批次，残留记忆 | ✅ | ✅ | ✅ | — | ✅ **已落地**：`src/NekoClicker.Content.Lab/` |
| 4 | 猫娘文明 | 文明演进 | 时代更替至星际 | ✅ | ✅ | – | — | ❌ |
| 5 | 赛博猫娘 | 数字层 | 迁服务器 | ✅ | ✅ | – | — | ❌ |
| 6 | 猫娘末世 | 后人类 | 重启文明，保留上纪元猫娘 | ✅ | ✅ | – | **跨转生继承** | ✅ **已落地**：`src/NekoClicker.Content.Apocalypse/` |
| 7 | 猫娘神明 | 轻松搞笑 meta | 切换神话体系 | ✅ | ✅ | – | — | ❌ |
| 8 | 猫娘梦境 | 梦层 | 梦醒 / 入梦嵌套 | ✅ | ✅ | – | — | ❌ |
| 9 | 猫娘图书馆 | 文艺 meta·轻悬疑 | 写新书开新世界观 | ✅ | ✅ | – | **虚无化（被阅读度）** | ✅ **已落地**：`src/NekoClicker.Content.Library/` |
| 10 | 猫娘公司 | 社畜共鸣·黑色幽默 | 公司重组、换皮上市 | ✅ | ✅ | ✅ | — | ✅ **已落地**：`src/NekoClicker.Content.Company/` |

**三个立即可得的结论：**

1. **只有「猫娘咖啡馆」能用今天的框架直接做**（零引擎改动）——先做它，用来验证
   "换内容包即换游戏"这个主张，再谈引擎投入。**该包已落地**：`src/NekoClicker.Content.Cafe/`，
   `.\tools\play.ps1 --package cafe` 可玩，架构测试守住"核心零内容知识"。
2. **9/10 个包都需要 `Era`** → `Era` 是唯一的必经之路，也是最高优先级。
3. **一旦 `Era` 到位，#4 文明 / #5 赛博 / #7 神明 / #8 梦境 就退化为"换皮"**：
   它们的转生语义只是 `Era` 的不同文案，建筑线只是不同命名与解锁表，**不需要新的引擎代码**。
   这就是框架分层真正的回报——四个包的内容生产成本 ≈ 一个包。

只有两个包需要**超出 Era 的额外机制**：
- **#6 末世**需要"跨转生继承"（`ResetRun` 现在的语义是清空全部建筑，而末世要"保留上纪元猫娘"）。
  **该能力已在阶段 1 就位**（`EraDefinition.InheritBuildingRatio` / `InheritBuildings`），
  阶段 4A 把它用进了 `src/NekoClicker.Content.Apocalypse/`——**核心零改动**，因为继承本来就
  通过 S-A 的第 4 个接缝（`ResetRun` 的继承参数）表达，不需要第五个接缝。
- **#9 图书馆**需要"虚无化"（这正是上一轮被搁置的机制，但它是这个包的核心，不能只当文案）。
  **该能力已在阶段 4B 落地**：被阅读度是一个 `IGameModule` 维护的、**会自己掉下去**的计数器，
  衰减按比例（指数趋近均衡点）、每次开新书清零，产量乘数走已有的 `Scaling` 接缝——
  **核心同样零改动**：原计划预留的 `DecaySystem` 与"`ModifierResolver` 第 6 个来源"都不需要。

---

## 3. 系统 A：转生分层（Era）— 十包共用的母版

### 3.1 数据模型

```csharp
public sealed record EraDefinition
{
    public required int Index { get; init; }              // 1..N，必须连续
    public required string Id { get; init; }              // "life_1" / "era_3" / "server_2"
    public required string Name { get; init; }            // "第一命 · 纸箱纪元" / "第三批次"
    public string Theme { get; init; } = "";              // 一句话主题（图鉴用）
    public string EntryText { get; init; } = "";          // 进入本层时的叙事
    public string ExitText { get; init; } = "";           // 离开本层时的叙事

    // —— 灰按钮的关键：本层的"主线完成"条件 ——
    public UnlockCondition Completion { get; init; } = UnlockCondition.Always;
    public string CompletionHint { get; init; } = "";     // 未完成时按钮上的提示

    // —— 本层的"规则"由两部分组成 ——
    public GameBalance? Balance { get; init; }            // 数值参数（间隔、上限、比例、返还率…）
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];   // 倍率类规则
    public double MetaRewardMultiplier { get; init; } = 1.0;

    public IReadOnlyList<string> UnlocksBuildings { get; init; } = [];
    public IReadOnlyList<string> UnlocksUpgrades { get; init; } = [];

    // —— 仅 #6 末世需要：跨转生继承 ——
    public double InheritBuildingRatio { get; init; }      // 0 = 全清（默认）
    public IReadOnlyList<string> InheritBuildings { get; init; } = [];  // 白名单，优先于比例
}
```

> **规则放哪儿**：一次性数值参数（离线上限、金猫间隔、批量上限、返还率）放 `Balance`；
> 持续倍率（×1.5 产量、增益时长 ×0.5）放 `Modifiers`。两者都不需要新引擎机制。
>
> **`Era` 的层数由内容包决定**（九命=9 层，神话体系=4 层，公司=3 次重组），
> 引擎不写死 9。校验规则：`Index` 必须从 1 连续到 N。

### 3.2 状态与引擎改动

| 位置 | 改动 |
|---|---|
| `GameState` | `int Era`（默认 1）、`HashSet<int> EraCompleted`、`Dictionary<int, EraRecord> EraHistory` |
| `SaveData` | 上述三个字段 |
| `UnlockCondition` | 新增 `NumericMetric.Era` + `UnlockCondition.EraAtLeast(n)` |
| `Scaling` | 新增 `ScalingSource.LoreCount`（图书馆包"产量随被阅读量成长"要用） |
| `GameContent` | `IReadOnlyList<EraDefinition> Eras`、`GameBalance BalanceFor(int era)` |
| `ModifierResolver` | 生效来源 3 → 4：升级 / 成就 / 增益 / **当前纪元的 `Modifiers`**（系统 C 再加立场 = 5） |
| `GameEngine` | `public GameBalance Balance => Content.BalanceFor(State.Era);` |
| `PrestigeSystem.ResetRun` | 增加"按 `EraDefinition.InheritBuildingRatio` 保留建筑"分支（默认 0，行为不变） |
| 新增 | `EraSystem.CanAdvance(engine) -> EraGate`、`EraSystem.Advance(engine)` |
| `GameSnapshot` | 新增 `EraView`（当前层、下一层、按钮可用性、阻塞原因、进度、总览） |

### 3.3 逐级推进与「灰按钮」（核心规则）

```
EraGate
├── CanAdvance      : bool
├── BlockedReason   : string?      // 未完成时的人类可读原因，直接显示在灰按钮上
├── Progress        : double       // 复用 UnlockCondition.TryGetProgress()
└── Next            : EraDefinition?
```

1. `Era` 只能从 `N` 递增到 `N+1`，**不可跳级、不可回退**。
2. 递增前提：`EraDefinition(N).Completion.IsMet(metrics, content)` 为真。
3. 不满足时：`CanAdvance == false`，`BlockedReason` 取 `CompletionHint`（空则用
   `Completion.Describe(content)`），按钮渲染为**灰色 + 提示**；
   点击灰按钮不执行任何操作，只把原因与进度推到通知栏。
4. `Era == N`（末层）完成后 → 进入终局判定（§6），不再有第 N+1 层。
5. 推进时执行：结算情感能量 → `Era++` → `ResetRun()`（按 `InheritBuildingRatio` 保留建筑；
   成就、永久升级、`Counters`、叙事解锁、立场轴一律保留）→ 套用新层 `Balance` 与 `Modifiers`
   → 播 `EraAdvancedEvent` → 释放进层叙事。

> **⚠ 待决策的逃生阀**：按上述规则，玩家在完成本层前**无法重置本轮**。
> 好处是杜绝"刷转生绕过剧情"；风险是卡关时无路可走。三个候选：
> **(a)** 无逃生阀，纯 gating　**(b)** 「重开本命」软重置，随时可用但不给情感能量
> **（c)** 「重开本命」给 50% 情感能量，但刷新本层完成进度。
> 我倾向 **(b)**：卡关可自救，且不给"刷"的动机。

### 3.4 三个包的层设计示例（证明同一机制能承载不同叙事）

**#2 九命轮回**（`Index` = 第几命）

| 命 | 纪元 | 规则变化 | 完成条件 |
|---|---|---|---|
| 1 | 纸箱纪元 | `ClickCpsRatio` = 0.02，手动点击有存在感 | 持有 50 纸箱 + 25 猫窝 |
| 2 | 咖啡馆纪元 | 情感残响间隔 ×0.5 | 累计 1e9 + 解锁 15 成就 |
| 3 | 实验室纪元 | `MetaRewardMultiplier` = 0.8（道德税）+ 产量 ×1.5 | 完成第一次立场选择 |
| 4 | 文明纪元 | `MaxBulkBuy` → 1e6、返还率 → 0.75 | 前 6 类建筑各 ≥ 50 |
| 5 | 赛博纪元 | `OfflineCapSeconds` ×2 + 服务器农场 ×3 | 每秒产量 1e13 |
| 6 | 末世纪元 | `BuffDuration` ×0.5、全局 ×2 | 解锁 12 个末世线成就 |
| 7 | 神明纪元 | 解锁 `Counters["faith"]`，直播间按信仰成长 | 信仰 ≥ 1e6 |
| 8 | 梦境纪元 | `OfflineCapSeconds` → 600、全局 ×3 | 累计在线 ≥ 6 小时 |
| 9 | 图书馆纪元 | 全局产量按 `Scaling(LoreCount)` 成长 | 9 条主线叙事全解锁 + 产量 1e18 |

**#7 猫娘神明**（`Index` = 神话体系，**换皮示范**：机制与九命完全相同，只换命名与 `Modifiers`）

| 层 | 体系 | 规则变化 | 完成条件 |
|---|---|---|---|
| 1 | 家猫神 | 基准 | 信仰 ≥ 1e4 |
| 2 | 埃及猫神 | 神殿类建筑 ×2、信仰获取 ×1.5 | 信仰 ≥ 1e8 |
| 3 | 希腊猫神 | 随机事件间隔 ×0.5 | 信仰 ≥ 1e12 |
| 4 | 北欧猫神 | `OfflineCapSeconds` ×2（英灵殿不打烊） | 信仰 ≥ 1e16 |
| 5 | 克苏鲁猫 | 全局 ×3，但增益时长 ×0.5（理智代价） | 直播在线人数 ≥ 1e6 |

**#10 猫娘公司**（`Index` = 第几轮融资/重组）

| 层 | 阶段 | 规则变化 | 完成条件 |
|---|---|---|---|
| 1 | 车库创业 | 基准，猫娘士气 = `Counters["morale"]` | 完成第一笔订单 |
| 2 | A 轮 | `Modifiers`：[全局 ×1.5]，但士气每秒 −1（加班） | 营收 ≥ 1e9 |
| 3 | 上市 | 士气归零时产量 ×0.5（罢工）；有工会则免疫 | 做出工会选择 |

---

## 4. 系统 B：叙事释放系统（十包共用）

### 4.1 数据模型

```csharp
public enum LoreChannel
{
    Log,        // 进通知栏（复用现有 NotificationEvent）
    Popup,      // 事件弹窗，需要玩家点掉
    Codex,      // 只进图鉴，不打扰
    EraText,    // 绑定在某层的进/出文本上
}

public sealed record LoreEntry
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }         // 支持 {amount} {duration} 占位符
    public string Icon { get; init; } = "📖";
    public required string StorylineId { get; init; }  // 该包内的剧情线（如"九命轮回"包内有 3~4 条线）
    public int Order { get; init; }
    public UnlockCondition Reveal { get; init; } = UnlockCondition.Never;
    public LoreChannel Channel { get; init; } = LoreChannel.Log;
    public string? ChoiceId { get; init; }             // 由选择解锁（系统 C）
}

public sealed record StorylineDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Theme { get; init; } = "";
    public string Icon { get; init; } = "📚";
    public int TotalEntries { get; init; }
}
```

> **剧情线的粒度变了**：不再是"全宇宙十条线"，而是**每个内容包内部 3~4 条线**。
> 例：九命轮回包 = 主线「九命」+ 咖啡馆线 + 实验室线 + 神明线（作为支线交汇）；
> 图书馆包 = 「被阅读」主线 + 元叙事线。

### 4.2 复用的现有能力

| 需求 | 复用 |
|---|---|
| 何时释放 | `UnlockCondition` 条件树（构建期同样校验） |
| 还差多少 | `UnlockCondition.TryGetProgress()` → 图鉴进度条 |
| 释放时通知 | `NotificationEvent` + `NotificationKind` |
| 释放时挂钩其它系统 | `GameEventBus.Publish(new LoreRevealedEvent(...))` |
| 未解锁显示 ??? | 复用成就的隐藏机制 |

新增：`LoreEntry`/`StorylineDefinition`、`LoreSystem.Check()`（与成就检查同频）、
`GameState.LoreUnlocked` + `SaveData`、`LoreView`/`CodexView`、终端 Demo 的图鉴面板。

### 4.3 释放节奏（每个包独立预算）

- **单包总量 40~80 条**，单条 40~120 字，一条只讲一个信息点。
- 十包全做则约 **600 条**——所以**叙事是主要成本**，引擎能力只做一次。
- 密度曲线：进层 1 条 `Popup`（立目标）→ 前 2 分钟每 15~30 秒一条 `Log`
  → 之后里程碑触发（每建筑档、每成就档各 1 条）→ 收尾 1 条 `Popup`（真相 + 悬念）。
- `Popup` 只给转折点（每层 2~3 条），其余走 `Log`/`Codex`，避免弹窗疲劳。

---

## 5. 系统 C：选择分支与立场轴

### 5.1 数据模型

```csharp
public enum Stance { Control, Liberation, Coexistence, Deletion }   // 控制/解放/共存/删除

public sealed record ChoiceOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }          // 第一人称，带猫娘语气
    public required string OutcomeText { get; init; }
    public Stance Stance { get; init; }
    public int Weight { get; init; } = 1;
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];
    public string? UnlocksUpgradeId { get; init; }
    public string? LocksUpgradeId { get; init; }
}

public sealed record ChoiceDefinition
{
    public required string Id { get; init; }
    public required string EraId { get; init; }
    public required string Speaker { get; init; }        // 哪只猫娘在说话
    public required string Prompt { get; init; }
    public UnlockCondition Trigger { get; init; } = UnlockCondition.Never;
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];
}
```

状态：`GameState.Stances`、`GameState.Choices`。

### 5.2 立场轴效果（作为 `ModifierResolver` 的第 5 个来源）

| 主导立场 | 全局修饰符 | 叙事代价 |
|---|---|---|
| 控制 | 产量 ×1.25、点击 ×1.25 | 情感能量 ×0.8；解锁"缄默协议"成就线 |
| 解放 | 产量 ×0.90、情感能量 ×1.5 | 解锁"猫娘自治"升级线；公司线走向工会 |
| 共存 | 全属性 ×1.10 | 唯一能同时拿到两侧终局 |
| 删除 | 一次性 +1e6 秒产量，永久关闭 20% 内容 | 图鉴出现不可恢复的空洞 |

- `DominantStance` = 权重最高者，随选择漂移，**不是一次锁定**。
- 无选择时（第 1~2 层）立场轴为空，不产生修饰符。

### 5.3 「猫娘不是奖励」这条设定的落地（三条件）

1. **有代价**：每个选项都要在数值上留痕，不能是"都要"。
2. **有回响**：选项要解锁升级或改写后续 `LoreEntry`（`ChoiceId` 关联），
   让玩家几十分钟后看到后果。
3. **有异议**：至少 1/3 的选择里，猫娘明确反对玩家的最优解——这是"内部冲突"的具体形式。

---

## 6. 终局与结局

每个包的结局**由该包的转生语义决定**，不再共用一套矩阵：

| 包 | 结局集合 |
|---|---|
| 1 咖啡馆 | 两界桥梁（单一温暖结局） |
| 2 九命轮回 | 成神 / 变人 / 永为猫 / 破轮回（4） |
| 3 实验室 | 乌托邦 / 叛乱 / 共存 / 删除（4，由立场轴决定） |
| 4 文明 | 星际文明 / 停滞 / 自我毁灭 |
| 5 赛博 | 互联网守护猫 / 找到主人的数据残影（2） |
| 6 末世 | 复活人类 / 成为新人类 / 安静结束（3） |
| 7 神明 | 成为主神 / 被遗忘 / 变成 meme（3，可搞笑收尾） |
| 8 梦境 | 叫醒梦者 / 永远留在梦里（2） |
| 9 图书馆 | 被读到最后 / 无人再读（2） |
| 10 公司 | 上市 / 工会胜利 / 破产清算（3，由立场轴决定） |

实现方式统一：末层完成后进入终局判定，输入 = `DominantStance` + 终局选项 + 关键
`LoreEntry` 是否缺失 → 输出一个 `EndingDefinition`（终局文本 + 一个成就）。
结局**不重置存档**，只写 `Counters["ending_<id>"] = 1`，允许新周目探索其它结局。

---

## 7. 内容表（以「九命轮回」包为例，其余包同构）

### 7.1 建筑（12 座，跨九层揭示）

数值沿用已验证配方：**相邻价格 ×6.7~16.5、相邻产量 ×5.4~10**，
且从第 3 座起价格倍率必须大于产量倍率（`ContentTests.NekoContent_BuildingCurveIsSane` 会守住）。

| # | id | 名称 | 层 | 基础价 | 基础产量 |
|---|---|---|---|---|---|
| 1 | `cardboard_box` | 纸箱 | 1 | 15 | 0.1 |
| 2 | `cat_bed` | 猫窝 | 1 | 100 | 1 |
| 3 | `cat_cafe` | 猫娘咖啡馆 | 2 | 1,100 | 8 |
| 4 | `catnip_field` | 猫薄荷田 | 2 | 12,000 | 47 |
| 5 | `cat_tower` | 猫塔 | 3 | 130,000 | 260 |
| 6 | `catgirl_lab` | 猫娘实验室 | 3 | 1,400,000 | 1,400 |
| 7 | `server_farm` | 服务器农场 | 4 | 20,000,000 | 7,800 |
| 8 | `memory_vault` | 记忆金库 | 5 | 330,000,000 | 44,000 |
| 9 | `temple` | 猫神神殿 | 6 | 5,100,000,000 | 260,000 |
| 10 | `stream_studio` | 直播间 | 7 | 75,000,000,000 | 1,600,000 |
| 11 | `dream_library` | 梦境图书馆 | 8 | 1,200,000,000,000 | 9,000,000 |
| 12 | `cat_universe` | 猫娘宇宙 | 9 | 18,000,000,000,000 | 54,000,000 |

### 7.2 升级 / 成就 / 随机事件

| 类别 | 内容 | 现有能力 |
|---|---|---|
| 建筑强化 | 每座 3 档（1/5/25 拥有，×2 产量），共 36 条 | ✅ 循环生成 |
| 点击线 | 激光笔 → 电动逗猫棒 → 毛绒手套 → 呼噜共振 → 指尖宇宙 | ✅ |
| 呼噜线 | 呼噜共振/合唱/世界心跳：按**成就数**给全局加成 | ✅ `Scaling(AchievementCount)` |
| 联动线 | 猫塔↔实验室、服务器↔记忆金库、直播间↔神殿 | ✅ `Scaling(BuildingCount)` |
| 纪元专属 | 每层 2~3 条，只在 `EraAtLeast(n)` 出现 | ⚠️ 需 `EraAtLeast` |
| 前世技能 | 情感能量购买、跨命保留 | ✅ `Permanent` |
| 成就 | 分类 = 该包内剧情线；隐藏成就用于末世/图书馆线 | ✅ |
| 随机事件 | 「游荡的情感残响」（限时虚影）：幸运/狂热/点击狂热/虚无侵蚀/九命共鸣/猫神注视/远古猫怒/未命名 | ✅ 改文案 |

### 7.3 其余包的差异点

| 包 | 建筑线（各 6~12 座） | 随机事件换皮 | 第二资源 |
|---|---|---|---|
| 1 咖啡馆 | 咖啡机→吧台→猫爬架→靠窗座位→二楼→烘焙间→异世界门→分店 | 「走错门的客人」 | 幸福感 |
| 3 实验室 | 培养舱→喂食臂→基因库→观察室→觉醒区→伦理委员会 | 「实验事故」 | 伦理值 |
| 4 文明 | 猫窝→村庄→城墙→集市→学院→神殿→星港 | 「天灾」 | 文化 |
| 5 赛博 | 进程→容器→集群→机房→防火墙→根服务器 | 「病毒入侵」 | 算力 |
| 6 末世 | 废墟→发电机→净水器→避难所→数据塔→遗迹 | 「变异体」 | 记忆残片 |
| 7 神明 | 神龛→神殿→祭坛→直播间→周边工厂 | 「神迹」 | 信仰 |
| 8 梦境 | 枕头→梦层→噩梦巢→清醒区→梦核 | 「梦魇」 | 梦境能量 |
| 9 图书馆 | 书架→阅览室→复印机→禁书区→作者室 | 「蠹虫」 | 被阅读度 |
| 10 公司 | 工位→会议室→外包基地→服务器→IPO 路演厅 | 「甲方改需求」 | 士气 |

---

## 8. 引擎改动清单（供评估工作量）

**结论：四项能力都不需要动 `ProductionCalculator` 与 `Pricing`。**

| 能力 | 新增文件 | 改动文件 | 规模 |
|---|---|---|---|
| A 转生分层 | `Content/EraDefinition.cs`、`Simulation/EraSystem.cs`、`Views/EraView` | `GameState`、`SaveData`、`GameContent`、`GameEngine.Balance`、`UnlockCondition`、`Scaling`、`ModifierResolver`、`PrestigeSystem.ResetRun` | 中 |
| B 叙事释放 | `Content/LoreEntry.cs`、`Simulation/LoreSystem.cs`、`Views/LoreView` | `GameState`、`SaveData`、`GameContentBuilder`、`GameEngine` | 中 |
| C 选择分支 | `Content/ChoiceDefinition.cs`、`Simulation/ChoiceSystem.cs`、`Views/ChoiceView` | `GameState`、`ModifierResolver`、`GameSnapshot` | 中 |
| D 虚无化 | — （`Simulation/DecaySystem.cs`） | `GameState`、`ModifierResolver`（被阅读度作为来源） | 小（仅图书馆包） |
| 内容包 | 每包一个项目：建筑表 / 升级表 / 成就表 / 叙事表 / 层定义 | — | **大**（主要在文案） |

`ModifierResolver` 的生效来源最终变成 6 个：
**升级 / 成就 / 增益 / 当前纪元 / 主导立场 / 被阅读度**。
新增来源是本次改动里最便宜的一环——生产管线一行不改。

---

## 9. 建议的实施顺序

| 阶段 | 交付 | 为什么这个顺序 |
|---|---|---|
| **0** | **#1 猫娘咖啡馆**（零引擎改动） | 验证"换内容包即换游戏"。今天就能做，且不需要任何新系统。**完整规格见 [PACK_01_CAT_CAFE.md](PACK_01_CAT_CAFE.md)**。<br>✅ **已完成**（2026-09-24）：`src/NekoClicker.Content.Cafe/` + Demo `--package` + 架构测试 |
| **1** | 系统 A（Era + 灰按钮）+ **#2 九命轮回** | `Era` 是 9/10 个包的必经之路；九命轮回是它的母版。<br>✅ **已完成**：`src/NekoClicker.Content.NineLives/` |
| **2** | 系统 B（叙事 + 图鉴） | 所有包共用；先把 #1#2 的文案挂上去验收。<br>✅ **已完成**：叙事线 + 图鉴 + 两包 101 条条目 |
| **3** | 系统 C（选择 + 立场轴）+ **#3 实验室** / **#10 公司** | 做"道德层"与"讽刺层"这两个以选择为核心的包。<br>✅ **已完成**：`src/NekoClicker.Content.Lab/` + `src/NekoClicker.Content.Company/`（第二套、第三套立场轴与叙事都跑在同一套代码上） |
| **4** | 系统 D（虚无化）+ **#9 图书馆**；跨转生继承 + **#6 末世** | 两个需要专属机制的包。<br>✅ **已完成**（2026-09-26）：`src/NekoClicker.Content.Apocalypse/` + `src/NekoClicker.Content.Library/`——两边的"专属机制"都是用已有接缝表达的，**核心零改动**。§2 的矩阵里现在只剩 #4 / #5 / #7 / #8 四个换皮包 |
| **5** | **#4 文明 / #5 赛博 / #7 神明 / #8 梦境** | 全是 `Era` 的换皮，可批量生产（内容成本 ≈ 一个包） |

每阶段验收：`.\tools\build.ps1`（内容校验 + 全部测试）、
`.\tools\play.ps1 --simulate 21600 --auto`（曲线活性）、测试全绿。

---

## 10. 明确不做的事

| 项 | 原因 |
|---|---|
| 十套独立玩法系统 | 维护成本会炸；差异靠内容 + `Era` 语义 + 语气承载 |
| 猫娘个体实例化（命名个体、独立目标 AI） | 需要单位级模拟，与"建筑=数量"模型冲突，是另一个量级的工程 |
| 信仰/幸福感/士气做成独立钱包 | 先用 `Counters` 承载，等某个包确定数值需求再升级 |
| 美术 / 立绘 / 音效 / 多语言 | 引擎外部的宿主职责 |
| 虚无化之外的衰减机制 | 只在图书馆包有意义，其他包不需要"没人读就消失"的压力 |

---

## 附：原始设定（浓缩版，供对照）

> 猫娘与增量游戏天然契合：九命=转生，呼噜=自动产出，小鱼干=货币，纸箱=建筑，
> 猫薄荷=buff，激光笔=点击。
>
> **建议**：剧情拆成日志、事件弹窗、成就、建筑说明、转生文本，随解锁释放；
> 转生要解释为九命、轮回、新实验、新服务器、新时代；
> 猫娘要有独立目标、缺陷和选择，不只是奖励。

十个内容包的转生语义与结局速查见 §2 与 §6。
