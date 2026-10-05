# 版本与兼容性承诺

> 这份文档回答一个问题：**我能不能依赖 `NekoClicker.Core`，以及升级时会不会突然编不过。**
>
> 一句话版本：**从 1.0.0 起，公开 API 只增不改。** 删改任何公开成员都必须升主版本号，
> 而且这条规矩不是靠自觉——有测试守着。
>
> 当前版本 **1.10.2**：**patch——公开 API 一行没动**（依据下面 §2 那张表）。
> 这一版把 `[未发布]` 桶结掉了，内容全在**前端 / 测试 / 工具链 / 文档**里：
> 三轮界面改动、三条新守卫（用例数漂移、`cpsEach` 的值、横扫全部包的「拥有 ⇒ 已解锁」）、
> 五处机械项（端到端探针收尾分层、打包产物自检进脚本、NuGet 元数据、文档计数与一次逐句复核）、
> 两份测量与验证记录，外加一个新的 Linux CI 作业。
> 公开表面没动的证据是快照本身：**重生成之前**，树上的 `engine/core/PublicApi.txt` 与 `v1.10.1`
> 是**同一个 blob**（`4d67559d…`）；重生成之后 `git diff v1.10.1` 的 `--numstat` 是 **`1 1`**
> ——只差首行 `version=`，**2026 行**一行不多一行不少。
> 细节见 [CHANGELOG](../../CHANGELOG.md) 的 1.10.2。
>
> 上一版 **1.10.1**：patch——同样一行公开 API 没动，把 1.10.0 的导出/导入接到两个宿主上
> （Web 的「存档」窗口、终端宿主的 `E` / `I` 路径提示），外加十一个包的「点击 × 建筑」桥、
> 两条宿主修复与一条工具链守卫。
>
> 再上一版 **1.10.0**：minor——公开 API **只增不改**：新增 `SaveTransfer` 类型、
> `SaveTransferKind` 枚举、`SaveTransferResult` 记录，以及 `SaveManager` 上的
> `PackId` 属性、`Export()` / `Import(string)` 两个方法。
> **它同时是一处与"版本"有关的事实澄清**：那次新增的 `SaveTransfer.FormatVersion`
> 是**信封自己**的格式版本，与存档格式版本（`SaveSerializer.CurrentVersion`，仍然 `1`）
> 是两条独立的轴；**存档格式一个字都没改、没有新增任何 `ISaveMigration`**，
> 所以 §6 第 3 条（存档兼容由迁移机制负责）在那次改动里**没有被动过**。
> 细节与判别力见 [CHANGELOG](../../CHANGELOG.md) 的 1.10.0、
> [SAVE_TRANSFER_PLAN](SAVE_TRANSFER_PLAN.md)。
>
> 上一版 **1.9.0**：minor——公开 API **只增不改**：新增 `EraStage` / `EraStageGate` 两个类型、
> `EraDefinition.Stages`、`EraSystem.Stage(GameEngine)` / `CheckStage(GameEngine)` /
> `StageCounterPrefix`、`GameEngine.CheckEraStage()`，以及 `EraView` 上阶段那六个只读字段。
> 它是这张表里最标准的一格：**给"一层之内的第几段"一个公开的形状**，而阶段本身是
> **派生**的（由单调指标算出来，不占存档位、不动存档格式）。老成员的语义
> （`BuildingView.NextMilestoneAt` 指的是**单座建筑**的下一档）**一字未动**，
> 线上老字段一个都没改，所以没有一个消费者被打断。
> 细节与判别力见 [CHANGELOG](../../CHANGELOG.md) 的 1.9.0、
> [CONTENT_AUTHORING](CONTENT_AUTHORING.md) §11.2 与 [TUNING_ANALYSIS](TUNING_ANALYSIS.md) §4.10。
>
> 上一版 **1.8.0**：minor——公开 API **只增不改**：新增 `UpgradeCategories` 类型、
> `GameContent.UpgradesByBuilding` / `UpgradesForBuilding(string)`、`BuildingView.UpgradeIds`。
> 它不是语义例外，是这张表里最标准的一格：把 `UpgradeDefinition.Category` 上那条
> `"building:<id>"` 约定（"这条升级属于哪座建筑"）从"没人校验、没人读"变成
> **构建期校验 + 索引 + 快照字段**——老字段（`upgrades[].category` / `tier`、
> `buildings[].nextMilestoneAt`）**一个都没动**，所以没有一个消费者被打断。
> 细节与判别力见 [CHANGELOG](../../CHANGELOG.md) 的 1.8.0、
> [BUILDING_UPGRADES_PLAN](BUILDING_UPGRADES_PLAN.md) 与 [OPEN_WORK](OPEN_WORK.md) 的最新一节。
>
> 上一版 **1.7.0**：minor——公开 API **只增不改**：新增 `GameSnapshot.ModeName`（string）
> 与 `PurchaseModes.WireName()`。前端原本拿线上的枚举序数 `mode` 当档位名用
> （`(state.mode ?? "").toLowerCase()`），于是真页面上每次 `render()` 都在中段抛
> `TypeError`；修法是**给名字**而不是让前端解释序数（与 1.4.0 的 `UpgradeView` 同一形状），
> 数字形的 `mode` **原样留着**。见 [CHANGELOG](../../CHANGELOG.md) 的 1.7.0 与
> [OPEN_WORK](OPEN_WORK.md) 的 N 条。
>
> 上一版 **1.6.0**：minor——公开 API **一个成员都没有增删**（`MarkPendingChoicesShown()`
> 还在）。但它**同时是一处刻意的语义不兼容**：结局的落定条件从"玩家**被展示过**那批待答表态"
> 换成"玩家把它们**答完**"，于是"从不作答的玩家永远拿不到结局"这条代价的覆盖面更大了
> （1.5.0 起"不会永远悬着"就已经被有意移除，那会儿是"没看到"）。
> 按下面 §2 那张表，改语义本该是 major；这次按 1.1.0 / 1.5.0 的先例走 minor——
> **同一处、同一类改动的第三次**，例外同样记录在案。
> **1.5.0** 也是 minor（结局落定条件 定时 → "被展示过"这个条件）；
> **1.4.0** 也是 minor（`UpgradeView` 新增
> `UsesPrestigeCurrency` / `CurrencyName` / `CurrencyIcon`，也就是"这一行花哪种货币"）。
> **1.3.0** 也是 minor（新增 `GameSnapshot.Offline`、`OfflineView`、
> `GameEngine.PendingOfflineProgress` 与 `DismissOfflineProgress()`，离线收益第一次能走到界面上）；
> **1.2.1** 是 patch（421 条剧情散文搬出 dll，外加 `ContentText` 的一个并发修复），
> **1.2.0** 只新增了 `ContentText`（剧情散文的外部化载体），没有不兼容改动。
> 语义层面的破坏性变更至今有**三处，都是同一件事的三次决定**——
> 1.1.0 让结局条件成立后先等一段作答宽限期（`GameEngineOptions.EndingGraceSeconds`，
> 默认 30 模拟秒），期间 `CheckEnding()` 返回 `null`；1.5.0 把那段宽限换成"玩家被**展示过**"
> 这个条件；1.6.0 又把它收紧成"玩家把它们**答完**"。
> 详见 [CHANGELOG](../../CHANGELOG.md)。

