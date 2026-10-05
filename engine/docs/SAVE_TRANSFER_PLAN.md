# 存档的导出与导入（方案）

> 需求原话：「然后重新写一个存档系统，要有导出和导入窗口」。
> 本文回答三件事：**今天缺的到底是什么**（§1，带证据）、**要做的形状**（§2~§6）、
> **分几次做、这次做到哪**（§7）。
>
> 状态：**全部落地（2026-10-04）**——第一刀（引擎侧 API + 守卫，随 **1.10.0** 发布）、
> 第二刀（Web 的导出/导入窗口，`OPEN_WORK` 的 **W13**）、第三刀（终端的 `E` / `I` 路径提示，
> **W14**）都在树上；实测见 §8（第一刀）与 §9（第二、三刀）。
> 第四刀（`tools/` 里一条 `--wrap`）**仍然不做**：它等的"我手上只有旧文件"这句真人原话
> 至今没有出现（§7）。
> 本文是这一件事的**方案**；「现在是什么样」归 [ARCHITECTURE](ARCHITECTURE.md)，
> 「还欠什么」归 [OPEN_WORK](OPEN_WORK.md) 的《下一步：唯一权威清单》。
>
> **后记（2026-10-06）**：本文的信封与九道闸**一个字节都没变**。另有一件"把存档搬到别处去"的
> 新事——**分享链接**（`?share=<密文>`，用密码保护）——归
> [`SHARE_LINK_PLAN.md`](SHARE_LINK_PLAN.md)：它**不新造导入路径**，
> 解出来的就是本文这套 `Export()` 的原文，原样交给**同一个** `SaveManager.Import`（该方案 §5.3）。

---

## 1. 先量：今天有什么、缺什么

### 1.1 已经有的（不要再造一遍）

| 能力 | 证据 | 状态 |
|---|---|---|
| 存储抽象 + 内存实现 | `engine/core/Persistence/IStorage.cs:10` `IStorage`，`:225` `MemoryStorage` | 够用 |
| **写盘的三道闸**：先写 `.tmp` → 反解一遍证明读得回来 → `File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true)`；被换下来的那份**自己读得回来**才配当备份 | `IStorage.cs:95-132`（`Write`）、`:171-184`（`VerifyLoadable`）、`:189-200`（`IsLoadable`） | **够用，而且比"重写一遍"能做到的更可信** |
| 版本迁移机器（对 JSON 树执行，反序列化**之前**） | `SaveSerializer.cs:202-267`（`Parse`）、`SaveData.cs:177`（`ISaveMigration`）、`SaveSerializer.cs:22`（`Migrations` 注册表） | 够用（生产里零实现，见 D5） |
| 损坏一律 `InvalidDataException`（含"合法 JSON 但读不成 SaveData"） | `SaveSerializer.cs:214/217/222/244/247/279/293` | 够用 |
| 自动存档（60 秒）与退出前存档 | `SaveManager.cs:144-151`（`Tick`）、`GameHost.cs:426-451`（关停排空） | 够用 |
| 两个宿主的存档位置开关 | Web `--save-root`（`Program.cs:44`）、终端 `--save` / `--no-save`（`CliOptions.cs:143-149`） | 够用 |
| 导出：`ExportShareCode()`（base64） | `SaveManager.cs:136`、`SaveSerializer.cs:34-36` | **有，但没人用、也不自证身份** |
| 导入：`WriteRaw(json)`（写盘）+ `ReadRaw()` | `SaveManager.cs:117-133` | **半条**：只写文件，不碰当前会话 |

### 1.2 缺的（这就是要补的东西）

**缺口 A — 导出物不自证身份。** `ExportShareCode()` 是 `SaveData` 裸 JSON 的 base64
（`SaveSerializer.cs:35-36`），里面只有 `SaveData.Version` 一个数字。**包 id、校验和、格式标签、
导出时刻，一个都没有**。十一个包写出来的存档形状**逐字段同构**，区别只在文件名叫
`saves/<包 id>.json`（`GameHost.cs:117`、`CliOptions.cs:204-207`）——**内容里没有一处写着
"我是哪个包的"**。代价不是理论：`SaveSerializer.Parse` 对
`{"Version":1,"Buildings":{"cafe_table":3}}` 是**成功**的，`Apply` 又**照单全收**未知 id
（`SaveSerializer.cs:133-134`，这是刻意的，见 `ARCHITECTURE.md:124`），于是"把咖啡馆的存档
导进末世包"会**一声不响地成功**，然后在下一局以"界面一片正常、只有通知里说了一句"的样子
出现——正是 `OPEN_WORK` §5.1 第二条与 **W9** 记着的那类现象。

**缺口 B — 今天没有"导入"这个动作，只有"写文件"。**
`WriteRaw(json)` 只调 `_storage.Write(Slot, json)`（`SaveManager.cs:125`），**不碰正在跑的引擎**。
于是：文件被换成导入的那份，而**内存里还是旧状态**；60 秒后自动存档
（`SaveManager.cs:144-151`，由 `SaveManager` 构造时订阅的 `TickEvent` 驱动）
**用旧状态把刚导入的文件盖回去**。也就是说今天这条路的结局是
"报告成功 → 60 秒内自我销毁"，而且全程没有一句话。
**这是本次要补的核心缺口**：导入必须是"校验 → 落盘 → 应用到当前会话"三件事，
而不是其中一件。

