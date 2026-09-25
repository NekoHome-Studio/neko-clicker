# 路线图与实施规划

> **这份规划要回答一个问题**：从今天的框架出发，用最少的引擎改动、可验收的步骤，
> 把《九命猫娘》的十个内容包做出来。
>
> 它同时是一份**决策记录**：所有待决策项都在第 3 节给出了结论与理由，
> 并标注了是否可逆。读完之后，实现阶段不应该再剩下任何设计决策。
>
> 关联：[NINE_LIVES_DESIGN.md](NINE_LIVES_DESIGN.md)（世界观映射）、
> [PACK_01_CAT_CAFE.md](PACK_01_CAT_CAFE.md)（第一个包的完整规格）、
> [ARCHITECTURE.md](ARCHITECTURE.md)（引擎结构）

---

## 1. 目标与非目标

### 可验收的目标

| # | 目标 | 验收方式（可证伪） |
|---|---|---|
| G1 | 十个内容包共用**同一个核心**，核心零内容知识 | 架构测试：核心不引用任何内容项目、且不含任何内容 id 字面量 |
| G2 | #1 咖啡馆包可独立发布、可玩 | 试玩 + 6 小时无头模拟曲线报告 |
| G3 | 转生分层（`Era`）能承载至少三种完全不同的叙事 | #2 九命 / #7 神明 / #10 公司 三个包的层定义共用同一套代码 |
| G4 | 玩家从第 1 层到最后一层**全程可达**，不存在卡死 | 机器人测试：脚本化自动游玩必须走到末层并触发终局 |
| G5 | 剧情随解锁释放，不一次讲完 | 图鉴覆盖率测试 + 前 10 分钟只释放 ≤3 条 |
| G6 | 每次改动后既有测试全绿（当前 161 个） | `.\tools\build.ps1` 退出码 0 |

### 非目标（明确不做，避免范围蔓延）

- 十套独立玩法系统。差异由内容 + `Era` 语义 + 语气承载。
- 美术 / 立绘 / 音效 / 多语言 / 云存档 / 排行榜。
- 猫娘个体实例化（命名个体、独立 AI）。与"建筑=数量"模型冲突，是另一个量级的工程。
- 改造 `ProductionCalculator` 与 `Pricing`。本规划的所有能力都不应触碰它们。

---

## 2. 现状盘点（已核实，不是估计）

以下每一条都在当前代码里确认过（含验证方式），后续规划建立在这些事实之上：

> **状态说明**：本表写于规划阶段。标注「**已补齐**」的行是随后一轮"完善框架"
> 落地掉的改动（内容校验补漏 + 可达性分析 + 条件指标对称性 + 模块离线钩子），
> 详见 §6.1 与 git 历史。

| 事实 | 验证方式 | 对规划的意义 |
|---|---|---|
| 核心 0 个第三方依赖，测试全绿（规划时 126 个；阶段 0 交付后 161 个） | `.\tools\build.ps1` | 后续改动有回归网 |
| `ScalingSource.PurchasedUpgrades` / `CustomCounter` 存在 | grep `Modifier.cs:49,61` | 第二资源可驱动修饰符，无需新机制 |
| `IGameMetrics.GetCounter` / `GameState.AddCounter` 存在 | grep `IGameMetrics.cs:75`、`GameState.cs:112` | 第二资源可读写 |
| `GameContent.Modules` 会被引擎自动挂载 | 测试 `Modules_ReceiveConfigureAttachAndTick` | 第二资源可用 `IGameModule` 实现，零核心改动 |
| `NumericMetric` 原有 13 个指标，缺 `Counter` 与 `TaggedUpgrades` | 读 `UnlockCondition.cs` 的枚举 | **已补齐**（§6.1）；"计数器 ≥ N"的解锁条件因此自带进度条 |
| `AllCondition.TryGetProgress` 返回**最落后**的子条件 | 读 `UnlockCondition.cs` 的 `AllCondition` 实现 | 灰按钮能自动显示"卡在哪一项"，不需要额外设计 |
| `Permanent` 升级必须用转生货币计价，否则构建期报错 | 读 `GameContentBuilder.cs:195-196`（在 `Build()` 的升级校验里，不在 `ValidateModifiers`） | "常客的记忆只能用信换"是硬约束，不是文案 |
| 转生不清空 `Counters` | 读 `PrestigeSystem.ResetRun` | 第二资源天然跨转生保留 |
| MSBuild 在受限环境需 `-m:1`；无 NuGet 网络 | `tools/dnet.ps1` 的由来 | 不能引入任何新 NuGet 包（含测试框架） |

