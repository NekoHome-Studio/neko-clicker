# 现状

> 记录时间：**2026-10-02**（版本行 **2026-10-03 补记**）｜ 分支 `main` ｜ 工作树版本 **1.7.0**
> （`39503d3` 落地，**未打 tag / 未推送**；已发布的最后一个 tag 仍是 `v1.4.0`）
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
| `main` | **唯一的开发线** | 工作树 = **1.7.0**（`39503d3` 落地，**未打 tag / 未推送**） |
| `v1.4.0` | 1.3.0 + 升级行的货币语义（`UpgradeView` 三字段）+ Web 的「永久」面板 | 已发布（tag 已推送）——**已发布的最新版** |
| `v1.3.0` | 1.2.1 + 离线收益进快照（`GameSnapshot.Offline`）+ Web 弹窗 + 打包产物内容根修复 | 已发布 |
| `v1.2.1` | 1.2.0 + 421 条散文铺满十个包 + `ContentText` 并发修复 | 已发布 |
| `v1.2.0` | 1.1.0 + Web 前端 + 两层重构 + 剧情外部化试点 | 已发布（已知含一个并发缺陷） |
| `v1.1.0` | 结局作答宽限期 + 套件并行化 | 已发布 |
| `v1.0.0` | 首个承诺公开 API 稳定的版本 | 已发布 |
| `feature/web-frontend-ui` | 旧分支，已并入 `main` | **作废**（远端还在，可删） |

**工作树版本 `1.7.0`**（`NekoClicker.Core` 的公开 API 版本）：**minor**——公开 API **只增不改**：
新增 `GameSnapshot.ModeName`（`string`）与 `PurchaseModes.WireName()`，数字形的 `mode` **原样留着**
（把已有字段的 JSON 类型改掉对所有 `/api/snapshot` 消费者都是一次静默破坏）。修的是线上那条
"Web 页面每帧在中段抛异常、一半面板从来没画出来过"的故障。兼容性口径、证据、以及
"两套测试当时为什么都是绿的"见 [CHANGELOG](CHANGELOG.md) 的 1.7.0 与
[OPEN_WORK](engine/docs/OPEN_WORK.md) 的 N 条。**未打 tag、未推送**——放行前的打包 / tag / 推送
是子代理**没做**的事（本会话约定）；要精确到当前提交，用 `git log -1`（本文不写 HEAD，见上）。

**`1.6.0`（已并入 `main`；下面这段只作历史）**：**minor**——公开 API
**一个成员都没有增删**，但它**同时是一处刻意的语义改动**：
结局的落定条件从 1.5.0 的"**玩家被展示过那批待答表态**"改成"**玩家把它们答完**"
（`GameEngine.MarkPendingChoicesShown()` 保留但**不再参与判定**，只剩诊断用途——
它记下"每条表态到底露过面没有"，见它的 XML 注释与 `GameState.Counters` 的 `$choice_shown_<id>`）。
于是"不会永远悬着"这条性质**又一次被有意扩大移除**——**从不作答的玩家永远拿不到结局**
（1.5.0 时是"从不看到面板"）。另外新增一条补丁规则：**答不上的待答表态不拦结局**
（内容改版删掉了它 / 换包读档 / 已答却又挂在队列里），否则这一局会永久卡死；
那种情况引擎会发一条警告把它说出来。
快照 **1931 行**（只变了第一行的版本号）；用例 **460 → 469**；两个宿主都已接线
（它们仍在报告"展示过"，但那是诊断，不是判据）。
**这是 K 条的实现**（决定、代价、跨层/转生/读档的实测证据与故障注入见
[OPEN_WORK](engine/docs/OPEN_WORK.md) K、条目见 [CHANGELOG](CHANGELOG.md) 的 1.6.0；
上一代是同一文档的 J 条与 CHANGELOG 的 1.5.0）。
**当时尚未提交、未打 tag、未发布**——放行前的打包 / tag / 推送是子代理**没做**的事（本会话约定）。
上一版 `1.5.0` 是 minor（结局落定条件 定时 → 被展示过），
1.4.0 是 minor（升级行的货币语义，快照 1927 → 1930），
1.3.0 也是 minor（离线收益第一次走到界面上），1.2.1 是 patch（421 条散文铺满十个包 + `ContentText` 并发修复）。

