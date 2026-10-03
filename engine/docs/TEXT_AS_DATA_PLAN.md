# 把剧情做成可读文件：方案

> 状态：**已全部落地**（2026-10-01）。`ContentText` 已公开（1.2.0），
> **十个有剧情的包、共 421 条散文**全部搬进了各自的 `text.json` 并从文件读；
> 试点的运行期守卫已泛化成 `ContentTextFileTests`（覆盖全部已外部化的包）。
> 下面这份方案保留原样，作为"当时怎么定的、为什么这么定"的记录——
> 实际执行时有两处与方案不同，记在这里：
> ① 迁移是**一个包一个提交**做的，抽取用的判据不是"再写一个 C# 解析器"，
>    而是**迁移前后各 dump 一次运行期文字、逐字节比对**（176,708 字节完全相同）；
> ② `tools/extract-lore-text.ps1` 只覆盖工厂写法，对象初始化器写法的六个包由
>    另一个一次性扫描器处理——所以那个脚本的历史使命已经结束。
> 规模与风险是量出来的，不是估的。

---

## 1. 现状：剧情现在以什么形式存在

全部**编译进 dll 的 C# 字面量**。三类载体：

| 载体 | 规模 | 内容 |
|---|---|---|
| `Lore.cs`（10 个包） | 约 **420 条**（6 个包各 40 条；咖啡 51、九命 50、公司/实验室各 40 上下） | `Title` + `Body`（图鉴条目） |
| `Endings.cs`（9 个包） | 56 KB 源码 | 结局正文（终局那一大段） |
| `Eras.cs`（9 个包） | 82 KB 源码 | 每层的叙事文字 |
| `Choices.cs`（3 个包） | 29 KB 源码 | 表态的问句、选项标签、结果文本 |

**这是一份几百 KB 的中文散文集**，现在改一个字都要重编 + 走一遍测试与版本流程。
"可读文件"要解决的正是这个。

## 2. 这个改动真正的难点：id ↔ 文本会失去编译期检查

**不是"怎么读 JSON"。** 难点在这里：

现在 `LoreEntry` 是一个 C# 记录，`Id`/`Title`/`Body` 是同一个对象上的属性——
**编译器保证它们同生共死**。文本一旦搬到文件里，两者只剩"字符串相等"这一层关系：

- 文本文件里 id 打错一个字母 → 界面上是**空白**（或悄悄回退成 id 本身）
- 代码里删了一条、文件里没删 → **孤儿条目**，永远没人读
- 两条线共享同一个 id → 后一条**静默覆盖**前一条

这三件事**都不会让任何测试变红**——而本仓库的全部纪律（`ValidateReachability`、
单调性白名单、唯一性快照、结局-表态顺序校验）恰恰建立在"沉默失败要变成红色"上。
所以**外部化文本必然新增一整类沉默失败面**，方案的核心必须是把它们变红。

**好消息：仓库里已经有这个模式可以照抄。** `Scaling.Describe` / `UnlockCondition.Describe`
那条"键 → 中文名"的表就是同构的：内容是数据、键是字符串、**未登记的键有既定行为**
（回退成键本身，旧内容不崩），而且**有一条守卫在核对它**（写那条守卫时它立刻抓出了
九命包的「信仰」漏登记）。本方案照抄这套：回退行为 + 双向守卫。

## 3. 要定的三件事

### ① 范围：先做哪一类？

| 选项 | 工作量 | 说明 |
|---|---|---|
| **(a) 只做 `Lore.cs` 的 `Title`/`Body`（推荐）** | 中 | 剧情本体就是图鉴条目。纯散文、**不含逻辑**，最安全的第一步 |
| (b) (a) + `Endings` + `Eras` + `Choices` 文本 | 中大 | 覆盖全部"叙事"，但 `Choices` 里混着选项与立场 id，要小心切分 |
| (c) 全部面向玩家的文本 | 大 | 再加建筑/升级/成就/增益的 `Name`/`Description`——那是几千条，且很多与数值耦合 |

推荐 (a)：它把"剧情可读"这件事**完整解决**（剧情=图鉴），而把风险留在最小的地方。
做完 (a) 再决定要不要往下走，比一次全铺开安全得多。

### ② 形态：嵌入资源 还是 散文件？

用户要的是"可以读取的文件"，所以：

| 选项 | 好处 | 代价 |
|---|---|---|
| 嵌入资源 + `--dump-text` 导出 | 不丢文件、"一个目录就能玩" | 改文本要导出/重新嵌入，**不是真正的"可读文件"** |
| **(b) 散文件，随包发布（推荐）** | 直接能打开、能改、能给别人替换 | 丢文件=丢剧情，所以**必须有"文件缺失就报错"的检查**，不能静默空白 |

推荐 (b)：`content/<包名>/lore.json`，随 zip 一起发。源码树里放在内容包项目下，
用 csproj 的 `CopyToOutputDirectory` 复制到输出目录，publish 时自动带上（这一点
已经在 `wwwroot` 上验证过是同一条机制）。

### ③ 条件（`Reveal`）怎么办？

**留在代码里。** `Reveal` 是 `UnlockCondition` 条件树，用数据表达需要一个 DSL
+ 解析器 + 校验器，工作量是文本外部化的好几倍，而且它与"剧情可读"这个需求无关。
所以：**条件在 `Lore.cs`，散文在 `lore.json`，两边靠 id 关联**，id 的一致性由第 4 节的守卫保证。

## 4. 守卫（这个方案能不能成立，全看这一节）

1. **缺文本 → 构建期报错**，不是运行期空白。每个在 `Lore.cs` 里出现的 id 都必须在
   `lore.json` 里有一条，缺一条就抛，错误信息带上包名与 id。
2. **孤儿文本 → 构建期报错。** `lore.json` 里有、代码里没引用的条目也抛——
   否则删了剧情却留着文本，永远没人发现。
3. **id 唯一性**：沿用已有的"唯一性快照"套路，禁止重复。
4. **反例证明**：像其它守卫一样，**用一次真实的失败证明它会红**（照抄
   `PublicApiGuard_RejectsEveryKindOfBreakingChange` 的做法，喂三份坏文件：
   缺一条、多一条、id 打错一个字母）。

## 5. 落地步骤

```
① 在 core 加一个文本加载器（用 System.Text.Json，BCL 自带，**零新依赖**）
   —— 只是"读文件 + 建 id→文本 表 + 缺/多就抛"，不含任何内容 id
② 挑一个包试点：#3 实验室（40 条、4 条线，规模适中，且它有表态与结局，能顺带暴露接口问题）
③ 把它的 lore.json 写出来，跑 421 条回归，确认行为一字不变
④ 补齐第 4 节四条守卫
⑤ 逐包迁移（每包一个提交，便于回退与复核）
```

**为什么先做一个包**：接口错了，改一处；接口对了，剩下 9 个包是机械劳动。
一次性铺 10 个包，万一接口要改就是 10 倍返工——这条与仓库里"3C 拆成 3C-1/3C-2"
同一个理由。

## 6. 明确不做

- **不把条件也做成数据**（见 ③）。
- **不动数值**：价格、门槛、权重一律留在代码里。这次只搬散文。
- **不改存档**：文本不进存档，`CurrentVersion` 不动。
- **不改公开 API 的语义**：`LoreEntry` 的构造方式可能多一个入口，但已发布成员不改；
  若确认要加公开成员，按 `VERSIONING.md` 走 minor + 快照 + CHANGELOG。

## 7. 需要你定

1. **范围**：(a) 只做图鉴的 `Title`/`Body` —— 还是连 `Endings`/`Eras`/`Choices` 一起？
2. **形态**：散文件随包发布（推荐）—— 还是嵌入资源 + 导出命令？
3. **文件格式**：JSON（推荐，BCL 自带、与存档同款）—— 还是你更想要别的（YAML/Markdown
   都要么引依赖、要么自己写解析器，与"零第三方依赖"直接冲突）？

---

## 8. 执行记录（2026-10-01，实际是怎么做的）

> 这一节是**事后**写的：上面 1~7 节保持当时的判断不动，这里回答"实际执行时哪里一样、
> 哪里不一样、路上撞到了什么"。三处决定最后都按方案的推荐值落地：
> 范围 =(a) 只搬图鉴散文、形态 = 散文件随包发布、格式 = JSON。

### 8.1 交付

| 项 | 结果 |
|---|---|
| 范围 | 十个有剧情的包、**421 条**散文全部搬出 dll（示例包「猫咖物语」没有图鉴，不涉及） |
| 逐包条数 | 咖啡馆 51 / 九命 50 / 实验室 40 / 公司 40 / 末世 40 / 图书馆 40 / 神明 40 / 文明 40 / 赛博 40 / 梦境 40 |
| 文件 | `engine/content/<包名>/text.json`；随包复制到输出目录，测试 / Demo / Web / publish 四条路都实测带上 |
| 用例 | 433 → **435**（守卫从"只覆盖 Lab"泛化成覆盖十个包 +4；并发守卫 +1；旧的"逐字比对" −3） |
| 版本 | 不动公开 API；**已随 1.2.1 发布**（2026-10-01，patch） |

