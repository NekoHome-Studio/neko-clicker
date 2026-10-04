# 结构优化清单：让**下一次**改动更便宜的那些东西

> **这份文档是什么**：一张**按优先级排的、结构层**的欠账表——代码的形状、构建与验证回路、
> 内容创作模型、重复的机制、没人消费的计算、每次都要手工重做的活。
>
> **这份文档不是什么**：
> - 不是**数值**清单。门槛、倍率、价格、层数该是多少，归 [TUNING_ANALYSIS](TUNING_ANALYSIS.md)
>   与 `OPEN_WORK` 登记册的 `D*` 行；
> - 不是**界面**清单。画什么、怎么画、好不好看，归 Web UI 那几轮与登记册的 `W*` 行；
> - **不产生"下一步"**。唯一回答"下一步做什么"的地方是
>   [OPEN_WORK 文首的《下一步：唯一权威清单》](OPEN_WORK.md)，本文一行都不往那里加。
>   本文与它的关系是：那三张表是**要做的事**，本文是**做那些事之所以贵的结构原因**。
>
> **为什么单独一份文件、而不是塞进已有的某一份**（选家的理由，逐份核过）：
>
> | 候选 | 为什么不放它 | 
> |---|---|
> | [ARCHITECTURE](ARCHITECTURE.md) | 它写的是"现在**是**什么"（模块地图、时间模型、扩展点表），而且它**明确拒绝**承载会变的数字（第 269~272 行：用例数"写死就会烂"，以 `build.ps1 -Strict` 打印的那一行为准）。本文写的是"现在**该改**什么"——两种时态不能同居 |
> | [OPEN_WORK](OPEN_WORK.md) | 它拥有"下一步"。本文若成为它的一节，就会与文首那三张表的权威性打架（那里的每行是**待办**，本文每行是**成本与证据**）。而且它此刻正被另一轮持有 |
> | [DEVELOPMENT_SUMMARY](DEVELOPMENT_SUMMARY.md) | 它是一次会话的**事后总结**（含 §7 那份漂移审计），带日期、是快照。本文是要被**继续维护**的活清单 |
> | [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) / [FRONTEND_CHECKS](FRONTEND_CHECKS.md) / [WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md) | 各拥有一**个**主题（内容外置 / 前端检查 / 已搁置的扩展方案）。本文要横着切过它们，放进任何一份都会变成"那份文档的第二节" |
>
> **证据规矩**（与全仓同一条）：每条都必须给得出**可核对的东西**——文件与行号、一个数（并写清
> 是怎么数出来的）、或一个提交。给不出的，就在那条里写明**"疑似，未量"**，不许写成结论。
>
> **基线**：写作期间那两处 Web 改动（`games/hosts/Web/wwwroot/app.js`、`tools/web-smoke.mjs` 的 §23）
> 在树上的哈希先是 `22d7e30`，随后被那一轮重提为 **`618a46c`**——**同一份内容**，本文引用的是它。
> 写作时 `engine/docs/OPEN_WORK.md` 与 `app.js` 还各有未提交改动，**另一轮正在持有它们**，
> 本文一个字节都没动它们。凡引用被持有的文件，都以"我读到的那一刻"为准并写明；
> 提交本文时（`0782d22`）工作树已经干净。

---

## 一、优先级表

排序依据只有一条：**修它之后，后面多少次改动会变便宜**。排在前面的是"让每一次验证/每一次测量都更贵"的东西。

| # | 结构问题（一句话） | 复发成本 / 它会造成的 bug 类别 | 代价 | 谁定 | 详情 |
|---|---|---|---|---|---|
| **✅ S1** | Web 前端冒烟套件（`tools/web-smoke.mjs`，135 处断言）**不在任何自动闸门上**：`build.ps1` 不跑它，CI 也不跑它 | 前端回归只能靠人记得。**这正是 `FRONTEND_CHECKS` 那一整篇事故的成因**，而且 `618a46c` 刚为"没人画的字段"这个复发 5 次的 bug 类加了 210 行守卫——加在一条没人自动跑的套件里 | **S**（CI 一个 step + `build.ps1` 一行） | 🔧 工程（要 CI） | §S1 · **已关闭 `4a89cee`** |
| **✅ S2** | 冒烟套件的**夹具**是手工从真宿主抓的，**抓取脚本不在仓库里**（在 `.tmp/capture-fixture.ps1`，gitignore） | 动了 `wwwroot/` 或协议就要重抓夹具；今天只能"读文档里那句话 + 自己拼一个请求"。抓错形状 → 两套测试全绿而页面是死的（`mode: "buy10"` 那次） | **S**（把抓取收进 `tools/`，一条命令） | 🔧 工程 | §S2 · **已关闭 `cf40b9d`（2026-10-04，见 OPEN_WORK §0.19）** |
| **✅ S3** | `build.ps1` 撞上"bin 被占"时**只打一句红字**，不判断占用者是谁；而"是谁占着"的判据**仓库里已经写好了**（`start.ps1:39~50`） | 一次失败给出 144~170 条 `MSB3021/MSB3027`，读的人从错误里看不出"是宿主在跑还是别人也在构建"；这条路径**记录过一次约 320 个 `dotnet` 进程 / 9.9 GB**，玩家侧观感是**数字卡死** | **S** | 🔧 工程 | §S3 · **已关闭 `cf40b9d`（2026-10-04，见 OPEN_WORK §0.19）** |
| **S4** | **测量机器人没有家**：每个测量任务把 `PrestigeTests` 那条机器人**逐行抄**进一个 `.tmp` 里的临时工程；测量产物与三份分析脚本也都在 `.tmp` | 仓库里被引用最多的那些数（`TUNING_ANALYSIS` §1 / §四 的全部表格）**每次都要重抄一遍**，而且**复现不了**（探针不在版本控制里）。已经在两周内重抄至少 4 次 | **S~M** | 🔧 工程 | §S4 · **2026-10-04 只补了设计**（机器人的家、谁编它、验收判据写在 §S4 的补记里；代码未动） |
| **S5** | 一份**环境契约**（`DOTNET_CLI_HOME` / `NUGET_PACKAGES` / `MSBUILDDISABLENODEREUSE` / `-m:1`）**抄了三遍**，另有两条路径**完全绕过**它 | 漏一处就是"受限沙箱下构建**静默失败**"（`dnet.ps1` 头部写明的那个坑）或 NuGet 写不进 `%USERPROFILE%`。`-m:1` 这个字面量手工重复在 8 处 | **S** | 🔧 工程 | §S5 |
| **S6** | "当前数字"（用例数 / 冒烟条数 / API 行数）以**散文**形式写在 **10 个以上文件**里，没有一处是机器校验的 | 每加一条守卫就要人工再扫一遍——`DEVELOPMENT_SUMMARY` §7 一次审计就抓出 6 类漂移；今天同一个套件有 **108 / 128 / 135** 三个数并存 | **S**（登记册 **W1** 拥有立即清理；本行是它的结构层） | 🔧 工程 | §S6 |
| **S7** | `text.json` 里**同一个值存在两处**（104 对建筑图标 / 29 对结局名 / 45 对增益名），守卫只查"代码 ↔ 文件"、**不查"文件里的两处是否一致"**；派生文案（成就名、三档升级说明）是**物化**的 | 6,131 条字符串里 **1,499 条（24.5%）是重复值**；改名一座建筑要手动改 **≈6 处**（全仓 634 处 `「建筑名」` 引用）。两处不一致**今天不会红**——守卫方向是单向的 | **S**（加一致性问题守卫）/ **M**（别名或重生成机制） | 🔧 工程 → 机制要 👤 定（**D4**） | §S7 |
| **S8** | 新内容包的**登记散在手写清单**里：`Demo.Cli/ContentPackages.cs`、`tools/start.ps1:94`、`TestGame.cs:226`、还有一个测试文件里的 **10 张表** | 加一个包要改 ~16 处；`start.ps1:94` 那处**没有任何守卫**（漏了就是"`start.cmd list` 里没有它"）。Web 宿主已经用**运行时发现**把这件事做对了（`PackageCatalog.cs`） | **M** | 🔧 工程 | §S8 |
| **S9** | "快照里没人读的字段"这条新守卫**只覆盖 app.js**（守卫自己写明"前端 = app.js"）；**终端宿主与 `engine/core` 的公开视图面没有对应守卫** | 同一个 bug 类在终端侧**无守卫**；而视图字段一旦进 `PublicApi.txt`，**删掉就是 major**（`VERSIONING` §2）——没人读的字段会永久留在公开 API 上 | **S~M** | 🔧 工程 | §S9 |
| **S10** | 「**拥有 ⇒ 已解锁**」这条不变量**只有一个包的私有辅助方法在守**（`ApocalypseContentTests`），而它是一条**横扫全部包**就能写的性质 | 下一个打开 `InheritBuildingRatio` 的包若漏改 25~50 条解锁条件，症状是"手里有 50 座建筑，列表里全是未解锁"——**产量照算、看不到也卖不掉**，今天只有一条人手写的 checklist 拦着 | **S** | 🔧 工程 | §S10 |
| **S11** | `tools/` 里**闸门、环境壳、一次性迁移档案、三个启动器**混在一起，没有分类 | 新来的人（或新会话）不知道该跑哪几个；`RELEASING` 不得不把 11 步顺序再讲一遍。`extract-lore-text.ps1` 自己已声明"不再有运行期职责"，但**在文件列表里与闸门长得一样** | **S** | 🔧 工程 | §S11 |
| **S12** | `text.json` **没有 schema 版本号**，而它现在已经有**保留键**（`$`）与一张**清单**（`$tables`） | 第一次破坏格式的改动**没有探测器**——存档那边有 `ISaveMigration`（`VERSIONING` §6），文本格式这边什么都没有；判断"这个文件是哪个年代的"只能读文档 | **S** | 👤 决定（**D4**）+ 🔧 实现 | §S12 |
| **✅ S13** | 全项目**唯一的人类数据仪器有两份实现**：`Demo.Cli/ChoiceLatencyLog.cs`（90 行，**不落盘**）与 `Web/ChoiceLatencyLog.cs`（446 行，**写 `artifacts/latency.txt`**）；而 `start.ps1` 的**默认模式**跑的是前者 | 走默认启动器玩一局，**一个样本都不会留下**；要贡献样本必须知道"得用 `start.cmd web`"。这是登记册 **H1** 那个"全项目唯一没有人类数据的参数"至今只有 21 条 `session start`、**0 条样本**的一个结构解释——**与 S4 是同一个回路的两面** | **S** | 🔧 工程 | §S13 · **已关闭 `4a89cee`** |

