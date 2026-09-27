# 变更日志

本项目的版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。
**版本号指的是 `NekoClicker.Core` 对外承诺的公开 API 版本**，不是内容包或 Demo 的版本。

什么改动升哪一位、破坏性变更要走什么流程，见 [engine/docs/VERSIONING.md](engine/docs/VERSIONING.md)。

---

## [未发布]

分支 `feature/web-frontend-ui`。**公开 API 一行未改**——所以还没到发版本的时候。

### 新增

- **Web 前端可玩闭环** `games/hosts/Web/`：浏览器里能真正玩起来。
  - **界面**：大数字（每帧插值平滑）、点击、金猫浮层（抓到有奖）、增益条、
    **纪元面板**（唯一的「舍一命」主按钮 + 进度条 + 卡在哪一项）、**表态**（待答卡片 +
    立场轴 + 结局）、**图鉴**（按剧情线分组，未读到显示 `???` 但保留条件与进度）、
    成就、建筑与升级列表（批量档位 ×1 / ×10 / ×100 / 买满）。
    面板切换与 URL hash 双向同步，于是 `#tab=codex` 可以直接发给别人。
  - **零前端依赖**：手写 ES 模块 + 一份 CSS，`wwwroot/` 直接签进仓库。
    没有 npm、没有打包步骤——"clone 下来只要有 dotnet 就能跑"这条前提保持不变。
  - **推送协议** `SnapshotProtocol.cs`：SSE 推「信封 + 变化字段」。
    **实测：全量 56.9 KB，增量均 1.99 KB，省 97%**（见下面「这一版学到的三件事」）。
  - **单线程状态所有权** `GameHost.cs`：一条专用线程独占 `GameEngine`（引擎是单线程可变对象，
    而 ASP.NET Core 用线程池——连点两下就是两个并发请求），HTTP 命令走 `Channel` 投递。
  - **内容包运行时发现** `PackageCatalog.cs`：扫描输出目录里的 `NekoClicker.Content.*.dll`，
    宿主里没有任何包名字面量。`?package=<id>` 换包，缺省用目录里第一个。
  - **存档**：复用引擎自带的 `FileStorage`（已是"先写 .tmp 再原子替换"）与 `SaveManager`，
    槽位与终端 Demo 同构（`saves/<包 id>.json`），于是**同一份存档两个前端都能接着玩**。
    关停时经 `ApplicationStopping` 强制存档一次。
- **`tools/web.ps1`**：`build` / `run` / `clean`，环境重定向与 `tools/dnet.ps1` 一致。
- **协议契约测试** `engine/tests/WebSnapshotProtocolTests.cs`（**10 条**，用例 404 → 414）：
  增量累积必须与全量在协议口径下逐字节相同、字段名必须是 camelCase、
  `null` 补丁要应用而不是跳过、跨纪元仍能还原、派生字段翻转不能重发整表、
  以及**按字节数**守住增量协议的前提。

### 变更

- **仓库重构成两层**：`engine/`（引擎：`core/` + `content/` 十一个内容包 + `tests/` + `docs/`）
  与 `games/`（旗舰示例作品：`hosts/` 两个前端宿主 + `docs/` 世界观与路线图）。
  这只是移动文件，**引擎行为零改动**。
  - `engine/` 是**自包含**的：整个目录搬走，配合仓库根的 `Directory.Build.props`
    就是一个能独立构建的引擎仓库。内容包之所以放在引擎侧而不是作品侧，是因为它们是
    引擎的集成测试探针（`ArchitectureTests` 靠它们证明"核心不认识内容"，
    `EraTests` / `LoreTests` / `PrestigeTests` 的通用守卫靠横扫它们抓沉默失败）。
  - 文档按同一把刀切开：`engine/docs/`（架构、内容作者指南、版本承诺）与
    `games/docs/`（路线图、世界观、包规格、换皮手册）。`README.md` 与 `CHANGELOG.md`
    刻意留在仓库根——版本守卫直接读它们。
  - `NekoClicker.sln` 的解决方案文件夹同步为 `engine` / `games` / `tests`。
- `games/hosts/Web/NekoClicker.Web.csproj` 除 `TargetFramework` 外还覆盖了 `LangVersion`
  （`Directory.Build.props` 钉在 C# 12.0，而 net10.0 上的 `System.Threading.Lock` 要 C# 13）。
  只有这个宿主项目跟着自己的框架走，引擎与内容包一律留在 C# 12.0 / net8.0。
- `.gitignore` 增补 web 前端相关（`.pnpm-store/`、`node_modules/`、`.tmp/`）。

### 这一版学到的三件事（都是实测，不是推理）