---

## 3. 决策清单（每项都已定，标注可逆性）

### R1 —— 转生与纪元推进**是同一次操作**（可逆：低）

只保留一个按钮：**「舍一命」**（各包换名：店休 / 迁服务器 / 公司重组）。
它同时完成三件事：结算情感能量 → `Era++` → 重置本轮。

**理由**：如果保留两个按钮（"重置变强" + "推进故事"），玩家就能在同一层无限刷转生攒情感能量，
再从第 1 层一路平推到第 9 层，彻底废掉分层设计。合并成一个受门控的动作，
"不能刷"这个性质就是**结构性保证**，而不是靠数值调参。

### R2 —— **不做逃生阀**（可逆：低）

**理由**：我原本以为需要"重开本命"来防卡死，但把九层的完成条件逐条推了一遍之后发现——
只要**完成条件强制单调**（见 R3），玩家就**不可能**卡死：所有条件只会随时间自动推进，
不存在"资源被浪费后无法达成"的状态。

所以逃生阀解决的是一个不存在的问题。**这是本轮最重要的简化**：
少一个按钮、少一个状态、少一套 UI 分支，而且卡死风险由 R3 的校验从根上消除。

### R3 —— 完成条件**必须单调**（不可逆：这是架构不变量）

`EraDefinition.Completion` 只允许引用**单调不减**的指标：

| 允许 ✅ | 禁止 ❌ | 为什么禁止 |
|---|---|---|
| `CookiesEarnedThisRun` / `CookiesEarnedAllTime` | `CurrentCookies` | 会被花掉，花完就倒退 |
| `AchievementCount` / `PurchasedUpgrades` | `BuildingCount` | 允许出售，卖完就倒退 |
| `TotalClicks` / `GoldenCookiesClicked` / `PlayTimeSeconds` | `Cps` | 增益到期会掉下来，灰按钮会闪 |
| `PrestigeLevel` / `Era` / `LoreCount` | — | — |
| `Counter`（新增，如 `peak_cps`） | — | — |

**强制手段**（三层，缺一不可）：
1. **构建期校验**：`GameContentBuilder` 扫描 `Completion` 的条件树，出现禁用指标即报错。
2. **单元测试**：对示例内容包断言所有 `Completion` 只含白名单指标。
3. **文档约束**：本表即内容作者规范。

### R4 —— 情感能量公式**全局恒定**，`Era` 不参与货币计算（可逆：中）

删掉原设计里"每层 `PrestigeDivisor` 递增"的想法。

**理由**：那个设计的目的是"防止在一层刷穿九层"。但 R1 已经用**结构性方式**杜绝了刷——
不能推进纪元就不能重置，根本没有刷的机会。所以递增除数是在解决一个已被解决的问题，
只会增加一个没人能调准的旋钮。**少一个参数，少一类 bug。**

### R5 —— 推进纪元**允许给 0 情感能量**（可逆：低）

`chips = max(0, LevelFor(历史累计) - 当前等级) × MetaRewardMultiplier`。
早期层可能给 0（因为等级还没涨够），但**故事照样推进**。

**理由**：故事推进绝不能以数值增长为条件——否则"我打不过去"会变成"我看不到剧情"，
而剧情才是这个项目的核心资产。

### R6 —— 选择**不阻塞游戏**（可逆：低）

待决策的选择以常驻横幅 + 快捷键呈现，游戏继续跑。不做模态弹窗。

**理由**：挂机游戏弹模态框打断放置，是最招人烦的设计之一。但选择必须出现在
`GameSnapshot.PendingChoices` 里，让任何前端都能渲染。

### R7 —— **每包一个 csproj**（可逆：中）

`src/NekoClicker.Content.Cafe/`、`...Content.NineLives/` …… Demo 引用全部包并提供 `--package`。

