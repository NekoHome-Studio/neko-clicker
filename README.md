# NekoClicker — 增量游戏框架（C# / .NET 8）

参考 **Cookie Clicker** 的机制设计的一套**增量（放置 / 点击）游戏框架**，纯 C# 实现，
**零第三方依赖**，附带十一个内容包（示例包「猫咖物语」+ #1《猫娘咖啡馆》+ #2《九命轮回》
+ #3《猫娘实验室》+ #10《猫娘公司》+ #6《猫娘末世》+ #9《猫娘图书馆》
+ #7《猫娘神明》+ #4《猫娘文明》+ #5《赛博猫娘》+ #8《猫娘梦境》）和一个可玩的终端 Demo。

**当前版本 `1.10.1`**（`NekoClicker.Core` 的公开 API 版本）。从 `1.0.0` 起
**公开 API 只增不改**，而且有测试守着——见 [版本与兼容性承诺](#版本与兼容性承诺)。
1.10.1 是 **patch：公开 API 一行没动**——动的全在内容包、宿主与前端：存档的导出/导入第一次
有了**界面入口**（Web 的「存档」窗口：只读文本框 + 复制 + 下载 `.json`、导入 + 引擎原话的结果；
终端宿主的 `E` / `I` 两个键各开一次路径提示），十一个包各补一条「点击 × 建筑」的桥
（每座建筑 +0.5%、最多算 120 座，持有建筑越多点一下越值钱），外加两条宿主修复
（关停时真的会存档、坏内容不再静默地成为当前存档）与一条工具链守卫。详见
[CHANGELOG](CHANGELOG.md) 的 1.10.1。
上一版 1.10.0 是纯新增：**存档的导出与导入**——一份能被人复制粘贴的文本（外层是信封：格式标签、
包 id、存档格式版本、校验和；内层是存档本体），以及"导入一份坏东西绝不弄坏能用的存档"
那条不变量（新增 `SaveTransfer`、`SaveTransferKind`、`SaveTransferResult`，以及
`SaveManager` 上的 `PackId` / `Export()` / `Import()`）。**存档格式一个字都没改**
（`SaveSerializer.CurrentVersion` 仍是 `1`，不需要任何迁移）：信封是存档**外面**的一层。
再上一版 1.9.0 是**纪元内的阶段**——一层之内分段推进，跨过一段什么都不清
（`EraStage`、`EraDefinition.Stages`、`EraStageGate`、`EraSystem.Stage` /
`EraSystem.CheckStage`，以及 `EraView` 上的六个只读字段）。
1.8.0 是**建筑升级系统**——把 `UpgradeDefinition.Category` 上那条
`"building:<id>"` 约定（"这条升级属于哪座建筑"）变成**被校验、被索引、也被界面用上**的
（新增 `UpgradeCategories`、`GameContent.UpgradesForBuilding`、`BuildingView.UpgradeIds`；
1.7.0 是 `GameSnapshot.ModeName` + `PurchaseModes.WireName()`，修掉"前端拿枚举序数
当档位名用"那条线上故障）。这些版本**没有一处不兼容改动**：老字段原样留着，
没有破坏任何消费者——1.10.1 更是连一行公开 API 都没动。
语义层面的破坏性变更至今有三处，而且是**同一件事的三次决定**：1.1.0 让结局条件成立后
先等一段作答宽限期（默认 30 **模拟**秒）；1.5.0 把那段时间换成条件——玩家
**被展示过**那批待答表态之后结局才允许落定；**1.6.0 又把条件收紧成"玩家把它们答完"**
（`GameEngine.MarkPendingChoicesShown()` 保留但**不再参与判定**，只剩诊断用途）。
**代价是刻意接受的，而且 1.6.0 起更宽：从不作答的玩家永远拿不到结局。**
1.2.0 只新增了 `ContentText`（剧情散文的外部化载体），没有不兼容改动；
1.2.1 是 patch：421 条散文铺满十个包、修掉 `ContentText` 的并发缺陷，公开 API 一行没动；
1.3.0 是 minor：新增 `GameSnapshot.Offline` 一族（离线收益第一次能走到界面上）；1.4.0 也是 minor：`UpgradeView` 补上货币语义（前端不再解释枚举序数），Web 端多了「永久」面板。
以上每一处的口径与影响面都记在 [CHANGELOG](CHANGELOG.md)。

框架的核心目标是**把"引擎"和"内容"彻底分开**：引擎负责时间推进、数值管线、存档与事件；
内容只描述"这个世界有什么"。换掉内容包就能做出完全不同的游戏，引擎代码一行都不用改。
内容包 #1 就是这句话的实物证据：10 建筑 / 48 升级 / 45 成就 / 5 增益 / 8 随机事件 /
第二资源模块，核心引擎改动 **0 行**。

```
┌──────────────────────────────┐
│  你的前端（终端 / Web / Unity）│  只读 GameSnapshot，只发命令
└───────────────┬──────────────┘
                │  Snapshot() / Click() / BuyBuilding() / ...
┌───────────────▼──────────────┐
│      NekoClicker.Core        │  引擎：时间、数值、存档、事件（无 UI、无 IO 硬编码）
└───────────────┬──────────────┘
                │  GameContent（不可变的内容定义；一个包 = 一个 csproj）
┌───────────────▼──────────────┐
│  NekoClicker.Content.Neko    │  示例包：猫咖物语的建筑 / 升级 / 成就 / 增益 / 金猫结果表
│  NekoClicker.Content.Cafe    │  内容包 #1：猫娘咖啡馆 + 幸福感模块（IGameModule）
│  NekoClicker.Content.NineLives│ 内容包 #2：九命轮回（分层转生 + 叙事 + 表态）
│  NekoClicker.Content.Lab     │  内容包 #3：猫娘实验室（Era + Lore + Choice 三项全用）
│  NekoClicker.Content.Company │  内容包 #10：猫娘公司（劳资立场轴 + 士气模块 + 三轮重组）
│  NekoClicker.Content.Apocalypse│ 内容包 #6：猫娘末世（跨转生继承 + 记忆残片 + 五次重启）
│  NekoClicker.Content.Library │  内容包 #9：猫娘图书馆（虚无化 + 被阅读度 + 五本书）
│  NekoClicker.Content.God     │  内容包 #7：猫娘神明（五套神话体系 + 信仰 + 在线人数峰值）
│  NekoClicker.Content.Civ     │  内容包 #4：猫娘文明（五个时代 + 文化 + 星际结局）
│  NekoClicker.Content.Cyber   │  内容包 #5：赛博猫娘（迁服务器 + 算力）
│  NekoClicker.Content.Dream   │  内容包 #8：猫娘梦境（五层梦 + 梦境能量）
└──────────────────────────────┘
```

---

## 快速开始

> 下面的命令使用 `.\tools\` 下的脚本调用（Windows PowerShell 与 PowerShell 7 都适用）。
> 若执行策略拦截脚本，先在当前会话运行 `Set-ExecutionPolicy -Scope Process Bypass`。
> 在普通开发机上也可以直接 `dotnet build` / `dotnet run`。
>
> 传统 conhost（PowerShell 5.1 / cmd 直接打开的窗口）下会自动改用主缓冲区渲染：
> conhost 在「备用屏缓冲区 + 缩放窗口」组合下有一个已知崩溃（microsoft/terminal#13037），
> 那是宿主进程崩溃，程序里捕获不到。Windows Terminal / VS Code 终端等不受影响；
> 也可以用 `--no-altscreen` / `--altscreen` 手动覆盖。
>
> conhost 在窗口宽度变化时还会做缓冲区换行重排（microsoft/terminal#9461），所以主缓冲区
> 模式下还会：清空滚动缓冲、每行每屏各留 1 格安全边距、缩放后短暂静默再重绘。
> 代价是画面右侧与底部各有一格空白——这是换「拖横向不崩」付出的代价。

```powershell
# 构建 + 跑测试（571 个用例，零依赖迷你运行器）
.\tools\build.ps1

# 逐条计时：打印最慢的一批与分档占比（想知道"什么变慢了"就用它）
.\tools\build.ps1 --timing
.\tools\build.ps1 --timing --serial      # 看真实代价请关并行（并行下单条墙钟含 CPU 竞争）
.\tools\build.ps1 --timing Prestige      # 也可以只计时某一批

# 用例之间互不依赖，所以默认并行跑（实测 261s → 150s）
.\tools\build.ps1 --serial               # 串行：某条用例"只在并行下红"时用它定位
.\tools\build.ps1 --jobs 4               # 指定并发度

# 只构建全部项目
.\tools\dnet.ps1 build NekoClicker.sln

# 开始玩（终端全屏界面；默认「猫咖物语」）
.\tools\play.ps1

# 换内容包：玩内容包 #1《猫娘咖啡馆》（第二资源「幸福感」）
.\tools\play.ps1 --package cafe

# 换内容包：玩内容包 #2《九命轮回》（九层纪元 + 逐级推进的舍命按钮）
.\tools\play.ps1 --package ninelines

# 换内容包：玩内容包 #3《猫娘实验室》（七批纪元 + 伦理值 + 道德立场轴）
.\tools\play.ps1 --package lab

# 换内容包：玩内容包 #10《猫娘公司》（三轮重组 + 士气 + 劳资立场轴）
.\tools\play.ps1 --package company

# 换内容包：玩内容包 #6《猫娘末世》（五次重启 + 记忆残片 + 唯一会「继承」的包）
.\tools\play.ps1 --package apocalypse

# 换内容包：玩内容包 #9《猫娘图书馆》（五本书 + 唯一会「掉」的第二资源「被阅读度」）
.\tools\play.ps1 --package library

# 换内容包：玩内容包 #7《猫娘神明》（五套神话体系 + 信仰 + 三个结局）
.\tools\play.ps1 --package god

# 换内容包：玩内容包 #4《猫娘文明》（五个时代 + 文化 + 三个结局）
.\tools\play.ps1 --package civ

# 换内容包：玩内容包 #5《赛博猫娘》（五层数字层 + 算力 + 两个结局）
.\tools\play.ps1 --package cyber

# 换内容包：玩内容包 #8《猫娘梦境》（五层梦 + 梦境能量 + 两个结局）
.\tools\play.ps1 --package dream

# 无头模拟：让机器人替你玩 6 小时并打印数值报告
.\tools\play.ps1 --simulate 21600 --auto
.\tools\play.ps1 --package cafe --simulate 21600 --auto

# 分层转生的包要跑得久一点才看得出九命的推进（这里 48 小时）
.\tools\play.ps1 --package ninelines --simulate 172800 --auto

# 实验室要跑 4 小时左右才看得出七批的推进（这时约在第 5 批）
.\tools\play.ps1 --package lab --simulate 14400 --auto

# 公司跑 6 小时左右能走完三轮重组（机器人不表态 → 兜底结局）
.\tools\play.ps1 --package company --simulate 21600 --auto

# 末世跑 12 小时左右能走完五次重启（机器人一路"够条件就重启" → 自然落到某个结局）
.\tools\play.ps1 --package apocalypse --simulate 43200 --auto

# 图书馆跑 12 小时左右能写完五本书（机器人一路「够条件就开新书」 → 自然落到某个结局）
.\tools\play.ps1 --package library --simulate 43200 --auto


# 无头模拟默认读写 saves/<包>.json。旧存档里的纪元 / 转生状态会一起读进来，
# 量"这个包现在跑得怎么样"时请指向一个新文件，否则读到的可能是上一轮的终局状态。
.\tools\play.ps1 --package ninelines --simulate 43200 --auto --save .tmp/probe.json

# 渲染一帧界面（用于验证布局 / 截图，可重定向到文件）
.\tools\play.ps1 --simulate 1800 --auto --frame 118x32 --no-color

# 渲染时直接停在某个面板（buildings | upgrades | achievements | codex | choices）
.\tools\play.ps1 --package cafe --simulate 5400 --auto --frame 118x32 --panel codex --no-color

# 九命轮回的「表态」面板：待答选择 + 立场轴 + 结局（第 5 命就攒了 3 项待答）
.\tools\play.ps1 --package ninelines --simulate 21600 --auto --frame 118x24 --panel choices --no-color

# 实验室的「表态」面板：四条道德立场 + 结局（第 5 批前后攒了 3 项待答）
.\tools\play.ps1 --package lab --simulate 14400 --auto --frame 118x24 --panel choices --no-color

# 公司的「表态」面板：三条劳资立场 + 结局（走完三轮之前会攒满 6 项待答）
.\tools\play.ps1 --package company --simulate 14400 --auto --frame 118x24 --panel choices --no-color

# 末世与图书馆都没有表态面板（不需要选择轴的包）：看它们的图鉴与纪元总览
.\tools\play.ps1 --package apocalypse --simulate 21600 --auto --frame 118x32 --panel codex --no-color
```

### 目录结构

```
engine/core/            框架核心：内容定义、模拟引擎、存档、事件、UI 视图
engine/content/Neko/    示例内容包「猫咖物语」（纯数据，无逻辑；框架回归基线）
engine/content/Cafe/    内容包 #1《猫娘咖啡馆》（含幸福感模块，核心零改动）
engine/content/NineLives/ 内容包 #2《九命轮回》（九层纪元，第一个用分层转生的包）
engine/content/Lab/     内容包 #3《猫娘实验室》（七批纪元，第一个同时用 Era+Lore+Choice 的包）
engine/content/Company/ 内容包 #10《猫娘公司》（三轮重组，第二个用 Choice + 自己的劳资立场轴）
engine/content/Apocalypse/ 内容包 #6《猫娘末世》（五次重启，第一个用跨转生继承的包）
engine/content/Library/ 内容包 #9《猫娘图书馆》（五本书，「虚无化」唯一的用武之地）
engine/content/God/     内容包 #7《猫娘神明》（五套神话体系，换皮批产的形态样板）
engine/content/Civ/     内容包 #4《猫娘文明》（五个时代，从猫窝走到星港）
engine/content/Cyber/   内容包 #5《赛博猫娘》（五层数字层，迁服务器）
engine/content/Dream/   内容包 #8《猫娘梦境》（五层梦，越睡越浓的梦境能量）
games/hosts/Demo.Cli/        终端 UI 适配层（ANSI 全屏 + 键盘 + 无头模式 + --package）
games/hosts/Web/         Web 前端宿主（本地 HTTP + wwwroot，浏览器里可玩）
engine/tests/    571 个测试 + 自研迷你测试运行器（含架构不变量与全程可达测试）
engine/docs/ARCHITECTURE.md             架构与设计决策
engine/docs/CONTENT_AUTHORING.md        如何写内容（数值节奏、校验规则、常见坑）
engine/docs/VERSIONING.md               版本与兼容性承诺：什么改动升哪一位、公开 API 快照怎么用
games/docs/ROADMAP.md                  实施规划与决策记录：11 项已定决策、4 条架构不变量、5 个阶段
games/docs/NINE_LIVES_DESIGN.md        《九命猫娘》设计映射：1 个共享核心 + 10 个内容包（10 个已落地）
games/docs/PACK_01_CAT_CAFE.md         #1《猫娘咖啡馆》完整内容规格（已落代码，也是其余九个包的模板）
engine/docs/ENDING_GRACE_PLAN.md        终局判定的作答宽限：方案、实测与取舍
engine/docs/MERGE_WEB_FRONTEND_PLAN.md   feature/web-frontend-ui 的合并方案
STATUS.md                        项目现状（跨层，所以放在根目录）
games/docs/STAGE_5_RESKINS.md           阶段 5 换皮批产手册：#4/#5/#7/#8 四个包的交接件（规则清单 + 验收命令 + 已知坑）
CHANGELOG.md                     变更日志（按版本记录，含兼容性影响）
tools/build.ps1                  一键构建 + 测试
tools/play.ps1                   构建并运行终端 Demo（参数转发给程序）
tools/web.ps1                    构建 / 运行 Web 宿主（自带单项目 sln，见「环境说明」）
tools/api-test.ps1               端到端回归：起真宿主、打一遍全部端点（含 SSE），收尾时自己清进程
tools/dnet.ps1                   在受限环境里运行 dotnet CLI 的包装脚本
tools/public-api.ps1             重新生成公开 API 快照（有意改动 API 后的最后一步）
tools/seed-packages.ps1          把全局 NuGet 缓存里的 net8.0 targeting pack 播种进仓库（离线构建）
.github/workflows/ci.yml         CI：windows-latest 上跑 tools/build.ps1 -Strict 与 tools/api-test.ps1
```

顶层只有两个目录表达"这是什么"，而不是靠文件名猜：

| 目录 | 是什么 | 提取性 |
|---|---|---|
| `engine/` | **引擎**：`core/` + `content/`（十一个内容包）+ `tests/` + `docs/` | **自包含**。把 `engine/` 整个搬走，配合仓库根的 `Directory.Build.props` 就是一个能独立构建的引擎仓库 |
| `games/` | **旗舰示例作品**：`hosts/`（两个前端宿主）+ `docs/`（世界观与路线图） | 依赖 `engine/`，反向不依赖 |

**为什么内容包放在 `engine/` 而不在 `games/`**：它们不是"某一部作品的资产"，而是**引擎的集成测试探针**。
`ArchitectureTests` 要拿它们证明"核心不认识内容"，`EraTests` / `LoreTests` / `PrestigeTests` 的通用守卫
要横扫每一个包才抓得出沉默失败（阶段 4B 的两条真缺陷就是这么发现的）。
所以 `engine/tests` 与内容包同属引擎侧，一起被提取——这也是 `engine/` 能自包含的原因。

> **`games/hosts/Web/` 的状态**：**可玩**——点击、买建筑与升级、推进纪元、表态、看图鉴、
> 日志（引擎的通知列表，`#tab=log`）、**离线收益弹窗**（读档补发的那一笔，收下之后不再弹）、**永久升级线**（`#tab=permanent`：转生后保留的那条线，锁着的行也列出来），
> 与终端 Demo 共用同一份存档（`saves/<包 id>.json`）；零前端依赖（手写 ES 模块 + 一份 CSS，
> 无 npm、无打包步骤）。调试要用 `NEKO_DEBUG_KEY` 开门，见
> [WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。它刻意不挂进 `NekoClicker.sln`
> ——见 [环境说明](#环境说明为什么有-toolsdnetps1)。


---

## 框架能力

下列机制全部来自 Cookie Clicker，并且都被抽象成**内容可配置**的形式：

| 机制 | 实现 | 内容里怎么配 |
|---|---|---|
| 建筑（自动生产） | 价格按 `PriceGrowth` 指数增长，产量与数量线性 | `BuildingDefinition` |
| 批量购买 | 等比数列**闭式解**，含"买满"的解析反解 | 无需配置，引擎内置 |
| 出售 | 按最近买进的单价 × 返还比例 | `SellRefundRate` / `GameBalance` |
| 升级 | 一次性 / 有限次（`MaxPurchases` + `PriceGrowth`）/ 转生后保留 | `UpgradeDefinition` |
| 成就 | 条件树达成即永久激活修饰符 | `AchievementDefinition` |
| 「牛奶」式成长 | 效果随成就数、建筑数、转生等级、标签数动态变化 | `Scaling` |
| 限时增益 | 刷新 / 叠加时长 / 叠层三种叠加规则 | `BuffDefinition` + `BuffStackMode` |
| 金猫（随机事件） | 加权结果表：幸运、狂热、点击狂热、被抢、连锁…… | `GoldenCookieOutcome` |
| 转生 / 天堂升级 | `等级 = ⌊(历史累计 / 1e12)^(1/3)⌋`，永久升级跨转生保留 | `UpgradeCurrency.PrestigeChips` + `UpgradePersistence.Permanent` |
| 分层转生（`Era`） | 纪元串成一条链，**「舍一命」只有一个按钮**，上一层未完成时按钮为灰并显示最落后的子条件 | `EraDefinition` + `EraGate` |
| 图鉴 / 叙事释放 | 条目挂在**叙事线**上，达成条件即解锁；未解锁显示 `???` + 条件进度 | `LoreEntry` + `StorylineDefinition` |
| 选择分支 / 立场轴 | 条件达成即触发对话，作答后才生效；各立场累加权重，**主导立场**的修饰符计入产量 | `ChoiceDefinition` + `StanceDefinition` |
| 终局判定 | 主线走完后按条件树挑出**一个**结局（Priority 定序、互斥），构建期强制留兜底结局 | `EndingDefinition` |
| 离线收益 | 按**不含增益**的产量补发，有上限 | `GameBalance.OfflineCapSeconds` |
| 存档 | JSON + base64 分享码 + **版本迁移** | `ISaveMigration` |
| 通知 / 事件 | 类型安全的 `GameEventBus`，十几类领域事件 | 无需配置 |
| 模块扩展 | 引擎不认识的系统（花园、股票、小游戏） | `IGameModule` |

### 数值管线（框架的核心）

所有加成最终折叠成一条可预测的公式，而不是散落在各处的 `if`：

```
单个建筑产量 = ((基础产量 + 加法项) × (1 + Σ加法百分比) × Π乘法) ^ Π乘方
建筑总产量   = 单个建筑产量 × 持有数量
总 CPS       = Σ 建筑总产量 × 全局倍率，再统一施加全局乘方
点击收益     = (固定基础 + 总CPS × 比例 + 加法项) × 点击倍率
```

这样设计的直接好处是：**新增一个升级不需要改引擎**，只要声明它对哪个目标做什么运算。
加法百分比与乘法分开累计，是为了让"两个 +50%"得到 +100%（而不是 ×2.25），
而"两个翻倍"得到 ×4 —— 这是玩家能直觉预期的行为。

---

## 最小示例

```csharp
using NekoClicker.Core;
using NekoClicker.Core.Content;

// 1) 用代码描述你的游戏（纯数据 + 校验）
GameContent content = new GameContentBuilder("我的放置游戏")
    .WithCurrency("金币", "🪙", "挖矿")
    .Add(new BuildingDefinition { Id = "pick", Name = "镐子", BasePrice = 15, BaseCps = 0.1 })
    .Add(new UpgradeDefinition
    {
        Id = "steel_pick",
        Name = "钢镐",
        Price = 100,
        Unlock = UnlockCondition.BuildingsAtLeast("pick", 1),
        Modifiers = [Modifier.BuildingMultiplier("pick", 2)],
    })
    .Add(new AchievementDefinition
    {
        Id = "first_100",
        Name = "第一桶金",
        Unlock = UnlockCondition.EarnedAllTimeAtLeast(100),
    })
    .Build();

// 2) 创建引擎
var engine = new GameEngine(content);

// 3) 命令行/服务器/测试里都只需要这几行
engine.Click();
engine.BuyBuilding("pick", 10);
engine.BuyUpgrade("steel_pick");
engine.Simulate(3600);                      // 跳过 1 小时
Console.WriteLine(engine.CookiesPerSecond);
Console.WriteLine(engine.Save());           // JSON 存档
```

接前端时只需要 `engine.Snapshot(mode)` 拿到只读视图（建筑价格、能否买得起、进度百分比、
下一里程碑提示、格式化后的文本都已算好），再把点击/购买命令发回引擎即可。

---

## 设计取舍

**为什么用 `double` 而不是大数库？**
与 Cookie Clicker 一致。增量游戏的数值跨度是几十个数量级，`double` 到 1e308 完全够用，
且算术开销与工程复杂度最低。溢出风险由 `Num.SafeAdd/SafeMul` 统一拦成饱和值，
避免"价格变成 `Infinity` 后永远买不起"这类经典 bug。真要换成 `BigInteger`/对数表示，
改动面被限制在 `Numbers/` 与 `Pricing` 两个文件。

**为什么零第三方依赖？**
核心只用 BCL（含 `System.Text.Json`）。这让框架可以被任意宿主引用而不产生依赖冲突——
包括 Unity、Mono、以及像本仓库这样没有 NuGet 网络访问的构建环境。
测试也因此自带了一个 200 行的迷你运行器，而不是引入 xunit。

**为什么时间推进用固定步长？**
真实帧率波动不影响产量计算结果；配合累加器与补算上限，既不会"卡帧白拿产量"，
也不会在长时间挂起后一次算爆。需要跳跃式模拟（测试、离线校验）时用 `Simulate()`。

**为什么存档与状态分成两套类型？**
`GameState` 可以自由重构，而 `SaveData` 一旦发布就必须向后兼容。
版本迁移在反序列化<b>之前</b>对 JSON 树执行，因此迁移代码不需要认识旧的 C# 类型——
这正是"删掉一个字段后老存档还能读"的关键。

---

## 版本与兼容性承诺

**当前版本 `1.10.1`。从 `1.0.0` 起，`NekoClicker.Core` 的公开 API 只增不改。**

| 改动 | 升哪一位 |
|---|---|
| 公开 API 只增不改（新增方法 / 类型 / 带默认值的参数） | minor |
| 公开 API 有不兼容改动（删除、改签名、收紧可空标注、改语义） | major |
| 公开 API 一行没动（内容数值、文案、修 bug） | patch |

> **已知例外**：`1.1.0`、`1.5.0`、`1.6.0` 都改了**同一处**语义（结局的落定时机）却按 minor 发布。
> 三次都是有意为之，理由与影响面写在 [CHANGELOG](CHANGELOG.md) 与
> [VERSIONING §2](engine/docs/VERSIONING.md) 里——例外要被记录，否则下次就分不清
> "决定"和"疏忽"，这张表也就退化成橡皮图章了。**连着三次也说明：下次再动这里该考虑 major。**

版本号的单一事实来源是 `Directory.Build.props` 的 `<Version>`，宿主可以在运行时读到它：

```csharp
using NekoClicker.Core;

Console.WriteLine(ApiVersion.Current);         // "1.10.1"
Console.WriteLine(ApiVersion.AssemblyVersion);  // 1.10.1.0
```

### 这条承诺是怎么被守住的

不是靠自觉，是靠一份**快照**加四条守卫：

- `engine/core/PublicApi.txt` —— 2026 行的公开表面逐项清单，
  **嵌进 `NekoClicker.Core.dll`**，随 dll 一起走。任何拿到这个 dll 的宿主都能自己断言
  "这份二进制的公开 API 与我预期的一致"，不需要把本仓库的测试代码也带走。
- `PublicApiTests` —— 快照必须逐项一致；快照必须真的覆盖每个公开成员（防止守卫自己瞎掉）；
  快照记录的版本必须等于当前版本（**这是"改 API 必须同时升版本"的执法点**）；
  外加一条**故障注入**用例，用五类真实改动证明守卫真的会红。
- `VersionTests` —— 版本号与程序集元数据、API 快照、CHANGELOG 三方对齐。

守卫红了的时候，它想问的是「你知道自己在破坏兼容性吗」，而不是「要我帮你把红变绿吗」——
所以 `tools/public-api.ps1` 只是流程的**最后一步**，不是第一步。

完整规矩、发布检查清单，以及这套机制**保证不了**什么（语义变化、存档兼容、数值一致性），
见 **[engine/docs/VERSIONING.md](engine/docs/VERSIONING.md)**；逐版本记录见 [CHANGELOG.md](CHANGELOG.md)。

---

## 环境说明（为什么有 `tools/dnet.ps1`）

本仓库的构建脚本不是多余的包装，它解决三个真实约束：

1. **`dotnet` CLI 首次运行会往 `%USERPROFILE%\.dotnet` 写 sentinel 文件**，并把 NuGet 包缓存
   放进 `%USERPROFILE%\.nuget\packages`。在只写工作区的沙箱里这两个路径不可写，
   CLI 会直接抛 `UnauthorizedAccessException`。脚本把两者重定向到仓库内的隐藏目录。
2. **MSBuild 的多进程节点复用依赖命名管道**，受限沙箱不允许命名管道：一旦项目之间有
   `ProjectReference`，构建会**静默失败**（输出 `Build FAILED` 却显示 `0 Error`）。
   脚本给会调用 MSBuild 的动词强制加 `-m:1`（单节点、全进程内执行）。
3. **目标框架是 net8.0，但开发机可能只装了更新的 SDK**（例如 .NET 10）。这类 SDK 不自带
   net8.0 的 targeting pack，restore 会去 nuget.org 下载；叠加第 1 条的缓存重定向，
   没有网络时构建会以 `NU1301` 失败。`tools/seed-packages.ps1` 从**当前 SDK 自己的声明**
   （`Microsoft.NETCoreSdk.BundledVersions.props` 里 net8.0 的 `KnownFrameworkReference`）
   解析出真正需要的包与精确版本，然后分三种情况处理：SDK 的 `packs` 目录已自带该版本 →
   **静默什么都不做**；否则从全局 NuGet 缓存播种它；两边都没有才报警。`dnet.ps1`
   在执行 build/restore 前自动调用它。

> 判定依据可以用 `.\tools\seed-packages.ps1 -Explain` 打印出来。
> 若确实缺包，先用一次联网的 `dotnet restore` 把它们拉下来再运行该脚本；
> 也可以用 `-Version` 强制指定版本（跳过 SDK 声明解析）。

在其他环境下（普通开发机、CI）可以直接用 `dotnet build` / `dotnet run`，不需要这个脚本。

### 为什么 Web 宿主不在 `NekoClicker.sln` 里

`games/hosts/Web/` 自带一个单项目解决方案，用 `.\tools\web.ps1` 构建与运行。理由不是框架差异
——它现在跟全仓库一样是 net8.0（合并期间曾单独覆盖成 net10.0，已撤销）——而是**把
"改前端"与"动引擎基线"在结构上分开**：它是一次性的示例作品宿主，构建前提与发布产物
都和引擎不同。分开之后，"改前端不会动到引擎基线"这句话不是靠自觉，而是靠 sln 边界。

代价只有一处：主 sln 编不到它。所以 `tools/build.ps1` **两条都编**——
一条命令就是全仓库的验收：

```powershell
.\tools\build.ps1 -Strict   # 引擎 + 内容 + 571 条用例 + Web 宿主 + 前端冒烟（0 警告）
.\tools\web.ps1   build -Strict   # 只编 Web 宿主时用它
.\tools\api-test.ps1        # 再把宿主真起起来，打一遍端点（含 SSE 流）
```

`tools/build.ps1` 与 `tools/api-test.ps1` 也是 CI 在远端跑的命令
（`.github/workflows/ci.yml` 现在是**三个作业**：`build-and-test`（这条命令）、
`web-smoke`（`node tools/web-smoke.mjs`）与 `end-to-end`（`tools/api-test.ps1`））
——前者守引擎与公开 API，后者守"宿主 + 浏览器协议"那一段。

---

## 状态

- 核心引擎、十一个内容包（猫咖物语 / 猫娘咖啡馆 / 九命轮回 / 猫娘实验室 / 猫娘公司 / 猫娘末世 /
  猫娘图书馆 / 猫娘神明 / 猫娘文明 / 赛博猫娘 / 猫娘梦境）、**两个前端宿主**（终端 Demo + Web），
  **564 个测试**全部通过（2026-10-04 实测；这个数会随守卫增长，权威出处永远是
  `tools/build.ps1 -Strict` 打印的那一行——见 [OPEN_WORK](engine/docs/OPEN_WORK.md) 的 W1）。
- **端到端回归与 CI 已落地**：`tools/api-test.ps1` 起真宿主打一遍全部端点（静态文件 / 前端面板 /
  元信息 / 快照 / 命令 / 通知 / 负数 / SSE 流 / **离线收益** / **永久线货币语义** /
  **建筑专属升级** / **存档的导出与导入**，65 项检查——最后一段会带同一份存档
  **再起一次宿主**，验"读档补发 → 没播报之前刷新不消失 → 收下之后消失"），自带临时存档目录、
  缺省把调试门关着、收尾按端口反查并清掉
  `dotnet run` 的子进程；`.github/workflows/ci.yml` 在每次 push / PR 上跑 `tools/build.ps1 -Strict`
  与它——"公开 API 只增不改"因此不再只靠纪律。
- **分层转生（`Era`）已落地**：逐级推进的转生按钮、每层换规则的平衡覆盖、
  跨层继承、构建期的完成条件单调性校验。九命（9 层）、实验室（7 批）、公司（3 轮）、
  末世（5 次重启）、图书馆（5 本书）、神明（5 套神话）、文明（5 个时代）、赛博（5 层）、
  梦境（5 层梦）全程可达由机器人测试守住。
- **图鉴 / 叙事释放（S-B）已落地**：叙事线 + 条目、条件达成即解锁、图鉴面板（`???` 遮蔽
  与条件进度）、待读提示、存档往返。#1 咖啡馆 51 条（3 条线）+ #2 九命 50 条（4 条线）
  + #3 实验室 40 条（4 条线）+ #10 公司 40 条（4 条线）+ #6 末世 40 条（4 条线）
  + #9 图书馆 40 条（4 条线），全部条目的释放条件互不相同、且线内顺序与实际解锁顺序一致；
  层内门槛一律低于本层完成门槛（通用守卫 `LoreTests.EraGatedLore_StaysBelowItsEraCompletion`）。
- **选择 / 立场轴（S-C）与终局判定已落地**：内容自定义的 `StanceDefinition`（不是 enum）、
  作答后累加立场权重、主导立场作为 `ModifierResolver` 的第 5 个来源、末层完成后按条件树挑出
  唯一结局。#2 九命（神性 / 人形 / 猫形 / 断绝，6 次表态）、#3 实验室（乌托邦 / 叛乱 / 共存 /
  删除，6 次表态）、#10 公司（上市派 / 工会派 / 清算派，6 次表态）三套词表共用同一套代码；
  Demo 有「表态」面板，无头报告会打印立场轴与结局（供 CI 断言）。
  立场与已答选择跨重组 / 舍命 / 转生保留。
- **ROADMAP 阶段 0 已交付**：内容包 #1《猫娘咖啡馆》（10 建筑 / 48 升级 / 45 成就 /
  5 增益 / 8 随机事件 / 幸福感模块）+ Demo `--package` 切换 + 架构不变量测试，
  全部走既有框架能力（核心引擎零改动）。
- **3C-1 已交付**：内容包 #3《猫娘实验室》——第一个同时用 `Era` + `Lore` + `Choice`
  三项能力的包（9 建筑 / 46 升级 / 66 成就 / 8 增益 / 10 随机事件 / 40 条叙事（4 条线）/
  7 批纪元 / 4 条道德立场 / 6 次表态 / 5 个结局 + 「伦理值」模块），**核心引擎仍零改动**。
- **3C-2 已交付**：内容包 #10《猫娘公司》——第三套 `Era` 叙事（车库 → A 轮 → 上市）、
  第二套立场轴（劳资：上市派 / 工会派 / 清算派）、原创模块「士气」（团队建筑养、加班吃）
  （9 建筑 / 46 升级 / 66 成就 / 8 增益 / 10 随机事件 / 40 条叙事（4 条线）/ 3 轮重组 /
  6 次表态 / 4 个结局），**核心引擎同样零改动**。
- **阶段 4 已全部交付**：前半（4A）是内容包 #6《猫娘末世》——**全项目第一个用「跨转生继承」的包**
  （继承比例 0 → 25% → 40% → 55% → 70%），第二资源「记忆残片」的产率取决于上一次重启留下多少座位；
  后半（4B）是内容包 #9《猫娘图书馆》——**「虚无化」唯一的用武之地**，第二资源「被阅读度」
  是全项目唯一一个**会自己掉下去**的指标（按比例衰减、每次开新书清零），产量乘数随它走
  （0 读者 ×0.5、2 万 ×1.0、6 万 ×2.0 封顶，**下限保证不会归零到死锁**）。
  两个包都**没有立场轴**，结局分别由"记住了多少 + 图鉴读了多少"和
  "合上书的时候还有没有人在读"决定——同一套 `EndingDefinition` 与优先级判定，
  既能表达立场驱动的结局，也能表达记忆 / 存在驱动的结局。
  **S-D 没有新增核心能力**：原计划预留的 `DecaySystem` 与"`ModifierResolver` 第 6 个来源"
  都不需要，它落在已有的 `IGameModule` + `Scaling(ScalingSource.CustomCounter)` 上。
- 4A 顺带暴露并处理了一个继承特有的坑：解锁条件若用「本轮累计」，重启归零之后，
  手上已有的建筑会显示成「未解锁」——末世包改用「历史累计」，并有用例钉住。
- 4B 顺带查出并修掉两处**沉默失败**：`Scaling.Cap` 限的是原始计数值而不是加成结果
  （末世包有四处文案与数值不符），以及"挂了纪元门槛的叙事，层内门槛必须低于本层完成门槛"
  这条纪律以前只写在文档里——现在它是通用守卫，对全部内容包生效。
  两个包的天花板也都按**实测包络**重设过一遍（重设后图鉴均为 40/40）。
- **阶段 5 已全部交付**（四个「换皮」包，**核心一行都没动**——逐包 `git diff engine/core/` 为空）：
  #7《猫娘神明》（五套神话体系 + 信仰 + 在线人数峰值）、#4《猫娘文明》（五个时代 + 文化）、
  #5《赛博猫娘》（迁服务器 + 算力）、#8《猫娘梦境》（五层梦 + 梦境能量）。
  四个包各 9 建筑 / ≥48 升级 / ≥65 成就 / 40 条叙事（4 条线）/ 5 层 / 2~3 结局，
  第二资源都是**单调不减**的（所以都能进纪元完成条件；这与 #9 那条"会掉的计数器不能进完成条件"
  正好互为对照），每层只改一处规则。实测四个包各跑 12 小时：图鉴全部 **40/40**、
  机器人全部走到第 5 层。同一阶段还把「建筑曲线配方」从纪律变成了通用守卫
  （`ContentTests.BuildingCurve_FollowsTheRecipeInEveryContentPack`）。
- 十个内容包（#1 咖啡馆 / #2 九命 / #3 实验室 / #4 文明 / #5 赛博 / #6 末世 / #7 神明 /
  #8 梦境 / #9 图书馆 / #10 公司）的数值曲线都经过无头模拟验证（见 `--simulate --auto`；
  #2 跑 48 小时、#3 跑 4 小时、#10 跑 6 小时、其余各跑 12 小时看阶段推进）。
- **转生货币线已修**（专项 #1）：五个纪元包原本都照抄 `PrestigeDivisor = 1e12`，
  而它们的可结算历史累计被自己的阶梯卡在 1e8~1e11——实测结算货币全是 **0**，
  「前世技能 / 前世经验 / 余烬 / 批注」这几条线**结构上打不开**（不是难，是永远拿不到）。
  现在每个包的除数按**自己的阶梯**标定（目标是「最后一次结算落在 ~100 级」），
  永久线价格也压进了这个包络。实测结算货币 **98 / 59 / 144 / 100 / 101**，
  永久线总价 **80 / 47 / 115 / 80 / 80**——一局买得完。
  守卫：`PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun`。
  经典包「猫咖物语」与 #1 咖啡馆**刻意不动**：它们的转生是可重复的循环，包络本来就是开放的。
- **计数器内部键不再漏进玩家文案**（专项 #2）：`readership` / `morale` / `faith` 这些键
  会出现在解锁提示、升级效果和「本层规则」里，渲染出来就是「每点「readership」」。
  现在内容与模块可以在 `Configure` 里登记显示名（`AddCounterName`），
  两条渲染路径（`Scaling.Describe` / `UnlockCondition.Describe`）都走这张表，
  未登记的键回退成键本身（旧内容不会崩）。
  守卫：`ContentTests.CounterNames_AreRegisteredForEveryReferencedCounter`——
  它要求「引用到的每个计数器都登记过」，而且**真的去渲染一遍**，断言输出里既含显示名、又不含内部键。
- **v1.0.0：框架已被产品化**——规划范围内的功能早已全部交付，缺的是"别人能安全依赖它"
  这件事。这一版补齐的就是它：版本号单一事实来源、`ApiVersion`、1897 行公开 API 快照、
  五条版本守卫（含故障注入证明守卫会红）、`engine/docs/VERSIONING.md` 与 `CHANGELOG.md`。
  **没有改任何游戏行为**——十一个内容包与 396 条既有用例的行为完全不变，用例 396 → 404。
  这一步也把"核心零内容知识"这条主张补上了它的另一半：**公开 API 只增不改**。
- **Web 前端已可玩**（已并入 `main`）：浏览器里能点击、
  买建筑与升级、推进纪元、表态、看图鉴，挂机不掉线，与终端 Demo **共用同一份存档**。
  零前端依赖（手写 ES 模块 + 一份 CSS，无 npm、无打包步骤）。
  跳层的调试门默认关闭，要用环境变量 `NEKO_DEBUG_KEY` 临时开门；
  详见 [STATUS.md](STATUS.md) 与 [WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。
- **剧情可以当文件读了**：十个有剧情的包（咖啡馆 / 九命 / 实验室 / 公司 / 末世 / 图书馆 /
  神明 / 文明 / 赛博 / 梦境）的**全部 421 条图鉴散文**都搬进了各自目录下的 `text.json`
  （1.2.0 先在实验室试点，随后铺满全部包），改一个错字不再需要重编。
  散文在文件里、条件与序号在代码里，两边靠 id 关联；id 打错、文件里多一条 / 少一条都会在
  启动时当场抛，而不是变成一本空白图鉴或一段永远没人读的死文本
  （守卫：`ContentTextFileTests`，含一条"有 text.json 的包必须都在守卫表里"的覆盖度用例）。
  承载它的 `ContentText` 是公开 API。
- **未包含**：**美术与音效资源**、本地化资源系统、账号 / 云存档、排行榜、反作弊。
  这些都被设计为引擎外部的宿主职责。
  > 这里原本还列着「图形前端」——它已经不再是非目标：`games/hosts/Web/` 就是。
  > 但对"美术资源"这条仍然成立：Web 前端目前用的是 emoji 与 CSS，没有立绘、没有音效。
  ROADMAP 规划范围内的**十个内容包至此全部交付**，
  四个引擎能力里只有 S-A / S-B / S-C 动过核心（S-D 一行核心代码都没写）。

**接下来做什么**：见 [OPEN_WORK 顶部的《下一步：唯一权威清单》](engine/docs/OPEN_WORK.md)
（H / D / W 三张表——只有真人能做的、先要人拍一句话的、以及不需要人的机械项）。
`STATUS.md` 的 §8 是**历史记录**，不再产生待办。工作树是 **1.10.1**
（patch：导出/导入第一次有了两个宿主里的窗口（W13 / W14）+ 十一个包的「点击 × 建筑」桥
+ 两处宿主修复；**公开 API 一行没动**，快照除首行版本号外逐字节不变。
上一版 1.10.0 是引擎侧那一半：信封 + 校验和 + 尺寸闸 + "坏输入绝不弄坏能用的存档"的
12 类失败语义，**存档格式一个字没改**，见
[SAVE_TRANSFER_PLAN](engine/docs/SAVE_TRANSFER_PLAN.md)。
1.9.0 是**纪元内的阶段**：`EraStage` + `EraDefinition.Stages` + `EraSystem.Stage` +
`EraView` 的阶段六字段，试点只有公司包 5/8/8 段，**一行数值都没改**；
1.8.0 是**建筑升级系统**：
`UpgradeCategories` + `GameContent.UpgradesForBuilding` + `BuildingView.UpgradeIds`；
1.7.0 是那条线之外的纯新增 `GameSnapshot.ModeName`。
结局的落定条件那三次改动（定时 → 被展示过 → **被答完**）各配了一次 minor 升版与快照再生成），
**这一版已经发完**：打包产物与两个 exe 实跑都验过（原文见 [RELEASING](engine/docs/RELEASING.md) §6），
提交与 tag 都已推送，CI 两个作业 success（2026-10-05 复核，详见 `STATUS.md` §1 末尾）；
再往后是二周目界面（要先定语义）——`PackageId` 那件打包元数据 **2026-10-05 已经补上了**（登记册 W5）。
1.2.1 / 1.3.0 / 1.4.0 是怎么发的（含发布流程与实际执行记录）
见 [engine/docs/RELEASING.md](engine/docs/RELEASING.md)。
