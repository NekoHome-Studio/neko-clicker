# NekoClicker — 增量游戏框架（C# / .NET 8）

参考 **Cookie Clicker** 的机制设计的一套**增量（放置 / 点击）游戏框架**，纯 C# 实现，
**零第三方依赖**，附带两个内容包（示例包「猫咖物语」+ 内容包 #1《猫娘咖啡馆》）和一个可玩的终端 Demo。

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
└──────────────────────────────┘
```

---

## 快速开始

> 下面的命令使用 `.\tools\` 下的脚本调用（Windows PowerShell 与 PowerShell 7 都适用）。
> 若执行策略拦截脚本，先在当前会话运行 `Set-ExecutionPolicy -Scope Process Bypass`。
> 在普通开发机上也可以直接 `dotnet build` / `dotnet run`。

```powershell
# 构建 + 跑测试（161 个用例，零依赖迷你运行器）
.\tools\build.ps1

# 只构建全部项目
.\tools\dnet.ps1 build NekoClicker.sln

# 开始玩（终端全屏界面；默认「猫咖物语」）
.\tools\play.ps1

# 换内容包：玩内容包 #1《猫娘咖啡馆》（第二资源「幸福感」）
.\tools\play.ps1 --package cafe

# 无头模拟：让机器人替你玩 6 小时并打印数值报告
.\tools\play.ps1 --simulate 21600 --auto
.\tools\play.ps1 --package cafe --simulate 21600 --auto

# 渲染一帧界面（用于验证布局 / 截图，可重定向到文件）
.\tools\play.ps1 --simulate 1800 --auto --frame 118x32 --no-color
```

### 目录结构

```
src/NekoClicker.Core/            框架核心：内容定义、模拟引擎、存档、事件、UI 视图
src/NekoClicker.Content.Neko/    示例内容包「猫咖物语」（纯数据，无逻辑；框架回归基线）
src/NekoClicker.Content.Cafe/    内容包 #1《猫娘咖啡馆》（含幸福感模块，核心零改动）
src/NekoClicker.Demo.Cli/        终端 UI 适配层（ANSI 全屏 + 键盘 + 无头模式 + --package）
tests/NekoClicker.Core.Tests/    161 个测试 + 自研迷你测试运行器（含架构不变量测试）
docs/ARCHITECTURE.md             架构与设计决策
docs/CONTENT_AUTHORING.md        如何写内容（数值节奏、校验规则、常见坑）
docs/ROADMAP.md                  实施规划与决策记录：11 项已定决策、4 条架构不变量、5 个阶段
docs/NINE_LIVES_DESIGN.md        《九命猫娘》设计映射：1 个共享核心 + 10 个内容包（1 个已落地）
docs/PACK_01_CAT_CAFE.md         #1《猫娘咖啡馆》完整内容规格（已落代码，也是其余九个包的模板）
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
   没有网络时构建会以 `NU1301` 失败。`tools/seed-packages.ps1` 会把全局 NuGet 缓存里
   已有的 8.0.x targeting pack 复制进仓库内的 `.packages`（找不到就跳过），`dnet.ps1`
   在执行 build/restore 前自动调用它。

> 若第 3 条仍然失败（全局缓存里也没有这些包），先用一次联网的 `dotnet restore` 把它们拉下来，
> 再运行 `.\tools\seed-packages.ps1`；也可以用 `-Version` 指定本机 SDK 需要的补丁版本。

在其他环境下（普通开发机、CI）可以直接用 `dotnet build` / `dotnet run`，不需要这个脚本。

---

## 状态

- 核心引擎、两个内容包（猫咖物语 / 猫娘咖啡馆）、终端 Demo、**161 个测试**全部通过。
- **ROADMAP 阶段 0 已交付**：内容包 #1《猫娘咖啡馆》（10 建筑 / 48 升级 / 45 成就 /
  5 增益 / 8 随机事件 / 幸福感模块）+ Demo `--package` 切换 + 架构不变量测试，
  全部走既有框架能力（核心引擎零改动）。
- 两个内容包的数值曲线都经过 6 小时无头模拟验证（见 `--simulate --auto`）。
- **未包含**：图形前端、本地化资源系统、账号/云存档、排行榜、反作弊，以及 ROADMAP 阶段 1~5
  的 `Era` / 叙事 / 选择 / 虚无化四项引擎能力。这些都被设计为引擎外部的宿主职责或后续阶段。
