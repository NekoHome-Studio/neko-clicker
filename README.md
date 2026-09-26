# NekoClicker — 增量游戏框架（C# / .NET 8）

参考 **Cookie Clicker** 的机制设计的一套**增量（放置 / 点击）游戏框架**，纯 C# 实现，
**零第三方依赖**，附带七个内容包（示例包「猫咖物语」+ #1《猫娘咖啡馆》+ #2《九命轮回》
+ #3《猫娘实验室》+ #10《猫娘公司》+ #6《猫娘末世》+ #9《猫娘图书馆》）和一个可玩的终端 Demo。

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
# 构建 + 跑测试（320 个用例，零依赖迷你运行器）
.\tools\build.ps1

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
src/NekoClicker.Core/            框架核心：内容定义、模拟引擎、存档、事件、UI 视图
src/NekoClicker.Content.Neko/    示例内容包「猫咖物语」（纯数据，无逻辑；框架回归基线）
src/NekoClicker.Content.Cafe/    内容包 #1《猫娘咖啡馆》（含幸福感模块，核心零改动）
src/NekoClicker.Content.NineLives/ 内容包 #2《九命轮回》（九层纪元，第一个用分层转生的包）
src/NekoClicker.Content.Lab/     内容包 #3《猫娘实验室》（七批纪元，第一个同时用 Era+Lore+Choice 的包）
src/NekoClicker.Content.Company/ 内容包 #10《猫娘公司》（三轮重组，第二个用 Choice + 自己的劳资立场轴）
src/NekoClicker.Content.Apocalypse/ 内容包 #6《猫娘末世》（五次重启，第一个用跨转生继承的包）
src/NekoClicker.Content.Library/ 内容包 #9《猫娘图书馆》（五本书，「虚无化」唯一的用武之地）
src/NekoClicker.Demo.Cli/        终端 UI 适配层（ANSI 全屏 + 键盘 + 无头模式 + --package）
tests/NekoClicker.Core.Tests/    320 个测试 + 自研迷你测试运行器（含架构不变量与全程可达测试）
docs/ARCHITECTURE.md             架构与设计决策
docs/CONTENT_AUTHORING.md        如何写内容（数值节奏、校验规则、常见坑）
docs/ROADMAP.md                  实施规划与决策记录：11 项已定决策、4 条架构不变量、5 个阶段
docs/NINE_LIVES_DESIGN.md        《九命猫娘》设计映射：1 个共享核心 + 10 个内容包（6 个已落地）
docs/PACK_01_CAT_CAFE.md         #1《猫娘咖啡馆》完整内容规格（已落代码，也是其余九个包的模板）
docs/STAGE_5_RESKINS.md           阶段 5 换皮批产手册：#4/#5/#7/#8 四个包的交接件（规则清单 + 验收命令 + 已知坑）
tools/build.ps1                  一键构建 + 测试
tools/play.ps1                   构建并运行终端 Demo（参数转发给程序）
tools/dnet.ps1                   在受限环境里运行 dotnet CLI 的包装脚本
tools/seed-packages.ps1          把全局 NuGet 缓存里的 net8.0 targeting pack 播种进仓库（离线构建）
```

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

---

## 状态

- 核心引擎、七个内容包（猫咖物语 / 猫娘咖啡馆 / 九命轮回 / 猫娘实验室 / 猫娘公司 / 猫娘末世 /
  猫娘图书馆）、终端 Demo，**320 个测试**全部通过。
- **分层转生（`Era`）已落地**：逐级推进的转生按钮、每层换规则的平衡覆盖、
  跨层继承、构建期的完成条件单调性校验。九命（9 层）、实验室（7 批）、公司（3 轮）、
  末世（5 次重启）、图书馆（5 本书）全程可达由机器人测试守住。
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
- 六个内容包（#1 咖啡馆 / #2 九命 / #3 实验室 / #10 公司 / #6 末世 / #9 图书馆）的数值曲线
  都经过无头模拟验证（见 `--simulate --auto`；#2 跑 48 小时、#3 跑 4 小时、#10 跑 6 小时、
  #6 与 #9 跑 12 小时看阶段推进）。
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
- **未包含**：图形前端、本地化资源系统、账号/云存档、排行榜、反作弊，以及 ROADMAP 阶段 5 的
  #4 文明 / #5 赛博 / #7 神明 / #8 梦境四个「换皮」内容包。
  这些都被设计为引擎外部的宿主职责或后续阶段。