---

## 1. 版本号指的是什么

版本号指 **`NekoClicker.Core` 对外承诺的公开 API 版本**，不是内容包、不是 Demo、
也不是"这个游戏"的版本。内容包与 Demo 跟着框架走，但它们自己不承诺 API 稳定性
（内容包之间本来就不许互相引用，见 ROADMAP 的 A1 / R7 不变量）。

单一事实来源是 **`Directory.Build.props` 里的 `<Version>`**：

```xml
<Version>1.10.2</Version>
<AssemblyVersion>1.10.2.0</AssemblyVersion>
<FileVersion>1.10.2.0</FileVersion>
```

这个值会同时成为程序集版本、文件版本，以及 `ApiVersion.Current` 报告的版本。
**不允许在别处再写一遍版本号。**

> **为什么手写而不是从 git tag 推导？**
> `tools/dnet.ps1` 的存在前提就是"本机可能没有网络、也可能没有 git 元数据"。
> 从 tag 推导会让构建在浅克隆、导出源码包、CI 无 `.git` 的环境里产出 `0.0.0` 或直接失败。
> 版本号是这个框架对使用者的承诺，不该取决于构建环境碰巧具备什么。

宿主可以在运行时读到自己依赖的版本：

```csharp
using NekoClicker.Core;

Console.WriteLine(ApiVersion.Current);        // "1.10.2"
Console.WriteLine(ApiVersion.AssemblyVersion); // 1.10.2.0
Console.WriteLine(ApiVersion.Major);           // 1
```

