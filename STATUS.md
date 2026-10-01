# 现状

> 记录时间：**2026-10-01** ｜ 分支 `main` ｜ 当前版本 **1.2.1**
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
| `v1.2.1` | 1.2.0 + 421 条散文铺满十个包 + `ContentText` 并发修复 | **当前版本** |
| `v1.2.0` | 1.1.0 + Web 前端 + 两层重构 + 剧情外部化试点 | 已发布（已知含一个并发缺陷） |
| `v1.1.0` | 结局作答宽限期 + 套件并行化 | 已发布 |
| `v1.0.0` | 首个承诺公开 API 稳定的版本 | 已发布 |
| `feature/web-frontend-ui` | 旧分支，已并入 `main` | **作废**（远端还在，可删） |

**当前版本 `1.2.1`**（`NekoClicker.Core` 的公开 API 版本）：**patch**——公开 API
一行没动，改的是剧情散文的存放位置（421 条铺满十个包）与 `ContentText` 里那个并发缺陷。
上一版 1.2.0 相对 1.1.0 只新增了 `ContentText`（剧情散文的外部化载体），没有不兼容改动
→ 按 [VERSIONING](engine/docs/VERSIONING.md) 是 **minor**。

> **为什么补发 1.2.1**：`v1.2.0` 的 tag 里没有那个并发修复——拿到 1.2.0 的人手里的
> `ContentText` 是"多线程用了就可能炸"的类型（实测 1200 次构建里 522 次抛异常）。
> 发布流程与这次的执行记录见 [RELEASING](engine/docs/RELEASING.md)。
>
> **`[未发布]` 里现在挂着**：Web 日志面板（第六个面板，`games/hosts/Web/`——
> 宿主侧改动，**公开 API 一行没动**；只发它时按 patch）。

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
  engine/tests/    435 个用例 + 自研迷你运行器
  docs/            架构 / 内容作者指南 / 版本承诺 / 发布流程（RELEASING）
games/             旗舰示例作品。依赖 engine/，反向不依赖
  hosts/Demo.Cli/  终端前端，同时是框架回归基线（FrameRenderTests 引用它）
  hosts/Web/       Web 前端（可玩；自带独立 sln，build.ps1 会一起编）
  docs/            路线图 / 世界观 / 包规格 / 换皮手册
README.md          刻意留在仓库根
CHANGELOG.md       刻意留在仓库根（VersionTests 的两个守卫直接读这两个文件）
tools/             全部 ps1 工具（含 api-test.ps1：端到端起真宿主打端点）
.github/workflows/ci.yml  CI：两条命令 = 引擎验收 + 端到端
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
.\tools\build.ps1 -Strict    # 一条命令：引擎 + 内容 + 435 用例 + Web 宿主（0 警告）
.\tools\play.ps1             # 终端 Demo；--package <id> 换包
.\tools\play.ps1 --package lab --simulate 21600 --auto   # 无头跑图 + 数值报告
.\tools\web.ps1 run          # Web 前端（开发期必须 dotnet run 起，见 §7）
.\tools\api-test.ps1         # 端到端：起真宿主、打一遍全部端点（含 SSE），约 15 秒
.\tools\pack.ps1             # 打出 artifacts/neko-clicker-<版本>-win-x64.zip
```

**Web 前端**（`?package=<id>` 换包，缺省用扫描到的第一个）：大数字按每秒产量**逐帧累加**
（不是追 250ms 一跳的目标）、点击、金猫浮层、增益条、**纪元**（唯一的「舍一命」主按钮 +
进度条 + 卡在哪一项）、**表态**（待答卡片 + 立场轴 + 结局）、**图鉴**（未读到显示 `???`
但保留条件与进度）、成就、**日志**（引擎的通知：成就 / 买升级 / 增益 / 金猫 / 舍命 / 存档失败）、
建筑与升级（×1/×10/×100/买满）。
面板与 URL hash 同步，`#tab=codex` 可以直接分享。
**两个前端共用同一份存档**（`saves/<包 id>.json`），一份进度两边都能接着玩。

