# 内容作者指南

本文说明如何为这套框架写内容。所有示例都取自 `src/NekoClicker.Content.Neko`。

## 0. 心智模型

引擎只认识四件事：**修饰符（Modifier）**、**解锁条件（UnlockCondition）**、
**数值公式**、**时间**。你做内容时的全部工作就是把设计意图翻译成这三样的组合：

> "买了钢镐之后，镐子产量翻倍，但需要先有 1 把镐子"
> → `Modifiers = [Modifier.BuildingMultiplier("pick", 2)]` + `Unlock = BuildingsAtLeast("pick", 1)`

写内容时**永远不需要写逻辑代码**。如果你发现必须写，先确认是不是该用
`Scaling`（成长曲线）或 `IGameModule`（新玩法系统）。

## 1. 组装内容包

```csharp
public static GameContent Build()
    => new GameContentBuilder("猫咖物语")
        .WithCurrency("小鱼干", "🐟", "撸猫")     // 名称 / 图标 / 点击动作名
        .WithPrestigeCurrency("猫薄荷", "🌿")
        .WithBalance(BuildBalance())
        .AddBuildings(BuildingTable.All)
        .AddUpgrades(UpgradeTable.All)
        .AddAchievements(AchievementTable.All)
        .AddBuffs(BuffTable.All)
        .AddGoldenCookieOutcomes(OutcomeTable.All)
        .Build();                                  // ← 这里做全量校验
```

`Build()` 会校验并在失败时抛出 `GameContentValidationException`，错误信息里包含全部问题
（不是遇到第一个就停），所以可以一次修完。校验内容：

- id 重复 / 为空
- 价格、产量、增长率非法（`PriceGrowth <= 1` 会让价格永不增长）
- 解锁条件引用了不存在的建筑 / 升级 / 成就
- 金猫结果引用了不存在的增益，或声明了增益却没给时长
- 成就的条件恒为假（不可达内容）
- 永久升级用了普通货币计价

## 2. 建筑：数值节奏是全局体验的基础

```csharp
new BuildingDefinition
{
    Id = "cat_bed",              // 存档键，改动会导致旧存档丢这个建筑
    Name = "猫窝",
    Icon = "🛏️",
    Description = "猫在里面睡 16 小时。",
    BasePrice = 1_100,
    BaseCps = 8,
    Unlock = UnlockCondition.EarnedThisRunAtLeast(330),
    Tags = ["cat", "warm"],      // 供成长曲线引用
}
```

**相邻层的两个倍率决定整个游戏的节奏**（这是 Cookie Clicker 的核心配方）：

| 倍率 | 参考值 | 作用 |
|---|---|---|
| 价格倍率 | **×10 ~ 13** | 太大 → 每层只买得起一两个，节奏断裂 |
| 产量倍率 | **×6 ~ 8** | 太小 → 新建筑不值得买；太大 → 老建筑瞬间报废 |

价格涨得比产量快，意味着**单看"每块钱买多少产量"，越往后越差**。
这听起来反直觉，但正是它让游戏成立：由于价格随持有量指数增长，
当你有 100 个第一层建筑时，它的边际成本已经高得离谱，
此时新建筑的第一份才是真正的性价比之王。

`tests/NekoClicker.Core.Tests/ContentTests.cs` 里有一条测试专门守住这个区间，
改数值时它会告诉你有没有把曲线改坏。

**解锁条件建议用「本轮累计赚取」而不是「当前持有」**：玩家在快买得起时就能看到下一层，
形成"再攒一点就解锁"的牵引感；转生后重新逐层揭示，也避免开局被一长串灰色条目淹没。

## 3. 升级：四种写法覆盖绝大多数需求

### (a) 批量生成（建筑强化档）

```csharp
(int Required, double PriceFactor, string Suffix)[] tiers = [(1, 10, "更好的"), (5, 100, "加倍的"), (25, 500, "传奇的")];
foreach (var building in Buildings.All)
    foreach (var (required, priceFactor, suffix) in tiers)
        yield return new UpgradeDefinition
        {
            Id = $"{building.Id}_tier{required}",
            Name = $"{suffix}{building.Name}",
            Price = building.BasePrice * priceFactor,
            Unlock = UnlockCondition.BuildingsAtLeast(building.Id, required),
            Modifiers = [Modifier.BuildingMultiplier(building.Id, 2)],
            Category = $"building:{building.Id}",
        };
```