**缺口 C — 没有任何尺寸闸。** `WriteRaw` / `FileStorage.Write` 收多大写多大；
`SaveSerializer.Normalize` 还会对整串做一次 `Convert.FromBase64String`（`SaveSerializer.cs:289`）。
一次病态粘贴（几百 MB）在解析之前就已经把内存吃掉了。

**缺口 D — 界面上一个入口都没有。** Web 的「存档」按钮（`index.html:108`）只做
`send("save")`（`app.js:1538`）＝ 手动存一次盘，**与导出/导入无关**。
`ExportShareCode()` 这个公开 API **至今零调用者**（`grep` 只命中它自己与 `PublicApi.txt`）；
终端那边也只有 F5 手动存档（`TerminalUi.cs:596`）、没有导出/导入键。
需求里的「窗口」今天**完全不存在**。

**缺口 E — 内部一致性只到"类型对不对"这一层。** `Parse` 保证结构对得上，
但 `NumberHandling = AllowNamedFloatingPointLiterals`（`SaveSerializer.cs:28`）意味着
`"Cookies": "NaN"` 能被**成功**读成 `double.NaN`；`Era = 0` 被静默修成 1（`SaveSerializer.cs:120`）。

### 1.3 结论：**扩展，不重写**

**"重写"这个词在这里没有落脚点**，理由三条，每条都能复核：

1. **最难写对、也最容易写错的那一半已经写对了、而且有守卫**：写盘的三道闸
   （`IStorage.cs:95-132`）与它的用例（`SaveFileTests.SaveManager_FailedWriteIsLoudAndLosesNothing`
   等，`engine/tests/SaveFileTests.cs:265-315`）。重写它只会把"已经用测试钉住的失败语义"
   换成"一份还没被钉住的实现"——这正是本仓库一贯拒绝的交换。
2. **需求真正的内容是"窗口"，而窗口今天一条都没有**（缺口 D）。把后端推倒重来，
   **窗口的工作量一分不减**，却要额外再验一遍原子性。
3. 缺口的形状是**在既有链路外面加壳**：信封（A）、落盘之后那一步 apply（B）、
   入口处一道尺寸闸（C）、UI（D）、一致性报告（E）。四项全是在既有类型上**新增**，
   `SaveData`、`FileStorage`、`SaveSerializer.CurrentVersion` **一个字都不用动**。

所以：**扩展**。下面 §2~§6 是方案，§7 是切法。

---

## 2. 导出/导入的格式（决定：信封）

### 2.1 形状

```json
{
  "Format": "neko-save",
  "FormatVersion": 1,
  "PackId": "neko",
  "SaveVersion": 1,
  "FrameworkVersion": "1.10.2",
  "ExportedAt": "2026-10-04T02:11:33.412+00:00",
  "Checksum": "sha256:9f2c...（64 位小写十六进制）",
  "Save": "{\"Version\":1,\"Cookies\":1234,...}"
}
```

- **`Format` / `FormatVersion`**：**信封自己**的格式标签与版本。
  它与 `SaveData.Version` 是**两条独立的轴**：信封改形状（比如以后加签名字段）
  只动 `FormatVersion`，存档格式改形状只动 `SaveVersion`。混成一个数字会让
  "我只是换了个包装"和"老存档读不出来了"变成同一件事。
- **`PackId`**：导出时由宿主告诉 `SaveManager`（新增属性 `SaveManager.PackId`）。
  导入时用来**拒绝别的包的存档**（§3 第 5 步）。
- **`SaveVersion`**：导出那一刻存档体内的 `SaveData.Version`。
  导入时**必须与 `Save` 里读出来的版本一致**——信封与内容是同一份事实的两处写法，
  不一致就是有人手改过或拼错了，要响。
- **`FrameworkVersion`**：`ApiVersion.Current`。**只做诊断，不参与任何判定**
  （框架版本与存档兼容性是两套东西，见 `VERSIONING.md` §6 第 3 条）。
- **`ExportedAt`**：给人看的"这是什么时候导的"，也只做诊断。
- **`Checksum`**：`sha256:` + 小写十六进制，**对 `Save` 字段那个字符串的 UTF-8 字节**求hash。
- **`Save`**：**一个字符串**，里面装紧凑的 `SaveData` JSON。

### 2.2 为什么 `Save` 是字符串，而不是嵌套的 JSON 对象

两个形状的可读性几乎一样，代价几乎一样，但**校验和的确凿程度差一个量级**：

| | `Save` 是字符串 | `Save` 是嵌套对象 |
|---|---|---|
| 校验和算的是什么 | **导出时那串字节，一字不差** | 必须先"规范化"（键排序、数字写法归一），否则用 JSON 格式化工具重排一次就会变 |
| 谁可能让它变红 | 只有内容真被改了 | 任何重排空白的工具 |
| 实现 | `SHA256(UTF8(Save))` | 一个递归规范化器 + 每个数字的亲等判断 |

**任何 JSON 格式化工具都不会改写字符串字面量内部**，所以字符串这一版
"用 Notepad++ 格式化一下再导入"**照样过**，而"把 `1900` 改成 `19000`"必红。
规范化那一版要自己保证"数字写法的归一"不出错——而 `RandomState0` 是 `ulong`，
走双精度会**悄悄毁掉 PRNG 状态**。**不给自己造这个坑。**

### 2.3 为什么外面好看、里面紧凑

- **外壳缩进（`WriteIndented = true`）**：外壳是**给人看的**——"这是哪个包的、什么时候导的、
  什么版本"，正是玩家在按下"导入"之前唯一能自己核对的东西。它只有一百多字节。