建议把它写进你那份存档或日志——"用户报的 bug 出在哪个框架版本上"就不需要靠猜。

---

## 2. 什么改动升哪一位

| 改动 | 升哪一位 | 例子 |
|---|---|---|
| **公开 API 只增不改** | **minor** | 新增一个 `GameEngine` 方法、新增一个 `UnlockCondition` 子类、新增一个可选参数（带默认值） |
| **公开 API 有不兼容改动** | **major** | 删除 / 重命名公开成员、改参数类型或顺序、收紧可空标注、改枚举成员的值、改变已有成员的语义 |
| **公开 API 一行没动** | **patch** | 内容包数值调整、文案、注释、修 bug（不改变公开签名）、性能优化 |

"公开 API" = `NekoClicker.Core.dll` 里所有 `public` / `protected` 类型与成员。
**internal 成员随便改**，没人看得见。

### 已知的三次例外：1.1.0 / 1.5.0 / 1.6.0

**1.1.0 改了行为语义，却按 minor 发布。** 它让 `CheckEnding()` 在结局条件成立后先等一段
宽限期（默认 30 模拟秒），期间返回 `null`——按上表属于"改变已有成员的语义"，本该 major。
**1.5.0 是同一个位置的第二次改动**：它把那段定时宽限换成条件（玩家**被展示过**那批待答表态
之后才允许落定，`GameEngine.MarkPendingChoicesShown()`），同样按 minor 发布。
**1.6.0 是第三次**：条件又从"被展示过"收紧成"被**答完**"——
`MarkPendingChoicesShown()` **保留但不再参与判定**（它的文档、两个宿主的注释与命令回复
都改成了"只是诊断信号"；引擎不再读它）。
这次连"看过就有收场"也不成立了：**从不作答的玩家永远拿不到结局。**

这三次都是**有意**的判断：判定结果不变（仍是按 `Priority` 取第一个条件成立者），
受影响的只是结果出现的时刻（1.1.0 是"推迟一段固定时间"，1.5.0 是"推迟到玩家看过"，
1.6.0 是"推迟到玩家答完"）。写在这里**不是给它开先例，而是相反**——例外要被记录；
否则下次就分不清"决定"和"疏忽"，而一旦分不清，这张表就退化成橡皮图章。
理由与影响面同时记在 [CHANGELOG](../../CHANGELOG.md)，决定记录见
[OPEN_WORK](OPEN_WORK.md) 的 J 与 K 两条。

> **下一次再动这里，就该考虑 major 了。** 同一个位置连着三次用 minor 记录例外，
> 本身就是一个信号：要么这张表太高（"改落定时机"其实是这个框架承诺里的一部分），
> 要么这段语义该先被设计稳定下来。这次仍然按先例走 minor，是因为改动**只**发生在
> 落定时机上、公开表面一字未动；但这条理由不会一直成立。

### 容易漏掉的两类"不兼容"

1. **收紧可空标注**。把 `string? Snapshot()` 改成 `string Snapshot()`，
   调用方的 `?? fallback` 与 `x?.Y` 会立刻开始报"不必要的"警告；
   反过来放松是安全的。快照会把 `?` / `!` 记下来，所以这类改动同样会红。