### 8.2 方法：把"真值"放在运行期，而不是再写一个 C# 解析器

方案 §5 写的是"机器抽取 + 逐字比对"。实际执行时换了判据的落点，更硬：

1. **迁移前**：一次性探针（`.tmp/text-dump`）把每个包的 `Storylines` 与 `LoreEntries`
   按 id dump 成 JSON（`.tmp/text-baseline.json`）。这是**引擎真正读到的值**，不是源码文本。
2. **抽取**：`text.json` 直接由这份 dump 生成——于是"文件里的散文"与"代码里的散文"
   按定义就相同。
3. **改写源码**：一个小扫描器（尊重字符串字面量与括号嵌套）把字面量换成 `Prose.Text(...)`；
   每删一处都断言"它 == dump 里对应的值"，任一处不符即整体中止、不写任何文件。
4. **迁移后**：再 dump 一次，与基线**逐字节比对**：**176,708 字节完全相同**
   （含剧情线名称/主题/图标，以及每条散文的标题与正文）。

这样做省掉了"再实现一遍 C# 字符串字面量解析"（跨行 `+` 拼接、括号嵌套、逗号出现在
字符串里都得处理），而且判据落在**行为**上，不是落在"两份文本看起来一样"上。

十个包有两种写法，扫描器都覆盖到：

| 写法 | 包 | 形态 |
|---|---|---|
| 工厂 | 咖啡馆 / 九命 / 公司 | `Popup\|Log\|Codex("id", order, "title", "body", reveal)`；正文可跨行拼接、`reveal` 可以是多行表达式 |
| 对象初始化器 | 末世 / 图书馆 / 神明 / 文明 / 赛博 / 梦境 | `new() { Id = …, Title = "…", …, Body = "…" + "…", … }`（Title 与 Body 不相邻） |

### 8.3 与方案不同的两处

1. **`tools/extract-lore-text.ps1` 只覆盖工厂写法**：对象初始化器写法的六个包由另一个
   一次性扫描器处理。那个脚本现在已经**没有运行期职责**（文件头加了 STATUS 说明），
   保留它只为让迁移过程可复核。
2. **复制规则的落点变了**：方案建议"每个包的 csproj 标 `CopyToOutputDirectory`"，
   试点时也确实只写在 Lab 一个 csproj 里；铺到十个包时才看出这是**通用能力**，
   于是上移到仓库根的 `Directory.Build.props`——内容包目录里有 `text.json` 就自动复制成
   `content/<目录名>/text.json`。新增一个包不必再手工加一条（漏加会在启动时抛，响，
   但没必要给人留一个能忘的步骤）。

### 8.4 守卫：泛化，以及路上抓到的那个并发缺陷

**泛化**：试点期的 `LabTextFileTests` 换成 `ContentTextFileTests`，同一组断言横扫每一个
已外部化的包（复制到位、代码文本 == 文件文本、把文件改坏会响、原样读一遍不抛），
并加一条**覆盖度**用例：`engine/content/*/text.json` 有几个，守卫表里就得有几个。

**缺陷**（这个才是执行记录里最该记的部分）：铺完之后第一次全量测试红了 17 条，
**再跑一次却全绿**——仓库最忌讳的"只在并行下红"。根因：

- 一个包只持有**一份** `ContentText`（静态懒加载），而 `Build()` 在同一进程里有多个入口：
  测试的 `ArchitectureTests`、`TestGame` 的缓存、Demo 的 `ContentPackages` 各建一次；
- 于是"哪些 id 已取过"那张 `HashSet` 被并发写坏。`ContentText` 正是 1.2.0 新公开的 API，
  也就是说发布出去的是一个"用了就可能炸"的类型；
- 复现：竞态探针（`.tmp/race-probe`）40 线程 × 30 轮 → **1200 次构建里 522 次抛异常**。

修法与守卫：所有对那张表的读写走一把锁（不改公开签名），守卫
`ContentTextTests.ConcurrentReaders_DoNotCorruptTheUsedSet`。
**守卫的第一版是橡皮图章**：只用了两个键，撞不出 `HashSet` 的扩容竞态，
故障注入（把锁去掉）下照样绿；改成 200 条 × 16 线程 × 30 轮之后才真的红——
这条纪律与 `PublicApiTests` 的故障注入是同一条：**守卫也要被证明会红**。

### 8.5 现在的状态

- 迁移与并发修复**已随 1.2.1 发布**（2026-10-01，patch——公开 API 一行没动）；
  `v1.2.0` 的 tag 不动（它标记的是当时的发布状态，缺陷随 1.2.1 修掉）。
  发布过程与实际执行记录见 [RELEASING](RELEASING.md)。
- 图鉴散文的**写作形态**已经写进 [CONTENT_AUTHORING](CONTENT_AUTHORING.md) §12.0，
  换皮手册 [STAGE_5_RESKINS](../../games/docs/STAGE_5_RESKINS.md) 顶部加了指向它的补记。
- 迁移用的一次性工具（dump 探针、迁移扫描器、竞态探针）都在 `.tmp/`（已 gitignore），
  它们的作用是"让这次搬迁可复核"，不是运行期的一部分。

---

## 9. 执行记录：建筑文案（第二轮，实际是怎么做的）

> 图鉴散文之后的第二类。范围：**全部 11 个包、104 座建筑**的
> `BuildingDefinition.Name` / `Description`。
> 数量与任务书里"93 座、NineLives 1 座"的估算不一致——实测 NineLives **12** 座、
> Neko / Cafe 各 10 座、其余八个包各 9 座；按包量出来的数字记在这里，不按估算走。

### 9.1 形态

`content/<包>/text.json` 新增 `buildings` 分区，`id → { name, description }`。
`Icon` / `BasePrice` / `BaseCps` / `PriceGrowth` / `Unlock` / `Tags` **留在代码里**：
图标是符号而不是散文，其余是逻辑——与方案 §3③ 同一条理由（能进数据的只有散文）。

写法（十个对象初始化器的包，以及九命的工厂写法都一样）：

```csharp
Name = Prose.Text("buildings", "incubator", "name"),
Description = Prose.Text("buildings", "incubator", "description"),
```

`Prose` 就是 `Lore.Prose`（`private` 改 `internal`）：**每个包仍然只持有一份 `ContentText` 实例**。
必须共用——`EnsureNoOrphans` 遍历整份文件的每个 kind，而"哪些 id 已取用"是按实例记的，
两份实例各记一半，就会把对方的条目全报成孤儿。示例包「猫咖物语」没有图鉴，
所以它自己持有那一份（`Buildings.ProseCache`），并在 `NekoContent.Build()` 末尾查孤儿。

### 9.2 方法（与图鉴那次同一条：判据落在运行期，不是再写一个解析器）

1. 迁移前用一次性探针把 11 个包的运行期建筑表 dump 成 `.tmp/buildings-baseline.txt`
   （`pack` / `id` / `name` / `description`）；
2. `text.json` 由这份 dump 生成——文件名与文案都只当**文件内容**处理，脚本里一行中文字面量都没有；
3. 改 `Buildings.cs` 的扫描器**每删一个字面量都先断言它 == dump 里对应的值**，任一处不符即整体中止；
   Cyber 那 9 条跨行 `+` 拼接的说明也一并覆盖（合起来与 dump 逐字相同）；
4. 迁移后再 dump 一次逐字节比对：**13,567 字节完全相同**
   （SHA-256 `BB40F11C302A9F4593671EE5B1F0126F035C0AFC54983D4F59F900C8CE9A2F19`），104 座全部在内。

### 9.3 守卫（`ContentTextFileTests`，7 条）

- `ExpectedBuildings`：**写死每个包的建筑条数**。它管的是"两边同时少一座"——
  代码与文件一致、但都少了一座时，只有它会红（曲线回归只关心价格与产量）。
- `EveryBuilding_ResolvesItsTextFromTheFile`：代码 ↔ 文件**两个方向** + 名字/说明非空。
- `EditingTheBuildingTextWrongly_FailsLoudly`：缺一条（读它的那一刻抛，点名 id）、
  多一条（孤儿检查点名）两种坏文件都要响。
- `BuildingCountTable_CoversExactlyTheGuardTable`：新增包时"忘了登记期望条数"不沉默。
- 外加上一轮就有的：有 `text.json` 的包 == 守卫表、复制到输出目录且与仓库逐字节相同、
  图鉴条目双向逐字。示例包没有 lore，所以这些断言改成"分区缺失 ⇒ 代码里也必须没有"。

**反例证明**（与 `PublicApiGuard_RejectsEveryKindOfBreakingChange` 同一条纪律）：

| 故意改坏 | 红在哪 | 原文 |
|---|---|---|
| 真文件里删掉 Lab 的 `incubator` | 整个包 `Build()` 当场抛 | `Lab/text.json：buildings 里没有 id「incubator」。` |
| 真文件里加一条 `zz_orphan_building` | 孤儿检查 | `内容包「Lab」的剧情文本里有 1 条没人取用（孤儿条目）：buildings/zz_orphan_building。` |
| 期望条数 9 改成 8 | 条数守卫 | `Lab: 代码里的建筑数与期望值对不上。｜期望 <8>，实际 <9>。` |