- **`Save` 紧凑**：它是机器数据，塞进字符串之后换行要写成 `\n`，
  缩进只会把体积翻倍而一个人也读不懂。

### 2.4 尺寸（实测）

仓库里真实存档 `saves/`：**11 个文件**（5 份存档 + 5 份 `.bak` + `ninelines.json`）。
用真实存档逐个跑了一次 `Wrap`（一次性探针，跑完即删；见 §8）：

| 存档 | 本体 | 导出文本 | 增幅 |
|---|---|---|---|
| `lab.json` | 831 | 1495 | +79.9% |
| `cafe.json` | 840 | 1505 | +79.2% |
| `neko.json` | 846 | 1511 | +78.6% |
| `apocalypse.json` | 1516 | 2592 | +71.0% |
| `company.json` | 1517 | 2590 | +70.7% |
| `ninelines.json` | 3718 | 6203 | +66.8% |

**即一份真实存档导出来是 1.5~6.2 KB 的文本，复制粘贴毫无压力。**

**增幅比我原先估的（+7%）大一个量级，原因是 `System.Text.Json` 的默认转义**：
`Save` 是字符串，里面的每一个 `"` 都被写成 `\u0022`（6 个字符），
而不是 `\"`（2 个）。真实存档里引号很多（每个键一对），所以这一项占了开销的大头。
换 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 能把它压到约 +25%，**这里是刻意不换**：
导出文本的下一站是 Web 的 `<textarea>` 与（将来的）别的宿主，
把一份内容里有 `<`/`&` 的文本做成"不转义"的形态，等于给前端埋一个 HTML 注入的口子——
为省 3 KB 换这个风险不划算。这条取舍记录在案，不是疏忽。

**尺寸上限**：`SaveTransfer.MaxTransferChars = 1 MiB`（1,048,576 字符）。
比最大真实存档（3718）大约 **280 倍**，所以正常输入永远碰不到它；
它存在的唯一目的是让一次病态粘贴**在解析之前**就被拒（缺口 C）。

---

## 3. 原子性：不许把一份能用的存档弄坏（本次的心脏）

### 3.1 先说清楚今天已经保证了什么

`FileStorage.Write` 已经给出**文件层面的原子性**（`IStorage.cs:95-132`）：
内容读不回来 ⇒ 抛，**当前存档与备份一个字节都不动**；读得回来 ⇒ `File.Replace` 原子替换，
且把被换下来的那份滚进 `.bak`。所以**"导入失败会不会弄坏存档"这个问题，
答案已经在树上了，而且有 4 条用例**（`SaveFileTests.cs:265-361`）。
导入**不需要**再实现一遍原子替换——**需要的是让"坏输入"在碰到存储之前就被拦住**。

### 3.2 导入的顺序（每一道闸都在下一道之前）

```
① 尺寸闸      text.Length > 1 MiB                      → TooLarge
② 信封闸      是 JSON 对象？Format == "neko-save"？     → NotAnEnvelope
              必填字段在？类型对？                      → CorruptEnvelope
              FormatVersion <= 1？                      → NewerFormat
③ 版本闸      SaveVersion <= CurrentVersion(1)？        → NewerSave
④ 校验闸      sha256(Save 的字节) == Checksum？          → ChecksumMismatch
⑤ 归属闸      envelope.PackId == this.PackId？           → ForeignPack
⑥ 结构闸      SaveSerializer.Parse(Save)（含迁移）        → CorruptSave
⑦ 一致性闸    数字全是有限的？                           → NonFiniteNumbers
              未知 id **只报告不拦**（见 §3.4）
⑧ 落盘        _storage.Write(Slot, Save)   ← 这里才第一次碰磁盘；坏输入到不了这一步
⑨ 应用        SaveSerializer.Apply(engine, data)  ← 落盘成功之后才动当前会话
```

**为什么是"先落盘、再应用"而不是反过来**（这一条是刻意选的，值得写下来）：

- **存档文件是耐久的、引擎状态是易失的。** 两件事都会出错，那么先做哪一件
  决定了"崩在中间"剩下什么：先落盘 ⇒ 崩了之后磁盘上是**新的**，下一次启动读到的就是导入结果
  ——**导入生效了**；反过来 ⇒ 磁盘上还是旧的，玩家看到的是一次"什么都没发生"的导入。
  前者是"成了"，后者是"说不清"。**先让耐久的那份正确。**
- **失败面更小。** 校验全部发生在落盘之前（②~⑦一条都不碰磁盘），
  所以"坏输入"这条路上**引擎与磁盘都一动没动**——这是需求里"绝不损坏能用的存档"最强的形态，
  比"弄坏了再回滚"强，因为回滚本身也会失败。
- 代价是一个**诚实的残余窗口**：⑧成功、⑨抛异常时，磁盘是新存档、内存是旧状态。
  它今天**不可能出现**（`Apply` 只是把已经解析成功的 `SaveData` 拷进一个新 `GameState`，
  `SaveSerializer.cs:98-166` 没有任何 I/O 与可能抛的运算），但**不许假装它不存在**：
  它有自己的类别 `ApplyFailed`，消息里明说"存档已写入、会话没换成"，不吞。

### 3.3 每一个坏输入各自怎么响（消息里都必须带"怎么办"）

