# 开发总览：这个仓库的形状、这段开发怎么走过来的、以及踩出来的规矩

> **这份文档是入口，不是权威。** 它只做三件事：**定位**（东西在哪、怎么拼起来）、
> **叙事**（当前这个状态是一步步怎么来的）、**教训**（哪些规矩是付过代价才得到的）。
> 每一句关于代码或流程的话都指向权威文件；**本文不复制它们的内容**——
> 一份重复了别处内容的总结，比没有总结更坏（它会先过期，然后被当成事实引用）。
>
> | 你想知道 | 权威文档 |
> |---|---|
> | 这个项目现在是什么样 | [README](../../README.md)、[STATUS](../../STATUS.md) |
> | 逐版本改了什么、兼容性影响 | [CHANGELOG](../../CHANGELOG.md) |
> | 引擎怎么分层、为什么这么设计 | [ARCHITECTURE](ARCHITECTURE.md) |
> | 还差什么、哪些还没验 | [OPEN_WORK](OPEN_WORK.md) |
> | 怎么写内容、怎么验收 | [CONTENT_AUTHORING](CONTENT_AUTHORING.md)、[ROADMAP](../../games/docs/ROADMAP.md) |
> | 什么改动升哪一位版本 | [VERSIONING](VERSIONING.md) |
> | 怎么发一个版本 | [RELEASING](RELEASING.md)、[DISTRIBUTION](DISTRIBUTION.md) |
> | 这个会话里派过哪些子 agent、各自落在哪 | [AGENT_ARCHIVE](AGENT_ARCHIVE.md) |
>
> **写作时点**：2026-10-03，`HEAD = 172f386`（`main`），工作区**同时有另一轮改动在进行**
> （见 §6.3）。本文里所有计数都在 §4 注明了"何时、在哪棵树上"——
> 这个仓库的数字**每加一条守卫就会变**，任何"当前 N 条"都必须连同时间一起读
> （这条纪律本身就是 [OPEN_WORK](OPEN_WORK.md) 的 M 条买来的）。

---

## 1. 项目的形状

### 1.1 两个顶层目录，一条单向依赖

```
engine/    引擎：core/ + content/（十一个内容包）+ tests/ + docs/     ← 自包含，可整体提取
games/     旗舰示例作品：hosts/（终端 + Web 两个宿主）+ docs/          ← 依赖 engine/，反向不依赖
```

- `engine/` 的**自包含**是一个可以执行的主张：把它整个搬走，配合仓库根的 `Directory.Build.props`
  就是一个独立引擎仓库（[engine/README](../README.md) 开头）。
- 依赖方向是**单向**的：`Views → GameEngine → Simulation / Content → Numbers / Randomness`
  （[ARCHITECTURE](ARCHITECTURE.md) 第 49 行）。
- **内容包为什么在 `engine/` 而不在 `games/`**：它们是**引擎的集成测试探针**，不是某部作品的资产。
  `ArchitectureTests` 靠它们证明"核心不认识内容"；`EraTests` / `LoreTests` / `PrestigeTests`
  的通用守卫靠**横扫每一个包**才抓得出沉默失败（[README](../../README.md) 第 206~209 行、
  [engine/README](../README.md) 第 24~31 行）。

### 1.2 什么是一个"内容包"

一个包 = **`engine/content/<包名>/` 一个目录 + 一个 csproj**，里面是**纯数据**：
建筑 / 升级 / 成就 / 增益 / 金猫结果 / 纪元 / 叙事 / 表态 / 立场 / 结局，
加上可选的 `IGameModule`（第二资源，如幸福感 / 士气 / 被阅读度）与 `text.json`（面向玩家的文案）。
形态与写法见 [CONTENT_AUTHORING](CONTENT_AUTHORING.md) §1~§13；
换皮批产的验收清单见 [STAGE_5_RESKINS](../../games/docs/STAGE_5_RESKINS.md)。

三条"加一个包"的硬事实：

- **核心零改动**。十一个包里四个换皮包的核心 diff 是 0 行（[engine/README](../README.md) 第 48~50 行）。
- **包与包不许互相引用**（R7）——每个包一个 csproj，编译期就挡住。
- **`text.json` 会被自动复制到输出目录**：复制规则在仓库根的 `Directory.Build.props`（第 77~86 行），
  **所以新增包不需要改 csproj**（[OPEN_WORK](OPEN_WORK.md) C 条末尾）。

### 1.3 引擎"拒绝知道"什么，以及**哪条用例在守**

这是这个仓库最核心的一组主张。**四条不变量里只有三条有用例**，第四类靠评审与 diff——
这一点必须说清，否则"违反即设计事故"会被读成"有测试兜着"。

| # | 不变量 | 权威定义 | 守它的用例 |
|---|---|---|---|
| **A1** | 核心不认识内容：不引用任何 `NekoClicker.Content.*`，且程序集字符串字面量里不出现任何内容 id | [ROADMAP](../../games/docs/ROADMAP.md) §4 第 180 行 | `ArchitectureTests.Architecture_CoreDoesNotReferenceContentProjects`、`Architecture_CoreContainsNoContentIds`（`engine/tests/ArchitectureTests.cs:27`、`:46`；后者先断言 id 集合 ≥100，**防这条守卫自己假绿**，见 `:53`） |
| **A2** | `Era` 只通过**四个接缝**进引擎，核心不出现针对具体层号的分支 | [ARCHITECTURE](ARCHITECTURE.md) 第 144~154 行、[ROADMAP](../../games/docs/ROADMAP.md) 第 181 / 185~194 行 | `ArchitectureTests.Architecture_CoreHasNoEraSpecificBranches`（`ArchitectureTests.cs:90`，禁用 token 表在 `:95-100`） |
| **R7** | 内容包之间互不引用——**每包一个 csproj**（换包必须能独立发布） | [ROADMAP](../../games/docs/ROADMAP.md) §3 的 R7（第 130 行） | `ArchitectureTests.Architecture_ContentPacksDoNotReferenceEachOther`（`ArchitectureTests.cs:66`） |
| **A3** | 新增能力必须以**通用形式**命名，不得以某个包的名字命名 | [ROADMAP](../../games/docs/ROADMAP.md) 第 182 行 | **没有用例**：命名规范 + 评审（该表自己这么写的） |
| **A4** | 不动 `ProductionCalculator` / `Pricing` | [ROADMAP](../../games/docs/ROADMAP.md) 第 183 行 | **没有用例**：每次改动的 diff 检查 |

A2 那四个接缝（`BalanceFor` / `ModifierResolver` 的来源表 / `NumericMetric.Era` /
`ResetRun` 的继承参数）之所以是"四"而不是"若干"，是因为**任何第五个地方都要先证明
它无法被这四个表达**（[engine/README](../README.md) 第 44~46 行）。

### 1.4 另外几条**确实有用例**的硬约束（换改动类型时要看的那几条）