内容量上去之后，**这是唯一能维护下去的写法**：给建筑表加一行，强化档自动出现。

### (b) 联动成长（`Scaling`）

```csharp
Modifiers =
[
    new Modifier(
        ModifierTarget.BuildingCps("cat_bed"),
        ModifierOperation.AdditivePercent,
        0,                                                  // 基础值
        new Scaling(ScalingSource.BuildingCount, 0.02,      // 每单位 +2%
                    Cap: 100, Id: "curled_cat")),           // 上限 100 个单位
]
```

`ScalingSource` 可选：建筑数量、建筑总数、成就数、转生等级、点击数、累计赚取、
已购升级数、**带标签的升级数**、金猫数、游玩小时数、自定义计数器。
`Cap` 一定要设——否则后期一个升级就能吃掉整条曲线。

### (c) 「牛奶」：成就 → 全局倍率

成就本身不给数值，而是由一组升级按成就数量换算：

```csharp
new UpgradeDefinition
{
    Id = "kitten_helpers",
    Name = "小猫帮手",
    Price = 9_000_000,
    Unlock = UnlockCondition.AchievementsAtLeast(5),
    Modifiers = [Modifier.GlobalPercent(0, new Scaling(ScalingSource.AchievementCount, 0.01))],
}
```

这个设计的价值：玩家追求**任何**目标时都会顺带解锁成就，而成就立刻变成实打实的产量，
不会出现"解锁了但没用"的失落感。建议同时做 2~3 档（+1% / +2% / +3%）。

### (d) 可重复购买 / 天堂升级

```csharp
new UpgradeDefinition
{
    Id = "bulk_kibble_coupon",
    MaxPurchases = 10,
    PriceGrowth = 2.5,                    // 每张涨价 2.5 倍；不填则每次同价
    Modifiers = [Modifier.GlobalMultiplier(1.5)],
}

new UpgradeDefinition
{
    Id = "eternal_paw",
    Price = 3,
    Currency = UpgradeCurrency.PrestigeChips,      // 用转生货币买
    Persistence = UpgradePersistence.Permanent,    // 转生后保留
    Unlock = UnlockCondition.PrestigeChipsAtLeast(3),
    Modifiers = [Modifier.ClickMultiplier(3)],
}
```

> 校验规则：`Permanent` 的升级必须用转生货币计价。普通货币买永久升级会让
> "转生"这个决策失去意义，所以框架直接拦掉。

## 4. 成就：解锁条件即内容

```csharp
new AchievementDefinition
{
    Id = "curled_cat_x25",
    Name = "蜷缩的猫收藏家",
    Unlock = UnlockCondition.BuildingsAtLeast("curled_cat", 25),
}
```

条件可以组合：

```csharp
Unlock = UnlockCondition.All(
    UnlockCondition.ClicksAtLeast(500),
    UnlockCondition.UpgradeOwned("plush_gloves")),

Unlock = UnlockCondition.Any(UnlockCondition.CpsAtLeast(1e9), UnlockCondition.TotalBuildingsAtLeast(500)),
```

- 数值型条件会自动生成进度（UI 显示 `5 / 25`），所以**优先用数值条件**。
- `Hidden = true` 的成就在解锁前显示为 `???`。
- 少数成就直接给修饰符（见示例包里的「万次撸猫」）也是合法的。

### 4.1 全部可用的条件

| 工厂方法 | 说明 |
|---|---|
| `CookiesAtLeast` / `EarnedThisRunAtLeast` / `EarnedAllTimeAtLeast` | 存量与累计赚取 |
| `CpsAtLeast` / `ClicksAtLeast` / `PlayTimeAtLeast` | 产量、点击、时长 |
| `BuildingsAtLeast(id, n)` / `TotalBuildingsAtLeast(n)` | 建筑数量 |
| `UpgradesAtLeast(n)` / `TaggedUpgradesAtLeast(tag, n)` | 升级总数 / 按标签计数 |
| `AchievementsAtLeast(n)` / `GoldenCookiesAtLeast(n)` | 成就、随机事件 |
| `PrestigeLevelAtLeast(n)` / `PrestigeChipsAtLeast(n)` | 转生进度 |
| `UpgradeOwned(id)` / `AchievementUnlocked(id)` | 持有型条件 |
| **`Counter(key, n)`** | **自定义计数器**（第二资源，如 `Counter("happiness", 500)`） |
| `All(...)` / `Any(...)` / `Not(...)` | 组合子 |
| `Custom(描述, 谓词)` | 逃逸口，见 §8 |