| 输入 | 类别 | 结果 |
|---|---|---|
| `null` / 空白 | `NotAnEnvelope` | 拒绝；"什么都没有粘贴进来" |
| 超过 1 MiB | `TooLarge` | 拒绝；报出实际字符数与上限；"一份真实存档只有几 KB" |
| 不是 JSON / 根不是对象 | `NotAnEnvelope` | 拒绝；"这不是本游戏导出的存档文本（它连合法 JSON 都不是 / 根不是对象）" |
| `Format` 标签不对 | `NotAnEnvelope` | 拒绝；报出读到的标签；没有这个字段时说"没有 `neko-save` 信封头" |
| **base64 分享码**（`ExportShareCode()` 的产物） | `NotAnEnvelope` | 拒绝。它不以 `{` 开头，所以走的是"连合法 JSON 都不是"那一支，消息末尾仍然写着"请整份复制导出出来的内容"。**刻意不支持**（§7） |
| 缺 `Checksum` / `Save` 等必填字段，或类型不对 | `CorruptEnvelope` | 拒绝；报出**是哪一个字段** |
| `FormatVersion = 2`（比本版新） | `NewerFormat` | 拒绝；"这份导出文本是更新的版本写的（信封 2 > 1），请更新游戏" |
| `SaveVersion = 2`（比本版新） | `NewerSave` | 拒绝；与 `Parse` 同一句话（`SaveSerializer.cs:221-222`） |
| 校验和对不上 | `ChecksumMismatch` | 拒绝；报出**期望值前 12 位与实际值前 12 位**，并说"文本被改动或被截断过——请重新复制整份" |
| 信封声明的 `SaveVersion` 与 `Save` 里的版本不一致 | `CorruptEnvelope` | 拒绝；两个数字都报出来 |
| 别的包的存档 | `ForeignPack` | 拒绝；报出**对方的包 id 与本会话的包 id** |
| `Save` 里的 JSON 坏 / 结构对不上 | `CorruptSave` | 拒绝；转发 `Parse` 的原话（含"不是合法的 JSON"/"结构与 SaveData 对不上"） |
| 数字是 `NaN` / `±Inf` | `NonFiniteNumbers` | 拒绝；报出**是哪个字段**（`AllowNamedFloatingPointLiterals` 让 `"NaN"` 能过 `Parse`，见缺口 E） |
| 有本包不认识的 id | `Ok`（**只报告**） | 接受；消息里报出**条数与前几个 id**。理由见 §3.4 |
| 存储写不进去（磁盘满 / 权限） | `WriteFailed` | 拒绝；转发存储的原话；引擎与旧存档都没动 |
| 落盘成功但 apply 抛了（今天不可达） | `ApplyFailed` | **报告失败**；"存档已写入、当前会话没换成" |

**所有失败路径都满足两条**：（一）磁盘上的当前存档与 `.bak` **一个字节都不变**；
（二）内存里的会话**一个字段都不变**。守卫就是这么钉的（§5）。

### 3.4 "解析得出但内部不一致"：拒绝什么、只报告什么

- **拒绝**：非有限数字。一份真存档里 `Cookies` 不可能是 `NaN`——它是"内容坏了"，
  而且坏得会让后面每一次结算都变成 `NaN`（传染性的）。
- **只报告、不拒绝**：**未知 id**（建筑 / 升级 / 成就 / 叙事 / 表态 / 结局）。
  这是**刻意的**：未知 id 不被丢弃是本仓库写进 `ARCHITECTURE.md:124` 的既有不变量
  （"内容包临时下线某个建筑时，存档依然无损"），而且未知是个**正常**状态——
  §5.1 记着的那次内容改版真删过表态 id。所以导入**接受**它，但**把条数与 id 报出来**：
  "界面上一片正常、没有任何地方说得出这件事"正是 W9 要修的那个形状。
  报告是这句话的最小修复。

**诚实边界**：`PackId` 是**声明**，不是证明。手改信封能把 id 改成任意值；
导入**无法**证明"这份内容真是这个包的"——内容与存档之间的一致性检查
（比如"每个 id 在本包里都存在"）**不能**当判据，因为未知 id 是合法状态（上一段）。
所以 §5 里那条"不同包的存档"用例验的是**声明不符时会被拦住**，
不是"伪造声明会被识破"。这句话必须写在这里，否则下一个人会以为它比实际更强。

---

## 4. 窗口放在哪

### 4.1 Web：复用已有的 sheet，不新造第二套弹窗

树上的 sheet 只有一套：`.sheet-layer` / `.sheet` / `.sheet-ok` + `sheet-in` 动画，
现由"离线收益"与"表态"两处使用（`index.html:21-54`，并有一行注释明说
"这里没有第二套弹窗"）。导出/导入**必须**共用它——`index.html:31-32` 那句注释
就是这条约定本身。

- 入口：英雄区 meta 行上现有的「存档」按钮（`index.html:108`）**改成打开一个 sheet**
  （而不是直接 `send("save")`），sheet 里两栏：**导出**（只读 textarea + 「复制」
  + 「下载 .json」）与**导入**（textarea + 「导入」按钮 + 结果/失败原因）。
  手动存档那条命令**保留**在 sheet 里（它今天是有用的），不删功能、只换入口。
- 失败一律显示在 sheet 内：`SaveTransferResult.Message` 原样呈现
  （引擎已经把"哪一个字段、期望什么、怎么办"写好了，前端**不要**再翻译一遍——
  与 `GameHost` 那条"用引擎自己的 Message"的约定一致，`GameHost.cs:284-289`）。