1. **"挂机时只推变化字段"要成立，得先把派生字段处理掉。**
   第一版按顶层字段做 diff，结果每次推送都带上 28 KB：不是因为有东西真的变了，
   而是 `canAfford`（48 条升级各自的"买得起"）、`unlockProgress` / `progress`（比例值）
   随产量每帧在小数点后第 5 位以后漂移。**当时的用例断言"变化字段数 ≤ 4"，照样是绿的**
   ——所以现在的判据是**字节数**：一个 tick 的增量必须小于全量的 5%。
   修法是三类：派生量不进增量（`canAfford` 由前端推）、量化后比较（比例值到 1%）、
   派生文本不进增量（`progressText` / `effectSummary`）。
2. **`System.Text.Json` 默认不转换命名**，`PropertyNamingPolicy` 忘了设就会输出
   `Cookies` 而不是 `cookies`。前端按 `cookies` 取值，于是服务端一切正常、浏览器上什么都不动。
   这一条是被契约测试 `FieldNames_AreCamelCase` 抓住的。
3. **`wwwroot` 在开发期由 Web SDK 的 staticwebassets 清单解析到源码目录，只有 publish 才复制进
   `bin`**。所以必须 `dotnet run` 启动（或发布产物）；直接起 `bin` 里的 exe 会让 `/api/*`
   全部正常而首页 404，且编译期 0 警告 0 错误。

---

## [1.0.0] - 2026-09-27

第一个承诺 API 稳定的版本。这一版**没有改任何游戏行为**——它做的是把"框架可以被人依赖"
这句话从主张变成可执行的东西：定下版本号、给公开 API 上了快照守卫、写清破坏性变更的规矩。

### 新增

- **版本号单一事实来源**：`Directory.Build.props` 的 `Version` 同时成为程序集版本、
  文件版本与公开 API 承诺版本，不再散落在多处。
- **`ApiVersion`**（`NekoClicker.Core`）：宿主可以在运行时读到框架版本，
  用于"关于"面板、写进日志或存档，不必再靠猜用户报的 bug 出在哪个版本上。
- **公开 API 快照守卫**：
  - `engine/core/PublicApi.txt`——1897 行的公开表面快照，嵌进 `NekoClicker.Core.dll`，
    随 dll 一起走（任何宿主都能拿它校验自己手上的二进制）。
  - `PublicApiTests`——三条用例：快照必须逐项一致、快照必须覆盖每个公开类型与成员
    （防止守卫自己瞎掉）、快照记录的版本必须等于当前版本。
  - `PublicApiGuard_RejectsEveryKindOfBreakingChange`——故障注入用例，
    用五类真实改动（删成员 / 改签名 / 只新增 / 收紧可空标注 / 纯重排序）
    证明这条守卫真的会红，而不是永远绿。
  - `VersionTests`——版本号与程序集元数据、API 快照、CHANGELOG 三方对齐。
- **`tools/public-api.ps1`**：有意改动公开 API 后重新生成快照（流程的最后一步）。
- **`engine/docs/VERSIONING.md`**：版本语义、破坏性变更定义、发布检查清单、快照怎么用。
- **`.gitattributes`**：快照强制 LF，保证跨平台生成的快照逐字节一致。

### 变更

- `engine/docs/CONTENT_AUTHORING.md` 增补一节：内容包作者何时会碰到公开 API 的变化。
- `README.md` 增补版本与兼容性承诺一节；用例数 396 → 404。
- `games/docs/ROADMAP.md` 增补"阶段 6：产品化"交付记录。

### 兼容性

**这是首个正式版本，不存在"从哪个版本升级"的问题。** 从这一版起：

- 公开 API **只增不改**；只新增成员的版本是 minor。
- 删除或修改任何公开成员、收紧可空标注、改变已有行为的语义 → major。
- 内容包与 Demo 不受影响：本次改动没有触碰游戏逻辑，
  十一个内容包的行为与 396 条既有用例全部保持不变。

---

## 历史版本（1.0.0 之前）

1.0.0 之前的开发没有版本号，用阶段（stage）记录。
完整交付历史见 [games/docs/ROADMAP.md](games/docs/ROADMAP.md) 与 git 历史，摘要如下：

| 阶段 | 交付 |
|---|---|
| 0 | 内容包 #1《猫娘咖啡馆》+ 架构不变量测试 + Demo `--package` |
| 1 | 引擎能力 S-A（`Era` 分层转生）+ 内容包 #2《九命轮回》 |
| 2 / 2.6 / 2.7 | 引擎能力 S-B（叙事释放 + 图鉴）+ 两包 101 条条目；叙事节奏重排 |
| 3A~3C | 引擎能力 S-C（选择 + 立场轴）+ 终局判定 + #3 实验室 + #10 公司 |
| 4A / 4B / 4C | 跨转生继承 + #6 末世；虚无化 + #9 图书馆；两个跨包专项 |
| 5 | 四个换皮包：#7 神明 / #4 文明 / #5 赛博 / #8 梦境（核心零改动） |
| 阶段 5 之后 | 补齐 R3 运行期守卫、消灭手写包清单（用例 394 → 396） |