三处都还原，还原后 7 条全绿。

### 9.4 还没做

"面向玩家的文案"这一类里，剩下的仍是代码里的字面量：`Upgrades.cs`（≈444 处）、
`Buffs.cs`（≈212）、`Eras.cs`（≈196）、`Endings.cs`（58）、`Choices.cs`（36）、
`Achievements.cs`（36）、`Stances.cs`（33）。**建筑之外一个都没动。**
写作形态还没有补进 [CONTENT_AUTHORING](CONTENT_AUTHORING.md) §12.0（图鉴那次补了）。

> **第三轮补记（2026-10-03）**：上面这张清单里的 `Eras.cs` **已经做掉**，
> 建筑**刻意跳过**的 `Icon` 也补上了（`OPEN_WORK.md` 的 L 条因此结案）——
> 见下面的 §10。建筑与纪元的写作形态已补进 `CONTENT_AUTHORING.md` §12.0.1 / §12.0.2。

---

## 10. 执行记录：建筑的图标 + 纪元文案（第三轮，实际是怎么做的）

> 本轮做的是上面 §9.4 那张清单的**前两项**：
> ① 补上第二轮**刻意跳过**的建筑 `Icon`（11 个包、104 座）；
> ② `EraDefinition` 的**六个面向玩家的字符串**（9 个包、49 层，≈196 处字面量）。
> 两者都**没有**改 `engine/core`、没有动存档、没有升版本：`BuildingDefinition` 与
> `EraDefinition` 两个记录**一行未改**，改的只是那些字符串**从哪里来**。

### 10.1 交付与形状

| 项 | 结果 |
|---|---|
| 建筑图标 | **11 个包、104 座** → `buildings.<建筑 id>.icon` |
| 纪元文案 | **9 个包、49 层** → `eras.<纪元 id>.{name, theme, icon, entryText, exitText, completionHint}` |
| 逐包层数 | 九命 9 / 实验室 7 / 公司 3 / 末世·图书馆·神明·文明·赛博·梦境 **各 5**；`Neko` 与 `Cafe` **没有纪元**（连 `Eras.cs` 都没有，文件里也**不许有** `eras` 分区） |
| 代码侧 | `Icon = Prose.Text("buildings", id, "icon")`；纪元六项一律 `Prose.Text("eras", <id>, "<field>")` |
| 文件 | 同一份 `content/<包>/text.json`，`eras` 是新加的根分区（追加在末尾，EOL 跟随原文件——这批文件里 LF / CRLF 两种都有，脚本按各自的原样保留） |
| 用例 | **469 → 472**（`ContentTextFileTests` 7 → 10；2026-10-03 09:34 跑 `tools/build.ps1 -Strict`：两个 sln 0 警告、退出码 0） |
| 版本 | **不动**（公开 API 一行未改，无 minor 可升） |

**什么留在代码里、为什么**——这一栏是判断，不是机械搬运：

| 字段 | 处置 | 为什么 |
|---|---|---|
| `EraDefinition.Index` | 留 | 层号是"必须从 1 连续"那条构建期校验的依据，是顺序而不是文案 |
| `EraDefinition.Id` | 留 | 存档键与叙事引用键（与 `Buildings` / `Lore` 同一条划法） |
| `EraDefinition.Completion` | 留 | `UnlockCondition` 条件树 = 逻辑（方案 §3③：条件是 DSL，不在本轮范围内） |
| `EraDefinition.Balance` / `Modifiers` / `MetaRewardMultiplier` | 留 | 数值与规则 |
| `EraDefinition.InheritBuildingRatio` / `InheritBuildings` / `UnlocksBuildings` / `UnlocksUpgrades` | 留 | 比例、白名单、**id 清单**（id 不是散文） |
| `Name` / `Theme` / `Icon` / `EntryText` / `ExitText` / `CompletionHint` | **搬** | 六个都是给玩家读的字符串 |
| `BuildingDefinition.Icon` | **搬** | 同上（本轮之前它是唯一一个"玩家看得见却留在代码里"的例外） |
| `BasePrice` / `BaseCps` / `PriceGrowth` / `Unlock` / `Category` / `Tags` / `HiddenUntilUnlocked` / `SellRefundRate` | 留 | 逻辑与数值 |

**`CompletionHint` 值得单独说**：它在 C# 里的默认值是**空串**，而空串 ⇒ 界面**静默回退**成
条件树的自动描述。也就是说它是六个字段里**唯一自带静默降级路径**的那个。
所以搬的时候用的是 `Text(...)`（缺失即抛）而**不是** `TextOr`，守卫里还额外断言六项**都非空**
（"没写提示"从"悄悄换一套文案"变成"当场抛"）。

### 10.2 方法：还是"判据落在运行期"

与 §8.2 / §9.2 同一条，没有另写 C# 字符串解析器：

1. **迁移前**：一次性探针 `.tmp/EraDump`（独立小工程，不在 sln 里）读**运行期**的
   `GameContent.Eras` 与 `GameContent.Buildings`，dump 成两份 JSON：
   `.tmp/baseline-before/eras-baseline.json`（**45,621 字节**，
   SHA-256 `A6D8D08895194F7CBBA05302E928190A50CEBE5E6F4892206DDC12D3FEC4BEA7`）与
   `building-icons-baseline.json`（**7,895 字节**，
   SHA-256 `FB7973D8500C864A6886D1B7A331CEF682604ABBCF4A1FB1DBCDFF1AB3E3E0B2`）。
2. **生成**：`text.json` 的两个分区**由这两份 dump 生成**——于是"文件里的文案"与
   "引擎读到的文案"按定义就是同一批字符串；生成脚本每写一个包就**读回来逐字段核对**一遍。
3. **改写源码**：扫描器把字面量换成 `Prose.Text(...)`；**每删一处都先断言
   "它 == dump 里对应的值"**，任一处不符即整体中止、不写任何文件。
   纪元那边要处理跨行 `+` 拼接（神明 / 赛博 / 文明 / 梦境 / 图书馆 / 末世都有）。
4. **迁移后**：同一个探针再跑一次，两份 dump 与新产物**逐字节比对**：都是**完全相同**
   （`identical=True`，字节数与 SHA-256 都没变）。这是本轮最硬的那条证据。

**先把一个包走完再铺开**：试点是**实验室**（7 层纪元 + 9 座建筑 + 有图鉴，三类都在），
试点后 dump 就已经逐字节相同；随后才铺其余 8 个纪元包与 11 个包的图标。
（中途踩了一次：`git checkout` 还原"故意改坏"的文件时把试点包的迁移一起撤掉了——
用 `.tmp` 里的快照还原，别用 `git checkout` 还原一个已经迁移过的文件。）

### 10.3 守卫（`ContentTextFileTests`，7 → **10** 条）

| 新增/扩写 | 守什么 |
|---|---|
| **新增** `ExpectedEras` + `EraCountTable_CoversExactlyTheGuardTable` | **写死每包层数**（两张表都是 11 个包，没有纪元的两个写 **0**）。这是唯一能发现"代码与文件**同时**少一层"的守卫；对应性用例保证新增包**不可能忘记登记** |
| **新增** `EveryEra_ResolvesItsTextFromTheFile` | 代码 ↔ 文件**两个方向** × 六个字段逐字比对 + 六项非空；**期望 0 的包还要求文件里没有 `eras` 分区** |
| **新增** `EditingTheEraTextWrongly_FailsLoudly` | 少一层（取它那刻抛、点名 id）/ 多一层（孤儿检查点名）两种坏文件都要响 |
| **扩写** `EveryBuilding_ResolvesItsTextFromTheFile` | 从两个字段扩到 **name / description / icon** |
| **扩写** `ReadAll` / `ReadAllExcept` / `ReadBuildings` + 新的 `ReadEras` | 夹具读法必须覆盖**整份文件**，否则孤儿检查会把新分区全报成孤儿——这本身就是 `EnsureNoOrphans` 遍历全文件的证据 |

> `Icon` **没有**单独的字段级孤儿检查：`ContentText.IsUsed` 是**按条目**记的
> （条目下任一字段被取过即算已用），所以"图标字段没人读"这件事在结构上不存在——
> 座位的粒度就是"这座建筑有没有人读"，那条由既有的建筑双向比对与孤儿检查管。

### 10.4 反例证明（三处故意改坏，都在**真文件/真守卫**上做）