> `Counter` 与 `TaggedUpgradesAtLeast` 都会给出**进度条**（`TryGetProgress`），
> 所以第二资源的解锁提示可以自动显示"还差多少"。
> `Custom` 没有进度，能不用就不用。

### 4.2 不可达内容会在构建期报错

`Build()` 的最后一步是**全局可达性分析**：确认每个建筑 / 升级 / 成就都存在一条
从开局状态出发的解锁路径。它会抓到两类内容错误：

```csharp
// 环：建筑 b 要升级 u，升级 u 又要建筑 b —— 谁也解不开
new BuildingDefinition { Id = "b", Unlock = UnlockCondition.UpgradeOwned("u") }
new UpgradeDefinition  { Id = "u", Unlock = UnlockCondition.BuildingsAtLeast("b", 1) }
// → 构建期抛 GameContentValidationException：
//   建筑「b」 永远无法解锁：它依赖 升级「u」，而这些内容同样解不开。
```

- 只有"指定建筑的数量"和"持有型条件"会阻塞解锁；其余指标（累计赚取、点击、时长、
  成就数、转生等级、计数器…）都会随进程自然增长，分析时视为可达。
- `Any(...)` 只要**有一条**可达分支就判定为可达，不会误伤。
- `Not(...)` 与 `Custom(...)` 一律视为可达（保守处理，避免误报）。

## 5. 增益：改变"当下该做什么"

```csharp
new BuffDefinition
{
    Id = "frenzy",
    Name = "猫薄荷狂热",
    Duration = 77,
    StackMode = BuffStackMode.Refresh,
    Modifiers = [Modifier.GlobalMultiplier(7)],
}
```

| `StackMode` | 重复施加时 |
|---|---|
| `Refresh` | 取较长的剩余时间（默认，适合狂热类） |
| `Extend` | 累加剩余时间（适合可续杯的效果） |
| `Stack` | 层数 +1 并重置时间（配合 `MaxStacks`，修饰符按层数重复计入） |

增益的定位是**短时间超高倍率**，它的价值取决于玩家是否在线并作出反应——
这和"离线收益"形成互补：挂机有保底，在线有爆发。

## 6. 金猫结果表

```csharp
new GoldenCookieOutcome
{
    Id = "lucky",
    Name = "幸运",
    Description = "幸运！获得 {amount} 条小鱼干。",
    Weight = 42,
    CookiesFromBankFraction = 0.15,                      // 存量的 15%
    CookiesFromBankFractionCapSecondsOfCps = 900,        // 但不超过 900 秒产量
    CookiesFromCpsSeconds = 13,                          // 再加 13 秒产量
}
```

公式与 Cookie Clicker 一致：`min(存量 × 15%, 产量 × 900秒) + 产量 × 13秒`。
说明文本支持 `{amount}` / `{duration}` / `{name}` 占位符。

**权重设计建议**（示例包的做法）：把"幸运 + 狂热"设在 70% 以上，保证玩家每次点金猫
大概率有正反馈；负面结果只占约 4%，并且只扣 5% 存量、不会扣成负数；
稀有结果合计约 6%，负责制造"截图发群"的时刻。

## 7. 平衡参数（`GameBalance`）

`GameBalance` 是"引擎行为"级的常量，所有默认值都对着 Cookie Clicker 的手感。
常调的几项：

| 参数 | 默认 | 说明 |
|---|---|---|
| `TickRate` | 30 | 逻辑帧率。降低会减少 CPU 但影响增益计时的精度 |
| `ClickBasePower` / `ClickCpsRatio` | 1 / 0.01 | 点击 = 固定值 + 当前产量的 1% |
| `GoldenCookieMinDelay` / `MaxDelay` | 5 / 15 分钟 | 随机事件间隔 |
| `FirstGoldenCookieDelayFactor` | 0.25 | 第一只提前出现，让新手尽早接触这个机制 |
| `OfflineCapSeconds` | 3 小时 | 离线收益上限 |
| `PrestigeDivisor` / `Exponent` | 1e12 / 1/3 | 转生公式；1 兆 = 1 级 |
| `MaxBulkBuy` | 100000 | `BuyMax` 单次上限，防止极端数值下的求解抖动 |

## 8. 常见坑