**理由**：一个包不可能"不小心依赖"另一个包——编译期就挡住。这是 G1 主张的工程化体现。
代价是解决方案里多几个项目（13 个项目，构建仍在秒级）。

### R8 —— 存档版本策略：**默认值安全则不 bump**（可逆：低）

新增字段若默认值对旧存档安全（如 `Era = 1`、`LoreUnlocked = 空`、`Stances = 空`），
**不升版本、不写迁移**——`System.Text.Json` 缺字段自动取默认值。
只有发生**语义变化**（需要改写已有数据）时才 `CurrentVersion++` 并写 `ISaveMigration`。

**理由**：无谓的版本号会掩盖真正的破坏性变更，让"这个版本需要迁移"这个信号失效。

### R9 —— `Cps` 非单调，改用 `PeakCps` 计数器（由 R3 派生）

"每秒产量达到 1e13"这类条件不能用 `Cps`：增益到期后它掉下来，灰按钮会闪烁。
改为新增 `Counters["peak_cps"]`，每 tick 取 `max(旧值, 当前Cps)`，条件用
`UnlockCondition.Counter("peak_cps", 1e13)`。

**这正好复用了 C1（`NumericMetric.Counter`）那 3 行改动** —— 一次改动服务两个需求，
是"把能力做成通用形式"而不是"为需求打补丁"的直接回报。

### R10 —— 阶段 0 的叙事**先走既有通道**（可逆：低）

**这是我之前的一个错误结论**：我说"#1 咖啡馆包零引擎改动"，但它的规格里含 50 条独立叙事条目——
那需要叙事系统（S-B）。所以严格说它**不是**零改动。

纠正后的做法：阶段 0 把叙事**塞进框架已有的文本通道**——
`BuildingDefinition.Description`、`AchievementDefinition.Description`、增益/事件名称与说明。
这些字段本来就是叙事通道，且框架已完整支持。50 条独立条目留到 S-B 到位后再补。

**这带来一个额外好处**：同一份内容有了"叙事系统上线前 / 上线后"的 A/B 对照，
可以真实评估 S-B 到底值不值。

### R11 —— 「猫咖物语」保留为**框架回归基线**（可逆：低）

现有 `NekoClicker.Content.Neko`（猫咖物语）**不属于**九命猫娘宇宙，它是引擎的技术演示，
且被大量测试依赖。新的 #1 咖啡馆是**独立项目**，命名与内容重新设计。

**理由**：两者主题相近（都是猫咖啡馆），必须在地理上、文档上、命名上划清界限，
否则半年后没人分得清哪个是"官方游戏"。

---

## 4. 架构不变量（违反即视为设计事故）

| # | 不变量 | 强制手段 |
|---|---|---|
| A1 | **核心不认识内容**：`NekoClicker.Core` 不引用任何 `Content.*` 项目，且源码中不出现任何内容 id 字面量 | 新增架构测试：读 `Core.csproj` 的 ProjectReference；反射扫描核心程序集的字符串字面量，与所有内容包的 id 集合求交集，必须为空 |
| A2 | **`Era` 只通过 4 个接缝进入引擎**，引擎内不出现 `if (era == ...)` | 代码评审 + 架构测试（禁止核心出现 `Era` 与具体层号的比较） |
| A3 | **能力必须是通用形式**：新增能力不得以"某个包的名字"命名 | 命名规范 + 评审 |
| A4 | **不动 `ProductionCalculator` / `Pricing`** | 每次改动的 diff 检查 |

`A2` 的 4 个接缝是：

```
1. GameContent.BalanceFor(era)        → 数值参数随层变化
2. ModifierResolver 的来源列表         → 倍率类规则随层变化
3. UnlockCondition 的指标（Era）        → 内容按层门控
4. PrestigeSystem.ResetRun 的继承参数   → 跨层保留什么
```

任何第五个"Era 影响引擎行为"的地方，都必须先证明它无法被上面四个表达。

---

## 5. 能力依赖图