- 大文本框粘贴要走 `POST /api/command` 的 `export` / `import` 两条命令，
  1 MiB 的请求体在 ASP.NET Core 默认上限（30 MB）之内，不需要改 Kestrel 配置。

### 4.2 终端：没有 GUI，"窗口"就是一次**路径提示**

终端宿主没有弹窗（`TerminalUi.cs` 是逐帧重绘的字符界面）。**诚实的答案**是：
"窗口"在那里 = 一次**文件路径输入**：

- `E` = 导出：提示一个路径（默认 `saves/<包 id>-export.json`），写信封文本；
- `I` = 导入：提示一个路径，读入 → 走**同一个** `SaveManager.Import`，
  结果与失败原因按终端既有的 `Log(...)` 播报。

它必须走同一个引擎入口，**不许**自己再解析一遍——"两个宿主两套语义"是这一节要避免的事。

### 4.3 第一刀不碰前端的理由

`games/hosts/Web/wwwroot/*` 与 `tools/web-smoke.mjs` **此刻正有另一个 agent 在改**。
第一刀（引擎 API + 守卫）与它**零交集**，可以立刻做完、验完；
第二刀（两个窗口）必须**基于那时的 wwwroot 重做**，不能基于今天这棵树的记忆。

---

## 5. 守卫（必须在第一刀里落地）

新文件 `engine/tests/SaveTransferTests.cs`，**17 条**（落地后的实际形态）：

1. **往返性质**（`SaveTransfer_RoundTripsStateExactly`）：一台引擎灌入一批
   有代表性的状态（货币、建筑、升级、成就、转生、纪元、计数器、元数据、
   增益、场上的金猫、已答表态、待答表态、立场权重、结局、叙事、PRNG 状态字；
   id 一律从**这个包自己的内容**里现取，不写死字符串）
   → `Export()` → **另一台全新引擎** `Import()` → 两边 `engine.Save()` 的文本
   **逐字节相同**。
   比原计划的"逐字段规范化比对"更强：存档文本相等同时覆盖了字段值、字典顺序、
   数字写法与 `RandomState0/1`（`ulong`，过一趟双精度就会红）；
   两台引擎的手动时钟都从 `2024-01-01` 起，所以 `LastSavedAt` 也落在同一刻，
   这条相等是严格的。失败信息只报"第一处不同在第几个字符"，不刷两万字节。
2. **导出的文本自己说得清**：`Format`/`FormatVersion`/`PackId`/`SaveVersion` 都在，
   `Checksum` 恰好等于对 `Save` 字段求出的值，外壳缩进而本体不缩进；顺带打印实测体积。
3. **重排空白不许变红**（`SaveTransfer_ReformattingTheEnvelopeDoesNotBreakTheChecksum`）：
   把导出文本重新紧凑化之后再导入必须成功——这是"校验和算在字符串字节上"这个选择的
   唯一理由。（"改一个数字必红"是另一条用例。）
4. **§3.3 那张表逐行一条用例**，每条断言四件事：
   ① `Ok == false` 且 `Kind` 正确；② 消息里含**那个**关键片段；
   ③ **磁盘上的当前存档与 `.bak` 逐字节不变**（用例开始前读出来存着，结束后比对）
   ——比"再读一次还能读出来"强，因为它连"被换成另一份同样能读的存档"也拦得住；
   ④ **内存里的会话也不许变**。
   为了让 ③ 有意义，每个用例的会话都**存过两次盘**（当前 600 / 备份 555）——
   没有 `.bak` 的话"没动备份"这句话是无从验起的。
5. **未知 id 只报告不拦，而且必须原样留着**：往存档里塞一个 `ghost_building` ⇒ `Ok == true`、
   `UnknownIdCount == 1`、`UnknownIds == ["建筑:ghost_building"]`、消息里列出它，
   而导入之后的 `State.BuildingCounts["ghost_building"] == 7`。
6. **导入之后自动存档不会把导入的成果盖掉**（缺口 B 的正面证明）：
   导入后把自动存档计时走过头、调 `Tick()`，盘上读回来的仍是导入的那份。
7. **`NaN` 逐字段验**（`SaveTransfer_RejectsNonFiniteNumbers`）：对 `SaveData` 上
   **每一个** `double` 字段各造一份 `"NaN"` 存档，各断言一次；
   并且先断言"这份编辑确实被 `Parse` 收下了"——否则这条用例就没在验它以为在验的东西。
8. **清单守卫**（`SaveData_DoubleFieldsAreAllCoveredByTheFiniteCheck`）：
   反射数一遍 `SaveData` 的 `double` 字段，与 `FindNonFinite` 那份**手写清单**比对。
   有人给 `SaveData` 加新数值字段时这条会红——它是那份清单唯一的守卫。
9. **AI 说不了的时候要说出来**（`SaveTransfer_SaysSoWhenThePackCheckDidNotRun`）：
   会话没有包标识时，结果消息里必须出现"没有做跨包检查"。
10. `Wrap` 收到非存档时抛 `InvalidDataException`，不许产出一份自己都不自洽的信封。

**判别力（故障注入）——已做，两处都真跑过**：

① **拿掉归属闸**（`if (PackId is not null && ...)` → `if (false && ...)`）⇒ 只有
`SaveTransfer_RejectsForeignPack` 变红，原文：

