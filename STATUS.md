# 现状

> 记录时间：**2026-10-01** ｜ 分支 `main` ｜ 当前版本 **1.2.0**
>
> 这份文档只回答一个问题：**现在是什么状况**。它不重复别处已有的内容——
> 架构看 [ARCHITECTURE](engine/docs/ARCHITECTURE.md)，规划与决策看 [ROADMAP](games/docs/ROADMAP.md)，
> 逐版本改动看 [CHANGELOG](CHANGELOG.md)。文档之间冲突时，以本文的"事实"部分为准。
>
> **本文刻意不写 HEAD。** 上一版把分支名与 HEAD 写死在标题里，两天后全部过期
> （那个分支早就并进了 main）。要精确到提交，用 `git log -1`——文档不该跟提交哈希赛跑。

---

## 1. 分支与版本

| 分支 / tag | 内容 | 状态 |
|---|---|---|
| `main` | **唯一的开发线** | 可发布 |
| `v1.2.0` | 1.1.0 + Web 前端 + 两层重构 + 剧情外部化试点 | 当前版本 |
| `v1.1.0` | 结局作答宽限期 + 套件并行化 | 已发布 |
| `v1.0.0` | 首个承诺公开 API 稳定的版本 | 已发布 |
| `feature/web-frontend-ui` | 旧分支，已并入 `main` | **作废**（远端还在，可删） |

**当前版本 `1.2.0`**（`NekoClicker.Core` 的公开 API 版本）。相对 1.1.0 **只新增了
`ContentText`**（剧情散文的外部化载体），没有不兼容改动 → 按
[VERSIONING](engine/docs/VERSIONING.md) 是 **minor**。

> **一条已作废的策略**，写在这里免得再被引用：原定"`main` 停在 v1.0.0、加前端一律走
> `feature/web-frontend-ui` 分支"。2026-10-01 协作者把该分支合并进 main 并在主线继续，
> 用户也在 main 上做，所以**现在 main 就是开发线**。仍然有效的两条长期约束是：
> **公开 API 冻结**（只增不改，由快照 + 版本守卫执法）与**尽量不碰 `engine/core/`**
> （用户原话是"这个引擎不要动它，就作为一个开源引擎项目"）。

---

## 2. 仓库结构（`b7cff2f` 落地，`e9415ca` 并入 main）

```
engine/            自包含：搬走它 + 仓库根的 Directory.Build.props = 独立引擎仓库
  core/            引擎本体（平台中立，可在 Linux/macOS 直接构建）
  content/<包名>/  十一个内容包，一个包一个 csproj
  engine/tests/    433 个用例 + 自研迷你运行器
  docs/            架构 / 内容作者指南 / 版本承诺
games/             旗舰示例作品。依赖 engine/，反向不依赖
  hosts/Demo.Cli/  终端前端，同时是框架回归基线（FrameRenderTests 引用它）
  hosts/Web/       Web 前端（可玩；自带独立 sln，build.ps1 会一起编）
  docs/            路线图 / 世界观 / 包规格 / 换皮手册
README.md          刻意留在仓库根
CHANGELOG.md       刻意留在仓库根（VersionTests 的两个守卫直接读这两个文件）
```

**内容包为什么在 `engine/` 而不在 `games/`**：它们是引擎的集成测试探针——
`ArchitectureTests` 靠它们证明"核心不认识内容"，`EraTests` / `LoreTests` / `PrestigeTests`
的通用守卫靠横扫它们抓沉默失败。放引擎侧，`engine/` 才能自包含、才能被整体提取。

---

## 3. 现在能做什么

```powershell
start.cmd                    # 一体化入口（双击也行）：终端 UI + 默认内容包
start.cmd web                # 浏览器那一侧，自动打开 http://127.0.0.1:5273
start.cmd play lab           # 终端 + 指定内容包
start.cmd list               # 列出全部内容包
```

走脚本的话：

```powershell
.\tools\build.ps1 -Strict    # 一条命令：引擎 + 内容 + 433 用例 + Web 宿主（0 警告）
.\tools\play.ps1             # 终端 Demo；--package <id> 换包
.\tools\play.ps1 --package lab --simulate 21600 --auto   # 无头跑图 + 数值报告
.\tools\web.ps1 run          # Web 前端（开发期必须 dotnet run 起，见 §7）
.\tools\pack.ps1             # 打出 artifacts/neko-clicker-<版本>-win-x64.zip
```