| 故意改坏 | 红在哪 | 原文 |
|---|---|---|
| 真文件里删掉 Lab 的 `batch_1` 整条 | 整个包 `Build()` 当场抛（6 条守卫红） | `...\content\Lab\text.json：eras 里没有 id「batch_1」。` |
| 真文件里加一条 `zz_orphan_era` | 孤儿检查（6 条守卫红） | `内容包「Lab」的剧情文本里有 1 条没人取用（孤儿条目）：eras/zz_orphan_era。它们要么是 id 与代码对不上，要么是代码里已经删掉了这段剧情。` |
| 期望层数 `Lab: 7` 改成 `6` | **只有** `EveryEra_ResolvesItsTextFromTheFile` 一条红 | `Lab: 代码里的纪元数与期望值对不上。｜期望 <6>，实际 <7>。` |

第三处是这三条里最说明问题的：**双向比对全绿，只有写死的条数表响了**——
正是"代码与 JSON 同时少一层"那个盲区。三处都还原，还原后 10 条全绿
（`9 通过 / 1 失败` → `全部通过：10 个用例`）。

### 10.5 这一轮之后还没做

`Upgrades.cs`（≈444）、`Buffs.cs`（≈212）、`Endings.cs`（58）、`Choices.cs`（36）、
`Achievements.cs`（36）、`Stances.cs`（33）。**纪元的六个字段与建筑的三个字段是全部已迁完的**
（剧情散文 / 建筑 / 纪元）。

---

## 11. 执行记录：结局 / 表态 / 立场 / 成就（第四轮，实际是怎么做的）

> 本轮做的是 §10.5 那张清单里 `Endings` / `Choices` / `Stances` / `Achievements` 四项。
> 和前三轮一样：**没有改 `engine/core` 一行**（`EndingDefinition` / `StanceDefinition` /
> `ChoiceDefinition` / `AchievementDefinition` 四个记录一行未动），没有动存档格式、
> 没有动快照协议、没有升版本——改的只是那些字符串**从哪里来**。

### 11.1 交付与形状

| 项 | 结果 |
|---|---|
| 结局 | **9 个包、29 个结局** → `endings.<id>.{name, icon, text}`（`Neko` / `Cafe` 没有结局） |
| 立场 | **3 个包、11 条立场** → `stances.<id>.{name, theme, icon, costText}` |
| 表态 | **3 个包、18 次表态、36 个选项** → `choices.<表态 id>.{speaker, prompt}` + `choices.<表态 id>.options.<选项 id>.{label, outcomeText}` |
| 成就 | **11 个包、712 条成就** → `achievements.<id>.{name, icon, description}` |
| 代码侧 | 一律 `Prose.Text("<分区>", "<id>", "<字段>")`；选项那一层是 `"表态id/选项id"` |
| 文件 | 仍是同一份 `content/<包>/text.json`，四个新根节**追加在末尾**，EOL 跟随原文件 |
| 用例 | **472 → 484**（`ContentTextFileTests` 10 → 22；`tools/build.ps1 -Strict` 两个 sln 0 警告、退出码 0） |
| 版本 | **不动**（公开 API 一行未改，无 minor 可升） |

**什么留在代码里、为什么**——与 §10.1 同一栏性质的判断：

| 字段 | 处置 | 为什么 |
|---|---|---|
| `EndingDefinition.Id` / `Priority` / `Condition` | 留 | 存档键 / 互斥顺序 / 条件树（逻辑） |
| `StanceDefinition.Id` / `Modifiers` | 留 | 引用键 / 数值管线 |
| `ChoiceDefinition.Id` / `EraId` / `Trigger` | 留 | 引用键、楼层硬门、条件树 |
| `ChoiceOption.Id` / `StanceId` / `Weight` / `Modifiers` | 留 | 键、立场归属、"这题值几分"（逻辑） |
| `AchievementDefinition.Id` / `Unlock` / `Modifiers` / `Category` / `Tier` / `Hidden` | 留 | 键、条件树、数值、分组标签、排序档位、显隐开关 |
| `Name` / `Icon` / `Text`（结局） | **搬** | 玩家读得到 |
| `Name` / `Theme` / `Icon` / `CostText`（立场） | **搬** | 同上（`CostText` 是 UI 与结算展示的代价说明） |
| `Speaker` / `Prompt` / `Label` / `OutcomeText`（表态） | **搬** | 同上（`Speaker` 是"谁在说话"，也显示给玩家） |
| `Name` / `Icon` / `Description`（成就） | **搬** | 同上 |

**成就这一类有一处与其它三类本质不同，值得单独说。** 成就表大多由循环铺出来
（"每座建筑三档"、`data_1e4`… 这类阈值档），名字里带建筑名、说明里带数字——
它们是**算出来的**，不是写死的。而外置机制只有"id → 字段"这一种（方案 §3 定下的），
所以写进文件的是**按 id 展开后的成品**：712 条各自一条文本，id 仍由代码算
（`$"{building.Id}_x{count}"`）。改名一座建筑，那三条成就的文案不会跟着变——
这是"按 id 展开"的代价，也是守卫必须逐条比对"代码值 == 文件值"的原因。
**没有为此发明模板引擎**：那会是一个新机制，超出"只搬散文"的范围。

**`Icon` 也搬了**（包括按建筑派生的那部分）。理由沿用 OPEN_WORK 的 L 条那条判据：
"一切玩家可见的东西都进文件"，图标既不是数值也不是逻辑。代价是
`buildings.<建筑>.icon` 与 `achievements.<建筑>_x1.icon` 现在是两份值——
`ContentText` 的守卫保证"代码读到的 == 文件里的"，但不保证两份文件值一致；
这一点是**已知且刻意接受**的（同性质的重复还有 `endings.<id>.name` 与
`achievements.ach_end_<id>.name`）。

### 11.2 方法：还是"判据落在运行期"

与 §8.2 / §9.2 / §10.2 同一条：

1. **迁移前**：一次性探针 `.tmp/Round4Dump`（独立小工程，不在 sln 里）读**运行期**的
   `GameContent.Endings` / `Stances` / `Choices` / `Achievements`，把**每个字段**（含
   `Priority`、条件树的 `Describe`、`Weight`、`Modifiers.Describe`、`Category`、`Tier`、
   `Hidden` 等**不搬**的字段）dump 成四份 JSON 基线——共 **345,460 字节**：
   `endings-baseline.json` 31,916（SHA-256 `23AE2BD7…`）、
   `stances-baseline.json` 6,669（`DEC91D1A…`）、
   `choices-baseline.json` 26,278（`DC8F5348…`）、
   `achievements-baseline.json` 280,597（`4DC44384…`）。
2. **生成**：四个分区**由这四份 dump 生成**，生成脚本每写一个包就把文件读回来、
   逐字段与运行期基线核对一遍（任一处不符即中止）。
3. **改写源码**：扫描器把字面量换成 `Prose.Text(...)`；能对上基线的字面量**先断言相等**
   （结局 / 立场 / 表态全部，成就里 id 与文本都是字面量的那些），任一处不符即整体中止。
   成就那边是"引用式正确"：查找用的 id 表达式**就是**该对象自己 `Id = …` 的那个表达式，
   所以不存在"id 与文案配错"的可能——配错只可能发生在"表里删错一列"，
   而那会改变数值与解锁条件，被下面第 4 步抓住。
4. **迁移后**：同一个探针再跑一次，四份 dump 与基线**逐字节相同**
   （`identical=True`，字节数与 SHA-256 一个都没变）。这是本轮最硬的那条证据。

**先把一个包走完再铺开**：试点仍是**实验室**（四个分区都有，5 结局 + 4 立场 + 6 表态 +
66 成就），试点后 dump 就已经逐字节相同；随后才铺其余 10 个包。
（成就那一步踩了两次编译期坑，都是**响的**、没有静默：① 单例成就的字面量 id 被误"提升"成
`string id = "click_100";`，同一方法里连写几处就重复声明；② 一个 `foreach` 体内有两处提升，
同名冲突。修法：id 表达式本身是标识符或字符串字面量时**直接用**，其余提升成
`id` / `id2` / … 各自独立的局部变量。）

### 11.3 守卫（`ContentTextFileTests`，10 → **22** 条）

| 新增/扩写 | 守什么 |
|---|---|
| **新增** `ExpectedEndings` / `ExpectedStances` / `ExpectedChoices` / `ExpectedAchievements` + 四条 `…CountTable_CoversExactlyTheGuardTable` | **写死每包条数**（四张表都是 11 个包；没有这一类内容的包写 **0**）。这是唯一能发现"代码与文件**同时**少一条"的守卫 |
| **新增** `EveryEnding_ResolvesItsTextFromTheFile` | 代码 ↔ 文件**两个方向** × 三字段逐字 + 非空；**期望 0 的包还要求文件里没有 `endings` 分区** |
| **新增** `EveryStance_ResolvesItsTextFromTheFile` | 同上 × 四字段 |
| **新增** `EveryChoice_ResolvesItsTextFromTheFile` | 问句两字段 + **选项两层 id**（`表态id/选项id`）两字段 + 条数（表态数与选项数）双向 |
| **新增** `EveryAchievement_ResolvesItsTextFromTheFile` | 同上 × 三字段（**11 个包全部有成就**，示例包也在内） |
| **新增** `EditingTheEndingTextWrongly_FailsLoudly` 等四条 | 少一条（取它那刻抛、点名 id）/ 多一条（孤儿检查点名）两种坏文件都要响 |
| **扩写** `ReadAll` / `ReadAllExcept` + 新的 `ReadEndings` / `ReadStances` / `ReadChoices` / `ReadAchievements` | 夹具读法必须覆盖**整份文件**，否则孤儿检查会把新分区全报成孤儿。跳过的写法统一成 `"分区/id"`（旧的三处调用点一并改成这个写法） |