```
✗ SaveTransferTests.SaveTransfer_RejectsForeignPack
  AssertionException: 咖啡馆的存档导进 mini 会话：必须拒绝，实际却收下了（已导入：内容包「cafe」｜
  存档格式 1｜框架 1.9.0｜…｜已核对包标识（mini）｜存档里的 id 这个包全都认识。）。
16 通过 / 1 失败（共 17）。
```

注意那条消息里同时写着"内容包「cafe」"与"已核对包标识（mini）"——**这正是"没有闸门时，
消息本身看起来完全正常"的样子**，也说明这句话必须由闸门来保证，而不是由消息来保证。

② **把落盘提到全部判定之前**（在 `Import` 开头插一行 `_storage.Write(Slot, from.Save)`）⇒
3 条红，全部是"被拒绝的导入不许动当前存档"：

```
✗ SaveTransferTests.SaveTransfer_RejectsForeignPack            （存档被换成了咖啡馆那份）
✗ SaveTransferTests.SaveTransfer_RejectsUnreadableSaveBody     （InvalidDataException 从 Import 里漏了出来）
✗ SaveTransferTests.SaveTransfer_RejectsNonFiniteNumbers       （盘上出现了 "Cookies":"NaN"）
14 通过 / 3 失败（共 17）。
```

第 ③ 条红是本次最有价值的一条证据：它**实测**证明了一份 `NaN` 存档能通过
`FileStorage.Write` 的"提交前反解一遍"（因为 `Parse` 收得下 `"NaN"`），
也就是说那道既有的闸**结构性拦不住**这一类——只有导入这道新闸拦得住。

两处注入都已还原，还原后的 `-Strict` 全绿（§8）。

---

## 6. 版本：**加法，minor，1.10.0**（存档格式不动）

- 公开表面**只增不改**：新增 `SaveTransfer` 类型、
  `SaveTransferKind` 枚举、`SaveTransferResult` 记录、`SaveManager.PackId` /
  `SaveManager.Export()` / `SaveManager.Import(string)`。
  按 `VERSIONING.md` §2 的表，这属 **minor**。
- **`SaveSerializer.CurrentVersion` 保持 `1`，不加任何 `ISaveMigration`。**
  `SaveData` 一个字段都没动——信封是**外面**的一层，存档本体与 1.9.0 逐字节同构。
  证据：往返用例里导出的 `Save` 字段与 `engine.Save()` 的输出**完全同文**。
- 因此 **D5 与 W9 这两条没有被本次解决**（它们要的是"真的改一次存档格式"），
  本次**也不该**声称解决了它们。W9 的半个前提（"先造一份真实存档副本"）现在有了工具：
  `Export()` 一份真实存档就是那个副本——但"用真实跨版本存档验一次"仍然欠着。
- 收尾顺序照 `VERSIONING.md` §4：改代码 → 升 `<Version>`/`<AssemblyVersion>`/`<FileVersion>`
  → CHANGELOG 加带日期条目 → **重新构建之后**再跑 `tools/public-api.ps1` → `-Strict` 全绿。
- 同步要改的"当前版本"字样：`README.md`（有守卫：`VersionTests.Readme_AdvertisesTheCurrentVersion`）、
  `engine/README.md`、`engine/docs/VERSIONING.md` 顶部（用 `git grep -n 1\.9\.0` 现数，不靠记忆）。
  **本次没改**的是 `STATUS.md` 里描述"某个历史时刻"的版本行（第 3 / 19 / 28 行三处现在时 1.9.0）——
  它同时也是 D8（1.5.0~1.9.0 的补发）与 W1（"当前用例数"散在 8 处）的题材，
  在这里顺手改会与那两条的处置撞车；已在 `OPEN_WORK` 的 **W15** 里登记成一条独立的欠账。
  **更正（2026-10-04）**：这条原先还写着 `engine/docs/RELEASING.md`，那是**错的**——
  `git grep -n "1\.9\.0\|1\.10\.0" -- engine/docs/RELEASING.md` 只命中第 48 行一条**带日期的历史记录**
  （"1.10.0 落地时漏了它"），那份文档里**没有一行现在时版本行**：它是流程文档，
  它的 §2 列的恰恰是"**别的哪些文件**写版本号"（`Directory.Build.props` 是单一事实来源）。
  所以结论从"两处"改成"一处"，W15 也按这个口径收窄。

---

## 7. 分刀与本次范围

| 刀 | 内容 | 状态 |
|---|---|---|
| **一** | 引擎侧：`SaveTransfer`（信封 + 校验和 + 尺寸闸）、`SaveManager.PackId/Export/Import`、`SaveTransferTests` 17 条、文档、1.10.0 升版 | **✅ 已完成**（`835f8df` 合进 `main`） |
| 二 | Web 窗口：`.sheet-layer` 复用、`export`/`import` 两条命令、`GameHost.PackId = _package.Id`、`web-smoke` 断言 | **✅ 本轮做完**（W13；见 §9） |
| 三 | 终端窗口：`E`/`I` 两个键 + 路径提示、`GameSession.Saves.PackId = package.Id` 接线 | **✅ 本轮做完**（W14；见 §9） |
| 四 | `tools/` 里一条 `--wrap`：把既有的裸 `saves/<包>.json` 包成信封（给"我手上只有旧文件"的人一条路） | 待做（没有真实需求前不做。第二刀/第三刀之后若真人报"我只有旧文件"，它才变成待办） |

**第二刀接线的地方（本轮已改）**：

