# 现状

> 记录时间：**2026-09-27** ｜ 分支 `feature/web-frontend-ui` ｜ HEAD `d2c2e5a`
>
> 这份文档只回答一个问题：**现在是什么状况**。它不重复别处已有的内容——
> 架构看 [ARCHITECTURE](engine/docs/ARCHITECTURE.md)，规划与决策看 [ROADMAP](games/docs/ROADMAP.md)，
> 逐版本改动看 [CHANGELOG](CHANGELOG.md)。文档之间冲突时，以本文的"事实"部分为准。

---

## 1. 分支与版本

| 分支 | 内容 | 状态 |
|---|---|---|
| `main` | v1.0.0（`fa4651d`，tag `v1.0.0`） | **未动**。仍是可发布状态 |
| `feature/web-frontend-ui` | `main` + 4 个提交 | 已推送到 origin，**尚未并入 main**，没有 PR |

`main` 与 `feature/web-frontend-ui` 的 4 个提交：

```
d2c2e5a  修：大数字一跳一跳——根因是"追离散目标"，不是刷新率不够
d238825  Web 前端可玩闭环：SSE 增量协议 + 单线程状态所有权 + 零依赖界面
b7cff2f  目录重构：engine/ + games/ 两层，引擎可独立提取
b9a99d9  Web 前端骨架：宿主 + 独立 sln + tools/web.ps1
```

**公开 API 一行未改**，所以还没到发版本的时候（`CHANGELOG.md` 里挂的是 `[未发布]`）。
按 [VERSIONING.md](engine/docs/VERSIONING.md) 的规矩，合进 main 时应当是 **minor**
（新增了宿主与内容发现能力，没有不兼容改动）。

---

## 2. 仓库结构（`b7cff2f` 落地）

```
engine/            自包含：搬走它 + 仓库根的 Directory.Build.props = 独立引擎仓库
  core/            引擎本体（平台中立，可在 Linux/macOS 直接构建）
  content/<包名>/  十一个内容包，一个包一个 csproj
  engine/tests/    421 个用例 + 自研迷你运行器
  docs/            架构 / 内容作者指南 / 版本承诺
games/             旗舰示例作品。依赖 engine/，反向不依赖
  hosts/Demo.Cli/  终端前端，同时是框架回归基线（FrameRenderTests 引用它）
  hosts/Web/       Web 前端
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
dotnet build NekoClicker.sln     # 引擎 + 内容包 + 414 用例（或用 .\tools\build.ps1 -Strict）
.\tools\play.ps1                 # 终端 Demo；--package <id> 换包
.\tools\web.ps1 run              # Web 前端，浏览器开 127.0.0.1:5273
```

Web 前端 `?package=<id>` 换包（缺省用扫描到的第一个）。界面包含：大数字（按每秒产量
**逐帧累加**，不是追 250ms 一跳的目标）、点击、金猫浮层、增益条、**纪元**（唯一的
「舍一命」主按钮 + 进度条 + 卡在哪一项）、**表态**（待答卡片 + 立场轴 + 结局）、
**图鉴**（未读到显示 `???` 但保留条件与进度）、成就、建筑与升级（×1/×10/×100/买满）。
面板切换与 URL hash 同步，`#tab=codex` 可直接分享。

**两个前端共用同一份存档**（`saves/<包 id>.json`），一份进度两边都能接着玩。

---

## 4. 验证基线（改动前后都该守住的）

| 命令 | 期望 |
|---|---|
| `.\tools\build.ps1 -Strict` | **421 个用例全绿**、0 警告 |
| `.\tools\web.ps1 build -Strict` | 0 警告 |

404 条是 `main` 上的基线（阶段 6 交付时定的），10 条是 Web 推送协议的契约测试。
两条命令都要跑——Web 宿主刻意不在 `NekoClicker.sln` 里（理由见
根 README 的「环境说明」），所以 `-Strict` 一次跑不完整个仓库。

**Web 推送协议的契约测试**（`engine/tests/WebSnapshotProtocolTests.cs`）守的是：