2. **改变已有行为的语义**。签名一模一样但意思变了——比如某个方法原来返回"本轮累计"、
   现在返回"历史累计"。**快照守卫抓不到这类改动**（字面没变），
   只能靠 CHANGELOG 写清楚 + 主版本号。这是本机制最诚实的边界。

### 关于内容包

内容包是"用公开 API 写出来的东西"，所以框架的不兼容改动会**直接打断十一个内容包**。
这不是巧合，是本项目的核心主张（"换内容包即换游戏"）的代价与证据：
正因为它们真的只依赖公开 API，任何核心改动的破坏半径才是可测量的。

---

## 3. 执法机制：公开 API 快照

光有规矩没有守卫，规矩会在第一次赶工时失效。所以有一份**快照**：

```
engine/core/PublicApi.txt     ← 公开表面的逐项清单（行数随公开成员增减：1.8.0 加了
                                UpgradeCategories / UpgradesForBuilding / UpgradeIds；
                                1.9.0 又加了 EraStage / EraStageGate / EraDefinition.Stages /
                                EraSystem.Stage 一族 + EraView 的阶段六字段；
                                1.10.0 加了 SaveTransfer / SaveTransferKind /
                                SaveTransferResult + SaveManager 的三个成员，
                                所以现在是 2026 行；1.10.1 与 1.10.2 都是"一行没动"的 patch，
                                所以仍然是 2026 行）
```

它被**嵌进 `NekoClicker.Core.dll`**，随 dll 一起走。任何拿到这个 dll 的宿主都能断言
"这份二进制的公开 API 与我预期的一致"，不需要把本仓库的测试代码也带走。

快照长这样（节选）：

```
# NekoClicker 公开 API 快照 format=1 assembly=NekoClicker.Core version=1.0.0

public sealed class NekoClicker.Core.GameEngine
  ctor NekoClicker.Core.GameEngine(NekoClicker.Core.Content.GameContent! content, NekoClicker.Core.GameEngineOptions? options = null)
  method NekoClicker.Core.ClickResult! Click()
  method System.Double Update(System.Double deltaSeconds)
  prop System.Double CookiesPerSecond { get; }
```

约定：

- `!` = 不可空，`?` = 可空，什么都不写 = 该位置没有可空信息
- 成员按行文本排序，**顺序不参与比较**（改一行注释不会让守卫红）
- 编译器合成成员（record 的 `<Clone>$` 之类）不进快照；但 record 的
  `Equals` / `op_Equality` / `Deconstruct` **保留**——它们跟着属性集合变化，
  跳过了反而会漏掉"给 record 加了个属性"这种真实变化

四条守卫（`PublicApiTests` + `VersionTests`）：

| 用例 | 守什么 |
|---|---|
| `PublicApi_MatchesTheCommittedSnapshot` | 公开表面与快照逐项一致 |
| `PublicApi_CoversEveryExportedTypeAndEveryPublicMember` | 快照确实覆盖了每个公开类型与成员——防止守卫**自己瞎掉**（两边都少 = 假绿） |
| `PublicApi_SnapshotRecordsTheCurrentVersion` | 快照记录的版本 = 当前版本。**这是"改 API 必须同时升版本"的执法点** |
| `PublicApiGuard_RejectsEveryKindOfBreakingChange` | 故障注入：证明守卫对五类改动真的会红 |
| `VersionTests.Changelog_HasAnEntryForTheCurrentVersion` | CHANGELOG 里有当前版本的带日期条目 |

> **为什么"判别力"要单独有一条用例**：一条永远不报警的守卫，与一条正确的守卫，
> 在测试输出里长得一模一样。本项目从阶段 2.7 起就把"用故障注入证明守卫会红"当成惯例
> （`RevealConditions_AreUniqueWithinEachPack`、`MonotonicGuard_RejectsADecayingCounterInCompletion`
> 都是这么验的）——这次照办。

---

## 4. 改了公开 API 之后怎么走

**顺序很重要。先重新生成快照会让守卫退化成橡皮图章。**

