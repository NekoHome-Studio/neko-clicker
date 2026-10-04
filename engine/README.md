# NekoClicker Engine

**增量（放置 / 点击）游戏框架。纯 C#，目标框架 `net8.0`，零第三方依赖。**

这个目录是**自包含**的：把它整个搬走，配合仓库根的 `Directory.Build.props`，
就是一个能独立构建的引擎仓库。它不依赖 `games/` 里的任何东西，反向是依赖关系。

```powershell
dotnet build NekoClicker.sln     # 或者用仓库根的 .\tools\build.ps1
```

---

## 这个目录里有什么

| 路径 | 是什么 |
|---|---|
| `core/` | **引擎本体**。时间推进、数值管线、价格闭式解、解锁条件树、存档与迁移、事件总线、只读视图 |
| `content/Neko/` | 示例内容包「猫咖物语」——引擎的技术演示，同时是框架回归基线 |
| `content/{Cafe,NineLives,Lab,Company,Apocalypse,Library,God,Civ,Cyber,Dream}/` | 十个内容包，**每个都是独立的 csproj** |
| `tests/` | 564 个用例 + 自研迷你测试运行器（零依赖，不需要 xunit） |
| `docs/` | 架构、内容作者指南、版本承诺 |

**为什么内容包算引擎的一部分？** 因为它们是**引擎的集成测试探针**，不是某个作品的资产：

- `ArchitectureTests` 靠它们证明"核心不认识内容"——扫描核心程序集的字符串字面量，
  与所有内容包的 id 集合求交集，必须为空；
- `EraTests` / `LoreTests` / `PrestigeTests` 的**通用守卫**横扫每一个包才抓得出沉默失败
  （"某层叙事门槛高于本层完成门槛、结构上永远读不到"这类问题，运行期不报错、只在图鉴里表现为读不到）。

一个包不可能"不小心依赖"另一个包——每个包一个 csproj，编译期就挡住。

---

## 三条架构不变量（违反即视为设计事故）

| # | 不变量 | 强制手段 |
|---|---|---|
| **A1** | 核心不认识内容：`NekoClicker.Core` 不引用任何 `Content.*`，源码里不出现任何内容 id 字面量 | `ArchitectureTests` 反射扫描 |
| **A2** | `Era` 只通过 **4 个接缝**进入引擎，引擎内不出现 `if (era == ...)` | `ArchitectureTests` 禁止核心出现层号比较 |
| **A3** | 能力必须是通用形式：新增能力不得以"某个包的名字"命名 | 命名规范 + 评审 |
| **A4** | 不动 `ProductionCalculator` / `Pricing` | 每次改动的 diff 检查 |

`Era` 的那 4 个接缝是：`GameContent.BalanceFor(era)`、`ModifierResolver` 的来源列表、
`UnlockCondition` 的 `NumericMetric.Era`、`PrestigeSystem.ResetRun` 的继承参数。
任何第五个"Era 影响引擎行为"的地方，都必须先证明它无法被上面四个表达。

**这条纪律的成果**：十个内容包承载了十套完全不同的世界观（九层轮回 / 七批实验 / 三轮重组 /
五次重启 / 五本书 / 五套神话 / 五个时代 / 五层数字层 / 五层梦 / 一家咖啡馆），
其中**四个换皮包的核心改动是 0 行**。

---

## 扩展点

接一个新玩法**不应该**改引擎。已有的接缝：

| 想做的事 | 用哪个接缝 |
|---|---|
| 新增一个第二资源（会涨会花、会掉都行） | 实现 `IGameModule`：状态放 `GameState.Counters`，`OnTick` 推进、`OnOffline` 补算、`OnAscend` 决定是否清零 |
| 让计数器驱动产量 | `Modifier.GlobalPercent(0, new Scaling(ScalingSource.CustomCounter, perUnit, Cap: …, Id: "键"))`——**不需要新增来源**。注意 `Cap` 限的是**计数值**而不是加成结果 |
| 让计数器出现在玩家文案里 | `builder.AddCounterName(计数器键, "显示名")`，写在模块的 `Configure` 里；未登记会回退成内部键 |
| 新增一层纪元 | 加一条 `EraDefinition`（层号连续、完成条件**必须单调**），核心零改动 |
| 新增一条叙事线 | 加 `StorylineDefinition` + 若干 `LoreEntry`，核心零改动 |
| 新增一整套立场轴 | 加 `StanceDefinition`（是内容自定义的字符串 id，**刻意不是 enum**） |
| 换前端 | 只依赖 `GameSnapshot` 与引擎的公开命令方法 |
| 换存储介质 | 实现 `IStorage`（文件 / 内存 / 浏览器 localStorage / 云） |

`Stance` 不做成 enum 是刻意的：`enum Stance { Control, Liberation, ... }` 会把某个包的世界观
焊进核心，别的包要"道德""劳资"时还得改引擎。代价是失去编译期检查，用构建期校验补回来。