**调试门**：`/?package=<id>&password=<密钥>&epoch=<层号>` 把这一局直接置到第 N 层。
密钥只来自环境变量 `NEKO_DEBUG_KEY`（不设就**不存在**这道门）；跳层会话**不写存档**。
方案见 [WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。

**剧情现在是可读文件**：十个有剧情的包（咖啡馆 / 九命 / 实验室 / 公司 / 末世 / 图书馆 /
神明 / 文明 / 赛博 / 梦境）的**全部 421 条图鉴散文**住在各自目录下的 `text.json` 里，
代码里只剩条件、序号、频道与条数——改一个错字不用重编，丢一条 / 多一条 / id 对不上
都会在启动时当场抛。示例包「猫咖物语」没有图鉴，所以没有文本文件。
搬迁的保真判据：迁移前后各把全部包的图鉴文字 dump 一次，**176,708 字节逐字节相同**。
完整过程（含路上抓到的那个并发缺陷）见 [TEXT_AS_DATA_PLAN](engine/docs/TEXT_AS_DATA_PLAN.md) §8；
写新包的形态见 [CONTENT_AUTHORING](engine/docs/CONTENT_AUTHORING.md) §12.0。

---

## 4. 验证基线（改动前后都该守住的）

| 命令 | 期望 |
|---|---|
| `.\tools\build.ps1 -Strict` | **435 个用例全绿**、0 警告（主 sln 与 Web sln 都编） |
| `.\tools\api-test.ps1` | **26 项端到端检查全通过**——真起宿主（`dotnet run`）、真读 SSE 流，收尾自己清进程 |
| `.\tools\web.ps1 build -Strict` | 只编 Web 宿主时用（0 警告） |

435 的构成：404（阶段 6 基线）+ 7（合并时的守卫）+ 10（Web 推送协议契约）
+ 9（`ContentText` 加载器）+ 1（并发读写同一份文本）+ 4（剧情文本的运行期守卫；
它替换掉的那条"逐字比对"已在搬迁完成后按设计退役）。

**三条最容易被误伤的守卫**：

- `WebSnapshotProtocolTests` 守 Web 推送协议：增量累积出的快照必须与直接取的全量在
  **协议口径下逐字节相同**、字段名必须 camelCase、`null` 补丁要应用而不是跳过、
  以及**按字节数**守住"一个 tick 的增量远小于全量"
  （派生字段翻转不能重发整表：`AffordabilityFlip_DoesNotResendTheWholeList`）。
- `ContentTextFileTests` 守剧情外部化的装配（覆盖**全部**已外部化的包）：输出目录里的
  `text.json` 与仓库里那份**逐字节相同**、代码里每条散文都等于文件里的对应字段、
  把文件改坏（删一条 / 多一条）真的会红——而原样读一遍一条都不抛；
  另有一条覆盖度用例盯着"有 text.json 的包"与"守卫表里的包"必须一致。
- `ContentTextTests.ConcurrentReaders_DoNotCorruptTheUsedSet` 守 1.2.0 那个并发缺陷
  （同一份文本被多个线程同时读）。它自己第一版是橡皮图章（只两个键、撞不出扩容竞态），
  被故障注入抓出来后改成 200 条 × 16 线程 × 30 轮。

Web 宿主不在 `NekoClicker.sln` 里（它是独立的单项目 sln），但 `build.ps1` 会**两条都编**，
所以上面第一条命令就是全仓库的验收；`api-test.ps1` 再补上"编得过"证明不了的那一半。

**`api-test.ps1` 打的是哪 26 项**（判据只有一条：**真的通**）：静态文件三件（`/`、`/app.js`、
`/app.css`，含 needle 检查——"首页 404 而 `/api/*` 全正常"是这一层最经典的沉默失败）、
**前端面板两件**（首页里有日志面板挂载点、`app.js` 里有渲染函数）、
`/api/ping` 报出的版本 == `Directory.Build.props` 的版本、`/api/packs` 扫到 11 个包、
快照顶层字段 / camelCase / 紧凑 JSON、40 次点击真的涨钱、超大购买数量被钳到预算内、
**动作之后快照的 `notifications` 里有真消息**（字段齐全、`kind ∈ 0..3`、时间戳不超过当前游戏时间）、
未知命令 `ok=false`、未知包在 `/api/snapshot` 与 `/api/stream` 都是 404、
**没有 `NEKO_DEBUG_KEY` 时带 `epoch` 必须是 403**、SSE 真读 6 秒（第一帧全量 / 之后以增量为主，
撞上 30 秒一次的全量对账不算故障 / 按字节数均值 < 全量 10% / `seq` 单调）。三条刻意为之的行为写在脚本头部注释里：
自带临时存档目录（探针会点击和买入，跑在玩家存档上等于拿进度当夹具）、
起子进程前把 `NEKO_DEBUG_KEY` 摘掉（让"门是关着的"成为被测事实而不是巧合）、
收尾按**端口**反查进程（`dotnet run` 会再起一个真宿主子进程，只杀它会留下孤儿）。

---

## 5. 有意没做的（都是决策，不是遗漏）

| 项 | 现状 | 为什么 |
|---|---|---|
| **剧情外部化的收尾** | 十个有剧情的包全部搬完（421 条）；`tools/extract-lore-text.ps1` 只支持工厂写法，作为"迁移过程可复核"的存档保留 | 迁移已完成，这个脚本不再有运行期职责；真要再用它抽新包，得先扩展对象初始化器写法 |
| **永久升级线 / 二周目界面** | 引擎支持，Web 未铺（**具体缺什么要先定义**，见 §8.2） | 先做能玩的最小闭环 |
| **离线收益弹窗** | 只在宿主控制台打印一行 | 还没定呈现方式 |
| **通知日志** | **已渲染**（2026-10-01：第六个面板「日志」，端到端加了 4 条检查）。**未读游标仍未做**——那要新增状态 + 公开字段，见 §8.2 | 先做能玩的最小闭环 |
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
  **一个可复制的例外（2026-10-01，日志面板）**：把 `index.html` / `app.css` / `app.js`
  内联成一张静态页、把 `EventSource` 换成立刻推一帧假快照的桩，就能用无头浏览器截图——
  它验的是**布局与配色**（真 CSS + 真渲染函数 + 假数据），验不了"和真宿主配合得对不对"
  （后者归 `tools/api-test.ps1`）。手法记在 [games/README](games/README.md) 的 Web 一节。
- **CI 只跑在 Windows 上**：`.github/workflows/ci.yml` 是两个 `windows-latest` 作业。没有加
  Linux 作业——`engine/core/` 平台中立这条主张仍然只由"全仓库只有两处平台相关代码"这个
  事实支撑，没有一个远端作业在守着它（想守就得先确认 Demo 与测试项目在 Linux 上也能编）。
- **CI 的远端表现（2026-10-01 补记）**：截至 `3574762` 的**六次运行全绿**——
  `c84c478`（workflow 首跑）、`4bb2690`、`67a1f22`（1.2.1 发布）、`5cf5f9c`（文档回填）、
  `602b5c8`（日志面板）、`3574762`（预览手法回填），每次都是两个作业各自 success
  （构建 + 435 用例 / Web 宿主端到端）。
  原先这条写的是"还没在远端跑过一次"——写下时是事实，但一直没人回填；这次补上，并推翻它。
  runner 镜像里的 .NET 版本、`Get-CimInstance` 的行为、端口占用都没出问题。
  观测方式见 §7 第 11 条。
- **`engine/tests/ArchitectureTests.cs` 直接枚举内容包**：A1 守卫以内容包为探针，
  搬走内容包就失去判别力。将来真要分离引擎仓库，补救方向是改用极小的合成测试包
  （沿用阶段 3A"全部用合成内容测试"的既有做法）。
- **远端：HTTPS 结构性不通，要走 SSH**。`github.com` 解析到 `140.82.116.4`，而该 IP 的 443
  **连续 8 轮都是** `Failed to connect ... after 21s`——那不是抖动，是路由。SSH 则是通的
  （`github.com:22` 与 `ssh.github.com:443` 均可达，密钥认证通过）。细节与命令见 §7 第 10 条。
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
   **这条记录的机器状态已经变了（2026-10-01 复核）**：本机现在是 SDK **10.0.203 / 10.0.400**，
   没有 8.0.x 的 SDK——net8.0 照样编得过（`0 Warning(s) / 0 Error(s)`），因为 SDK 10 会带上
   8.0 的 targeting pack（`C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref`）；
   仓库内的 `.packages` 里也播了 8.0.30 的 ref pack 兜底。所以"一个 SDK 编全部"仍然成立，
   只是那个 SDK 现在不是 8.0.303。
8. **跑宿主需要 `Microsoft.AspNetCore.App 8.x` 运行时，本机没有**（只有 10.0.7 / 10.0.11）。
   宿主声明的是 net8.0，缺 8.x 时它**拒绝启动**并打印一段英文（"You must install or update
   .NET"），看起来像"宿主坏了"，其实是环境缺件。`tools/api-test.ps1` 因此在启动前探测一次：
   没有 8.x 就设 `DOTNET_ROLL_FORWARD=Major`（装了 8.x 的机器不受影响，仍然跑 8.x），
   并**把这件事打印出来**——实测上滚到 10.0.11 后 22 项检查全过。
9. **`git` 报 "dubious ownership"**，用
   `$env:GIT_CONFIG_COUNT=1; GIT_CONFIG_KEY_0="safe.directory"; GIT_CONFIG_VALUE_0="D:/githb/neko-clicker"` 绕过。
10. **推送走 SSH，别跟 HTTPS 死磕**（2026-10-01 实测）：`github.com` 解析到 `140.82.116.4`，
    该 IP 的 443 **连续 8 轮**都是 `Failed to connect ... after 21s`，重试不会变好；
    而 SSH 正常（`github.com:22`、`ssh.github.com:443` 都可达，`git@github.com` 密钥认证通过）。
    不想改 `origin` 就用显式 URL（**2026-10-01 实测可用**，第一次就成功）：

    ```powershell
    git push git@github.com:NekoHome-Studio/neko-clicker.git main
    git push git@github.com:NekoHome-Studio/neko-clicker.git v1.2.1
    ```

    > 上一版这里写的是一次性 URL 重写
    > `git -c url.ssh://git@github.com/.insteadOf=https://github.com/ push origin main`——
    > **实测不可用**：重写出来的 `ssh://git@github.com/NekoHome-Studio/...` 缺前导斜杠，
    > GitHub 直接拒收（`/NekoHome-Studio/neko-clicker is not a valid repository name`，
    > 连试 5 次都一样）。那行命令从没在真推送上验过——发 1.2.1 时才踩到，换成了上面的写法。

    想永久改：`git remote set-url origin git@github.com:NekoHome-Studio/neko-clicker.git`。
    顺带一条判据：另外几个 GitHub IP（`140.82.112/113/114/116.3`、`20.205.243.166`）的 443
    是通的——所以历史上"重试几次就成功"很可能是解析到了别的 IP，而不是同一 IP 时好时坏。
11. **看 CI 走 `api.github.com`，不用碰 `github.com`**（2026-10-01 实测）：`github.com:443`
    不通不妨碍 `https://api.github.com/repos/NekoHome-Studio/neko-clicker/actions/runs`
    1.3 秒返回 200（公开仓库只读，不要 token）；本机还装了 `gh`
    （`C:\Program Files\GitHub CLI\gh`）。结果见 §6。

---

## 8. 下一步与交接（给下一个接手的人）

> **停在哪里**：**1.2.1 已发布**（tag `v1.2.1`），工作区干净、全部提交都已推到远端
> （HEAD 见 `git log -1`）。引擎 435 用例全绿、Web 宿主 26 项端到端全过、CI 在远端跑过六次
> 且全绿（见 §6）。**§8.1 与 §8.2 的通知面板已交付**（留在这里当记录）；
> **§8.2 剩余三条与 §8.3 起都还没开工**，按建议顺序排。
> 想知道"现在到底什么状况"，跑 §4 那三条命令——这份文档刻意不写 HEAD，别再让它跟着提交跑。

### 8.1 ✅ 已交付：1.2.1（2026-10-01）

**为什么先做它**：`[未发布]` 里那两件事——421 条散文外部化、`ContentText` 的并发修复——
**都不动公开 API**，按 [VERSIONING](engine/docs/VERSIONING.md) 是 patch。但它们在 main 上
已经躺了一段时间，而 **`v1.2.0` 的 tag 里没有那个并发修复**：拿到 1.2.0 的人手里的
`ContentText` 是个"多线程用了就可能炸"的类型（实测 1200 次构建里 522 次抛异常）。
所以这不是"顺手发个版"，是补一个**已经发出去的**缺陷。
完整执行记录（清单外的几处发现也在里面）见 [RELEASING](engine/docs/RELEASING.md) §3。

改动清单（已按此执行；**少一处，`PublicApiTests` / `VersionTests` 就会红**）：

| 文件 | 改什么 |
|---|---|
| `Directory.Build.props` | `Version` / `AssemblyVersion` / `FileVersion` 三处一起（`1.2.0` → `1.2.1`、`1.2.0.0` → `1.2.1.0`） |
| `engine/core/PublicApi.txt` | **不要手改**：跑 `tools/public-api.ps1` 重生成（它会把第一行的 `version=` 一并换掉；公开表面本身不变） |
| `CHANGELOG.md` | `## [未发布]` → `## [1.2.1] - YYYY-MM-DD`，并把开头"承接 1.2.0…发布时的升位"那段改成已发布口径 |
| `README.md` | 两处「当前版本 `1.2.0`」。**第一处必须是全文第一个反引号版本号**——`VersionTests` 就是锚在它上面的 |
| `README.md`（清单外） | 代码示例的输出注释（`"1.2.0"` / `1.2.0.0`）与一句陈旧的「1897 行快照」（实际已是 1909 行）——不在守卫射程内，但留着就是错信息 |
| `engine/README.md` | 那句「当前 `1.2.0`」 |
| `engine/docs/VERSIONING.md` | §1 的当前版本 + §2 示例里的版本号与输出（`ApiVersion.Current` 会打印成 `1.2.1`） |
| `STATUS.md` | 本文件 §1 的表格与"当前版本"段 |
| `engine/docs/TEXT_AS_DATA_PLAN.md` | §8 末尾"发布时按 patch（1.2.1）"改成既成事实 |
| `engine/docs/RELEASING.md`（新增） | 发布流程 + 这次的实际执行记录：以后发版照它走，不用再从零拼命令 |

验收与收尾（实际结果）：`.\tools\build.ps1 -Strict` **435 全绿、0 警告**（它同时验了版本号、
快照与 CHANGELOG 条目）+ `.\tools\api-test.ps1` **22 项全过**（当时的检查数；同一天稍后
铺日志面板时加到 26 项，见 §8.2）；`tools\pack.ps1` 产出
`artifacts\neko-clicker-1.2.1-win-x64.zip`；`git tag -a v1.2.1 -m "..."` 后用 SSH 推
（§7 第 10 条），提交与 tag 都上去了。

**那个留给你判断的取舍，这次定的是"收录"**：`[未发布]` 里挂着的工具与 CI
（`tools/api-test.ps1` + workflow）收进了 1.2.1 条目，理由已经写进 CHANGELOG 那一节的开头——
本仓库的 CHANGELOG 同时充当改动记录（1.2.0 条目里也记了 Web 前端与启动器），
版本号本身仍然只描述 `NekoClicker.Core` 的公开 API。

### 8.2 铺完剩下的 Web 界面（游戏侧，工作量最大）

通知面板已交付（见下）。**剩下三条都不是"照字面直接开工"就行**：离线弹窗与永久升级线
各要先定一个语义、而且都要给 `GameSnapshot` 新增字段（= 公开 API 新增 = minor），
二周目更连方向都还没有——照 §5 那行字直接开工会走偏。

- **通知面板 ✅ 已交付**（2026-10-01）：`wwwroot/` 里多了第六个面板「日志」——
  按四种 `NotificationKind` 上色、新在上、相对游戏时间；端到端 22 → **26 项**。
  **仍未做**：未读游标 / 角标（要新增状态 + 公开字段 = minor），等有人真的需要再谈。
  一个过程中确认的引擎事实：**买建筑刻意不发通知**（只有买升级 / 成就解锁 / 增益 /
  金猫 / 舍命会发），所以端到端那条检查守的是"有真消息且形状对"，不点名文案。
- **离线收益弹窗：先解决"它现在有多隐蔽"。** 宿主已经有 `GameHost.OfflineOnLoad`
  （`OfflineProgress`），但只在控制台打一行（`Program.cs` 的 `Sessions.TryGet`）。
  要弹窗就得让它进快照——**那是 `GameSnapshot` 的新字段 = 公开 API 新增 = minor**，
  别和 8.1 的 patch 混在一个提交里。另有一条语义坑：离线补发只在**会话首次创建**时发生
  （一个包一个会话，刷新页面不会重来），"弹一次"的边界要跟"刷新页面不该重复弹"一起定。
- **永久升级线：先回答"到底缺什么"。** 素材其实都在快照里（`prestigeChips` /
  `prestigeCurrencyName` / `prestigeCurrencyIcon` / `upgrades[].currency` / `Prestige`），
  前端也已经在升级列表里用 `row.currency === 1` 区分转生货币——但那是**硬编码的枚举序数**
  （`UpgradeCurrency` 的成员顺序一变就静默错位）。要做的是独立面板还是分组先定下来；
  顺手该把那个魔法数字换成视图里的语义字段（同样是公开 API 新增）。
- **二周目：先跟用户确认语义再动手。** 引擎侧现在只有 `hardReset`（清空、不带继承）与
  纪元 / 转生的"舍一命"。"看完结局带继承重开"还是别的，**现在没有答案**。

验收：`tools\api-test.ps1` 能给新端点 / 新字段兜住 404 与 camelCase；界面好不好看没有守卫（§6）。

### 8.3 补 `PackageId` / `IsPackable`

纯元数据、不动公开 API——`engine/core/NekoClicker.Core.csproj` 现在只有 `Description`。
两条注意：① 版本号来自 `Directory.Build.props`，打包会自动跟上，**不要再写一份**；
② 本机零第三方依赖，`dotnet pack` 应该能离线跑，但"没有网络也能 pack"这条**最好实测一次**
再写进文档——这个仓库的既有教训是：没实测过的环境结论迟早会变成错的。

### 8.4 可选（都不是必须，但都是"已知没人守"的地方）

- **给 CI 加一个 Linux 作业**：能替"`engine/core` 平台中立"这条主张当守卫，
  前提是先确认 `games/hosts/Demo.Cli` 与测试项目在 Linux 上编得过（本机验不了）。
- **看一次 CI 的远端运行**：见 §6——本地全绿不等于 runner 上全绿。
- **观感**：Web 的"好不好看"仍然只能靠人看。

### 8.5 动手前的环境清单（这台机器）

- 构建与跑宿主：§7 第 3 / 7 / 8 条（`-m:1`；SDK 10 编 net8.0；缺 ASP.NET 8 运行时，
  `tools/api-test.ps1` 会自动设 `DOTNET_ROLL_FORWARD=Major` 并打印出来）。
- 写 `tools/*.ps1`：UTF-8 **with BOM**（§7 第 1 条）——`edit` 一类的工具会吃掉 BOM，改完要补。
- 推远端：SSH 一次性重写（§7 第 10 条），fetch / push 都要带重试。
- 任何提交前：`.\tools\build.ps1 -Strict` + `.\tools\api-test.ps1`——CI 跑的就是这两条。