```
[已存在] Core（建筑/升级/成就/增益/事件/存档/离线/视图）
   │
   ├── C1  NumericMetric.Counter（3 行）
   │        └─ 被 S-A（peak_cps 完成条件）与第二资源（幸福感/士气/信仰）共用
   │
   ├── S-A Era 转生分层 + 灰按钮
   │        ├─ C1（完成条件可用计数器）
   │        ├─ EraAtLeast 条件
   │        ├─ BalanceFor / ModifierResolver 来源
   │        └─ ResetRun 继承参数（仅 #6 末世需要非 0 值）
   │
   ├── S-B Lore 叙事释放 + 图鉴
   │        ├─ C1（释放条件可用计数器）
   │        ├─ 独立于 S-A（#1 咖啡馆包不需要 Era）
   │        └─ 但 #2~#10 的叙事门控要用 EraAtLeast → 与 S-A 有弱依赖
   │
   ├── S-C Choice 选择 + 立场轴
   │        ├─ ModifierResolver 来源（不依赖 S-A / S-B）
   │        └─ 选项改写叙事 → 与 S-B 有弱依赖（可后置）
   │
   └── S-D Decay 虚无化（仅 #9 图书馆）
            └─ 独立
```

**关键结论**：四项能力的依赖关系比看上去松。C1 是唯一的硬前置（3 行）。
S-A / S-B / S-C 之间只有"弱依赖"（内容上的引用，不是代码依赖），
所以**可以按内容需要单独交付**，不必串行等待。

---

## 6. 能力接口契约（到"无歧义"级）

### 6.1 C1 —— `NumericMetric.Counter` ✅ 已实现

```csharp
// UnlockCondition.cs
public enum NumericMetric
{
    // ... 既有成员不变 ...
    /// <summary>自定义计数器。</summary>
    Counter,          // ← 新增
}

// NumericCondition.Read 增加一行：
NumericMetric.Counter => metrics.GetCounter(Id ?? string.Empty),

// 工厂方法：
public static UnlockCondition Counter(string key, double target)
    => new NumericCondition(NumericMetric.Counter, target, key);

// Describe 增加一行：
NumericMetric.Counter => $"「{Id}」达到 {amount}",
```

- 构建期校验：`Id` 为空的 `Counter` 条件报错（与现有 `BuildingCount` 同款校验）。
- `TryGetProgress` 自动可用（复用 `NumericCondition` 的实现）→ **进度条免费获得**。

### 6.2 S-A —— Era

```csharp
public sealed record EraDefinition
{
    int Index;                       // 1..N，必须连续，缺层即校验失败
    string Id, Name, Theme;
    string EntryText, ExitText;
    UnlockCondition Completion;      // 必须单调（R3）
    string CompletionHint;
    GameBalance? Balance;            // null = 继承包的基准
    IReadOnlyList<Modifier> Modifiers;
    double MetaRewardMultiplier = 1.0;
    double InheritBuildingRatio;     // 0 = 全清；仅 #6 末世非 0
    IReadOnlyList<string> InheritBuildings;   // 白名单，优先于比例
    IReadOnlyList<string> UnlocksBuildings, UnlocksUpgrades;
}

public readonly record struct EraGate(
    bool CanAdvance, string? BlockedReason,
    double Progress, int CurrentIndex, int? NextIndex);

// GameState 新增：Era=1、EraCompleted、EraHistory
// 事件新增：EraAdvancedEvent(previous, next, chipsGained, blocksInherited)
// 视图新增：EraView（当前层/下一层/CanAdvance/BlockedReason/Progress + N 层总览）
// GameSnapshot 新增：EraView? Era（#1 咖啡馆包为 null，UI 自动隐藏该面板）
```

**引擎接缝**：`GameEngine.Balance` 改为 `Content.BalanceFor(State.Era)`；
`ModifierResolver.Build` 增加一个来源；`ResetRun` 增加继承分支。

### 6.3 S-B —— Lore

```csharp
public enum LoreChannel { Log, Popup, Codex, EraText }

public sealed record LoreEntry
{
    string Id, Title, Body;          // Body 支持 {amount}/{duration}
    string Icon; string StorylineId; int Order;
    UnlockCondition Reveal;
    LoreChannel Channel;
    string? ChoiceId;                // 由选择解锁
}

public sealed record StorylineDefinition
{
    string Id, Name, Theme, Icon; int TotalEntries;
}

// GameState 新增：LoreUnlocked（HashSet<string>）
// 事件新增：LoreRevealedEvent(id, title, channel, storylineId)
// 视图新增：LoreView / CodexView（按线分组 + 进度 + 未解锁显示 ???）
// 检查频率：与成就同频（复用 AchievementCheckInterval）
```