- **`Id` 是存档键**。发布后改名 = 老存档丢内容。要改名就写 `ISaveMigration`。
- **`Scaling` 记得设 `Cap`**，否则后期一个升级就能吃掉整条曲线。
- **`Scaling` / 修饰符引用的 id 必须真实存在**。写错建筑 id 或增益 id 在构建期就会报错——
  这类错误以前是静默失效（修饰符算了但没人受影响），现在拦在构建期。
- **不要让成就的 `Unlock` 恒为假**（构建期会报错）——不可达内容是纯负担。
- **不要写出互相依赖的解锁链**（构建期会报错，见 §4.2）。
  确实需要"暂时不可达"的内容时，用 `UnlockCondition.Custom` 显式声明。
- **修饰符数值必须是有限数**。`double.NaN` 会沿乘法链把整条产量算成 NaN，
  所以构建期直接拒绝 NaN / ∞；`Scaling.Cap = double.PositiveInfinity` 是合法的（表示不设上限）。
- **别给不接受 id 的目标传 id**（例如 `new ModifierTarget(ModifierTargetKind.GlobalCps, "cat_bed")`）——
  构建期会报错，因为这基本都是把建筑 id 写错了地方。
- **金猫结果里声明了 `BuffId` 就必须给 `BuffSeconds`**（构建期会报错）。
- **转生后建筑的解锁条件会重算**，用 `EarnedThisRunAtLeast` 时会重新逐层揭示——
  这是有意的，但如果你希望某些建筑永久可见，把它改成 `Always`。
- **`UnlockCondition.Custom` 会绕过可达性分析**（谓词无法静态分析），
  只在确实需要派生逻辑时使用，并自己保证它的可达性。
- **模块自己维护的派生状态要记得处理离线**。`Counters` 之类的状态不经过 `OnTick`
  就不会在离线期间增长——在 `IGameModule.OnOffline` 里按离线时长补算（见 §10）。

## 9. 验收内容改动

```powershell
# 1) 构建期校验 + 全部测试（含曲线区间回归）
.\tools\build.ps1

# 2) 曲线活性：6 小时自动游玩，观察产出曲线与解锁节奏
.\tools\play.ps1 --simulate 21600 --auto

# 3) 用固定种子对比改动前后的差异（可复现）
.\tools\play.ps1 --simulate 21600 --auto --seed 4242
```

第 3 步是这套框架相对"手写一个 clicker"最大的好处：因为随机是确定性的、
引擎是无头的，**数值改动可以被量化对比**，而不是靠感觉。

## 10. 扩展：模块（`IGameModule`）

当你要的东西**不是数据而是行为**——第二资源、小游戏、天气、股票——就用模块。
模块随内容包一起注册，引擎会在合适的时机回调：

```csharp
internal sealed class HappinessModule : IGameModule
{
    public string Name => "happiness";

    // 构建期：可以往内容里补定义（本模块不需要）
    public void Configure(GameContentBuilder builder) { }

    // 引擎创建后：订阅事件
    public void OnAttach(GameEngine engine) { }

    // 每个固定步长（默认 30Hz）
    public void OnTick(GameEngine engine, double deltaSeconds)
    {
        double rate = engine.State.TotalBuildings() / 50.0;
        engine.State.AddCounter("happiness", rate * deltaSeconds);
    }

    // 离线结算后：补算离线期间没走 OnTick 的那部分
    public void OnOffline(GameEngine engine, OfflineProgress progress)
    {
        double rate = engine.State.TotalBuildings() / 50.0;
        engine.State.AddCounter("happiness", rate * progress.CreditedSeconds);
    }

    // 转生后：重置模块自己的运行时状态（Counters 是跨转生保留的，按需清理）
    public void OnAscend(GameEngine engine) { }
}
```

注册方式：`new GameContentBuilder("...").Add(new HappinessModule())`。

**要点**：

| 事项 | 说明 |
|---|---|
| `OnTick` 频率 | 固定步长（`GameBalance.TickRate`），不是渲染帧率 |
| 状态存哪 | 优先 `GameState.Counters` / `Metadata`——它们会自动存档、且跨转生保留 |
| 离线 | 必须实现 `OnOffline`，否则派生状态在离线期间凭空落后。**只有实际发放离线收益时才会调用**（`GrantOfflineProgress = false` 时不调用） |
| 驱动修饰符 | `Scaling(ScalingSource.CustomCounter, perUnit, Id: "happiness")` |
| 作为解锁条件 | `UnlockCondition.Counter("happiness", 500)` |
| 与内容包的边界 | 内容包只提供数据；模块可以提供行为。**核心引擎永远不依赖具体模块** |