```
① 改代码
② 决定升 minor 还是 major（对照 §2 那张表）
③ 改 Directory.Build.props 的 <Version> 与 <AssemblyVersion> / <FileVersion>
④ 在 CHANGELOG.md 顶部加一条：## [新版本] - YYYY-MM-DD
⑤ 跑 pwsh -File tools/public-api.ps1 更新快照
⑥ pwsh -File tools/build.ps1 -Strict 确认全绿
⑦ git commit + git tag v<版本>
```

`tools/public-api.ps1` 只做第 ⑤ 步。它不会替你改版本号——那正是它刻意不做的事。

守卫红的时候，它想问的是「**你知道自己在破坏兼容性吗**」，
而不是「要我帮你把红变绿吗」。先跑脚本再补版本号，等于跳过了 ②③④ 三个真正重要的步骤，
留下来的只是一个更好看的绿。

### 只在第 ⑦ 步做

```powershell
git tag -a v1.0.0 -m "NekoClicker.Core 1.0.0"
```

---

## 5. 发布检查清单

> 一步步的实际操作（含打包、推送、CI 观测，以及 1.2.1 的真实执行记录）见
> [RELEASING](RELEASING.md)。下面是判据清单。

- [ ] `pwsh -File tools/build.ps1 -Strict` 退出码 0，0 警告
- [ ] 全部用例通过（当前 **583** 个；2026-10-05 实测——这个数现在由 `TestCountDriftTests` 守着：加了用例却没改这一行，闸门会点名报红，见 OPEN_WORK 的 W1）
- [ ] 若这次动了 Web 宿主：`pwsh -File tools/api-test.ps1` 全部端到端检查通过（当前 65 项；脚本收尾自己核对"源码几处检查点 / 这次执行到几处"）
- [ ] 若这次动了 Web 前端：`node tools/web-smoke.mjs` 全绿（当前 **208** 条；S1 起它也在 `-Strict` 里跑）
- [ ] `Directory.Build.props` 的 `Version` / `AssemblyVersion` / `FileVersion` 三处一致
- [ ] `CHANGELOG.md` 有当前版本的带日期条目，写清了兼容性影响
- [ ] 若公开 API 有变动：快照已更新，且**确实**是有意为之
- [ ] 十一个内容包的行为没有被意外改变（做内容改动的那个提交要单独看）
- [ ] `ApiVersion.Current` 与实际 tag 一致
- [ ] 出可分发包：`pwsh -File tools/pack.ps1` → `artifacts/neko-clicker-<版本>-win-x64.zip`
- [ ] 打 tag：`git tag -a v<版本> -m "..."`
- [ ] 推送提交与 tag（本机 HTTPS 不通，走 SSH；见 [STATUS](../../STATUS.md) §7 第 10 条）
- [ ] 看一眼 CI 的两次运行（`.github/workflows/ci.yml`）

---

## 6. 诚实边界

**这套机制能保证什么**：签名层面不会静默破坏。删成员、改签名、收紧可空标注，
一定会在构建或测试阶段被拦下来，不管是谁改的、过了多久。

**它保证不了什么**：

1. **语义变化**。签名不变但意思变了，快照看不出来。只能靠 CHANGELOG 与人工评审。
2. **内容包级别的兼容**。快照守的是 `NekoClicker.Core.dll` 的表面。
   如果你依赖的是某个内容包的**具体数值**（比如"第 3 层门槛是 1e12"），那不在承诺范围内。
3. **存档格式的向后兼容**。存档兼容由 `ISaveMigration` 机制负责（见 `ARCHITECTURE.md`），
   与公开 API 版本是两套东西。**升级框架版本不会自动保证老存档能读**——
   这件事由每个 `ISaveMigration` 实现负责，改动存档结构时必须同时加迁移。
4. **运行时行为一致性**。同一份 API 在两个版本上可能给出不同的数值结果
   （那正是 patch 版本会做的事）。

写在这里不是免责，而是告诉你**哪些地方仍然需要你自己看一眼**。