> `Speaker` 与 `CostText` 这类字段没有单独的字段级孤儿检查，理由与 §10.3 的 `Icon` 相同：
> `ContentText.IsUsed` 是**按条目**记的，座位的粒度就是"这条有没有人读"。

### 11.4 反例证明（三处故意改坏，都在**真文件/真守卫**上做）

| 故意改坏 | 红在哪 | 原文 |
|---|---|---|
| 真文件里删掉 Lab 的 `end_open` 整条 | 整个包 `Build()` 当场抛（22 条守卫里 14 条红） | `…\content\Lab\text.json：endings 里没有 id「end_open」。` |
| 真文件里给 Lab 的 `achievements` 加一条 `zz_orphan_achievement` | 孤儿检查（14 条红） | `内容包「Lab」的剧情文本里有 1 条没人取用（孤儿条目）：achievements/zz_orphan_achievement。…` |
| 期望成就数 `Lab: 66` 改成 `65` | **只有** `EveryAchievement_ResolvesItsTextFromTheFile` 一条红 | `Lab: 代码里的成就数与期望值对不上。｜期望 <65>，实际 <66>。` |

第三处是这三条里最说明问题的：**双向比对全绿，只有写死的条数表响了**——
正是"代码与 JSON 同时少一条"那个盲区。三处都还原，还原后 22 条全绿
（`21 通过 / 1 失败` → `全部通过：22 个用例`），且 `text.json` 的 SHA-256 与改坏前逐一相同。

### 11.5 这一轮之后还没做

`Upgrades.cs`（≈444 处）与 `Buffs.cs`（≈212 处）——**"面向玩家的文案"这一类里就剩这两个**。
至此已迁完的是：剧情散文（10 包 421 条）、建筑（11 包 104 座 × 3 字段）、
纪元（9 包 49 层 × 6 字段）、结局（9 包 29 个 × 3）、立场（3 包 11 条 × 4）、
表态（3 包 18 次 × 2 + 36 选项 × 2）、成就（11 包 712 条 × 3）。

---

## 12. 执行记录：增益 / 升级 / 金猫结果（第五轮，实际是怎么做的）

> 本轮做的是 §11.5 那张清单的最后两项 `Buffs.cs` 与 `Upgrades.cs`，并且**顺手做掉了第三个文件**
> `GoldenCookieOutcomes.cs`（十一个包都有它，其中 `Neko` / `NineLives` 的那一份**就写在
> `Buffs.cs` 里**）。不做它的话，"面向玩家的文案已经全部外置"这句话就是假的——
> 而它和增益 / 升级是同一类东西：`GoldenCookieOutcome` 的 `Name` / `Description` / `Icon`
> 也是玩家在屏幕上读到的字。与前四轮一样：`engine/core` 的**公开 API 一行未改**
> （`BuffDefinition` / `UpgradeDefinition` / `GoldenCookieOutcome` 三个记录一行未动），
> 没有动存档格式、没有动快照协议、**没有升版本**；改的只是那些字符串**从哪里来**。
> （`ContentText.cs` 只改了 XML 注释，见 §12.6。）

### 12.1 交付与形状

**先把数字量准**——任务书给的"≈444 / ≈212 处"与实测差得很远（这个项目里估算错过不止一次）：

| 量的是什么 | 实测 |
|---|---|
| 源码里的字面量**赋值点** | `Upgrades.cs` **699**、`Buffs.cs` **318**（含 Neko / 九命写在里面的金猫结果）、`GoldenCookieOutcomes.cs` **267**，合计 **1,284 处** |
| 源码里的对象初始化器 | **428 个**（417 个字面量 id + 11 个算出来的 id）；428 × 3 = 1,284，每个初始化器恰好三个散文字段，一个不多一个不少 |
| 运行期的条目 | 增益 **86**、升级 **534**、金猫结果 **109** = **729 条**；差在升级：**11 个生成器把 233 个初始化器铺成了 534 条** |

| 项 | 结果 |
|---|---|
| 增益 | **11 个包、86 条** → `buffs.<id>.{name, description, icon}` |
| 升级 | **11 个包、534 条** → `upgrades.<id>.{name, description, icon}` |
| 金猫结果 | **11 个包、109 条** → `goldenCookies.<id>.{name, description, icon}` |
| 逐包条数 | 增益 6/5/8/8/8/10/8/8/9/8/10、升级 49/48/55/44/46/47/47/49/51/47/51、金猫 10/8/10/10/10/10/10/10/11/10/10（顺序同 `Externalized` 表：Neko / Cafe / NineLives / Lab / Company / Apocalypse / Library / God / Civ / Cyber / Dream） |
| 代码侧 | 一律 `Prose.Text("<分区>", <该对象自己的 id 表达式>, "<字段>")` |
| 文件 | 仍是同一份 `content/<包>/text.json`，三个新根节**追加在末尾**，EOL 跟随原文件（Neko 是 LF，其余十个是 CRLF） |
| 代码量 | 源码里**一个字面量都不剩**：`Prose.Text(` 出现 **1,284** 次，`Name = "` / `Description = "` / `Icon = "` 出现 **0** 次 |
| 用例 | **484 → 493**（`ContentTextFileTests` 22 → **31**；`tools/build.ps1 -Strict` 两个 sln 0 警告、退出码 0） |
| 版本 | **不动**（公开 API 一行未改，无 minor 可升） |

**什么留在代码里、为什么**——与 §10.1 / §11.1 同一栏性质的判断：

| 记录 | 搬走的（散文） | 留下的（结构 / 计算） |
|---|---|---|
| `BuffDefinition` | `Name` / `Description` / `Icon` | `Id`（存档与引用键）、`Duration`、`MaxStacks`、`StackMode`、`Modifiers`、`IsDebuff`、`Dispellable` |
| `UpgradeDefinition` | `Name` / `Description` / `Icon` | `Id`、`Price`、`Currency`、`Persistence`、`MaxPurchases`、`PriceGrowth`、`Unlock`（条件树）、`Modifiers`、`Tags`、`Category`、`Tier`、`HiddenUntilUnlocked` |
| `GoldenCookieOutcome` | `Name` / `Description` / `Icon` | `Id`、`Weight`、全部 `Cookies*`、`StealBankFraction`、`BuffId` / `BuffSeconds`、`SecondaryBuffId` / `SecondaryBuffSeconds`、`IsRare` |

**升级这一类是这一轮真正的判断工作，值得单独说。** 升级表里有一大类是**生成**出来的：
每个包都有一段"每座建筑三档"的循环（`Id = $"{building.Id}_tier{required}"`、
`Name = $"{prefix}{building.Name}"`、`Icon = building.Icon`、
`Description = $"「{building.Name}」的产量翻倍。"`）。它们看起来像**模板**，但外置机制只有
"id → 字段"这一种（§3 定下的），所以只有两条路：**发明一个模板引擎**，或者
**按 id 展开成成品**。第四轮在成就上已经撞过同一堵墙并选了后者，这一轮**沿用同一条路**：

- 写进文件的是 `upgrades.incubator_tier1.name = "校准过的培养舱"` 这样**逐条展开**的成品，
  一条一个 id；id 仍然由代码算。
- **没有发明模板引擎**——那会是一个新机制（占位符、解析器、渲染时机、失败模式），
  超出"只搬散文"的范围，而且会让"文件里的字"与"玩家看到的字"重新分开。
- **代价（已知、刻意接受）**：改名一座建筑，那三档升级的名字 / 说明 / 图标
  **不会**跟着变。同一份值现在存在两处（`buildings.<建筑>.name` 与
  `upgrades.<建筑>_tier1.name`），`ContentText` 保证"代码读到的 == 文件里的"，
  但**不保证两份文件值一致**——与 §11.1 里 `endings.<id>.name` 对
  `achievements.ach_end_<id>.name` 的重合是同一性质。守卫因此必须逐条比对
  "代码值 == 文件值"（`EveryUpgrade_ResolvesItsTextFromTheFile`），
  条数表则是唯一能发现"代码与文件**同时**少一档"的东西。

**一处**静默降级**路径**——`GoldenCookieOutcome.Description`：
`GoldenCookieSystem.Describe` 里写着
`string.IsNullOrWhiteSpace(outcome.Description) ? outcome.Name : outcome.Description`，
也就是**说明为空时界面悄悄改用名称**。这与 §10.1 的 `CompletionHint` 是同一形态，
所以搬的时候用 `Text(...)`（缺失即抛）而**不是** `TextOr`，守卫里也额外断言它非空。
它的 `{amount}` / `{duration}` 占位符**不是**模板引擎：替换仍然发生在核心代码里，
文件里存的是模板本身那一条条文本。另外两个散文字段（`Name` / `Icon`）没有这种回退路径，
但同样用了 `Text(...)` 并断言非空——一致性比"省一条断言"值钱。