- 增量累积出的快照必须与直接取的全量在**协议口径下逐字节相同**
- 字段名必须是 camelCase（`System.Text.Json` 默认不转换命名，忘了就是静默全废）
- `null` 补丁要应用而不是跳过（否则纪元/图鉴面板会一直挂着）
- **按字节数**守住增量协议的前提：一个 tick 的增量必须远小于全量
- 派生字段翻转不能重发整表（`AffordabilityFlip_DoesNotResendTheWholeList`）

---

## 5. 有意没做的（都是决策，不是遗漏）

| 项 | 现状 | 为什么 |
|---|---|---|
| **永久升级线 / 二周目界面** | 引擎支持，Web 未铺 | 先做能玩的最小闭环 |
| **离线收益弹窗** | 只在宿主控制台打印一行 | 还没定呈现方式 |
| **通知日志的"未读"游标** | 前端直接渲染 `notifications` | 关页面期间攒的会一次性显示，尚未处理 |
| **`npm` 工具链** | 不用 | 保持"clone 下来只要有 dotnet 就能跑" |
| **`PackageId` / `IsPackable`** | 没有 | 引擎目前没有 NuGet 分发形态；补它是纯元数据改动，随时可做 |
| **多玩家 / 分槽位存档** | 一个包一个槽位 | 现在只监听回环地址、单机单人 |
| **美术与音效** | emoji + CSS | ROADMAP 的 non-goal |

---

## 6. 已知边界与不可靠处（诚实清单）

- **文档重复**：本文件与 `README.md` 的「状态」一节、`ROADMAP.md` 的交付记录有重叠。
  冲突时以本文的"事实"部分为准。
- **观感未经验证**：数字平滑那一版（`d2c2e5a`）只做了代码级根因定位 + 端到端冒烟，
  **没有用人眼确认过**——本机浏览器自动化（`bsk`）存在协议版本漂移
  （扩展 1.3 vs daemon 1.0），非交互升级走不通。
- **端到端回归还没进仓库**：目前的端到端探针在 `.tmp/`（已 gitignore）。
  它是唯一的端到端手段，应当收成 `tools/api-test.ps1`。
- **`engine/tests/ArchitectureTests.cs` 直接枚举内容包**：A1 守卫以内容包为探针，
  搬走内容包就失去判别力。将来真要分离引擎仓库，补救方向是改用极小的合成测试包
  （沿用阶段 3A"全部用合成内容测试"的既有做法）。
- **CI 不存在**：没有 GitHub Actions。上面两条命令目前只能靠人跑。
  这也意味着 README 里"公开 API 只增不改"的承诺在远端没有自动化执法点。
- **远端连通性不稳定**：`push` / `ls-remote` / `fetch` 都可能失败（实测一次成功
  紧接着一次 `ls-remote` 失败）。**不要用单次成功或失败判断远端状态**，要带重试。

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
5. **Web 宿主必须 `dotnet run` 启动**：`wwwroot` 在开发期由 staticwebassets 清单解析到
   **源码目录**，只有 publish 才复制进 `bin`。直接起 exe 会让 `/api/*` 全正常而首页 404，
   编译期 0 警告 0 错误。
6. **Web 项目覆盖了 `TargetFramework` 为 net10.0 与 `LangVersion`**：本机只有
   ASP.NET Core 10.x 共享框架，net8.0 的 Web 项目编得过但跑不起来。引擎与内容包
   一律留在 net8.0 / C# 12——所以公开 API 与快照守卫不受影响。
7. **`git` 报 "dubious ownership"**，用
   `$env:GIT_CONFIG_COUNT=1; GIT_CONFIG_KEY_0="safe.directory"; GIT_CONFIG_VALUE_0="D:/githb/neko-clicker"` 绕过。

---

## 8. 下一步的候选（按我的建议排序）

1. **把端到端探针收成 `tools/api-test.ps1`** —— 现在它是唯一的端到端手段却躺在 `.tmp/` 里，
   最容易被误删。
2. **加 CI**（GitHub Actions 跑 `build.ps1 -Strict`）—— 让"414 条全绿"和
   "公开 API 只增不改"在远端有执法点，而不是只靠人跑。
3. **铺完剩下的界面**：永久升级线、二周目、离线收益弹窗、通知未读游标。
4. **补 `PackageId` / `IsPackable`** —— 给引擎留一条真正的分发路径（纯元数据，不动公开 API）。

