# NekoClicker

零依赖的 C#/.NET 8 增量游戏框架：一个引擎，十二个内容包，**换包即换游戏**。
引擎负责时间推进、数值管线、存档与事件；内容只描述"这个世界有什么"。
换掉内容包就能做出完全不同的游戏——最新一例《猫娘电台》把核心动词反转成"点击是干扰"，引擎代码零改动。

**当前版本 `1.13.2`**（`NekoClicker.Core` 的公开 API 版本）。从 `1.0.0` 起
**公开 API 只增不改**，而且有测试守着——见 [版本与兼容性承诺](#版本与兼容性承诺)。
逐版本明细见 [CHANGELOG](CHANGELOG.md)，项目现状见 [STATUS](STATUS.md)。

---

## 快速开始

> 脚本在 `.\tools\` 下，Windows PowerShell 与 PowerShell 7 都适用；若执行策略拦截，
> 先跑 `Set-ExecutionPolicy -Scope Process Bypass`。普通开发机也可以直接 `dotnet build` / `dotnet run`。

```powershell
# 构建 + 跑测试（619 个用例，零依赖迷你运行器）
.\tools\build.ps1

# 一条命令全仓库验收：引擎 + 内容 + 619 条用例 + Web 宿主 + 前端冒烟（0 警告）
.\tools\build.ps1 -Strict

# 玩（终端全屏界面；默认「猫咖物语」）
.\tools\play.ps1
.\tools\play.ps1 --package radio        # 换包：任意包 id，见下表

# 玩（Web；本机缺 ASP.NET 8 运行时也能起）
.\tools\play-web.ps1 -Package radio

# 无头模拟：机器人替你玩 6 小时并打印数值报告（--no-save 不碰真人存档）
.\tools\play.ps1 --package radio --simulate 21600 --auto
.\tools\balance-lab.ps1                 # 横扫全部包，聚合"到层时长 / 结局"对比表

# 造一个新包：一条命令生成合规两层 MVP（自动注册 + 同步全部跨包守卫表）
.\tools\new-pack.ps1 -Id garden -Name "猫娘花园" -Currency "花"
```

终端的 conhost 兼容细节（备用屏崩溃、缩放重排）与 `--frame` / `--panel` 渲染参数见
`tools/play.ps1` 头部注释与 [Demo.Cli 源码](games/hosts/Demo.Cli/)。

## 内容包（十二个）

| id | 名称 | 一句话特色 |
|---|---|---|
| `neko` | 猫咖物语 | 纯数据基线包，框架回归的对照物 |
| `cafe` | 猫娘咖啡馆 | 第二资源「幸福感」模块，核心零改动的第一个证据 |
| `ninelines` | 九命轮回 | 九层纪元 + 神性/人形/猫形/断绝立场轴 |
| `lab` | 猫娘实验室 | 七批纪元 + 伦理值 + 四条道德立场 |
| `company` | 猫娘公司 | 三轮重组 + 士气模块 + 劳资立场轴 |
| `apocalypse` | 猫娘末世 | 跨转生继承 + 记忆残片 |
| `library` | 猫娘图书馆 | 唯一会「掉」的第二资源「被阅读度」 |
| `god` | 猫娘神明 | 五套神话体系 + 信仰 |
| `civ` | 猫娘文明 | 五个时代 + 文化，从猫窝走到星港 |
| `cyber` | 赛博猫娘 | 迁服务器 + 算力 |
| `dream` | 猫娘梦境 | 五层梦 + 越睡越浓的梦境能量 |
| `radio` | 猫娘电台 | **点击是干扰**：收听率会掉，管住手才是最优解 |

## 仓库结构

```
engine/core/            框架核心：内容定义、模拟引擎、存档、事件、UI 视图（无 UI、无 IO 硬编码）
engine/content/<包>/    十二个内容包，一个包 = 一个 csproj（纯数据 + 可选 IGameModule）
engine/tests/           619 个测试 + 自研迷你测试运行器（含架构不变量与全程可达测试）
engine/docs/            架构、内容写作、版本、发布等设计文档
games/hosts/Demo.Cli/   终端宿主（ANSI 全屏 + 键盘 + 无头模式 + --package）
games/hosts/Web/        Web 宿主（本地 HTTP + wwwroot，零前端依赖，浏览器里可玩）
games/docs/             世界观、路线图、创意池
tools/                  构建 / 游玩 / 打包 / 端到端 / 数值实验室 / 新包脚手架
```

| 目录 | 是什么 | 提取性 |
|---|---|---|
| `engine/` | 引擎 + 内容包 + 测试 | **自包含**：整个搬走配合根 `Directory.Build.props` 即可独立构建 |
| `games/` | 旗舰示例宿主与文档 | 依赖 `engine/`，反向不依赖 |

内容包放在 `engine/` 是因为它们同时是**引擎的集成测试探针**：架构不变量与通用守卫
（曲线配方、主线长度、点击桥、语气棘轮……）要横扫每一个包才抓得出沉默失败。

---

## 框架能力

下列机制全部来自 Cookie Clicker，并且都被抽象成**内容可配置**的形式：

| 机制 | 实现 | 内容里怎么配 |
|---|---|---|
| 建筑（自动生产） | 价格按 `PriceGrowth` 指数增长，产量与数量线性 | `BuildingDefinition` |
| 批量购买 / 出售 | 等比数列闭式解，含"买满"的解析反解；出售按最近单价 × 返还比例 | `GameBalance` |
| 升级 | 一次性 / 有限次 / 转生后保留 | `UpgradeDefinition` |
| 成就 | 条件树达成即永久激活修饰符 | `AchievementDefinition` |
| 「牛奶」式成长 | 效果随成就数、建筑数、转生等级、标签数动态变化 | `Scaling` |
| 限时增益 | 刷新 / 叠加时长 / 叠层三种叠加规则 | `BuffDefinition` + `BuffStackMode` |
| 金猫（随机事件） | 加权结果表：幸运、狂热、被抢、连锁…… | `GoldenCookieOutcome` |
| 转生 / 天堂升级 | `等级 = ⌊(历史累计 / 除数)^(1/3)⌋`，永久升级跨转生保留 | `UpgradeCurrency.PrestigeChips` |
| 分层转生（`Era`） | 纪元串成链，「舍一命」只有一个按钮，未完成置灰并显示最落后子条件 | `EraDefinition` + `EraGate` |
| 图鉴 / 叙事释放 | 条目挂叙事线，达成即解锁；未解锁显示 `???` + 条件进度 | `LoreEntry` + `StorylineDefinition` |
| 选择分支 / 立场轴 | 作答后才生效；主导立场的修饰符计入产量 | `ChoiceDefinition` + `StanceDefinition` |
| 终局判定 | 主线走完后按条件树挑唯一结局（Priority 定序、互斥、强制兜底） | `EndingDefinition` |
| 离线收益 | 按不含增益的产量补发，有上限 | `GameBalance.OfflineCapSeconds` |
| 存档 | JSON + 加密分享码 + 版本迁移（迁移在反序列化之前对 JSON 树执行） | `ISaveMigration` |
| 模块扩展 | 引擎不认识的系统（第二资源、市场、小游戏） | `IGameModule` |

### 数值管线（框架的核心）

所有加成最终折叠成一条可预测的公式，而不是散落在各处的 `if`：

```
单个建筑产量 = ((基础产量 + 加法项) × (1 + Σ加法百分比) × Π乘法) ^ Π乘方
建筑总产量   = 单个建筑产量 × 持有数量
总 CPS       = Σ 建筑总产量 × 全局倍率，再统一施加全局乘方
点击收益     = (固定基础 + 总CPS × 比例 + 加法项) × 点击倍率
```

新增一个升级不需要改引擎，只要声明它对哪个目标做什么运算。加法百分比与乘法分开累计：
"两个 +50%"得 +100%，"两个翻倍"得 ×4——玩家能直觉预期。

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

// 2) 创建引擎；命令行/服务器/测试里都只需要这几行
var engine = new GameEngine(content);
engine.Click();
engine.BuyBuilding("pick", 10);
engine.BuyUpgrade("steel_pick");
engine.Simulate(3600);                      // 跳过 1 小时
Console.WriteLine(engine.CookiesPerSecond);
Console.WriteLine(engine.Save());           // JSON 存档
```

接前端时只需要 `engine.Snapshot(mode)` 拿只读视图（价格、能否买得起、进度、格式化文本都已算好），
再把点击/购买命令发回引擎。两个现成宿主（终端 / Web）就是这么接的。

## 设计取舍

- **`double` 而不是大数库**：与 Cookie Clicker 一致，1e308 够用；溢出由 `Num.SafeAdd/SafeMul`
  拦成饱和值。真要换表示，改动面限制在 `Numbers/` 与 `Pricing`。
- **零第三方依赖**：核心只用 BCL，任意宿主（Unity / Mono / 离线沙箱）引用都不产生依赖冲突；
  测试因此自带迷你运行器而不是 xunit。
- **固定步长推进**：帧率波动不影响产量；累加器 + 补算上限保证"卡帧不白拿、挂起不算爆"。
- **`GameState` 与 `SaveData` 分两套类型**：状态可自由重构，存档一旦发布必须向后兼容。

---

## Web 宿主与加密链接

Web 宿主可玩：点击、买建筑与升级、推进纪元、表态、图鉴、离线收益弹窗、永久升级线，
与终端**共用同一份存档**（`saves/<包 id>.json`）；零前端依赖（手写 ES 模块 + 一份 CSS）。
URL 上有三扇加密门（钥匙是环境变量 `NEKO_URL_KEY`，与明文调试门 `NEKO_DEBUG_KEY` 互不授权）：

- `?k=` **跳层令牌**：把某一局直接放到第 N 层（调试用，内存局、退出即弃）；
- `?share=` **分享链接**：当前存档加密成链接，密码走请求体、永不进 URL，导入走与导出同一条保证路径；
- `?spectate=` **观战链接**：同一段密文的只读打开——不建会话、不写盘、命令一律 no-op。

边界（8000 字符上限、密码最短 4 字符、可离线枚举、不是权限系统）与线格式见
[SHARE_LINK_PLAN](engine/docs/SHARE_LINK_PLAN.md)、[WEB_DEBUG_GATE_PLAN](engine/docs/WEB_DEBUG_GATE_PLAN.md)。
终端不做链接（没有地址栏），走「导出 → `I` 导入」。

---

## 版本与兼容性承诺

**当前版本 `1.13.2`。从 `1.0.0` 起，`NekoClicker.Core` 的公开 API 只增不改。**

| 改动 | 升哪一位 |
|---|---|
| 公开 API 只增不改（新增方法 / 类型 / 带默认值的参数） | minor |
| 公开 API 有不兼容改动（删除、改签名、收紧可空标注、改语义） | major |
| 公开 API 一行没动（内容数值、文案、修 bug） | patch |

已知例外（结局落定时机的三次 minor、`1.12.0` 的显示写法变更）都记录在
[VERSIONING §2](engine/docs/VERSIONING.md)——例外要被记录，否则分不清"决定"和"疏忽"。

版本号的单一事实来源是 `Directory.Build.props` 的 `<Version>`，宿主运行时可读：

```csharp
using NekoClicker.Core;

Console.WriteLine(ApiVersion.Current);         // "1.13.2"
Console.WriteLine(ApiVersion.AssemblyVersion);  // 1.13.2.0
```

这条承诺靠一份**快照**加守卫强制执行，不靠自觉：`engine/core/PublicApi.txt` 嵌进 dll，
`PublicApiTests` 逐项比对＋覆盖度＋故障注入，`VersionTests` 让版本号与程序集元数据、快照、
CHANGELOG 三方对齐。守卫红了问的是「你知道自己在破坏兼容性吗」——所以
`tools/public-api.ps1` 只是流程的最后一步。完整规矩与发布清单见
[VERSIONING](engine/docs/VERSIONING.md) 与 [RELEASING](engine/docs/RELEASING.md)。

---

## 环境说明

`tools/dnet.ps1` 解决三个真实约束：沙箱里 `%USERPROFILE%` 不可写（重定向 dotnet home 与 NuGet 缓存）、
受限沙箱禁命名管道（MSBuild 强制 `-m:1`）、新 SDK 不自带 net8.0 targeting pack
（`seed-packages.ps1` 离线播种）。普通开发机与 CI 直接 `dotnet build` 即可，不需要它。

Web 宿主刻意不在主 sln 里（"改前端"与"动引擎基线"靠 sln 边界分开），所以 `build.ps1` 两条都编；
`-Strict` 就是全仓库验收（见快速开始）。CI 在每次 push / PR 上跑四个作业：
`build-and-test`、`engine-linux`（平台中立守卫）、`web-smoke`、`end-to-end`
（`tools/api-test.ps1`：起真宿主打一遍全部端点（含 SSE），**123 项检查**）。

**未包含**：美术与音效资源、本地化资源系统、账号 / 云存档、排行榜、反作弊——都是引擎外部的宿主职责。

## 接下来读什么

| 文档 | 回答什么 |
|---|---|
| [ARCHITECTURE](engine/docs/ARCHITECTURE.md) | 引擎怎么分层、设计决策 |
| [CONTENT_AUTHORING](engine/docs/CONTENT_AUTHORING.md) | 怎么写内容：数值节奏、校验规则、常见坑 |
| [CREATIVE_PLAN](games/docs/CREATIVE_PLAN.md) | 创意池：下一批包与机制的候选与五关评审 |
| [ROADMAP](games/docs/ROADMAP.md) | 实施规划与已定决策 |
| [OPEN_WORK](engine/docs/OPEN_WORK.md) | 会话小结与待办唯一权威清单（最新一条在最前） |
| [HANDOVER_2026-11-13](engine/docs/HANDOVER_2026-11-13.md) | 最近一轮收工总结与交接 |
| [STATUS](STATUS.md) / [CHANGELOG](CHANGELOG.md) | 现在是什么状况 / 逐版本改了什么 |