> **1.4.0 已发布**（2026-10-02）：`build.ps1 -Strict` 441 全绿 0 警告、`api-test.ps1` 44 项全过、
> 产物自检通过（含那条"从包根目录启动"的老坑复核）、tag `v1.4.0` 已推送、
> CI [#11](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36983736167) 两个作业 success。
> 这一轮**没有新坑**——第一次照 RELEASING §1 的表走完、每一步都一次过。执行记录见
> [RELEASING](engine/docs/RELEASING.md) §5。
>
> **1.3.0 已发布**（2026-10-02）：`build.ps1 -Strict` 439 全绿 0 警告、`api-test.ps1` 39 项全过、
> 产物自检通过、tag `v1.3.0` 已推送。完整执行记录（含打包自检逮到的那个首页 404 缺陷）
> 见 [RELEASING](engine/docs/RELEASING.md) §4。
>
> **为什么这次必须当场升版本**：按 VERSIONING §4，动了公开 API 就要在同一次改动里升位，
> 不能像纯宿主的改动那样攒在 `[未发布]` 里。所以 `[未发布]` 里那条 Web 日志面板
> （宿主侧、本该按 patch）也一并收进了 1.3.0——判据与写法写在 CHANGELOG 那一节的开头。

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
  engine/tests/    493 个用例 + 自研迷你运行器
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
.\tools\build.ps1 -Strict    # 一条命令：引擎 + 内容 + 493 用例 + Web 宿主（0 警告）
.\tools\play.ps1             # 终端 Demo；--package <id> 换包
.\tools\play.ps1 --package lab --simulate 21600 --auto   # 无头跑图 + 数值报告
.\tools\web.ps1 run          # Web 前端（开发期必须 dotnet run 起，见 §7）
.\tools\api-test.ps1         # 端到端：起真宿主、打一遍全部端点（含 SSE），约 20 秒
.\tools\pack.ps1             # 打出 artifacts/neko-clicker-<版本>-win-x64.zip
```

**Web 前端**（`?package=<id>` 换包，缺省用扫描到的第一个）：大数字按每秒产量**逐帧累加**
（不是追 250ms 一跳的目标）、点击、金猫浮层、增益条、**纪元**（唯一的「舍一命」主按钮 +
进度条 + 卡在哪一项）、**表态**（待答卡片 + 立场轴 + 结局）、**图鉴**（未读到显示 `???`
但保留条件与进度）、成就、**日志**（引擎的通知：成就 / 买升级 / 增益 / 金猫 / 舍命 / 存档失败）、
**离线收益弹窗**（读档补发的那一笔：涨了多少、补了多久，被上限截断时说清实际离开了多久；
收下之后不再弹）、**永久升级线**（`#tab=permanent`：转生后保留的那条线单独成面板，
钱包、货币名与"买得起几项"的角标都来自服务端；锁着的行照样列出来带解锁进度）、
建筑与升级（×1/×10/×100/买满）。
面板与 URL hash 同步，`#tab=codex` 可以直接分享。
**两个前端共用同一份存档**（`saves/<包 id>.json`），一份进度两边都能接着玩。

**调试门**：`/?package=<id>&password=<密钥>&epoch=<层号>` 把这一局直接置到第 N 层。
密钥只来自环境变量 `NEKO_DEBUG_KEY`（不设就**不存在**这道门）；跳层会话**不写存档**。
方案见 [WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。

**剧情与建筑文案现在是可读文件**：十个有剧情的包（咖啡馆 / 九命 / 实验室 / 公司 / 末世 / 图书馆 /
神明 / 文明 / 赛博 / 梦境）的**全部 421 条图鉴散文**住在各自目录下的 `text.json` 里，
代码里只剩条件、序号、频道与条数——改一个错字不用重编，丢一条 / 多一条 / id 对不上
都会在启动时当场抛。**同一份 `text.json` 现在还住着建筑的 `name` / `description` / `icon`
（十一个包、104 座）与纪元的六个字段（九个包、49 层，根节 `eras`）**；
示例包「猫咖物语」没有图鉴与纪元，它的 `text.json` 只有 `buildings` 一个分区。
搬迁的保真判据都是"迁移前后各把运行期文字 dump 一次、逐字节比对"：
图鉴 **176,708 字节**、建筑 **13,567 字节**、纪元 **45,621 字节**、建筑图标 **7,895 字节**。
完整过程见 [TEXT_AS_DATA_PLAN](engine/docs/TEXT_AS_DATA_PLAN.md) §8 / §9 / §10；
写新包的形态见 [CONTENT_AUTHORING](engine/docs/CONTENT_AUTHORING.md) §12.0 / §12.0.1 / §12.0.2。

---

## 4. 验证基线（改动前后都该守住的）

| 命令 | 期望 |
|---|---|
| `.\tools\build.ps1 -Strict` | **493 个用例全绿**、0 警告（主 sln 与 Web sln 都编） |
| `.\tools\api-test.ps1` | **52 项端到端检查全通过**——脚本自己会做**检查点覆盖审计**（源码里有几处 `Check`、这次就该执行到几处，少了就点名报红；见脚本头部 ⑤）：实测 **源码 52 处 ｜ 执行到 52 处 ｜ 跳过 0**。真起宿主（`dotnet run`）、真读 SSE 流、**最后再起一次宿主读档**，收尾自己清进程 |
| `.\tools\web.ps1 build -Strict` | 只编 Web 宿主时用（0 警告） |

**1.4.0 时代基线 441 的构成**（此后又长过五轮，**当前 493**；1.5.0 / 1.6.0 两轮与文案外置第三、第四、第五轮的
用例增删见 [OPEN_WORK](engine/docs/OPEN_WORK.md) 的 J、K 条与
[TEXT_AS_DATA_PLAN](engine/docs/TEXT_AS_DATA_PLAN.md) 的 §9~§12——所以下面这份分解**只作历史**）：435（1.2.1 基线：
404 阶段 6 + 7 合并守卫 + 10 Web 协议 + 9 `ContentText` 加载器
+ 1 并发读写 + 4 剧情文本运行期守卫）+ 4（离线报告：出现 / 短离线不弹 / 重开清掉
/ 协议里的出现与 `null` 补丁）+ 2（升级行的货币语义：单包逐项 + 横扫十一个包）。

**四条最容易被误伤的守卫**：

- `WebSnapshotProtocolTests` 守 Web 推送协议：增量累积出的快照必须与直接取的全量在
  **协议口径下逐字节相同**、字段名必须 camelCase、`null` 补丁要应用而不是跳过、
  以及**按字节数**守住"一个 tick 的增量远小于全量"
  （派生字段翻转不能重发整表：`AffordabilityFlip_DoesNotResendTheWholeList`）。
- `ContentTextFileTests` 守文本外部化的装配（覆盖**全部**已外部化的包）：输出目录里的
  `text.json` 与仓库里那份**逐字节相同**、代码里每条散文 / 建筑字段 / 纪元字段都等于文件里的对应字段、
  把文件改坏（删一条 / 多一条）真的会红——而原样读一遍一条都不抛；
  另有一条覆盖度用例盯着"有 text.json 的包"与"守卫表里的包"必须一致，
  以及两张**写死的条数表**（每包几座建筑、每包几层纪元）——它们是唯一能发现
  "代码与文件**同时**少一条"的守卫。
- `ContentTextTests.ConcurrentReaders_DoNotCorruptTheUsedSet` 守 1.2.0 那个并发缺陷
  （同一份文本被多个线程同时读）。它自己第一版是橡皮图章（只两个键、撞不出扩容竞态），
  被故障注入抓出来后改成 200 条 × 16 线程 × 30 轮。
- `SaveTests.OfflineReport_*` + `WebSnapshotProtocolTests.OfflineReport_ArrivesThenLeavesAsANullPatch`
  守离线报告这条一次性播报的链路：读档补发之后它必须在快照里（没播报就不自过期）、
  消失时必须走 `null` 补丁而不是"没提到"、重开一局要一起清掉、短离线不许弹。
- `ViewTests.UpgradeRows_ReportTheRightWalletForEveryPack` 守升级行的货币语义（横扫十一个包）：
  `UsesPrestigeCurrency` 必须与内容包的 `Currency` 一致、货币名与图标必须取自正确的那一对、
  永久线必须全花转生货币。它替换掉的是一条**静默**的老写法（前端 `currency === 1`）——
  序数一错，界面上是"买得起的行灰着"，而控制台里什么都没有。

Web 宿主不在 `NekoClicker.sln` 里（它是独立的单项目 sln），但 `build.ps1` 会**两条都编**，
所以上面第一条命令就是全仓库的验收；`api-test.ps1` 再补上"编得过"证明不了的那一半。

**`api-test.ps1` 打的是哪 52 项**（判据只有一条：**真的通**。下面是按脚本自己的小节分的组，
数字是 2026-10-03 实测的**源码检查点数**——脚本每次跑完会自己复核一遍"源码里有几处检查点、
这次执行到几处"，对不上就红，见脚本头部 ⑤）：
**静态文件三件**（`/`、`/app.js`、`/app.css`，含 needle 检查——"首页 404 而 `/api/*` 全正常"
是这一层最经典的沉默失败）、
**前端面板八件**（首页里有日志面板 / 离线弹窗 / 永久线面板的挂载点，`app.js` 里有这三个渲染函数，
外加浏览器真的发得出 `choicesShown` 与 `answer`）、
**元信息三件**（`/api/ping` 报出的版本 == `Directory.Build.props` 的版本、`/api/ping` 报出运行时、
`/api/packs` 扫到 11 个包）、
**快照六件**（顶层字段齐全 / camelCase / `modeName` 是字符串且在词表里 / `mode` 仍是数字 /
七个档位 token 原样发回去能回读 / 紧凑 JSON）、
**升级行的货币语义三件**（每行都带 `currencyIcon`/`currencyName`/`usesPrestigeCurrency`、
图标与该行所用钱包一致、永久线全花转生货币）、
**命令九件**（40 次点击全部被接受、40 次点击真的涨钱、至少有一座建筑已解锁、超大购买数量被钳到预算内、
**动作之后快照的 `notifications` 里有真消息**、每条通知字段齐全且 `kind ∈ 0..3`、时间戳不超过当前游戏时间、
未知命令 `ok=false`、`choicesShown` 可用且幂等、`answer` 对不存在的表态明确失败）、
**负数三件**（未知包在 `/api/snapshot` 与 `/api/stream` 上都是 404、**没有 `NEKO_DEBUG_KEY`
时带 `epoch` 必须是 403**）、
**SSE 六件**（建流、收到帧、第一帧全量、之后以增量为主（撞上 30 秒一次的全量对账不算故障）、
按字节数均值 < 全量 10%、`seq` 单调）、
**离线十一件**（存档前产量 > 0 → `save` 命令 → **停掉宿主** → 把存档里的 `LastSavedAt`
改老 5 小时 → **带同一份存档重新起宿主** → 存档文件真的落在临时目录、那份存档能改老、
快照里出现 `offline` 且字段齐全、时长与补发量自洽、再取一次全量它仍在、`dismissOffline` 成功、
收下之后消失、重复 `dismissOffline` 幂等）。
三加八加三加六加三加九加三加六加十一，正好 52。
五条刻意为之的行为写在脚本头部注释里：自带临时存档目录（探针会点击和买入，
跑在玩家存档上等于拿进度当夹具）、起子进程前把 `NEKO_DEBUG_KEY` 摘掉（让"门是关着的"成为被测事实而不是巧合）、
收尾按**端口**反查进程（`dotnet run` 会再起一个真宿主子进程，只杀它会留下孤儿）、
最后一段**再起一次宿主**（离线补发只在读档那一刻发生，而"读档"没法在一次会话里伪造）、
收尾做**检查点覆盖审计**（源码里有几处 `Check` 就该执行到几处，少了就点名报红；
跑不了的用第 4 个参数显式跳过并打印理由——"某条检查悄悄没跑"从此不可能再靠计数对不上来发现）。

---

## 5. 有意没做的（都是决策，不是遗漏）

| 项 | 现状 | 为什么 |
|---|---|---|
| **剧情外部化的收尾** | 十个有剧情的包全部搬完（421 条）；`tools/extract-lore-text.ps1` 只支持工厂写法，作为"迁移过程可复核"的存档保留 | 迁移已完成，这个脚本不再有运行期职责；真要再用它抽新包，得先扩展对象初始化器写法 |
| **永久升级线** | **已交付**（2026-10-02：`UpgradeView` 补上货币语义 + 「永久」面板，用例 +2、端到端 +5） | 先做能玩的最小闭环 |
| **二周目界面** | 引擎侧只有 `hardReset`（清空、不带继承）与纪元 / 转生的「舍一命」；"看完结局带继承重开"**语义还没定**，见 §8.4 | 语义没定就开工会走偏 |
| **离线收益弹窗** | **已交付**（2026-10-02：`GameSnapshot.Offline` + Web 播报卡 + `dismissOffline` 命令，端到端加了 13 项检查） | 先做能玩的最小闭环 |
| **通知日志** | **已渲染**（2026-10-01：第六个面板「日志」，端到端加了 4 条检查）。**未读游标仍未做**——那要新增状态 + 公开字段，见 §8.4 | 先做能玩的最小闭环 |
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
  **一个可复制的例外（日志面板 2026-10-01、离线弹窗与永久线面板 2026-10-02 各用过一次）**：把
  `index.html` / `app.css` / `app.js` 内联成一张静态页、把 `EventSource` 换成立刻推一帧
  假快照的桩、`fetch` 换成返回 `{ok:true}` 的桩，就能用无头浏览器截图——
  它验的是**布局与配色**（真 CSS + 真渲染函数 + 假数据），验不了"和真宿主配合得对不对"
  （后者归 `tools/api-test.ps1`）。本机 Edge 与 Chrome 都在，命令与注意事项记在
  [games/README](games/README.md) 的 Web 一节。
  离线弹窗那一版的结论：遮罩压暗、卡片居中、标题/正文/灰色小字/按钮四层都在，
  被上限截断时那句"实际离开了多久"也在；永久线面板那一版还纠正了一个手法问题——
  **fixture 要拿真宿主抓的快照去改**（只改"玩法状态"这类假数据），手写的 fixture 会漏掉
  服务端算好的派生字段（`isUnlocked` / `isMaxed` / `isAvailable`），角标与配色会跟着假。
  **但它仍然只是"我看过"，不是守卫。**
- **CI 只跑在 Windows 上**：`.github/workflows/ci.yml` 是两个 `windows-latest` 作业。没有加
  Linux 作业——`engine/core/` 平台中立这条主张仍然只由"全仓库只有两处平台相关代码"这个
  事实支撑，没有一个远端作业在守着它（想守就得先确认 Demo 与测试项目在 Linux 上也能编）。
- **CI 的远端表现（2026-10-01 补记）**：截至 `3574762` 的**六次运行全绿**——
  `c84c478`（workflow 首跑）、`4bb2690`、`67a1f22`（1.2.1 发布）、`5cf5f9c`（文档回填）、
  `602b5c8`（日志面板）、`3574762`（预览手法回填），每次都是两个作业各自 success
  （构建 + 435 用例 / Web 宿主端到端）。
  原先这条写的是"还没在远端跑过一次"——写下时是事实，但一直没人回填；这次补上，并推翻它。
  runner 镜像里的 .NET 版本、`Get-CimInstance` 的行为、端口占用都没出问题。
  **2026-10-02 补齐（1.3.0 一轮）**：`#7 623bf9b`（上一轮的交接提交）、
  `#8 ed25c21`（1.3.0 发布：[运行 36975133007](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36975133007)，
  **209 秒**，端到端那条现跑的是新增的"重启一次宿主读档"那一段）、
  `#9 e4cc15d`（发布后的文档回填）——三次都是两个作业各自 success。
  **2026-10-02 再补（1.4.0 一轮）**：`#10 b8ef60e`（把 CI 编号写准的那次文档提交）、
  `#11 b418fbd`（1.4.0 发布：[运行 36983736167](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36983736167)，
  **229 秒**，两个作业 success）——两次也都全绿。
  编号来自 `actions/runs` 的 `run_number`，不是"第几次"的记数：**文档提交也会各触发一次运行**，
  所以它会一直涨，不必逐次回填。观测方式见 §7 第 11 条。
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
   **同一条里还有半条（2026-10-02 实测）：工作区里的 `.ps1` 是 CRLF**，而 `edit` 一类的工具
   会把它一起改成 LF（`git` 随后警告 `LF will be replaced by CRLF`）。两样一起补回来：
   先按 `\r\n` 归一化，再用 `UTF8Encoding($true)` 写回。中文文件解析失败时先把 BOM 补上再判断，
   本轮就是这样——修 BOM 之前解析器报了 20 多条错，补完一条都不剩。
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
   而首页 404，且编译期 0 警告 0 错误。
   **发布产物不受此限，但有一条 2026-10-02 才逮到的前提**：内容根默认是**当前工作目录**，
   所以从包根目录敲 `web\neko-clicker-web.exe` 会得到同一个症状（`/api/*` 全通、首页 404）
   ——1.2.0 起的包都有这个缺陷，说明书里写的恰好就是那条命令。
   已在 `Program.cs` 里修掉：程序集旁边有 `wwwroot`（= 发布产物）就拿它当内容根，
   没有（= 开发期）保持默认。**这一层没有用例守着**，只能靠打包后自检（§8.6）。
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

> **停在哪里**：**1.4.0 已发布**——tag `v1.4.0`（打在 `b418fbd`）与 `main` 都在远端，
> CI [#11](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36983736167) 两个作业 success，
> 工作树干净。本机验收：`build.ps1 -Strict` **441 全绿 0 警告**、`api-test.ps1` **44 项全过**、
> 打包产物自检通过。
> **"已经就位、只差执行"的事没有了**：往下的每一项都要先定一件事（见 §8.4）——
> 二周目的玩法语义、未读游标要不要做，都得先有一句答复才该开工。
> 想知道"现在到底什么状况"，跑 §4 那三条命令——这份文档刻意不写 HEAD，别再让它跟着提交跑。

### 8.1 ✅ 已交付：1.4.0 发布（2026-10-02）

两个提交：`4365e98`（升级行货币语义 + 「永久」面板）、`b418fbd`（文档回填）。
第 ⑨⑩⑪ 步的实际结果（产物自检的五项、推送一次成功、CI #11 用 229 秒两个作业 success）
见 [RELEASING](engine/docs/RELEASING.md) §5.1。**这一轮没有新坑**——第一次照 §1 的表走完、
每一步都一次过；前两轮攒下的三样东西（打包后自检、"两种布局各跑一遍"、SSH 显式 URL）这次都吃上了。

### 8.2 ✅ 本轮交付：永久升级线（2026-10-02）

| 位置 | 交付了什么 |
|---|---|
| `engine/core/Views/Views.cs`、`GameViewFactory.cs` | `UpgradeView` 新增 `UsesPrestigeCurrency` / `CurrencyName` / `CurrencyIcon`（公开 API 只增） |
| `games/hosts/Web/wwwroot/` | 第七个面板「永久」（`#tab=permanent`）：钱包行、"已买 N / M 项"、买得起几项的角标、舍一命可得多少的提示 |
| `engine/tests/`（+2 用例 → 441） | 单包逐项验 `Snapshot_UpgradeRowsCarryCurrencySemantics` + 横扫十一个包的 `UpgradeRows_ReportTheRightWalletForEveryPack` |
| `tools/api-test.ps1`（39 → **44 项**） | 面板挂载点 / 渲染函数 + 三条"升级行的货币语义" |

**三条设计决策**（要改它之前先读这三条）：

1. **面板按 `isPermanent` 分组，钱包按 `usesPrestigeCurrency` 选。** 两个概念分开说：
   内容构建期只强制了"Permanent ⇒ 转生货币"（`GameContentBuilder` 会报错），
   反方向不强制，所以不能拿其中一个当另一个用。
2. **这一面板刻意不服从 `hiddenUntilUnlocked`**（与「升级」面板相反）。理由：这条线要在玩家
   第一次舍命之前就可见，否则他不知道该攒什么——而"舍一命换转生货币、再拿它买永久升级"
   正是主循环。锁着的行照样列出来，带解锁条件与进度。
3. **顺手删掉了 `slice(0, 40)`**（普通升级列表的静默截断）：咖啡馆包 48 条升级，
   末尾几条此前会无声地少掉。这是同一类失败（数据都在、界面上什么都不说）。

**一个横扫出来的事实**：**11 / 11 个包都有永久线**（守卫的输出里会打印这一行），
所以那个标签在任何一个包里都不会是空的——但代码仍然按"没有就隐藏"写，免得将来加包时假设失效。

### 8.3 更早的交付（一行一条）

- **1.3.0 发布**（2026-10-02）：tag `v1.3.0` 打在 `ed25c21`，CI 两个作业 success。
  执行记录与清单外发现（打包自检逮到的首页 404 缺陷）见 [RELEASING](engine/docs/RELEASING.md) §4。
- **离线收益弹窗**（2026-10-02，随 1.3.0）：`GameSnapshot.Offline` + 压屏播报卡 + `dismissOffline`。
  三条语义决策（去重状态放引擎、收益为 0 不播报、"弹一次"= 播报过）写在 CHANGELOG 的 1.3.0 条目里。

### 8.4 ← 下一轮从这里开始：都需要先定一件事，别照字面开工

- **二周目：先跟用户确认语义再动手。** 引擎侧现在只有 `hardReset`（清空、不带继承）与
  纪元 / 转生的"舍一命"。"看完结局带继承重开"还是别的，**现在没有答案**。
- **通知未读游标 / 角标**：要新增状态 + 公开字段（= minor）。等有人真的需要再谈。

验收：`tools\api-test.ps1` 能给新端点 / 新字段兜住 404 与 camelCase；界面好不好看没有守卫（§6）。

### 8.5 补 `PackageId` / `IsPackable`

纯元数据、不动公开 API——`engine/core/NekoClicker.Core.csproj` 现在只有 `Description`。
两条注意：① 版本号来自 `Directory.Build.props`，打包会自动跟上，**不要再写一份**；
② 本机零第三方依赖，`dotnet pack` 应该能离线跑，但"没有网络也能 pack"这条**最好实测一次**
再写进文档——这个仓库的既有教训是：没实测过的环境结论迟早会变成错的。

### 8.6 可选（都不是必须，但都是"已知没人守"的地方）

- **把打包自检收进 `tools/pack.ps1`**（1.3.0 的首页 404 就是这么逮到的，但那次是手工的）：
  解包 → 在包根目录起发布产物 → 断言 `/`、`/app.js`、`/api/ping` 与快照里的新字段。
  它守的是**端到端探针看不见的那一半**（探针跑的是 `dotnet run` 的开发期路径）。
  要注意本机缺 ASP.NET 8 运行时，脚本里得自己设 `DOTNET_ROLL_FORWARD=Major`（照抄 `api-test.ps1`）。
- **给 CI 加一个 Linux 作业**：能替"`engine/core` 平台中立"这条主张当守卫，
  前提是先确认 `games/hosts/Demo.Cli` 与测试项目在 Linux 上编得过（本机验不了）。
- **观感**：Web 的"好不好看"仍然只能靠人看（§6 记了那条可复制的截图手法）。

### 8.7 动手前的环境清单（这台机器）

- 构建与跑宿主：§7 第 3 / 7 / 8 条（`-m:1`；SDK 10 编 net8.0；缺 ASP.NET 8 运行时，
  `tools/api-test.ps1` 会自动设 `DOTNET_ROLL_FORWARD=Major` 并打印出来）。
- 写 `tools/*.ps1`：UTF-8 **with BOM**（§7 第 1 条）——`edit` 一类的工具会吃掉 BOM，改完要补。
  **本轮实测还有第二样：它会把 CRLF 改成 LF**（`git` 会警告 "LF will be replaced by CRLF"）。
  两样都要补回来，一次做完（见本文件 §7 第 1 条与 `RELEASING` §5）。
- 推远端：SSH 一次性重写（§7 第 10 条），fetch / push 都要带重试。
- 任何提交前：`.\tools\build.ps1 -Strict` + `.\tools\api-test.ps1`——CI 跑的就是这两条。