- `GameHost.cs:117`：`new SaveManager(_engine, storage, $"{package.Id}.json") { PackId = package.Id }`；
- `GameHost` 上新增 `ExportAsync()` / `ImportAsync(string)`（走 `ExecuteAsync`，与 `SaveAsync` 同一形状），
  导出文本走 `CommandOutcome.Text` 回浏览器（新字段；`Message` 那一句仍然是给人看的话），
  `Program.cs` 的 `DispatchAsync` 加 `export` / `import` 两条命令，`/api/command` 的正文多一个 `text` 字段；
- `wwwroot/index.html` + `app.js`：`#save`（`index.html:108` / `app.js` 的事件段）改成开 sheet，
  手动存档那条命令挪进 sheet 里的「立刻存一次盘」；
- `GameSession.cs`：同样的 `PackId` 接线 + `E` / `I` 两个键 + 一行路径提示。

**§7 那三个"留给人的问题"，本轮按方案的选择实现（一个都没改）**：

1. **导入不结算离线收益** —— 引擎侧就是这么做的（`SaveManager.Import` 走 `Apply` 而不是
   `DeserializeInto` 那条补发的路），本轮一个字节都没碰它。
2. **导入之后不立刻再存一次** —— 同上：磁盘上那份**已经**是导入的字节，再存一次只会把
   `LastSavedAt` 刷成现在、并让 `.bak` 变成同一份内容的副本。本轮也没加这一笔。
   （**前端那一侧也没有偷偷补**：导入成功后重新生成的是**导出文本**，不是存档——它是一次读。）
3. **Web 的导出同时给「下载 .json」** —— **给了**（方案的建议），而且它与「复制」给的是
   **同一份字节**（校验和算在文本里，所以两条路等价）；剪贴板写不进去时它是唯一的出路，
   而那一次失败**不会**被写成"已复制"。

**明确不做**（本次）：

- **不改存档格式、不加迁移**（理由见 §6）。
- **不重写 `FileStorage`**（§1.3）。
- **不删 `ExportShareCode()` / `WriteRaw()`**：它们是已发布的公开 API，
  删了就是 major；`ExportShareCode` 的新语义写在 `Export()` 的文档里，让新代码用它。
- **不引入压缩**：最大真实存档 3.7 KB，gzip 省下的那点字节抵不上"人看不懂"的代价。
- **不做签名/防篡改**。这是单机离线游戏，改自己的存档是玩家的正当行为，
  不构成威胁模型；`Checksum` 的职责是**发现意外损坏**，§3.4 末尾已写明这条边界。

**留给人的问题**（原文保留：它们是"第二刀之前要有答案"的那三条）。
**2026-10-04 第二、三刀落地时三条全部按上面的选择实现**，实现位置的证据见本节上一段：

1. 导入的**离线收益要不要结算**？本方案选的是**不结算**——
   导入是"把这局恢复成那个样子"，不是"接上那段时间"；而且导入一份三天前的存档
   会凭空发三天产量，那是白送。若你要"接上"，就是 `DeserializeInto` 那条路，一行之差。
2. 导入之后要不要**立刻再存一次**？本方案选的是**不**：磁盘上那份**已经**是导入的字节，
   再存一次只会把 `LastSavedAt` 刷新成现在（并让 `.bak` 变成同一份内容的副本）。
3. Web 的导出要不要**同时给"下载 .json"**（而只有复制按钮）？
   本方案建议给（浏览器里"复制"在非 HTTPS 的 `127.0.0.1` 上受剪贴板权限影响），
   但那是第二刀的实现细节。→ **给了**，而且两条路给的是同一份字节。

---

## 8. 实测（第一刀，2026-10-03）

同一棵隔离 worktree（`.tmp/wt-save`，从 `1a018d4` 起）、同一个提交：

| 项 | 数字 | 怎么来的 |
|---|---|---|
| 基线用例数 | **531** | 动手之前先跑一次 `tools/build.ps1 -Strict`（未改动的树） |
| 落地后用例数 | **548** = 531 + **17** | 新增 `SaveTransferTests` |
| 公开表面变化 | **+36 项、-0 项** | 快照守卫自己报的（"少了 0 项、多了 36 项"）——**只增不改，所以是 minor** |
| 导出体积 | 真实存档 831→1495 … 3718→6203 字符（+67%~+80%） | 一次性探针逐份量真实 `saves/*.json`，跑完删除（§2.4 有整张表） |
| 故障注入 | 2 处，各自把 1 / 3 条用例变红 | 见 §5 末尾，红色原文已抄在那里 |
| 仓库真实 `saves/` | **本轮一个字节都没写** | 探针只读；`cafe/lab/neko/ninelines` 的 sha256 在全程前后完全一致。另外两份（`apocalypse`/`company`）在期间被**正在服务 5273 的宿主**按 60 秒自动存档重写过——那不是本轮写的，见 `OPEN_WORK` 的登记行 |

**没验的**（诚实边界）：

- **真人眼睛**：任何"窗口"都还不存在（第二刀的事），所以没有一个界面被人看过。
- **`ApplyFailed` 那条路**：如上所述今天不可达，只有代码与消息，没有实测。
- **真实跨版本存档**（W9 要的那件事）：本轮把这个工具做出来了，但没有真的改一次存档格式，
  所以"老存档打不开时长什么样"仍然没验过。
- **包标识是声明不是证明**：没有验"伪造 `PackId` 会不会被识破"——它**不会**，
  这是设计（§3.4 末尾），不是缺陷。

---

## 9. 实测（第二刀 + 第三刀，2026-10-04）