---

## 版本与兼容性

**当前 `1.10.1`。从 `1.0.0` 起，`NekoClicker.Core` 的公开 API 只增不改**，
由 `core/PublicApi.txt`（快照，**嵌进 dll**）加四条守卫强制执行。
规矩见 [docs/VERSIONING.md](docs/VERSIONING.md)。1.10.1 是 **patch：公开 API 一行没动**
（快照除首行版本号外逐字节不变）——那两件攒在 `[未发布]` 里的事随它发布：
把 1.10.0 的导出/导入接到**两个宿主**上（Web 的「存档」窗口、终端宿主的 `E` / `I` 路径提示），
以及十一个内容包各补的一条「点击 × 建筑」桥（内容侧）。方案见
[docs/SAVE_TRANSFER_PLAN.md](docs/SAVE_TRANSFER_PLAN.md) 与
[docs/OPEN_WORK.md](docs/OPEN_WORK.md) 的 §0.18。
上一个版本 1.10.0 是**纯新增**：
`SaveTransfer`（信封格式：格式标签 + 包 id + 存档格式版本 + 校验和 + 尺寸闸）+
`SaveTransferKind` + `SaveTransferResult` + `SaveManager.PackId` / `Export()` / `Import()`——
把"玩家自己复制粘贴一份存档"变成一条**坏输入绝不弄坏能用的存档**的路。
**存档格式一个字都没改**（`CurrentVersion` 仍是 `1`，没有迁移）：信封在存档**外面**。
再上一个版本 1.9.0 也是纯新增：
`EraStage` + `EraStageGate` + `EraDefinition.Stages` + `EraSystem.Stage` /
`CheckStage` / `StageCounterPrefix` + `GameEngine.CheckEraStage()` +
`EraView` 上阶段那六个只读字段——把"**一层之内的第几段**"变成内容可以声明、
构建期会校验、两个宿主都读得到的形状；阶段是**派生**的（不占存档位、不动存档格式），
没声明阶段的包行为一个字节不变。方案见
[docs/CONTENT_AUTHORING.md](docs/CONTENT_AUTHORING.md) §11.2。
再上一个版本 1.8.0 也是纯新增：
`UpgradeCategories` + `GameContent.UpgradesByBuilding` / `UpgradesForBuilding` +
`BuildingView.UpgradeIds`——把 `UpgradeDefinition.Category` 上那条 `"building:<id>"` 约定
（"这条升级属于哪座建筑"）从"没人校验、没人读"变成**构建期校验 + 索引 + 快照字段 + 界面**，
方案见 [docs/BUILDING_UPGRADES_PLAN.md](docs/BUILDING_UPGRADES_PLAN.md)。
上一个版本 1.7.0 也是纯新增：`GameSnapshot.ModeName` + `PurchaseModes.WireName()`——
它们修的是"前端拿枚举序数当档位名用"那条线上故障（`mode` 那个数字在线上原样留着，
没有破坏任何消费者），往返与判别力见 [CHANGELOG](../CHANGELOG.md) 与
[OPEN_WORK](docs/OPEN_WORK.md) 的 N 条。
**1.6.0 一个公开成员都没有增删**，
却把结局的落定条件又一次收紧了：从 1.5.0 的"玩家**被展示过**那批待答表态"
改成"玩家把它们**答完**"（`MarkPendingChoicesShown()` 因此退化成**诊断信号**，
不再参与判定）——那处语义改动、它刻意移除的那条性质与"答不上的表态不拦结局"这条
补丁规则都记在 [CHANGELOG](../CHANGELOG.md) 里。

改了公开 API 之后要跑 `tools/public-api.ps1` 重新生成快照——
**那是流程的最后一步，不是第一步**（先重生成会让守卫变成橡皮图章）。

---

## 从这里开始读

| 文档 | 什么时候读 |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 想改引擎：模块地图、时间模型、数值管线、价格求解、存档迁移、终端渲染的取舍 |
| [docs/CONTENT_AUTHORING.md](docs/CONTENT_AUTHORING.md) | 想写一个新内容包：数值节奏、构建期校验规则、以及一批真实踩过的坑 |
| [docs/VERSIONING.md](docs/VERSIONING.md) | 想动公开 API，或想知道这套快照守卫**保证不了**什么 |
| [docs/STRUCTURE_OPTIMIZATION.md](docs/STRUCTURE_OPTIMIZATION.md) | 想动**验证回路 / 构建脚本 / 内容创作模型**，或想知道"下一次改动为什么这么贵"（结构层清单，与 `docs/OPEN_WORK.md` 的待办表**不重复**） |
| 仓库根的 `README.md` | 想先跑起来玩一玩（`.\tools\play.ps1`） |
| `games/docs/ROADMAP.md` | 想看这套引擎是怎么被十个包验证过来的（含每条决策的理由与可逆性） |