---

## 二、逐条详情

### S1 Web 前端冒烟套件不在任何自动闸门上

> **✅ 已关闭（2026-10-03，`4a89cee`；现场与判别力抄在 [OPEN_WORK](OPEN_WORK.md) §0.15.1）**。
> `tools/build.ps1`（在"构建 Web 宿主（独立 sln）"之后、"=== 测试 ==="之前）与
> `.github/workflows/ci.yml`（新作业 `web-smoke`，`setup-node` 20）现在都跑
> `node tools/web-smoke.mjs`。**没有 node 时的选择是"故意红"**（exit 1 + 一句
> "这里刻意不静默跳过" + 人工出路 `-SkipWebSmoke`），不是警告后继续：静默跳过正是
> 本条描述的失效形态本身。判别力两条都已实测：删掉 `app.js` 的「单个」那一段 →
> `133 / 135` + `前端冒烟失败…` + **exit 1**（C# 那一档没轮到）；把 node 从 PATH 摘掉
> （纯 ASCII 探针）→ 同样的红与 `CHILD-EXIT=1`，加 `-SkipWebSmoke` 才走完且 exit 0。
> 下文保留原文（它记录的是**当时**的事实与理由，包括"没有 node 怎么办"这个问题）。

**是什么**。`tools/web-smoke.mjs`（1,838 行、135 处 `check(` 调用点）是无头 DOM 桩件烟测：
它**真的把 `app.js` 跑起来**、喂从真宿主抓的快照、断言不抛异常与 DOM 结构。
它是 `FRONTEND_CHECKS.md` 那篇事故的直接产物（"HTTP 层全绿 ≠ 页面能用"）。

**为什么贵**。今天**没有任何命令会自动跑它**：

- `tools/build.ps1`（55 行）只做三件事：编主 sln → 编 Web sln → `dnet.ps1 exec engine\tests\bin\Debug\net8.0\NekoClicker.Core.Tests.dll`。**没有一行提到 `web-smoke`**。
- `.github/workflows/ci.yml` 两个作业：`build.ps1 -Strict` 与 `api-test.ps1`。**没有 `node tools/web-smoke.mjs`**。
- 全仓 `git grep web-smoke` 在 `tools/*.ps1` / `*.cmd` / `.github` 里**零命中**（只有文档与 `web-smoke.mjs` 自己提到它）。
- `build.ps1` 头部那句自我要求是"**一条命令验证全部，才有资格叫『一键』**"（第 38~40 行，为 Web sln 单独写下的理由）——而前端 JS 恰好落在"全部"之外。

**为什么这条排第一**。`618a46c`（2026-10-03 23:18，我写作时它在树上是 `22d7e30`）刚给这一类缺陷加了 §23：
"快照里的每个字段都要有人决定过（画了，或写明不画）"，带 `NOT_DRAWN` 表、反向"过期借口也是红"、
空数组盲区清单、`.字段名` 读法完备性——**210 行**。那一节的注释自己写着：

> 五次都是**人注意到**或**代理审计**发现的——仓库里没有任何东西会在"多了一个没人画的字段"时变红。
> 这一节就是那个东西。（`tools/web-smoke.mjs:1681~1682`）

也就是说：**为"复发 5 次的 bug 类"专门造的守卫，被放进了一条只有人记得才会跑的套件。**
`FRONTEND_CHECKS.md` §四 立的规矩（"任何 `wwwroot/*` 的改动，交付时必须附页面真跑过的证据"）
到今天仍然**只靠纪律执法**。

**代价**：S。CI 加一个 `node tools/web-smoke.mjs`（不需要 .NET、不需要宿主、不需要网络——
套件自己建临时目录、结束时删掉），`build.ps1` 里也补一行（它已经编了 Web sln，只差跑这一条）。

**风险 / 可逆**：低。风险只有一条：套件依赖 `tools/fixtures/web-snapshot.json`（见 **S2**），
而**夹具过期的红**正是它设计上的行为（§23 反向那条）。可逆：删掉那一步即可。

**谁定**：🔧 工程（CI 那半要联网，见 `OPEN_WORK` **W7** 与 **W8** 是同一族的事）。

---

### S2 夹具的抓取工具不在仓库里