| 约束 | 守它的用例 | 权威 |
|---|---|---|
| 公开 API 只增不改；改 API 必须同时升版本 | `PublicApiTests`（4 条，含一条**故障注入**证明守卫会红）+ `VersionTests`（4 条，含 CHANGELOG 必须有当前版本条目、README 必须广告当前版本） | [VERSIONING](VERSIONING.md) §3 的表 |
| 三个内容类别的条件互不撞车、序号不倒挂 | `LoreTests`（25 条，含 `EraGatedLore_StaysBelowItsEraCompletion`） | [README](../../README.md) 第 437 行 |
| 纪元完成条件必须单调（白名单校验） | `EraTests`（28 条，含 `MonotonicGuard_RejectsADecayingCounterInCompletion`） | [ARCHITECTURE](ARCHITECTURE.md) 第 156~161 行 |
| 内容真的走得完（第 1 层到最后一层） | `EraTests.G4_RobotWalksFromTheFirstEraToTheLast` | [ROADMAP](../../games/docs/ROADMAP.md) G4（第 31 行）、[ARCHITECTURE](ARCHITECTURE.md) 第 279~282 行 |
| 建筑曲线配方 / 计数器显示名登记 | `ContentTests.BuildingCurve_FollowsTheRecipeInEveryContentPack`、`CounterNames_AreRegisteredForEveryReferencedCounter` | [README](../../README.md) 第 476~477、494~495 行 |
| 纪元包的永久线一局买得完（转生除数按自己的阶梯标定） | `PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun` | [README](../../README.md) 第 481~487 行 |
| `text.json` 与代码**双向**一致 + **写死的条数表**（防"代码与文件同时少一条"） | `ContentTextFileTests`（测量那棵树上是 **31** 条；工作区里已在往 **33** 条长，见 §4.3） | [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §12.3、[CONTENT_AUTHORING](CONTENT_AUTHORING.md) §12.0 |
| 增量帧的**字节预算**（一个 tick 的增量必须远小于全量） | `WebSnapshotProtocolTests.IdleDelta_StaysSmallInBytes_EvenWhileProgressDrifts`（判据 `engine/tests/WebSnapshotProtocolTests.cs:432`：`deltaBytes * 20 < fullBytes`） | [WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) 第 331~338 行 |
| 前端累积出的快照必须与全量逐字节相同 | `WebSnapshotProtocolTests.DeltaMergedIntoSnapshot_EqualsTheFullSnapshot`（`:34`） | 同上 |
| 结局等的是"作答"，不是"看过" | `EndingGraceTests`（13 条）；守它的那条用例**三代都是重写、从不删除** | [OPEN_WORK](OPEN_WORK.md) K 条（名字表在第 585~589 行） |
| 存档：写坏必须响、上一份好存档必须留、写前必须能读回来 | `SaveFileTests`（9 条） | [CHANGELOG](../../CHANGELOG.md) `[未发布]` 第 320~357 行 |

> **看这张表的方法**：守卫红了的时候，它问的是「**你知道自己在破坏什么吗**」，
> 不是「要我帮你把红变绿吗」——所以 `tools/public-api.ps1` 是流程的**最后一步**，不是第一步
> （[VERSIONING](VERSIONING.md) §4）。

### 1.5 两个宿主：同一份契约的两种消费方式

引擎是哑的：它只产出 `GameSnapshot`（纯数据）并接受命令（`Click` / `BuyBuilding` /
`AnswerChoice` / `DismissOfflineProgress` …）。两个宿主**共用同一份存档** `saves/<包 id>.json`
（[games/README](../../games/README.md) 第 10~32 行）。

| | `games/hosts/Demo.Cli/` | `games/hosts/Web/` |
|---|---|---|
| 形态 | 终端全屏 TUI：ANSI、**只重画变化的行**、无头模拟 | 本地 HTTP 宿主 + 静态 `wwwroot`：SSE 全量 + 增量、浏览器里可玩 |
| 怎么跑 | `tools\play.ps1`（`--package` / `--simulate` / `--panel` / `--frame`） | `tools\web.ps1 run`（开发期必须 `dotnet run`，见 [STATUS](../../STATUS.md) §7 第 6 条） |
| 第二重身份 | **框架回归基线**：`FrameRenderTests` 直接引用它，钉住"渲染行数 == 终端高度、每行宽度 == 终端宽度"（[games/README](../../games/README.md) 第 21~23 行） | **浏览器协议的唯一证据来源**：`tools/api-test.ps1` 真起它、真读 SSE |
| 状态所有权 | 交互循环独占引擎 | **一条专用线程独占 `GameEngine`**，HTTP 命令走 `Channel` 投递（[games/README](../../games/README.md) 第 53~54 行） |
| 构建 | 在 `NekoClicker.sln` 里 | **自带单项目 sln，刻意不挂进主 sln**（理由见 [README](../../README.md) 第 397~405 行）；`tools/build.ps1` **两条都编** |
| 调试 | `--grace` 等命令行开关 | 「跳层门」：`NEKO_DEBUG_KEY` + `?epoch=N`，默认**不存在**这道门，跳层**不写存档**（[WEB_DEBUG_GATE_PLAN](WEB_DEBUG_GATE_PLAN.md)） |

终端那边的渲染取舍（为什么不是每帧整屏重写）有一张实测表：写入量 4,170 KB → 137 KB，
**过去 97% 的终端写入是在重画没变的东西**——[ARCHITECTURE](ARCHITECTURE.md) 第 216~245 行
明确写了"不要『简化』回整屏重写"。

### 1.6 文案住在文件里：十一类

面向玩家的文案已经**全部**搬进各自的 `text.json`（第五轮收尾）：
`storylines` / `lore` / `buildings` / `eras` / `endings` / `stances` / `choices` /
`achievements` / `buffs` / `upgrades` / `goldenCookies`。
形态、条数与"哪一类故意不搬"见 [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §12.7 与
[CONTENT_AUTHORING](CONTENT_AUTHORING.md) §12.0~§12.0.4。

**刻意留在 C# 里的中文**（不是漏项，是另一类东西）：条件树与修饰符的 `Describe` 文案、
引擎的通知/报错消息、内容包自己的构建期校验消息——它们由引擎渲染，不是内容
（[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) 第 646~649 行）。

---

## 2. 这段开发是怎么走过来的

窗口：**2026-10-01 → 2026-10-03**（`67a1f22` → `172f386`，48 个提交）。
下面**按主题**分组，不按提交——同一个主题的提交常常横跨几天，而同一句话的反复修正才是重点。
每条只给"发生了什么、结论落在哪"，细节一律指过去。

### 2.1 Web 推送协议：全量 + 增量，以及那条线上故障

- **协议本身**：SSE 推「信封 + 变化字段」，全量 56.9 KB、增量均 1.99 KB（省 97%）；
  4 Hz 推送与 30 Hz 模拟步长解耦；每 120 帧做一次全量对账，兜住"增量丢一帧"。
  数字与常量位置：[games/README](../../games/README.md) 第 51 行、`games/hosts/Web/GameHost.cs`
  第 58~63 行。
- **真正的守卫是"按字节数"**，不是"按字段数"——`IdleDelta_StaysSmallInBytes_EvenWhileProgressDrifts`
  的判据是 `deltaBytes * 20 < fullBytes`（`WebSnapshotProtocolTests.cs:432`）。
  它后来成了"任何扩展都可能撑爆增量帧"这条担忧的**现成防线**
  （[WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) 第 330~338 行）。
- **mode 故障（`OPEN_WORK` N 条，`39503d3` 修复，`1.7.0`）**：线上 `"mode":0` 是**枚举序数**，
  而前端写的是 `(state.mode ?? "").toLowerCase()` ⇒ `TypeError`，**每一次 `render()` 都在中段中断**：
  批量档位按钮、离线弹窗、表态 sheet、图鉴、成就、日志**一次都没跑过**。
  修法是**给前端一个名字**（新增 `GameSnapshot.ModeName` + `PurchaseModes.WireName()`），
  而不是让它解释序数——同 1.4.0 的 `UpgradeView` 一条规矩。
  证据、影响面与"为什么两套测试当时都是绿的"写在 [CHANGELOG](../../CHANGELOG.md) 1.7.0 与
  [OPEN_WORK](OPEN_WORK.md) N 条。
- **这条故障的连锁后果照实写进了 CHANGELOG**：`renderOffline` 与表态 sheet 都在死掉的路径后面，
  所以 1.6.0 到 1.7.0 之间**玩家在浏览器里走不到结局**——`OPEN_WORK` 的 E 条
  （"真人样本一个都没有"）与"离线弹窗关不掉"大概率都是它的下游
  （[CHANGELOG](../../CHANGELOG.md) 第 41~44 行）。

### 2.2 结局落定的语义：30 秒 → "看过" → "答完"

同一处语义**被改了三次**，每一次都配一次 minor 升版，每一次都被记成**例外**：

| 版本 | 判据 | 提交 | 记录 |
|---|---|---|---|
| 1.1.0 | 结局条件成立后等 **30 模拟秒**，超时视作放弃 | — | [ENDING_GRACE_PLAN](ENDING_GRACE_PLAN.md)（原文保留，判据已被取代两次） |
| 1.5.0 | 换成**条件**：玩家**被展示过**那批待答表态才允许落定；30 秒**删掉**（不保留成兜底） | `cfc238c` | [OPEN_WORK](OPEN_WORK.md) J 条 |
| 1.6.0 | 再收紧成"玩家把它们**答完**"；`MarkPendingChoicesShown()` 保留但**不再参与判定**（只剩诊断） | `80e6f59` | [OPEN_WORK](OPEN_WORK.md) K 条 |

- **为什么"看过"不够**：它只证明面板画出来了；玩家可能正读着题、或把 sheet 收起来打算"待会儿再说"，
  而结局只有一次（[ENDING_GRACE_PLAN](ENDING_GRACE_PLAN.md) §0）。
- **代价是刻意接受的**：**从不作答的玩家永远拿不到结局**。"不会永远悬着"这条性质
  从 1.5.0 起就被有意移除，1.6.0 把覆盖面从"没看到"扩大到"没答"。
- **1.6.0 顺手补了一条不补就会永久卡死的规则**：只有**答得上**的待答表态拦结局
  （内容改版删掉了那条 id / 换包读档 / 已答却又挂在队列里 → 不拦，**但引擎必须发一条警告说出来**）。
  风险与证据见 [OPEN_WORK](OPEN_WORK.md) K.3。
- **三次都是 minor，而按 §2 那张表本该是 major**——理由：判定结果不变（仍按 `Priority` 取第一个
  条件成立者），受影响的只是"结果什么时候出现"。**例外被记录在案，而且明写了"下一次该考虑 major"**
  （[VERSIONING](VERSIONING.md) §2 的"已知的三次例外"）。

### 2.3 存档加固：写坏要响、上一份好存档要留、写前要能读回来

`d18335e` 把 `FileStorage` 的写入从"写 `.tmp` → `File.Move(overwrite: true)`"改成三步
（代码在 `engine/core/Persistence/IStorage.cs` 第 84~125 行）：

1. 内容落到 `<槽位>.tmp`（不变）；
2. **提交前把刚写出来的字节读回来、用 `SaveSerializer.Parse` 反解一遍**，读不回来就抛
   ——验的是**磁盘上那一份**；
3. `File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true)` 原子替换，
   并把被换下来的那一份留成 `<槽位>.bak`——**但只在它自己也读得回来时才留**
   （读不回来的那份不配叫"上一份好存档"）。

同一轮还修了三处**"存档失败却报告成功"**，其中一处是对上一条修复的更正；完整条目（含
`ignoreMetadataErrors: true` 为什么不是可选项、以及两条故障注入的红字）见
[CHANGELOG](../../CHANGELOG.md) `[未发布]` 第 320~357 行。守卫是 `SaveFileTests` 9 条
（`engine/tests/SaveFileTests.cs:32`~`:348`）。

同一时期的第二件事：**关停真的存档**（`dbe3183`）。1.2.0 的 CHANGELOG 曾宣称"关停时强制存档一次"，
**实测没有发生**；修法、阳性/阴性对照（优雅关停 vs `taskkill /F /T`）与"真实 `saves/` 指纹不变"
写在 [CHANGELOG](../../CHANGELOG.md) 与 [OPEN_WORK](OPEN_WORK.md) B 条。

### 2.4 文案外置：五轮，每轮一条同一个判据

五轮**都不是**"再写一个 C# 解析器"，而是**迁移前后各 dump 一次运行期文字、逐字节比对**——
判据落在**行为**上，不是"两份文本看起来一样"（[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §8.2）。

| 轮次 | 范围 | 保真判据（迁移前后 dump 逐字节相同） | 守卫 | 记录 |
|---|---|---|---|---|
| 一（起点） | 十个包的**图鉴散文** 421 条 | 176,708 字节 | 泛化成 `ContentTextFileTests` | [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §8 |
| 二 | 建筑的 `name` / `description`（11 包 104 座） | 13,567 字节 | 4 → 7 条 | §9 |
| 三 | 建筑的 `Icon` + 纪元的六个字段（9 包 49 层） | 图标 7,895 + 纪元 45,621 字节 | 7 → 10 条 | §10 |
| 四 | 结局 / 表态 / 立场 / 成就 | 四份共 345,460 字节 | 10 → 22 条 | §11 |
| 五（收尾） | 增益 / 升级 / 金猫结果 | 三份共 524,163 字节 | 22 → 31 条 | §12 |

每一轮都做了**三处故意改坏**来证明守卫有判别力，并且**坏的那一处要红在预期的那一条**
（尤其"期望条数 66→65 只有条数守卫一条红"——那是"代码与文件同时少一条"这个盲区的**唯一**守卫）。
`engine/core` 在这五轮里几乎一行未改（第五轮只改了一处 XML 注释，见 §12.6）。

**为什么这一类到此为止**：剩下留在 C# 里的中文不属于"给玩家读的散文"（见 §1.6）。
下一步要动的是另一件事——**自由文本映射表集**（`$tables`），设计已经写下、实现在进行中，见 §6.3。

### 2.5 Web 界面：几次交付 + 一批无障碍修正

| 交付 | 提交 | 一句话 |
|---|---|---|
| 表态从页签改成界面内 **sheet**（小窗口） | `3650b2f` | 同一次提交里新增了**无头 DOM 冒烟测试**——这条比 sheet 本身重要，见 §2.7 |
| **离线收益弹窗** | `40d3501`（1.3.0） | 读档补发的那一笔推到界面上；去重状态放**服务端**（放前端一定会错） |
| 离线弹窗补 `max-height` + 滚动 | `c298719` | 短窗口下唯一那个"收下"按钮此前会被推出屏幕。⚠️ 该文档注明这条**是推断、未实测**（[OPEN_WORK](OPEN_WORK.md) §0.4） |
| **计数器不再振荡** | `cd28dd4` | 领先量靠"停下来等"封顶，不再"超调再拉回一半"；三条不变量注释在 `app.js:44`~`60` |
| **建筑的可展开故事框** | `f9044ec` | 卡片**照旧点一下买**（那是核心循环），"看故事"是**另一个控件**（真 `<button>`，Tab 可达）；故事文本取自**快照里本来就有**的 `buildings[].description`，**没有加字段、没有升版本** |

无障碍与配色四项（都在 `wwwroot/app.css`）：`:focus-visible` 三处、`34rem` 断点、
`prefers-reduced-motion`（`34f1fce`）；"买得起"加第二重不依赖颜色的信号（`35cf8ab`）；
`--dim` 在卡片悬停背景上只有 4.12 → 提到 4.75 过 AA（`134e17f`，"只在悬停时失败"的缺陷）；
4 处重复的硬编码色值换成 `var(--token)`（`c234c51`，**构造上零视觉差异**）。
"哪些硬编码色值该统一、哪些本来就不该是 token"的判据见 [OPEN_WORK](OPEN_WORK.md) D 条——
它是**计数不是结论**那条教训的实例。

### 2.6 调试门

`NEKO_DEBUG_KEY` + `?epoch=N`：密钥只来自环境变量（不设 = **这道门不存在**），
403/400 **明确拒绝**而不是静默忽略，跳层会话**禁存档**，响应里**明说跳过了哪些账**
（继承 / 层历史 / 结算转生货币）。方案与六条验收见 [WEB_DEBUG_GATE_PLAN](WEB_DEBUG_GATE_PLAN.md)；
端到端里对应"没有 `NEKO_DEBUG_KEY` 时带 `epoch` 必须是 403"那一条。

### 2.7 前端验证这条线：从一次"整页停摆"长出来的一层

这条线是这个窗口里**最有价值的一段工程史**，值得单独读：

1. **事故**（1.5.0 引入、`b48f2fa` 修复）：`app.js` 给一个**从未声明**的标识符赋值，
   ES module 是严格模式 ⇒ 每帧第一行抛 `ReferenceError` ⇒ `recomputeAffordable()` / `render()`
   **从未执行**，**整页不更新**。而**四种检查全都没拦住**：`api-test.ps1` 不执行页面 JS、
   `build.ps1` 不含前端、`node --check` 只证语法、"人工读代码"取决于读的人
   （[FRONTEND_CHECKS](FRONTEND_CHECKS.md) §一、§二）。
2. **结论**：**HTTP 层全绿 ≠ 页面能用**，于是立下一条纪律——任何 `wwwroot/*` 的改动，
   交付时必须附"页面真跑过"的证据；"文件内容正确 / 服务器返回的是新文件 / 括号平衡"**都不算**；
   **不许拿 `node --check` 当唯一证据**（[FRONTEND_CHECKS](FRONTEND_CHECKS.md) §四）。
3. **落地**：`tools/web-smoke.mjs`——用最小 `document` / `EventSource` 替身**真跑 `app.js`**，
   喂合成快照，断言"没抛异常"与关键 DOM 状态。零第三方依赖，符合仓库底线。
4. **第二次事故与它的教训**：`mode` 那条 bug 之所以能绿，是因为冒烟夹具**写的是前端以为的形状**
   （`mode: "buy10"`），而线上是 `"mode":0`——**夹具对线上撒了谎**。
   现在夹具改成**从真宿主抓下来的快照**（`tools/fixtures/web-snapshot.json`），
   并加了一条"**夹具不是谎话**"守卫（[CHANGELOG](../../CHANGELOG.md) 1.7.0；
   [OPEN_WORK](OPEN_WORK.md) N 条）。
5. **诚实边界**：本机**结构性地起不了真实浏览器**（Chrome / Edge 在沙箱下拿不到 ACL），
   所以"观感"只能由真人判；那条**可复制的截图手法**（内联静态页 + 桩 + 无头截图，
   且 **fixture 要拿真宿主抓的快照去改**）见 [STATUS](../../STATUS.md) §6 与
   [games/README](../../games/README.md) Web 一节。**它是"我看过"，不是守卫。**

### 2.8 发布与文档纪律

- **已发布的最后一个 tag 是 `v1.4.0`**（`git describe` 可复核）。1.5.0 / 1.6.0 / 1.7.0
  **都已在 `main` 上提交，但未打 tag**。[RELEASING](RELEASING.md) §3~§5 是 1.2.1 / 1.3.0 / 1.4.0
  三次发布的**实际执行记录**（打包、自检、tag、推送、CI 编号与耗时）——那是流程文档里
  唯一有执行记录的部分，也是"清单之外还会撞到什么"的唯一来源。
- **清单外发现比清单本身值钱**：1.2.1 那一轮的"README 里两行过期信息不在守卫射程内"、
  1.3.0 那一轮的"**`[未发布]` 攒着的宿主改动要不要收进这一版**"、以及
  "**第 ⑨ 步的产物自检逮到了一个从 1.2.0 就在的首页 404 缺陷**"
  （开发期路径全绿 ≠ 产物能用）——都记在 [RELEASING](RELEASING.md) 各节的"逮到的三件事"里。
- **数字漂移的定点修正**（`571567d`、`a080765`、`OPEN_WORK` M 条）：用例数每加一条守卫就变，
  而"当前计数"散在 8 个文件里。做法固定为"**只改现在时、不改历史，带上下文逐处改，不做批量替换**"。
  ⚠️ M 条末尾自己写明：**真正该做的是让这些数只有一个出处，本轮没做**。
- **交接件与它的退役**：夜间交接件写于推送之前，推送完成后**文件删掉、仍成立的部分并入
  `OPEN_WORK` §0.6**（并入时还复核了"只差推送"确实已做完）。
- **子 agent 的索引**：这段开发是**多个独立上下文的 agent 接力**做的（本会话先后 16 个）。
  谁做了什么、落在哪个提交、以及**它额外贡献的发现**（那部分常常比提交本身值钱）
  由 [AGENT_ARCHIVE](AGENT_ARCHIVE.md) 索引；它的 §三 是**跨 agent 的教训**
  （夹具不撒谎、并发构建的代价、"我以为"必须被核对、诚实边界有价值）——
  与本文 §3 是同一批教训的两个视角。

---

## 3. 这个仓库实际执行的开发规矩（每一条都付过代价）

这一节是本文最有用的部分：**别重犯**。每条都给"代价"与"记在哪"。

### 3.1 中文永远不走 shell 字符串字面量

- **代价**：中文写进 `pwsh` 字符串 ⇒ `ParserError`、**整条命令未执行**（看起来像"命令跑了但没输出"）。
  这个仓库里记了两次：`FRONTEND_CHECKS.md` 第 76~78 行说那是同一天的**第三次**；
  `OPEN_WORK.md` 第 157 行说那一夜**共 4 次**，最后一次还夹了单引号。
- **规矩**：中文只经 `write` / `edit` 工具落到文件；shell 字面量保持**纯 ASCII**
  （`ef930a8` 的提交标题特意用 ASCII 写，就是为了记这件事）。
- 本文的写作过程遵守了这条：所有命令与提交信息都是 ASCII。

### 3.2 `edit` 工具要求**先用 `read` 读过那个文件**

- **代价**：用 `pwsh` 的 `Select-String` 扫过行**不算**——那不是"读过文件"。
- **记在**：[OPEN_WORK](OPEN_WORK.md) 第 158 行。读一个 10 行窗口即可。

### 3.3 `edit` 会**剥掉 `.ps1` 的 UTF-8 BOM**（还会顺手把 CRLF 改成 LF）

- **代价**：Windows PowerShell 5.1 没有 BOM 时按 **GBK** 解析中文 ⇒
  `string is missing the terminator` ⇒ 整个脚本 parse error、**一项都没跑**
  （实测那次修 BOM 之前解析器报了 20 多条错）。
- **规矩**：`tools/*.ps1` 必须**UTF-8 with BOM**；改完要**两样一起补**——
  先按 `\r\n` 归一化，再用 `UTF8Encoding($true)` 写回（`git` 会警告 `LF will be replaced by CRLF`）。
  带 BOM 的脚本如果要改一个数字，**按字节替换**写回（BOM 保留、长度不变、diff 只有那一行）。
- **记在**：[STATUS](../../STATUS.md) §7 第 1 条、[RELEASING](RELEASING.md) §6、
  [OPEN_WORK](OPEN_WORK.md) 第 147 行（`api-test.ps1` 的 BOM 实测是 `EF BB BF`）
  与第 206 / 239 行（两次按字节替换的执行记录）。

### 3.4 宿主占着 `bin` ⇒ `MSB3021` / `MSB3027`；这条路径要**先查再动手**

- **现象**：`error MSB3021: Unable to copy file ... The process cannot access the file ... because it
  is being used by another process`（伴随 `MSB3026` 重试与 `MSB3027: Exceeded retry count of 10`），
  摘要里可能出现 `0 Warning(s)` 却一堆 error。
- **仓库已记录的**：`tools/start.ps1` 第 6~7 行把这条写成了它存在的理由之一
  （"上次没退干净的宿主进程会占着 bin 里的 dll"），并在第 39~50 行做了一个**只清本仓库进程**的
  兜底：`Get-Process` 里筛 `Path` 以仓库根开头的那些，`Stop-Process -Force`。
- **"约 320 个 `dotnet` 进程 / 内存吃光"这条记录在 [AGENT_ARCHIVE](AGENT_ARCHIVE.md)**（别处没有）：
  第 24 行说那次**发现并清理了那 320 个泄漏进程，释放 9.9 GB**；
  第 39 行把它与"两个 agent 同时构建互锁 `bin`（**144 个 MSB3021/3027**）"连在一起，
  并写了**玩家侧的观感是"数字卡死"**（被卡死的正是前端每帧刷新的那条路径）。
  ⚠️ 同一份归档第 19 行**自己标了边界**：那 320 个进程与并发构建**只有时间相关性、没有解释**——
  所以它是**观察记录**，不是因果结论。
- **本文实测补充（2026-10-03）**：我**也踩到了这条路径**——`engine/tests/bin` 被占用，
  170 个 error、`0 Warning(s)`。当时的成因**不是泄漏**，而是**同一个工作区里另一轮构建正在跑**
  （同一时刻 `dotnet` 进程只有 **1 个**，`StartTime` 就在几分钟前）。
  正确处置是**先判断占用者是谁**：`netstat -ano | Select-String ':5273'` 找玩测宿主，
  `Get-Process dotnet | Select Id,StartTime,CPU` 看有没有**几百个**同源进程；
  只有确认是残留（而不是别人正在跑）才杀，而且要用严格的 `StartTime` 截止时间，
  别把自己这一轮也杀掉。等待 30 秒后锁自己就松了——**能等就别杀**。
- **可复用的判据**（`AGENT_ARCHIVE` 第 39 行结尾那句）：**机器状态要连结论一起报**——
  报告用例数、构建成败时，也要说清"当时机器上还有谁在跑"。

### 3.5 存档：原子写、**测试绝不写真实 `saves/`**、并用指纹证明

- **原子写**的形态与理由见 §2.3；代码在 `engine/core/Persistence/IStorage.cs` 第 84~125 行。
- **测试不许碰真实存档**：`tools/api-test.ps1` 自带 `--save-root <临时目录>`
  （脚本头部把它列为**刻意为之的行为**的第一条：探针会点击和买入，
  跑在玩家存档上等于拿进度当夹具）。
- **证明方式固定为"大小 + mtime + sha256"逐个文件核对**（含 `.bak`）：
  [OPEN_WORK](OPEN_WORK.md) 第 848 行、[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §12.5 第 622~623 行。
- **本文遵守**：写作前后各测了一次真实 `saves/` 的 11 个文件（大小 / mtime / sha256 前 16 位），
  **逐一相同**；`artifacts/latency.txt` 也未变（2069 字节、同一 mtime）。见 §4.2。

### 3.6 文档里的话必须是真的（这个仓库的**第一原则**）

- 写在 [OPEN_WORK](OPEN_WORK.md) 开头（"把**已经验证过的事实**和**仍然欠着的活**放在一处；
  标『未验证』的请不要当成已完成"），也写在 [games/docs/SETTING.md](../../games/docs/SETTING.md)
  第 12~14 行（"设定集与代码不许各说各话——这个项目的规矩是『文档里的话必须是真的』"）。
- **执行方式是"保留原文 + 就地更正"**，不是删掉写错的句子。句式不止一种，共同点是**不删原文**：
  B / F 用"原文（保留，以便看出当时看到的是什么）"（[OPEN_WORK](OPEN_WORK.md) 第 330 / 414 行），
  C / D / I 用"我判错了 / 查过了"的自述（第 354 / 376 / 485 行），M 用"原文保留，更正如下"（第 706 行）。
  `1.2.0` 那句"关停时强制存档一次"也是**保留原文、更正记在 `[未发布]` 区**
  （[CHANGELOG](../../CHANGELOG.md) 第 361~374 行）。
- **推论**：拿到一份文档时，先看它的**日期与"未验证"标注**；这个仓库不假装没写错，
  它假装**写错了会被标出来**。

### 3.7 守卫必须用**故障注入**证明它会红

一条永远不报警的守卫，与一条正确的守卫，在测试输出里长得一模一样。
所以每次加守卫都要**故意改坏、记下"红在哪一条"与红字原文**：
[VERSIONING](VERSIONING.md) §3 的第 4 条守卫、[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md)
§10.4 / §11.4 / §12.4、[OPEN_WORK](OPEN_WORK.md) §0.10 的三处（"点 📖 顺手买" / 清空展开状态 /
每帧重建节点）都是这个形态。
**顺序也重要**：先证明守卫会红，再接前端（[WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) 第 584~586 行）。

### 3.8 计数不是结论；先读依赖再下结论；数字要带上下文改

这一条是被**同一类错误反复收学费**换来的，[OPEN_WORK](OPEN_WORK.md) 里有四个实例：

| 错误形状 | 实例 | 正确做法 |
|---|---|---|
| **从计数推缺口** | "11 个包里 10 个有 `text.json`" ⇒ 断定第 11 个漏了。**实际 `Neko` 连 `Lore.cs` 都没有**，压根没有叙事可外置（C 条） | 先查**那一个**是否存在 |
| **拿计数当问题本身** | "44 个硬编码色值" ⇒ 当成"重复了 44 次"。实测只有 **4 个**在重复 token，其余是一次性色阶（D 条） | 分辨它的**构成** |
| **没读依赖就下结论** | 只看到断言失败就宣称"引擎过度阻塞"，读过代码后结论相反（§0.6） | **先读依赖，再下结论** |
| **用记忆代替查看** | 记得自己加过 `InternalsVisibleTo` 就判"该删"；队友早删了（I 条） | 先看文件现在长什么样；**字符串包含 ≠ 元素存在** |

**改"当前计数"的固定做法**：实测 → **带上下文逐处改** → 历史数字一律不动 →
把"改到的每一处"列表记进 [OPEN_WORK](OPEN_WORK.md) M 条（含"哪一处刻意不改、为什么"）。
**批量替换试过并被自己设的保险拦下**——这些数字在别的语境里还有别的出处。

### 3.9 测试夹具**不许对线上撒谎**

`mode` 那条 bug 能活下来，唯一原因就是冒烟夹具写的是前端**以为**的形状（§2.7）。
同类的更早一次是 `lastFrameAt`：四种检查都"绿"，页面却是死的。
**判据**：夹具要么**从真宿主抓**（`tools/fixtures/web-snapshot.json`），
要么至少有一条守卫断言"夹具里前端会用到的每个路径，**类型与真快照一致**"。
静态扫源码（"没有 `eval`"这一类）**只是廉价兜底，不是主要证据**
（[WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) 第 479~486 行自己这么说）。

---

## 4. 现在什么是绿的，以及**每一条命令看得见什么**

> **⚠️ 先读这一段，再看下面的表。** §4.1 / §4.2 是**一次带日期的实测**（2026-10-03，那棵树等于
> 提交 `39503d3`），它们**只对那棵树成立**，本文保留它们作历史。
> "**现在**是多少"**不在本文里找**——每加一条守卫它就会变。唯一出处是
> `tools\build.ps1 -Strict` 打印的那一行；**当轮基线记在 [STATUS](../../STATUS.md) §4 与
> [OPEN_WORK](OPEN_WORK.md) §0.12**。最近一次整合（2026-10-03，在 **`b625dd5`** 上实测）：
> **516 个用例**（`-Strict`，两个 sln 0 警告）、**55 项端到端**（收尾自审 55/55、跳过 0）、
> **103 条冒烟**（`node tools\web-smoke.mjs`）。

### 4.1 那一次的实测（2026-10-03，`+08:00`；树 = `39503d3`）

测量时的树：**内容等于提交 `39503d3`**（`1.7.0` 那次修复；`HEAD` 在构建开始时是 `f9044ec`，
`39503d3` 于 12:45:15 落地，`api-test` 用的就是它那份脚本）。
四个关键文件的 SHA-256 前缀可复核：`games/hosts/Web/wwwroot/app.js` = `222B16942DC4`、
`engine/tests/WebSnapshotProtocolTests.cs` = `ACC872A606F9`、`engine/core/Views/Views.cs` =
`521436C1EEAD`、`Directory.Build.props` = `B9C3773A440E`（其中 `<Version>1.7.0</Version>`）。

| 命令 | 实测结果 | 时刻 | 它**看得见**什么 | 它**看不见**什么 |
|---|---|---|---|---|
| `powershell -NoProfile -File tools\build.ps1 -Strict` | 退出码 **0**；两个 sln **各 0 Warning(s) / 0 Error(s)**；**全部通过：493 个用例** | 12:39:28 → ~12:44 | C# 编译（含 `-warnaserror`）、引擎与内容行为、公开 API 快照、版本守卫、内容通用守卫横扫 | **前端 JS 一行都不在编译范围内**（[FRONTEND_CHECKS](FRONTEND_CHECKS.md) 第 37 行） |
| `powershell -NoProfile -File tools\api-test.ps1` | 退出码 **0**；**全部通过：51 项检查** | 12:45:02 → 12:45:38 | HTTP 协议、camelCase、静态文件可达、命令、通知、**真读 SSE 流**、离线补发（**重启一次宿主读档**）、`modeName` 形状与七 token 往返 | **不执行页面里的 JS**；不判观感；不覆盖"打包产物从包根目录启动"那条路径（那一步仍是人工自检，见 [RELEASING](RELEASING.md) 第 ⑨ 步） |
| `node tools/web-smoke.mjs` | **82 / 82 条通过** | ~12:34 | **真的把 `app.js` 跑起来**：没抛异常、DOM 结构与状态、故事框展开/收起与"不顺手买"、空格键归属 | 只覆盖**被执行到的路径**（没走到的分支只有语法保证）；**没有真浏览器**（本机起不了）；不判布局与配色 |

其余可复核的数：`engine/tests` 里 `[Test]` 方法 **493 个**（与运行器报的 493 一致——
注意用 `^\s*\[Test\]` 逐行数会漏掉 17 条写在同行的方法）；
`engine/core/PublicApi.txt` **1933 行**；`artifacts/latency.txt` **2069 字节**。

> 构建日志里那句 `[作答延迟埋点] 写不进 <…>/blocker/latency.txt：…` **不是故障**：
> 它是 `WebChoiceLatencyTests.Probe_SurvivesAnUnwritableLogFileWithoutLosingTheMeasurement`
> 在**故意**制造一条不可写路径，验"量到了但写不下去时不许把这次测量丢掉"（那次 493 全绿）。

### 4.2 与"没写坏东西"有关的两个指纹

- **真实 `saves/`**：目录里共 **11 个文件**（5 个槽位各带一份 `.bak`，再加一个没有 `.bak` 的
  `ninelines.json`）在本次三条命令**前后**的大小 / mtime / sha256 前缀**逐一相同**。
- **`artifacts/latency.txt`**：2069 字节、mtime `2026-10-03T12:01:19`，未被这三条命令改写
  （测试用的是 `artifacts/latency-tests/` 下的临时路径）。

### 4.3 ⚠️ 别把上面这些数当"当前值"

本文 §4.1 的 `493` / `51` / `82` **只对那一棵树（`39503d3`）成立**，写完之后它们又动过好几轮：
写本文时工作区里那轮（§6.3 的 `$tables`）后来**已落地**（`3312715`），
之后还有建筑升级系统（[OPEN_WORK](OPEN_WORK.md) §0.11，504 → 516）。
要报数就照这个格式报：**数字 + 时刻 + 那棵树长什么样**；**当轮基线看
[STATUS](../../STATUS.md) §4 与 [OPEN_WORK](OPEN_WORK.md) §0.12**
（最近一次整合：2026-10-03 在 `b625dd5` 上 **516 / 55 / 103**）。

---

## 5. 新来的人从哪开始（按改动类型）

| 我想改 | 先读 | 验收（必须真跑） | 别做的事 |
|---|---|---|---|
| **一个内容包的文案 / 数值 / 叙事条件** | [CONTENT_AUTHORING](CONTENT_AUTHORING.md) §1~§14（尤其 §8 常见坑、§12.0~§12.0.4 文案形态、§13 表态/立场/结局的四条硬规则）；新包照 [STAGE_5_RESKINS](../../games/docs/STAGE_5_RESKINS.md) + [PACK_01_CAT_CAFE](../../games/docs/PACK_01_CAT_CAFE.md) | `tools\build.ps1 -Strict`（内容守卫会横扫全部包）+ `tools\play.ps1 --package <id> --simulate <秒> --auto` 看曲线活性 | 别为了让某个包"特殊"去加核心公开成员——先照 §1.3 的四个接缝想一遍（A3） |
| **引擎（`engine/core`）** | [ARCHITECTURE](ARCHITECTURE.md) 全篇（先看第 49 行的依赖方向与第 247 行的扩展点表）+ [VERSIONING](VERSIONING.md) §2~§4 | `tools\build.ps1 -Strict`；若动了公开 API：**先决定版本 → 改 `Directory.Build.props` → 写 CHANGELOG → 才**跑 `tools\public-api.ps1` | **别先重生成快照**（那会让守卫退化成橡皮图章） |
| **某个宿主（终端 / Web）** | [games/README](../../games/README.md) 的"两个前端宿主"一节 + [WEB_DEBUG_GATE_PLAN](WEB_DEBUG_GATE_PLAN.md)（若碰调试路径） | `tools\build.ps1 -Strict`（两条 sln 都编）+ `tools\api-test.ps1` | 别把 Web 宿主挂进主 sln（理由见 [README](../../README.md) 第 397~405 行）；开发期别直接起 `bin` 里的 exe（`wwwroot` 会解析到源码目录，见 [STATUS](../../STATUS.md) §7 第 6 条） |
| **前端（`wwwroot/*`）** | [FRONTEND_CHECKS](FRONTEND_CHECKS.md) 全篇（81 行，先看 §二那张"四种检查各证明什么"的表）+ [games/README](../../games/README.md) Web 一节 | `node tools\web-smoke.mjs`（**真的跑 `app.js`**）→ 再 `tools\api-test.ps1` | 别拿 `node --check`、`Content-Length` 或"服务器返回的是新文件"当证据；**新夹具要来自真宿主** |
| **存档格式 / 迁移** | [ARCHITECTURE](ARCHITECTURE.md) 的"存档与迁移"一节 + [VERSIONING](VERSIONING.md) §6 第 3 条（存档兼容**不在**公开 API 承诺范围内） | `tools\build.ps1 -Strict`（`SaveFileTests` / `SaveTests`）+ 手工验一条真存档 | 别写真实 `saves/`；别把"没 bump 存档版本号"当成"格式变了也没事"（R8 的默认值安全只在没真改格式时成立） |
| **发一个版本** | [RELEASING](RELEASING.md) §1 那张表（11 步，含"会写版本号的每一处"§2） | 按 §1 顺序走完，含**打包后自检** | 第 ⑦⑧ 步全绿之前**不要打 tag**（tag 推上去删不干净） |

---

## 6. 还没完的、还有争议的

### 6.1 活着的条目：看 [OPEN_WORK](OPEN_WORK.md)，本文只给指针

> **⚠️ 唯一权威的"下一步"清单在 [OPEN_WORK](OPEN_WORK.md) 文首的《下一步：唯一权威清单》**
> （2026-10-03 整合）。下表是整合之前按主题列的指针，它指的那些章节仍然成立，
> 但**每一行"还开着没有"以那份清单为准**——例如下表的 §0.3 第 1 条（推送）已经做完
> （`origin/main` 现在与 HEAD 同一提交），而它在这里只是当时的记录。

| 条目 | 一句话 | 指针 |
|---|---|---|
| **E（重点）** | 宽限期那 30 秒**仍无人类数据**；工程那半（埋点）已做完，缺的只有真人去玩。埋点落进 `artifacts/latency.txt`；**本文实测该文件目前 0 条样本行**（29 行里 14 行是 `session start`、15 行是表头/注释） | [OPEN_WORK](OPEN_WORK.md) 第 388 行起 + 开头"重点待办"块 |
| §0.3 第 1 条 | 未推送的提交（写作时 `origin/main = cd28dd4`，`HEAD = 172f386` ⇒ **领先 4 个**） | §0.3 第 1 条（原写"2 个"，已过期） |
| §0.3 第 3 条 | 离线弹窗"关不掉"的三种可能——**1.7.0 给出了新解释**：`renderOffline` 在死掉的路径后面 | §0.3 第 3 条 + [CHANGELOG](../../CHANGELOG.md) 1.7.0 |
| §0.3 第 6 / 7 条 | ⑥ 的调优（难度跨度 / 套件墙钟）先测每步成本；**存档迁移机制在生产里零实现**（第一次真改格式时那套机器才第一次被检验） | §0.3、[TUNING_ANALYSIS](TUNING_ANALYSIS.md)、G / H 条 |
| §0.3 第 5 条 / M 条 | "当前计数散在 8 个文件里"这件事本身仍是欠账（该让它们只有一个出处） | M 条末尾 |
| §8.4（STATUS） | 二周目界面、通知未读游标：**都要先定语义再动手**，现在没有答案 | [STATUS](../../STATUS.md) §8.4 |

### 6.2 刻意搁置的设计

**[WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md)（把 Web 这一侧做成可扩展外部接口）——状态：已搁置，本文不实施。**
理由写在它自己的头部（第 3~8 行），两条：① 十一个包**没有一个**需要扩展机制；
② 写作期间唯一的真实动因（"把建筑的剧情画出来"）已经用**引擎既有字段**落地（`f9044ec`），
**没有碰快照协议、没有升版本**。

它还留了两条将来要用的坐标：**真要落地时版本号从 `1.8.0` 起算**（`1.7.0` 已被 `ModeName` 占用），
**登记进 `OPEN_WORK` §3 的编号是 `O`**（`N` 已经被那条 `mode` 修复用了）。
它的 §11 还列着 D1~D9 九个**必须先拍板**的决定，以及 §12 十条"明确不做"。

> 为什么把它记在这里而不是"待办"里：**它是一次被搁置的提案，不是欠账**（注意用词：
> 是"搁置"不是"否决"——它第 551~552 行自己写着"如果人认为“外部接口”本身就是交付目标
> （而不是为了某个具体界面需求），那 A 应该做，**但那时 §11 的 D1~D9 必须逐条定完**"）。
> 仓库里这个区分是刻意的——`OPEN_WORK` §3 是"已验证的欠账"，
> 而这份是"尚未决定是否要做的设计"。

### 6.3 写作时"正在进行"、**现在已经落地**的（**不是本文、也不是上面任何一条**）

- **文案外置的第六步：自由文本映射表集（`$tables`）**。
  设计已写下并提交（`13142d4`，[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §13），
  要点是：**不搬任何现成文本**，而是给 `text.json` 加一种"代码还没读它"的表——
  于是它与 `EnsureNoOrphans` 的严格性正面冲突，解法是**根节点的保留清单 `$tables` 明文声明
  「这张表没人读」**，并由守卫核对"声明的诚实性"（声明为 `free` 的**不许**被代码取用）。
- **它现在已落地**（`3312715`）：`ContentText.cs` + 两个测试文件 + `Lab/text.json` +
  `CONTENT_AUTHORING.md` 都已提交；设计、守卫与三条判别力注入见
  [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §13，作者的写法见 `CONTENT_AUTHORING.md` §12.0.5。
  **它当时没能登记进 [OPEN_WORK](OPEN_WORK.md)**（那份文件正被另一轮持有）——
  本次整合补上了（§0.12.2 与 §3 的 P 条）。
- **对读者的直接影响**：本文 §4 的 `493` / `51` / `82` 都只对那棵树成立，
  引用计数前请自己再跑一次（当轮基线见 §4 顶部那段）。

---

## 7. 我在写这份总结时发现的文档漂移（只报告，未改）

> **✅ 2026-10-03 复核：下表的 7.1~7.5 已经全部修掉**——关掉它们的是 `bfd1b66`
> （7.2 的版本号后来又被 `a66c027` 推到 1.8.0）；**7.6** 随推送一并结案（`origin/main`
> 现在与 HEAD 同一提交，本地远程跟踪引用）；**7.7** 是它自己标明的"历史快照，不是缺陷"。
> 所以下表只剩记录价值，**不再是欠账**。本节 §8 里仍然开着的几条（§8.2 真人观感、
> §8.5 远端 CI、§8.6 逐句复核）在 [OPEN_WORK](OPEN_WORK.md) 文首的清单里各有一行（H3 / W8 / W10）。

按"**现在时**的说法与实测不符"排序。历史数字（各版本自己的"用例 N → M"、发布记录、
CI 运行记录）**按规矩不动**，不在下表里。

| # | 位置 | 文档写的 | 实测（2026-10-03，`HEAD = 172f386`） | 说明 |
|---|---|---|---|---|
| 7.1 | [ARCHITECTURE](ARCHITECTURE.md) 第 267~268 行 | "自带 **200 行**迷你运行器……共 **210 个用例**" | `engine/tests/MiniTest.cs` **423 行**；用例 **493**（在 §4.1 那棵树上）、工作区已到 **504** | 本文**没能确定它对应哪个版本**：同一份仓库里能查到的更晚数字是 396（阶段 5 后）/ 404 / 435 / 441，210 比它们都早——这一节从那以后就没再回填 |
| 7.2 | [STATUS](../../STATUS.md) 第 3 / 19 / 28 行 | "工作树版本 **1.6.0**（未提交 / 未打 tag）" | `Directory.Build.props` 第 54 行是 **1.7.0**，[CHANGELOG](../../CHANGELOG.md) 第 10 行已有 1.7.0 条目 | 1.7.0 那次提交（`39503d3`）改了 README / engine/README / VERSIONING / OPEN_WORK，**漏了 `STATUS.md`** |
| 7.3 | [README](../../README.md) 第 424 行、[STATUS](../../STATUS.md) 第 146 与 182 行、[RELEASING](RELEASING.md) 第 26 行 | `api-test.ps1` **44 项**（现在时） | **51 项**（§4.1 实测）；[VERSIONING](VERSIONING.md) 第 207 行与 [OPEN_WORK](OPEN_WORK.md) 第 813 行**已经**改成 51 | 同一件事有三套说法（44 / 48 / 51）。`48` 那一套在 [OPEN_WORK](OPEN_WORK.md) 第 885 行与 [CHANGELOG](../../CHANGELOG.md) 第 127 行，**前者是上一轮的快照、后者是 1.6.0 的历史**，都该原样留着 |
| 7.4 | [README](../../README.md) 第 357 行 | "`PublicApi.txt` —— **1930 行**" | **1933 行**（1.6.0 时是 1931） | `1930` 是 1.4.0 的值（[RELEASING](RELEASING.md) §5.1 记着），经 1.6.0 / 1.7.0 两次都没回填 |
| 7.5 | [games/README](../../games/README.md) 第 52 行 | "`WebSnapshotProtocolTests.cs`（**10 条**……）" | **11 条**（1.7.0 新增 `FrontendContract_FieldNamesAndTheModeToken`） | 小漂移 |
| 7.6 | [OPEN_WORK](OPEN_WORK.md) §0.3 第 1 条 | "推 **2 个**提交" | 写作时 `origin/main..HEAD` = **4**（`13142d4`、`39503d3`、`172f386` + `f9044ec`） | 这一条是**待办**，数字随提交增长，属正常过期 |
| 7.7 | [WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) §0.1 | "`main`（HEAD = `cd28dd4`）……『可展开的建筑故事框』**`main` 上没有**" | `HEAD` 是 `172f386`，故事框是 `f9044ec`（**已在 main 上**） | **已在文件内就地补记**（头部 ⚠️ 与 §0.2.1 指向 `f9044ec`），因此**不是缺陷**，列在这里只为说明它的 §0 是历史快照 |

---

## 8. 本文**没能**验证的（诚实边界）

1. **"泄漏约 320 个 `dotnet` 进程并耗尽内存"**：**先前写成"查不到记录"，那是错的**——
   记录在 [AGENT_ARCHIVE](AGENT_ARCHIVE.md) 第 19 / 24 / 39 行（清理者释放了 9.9 GB）。
   我第一次搜的时候它还没被提交（那份文档在本会话中途才以 `c902e1f` 落地），
   我随后按事实更正了 §3.4。**保留这条更正记录本身**，因为它正是这个仓库
   "先读依赖再下结论 / 数字要连机器状态一起报"那条规矩的又一个实例。
   仍未验证的是**成因**：归档自己写明那 320 个进程与并发构建"只有时间相关性、没有解释"。
2. **真人观感与真实浏览器**：本机结构性起不了 Chrome / Edge，所以
   "📖 展开后的间距""sheet 收起后一直不答的手感""对比度改完好不好看"**都没有人看过**
   （[OPEN_WORK](OPEN_WORK.md) 第 133~135 行、[WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) 第 482~483 行）。
3. **第六轮（`$tables`）的当前工作区是否全绿**：我只测了 §4.1 那棵树；
   写本文时那五个文件还是未提交状态，我**没有**对它跑构建。
   **后来补上了**：那一轮已落地（`3312715`），并且在 `b625dd5` 上复跑 `-Strict` 全绿
   （见 §4 顶部那段与 [OPEN_WORK](OPEN_WORK.md) §0.12）——但**那不是本文测的**，记清楚归属。
4. **`tools/api-test.ps1` 源码里有 52 处 `Check` 调用，运行器报 51 项**：
   写本文时我没有逐处追是哪一处条件未执行（猜"可能是分支/循环里的一处"），**故只引用运行器自己的计数**。
   **后来查清了**（`f27a2e7`）：**3 处调用点从来没执行过**（`if ($clickError)` 的一个面、
   `if ($laterDeltas…)` 的 `else` 里两处），同时 **1 处**写在 `foreach` 里跑了 3 次——
   一多一少正好相抵。脚本现在**收尾自己审计**这件事（机制见 `tools/api-test.ps1` 头部 ⑤、
   [OPEN_WORK](OPEN_WORK.md) §0.12.3）。
5. **CI 的远端表现**：本轮没有联网核；写作时 `origin/main = cd28dd4` 而 `HEAD = 172f386`，
   远端自然还没有这四个提交的运行记录。
6. **`STATUS.md` / `README.md` 的每一句"状态"**：我只核对了上表那几处数字与版本，
   **没有逐句复核**（那需要按它的"事实"部分逐条跑一遍）。

---

> **最后一句给下一个人**：这个仓库里最值钱的东西不是代码，是**"什么被验证过、怎么验证的"**。
> 写任何结论时都问一句：**这句话我能拿哪条命令、哪个文件、哪个提交去核对？**
> 答不上来的，就照这个仓库的规矩写"未验证"——它不会因此显得弱，
> 它会因此**活得更久**。