**校验规则**（构建期）：同一 `StorylineId` 内 `Order` 不得重复；
`TotalEntries` 必须等于实际条目数；`Reveal` 不得恒为 `Never`；不得引用不存在的 `ChoiceId`。

### 6.4 S-C —— Choice

```csharp
public enum Stance { Control, Liberation, Coexistence, Deletion }

public sealed record ChoiceOption
{
    string Id, Label, OutcomeText;
    Stance Stance; int Weight;
    IReadOnlyList<Modifier> Modifiers;
    string? UnlocksUpgradeId, LocksUpgradeId;
}

public sealed record ChoiceDefinition
{
    string Id, EraId, Speaker, Prompt;
    UnlockCondition Trigger;
    IReadOnlyList<ChoiceOption> Options;
}

// GameState 新增：Stances（Dictionary<Stance,int>）、Choices（Dictionary<string,string>）
// 事件新增：ChoiceOfferedEvent / ChoiceResolvedEvent
// 视图新增：PendingChoiceView；GameSnapshot.PendingChoices（R6 要求前端可见）
// ModifierResolver 新增来源：DominantStance 的修饰符
```

**校验规则**：每个 `ChoiceDefinition` 至少 2 个选项；同一选择的选项 `Id` 唯一；
`UnlocksUpgradeId` / `LocksUpgradeId` 必须存在；`Trigger` 不得恒为 `Never`。

### 6.5 S-D —— Decay（仅 #9 图书馆包，最后做）

```csharp
// 被阅读度 = Counters["readership"]
// 每 tick：readership += 活跃度衰减项；产量乘数 = f(readership)
// 作为 ModifierResolver 的第 6 个来源
```

**刻意不提前设计细节**：它的具体形式（线性衰减 / 阈值悬崖 / 有无数值下限）
应该由 #9 包的实际节奏反推，现在设计等于拍脑袋。**这是本规划里唯一被有意留白的接口。**

---

## 7. 阶段划分与验收

> 每阶段的共同验收：`.\tools\build.ps1` 全绿 + `.\tools\play.ps1 --simulate 21600 --auto`
> 曲线活着（无 NaN/∞、有事发生）。下表只列**该阶段特有的**验收项。

### 阶段 0 —— #1 猫娘咖啡馆（零引擎改动）✅ 已交付

| 项 | 内容 |
|---|---|
| 交付 | `NekoClicker.Content.Cafe` + Demo 的 `--package` 开关 + 幸福感模块 |
| 叙事 | 走既有文本通道（R10）：建筑/成就/增益/事件的描述字段 |
| 特有验收 | ① 幸福感模块的 4 条测试（累加、跨转生保留、`Permanent` 计价、事件权重）<br>② `--package cafe` 可玩，`--package neko` 仍可玩（回归） |
| 证伪点 | 如果这个包需要动核心，说明 G1 的主张是错的 → 停下来重新评估 |

**交付记录**（2026-09-24）：

| 验收项 | 结果 |
|---|---|
| 规模 | 10 建筑 / 48 升级 / 45 成就 / 5 增益 / 8 随机事件 / 1 幸福感模块 |
| 核心改动 | **0 行**（`NumericMetric.Counter` 属 C1，已在阶段 0 之前落地） |
| 架构测试（K1） | `ArchitectureTests`：核心不引用内容项目、核心程序集不含内容 id、内容包互不引用 |
| 专项测试 | `CafeContentTests` 14 条 + `SimulationTests.CafeSixHourGreedyRun`（PACK_01 §13 全部覆盖） |
| 回归 | `.\tools\build.ps1` 全绿：**161 个用例**（阶段 0 前 143 个） |
| 试玩 | `.\tools\play.ps1 --package cafe --simulate 21600 --auto` 曲线活着：10 座建筑全解锁、33/45 成就、52 位客人、幸福感 18 万 |
| 未覆盖 | §10 的 50 条叙事条目——按 R10 暂走描述字段，等阶段 2 的 S-B 到位再补 |