**Web 前端**（`?package=<id>` 换包，缺省用扫描到的第一个）：大数字按每秒产量**逐帧累加**
（不是追 250ms 一跳的目标）、点击、金猫浮层、增益条、**纪元**（唯一的「舍一命」主按钮 +
进度条 + 卡在哪一项）、**表态**（待答卡片 + 立场轴 + 结局）、**图鉴**（未读到显示 `???`
但保留条件与进度）、成就、建筑与升级（×1/×10/×100/买满）。
面板与 URL hash 同步，`#tab=codex` 可以直接分享。
**两个前端共用同一份存档**（`saves/<包 id>.json`），一份进度两边都能接着玩。

**调试门**：`/?package=<id>&password=<密钥>&epoch=<层号>` 把这一局直接置到第 N 层。
密钥只来自环境变量 `NEKO_DEBUG_KEY`（不设就**不存在**这道门）；跳层会话**不写存档**。
方案见 [WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。

**剧情现在是可读文件**（试点）：内容包 #3《猫娘实验室》的 40 条图鉴散文在
`engine/content/Lab/text.json`，代码里只剩条件、序号与频道——改一个错字不用重编。
其余 9 个包仍在 C# 里（见 §5）。

---

## 4. 验证基线（改动前后都该守住的）

| 命令 | 期望 |
|---|---|
| `.\tools\build.ps1 -Strict` | **433 个用例全绿**、0 警告（主 sln 与 Web sln 都编） |
| `.\tools\web.ps1 build -Strict` | 只编 Web 宿主时用（0 警告） |

433 的构成：404（阶段 6 基线）+ 7（合并时的守卫）+ 10（Web 推送协议契约）
+ 9（`ContentText` 加载器）+ 3（Lab 文本运行期守卫——它替换掉的那条"逐字比对"
已在搬迁完成后按设计退役）。

**两条最容易被误伤的守卫**：

- `WebSnapshotProtocolTests` 守 Web 推送协议：增量累积出的快照必须与直接取的全量在
  **协议口径下逐字节相同**、字段名必须 camelCase、`null` 补丁要应用而不是跳过、
  以及**按字节数**守住"一个 tick 的增量远小于全量"
  （派生字段翻转不能重发整表：`AffordabilityFlip_DoesNotResendTheWholeList`）。
- `LabTextFileTests` 守剧情外部化的装配：输出目录里的 `text.json` 与仓库里那份**逐字节相同**、
  代码里每条散文都等于文件里的对应字段、把文件改坏（删一条 / 多一条）真的会红——
  而原样读一遍一条都不抛。

Web 宿主不在 `NekoClicker.sln` 里（它是独立的单项目 sln），但 `build.ps1` 会**两条都编**，
所以上面第一条命令就是全仓库的验收。

---

## 5. 有意没做的（都是决策，不是遗漏）

| 项 | 现状 | 为什么 |
|---|---|---|
| **其余 9 个内容包的剧情外部化** | 只有 Lab 试点完成 | 先用一个包验证接口；剩下 9 个包中，末世 / 图书馆 / 神明 / 文明 / 赛博 / 梦境的 `Lore.cs` 用的是对象初始化器写法，`tools/extract-lore-text.ps1` 要先扩展 |
| **永久升级线 / 二周目界面** | 引擎支持，Web 未铺 | 先做能玩的最小闭环 |
| **离线收益弹窗** | 只在宿主控制台打印一行 | 还没定呈现方式 |
| **通知日志的"未读"游标** | 前端直接渲染 `notifications` | 关页面期间攒的会一次性显示 |
| **`npm` 工具链** | 不用 | 保持"clone 下来只要有 dotnet 就能跑" |
| **`PackageId` / `IsPackable`** | 没有 | 引擎目前没有 NuGet 分发形态；补它是纯元数据改动，随时可做 |
| **多玩家 / 分槽位存档** | 一个包一个槽位 | 现在只监听回环地址、单机单人 |
| **美术与音效** | emoji + CSS | ROADMAP 的 non-goal |

---

## 6. 已知边界与不可靠处（诚实清单）

- **文档重复**：本文件与 `README.md` 的「状态」一节、`ROADMAP.md` 的交付记录有重叠。
  冲突时以本文的"事实"部分为准。
- **观感没有留下验证记录**：Web 界面的数字平滑那一版只做过代码级根因定位 + 端到端冒烟，
  本机浏览器自动化（`bsk`）存在协议版本漂移（扩展 1.3 vs daemon 1.0），非交互升级走不通。
  "好不好看"这件事目前靠人看，仓库里没有守卫。
- **端到端回归还没进仓库**：端到端探针躺在 `.tmp/`（已 gitignore，随时可能被删），
  应当收成 `tools/api-test.ps1`。
- **CI 不存在**：没有 GitHub Actions。上面那条命令目前只能靠人跑，
  也就意味着"公开 API 只增不改"这条承诺在远端没有自动化执法点。
- **`engine/tests/ArchitectureTests.cs` 直接枚举内容包**：A1 守卫以内容包为探针，
  搬走内容包就失去判别力。将来真要分离引擎仓库，补救方向是改用极小的合成测试包
  （沿用阶段 3A"全部用合成内容测试"的既有做法）。
- **远端连通性不稳定**：`push` / `ls-remote` / `fetch` 都可能失败（实测要重试到第 4 次）。
  **不要用单次成功或失败判断远端状态**，要带重试。
- **这份文档自己曾经是错的**：上一版写于分支上、合并后没跟着改，于是长期宣称
  "main 还在 v1.0.0、前端未合并"。这次刻意不写 HEAD，就是为了别再犯同一个错。

---

## 7. 环境硬约束（换机器前先读）

这些不是偏好，是"不这么做就跑不起来"：

1. **`tools/*.ps1` 必须存成 UTF-8 with BOM。** 无 BOM 时 Windows PowerShell 5.1 按 GBK
   解析中文，报 `string is missing the terminator`。
2. **本机没有 `pwsh`**，只有 Windows PowerShell 5.1。读 UTF-8 文件要 `-Encoding UTF8`；
   `[Text.Encoding]::Latin1` 不存在（用 `GetEncoding(28591)`）。
3. **`dotnet build` 必须 `-m:1`**：受限环境下命名管道被禁，多节点 MSBuild 会**静默失败**
   （输出 `Build FAILED` 却 0 Error）。`tools/dnet.ps1` 已经处理。
4. **NuGet 在这台机器上不可用**（`NU1301`，TLS 拦截：npm 走得通而 .NET 证书链过不去）。
   所以引擎坚持零第三方依赖不是洁癖，是前提。
5. **沙箱为 workspace-write 时 MSBuild/Roslyn 被拦**（`MSB3883 ... 拒绝访问`，
   0 Warning / 2 Error）——任何源码改动都编不了，需要更宽的沙箱才能构建。
6. **Web 宿主开发期必须 `dotnet run` 起**：`wwwroot` 由 staticwebassets 清单解析到
   **源码目录**，只有 publish 才复制进 `bin`。直接起 `bin` 里的 exe 会让 `/api/*` 全正常
   而首页 404，且编译期 0 警告 0 错误。**发布产物不受此限**（已实测直接起 publish 的 exe：
   `/api/ping`、`/`、`/app.js` 全 200）。
7. **Web 宿主与全仓库同一个目标框架（net8.0）**：合并期间它曾单独覆盖成 net10.0，
   已撤销（本机只有 SDK 8.0.303，net10 既编不过也没必要），`LangVersion` 也一并对齐。
8. **`git` 报 "dubious ownership"**，用
   `$env:GIT_CONFIG_COUNT=1; GIT_CONFIG_KEY_0="safe.directory"; GIT_CONFIG_VALUE_0="D:/githb/neko-clicker"` 绕过。

---

## 8. 下一步的候选（按我的建议排序）

1. **其余 9 个包的剧情外部化** —— 接口已被 Lab 验证，剩下是机械劳动；
   先扩展 `tools/extract-lore-text.ps1` 支持对象初始化器写法（6 个包），再逐包迁移。
2. **把端到端探针收成 `tools/api-test.ps1`，并加 CI** —— 前者现在躺在 `.tmp/` 里，
   最容易被误删；后者让"433 条全绿"和"公开 API 只增不改"在远端有执法点。
3. **铺完剩下的界面**：永久升级线、二周目、离线收益弹窗、通知未读游标。
4. **补 `PackageId` / `IsPackable`** —— 给引擎留一条真正的分发路径（纯元数据，不动公开 API）。