> **✅ 已关闭 `cf40b9d`（2026-10-04，见 [OPEN_WORK](OPEN_WORK.md) §0.19）**。抓取现在是仓库里的
> **`tools/capture-fixture.ps1`**（`param` 化：`-Port` / `-Package` / `-Clicks` / `-Output`）。
> 它比 `.tmp` 那份多做了四件事：① 根目录由 `$PSScriptRoot` 推出来（不再需要 `-Worktree` 参数），
> 所以它在哪份检出里都能跑；② 临时目录落在**系统 temp**（原先落在 worktree 的 `.tmp\fixture-capture\`）——
> 仓库树里一个临时文件都不产生；③ 落盘前**否决**三种"看着像快照但是坏的"结果
> （`buildings` 空 / `notifications` 空 / `mode` 是**字符串**——最后一条正是本条记录的那次事故形态）；
> ④ 打印字节数 / sha256 / **覆盖前后对比** / `mode` / `era` / 通知条数，成功才删临时目录、失败保留并报路径。
> 落盘用 `-OutFile`（**响应体原始字节**），中文与转义保持线上那一个字节序列。
> `web-smoke.mjs` §14 的出处段已改指向它。下文保留原文（它记录的是**当时**没有工具的事实）。

**是什么**。`tools/fixtures/web-snapshot.json`（我读到的那一刻 **60,859 字节**，mtime `2026-10-03T23:16`）
是**从真宿主抓下来的原始响应**，充当 `web-smoke.mjs` 里"线上形状"的唯一参照
（§14"夹具不是谎话"、§23"每个字段都要有人决定过"都读它）。

**为什么贵**。规矩写得清楚（`BUILDING_UPGRADES_PLAN` §5.5 第 287 行）：
"夹具要在改动**之后**重新从真宿主抓一次（`tools/web-smoke.mjs` 自己写明它抓的是哪个端点）"。
但**抓取动作本身没有工具**：

- `tools/` 的 11 个文件里没有抓夹具的脚本（`git grep -ln web-snapshot.json -- tools games engine .github` 只命中 `web-smoke.mjs` 与四份文档）。
- 真正的抓取脚本是 `.tmp/capture-fixture.ps1`（4,528 字节，mtime `2026-10-03 17:45`）——**`git check-ignore` 确认 `.tmp/` 被忽略**（`.gitignore:20`）。
- 历史上这份夹具已经手工重抓过两次（`CHANGELOG.md:76` 60,859 字节、`:156` 61,143 字节）。

**为什么这比"少一个脚本"严重**：夹具是**测试输入**，而它的权威性来自"它真的是线上形状"。
一次抓错（少一个字段、字段名写错大小写）会让下面所有守卫退化成橡皮图章——
`AGENT_ARCHIVE` §三第 1 条记的正是这个：夹具写 `mode: "buy10"`（字符串）而线路送 `mode: 0`（数字），
**两个套件全绿，页面每帧在 `render()` 中段抛异常**。

**代价**：S。把抓取写成一个可提交的脚本（起宿主 → 打那个端点 → 按键排序 + 不缩进写文件 →
顺带报出字节数与 sha256 前 16 位），放进 `tools/`；`web-smoke.mjs` 头部改成指向它。

**风险 / 可逆**：低。它不改变夹具内容，只是把已经做过两次的动作变成一条命令。

**谁定**：🔧 工程。

---

### S3 `build.ps1` 撞上"bin 被占"时只报错、不诊断

> **✅ 已关闭 `cf40b9d`（2026-10-04，见 [OPEN_WORK](OPEN_WORK.md) §0.19）**。`tools/build.ps1` 里新增
> `Show-HolderDiagnosis`，在两处失败分支（主 sln / Web sln）都被调用，**只报告、不杀**：
> ① 按**映像路径以仓库根开头**筛出本仓库的进程（判据逐字照抄 `start.ps1:40~41`，逐个进程各自
> `try/catch` 取 `Path`——受保护进程读不到它，而这里 `$ErrorActionPreference = 'Stop'`），
> 逐个打印 image / PID / 启动时间 / 已运行时长；② 用 **`netstat -ano`**（**不是** `Get-NetTCPConnection`
> 或 `Get-CimInstance`——那两个在这个沙箱里会抛 `CimException`，登记册 **W2** 记的就是它）找默认
> 5273 / `NEKO_PORT` 的**监听者**，把 PID 映射回 `image`、启动时间，并高亮；③ 末尾给出判据：
> "1 个、刚起来 ⇒ 等 30 秒" / "几百个、StartTime 很旧 ⇒ 残留，要清就按严格 `StartTime` 截止" /
> "5273 上有监听者 ⇒ 可能是真人在玩，**别杀**"。
> 顺带修掉一个真会咬人的细节：两处失败分支原先直接 `exit $LASTEXITCODE`，而诊断里要跑 `netstat`、
> 那会把 `$LASTEXITCODE` 覆盖成 netstat 自己的退出码——所以现在**先把退出码抄进 `$code`**，再诊断、再 `exit $code`。
> `MSB3021` 那条原文照旧交给 MSBuild 报，这一节只加"谁占着"。下文保留原文。

**是什么**。`tools/build.ps1` 的失败路径（第 30~35 行）只有：

```powershell
if ($LASTEXITCODE -ne 0) {
    # （上面还有两行注释：说明 -warnaserror 会把警告报成 error）
    Write-Host '构建失败（若摘要显示 0 Warning(s) 却失败，那是警告被 -warnaserror 计成了 error）。' -ForegroundColor Red
    exit $LASTEXITCODE
}
```

**为什么贵**。宿主占着 `games\hosts\Web\bin` / `engine\tests\bin` 时，`dotnet build` 会吐一整片
`MSB3021`（copy 不到）＋`MSB3026`（重试）＋`MSB3027`（超过 10 次重试）。仓库里已经记录过这条路径的两种量级：

- `AGENT_ARCHIVE.md:47`：**"两个 agent 同时构建会互锁 `bin`（144 个 MSB3021/3027）"**，
  并且**那条失败路径泄漏过约 320 个 `dotnet` 进程、把内存吃光**，"玩家侧的观感就是**数字卡死**"。
- `DEVELOPMENT_SUMMARY.md` §3.4（第 320~342 行）：作者**自己也踩到了**——`engine/tests/bin` 被占，
  **170 个 error、`0 Warning(s)`**；成因不是泄漏，而是**同一个工作区里另一轮构建正在跑**（当时 `dotnet` 进程只有 1 个）。
  那一节给的处置是：`netstat -ano | Select-String ':5273'` 找玩测宿主、
  `Get-Process dotnet | Select-Object Id,StartTime,CPU` 看有没有几百个同源进程，
  **"能等就别杀"**、要杀就用严格的 `StartTime` 截止时间。

**关键点**：**这两条判据（谁占着、几百个还是 1 个、能不能等）在仓库里已经有了**，
而它们住在 `tools/start.ps1` 第 39~50 行那个"只清本仓库进程"的兜底里——
**只在"启动"这条路上执行，`build.ps1` 这条路上一个字都没有**。于是失败路径的读者得到的
是 144~170 条 copy 错误，而不是一句"`games\hosts\Web\bin` 被 PID 1234（dotnet，5273 端口，运行 3 小时）占着"。

**代价**：S。在 `build.ps1` 的失败分支里加一段**只报告、不杀**的诊断：

1. `Get-Process dotnet | Select Id,StartTime,CPU,Path` 筛 `Path` 以仓库根开头的（`start.ps1:40~41` 已有现成写法）；
2. `netstat -ano | Select-String ':5273'` 找玩测宿主；
3. 加上一句**判据**："1 个、刚起来 → 大概率是别人在构建，**等 30 秒**；几百个、`StartTime` 很旧 → 残留"；
4. **绝不自动杀**——那个宿主可能正在为真人服务（本仓库此刻就是这种状态：`127.0.0.1:5273` 上有一个 1.9.0 的宿主，
   而 `OPEN_WORK` **H1** 正等着真人去玩它）。

**风险 / 可逆**：低。纯增加诊断输出，不改退出码、不改构建参数。**注意 `.ps1` 的 UTF-8 BOM**
（`DEVELOPMENT_SUMMARY` §3.3：`edit` 会剥掉 BOM，Windows PowerShell 5.1 就按 GBK 解析、整脚本 parse error）。

**诚实边界**：那 320 个进程**与并发构建只有时间相关性、没有解释**——`AGENT_ARCHIVE.md:19` 自己标了这条边界，
`DEVELOPMENT_SUMMARY` §8 第 1 条也重申了。所以本条的成立**不依赖**那个因果：
即使成因是别的，"144 条错误里没有一行告诉你谁占着"这件事本身就值得修。

**谁定**：🔧 工程。相邻的一条在登记册里：**W2**（`api-test.ps1` 按端口杀宿主在沙箱下静默失效，
`Get-CimInstance` 抛 `CimException` 又被 `-ErrorAction SilentlyContinue` 吞掉）——
本条与它共用"怎么找占用者"这一个知识点，**修的时候应该一起看**。

---

### S4 测量机器人没有家，测量产物也不在版本控制里

**是什么**。仓库最贵的那些数（`TUNING_ANALYSIS` §1 的"一局 1.4~11.8 小时"、§四 的全部每层时长表）
来自**一条机器人**：8 次点击 → `TestGame.BuyGreedily` → 点金猫 → 答掉待答表态 → 能舍命就舍命 → `Simulate`。
它的守卫形态是 `PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun`（`engine/tests/PrestigeTests.cs:25~80`）。

**为什么贵**。它**没有可复用的形态**，于是每次测量都重抄一遍：

| 证据 | 内容 |
|---|---|
| `engine/docs/TUNING_ANALYSIS.md:107` | "机器人 = `PrestigeTests.EraPacks_...` 的**逐行复制**" |
| `.tmp/EraProbe-src/Program.cs:13~14` | 探针头部自己写着：`It runs the SAME robot as PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun` |
| `.tmp/EraProbe-src/EraProbe.csproj:18~29` | 探针只引用 `engine/core` + 9 个内容包——**它引用不了测试工程**，所以只能抄 |
| `engine/tests/NekoClicker.Core.Tests.csproj:4` | `<OutputType>Exe</OutputType>`：测试工程是**可执行程序集**，没有可被引用的库形态 |
| `.tmp/` 里的探针工程 | `EraDump`、`Round4Dump`、`Round5Dump`、`EraProbe-src` —— **4 个**独立小工程，各带一份自己的 dump/循环代码 |
| `.tmp/analyze*.py` | `analyze.py` / `analyze2.py` / `analyze3.py` —— 同一批数据被**三个**脚本各分析一遍 |

**为什么"重抄一遍"是结构问题而不是懒**：抄的时候**只有约定在约束你**——
步长（`Era >= 5` 起改 0.25 秒）、上限（30 游戏小时）、种子（`12345`）、
"要不要答掉待答表态"（1.6.0 起不答就走不到结局）都在注释里。
抄错一处不会红：它只会让**测量结论**悄悄偏掉，而结论会写进文档、被后面的决定引用
（`TUNING_ANALYSIS` §4.5 就记着"两个包对不上 §1 那张表"）。

**另一半：产物与仪器都不在仓库里，所以复现不了**。

- `engine/docs/TEXT_AS_DATA_PLAN.md:203` 自己写着："迁移用的一次性工具（dump 探针、迁移扫描器、竞态探针）**都在 `.tmp/`（已 gitignore）**"。
- `TUNING_ANALYSIS.md:118~119`：探针与三份分析脚本、`tune-out/*.json`（9 个包 + 4 组对照跑）"都在 gitignore 里、**随时可删**"。
- 而那份文档引用的路径**已经是错的**：第 110 行写 `.tmp/EraProbe`，实际目录是 `.tmp/EraProbe-src`（我核过：`.tmp/EraProbe` 不存在）。gitignore 里的路径就是这样烂掉的。
- 我量到的现状：`.tmp/` **49.9 MB / 1,397 个文件 / 36 个 `bin`或`obj` 目录**，其中 `.tmp/wt-stages` **31.5 MB**——
  而且它是一个**仍然登记着的 `git worktree`**（`git worktree list` → `.tmp/wt-stages  5dd477c [stage-work]`）。
  也就是说：仓库**里面**还有一份 17 个工程的完整源码副本，谁都不知道它还在。

**代价**：
- **机器人（S）**：把它抽成一个**只有一处实现**的文件。仓库里**已经有这个机制的成例**——
  `engine/tests/NekoClicker.Core.Tests.csproj:42~44` 用 `Compile Include` 把
  `SnapshotProtocol.cs` / `ChoiceLatencyLog.cs` / `RepositoryPaths.cs` 以**共享源码**的形式编进测试工程，
  注释写明理由："保证**只有一份实现**……不存在『测试测的是一份副本、线上跑的是另一份』"。
  （**2026-10-03 更正**：埋点与 `RepositoryPaths` 已搬到 `games/hosts/Shared/`、两个宿主各自
  `Compile Include` 同一份源码，测试项目改为经 `Demo.Cli` 的 `ProjectReference` 使用它——
  见 §S13 的"已关闭"一段。本节引用的是**当时**的行号与清单。）
  同一条路可以用在机器人上：抽一个 `MeasurementRobot.cs`，测试工程与任何探针都 `Compile Include` 它。
  注意那个 csproj 第 40 行立的规矩——**"改成 ProjectReference 能不能编过没有验证过；既然共享源码已经成立，就不为它冒险改结构"**：
  所以走共享源码，不要顺手改工程结构。
- **产物（S）**：把"怎么重跑 §四 那套测量"写成一段**能照着做的配方**（在哪个 commit、哪条命令、种子与步长、
  产物落在哪、怎么核对），并把**分析脚本**（`analyze*.py` 里那份最终版）收进 `tools/` 或文档附件。
  探针工程本身可以不进仓库，但"再造一个探针"必须是**照着配方 10 分钟的事**，而不是重抄 300 行。
- **worktree（S）**：`git worktree prune`（先确认没有别的会话正在用它——今天它对应分支 `stage-work` = `5dd477c`，
  是一个已经落地到 `main` 的提交）。

**已有成例**：`c84c478` "把 `.tmp` 里的探针收成 `tools/api-test.ps1`"——
**这件事做过一次，而且做对了**。本条只是把同一个动作用到测量机器人上。

**风险 / 可逆**：低。抽共享源码动的是"新文件 + 两行 `Compile Include`"，机器人行为一字不变
（可以用"同一局、同一颗种子、逐字节相同的 dump"证明，这正是本仓库惯用的保真判据）。
`git worktree prune` 可逆（worktree 可重建）。

**谁定**：🔧 工程。登记册 **D10** 明确说了"量 M（**复用 §4.1 那条机器人**，做丢弃式反事实）"——
也就是说**已经有一件事在等这个机器人**：这条结构欠账不是假想的，它是 D10 的前置。

> **2026-10-04 补记：这一轮只补设计，S4 仍未关闭。**
> 同一轮的 S1/S2/S3 都收干净了，这一条按"宁肯停在干净边界"处理——**没有动手**，
> 只把落地形状与验收判据写死，免得下一个人从头再想一遍：
>
> - **机器人的家**：`engine/tests/MeasurementRobot.cs`（新文件，`internal static`）。
>   把 `PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun`（`PrestigeTests.cs:25~80`）
>   里那段"8 次点击 → `TestGame.BuyGreedily` → 点金猫 → 答掉待答表态 → 能舍命就舍命 → `Simulate`"
>   抽成一个**带命名参数**的入口（种子 / 包 / 步长 / 游戏小时上限 / 要不要答待答表态 / 是不是从
>   `Era >= 5` 起改 0.25 秒步长），返回一份可 dump 的记录。
>   **这四条约定原来只活在注释里，抄错一处不会红**——那正是本条记录的复发机制，所以它们必须是参数。
> - **谁编它**：走**共享源码**，不新增 `ProjectReference`（成例是
>   `NekoClicker.Core.Tests.csproj:42~44`，以及第 40 行立的规矩"共享源码已经成立，就不为它冒险改结构"）。
>   探针工程的 `.csproj` 加一行 `<Compile Include="..\..\engine\tests\MeasurementRobot.cs" />`；
>   测试工程本来就自动收 `engine/tests/*.cs`。`PrestigeTests` 那条守卫改成**调用它**，行为一字不变
>   （所以它同时是这条抽取的第一道守卫）。
> - **验收判据 = "同一颗种子 → 同一局 → 逐字节相同的 dump"**，分两步、两步都要过：
>   ① **抽取前后**同一个 seed 的 dump **逐字节相同**（SHA-256 相同）——这是"抽出去没有改行为"的证据，
>   也是本仓库六轮外置一直在用的那套保真手法；
>   ② **同一份源码、同一个 seed 跑两遍** dump 逐字节相同——这是"机器人是确定性的"的证据
>   （**没有这一条，"同一颗种子 → 同一局"就只是句愿望**）；
>   ③ 探针侧复算同一份 dump，哈希与测试侧一致——这才是"只有一份实现"而不是"两份看起来一样"。
> - **剩下的两半没动**：测量产物与 `analyze*.py` 仍然只在 `.tmp`；`.tmp/wt-stages` 那个
>   仍然登记的 `git worktree` 也还在（`git worktree prune` 要先确认没有别的会话在用它）。

---

### S5 一份环境契约抄了三遍，另有两条路径绕过它

**是什么**。`tools/dnet.ps1` 的存在理由是两条硬约束（第 5~11 行）：沙箱里 `%USERPROFILE%` 不可写，
所以 CLI home / NuGet 缓存要重定向到仓库内；受限沙箱禁止命名管道，所以 MSBuild 必须 `-m:1`
（否则多节点构建**静默失败**：输出 `Build FAILED` 而 0 Error）。

那 7 行环境变量被**逐字抄了三遍**，每份的注释都写着"与 X 完全一致"：

| 位置 | 内容 |
|---|---|
| `tools/dnet.ps1:24~30` | 原件 |
| `tools/web.ps1:35~41`（注释在第 33~34 行说明"刻意不调用 dnet.ps1"） | 副本 1 |
| `tools/api-test.ps1:61~67`（注释在第 59~60 行说明"与 … 完全一致"） | 副本 2 |

而 `'-m:1'` 这个字面量被**手工重复在 8 处**：`dnet.ps1:47`、`api-test.ps1:199`、`api-test.ps1:266`、
`start.ps1:88`、`web.ps1:50`、`web.ps1:55`、`web.ps1:73`、`web.ps1:78`。

**绕过者**：`tools/start.ps1` 在**真正启动宿主**的两处用的是**裸 `dotnet`**：

- 第 88 行 `& dotnet run '-m:1' --no-build --project "$root\games\hosts\Web" -- '--urls' …`
- 第 116 行 `& dotnet exec $dll @demoArgs`

这两处**没有** `DOTNET_CLI_HOME` / `NUGET_PACKAGES` / `MSBUILDDISABLENODEREUSE`。
今天能跑，靠的是 `--no-build`（不触发 restore）这个巧合——也就是说**契约的保护范围取决于调用点碰巧做了什么**。

**为什么贵**：漏一处的症状是**静默失败**（不是红字）。这正是 `dnet.ps1` 头部第 9~11 行和
`web.ps1` 第 33~34 行各自写下的那段解释存在的原因——**每加一个脚本都要重新解释一遍**，
而解释不能防止下一个人漏。

**代价**：S。让 `dnet.ps1` 支持"自己决定动词"的用法（例如 `dnet.ps1 run --project … -- --urls …`，
或把 7 行抽成 `tools/env.ps1` 被 `dot-source`），然后把上面 8 处 `-m:1` 与两处裸 `dotnet` 收进去。

**风险 / 可逆**：中偏低，而且是**这类改动里最需要小心的一类**：
`tools/*.ps1` 必须 **UTF-8 with BOM**（`DEVELOPMENT_SUMMARY` §3.3，实测剥掉 BOM 后 5.1 按 GBK 解析、
报 20 多条 parse error、**一项都没跑**）；`dnet.ps1` 刻意**不用 `param()` 块**（第 18~20 行：声明参数会让
`-o` / `-n` 被当成公共参数解析）。改这些脚本要**按字节替换**、保留 BOM。
**这条规矩现在有守卫了**（`cf40b9d`，2026-10-04，见 [OPEN_WORK](OPEN_WORK.md) §0.19）：
`engine/tests/ToolingHygieneTests.PowerShellScripts_CarryTheUtf8Bom` 在 `-Strict` 里扫全仓 `*.ps1`
（跳过 `.git` / `.tmp` / `bin` / `obj` / `artifacts` / `node_modules`），开头不是 `EF BB BF` 就红**并点名文件**。
放在 C# 用例而不是 `build.ps1` 里的理由：它在**所有 `.ps1` 之外**执行，所以连 `build.ps1` 自己被剥了
BOM 都抓得到；而 `web-smoke.mjs` 可以被 `-SkipWebSmoke` 跳过，放那里闸门会漏。
渐进做法：先把 `start.ps1` 那两处裸 `dotnet` 改成走 `dnet.ps1`（收益最大、风险最小），再谈抽公共块。

**谁定**：🔧 工程。

---

### S6 "当前数字"以散文形式写在 10 个以上文件里（登记册 W1 的结构层）

**是什么**。同一件事——"现在有几个用例"——被手写在很多地方，而且**每加一条守卫都要人工扫一遍**。

**证据（我数到的，方法与结果都写出来）**：

- 引擎侧当前值 **531** 出现在：`README.md` 第 77 / 185 / 416 / 430 行、`STATUS.md` 第 105 / 135 / 175 / 183 行、
  `engine/README.md:21`、`games/README.md:85`、`engine/docs/RELEASING.md:25`、`engine/docs/VERSIONING.md:227`、
  `engine/docs/WEB_EXTENSION_PLAN.md` 第 398 / 576 / 656 行、`tools/api-test.ps1:12`、
  `.github/workflows/ci.yml:15`、`CHANGELOG.md:20`。
- 冒烟侧**同一个套件有三个数**：`STATUS.md:177` / `RELEASING.md:27` / `VERSIONING.md:229` / `CHANGELOG.md:20` 写 **108**；
  `OPEN_WORK.md`（§0.12 段）记的是 **128**；而 `618a46c` 提交后的 `tools/web-smoke.mjs` 里有 **135 处 `check(` 调用点**。
  （方法：`git show HEAD:tools/web-smoke.mjs | Select-String '^\s*check\('`；**这是调用点数，不是运行器报的数**——
  循环体内的调用会被多算。这正是问题本身：**只有运行器自己知道真值，而它没有把真值交给任何机器**。）
- 漂移不是假想：`DEVELOPMENT_SUMMARY` §7（第 529~537 行）是**一次**人工审计的结果，列出 **7 类**"现在时说法与实测不符"
  （运行器行数、版本号、`api-test` 项数三套说法、`PublicApi.txt` 行数、协议用例条数……）。
- 这个数确实一直在动：`TUNING_ANALYSIS` §2 记着套件墙钟由单条 `PrestigeTests` 决定；两天内它走过
  516 → 531（`OPEN_WORK` §0.12 与 `CHANGELOG.md:20`），冒烟走过 103 → 108 → 128。

**结构层的重点**（这才是我要加的，**立即清理归 W1**）：**仓库已经发明了正确的做法，只用在一处。**
`ARCHITECTURE.md` 第 269~272 行写着：

> **用例总数与运行器行数刻意不写在这一节。** 它们每轮改动都在变，写死就会烂——这一节曾经写着
> "200 行运行器 / 210 个用例"，两个数都早已不是真的……**以 `tools\build.ps1 -Strict` 打印的那一行为准**。

**指针代替数字**这条规矩已经成立、已经被写下来、已经在 `ARCHITECTURE` 生效——而另外 10 个文件仍写死数字。
所以这条的结构修法不是"再扫一遍"，而是二选一：

1. **推广已有规矩**：把那 10 处改成指针（"以 `build.ps1 -Strict` 打印的那一行为准"）；
2. **或者让真值可机读**：让运行器在结尾打一行**稳定 token**（例如 `NEKO-CHECKS passed=531 failed=0`），
   再让一条用例或 CI 步骤去核对文档里的数字——**数字错了就红**。
   `tools/api-test.ps1` 已经走了同一类路（它收尾**自审检查点覆盖**，见 `f27a2e7`），是现成的先例。

**代价**：S（第 1 种）/ M（第 2 种）。**风险**：低；但**不要批量替换数字**——
`OPEN_WORK` §0.4 记着批量替换**被自己的保险拦下**过（"命中数多于预期，说明这些数字还有别的出处"）。
历史数字（各版本自己的"N → M"、发布记录、CI 运行记录）**按规矩不动**。

**谁定**：🔧 工程。**登记册 W1 拥有立即清理**；**W3**（`FRONTEND_CHECKS.md:37` 那句"256 条前端逻辑"）是同类的残留，一并归它。

---

### S7 `text.json`：同一个值存两处，而守卫只查"代码 ↔ 文件"

**是什么**。内容外置（六轮）之后，同一份事实在两个分区里各存一份；派生文案（成就名、三档升级说明）
是**按 id 展开的成品**，而不是渲染出来的。

**证据（我自己数的，方法写清）**。对 `engine/content/*/text.json` 逐文件解析后比对：

| 重复形态 | 量 | 今天一致吗 | 有守卫吗 |
|---|---|---|---|
| `buildings.<id>.icon` vs `achievements.<id>_x1.icon` | **104 对** | 全一致（0 处不符） | **没有** |
| `endings.<id>.name` vs `achievements.ach_<id>.name` | **29 对** | 全一致（0 处不符） | **没有** |
| `buffs.<x>.name` vs `goldenCookies.<y>.name`（如末世 `blackout` = 全城断电） | 同值成对出现 | — | **没有** |
| `upgrades.<建筑>_tier1/10/25.description` 三档同一句 | **85 组**（11 个包合计，我数出来的总数） | 三档说明逐字相同 | 不适用（这是物化的代价） |
| 全部字符串值中"重复出现的条目" | **1,499 / 6,131 = 24.5%**（11 包合计） | — | — |

**改名一座建筑的真实代价**：`buildings` 之外有 **634 条**字符串里嵌着 `「建筑名」`
（方法：把该包所有建筑名取出来，数"不在 `.buildings.` 路径下、且含 `「名字」`"的条目）。
每座建筑平均 **≈6 处**（末世 9 座 → 55 处）——成就名/说明、三档升级名与说明、若干叙事。
而**守卫只查一个方向**：`ContentTextFileTests.EveryBuilding_ResolvesItsTextFromTheFile`
（第 473~505 行）断言的是 `row["icon"] == building.Icon`、`EveryAchievement_…`（第 743~782 行）
断言的是成就自己那三行——**"文件里这两处彼此一致"从来没有人查过**。
`endings.<id>.name` 与 `achievements.ach_<id>.name` 今天**恰好**一致，是**人写对了**，不是结构保证的。

**这件事已经被记录、代价也是刻意接受的**（所以本条不是"发现了一个没人知道的坑"，
而是"这个坑的守卫强度配不上它的复发概率"）：

- `engine/tests/ContentTextFileTests.cs:43~45`：升级表"存的是**按 id 展开后的成品**，id 仍由代码算；
  **改名一座建筑不会重算那些文案**，这是已知且刻意接受的代价"。
- `TEXT_AS_DATA_PLAN.md:410`（第四轮）："改名一座建筑，那三条成就的文案不会跟着变"；
  `:538`（第五轮）："**代价（已知、刻意接受）**：改名一座建筑，那三档升级的名字 / 说明 / 图标……"
- `AGENT_ARCHIVE.md:23` 把它作为那位 agent 的"诚实标注"记了下来："成就文案是『按 id 展开的成品』、**同一值存在两处**"。

**代价**：
- **一致性问题守卫（S）**：把"同一个值的两处必须相同"写成一条横扫用例——
  104 + 29 对（外加增益/金猫同 id 的那些）。它不需要改任何数据，只要把今天**恰好成立**的事实变成**保证**。
  这是本条最划算的一步：**今天改任何一处都会立刻红**，而不是等玩家看到图标对不上。
- **别名或重生成（M，格式改动）**：真要让"改名一处"成立，就得选一条路——
  ① 文件里允许引用（`"$ref": "buildings.ruins.icon"` 这类，形状与 `$tables` 同一个空间）；
  ② 或者保留物化，但加一个**重生成脚本**（像 `tools/extract-lore-text.ps1` 那样的一次性工具 + 逐字比对判据）。
  两条路都改 `text.json` 的格式语义，所以**要么先定 schema 版本（见 S12），要么接受"格式又变了一次、仍然没有版本号"**。

**风险 / 可逆**：加守卫**可逆且零数据改动**；别名机制**改格式**，影响 11 个包的全部文件，
必须带"迁移前后 dump 逐字节相同"的判据（这是本仓库六轮外置一直在用的保真手法）。

**谁定**：守卫 🔧 工程；机制要 👤 决定（**D4**：自由表将来谁读 + `text.json` 要不要 schema 版本号）。

---

### S8 新内容包的登记散在手写清单里

**是什么**。Web 宿主把"有哪些内容包"做对了——**运行时发现**，加一个包**不用改宿主**
（`games/hosts/Web/PackageCatalog.cs:17~28`，注释明确写着"**运行时扫描发现，不是硬编码清单**。
这一点与 `Demo.Cli` 那边刻意不同"）。但其余地方是**手写清单**：

| 位置 | 形态 | 有守卫吗 |
|---|---|---|
| `games/hosts/Demo.Cli/ContentPackages.cs:47` | 11 条 `ContentPackage` 记录（每条还带该包语气的欢迎语 / 转生叫法 / 帮助要点 / 报告提示） | 无（`FrameRenderTests` 只用 `Default` 与两个包） |
| `tools/start.ps1:94` | 11 个 id 的**裸字符串数组**（`start.cmd list` 用它） | **无** |
| `engine/tests/TestGame.cs:226` | 11 行 `(名字, 内容)` 表 | 无（但 `ContentTextFileTests.Table_CoversExactlyThePacksThatHaveATextFile` 从另一侧守"有 text.json 的包"） |
| `engine/tests/ContentTextFileTests.cs:51~239` | **10 张表**：`Externalized` + `ExpectedBuildings` / `Eras` / `Endings` / `Stances` / `Choices` / `Achievements` / `Buffs` / `Upgrades` / `GoldenCookieOutcomes` | **有**，每张都配一条 `*CountTable_CoversExactlyTheGuardTable`（这正是 `CONTENT_AUTHORING.md:927` 说的"新包**不可能忘记登记**期望数"） |
| `NekoClicker.sln`（17 个工程）+ `NekoClicker.Core.Tests.csproj:15~26` | 每个包一条 `ProjectReference` | 编译器会管 |

**为什么贵（诚实的量级）**：加一个包要动 **≈16 处**，其中 **10 处在一个测试文件里**、
但**那 10 处有守卫兜着**（忘了登记就会红——这是设计，不是欠账）。
**真正没有守卫的只有两处**：`start.ps1:94` 与 `Demo.Cli/ContentPackages.cs`。
所以这条的**结构价值不是"少写几行"，而是**：

1. 现在**同一件事有三种做法**（运行时发现 / 手写表 / 手写裸数组），新来的人要先读三处才知道该跟着哪个；
2. `start.ps1:94` 漏一个包的症状是**静默的**（`start.cmd list` 里少一行），而它**连"有 text.json 的包"这种旁证都没有**；
3. 阶段 5 那种"批产四个包"的场景里，这份清单就是**手工工序**——`games/docs/STAGE_5_RESKINS.md` 整份文档存在的理由就是它。

**代价**：M。最小步：`start.ps1` 的 id 列表改成**从 Web 宿主的发现结果或测试表里取**（或至少加一条用例
断言"仓库里的内容包目录数 == 那张表里的行数"）。较大步：让 `Demo.Cli` 也走发现，
只把**必须手写的语气文案**留成一张可选的覆盖表（缺省值由发现补）。

**风险 / 可逆**：低（加守卫）/ 中（改 `Demo.Cli`——那里的 `Welcome` / `HelpTips` / `PrestigeActionName`
是**按包写的文案**，不是可推导的；`PackageCatalog.cs` 的注释正是在解释"宿主这边不该有那些东西"）。

**谁定**：🔧 工程。

---

### S9 "没人读的字段"只在前端有守卫

**是什么**。`618a46c` 新增的 §23（`tools/web-smoke.mjs:1702~1825`）是**这一类缺陷的第一个守卫**：
它走一遍真夹具里的每个字段，要么 `app.js` 的代码读它，要么进 `NOT_DRAWN` 表写一句理由，
而且**反向也查**（"过期的借口也是红"）。它把这一类的历史事故写在了注释里：

> 五次都是**人注意到**或**代理审计**发现的……`buildings[].cpsEach`（§22，2026-10-03 修）
> （`tools/web-smoke.mjs:1680~1682`）

**残余（这才是本条要说的）**：那节自己写明了射程——"**前端 = app.js**：index.html 与 app.css 都画不出快照里的字段"。
于是下面这些**仍然没有守卫**：

1. **终端宿主侧**：没有任何东西会发现"`TerminalUi` 不再读某个视图字段"。
   `FrameRenderTests` 只关心渲染出来的帧，不关心"字段有没有人读"。
2. **`engine/core` 的公开视图面**：`BuildingView` / `UpgradeView` / `AchievementView` … 一共几十个属性，
   没有任何一条用例回答"这个属性有读者吗"。我手工数出来的实例（`field` 在 app.js 里的出现次数，
   注释剥掉后按 `.字段名` 数）：

   | 字段 | app.js | Demo.Cli | 结论 |
   |---|---|---|---|
   | `buildings[].sellRefundRate` | 0 | **0** | **全仓零读者**：`GameViewFactory.cs:80` 赋值、随快照出门、`PublicApi.txt:1696` 冻结、**没有任何宿主或用例读它**。而终端画"卖出"用的是 `Engine.Balance.DefaultSellRefundRate`（`TerminalUi.cs:614`）——**视图上那份谁都不看** |
   | `buildings[].unitPrice` | 0 | 2（`TerminalUi.cs:336`、`HeadlessRunner.cs:183`） | 只有终端读 |
   | `buildings[].nextMilestoneAt` / `nextMilestoneName` | 0 | 1（`TerminalUi.cs:457`） | 只有终端读 |
   | `upgrades[].category` / `upgrades[].tier` | 0 / 0 | 0 / 0（CLI 不读 `Category`；`Views.cs:73` 反而写明"前端不许自己按前缀筛"） | **全仓零读者**（只有构建期用定义上的 `Category`） |

   §23 已经把这几个写进 `NOT_DRAWN` 并给了理由（第 1736~1745 行）——**这是正确的处理**：
   它把"没决定"变成了"决定记录"。但它**只解决 Web 这一侧**，而且它**不解决"该不该继续把没人读的字段推上线"**。

3. **成本已经不可逆的地方**：公开成员一旦进了 `engine/core/PublicApi.txt`，
   按 `VERSIONING.md:96` **删除就是 major**。所以 `SellRefundRate` 这种"零读者的视图字段"
   **今天只能靠一次 major 才能拿掉**。它的字节成本可以忽略（常量字段只在全量帧里出现一次），
   **真正的代价是公开面永久变宽 + 每个读代码的人都要问一次"这个是给谁用的"**。

**代价**：S~M。
- **S**：把 §23 的判据搬到**终端侧**（同一张 `NOT_DRAWN` 表的 CLI 版）——但更好的形状是
  一次覆盖**两个宿主**：把夹具里的字段路径与"两个宿主各自的读取情况"列成一张表，谁没读就得有理由。
- **M**：在 `engine/tests` 加一条**枚举公开视图属性**的用例，要求每个属性要么有读者、
  要么在一张"**没人读，理由是 X，打算 Y 处理**"的表里。这条与 §23 是**同一个机制、两个方向**，
  而且它能把"该不该继续推"变成必须回答的问题。

**风险 / 可逆**：低。**不要顺手删字段**——删就是 major（`VERSIONING` §2）；
本条的产出应该是**决定记录 + 守卫**，而不是 API 变更。与 **D11**（未持有的建筑该显示什么产量）相邻：
那是"**该新增**一个字段"的决定，本条是"**已有的没人读**"的守卫，两者都落在同一个字段集合上。

**谁定**：🔧 工程。若涉及"这个字段到底该不该留"，归 👤 决定。

---

### S10 「拥有 ⇒ 已解锁」只有一个包的私有用例在守

**是什么**。内容里有一条隐含不变量：**手上有的建筑必须是已解锁的**。
它只在一种情况下会被打破——**开继承**：解锁条件用「本轮累计赚取」时，
舍命/重启把本轮累计清零，于是继承进来的建筑**显示成未解锁**（产量照算，但列表里看不到、也卖不掉）。

**证据**：

- 规则写在 `CONTENT_AUTHORING.md` §11.1（第 560~577 行）：不继承的包用 `EarnedThisRunAtLeast`（推荐），
  **只有做继承的包**必须换成 `EarnedAllTimeAtLeast`。理由是"继承会打破一条原本空成立的隐含不变量：`拥有 ⇒ 已解锁`"。
- 实测分布（数 `engine/content/*/*.cs` 里 `EarnedAllTimeAtLeast` / `EarnedThisRunAtLeast` 的出现次数）：
  **末世 11 / 图书馆 10，另外 9 个包各 1~2**（那 1~2 处是成就或图鉴，不是建筑）。
  与 `TUNING_ANALYSIS.md:319~320` 的实测一致（"9 个包里只有末世与图书馆的解锁门是历史累计"）。
- **守卫只在一个包的私有方法里**：`ApocalypseContentTests.OwnedBuildings_StayUnlockedAfterARestart`
  （第 240 行）→ 它调用**私有**辅助 `SnapshotAndCheckUnlocked`（第 340~351 行），
  断言"`Owned > 0` ⇒ `IsUnlocked`"。**别处没有**。
- 同一条规则在 `games/docs/STAGE_5_RESKINS.md` 里是**第 13 条手工 checklist**：
  "**只有做继承的包**才必须换成 `EarnedAllTimeAtLeast`，否则『拥有但未解锁』"。
- `TUNING_ANALYSIS` §4.7(b) 第 316~325 行把代价说透了：想把继承推广到其余包，"**改继承就得一并改那 8~9 条解锁条件**"，
  而反事实里"两者一起改"所以没法分开说"解锁门的改动本身改变不改变节奏"。

**为什么贵**：下一个打开继承的包，如果漏改 25~50 条解锁条件，症状是**玩家手里有 50 座建筑却全是灰的**——
而**唯一会红的用例属于末世**。也就是说：这条不变量与"哪些包开了继承"是耦合的，
但守卫是**按包写死的**，而按包写死的东西"新包不会自动被扫到"——
这正是 `PrestigeTests` 那条守卫被改成从 `TestGame.AllEraPacks()` 派生时留下的教训
（`engine/tests/PrestigeTests.cs:19~22` 与 `TestGame.cs:241~248` 都写着这件事）。

**代价**：**S**。写一条**横扫全部包**的用例：对每个包造一个"有若干建筑 + 本轮累计很低"的状态，
`MarkDirty()` → 快照 → 断言 `Owned > 0 ⇒ IsUnlocked`（未开继承的包天然成立，等于顺手给它们也上一道锁）。
`ApocalypseContentTests` 里那段现成的构造逻辑可以直接搬（它已经覆盖"再来一次，确保不是第一次恰好没事"）。

**注意不要顺手做的那件事**：**不要**提议"把所有包的解锁门统一成历史累计"——
`CONTENT_AUTHORING` §11.1 明确说这是**按包**的正确选择（不继承的包用「本轮累计」才有"每层重新揭示"的节奏），
而 `CONTENT_AUTHORING` §11.2.6 / `TUNING_ANALYSIS` §4.10 说"末世/图书馆要另一条规则"是**阶段**那边的问题。
统一门槛是 👤 决定（登记册 **D9** / **D2** 的辖区），本条只要**守卫**。

**风险 / 可逆**：低。新增用例；不改任何内容、不改任何门槛。

**谁定**：🔧 工程。

---

### S11 `tools/` 里闸门、环境壳、一次性档案混在一起

**是什么**。`tools/` 有 11 个文件，做的是**三类完全不同的事**，但长得一样（都是 `.ps1`）：

| 类别 | 文件 | 什么时候必须跑 |
|---|---|---|
| **闸门**（不改绿不许提交） | `build.ps1`（编 + 引擎用例）、`api-test.ps1`（真宿主端到端）、`web-smoke.mjs`（前端冒烟）、`pack.ps1`（打包产物） | 提交前 / 发布前 |
| **环境壳**（因为沙箱/环境才存在） | `dnet.ps1`、`seed-packages.ps1`、`start.ps1`、`web.ps1`、`play.ps1` | 按需 |
| **一次性迁移档案** | `extract-lore-text.ps1` | **不用跑**——`STATUS.md:255` 自己写着"迁移已完成，这个脚本**不再有运行期职责**……作为『迁移过程可复核』的存档保留" |
| **流程辅助** | `public-api.ps1`（有意改 API 后的最后一步） | 只在改公开 API 时 |

**为什么贵**：

1. **三个启动器**（`start.ps1` / `web.ps1` / `play.ps1`）的关系只能靠读注释搞清：
   `start.ps1:4~7` 说 `web.ps1` 的坑、`Start.ps1` 是"一体化"入口；`play.ps1:8~9` 说自己为什么不用 `dotnet run`。
   它们**各自都编一次、各有一份 URL/端口/包名拼装**（`start.ps1:60~88` 与 `web.ps1:53~74`
   是同一段"起宿主 + 等端口 + 开浏览器"，`start.ps1:78~79` 的注释正是在解释"为什么不用 web.ps1 那段"）。
2. **`extract-lore-text.ps1` 与闸门同形**：一个新人不可能从文件名或首行看出"这个不用跑"。
   （它**是刻意保留的**，不是死代码——本条不主张删它，只主张**标出来**。）
3. **顺序只写在文档里**：`RELEASING.md` §1 用一张 11 步的表规定"先跑哪个再跑哪个"
   （第 23 / 25 / 27 / 28 行……），包括"**先构建一次，再跑 `public-api.ps1`**（否则快照带旧版本号且看起来完全正常）"
   这种**错序不报错**的步骤。文档是对的，但它是**唯一**的地方。

**代价**：S。两种形状，选一种即可：
① `tools/README.md` 一张表（类别 / 什么时候跑 / 一条命令 / 谁拥有）；
② 或者每个脚本第 1 行加一个 ASCII 标记（`# load-bearing gate` / `# env shim` / `# one-shot archive`），
让 `grep` 得出来。**推荐 ①**：它同时是"一键验证"那句话的落点。

**风险 / 可逆**：极低（纯文档/注释）。注意 `.ps1` 的 BOM 规矩（见 S5）。

**谁定**：🔧 工程。

---

### S12 `text.json` 没有 schema 版本号

**是什么**。`text.json` 现在是一个**有语法的格式**：根节点上 `$tables` 是**保留清单**、
其它 `$XXX` 一律抛；条目形状被规定（`{text: 非空}`）；还有"声明为 free 的表不许被代码读"这种语义
（`TEXT_AS_DATA_PLAN` §13.1~§13.2）。**但它没有版本字段**：

- `ContentText.Load` 校验清单合法、`kind` 认识、声明的表存在、自由表非空、条目形状、全文重复键
  （`TEXT_AS_DATA_PLAN` §13.2 那张表），**没有一条是"这份文件是哪一版格式写的"**。
- 这件事被明确列为待定：`TEXT_AS_DATA_PLAN` §13.9 第 5 条（"要不要给 `text.json` 加 schema 版本号
  （`WEB_EXTENSION_PLAN` 的 D3）：同一个坑的另一面，本设计不顺手做"）。
- 对照面：**存档**那边有整套机制（`ISaveMigration` + `SaveSerializer.Migrations`，
  `ARCHITECTURE` 的扩展点表与 `VERSIONING` §6），"第一次真改格式怎么验"至少有一条演练路径（登记册 **D5**）。
  **格式**这边什么都没有。

**为什么贵**：第一次**破坏性**的格式改动（S7 的别名机制就是一个候选）落地时，
读取侧只能靠"结构看起来不对就抛"来发现——而"抛"只覆盖**结构**，
覆盖不了"同一个键在 v1 里是字符串、在 v2 里是对象"这类**语义**变化。
按 `VERSIONING.md:130~131` 的说法，这类改动"**快照守卫抓不到（字面没变）**"。

**代价**：**S**（只加"认得出/认不出"）：在 `Load` 里接受一个可选的 `"$version": 1`，
缺省 = 1；遇到比自己新的版本号**当场抛并点名**。**这与 `$tables` 用的是同一个保留键空间**，
所以不需要新概念。**要动 `ContentText.cs` 行为**（虽然公开签名可以一行不改），
所以按 `TEXT_AS_DATA_PLAN` §13.5 那条先例，**要先想清楚它算 patch 还是 minor**（登记册 **D3** 正在处理那笔账）。

**风险 / 可逆**：低（拒绝未知版本是"只在坏输入上生效"的一类行为改动，与 §13.5 的诚实边界同型）。
**不做的理由也是真的**：今天 11 个包的文件全都是同一个版本，加版本号是**为一个假想的未来付费**——
所以本条落在**最后一名**，而且它该由 **D4** 一起拍（"自由表将来谁读"与"格式要不要版本化"是同一个洞的两面）。

**谁定**：👤 决定（**D4**），实现 🔧 工程。

---

### S13 唯一的人类数据仪器有两份实现，而默认启动器跑的是不落盘的那份

> **✅ 已关闭（2026-10-03，`4a89cee`；现场与判别力抄在 [OPEN_WORK](OPEN_WORK.md) §0.15.2）**。
> 走的是本文的**形态②（收成一份）**：新增 `games/hosts/Shared/ChoiceLatencyLog.cs`
> （`LatencySample` / `LatencyLogFormat` / `LatencyLogFile` / `ChoiceLatencyLog`）与
> `Shared/RepositoryPaths.cs`，两个宿主各自 `Compile Include` **同一份源码**，
> `Demo.Cli/ChoiceLatencyLog.cs`（90 行）、`Web/ChoiceLatencyLog.cs`、
> `Web/RepositoryPaths.cs` 删除；测试项目改用 Demo.Cli 编好的那一份（再编一份会撞 CS0436）。
> **默认启动器（终端交互模式）现在默认写** `artifacts/latency.txt`——与 Web 宿主同一个
> 文件、同一条仓库根判据；`--simulate` / `--frame`（机器人作答）**默认不写**，要写必须显式
> `--latency-log`。`start.ps1` 的默认模式没改（换默认体验是另一个决定）。
> 判定为"**没写完**"而不是"刻意不落盘"的三条证据、以及"S/U 行一列不改 + 会话行加
> `host=` + 短 `HeaderSignature`（免得把 21 个真人会话行误判成别人写的内容）"都写在
> 那份类的注释与 §0.15.2 里。新增 5 条守卫；真实样本（临时路径）已产出：
> `S	…	company	choice_first_order	order_take	3.833	0.004	30	1`，
> 仓库那份真人文件指纹未变（2695 字节 / sha256 `FB64894F…`）。
> 下文保留原文（它记录的是**当时**的两份实现与"为什么默认路径产不出样本"）。

**是什么**。全项目只有**一个**参数**没有人类数据支撑**（结局宽限那 30 秒；工程那半早已做完，
现在是"真人从看到表态到作答要多久"这个事实本身）。量它的仪器叫 `ChoiceLatencyLog`，
而`ChoiceLatencyLog` **在两个宿主里各有一份实现**：

| | `games/hosts/Demo.Cli/ChoiceLatencyLog.cs` | `games/hosts/Web/ChoiceLatencyLog.cs` |
|---|---|---|
| 行数 | **90** | **446** |
| 记什么 | 模拟秒（`PlayTimeSeconds` 之差） | **模拟秒 + 真实墙钟秒 + 选项 id + UTC 时刻** |
| 落盘吗 | **不落盘**——只有 `Answered` 事件（`GameSession.cs:89`）与 `Summary()`（`GameSession.cs:504`，进游戏内日志面板 `:514`） | **写文件**：`LatencyLogFile`（`:151`）→ `artifacts/latency.txt`，带自述表头、"已有别人写的内容只追加不覆盖" |
| 谁在守它 | 无专门用例 | `WebChoiceLatencyTests`（把格式当纯函数钉住；`engine/tests` 用 `Compile Include` 共享这份源码，`NekoClicker.Core.Tests.csproj:43`） |

**为什么贵（不是"代码重复了 90 行"，而是回路走不通）**：

- `tools/start.ps1:18` 的**默认模式是 `play`**，也就是**终端宿主**——而终端宿主那份仪器
  **一个字节都不写盘**。所以"照默认方式玩一局"**不可能产出样本**；
  要贡献样本必须先知道"得用 `start.cmd web`"（或者读 `OPEN_WORK` H1 那一行）。
- 可核对的现状：`artifacts/latency.txt` —— **37 行、`S`（样本）行 0 条、`U`（如实记的不可量）1 条、
  "session start" 21 条**（我读到的那一刻 mtime `2026-10-03T21:46:42`）。
  也就是说：**这份文件记得住 21 次宿主启动，记不住一次作答**。
  （21 条都是真宿主启动——测试路径写的是另一个目录 `artifacts/latency-tests/`。）
- 两份实现已经在**格式上分叉**：终端那份是"内存里的样本 + 一句汇总"，
  Web 那份是"可追加、可被第三方读、表头自述、与旧埋点共存"的文件格式。
  于是"这个数只有这一个来源"这句话里，"这个来源"其实是**两个宿主的两种口径**。

**代价**：**S**。两种形状：
① 最小——把"终端宿主也能贡献样本"接上（复用 Web 那份格式；它已经在测试工程里以共享源码存在，
所以不必新写一个格式）；② 更彻底——把仪器收成**一份**（像 `SnapshotProtocol` 那样共享源码），
让两个宿主写同一个格式、同一份头。
**不要**顺手把 Web 那份搬进 `engine/core`：它是**宿主侧的埋点**，`engine/core` 不认识文件系统布局
（`ARCHITECTURE` 的依赖方向）。

**风险 / 可逆**：低。风险只有一条真实的：`artifacts/latency.txt` 里躺着**一份旧埋点实现**留下的内容
（`latency.txt.old-probe` 也在），所以写入端**必须保留**"别人写过就只追加 + 补一段表头"那条行为
（`WebChoiceLatencyTests` 有用例钉着它）。

**谁定**：🔧 工程。**取样本这个动作归真人**，那是登记册 **H1**（本文不重复它的动作，只指出
"默认路径产不出样本"这个结构原因）。

---

## 三、我**明确排除**的（并指向它的家）

| 排除的东西 | 为什么不属于本文 | 归谁 |
|---|---|---|
| 任何门槛 / 价格 / 倍率 / 层数该是多少 | 那是**数值**，本文一条都没量也不该量（`TUNING_ANALYSIS` §三、§4.8 已经把"我没做什么"写清楚了） | `TUNING_ANALYSIS` + 登记册 **D2 / D9 / D10** |
| "未持有的建筑该显示什么产量"（`cpsEach` 在 `owned == 0` 时恒为 0） | 那是**口径决定**（平均 / 边际 / 都不做），属于玩法语义 | 登记册 **D11**（`WEB_BUILDING_RATE_FEEDBACK` §4.2 是它的家） |
| 扁平「升级」列表保不保留、📖 位置挤不挤、sheet 手感 | **界面**决定与**观感**，只有眼睛能判 | 登记册 **D1 / H3** |
| `FRONTEND_CHECKS.md:37` 那句"256 条前端逻辑" | 计数漂移的**同一族**，登记册已经点名 | 登记册 **W3**（本文 S6 只给结构层） |
| `api-test.ps1` 按端口杀宿主在沙箱下静默失效 | 是**具体缺陷**，已被登记、有坐标 | 登记册 **W2**（与本文 **S3** 共用"怎么找占用者"这一个知识点） |
| 套件墙钟 / 单步成本 | 要**先测**才能谈，属性能问题 | 登记册 **W4** |
| `PackageId` / `IsPackable`、打包产物自检、CI 的 Linux 作业、1.5.0~1.9.0 补发 | 都是**已登记的具体欠账**，各自有行 | 登记册 **W5 / W6 / W7 / W8**，`D8` |
| 跨版本存档的真人验证、公开 API 快照的用法 | 已登记或被 `VERSIONING` 完整拥有 | 登记册 **W9 / W10**，`VERSIONING` §2~§4 |
| 自由表（`$tables`）**没有读者** | 那是**刻意**的（`TEXT_AS_DATA_PLAN` §13.4 "v1 的答案是『不读』"），不是死代码 | 登记册 **D4**（本文 S12 是它的结构面） |
| `WEB_EXTENSION_PLAN` 的 `ui.json` 声明式界面扩展（整份已搁置） | **今天没有一个包需要它**——而它原本要填的那个洞（"一座建筑的某段故事没地方写"）**已经由自由表补上了**（`TEXT_AS_DATA_PLAN` §13.7 自己这么写的）。所以本文**不主张**启动它 | 👤 决定（登记册 **D4**，`WEB_EXTENSION_PLAN` §11 的 D1~D9） |
| `NextMilestoneAt` 该不该在 Web 上画 | 是**界面**问题，而且 `web-smoke` §21 已经明确"页面不许画它" | 登记册 **W11 / D11** 一带 |
| 把架构重写一遍 | **不是本文的主张**。本文 13 条里**没有一条是 L**，全部是"让下一次改动便宜一点"，没有一条要求换形状 | — |

---

## 四、我会先做的两三件（以及为什么是它们）

**第一件：S1（把 `web-smoke.mjs` 接上闸门）。**
理由是"**一条命令验证全部**"这句话今天**是假的**，而仓库刚刚为最贵的那一类缺陷（复发 5 次）
造了一条新守卫（§23，210 行），**把它放进了唯一一条没有自动触发器的套件里**。
收益是**乘数**的：接上之后，`wwwroot/` 的每一次改动、每一次协议改动都会自动过一遍
"页面真的跑得起来 + 每个字段都有人决定过"；不接，那 210 行守卫的执法者**仍然是人的记忆**——
而 `FRONTEND_CHECKS.md` 全篇就是在讲"人的记忆不够"。
代价是**一行 CI + 一行 `build.ps1`**，而且这条套件不需要 .NET、不需要宿主、不需要网络。**没有比它更划算的一步。**

**第二件：S3（`build.ps1` 的占用失败路径会说话）。**
理由是它**挡在每一轮工作的入口上**：今天任何一次并发构建都会得到 144~170 条 copy 错误，
而**读的人从错误里得不到任何可操作信息**——`DEVELOPMENT_SUMMARY` §3.4 专门为此写了一整节处置规程，
说明这个诊断动作已经被反复手工做过。更糟的是**这条路径有恶性记录**（约 320 个 `dotnet` / 9.9 GB / 玩家侧卡死），
而规范做法（**先查再动手、能等就别杀**）**仓库里已经有现成的代码**（`start.ps1:39~50`），只是没长在失败路径上。
它同时**保护一个正在服务的真人**：诊断里必须明确"5273 上有一个宿主，**不许自动杀**"。

**第三件：S4 的前半（机器人只有一个实现）。**
理由是它**已经在挡着一件被登记的事**（**D10** 要"复用 §4.1 那条机器人"做丢弃式反事实），
而且它是**唯一一条会随每次测量重复付费**的欠账：两周内至少重抄 4 次，抄错一处不会红、
只会让结论偏掉——而结论会被写进文档、被后面的决定引用。
机制是现成的（`Compile Include` 共享源码，测试 csproj 已经在用它保证"只有一份实现"），
风险低，而且有一条**很近的验收**：抽完之后用同一颗种子跑同一局，dump 逐字节相同。

> **不选 S6（数字单一出处）当第一件**：它虽然便宜，但登记册 **W1** 已经在手上，
> 而且它**不改变任何验证能力**——它省的是"扫一遍文档"的时间。
> 上面三件省的分别是"整套前端验证""每一轮构建的入口""每一次测量"。
>
> **另外：S13 是本文最便宜的一条，也是唯一一条"只差接线"的**（终端宿主那份仪器不落盘，
> 而默认启动器跑的正是它）。它没进前三只是因为它的收益**只落在一个人一件事上**——
> 而"那件事"恰好是登记册 **H1**：全项目唯一没有人类数据的参数。**若你只想动一处、又想立刻见效，就是它。**

---

## 五、我**没能**量的（诚实清单）

1. **那 320 个 `dotnet` 进程的成因**。仓库自己标了边界（`AGENT_ARCHIVE.md:19`：与并发构建
   "只有时间相关性、**没有解释**"；`DEVELOPMENT_SUMMARY` §8 第 1 条重申）。我没能把它与
   `dnet.ps1` 的 `-m:1` / `MSBUILDDISABLENODEREUSE=1` / `start.ps1` 的裸 `dotnet` 联系起来。
   **本文不主张任何因果**：S3 的成立只依赖"144 条错误里没有一行说谁占着"这个**观察**。
2. **`tools/web-smoke.mjs` 真实跑出来的条数**。我只数了 `check(` 的**调用点**（135），
   没有运行它（另一轮正在改这个文件，而且运行它会写临时目录）。
   **权威来源是运行器自己打印的那一行**——这也正是 S6 想要的形状。同理，**我没有跑任何一条构建或测试**
   （`build.ps1 -Strict` / `api-test.ps1` 都按约束没跑：5273 上的宿主正为真人服务、并且占着 `bin`）。
   所以"当前 531 / 128 / 135"这些数**我一个都没有亲自复核**，只核对了它们**写在哪些文件的哪一行**。
3. **`WebSnapshotProtocolTests.FrontendContract_FieldNamesAndTheModeToken` 那张手写字段表的代价**。
   我读懂了它的形状（约 23 个**顶层**键、手写、只查"前端要的线上有没有"这一个方向），
   但**没有量**"它多久会漏一次"——它守的那类事故（`mode` 是数字）只发生过一次。
   本文没有为它单列一条：因为 §23（S9）已经把**嵌套字段**这一层接住了。**这条边界是我判断的，不是量出来的。**
4. **§23 的射程边界之外还有多少字段**。夹具只有**一个包（公司）**、43 个顶层键，
   而它自己钉住了 **4 个空数组盲区**（`buffs[]` / `goldenCookies[]` / `pendingLore[]` / `pendingChoices[]`，
   `tools/web-smoke.mjs:1763`）。我**没有找到**任何"只在别的包才出现的字段"（快照顶层键看起来与包无关），
   但**我没有逐个包抓快照去证明这件事**。**标记为：疑似没有，未证。**
5. **S7 的 634 / 1,499 / 104 / 29 / 85 这些数**是我用 PowerShell 解析 11 份 `text.json` 数出来的
   （比对脚本是纯 ASCII 的临时命令，没进仓库）。它们**没有经过第二个人复算**；
   尤其"重复值 24.5%"里**包含合法的巧合**（同一个词在两个地方各用一次），
   所以那个比例是**上界**，不能读成"24.5% 的字符串都该被合并"。
6. **S4 的"至少重抄 4 次"**：我数的是 `.tmp/` 里**现存的** 4 个探针工程（`EraDump` / `Round4Dump` /
   `Round5Dump` / `EraProbe-src`）＋ 3 个分析脚本＋文档里明写"逐行复制"的那一处。
   **被删掉的探针我数不到**（`.tmp/` 是 gitignore 的，历史上清理过），所以真实次数**只会更多、不会更少**。
7. **S1 里"CI 加一步就够了"这件事我没有实测**。`web-smoke.mjs` 需要 Node（本机有），
   它自己建临时目录并删除；但我**没有在 CI 的环境里跑过它**——CI 是 `windows-latest` +
   `powershell`（见 `ci.yml` 头部对解释器选择的说明），而套件是 `node`。
   这一步**看起来**只需要一个 `actions/setup-node` + 一行 `node tools/web-smoke.mjs`，
   但**"看起来"不是证据**，落地时要真跑一次（这也是登记册 **W7** 那条"CI 要联网"的同一类前提）。

---

## 六、怎么复核本文的每一条（写给下一个人）

| 要核的东西 | 一条命令 / 一个地方 |
|---|---|
| 当前 HEAD 与工作树状态 | `git log --oneline -1`、`git status --short`（本文引用的那一轮是 **`618a46c`**，写作时它在树上是 `22d7e30`） |
| S1：谁跑冒烟套件 | `git grep -n web-smoke -- tools .github "*.cmd"`（应**只**命中 `web-smoke.mjs` 自己） |
| S2：夹具从哪来 | `git check-ignore -v .tmp/capture-fixture.ps1`；`git grep -ln web-snapshot.json -- tools` |
| S3：失败路径 | `tools/build.ps1` 第 30~35 行；`tools/start.ps1` 第 39~50 行；`AGENT_ARCHIVE.md:47` |
| S4：机器人被抄过 | `git grep -n "SAME robot\|逐行复制" -- .`；`Select-String -Path .tmp -Pattern BuyGreedily -Recurse` |
| S4：worktree | `git worktree list`（今天列出 `.tmp/wt-stages 5dd477c [stage-work]`） |
| S5：契约抄了几遍 | `git grep -n "DOTNET_CLI_HOME" -- tools`；`git grep -n "'-m:1'" -- tools` |
| S6：数字散在哪 | `git grep -n "\b531\b" -- README.md STATUS.md engine games tools .github` |
| S7：两处不一致 | 解析 `engine/content/*/text.json`，比对 `buildings.<id>.icon` 与 `achievements.<id>_x1.icon` |
| S9：字段有没有读者 | `git grep -n "sellRefundRate\|nextMilestoneAt\|unitPrice" -- games engine/tests`；`tools/web-smoke.mjs:1721~1760` 的 `NOT_DRAWN` 表 |
| S10：不变量谁在守 | `git grep -n "StayUnlocked\|IsUnlocked" -- engine/tests`（今天只有 `ApocalypseContentTests`） |
| S11：`tools/` 有哪几类 | `Get-ChildItem tools -File`，对着 `STATUS.md:255`（那份档案）与 `RELEASING.md` §1（顺序） |
| S12：格式有没有版本 | `git grep -n '\$tables\|\$version' -- engine/core/Content/ContentText.cs` |
| S13：两份仪器 | `git grep -n "ChoiceLatencyLog" -- games`；`Get-Content artifacts\latency.txt` 数 `^S` / `^U` / session 行 |

> **最后一句**：本文每一条都写了"代价"与"谁定"，是因为**这张表唯一的作用是被人挑着做**——
> 挑不动、或者决定"不值得"的都该被划掉，而不是留着。划掉的时候请把**理由**留在那一行，
> 因为下一个人会问同一个问题。