### 12.2 方法：还是"判据落在运行期"

与 §8.2 / §9.2 / §10.2 / §11.2 同一条，没有另写 C# 字符串解析器：

1. **迁移前**：一次性探针 `.tmp/Round5Dump`（独立小工程，不在 sln 里）读**运行期**的
   `GameContent.Buffs` / `.Upgrades` / `.GoldenCookieOutcomes`，把**每个字段**（含不搬的
   `Duration`、`StackMode`、`Modifiers.Describe`、`Price`、`Unlock.Describe`、`Tags`、
   `Category`、`Tier`、`Weight`、全部 `Cookies*` 等）dump 成三份 JSON 基线——共 **524,163 字节**：
   `buffs-baseline.json` 46,720（SHA-256 `3C2BD4EF…`）、
   `upgrades-baseline.json` 401,690（`F9A314FC…`）、
   `goldenCookies-baseline.json` 75,753（`0C38C9D5…`）。
   浮点数写成 round-trip 字符串，因为其中一个默认值是 `+Infinity`，`System.Text.Json` 拒绝写成数字。
2. **生成**：三个分区**由这三份 dump 生成**，生成脚本每写一个包就把文件读回来、
   逐字段与运行期基线核对一遍（任一处不符即中止，且拒绝重复追加）。
3. **改写源码**：扫描器按"类 → 分区"逐文件走，把每个对象初始化器里的三个字面量换成
   `Prose.Text(...)`；**能对上基线的字面量先断言相等**（1,284 − 11 × 3 = **1,251 处**），
   任一处不符即整体中止、不写任何文件。
   算出来的那 11 个 id 是"引用式正确"：查找用的表达式**就是**该对象自己 `Id = …` 的那个表达式
   （提升成 `string id = $"{building.Id}_tier{required}";`），所以不存在"id 与文案配错"的可能。
4. **迁移后**：同一个探针再跑一次，三份 dump 与基线**逐字节相同**
   （`identical=True`，字节数与 SHA-256 一个都没变）。这是本轮最硬的那条证据。
5. **补一条静态兜底**：漏掉一个赋值点时，那个字面量会**原样留在代码里**，
   而 before/after dump 仍然会逐字节相同（字面量的值本来就等于文件里的值）——
   所以改写脚本最后会重扫一遍，要求这三个文件里
   `Name = "` / `Description = "` / `Icon = "` **一处都不剩**（实测 leftovers = 0）。
   这是"漏改"这个盲区的唯一守卫。

**先把一个包走完再铺开**：试点仍是**实验室**（8 增益 + 44 升级 + 10 金猫结果，三类都在），
试点后 dump 就已经逐字节相同；随后才铺其余 10 个包。
（路上踩了两次**自己造的**坑，都不是静默的：① 用 `open(path,'w')` 做 dry-run 会**当场把文件截断**
——备份救回来了，见 §12.5；② Python 默认的 universal newline 把 CRLF 读成 LF，
写回去会让整个文件变成全文件 diff——改成 `newline=''` 后，所有文件的行尾与改动前逐一相同。）

### 12.3 守卫（`ContentTextFileTests`，22 → **31** 条）

| 新增/扩写 | 守什么 |
|---|---|
| **新增** `ExpectedBuffs` / `ExpectedUpgrades` / `ExpectedGoldenCookieOutcomes` + 三条 `…CountTable_CoversExactlyTheGuardTable` | **写死每包条数**。三张表都是 11 个包、**一个 0 都没有**（这三类内容十一个包全有，与纪元 / 结局那几类不同）。这是唯一能发现"代码与文件**同时**少一条"的守卫 |
| **新增** `EveryBuff_ResolvesItsTextFromTheFile` | 代码 ↔ 文件**两个方向** × 三字段逐字 + 三字段非空 |
| **新增** `EveryUpgrade_ResolvesItsTextFromTheFile` | 同上；这一条同时是"**算出来的 id** 与文件里的键必须是同一套"的证据 |
| **新增** `EveryGoldenCookieOutcome_ResolvesItsTextFromTheFile` | 同上 × 三字段；`Description` 的非空断言带上了"为空会静默改用名称"的理由 |
| **新增** `EditingTheBuffTextWrongly_FailsLoudly` 等三条 | 少一条（取它那刻抛、点名 id）/ 多一条（孤儿检查点名）两种坏文件都要响 |
| **扩写** `ReadAllExcept` + 新的 `ReadBuffs` / `ReadUpgrades` / `ReadGoldenCookieOutcomes` | 夹具读法必须覆盖**整份文件**，否则孤儿检查会把新分区全报成孤儿。跳过的写法仍是统一的 `"分区/id"` |

> 三个散文字段都没有**字段级**孤儿检查，理由与 §10.3 的 `Icon` 相同：
> `ContentText.IsUsed` 是**按条目**记的，座位的粒度就是"这一条有没有人读"。

### 12.4 反例证明（三处故意改坏，都在**真文件/真守卫**上做）

| 故意改坏 | 红在哪 | 原文 |
|---|---|---|
| 真文件里删掉 Lab 的 `containment_breach` 整条 | 整个包 `Build()` 当场抛（31 条守卫里 20 条红） | `…\content\Lab\text.json：buffs 里没有 id「containment_breach」。` |
| 真文件里给 Lab 的 `buffs` 加一条 `zz_orphan_buff` | 孤儿检查（20 条红） | `内容包「Lab」的剧情文本里有 1 条没人取用（孤儿条目）：buffs/zz_orphan_buff。…` |
| 期望升级数 `Lab: 44` 改成 `43` | **只有** `EveryUpgrade_ResolvesItsTextFromTheFile` 一条红（31 条里 1 条） | `Lab: 代码里的升级数与期望值对不上。｜期望 <43>，实际 <44>。` |

第三处是这三条里最说明问题的：**双向比对全绿，只有写死的条数表响了**——
正是"代码与 JSON 同时少一条"那个盲区。三处都还原，还原后 31 条全绿
（`11 通过 / 20 失败` → `30 通过 / 1 失败` → `全部通过：31 个用例`），
且 `Lab/text.json` 的 SHA-256 与改坏前逐一相同（`B24B21A1…`）。

### 12.5 备份与"工作树干净"的证据

- 备份是**动手之前**做的：11 份 `text.json` → `.tmp/textjson-backup-round5/`，
  31 份源码（11 `Buffs.cs` + 11 `Upgrades.cs` + 9 `GoldenCookieOutcomes.cs`）→ `.tmp/cs-backup-round5/`。
  上面那个 `open(...,'w')` 的截断坑把 10 个文件清成 0 字节，**全部由备份还原**，
  还原后 `git status` 干净（初始状态下与 HEAD 逐字节相同）。
- 行尾：所有被改的文件行尾与改动前逐一相同（CRLF 计数 == LF 计数，Neko 的 `text.json` 仍是纯 LF）。
- 真实的 `saves/` **一次都没写**：探针与守卫都只读运行期对象，不碰磁盘上的存档；
  收尾时按"大小 + mtime ticks + sha256"逐文件核对了那 11 个文件（含 `.bak`），全部未变。

### 12.6 `ContentText.cs` 的注释修正（唯一一处 `engine/core` 改动）

`ContentText` 的类注释列分区时写的是**只到第一轮为止**的四个
（`lore` / `eras` / `choices` / `endings`），`Text` 的 `<param name="kind">` 同样是这四个；
现实是**十一个**（`storylines` / `lore` / `buildings` / `eras` / `endings` / `stances` /
`choices` / `achievements` / `buffs` / `upgrades` / `goldenCookies`）。本轮**只改注释**：
没有动签名、没有动行为、没有加成员，所以**没有版本 bump**；
公开 API 快照逐字节未变，`PublicApiTests` 四条（含
`PublicApiGuard_RejectsEveryKindOfBreakingChange`）全绿。
（任务书说这段注释把文件写成 `content/<包名>/lore.json`——**实测不是**，
那句早就是 `text.json`；真正过期的只有 kind 清单这一处，一并说明。）

### 12.7 这一轮之后：还剩什么

**"面向玩家的文案"这一类**：`storylines` / `lore` / `buildings` / `eras` / `endings` /
`stances` / `choices` / `achievements` / `buffs` / `upgrades` / `goldenCookies` **十一类全部迁完**。
累计规模：剧情散文（10 包 421 条）、建筑（11 包 104 座 × 3 字段）、
纪元（9 包 49 层 × 6 字段）、结局（9 包 29 个 × 3）、立场（3 包 11 条 × 4）、
表态（3 包 18 次 × 2 + 36 选项 × 2）、成就（11 包 712 条 × 3）、
增益（11 包 86 条 × 3）、升级（11 包 534 条 × 3）、金猫结果（11 包 109 条 × 3）。

