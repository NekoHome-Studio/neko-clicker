# 版本与兼容性承诺

> 这份文档回答一个问题：**我能不能依赖 `NekoClicker.Core`，以及升级时会不会突然编不过。**
>
> 一句话版本：**从 1.0.0 起，公开 API 只增不改。** 删改任何公开成员都必须升主版本号，
> 而且这条规矩不是靠自觉——有测试守着。

---

## 1. 版本号指的是什么

版本号指 **`NekoClicker.Core` 对外承诺的公开 API 版本**，不是内容包、不是 Demo、
也不是"这个游戏"的版本。内容包与 Demo 跟着框架走，但它们自己不承诺 API 稳定性
（内容包之间本来就不许互相引用，见 ROADMAP 的 A1 / R7 不变量）。

单一事实来源是 **`Directory.Build.props` 里的 `<Version>`**：

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
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

Console.WriteLine(ApiVersion.Current);        // "1.0.0"
Console.WriteLine(ApiVersion.AssemblyVersion); // 1.0.0.0
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
src/NekoClicker.Core/PublicApi.txt     ← 1897 行，公开表面的逐项清单
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

- [ ] `pwsh -File tools/build.ps1 -Strict` 退出码 0，0 警告
- [ ] 全部用例通过（当前 **404** 个）
- [ ] `Directory.Build.props` 的 `Version` / `AssemblyVersion` / `FileVersion` 三处一致
- [ ] `CHANGELOG.md` 有当前版本的带日期条目，写清了兼容性影响
- [ ] 若公开 API 有变动：快照已更新，且**确实**是有意为之
- [ ] 十一个内容包的行为没有被意外改变（做内容改动的那个提交要单独看）
- [ ] `ApiVersion.Current` 与实际 tag 一致
- [ ] 打 tag：`git tag -a v<版本> -m "..."`

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