同一棵隔离 worktree（`.tmp/wt-transfer`，从 `d02ab95` 起）、同一个提交 **`2323a2a`**
（分支 `save-transfer-windows-w13-w14`）：三条命令各跑一次，
数字全部是**测出来的**（不是加出来的）。

| 项 | 数字 | 怎么来的 |
|---|---|---|
| 基线（`d02ab95`） | `-Strict` **556** ／ 冒烟 **135** ／ api-test **55** | 上一轮三条分支合并后复测的值（`OPEN_WORK` §0.17） |
| 落地后 | `-Strict` **564**（+8）、冒烟 **162**（+27）、api-test **65**（+10） | 新增 `SaveTransferHostTests` 8 条、`web-smoke` 第 24 节 27 条、api-test 的导出/导入段 10 处 |
| 公开表面 | **一个字节都没动**（`PublicApi.txt` 逐字节相同） | 本轮只改宿主与前端：`GameHost` / `CommandOutcome` / `GameSession` / `TerminalUi` / `wwwroot/*` 都不在公开快照的射程里（快照只扫 `NekoClicker.Core`）⇒ **不升版本**（理由见 `CHANGELOG` 的 `[未发布]`） |
| 故障注入 | 3 处，各自把 1~3 条守卫变红（已全部还原） | ① 摘掉 `{ PackId = package.Id }` ⇒ 宿主用例 6/8（含"别的包的存档被收下了"那句原文）；② 导入失败也弹提示条 ⇒ 冒烟 3 条红；③ 拿掉 `#import-result` 的 `role="status"` ⇒ 实时区域计数那条红 |
| 仓库真实 `saves/` | **本轮一个字节都没写** | 全部用例与探针都指向临时目录；`saves/*.json` 与 `artifacts/latency.txt` 的 size + mtime + sha256 见本节末尾 |

**这一轮真的验到了什么**（每一条都有一条用例）：

- **入口真的存在了**：点「存档」开窗口、`E`/`I` 开路径提示；`save` 这条命令**换了入口而没被删**。
- **坏输入绝不动盘，而且是在宿主这一层动都没动**：三种坏输入（人话 / 别的包 / 校验和对不上）
  在 C#（`SaveTransferHostTests`）与 api-test（真宿主 + 真 HTTP）两处都断言了
  "磁盘文件逐字节不变"；api-test 那一条用的是 `Get-FileHash` 的前后对比。
- **导入是真的三件事**（校验 → 落盘 → 应用）：api-test 里"盘上那份 = 信封里那段 `Save`（逐字节）"
  与"导入后立刻取快照就是导入后的状态"各一条。
- **跨包闸真的拦得住**：cafe 会话拒收 neko 的文本，消息里两个包名都在。
- **校验和扛得住 HTTP 往返**：api-test 自己对 `Save` 字段算一次 sha256，与信封里的值逐字符相同。

**没验的**（诚实边界，与 §8 同一格式）：

- **真人眼睛**：Web 那张窗口**没有一双眼睛看过**（本机结构性起不了浏览器，`OPEN_WORK` §0.6）。
  无头 DOM 桩能证明"真的跑起来了、class 与文字对、命令序列对、不抛异常"，
  **证明不了**它好不好看：42rem 宽的那张 sheet 在窄屏上怎么折、两个文本框的高度合不合适、
  「复制」/「下载」两个按钮并排的观感——这三样只有人能判。终端那一侧的提示行同理
  （它至少有一个"每行恰好 N 列"的机器判据，见 `SaveTransferHostTests`）。
- **`ApplyFailed` 那条路**：仍然不可达（§8 的一条，本轮没变）。
- **真实跨版本存档**（W9）：仍然没做，本轮也没声称做了。
- **剪贴板**：只验了"两条分支各自说什么"（有 API 时写进去的是同一份、没有 API 时明说失败），
  **没有**在真浏览器里点过一次——那需要 HTTPS 或本地回环上的真实权限提示，属于真人那一步。

**真实文件没被动过**（2026-10-04 15:15 量；本轮从 14:45 起）：

| 文件 | size | mtime | sha256 前 16 位 |
|---|---|---|---|
| `saves/apocalypse.json` | 1539 | 2026-10-04 **07:01:21** | `9277E69548805BBD` |
| `saves/cafe.json` | 840 | 2026-10-02 22:32:51 | `AE5C4DC815686EE8` |
| `saves/lab.json` | 831 | 2026-10-02 22:32:51 | `FF7242EE8402E34A` |
| `saves/neko.json` | 846 | 2026-10-02 22:32:51 | `0E2015E22D976956` |
| `saves/ninelines.json` | 3718 | 2026-09-25 22:42:59 | `E4930E5AAB41ECC4` |
| `artifacts/latency.txt` | 2695 | 2026-10-03 21:46:42 | `FB64894FCE9E0A7E` |

判据是 **mtime**：最新的一份存档写着 **07:01**，比本轮开工（14:45）早 **7 小时 44 分**——
也就是说这一轮里没有任何一次写入碰过它们。主树的 `git status` 全程为空；
本轮的改动全部落在隔离 worktree `.tmp/wt-transfer` 里（`.tmp/` 已被 `.gitignore` 忽略）。
另外，本轮跑测试时唯一被创建过的仓库内目录是 `artifacts/latency-tests/`（空目录，由既有的
`TerminalLatencyProbeTests` / `WebChoiceLatencyTests` 建、它们自己删子目录）——
**那份真人埋点文件 `latency.txt` 一次都没被打开过**。