**还留在代码里的中文**（都不属于"给玩家读的散文"，刻意不动）：条件树与修饰符的
`Describe` 文本（`UnlockCondition.cs` / `Modifier.cs`，是引擎在渲染条件，不是内容）、
引擎的通知与错误消息（`GameEngine.cs` 等）、以及内容包自己为了**报错**而写的中文
（例如构建期校验消息）。这些要外置的话是**另一个方案**，不是本方案的收尾。

---

## 13. 自由文本映射表集：`$tables` 清单 + 没人读的表（机制，不是搬迁）

> 前五轮（§8~§12）做的都是**搬迁**：把已经在 C# 里的散文搬到 `text.json`。
> 这一步不是——它不搬任何现成文本，而是**给文件加一种可以自由增减的节**。
> 因此它也是唯一一步**必须动 `ContentText.cs` 的行为**（公开签名仍然一行未改，见 §13.5）。
> **§13.0~§13.9 是设计**（先写下来）；落地后的实况、红色原文与用例数补进 §13.6 与 §13.10。

### 13.0 要解决的那个张力

`EnsureNoOrphans`（`engine/core/Content/ContentText.cs:189`）会把"文件里有、代码从没取过"
的条目一律报成孤儿抛出去——这条严格正是它抓住 id 打错与半迁移的原因。但"自由文本表"的
**定义**就是"代码还没读它"。不区分这两件事，就只有两个坏选择：要么关掉孤儿检查（typo 重新静默），
要么自由表写不进去。所以：

| 类 | 谁读它 | 孤儿检查 |
|---|---|---|
| **代码消费的分区**（现有十一类，`ContentText.cs:24`~`:31`） | 包的 `Build()` 逐条 `Prose.Text(...)` | **照旧严格**：没人读就是孤儿 |
| **自由文本表**（本节新增） | **没有人**（v1 刻意如此，§13.4） | **豁免**，但必须**显式声明**，"没人读"由守卫核对 |

豁免**不能**靠"格式看起来不像"实现：`EnsureNoOrphans` 第 199 行对**值不是对象的条目直接
`continue`**，所以把自由表写成 `"rumours": { "k": "文本" }` 会被**静默跳过**——
那正是"把 typo 从守卫底下塞过去"的路。本设计因此同时做三件事：

1. 自由表**沿用文件既有的条目形状** `id → { 字段: 文本 }`（字段名固定 `text`），
   于是照旧**能被 1.2.0 就有的 `Text(kind, id, field)` 读**；
2. "它没人读"由根节点的保留清单 **`$tables`** 明文声明；
3. **未声明的分区里出现非对象条目 → 当场抛**（不再被第 199 行悄悄跳过）。

### 13.1 形状

```jsonc
{
  "$tables": {                       // 保留清单：根节点上只有这一个键是保留的
    "rumours": { "kind": "free" }    // free = 没人读；consumed = 代码消费（与不声明等价）
  },

  "rumours": {                       // 自由表：与其它分区同一层，随便加、随便删
    "rumour_41st": { "text": "值班记录本上第 41 页被撕掉了，撕口是新的。" }
  },

  "lore": { "door_01": { "title": "…", "body": "…" } }   // 十一类照旧，一个字节都不用改
}
```

- **清单**：根键 `$tables`（`$` 开头即保留；别的 `$XXX` 一律抛）。值是
  `表名 → { "kind": … }`：`kind` 必填、只认 `free` / `consumed`、声明里不许有别字段。
- **自由表条目**：`{ "text": "<非空字符串>" }`，**恰好一个字段**。
- **没有出现在清单里的分区**：行为与今天**完全一致**（等同 `consumed`）。
  十一类因此**不需要在清单里登记**——这就是"不回归"的实现方式：现有 `text.json` 一字不改照旧跑。

### 13.2 声明住在哪、谁检查它

**声明住在 `text.json` 自己（`$tables`）**，检查只有两处：

| 检查 | 在哪 | 判得了什么 |
|---|---|---|
| **结构与形状** | `ContentText.Load`（`ContentText.cs:73`） | 清单合法、kind 认识、声明的表真的存在、自由表非空、条目是 `{text:非空}`、**全文重复键**、未声明分区里的条目必须是对象 |
| **声明的诚实性 + 孤儿** | `ContentText.EnsureNoOrphans`（`ContentText.cs:189`） | 声明为 `free` 的**不许被代码取用**（取用了当场抛，点名表与键）；其余分区照旧查孤儿 |

`Load` 一包一次（内容包都持有 `Lazy<ContentText>`，`CONTENT_AUTHORING.md:661`）；
`EnsureNoOrphans` 由**包的 `Build()` 末尾**调用——**十一个包全都有这一句**
（`engine/content/*/` 的 `Lore.VerifyAllTextUsed()`，示例包是 `Neko/Buildings.cs:46` 的
`VerifyAllTextUsed()`），所以两条检查在测试、Web 宿主、Demo 三条路径上都会跑到。

**为什么放 `text.json` 而不是另起一个文件**：自由表的读者是**包自己**
（由包在 `Build()` 路径上校验），与 `text.json` 的读者同一批；另起文件要新增复制规则
（`Directory.Build.props:81`~`:86`）与第二个解析器，**更多代码**而不是更少。
`WEB_EXTENSION_PLAN` §5 否决的是"给 `text.json` 加**宿主读的**分区"，见 §13.7。

### 13.3 失败模式（每一条都点名文件 / 表 / 键）

| 失败 | 谁发现 | 消息要点 |
|---|---|---|
| `$tables` 不是对象 | `Load` | 路径 + 期望形状 |
| 声明不是对象 / 缺 `kind` / 不认识 `kind` / 多字段 | `Load` | 路径 + 表名 + **列全这一版认的 kind** |
| 声明了文件里没有的表 | `Load` | 路径 + 表名（"声明是给谁看的"） |
| 保留键里出现别的 `$XXX` | `Load` | 路径 + 键名 + 已知保留键 |
| 自由表为空 `{}` | `Load` | 路径 + 表名（空表 = 作者以为写了什么） |
| 条目不是对象 / 缺 `text` / `text` 非字符串 / `text` 全空白 / 多字段 | `Load` | 路径 + 表 + 键（+ 字段） |
| 同一对象里同一个键出现两次（任意层） | `Load` | 路径 + 位置 + 键名（类注释第 15 行早就承诺"重复 = 静默覆盖"要抛，此前**没有兑现**：实测 `JsonNode.Parse` 对重复键既不抛也不合并） |
| 未声明的分区里有非对象条目 | `Load` | 路径 + 分区 + 键 + **怎么改成自由表** |
| 声明 `free` 但代码取用了它 | `EnsureNoOrphans` | 路径 + 表 + 键 + "要么改声明、要么别读" |
| 声明 `consumed`（或不声明）却没人读 | `EnsureNoOrphans`（孤儿检查） | 照旧：列出全部孤儿 `分区/id` |

### 13.4 怎么读：v1 的答案是"不读"

**v1 刻意不提供"读自由表"的 API，也不在宿主 / 前端渲染它。** 这与"界面扩展"是两件事
（§13.7）：自由表现在是**给作者写的地方**，不是给玩家的内容。

将来要读它，**不需要动 `engine/core` 一个字节**：

1. 把那条声明从 `"kind": "free"` 改成 `"kind": "consumed"`（**改数据**）；
2. 在包代码里写 `Prose.Text("rumours", "rumour_41st", "text")`——**用的是 1.2.0 就有的 API**；
3. 改完立刻恢复严格：没被读到的其余条目**当场变成孤儿**（这正是我们要的）。

**这就是"条目形状沿用 `id → {字段}`"的全部理由**：它让"自由 → 消费"这一步**永远不需要新 API**。

### 13.5 版本：不动

`ContentText` 的四个公开方法（`Load` / `Text` / `TextOr` / `EnsureNoOrphans`）**签名一行未改**，
没有新增公开成员，所以按 `VERSIONING.md:76`~`:78` 不构成 minor；`engine/core/PublicApi.txt`
**逐字节不变**（`PublicApiTests` 四条照旧全绿）。

**诚实边界**：本轮确实改了两处**行为**，但都只作用在"本来就坏的输入"上——
① 未声明分区里的非对象条目：从"静默跳过"变成"抛"；② 重复键：从"静默保留"变成"抛"。
按 `VERSIONING.md:78` 最后一行（"公开 API 一行没动 → patch"）可以主张这是 patch（`1.7.1`）。
**本轮没有动版本号**：`Directory.Build.props` / `VERSIONING.md` / `CHANGELOG.md` 此刻正被
另一轮（`1.7.0`，`GameSnapshot.ModeName`）持有未提交改动，动它们会把两轮工作搅在一起。
这一条留在 §13.9 由人定。

### 13.6 守卫（落地实况）

`ContentTextTests`（**合成文件**，守加载器与"声明诚实性"的每条分支）**+9 条**：