### 阶段 1 —— S-A（Era）+ #2 九命轮回 ✅ 已交付

| 项 | 内容 |
|---|---|
| 交付 | S-A 全部接口 + 9 层纪元 + 12 座建筑 + 48 条升级 + 63 个成就 + 10 种随机事件 + 信仰模块 |
| 核心改动 | `EraDefinition` / `EraGate` / `EraSystem`；`NumericMetric.Era`；`GameContent.Eras` + `BalanceFor`；`ModifierResolver` 第 4 个来源；`ResetRun` 继承参数；`GameState`/`SaveData`/`EraView`；构建期单调性校验 |
| 特有验收 | ① **单调性校验**：`Completion` 只含白名单指标，用 `CurrentCookies`/`Cps`/`BuildingCount` 会被构建期拒绝 ✅<br>② **灰按钮**：未完成时 `CanAdvance=false`、原因非空、进度取最落后子条件 ✅<br>③ **A2 架构测试**：核心程序集里不出现 `era ==` 之类的层号比较 ✅<br>④ **G4 全程可达**：机器人从第 1 命走到第 9 命 ✅ |
| 回归 | `.\tools\build.ps1` 全绿：**188 个用例**（阶段 0 后 161 个），全套 8.8 秒 |
| 实测 | `--package ninelines --simulate 172800 --auto`：48 游戏小时内走完九命，最终 214 万亿、1485 建筑、60/63 成就 |

**这一阶段真正抓到的三个问题**（都不是"写完就过"）：

1. **`Advance` 漏判 `CanAdvance`**：`EraSystem.Advance` 原本只检查 `NextIndex` 是否存在，
   而 `CanAdvance` 为假时 `NextIndex` 仍然是下一层的编号——于是**未完成本层也能舍命**，
   整个 gating 形同虚设。是 `Advance_FailsWhileGateIsClosed_AndChangesNothing` 抓到的。
2. **阈值阶梯远比"每层从零重建"快**：初版每层门槛 ×1000（1e5 → 1e8 → 1e11 …），
   G4 机器人实测每层耗时约 ×3 增长（0.8h → 2.2h → 5.3h → 14.8h → 43.4h），
   第 4 层之后就走不动了。压平到 ×1.5~2 并给缺少全局加成的层补上纪元加成后，
   九命总耗时降到 ~33 游戏小时。**这正是 G4 存在的意义**——它证明的不是"能跑"，
   而是"内容在真实曲线下真的走得完"。
3. **`InheritBuildings` 的语义要写死**：它是"**进入**这一层时允许带进来什么"（写在目标层上），
   不是"离开上一层时带走什么"。两种读法都说得通，不写清楚将来必然被写反。

**已知的内容调参项**（不影响机制，留给后续）：第 5 命的 `peak_cps` 门槛造成约 19 小时的
节奏凸起，与相邻各层的 2~4 小时不成比例。

### 阶段 2 —— S-B（Lore）+ #1 的 50 条条目 + #2 的叙事

| 特有验收 | ① 图鉴进度与 `TotalEntries` 一致<br>② 同一 `Order` 不重复<br>③ 前 10 分钟释放 ≤3 条（G5）<br>④ 存档往返保留 `LoreUnlocked` |
| 观察 | 用 #1 咖啡馆做 A/B：叙事系统上线前 vs 上线后，同一个包的可玩性差异 |

### 阶段 3 —— S-C（Choice）+ #3 实验室 / #10 公司

| 特有验收 | ① 立场权重累加与主导立场切换<br>② 每个选项的 `Modifiers` 都真的影响产量（数值断言）<br>③ 未选择的选项不生效（一次性语义）<br>④ 存在"猫娘反对玩家最优解"的选项（内容审查项） |

### 阶段 4 —— S-D（Decay）+ #9 图书馆；继承机制 + #6 末世

| 特有验收 | ① 继承比例生效且默认 0 不改变既有行为（回归）<br>② 虚无化的数值下限不会让产量归零到死锁 |