| 用例 | 守什么 |
|---|---|
| `DeclaredFreeTable_IsExemptFromTheOrphanCheck` | **正例**：声明 `free` + 没人读 ⇒ 孤儿检查放行 |
| `FreeTableThatCodeReads_Throws` | **声明 free 却被代码读** ⇒ 抛，点名表与键 |
| `DeclaredConsumedTable_NobodyReadsIt_StillThrows` | **声明 consumed 却没人读** ⇒ 声明换不来豁免，照旧孤儿 |
| `FlippingToConsumed_NeedsNoNewApi_AndRestoresStrictness` | "自由 → 消费"只改数据：用 1.2.0 的 `Text()` 就能读，读不到的那条立刻变孤儿 |
| `BrokenManifest_ThrowsForEachShape` | 清单 7 种坏法（不是对象 / 声明不是对象 / 缺 `kind` / `kind` 不认识 / 多字段 / 悬空声明 / 别的 `$XXX`） |
| `BrokenFreeTableEntry_ThrowsForEachShape` | 条目 5 种坏法（空表 / 裸字符串 / 缺 `text` / `text` 非字符串 / 全空白） |
| `UndeclaredTableOfPlainText_Throws` | 未声明分区里的**非对象条目**（老实现会静默跳过的那个洞） |
| `DuplicateKey_IsRejectedAndNamed` | 任意层级的重复键，点名位置与键 |
| `WithoutAManifest_EverythingBehavesAsBefore` | **反面对照**：没有清单时行为与以前一致 |

`ContentTextFileTests`（**真实树**）**31 → 33 条**，新增两条：
`EveryPack_PassesTheFreeTableRulesWithItsRealFile`（十一个包各用自己的那一份 `text.json`
跑一遍 `Load` + 按代码的 id 表读全十一类 + `EnsureNoOrphans`）、
`EveryDeclaredTable_ExistsInTheFile_AndAtLeastOneFreeTableExists`（清单与分区必须成对，
且**至少真有一张自由表**——否则前一条横扫是假绿）。

**故意不给自由表写条数表**，理由见 §13.9 第 3 条。

### 13.7 与 `WEB_EXTENSION_PLAN` 的关系（不重复它）

那份（**已搁置、未实现**）方案 §5 明确否决了"给 `text.json` 加新分区"，理由两条：
`ui.json` 的读者是**宿主**不是包，以及**放松孤儿检查会削弱守卫**
（`WEB_EXTENSION_PLAN.md:269` 与它的 D2，`:534`）。本设计**两条都不违反**：

- 读者仍是**包自己**（声明写在包自己的 `text.json`，在包的 `Build()` 路径上校验）；
- **孤儿检查对其它分区一个字都没放松**——它新增的是一个**显式声明的类别**，
  而"声明为 free 却被人读"反而**多出一条**报错。

对象也不同：`ui.json` 管"**界面块从哪来**"（声明 → 快照 → 渲染），本设计管"**没人读的文本放哪**"。
那份方案真正缺的那一格（"这一座建筑的一段故事没有任何地方可写"，`WEB_EXTENSION_PLAN.md:166`）
恰好由自由表补上：**先有地方写，再谈画在哪**。将来真要渲染，仍按那份方案走
（白名单原语、`textContent`、快照顶层字段），本设计**不为它预埋任何东西**。

### 13.8 明确不做

- 不做模板 / 占位符 / 条件（沿用 §12.2 第 3 条那次判断）；
- 不做"读全部自由表"的通用 API，不做宿主 / 前端渲染，不做页签；
- 不做自由表的 `title` / 多字段 / 排序 / 分组（v1 恰好一个 `text`）；
- 不动存档、不动快照协议、不动 `wwwroot/`；
- **不放松任何既有守卫**去换这个特性。

### 13.9 待定（不替人拍板）

1. **要不要按 `VERSIONING.md:78` 升 patch（`1.7.1`）**：见 §13.5 的诚实边界。
2. **自由表将来谁来读**：包代码（推荐，`Text()` 即可）？宿主？前端？——若走宿主 / 前端，
   就回到 `WEB_EXTENSION_PLAN` 的 D1 / D7 / D9 那几条要定的问题，本设计不预判。
3. **要不要把"现有自由表清单"写进守卫表**：**现决定不写。** 理由：自由表**没有"代码那一侧"**，
   所以"两边同时少一条"这个盲区在结构上不存在（`CONTENT_AUTHORING.md:674` 那条条数表的
   适用前提在此不成立）。代价写清楚：**"把 `Lab` 那张示例表整张删掉"不会有任何用例变红——
   因为那是合法操作**；换来的是"加 / 删表零门槛"这条主张真的成立。
4. **一个包能声明多少张表、名字要不要前缀**：v1 不限，只有 `$` 前缀属保留键这一条约束。
5. **要不要给 `text.json` 加 schema 版本号**（`WEB_EXTENSION_PLAN` 的 D3）：同一个坑的另一面，
   本设计不顺手做。

### 13.10 执行记录（2026-10-03，实际是怎么做的）

| 项 | 结果 |
|---|---|
| 落地面 | `engine/core/Content/ContentText.cs` **一个文件**：`Load` 里三条新校验（重复键 / 清单 / 分区形状）+ `EnsureNoOrphans` 里两条（自由表豁免、声明的诚实性）。**公开签名一行未改** |
| 示例 | `engine/content/Lab/text.json`：根节点新增 `$tables`（1 张表）与 `rumours`（3 条，作者自己写的短句）。试点仍选**实验室**，与前五轮一致 |
| 用例 | **493 → 504**（`ContentTextTests` +9、`ContentTextFileTests` 31 → 33；`tools/build.ps1 -Strict` 两个 sln **0 警告**、退出码 0，2026-10-03 实测） |
| 公开 API | `engine/core/PublicApi.txt` **逐字节未变**（`PublicApiTests` 四条照旧全绿）；`Directory.Build.props` 的版本号**没动**（理由见 §13.5） |
| 行尾 | `Lab/text.json` 改动前后都是纯 CRLF（CRLF 数 == LF 数 == **1070**）；改动由 `edit` 工具完成（该工具按文件既有的行尾写新行——实测过，不是推断） |
| 文档 | 本节 + `CONTENT_AUTHORING.md` §12.0.5（给作者的写法） |

**判别力：三处故意改坏，都在真文件 / 真守卫上做**（先备份 + SHA-256 记账，逐处还原并核对哈希）：

| # | 故意改坏 | 红在哪 | 原文（截取） |
|---|---|---|---|
| 1 | `Lab/text.json` 的 `$tables` 把 `rumours` 从 `free` 改成 `consumed`（= **声明 consumed 却没人读**） | 52 条里 **22 条**红 | `内容包「Lab」的剧情文本里有 3 条没人取用（孤儿条目）：rumours/rumour_41st、rumours/rumour_centrifuge、rumours/rumour_quiet_wing。它们要么是 id 与代码对不上，要么是代码里已经删掉了这段剧情。（自由文本表不走这条检查，但它必须在 $tables 里声明为「free」。）` |
| 2 | `$tables` 里加一条 `"buffs": { "kind": "free" }`（把**代码消费的**分区声明成自由表） | **21 条**红 | `…\content\Lab\text.json：自由文本表「buffs」的条目「containment_breach」里多了一个字段「name」——这一版自由表的条目恰好一个字段「text」。` |
| 3 | 在 `Lab/Lore.cs` 的 `VerifyAllTextUsed()` 里读一条自由表（= **声明 free 却被代码读**），重编 Lab 后跑 | **21 条**红 | `…\content\Lab\text.json：表「rumours」在 $tables 里声明为「free」（没有人读），但代码取用了「rumours/rumour_41st」——声明与代码必须一致：要么把它改成「consumed」并让包真的把整张表读完，要么别读它。` |

三处都还原：`Lab/text.json` 与 `Lab/Lore.cs` 的 SHA-256 与改坏前逐一相同
（`text.json` = `DF37AC98…`，`Lore.cs` 的 `git diff` 为空），还原后 `-Strict` **504/504 全绿**。

> 第 2 处值得单独记一笔：它说明**两条检查的先后顺序是有意义的**——把一张
> `{name, description, icon}` 形状的分区声明成自由表时，`Load` 的**形状**检查先响
> （消息点名表、条目与那个多余的字段），轮不到 `EnsureNoOrphans` 的诚实性检查。
> 诚实性检查真正管的是第 3 处那种情形：表**本来就是** `{text}` 形状（本来是自由表），
> 后来有人开始读它却忘了改声明。这两条合起来覆盖"声明与实际不符"的两种方向。

**没做 / 没能做**：

- **没有给出"读自由表"的 API 或界面**（§13.4 的刻意留白）。
- **没有随本轮更新"当前用例数"**（`README.md` / `STATUS.md` / `VERSIONING.md` 等处仍是 `493`）：
  那几处此刻正被 `1.7.0` 那一轮的未提交改动持有（`git status` 里是 `M`），
  动它们会把两轮工作搅在一起。**这是一处已知的文档漂移**，应由 `1.7.0` 那轮或之后一次收尾统一改。
- **没有登记进 `OPEN_WORK.md`**：同一个理由（该文件也在那一轮手里）。