### 阶段 5 —— #4 文明 / #5 赛博 / #7 神明 / #8 梦境（换皮批产）

| 特有验收 | 四个包共用 S-A/S-B，**不得新增任何核心代码**（A3 的检验） |

---

## 8. 风险登记册

| # | 风险 | 影响 | 最便宜的验证手段 | 缓解 |
|---|---|---|---|---|
| K1 | 核心被内容需求侵蚀（A1/A2/A3 被破坏） | 高：框架主张失效 | **架构测试**（阶段 0 就加，成本约 30 行） | 三条不变量 + 评审 |
| K2 | 某层完成条件在实际曲线下不可达 → 卡死 | 高 | **机器人全程可达测试**（阶段 1 加） | R3 单调性 + 该测试 |
| K3 | 灰按钮的 `BlockedReason` 表述不清，玩家不知道要做什么 | 中 | 试玩；`AllCondition` 已返回最落后子条件，直接用 | 文案打磨 |
| K4 | 叙事量大（十包约 600 条）拖垮进度 | 高 | 先只做 #1 的 50 条，测写作速度 | 只承诺前 3 个包，其余按需 |
| K5 | `Era` 与 `PrestigeLevel` 双轴让玩家困惑 | 中 | 试玩；UI 上把"舍一命"做成唯一的主按钮 | R1（只有一个按钮）大幅降低该风险 |
| K6 | 无 NuGet 网络 → 不能引入测试/序列化库 | 低（已适应） | 现状已验证 | 继续用自带迷你运行器 |
| K7 | 选择系统的立场轴让平衡难以调准 | 中 | 把立场修饰符做成可单独断言的数值测试 | 立场倍率先取保守值（±10~25%） |

**优先级**：K1 和 K2 必须在阶段 0/1 就建立防线——它们的失败会让后面所有工作白做。

---

## 9. 工作量与优先级（相对量级，不编工时）

| 工作 | 规模 | 主要成本在哪 | 置信度 |
|---|---|---|---|
| C1 计数器指标 | XS（3 行 + 3 测试） | — | 高 |
| S-A Era | M | 数据模型 + 4 个接缝 + 视图 | 高 |
| S-B Lore | M | 模型 + 图鉴视图 | 高 |
| S-C Choice | M | 立场轴与修饰符的接合 | 中（平衡需实测） |
| S-D Decay | S | 数值形式需由 #9 反推 | 低（有意留白） |
| 每个内容包的**代码** | S | 表格转代码，可循环生成 | 高 |
| 每个内容包的**文案** | **L** | 50~80 条叙事 + 60~100 条描述 | 中（写作速度未知，见 K4） |
| 架构测试 | S | 反射扫字符串 | 高 |

**结论**：**代码不是瓶颈，文案才是。**
引擎侧总量约 4 个 M 级系统；内容侧每个包都是一次 L 级的写作投入，乘以十就不可承受。
所以规划只承诺 **阶段 0~3（#1 #2 #3 #10 四个包）**，其余按需追加。

---

## 10. 执行前提与诚实边界

**执行前提**（缺一不可）：

1. `.\tools\build.ps1` 在改动前后都全绿——任何阶段都不许"先红着，回头再修"。
2. 架构测试（K1）在阶段 0 建立，之后不许绕过。
3. 每个阶段结束只推一个可玩的包，不做"半成品堆在一起"。

**诚实边界**：

- 阶段 0（#1 猫娘咖啡馆 + 架构测试 + `--package`）已落地；其余九个内容包仍只是文档。
  阶段 1~5 的四项引擎能力（S-A `Era` / S-B `Lore` / S-C `Choice` / S-D `Decay`）**一行都还没写**。
- 阶段 4 的 S-D 接口**有意留白**，因为它的正确形式依赖 #9 包的实际节奏，现在设计等于猜。
- 阶段 5 的四个"换皮"包能批量做，前提是阶段 1~2 的抽象确实成立——
  这个前提要到阶段 1 结束才能验证。**在阶段 1 结束前，不要承诺阶段 5。**
- 工作量栏的"规模"是相对量级（XS/S/M/L），不是工时估算。文案写作速度没有基准数据（K4）。
