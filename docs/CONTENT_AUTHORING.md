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

> **这不是伪代码**：内容包 #1 的真实实现就在 `src/NekoClicker.Content.Cafe/HappinessModule.cs`，
> 它把幸福感写进 `Counters["happiness"]`，由
> `UnlockCondition.Counter("happiness", n)` 与 `Scaling(ScalingSource.CustomCounter, ...)` 消费。
> 想照着做一个"士气""信仰""被阅读度"，复制它改两个常量即可。

**要点**：

| 事项 | 说明 |
|---|---|
| `OnTick` 频率 | 固定步长（`GameBalance.TickRate`），不是渲染帧率 |
| 状态存哪 | 优先 `GameState.Counters` / `Metadata`——它们会自动存档、且跨转生保留 |
| 离线 | 必须实现 `OnOffline`，否则派生状态在离线期间凭空落后。**只有实际发放离线收益时才会调用**（`GrantOfflineProgress = false` 时不调用） |
| 驱动修饰符 | `Scaling(ScalingSource.CustomCounter, perUnit, Id: "happiness")` |
| 作为解锁条件 | `UnlockCondition.Counter("happiness", 500)` |
| 与内容包的边界 | 内容包只提供数据；模块可以提供行为。**核心引擎永远不依赖具体模块** |

## 11. 纪元：把"重开"变成一条链

普通的转生是**循环**（重开 → 变强 → 再重开）。纪元（`Era`）把它改成**链**：
每一层有各自的完成条件，条件不满足时"舍一命"按钮是灰的，玩家不能跳过任何一层。

```csharp
builder.AddEras(
    new EraDefinition
    {
        Index = 1,
        Id = "nine_01",
        Name = "第一命 · 纸箱纪元",
        Theme = "从一只纸箱开始",
        Icon = "📦",
        EntryText = "你睁开眼，世界是纸箱做的。",
        ExitText = "你把最后一条小鱼干留给下一只猫。",
        Completion = UnlockCondition.EarnedThisRunAtLeast(1e5),   // 必须单调
        Balance = new GameBalance { ClickBasePower = 2,           // 本层可以换规则
                                    GoldenCookieMinDelay = 60 * 8 },
        Modifiers = [Modifier.GlobalMultiplier(1.2)],             // 本层常驻倍率
        InheritBuildingRatio = 0.0,                               // 进入本层的保留比例
    },
    /* …… 层号必须从 1 连续递增 …… */);
```

**规则**（构建期会替你拦住大部分错误）：

| 事项 | 说明 |
|---|---|
| 层号连续 | 从 `1` 开始、无空洞。断链会让"逐级推进"失去意义，构建期直接报错 |
| 完成条件**必须单调** | 白名单：累计赚取、成就数、点击数、金猫数、已购升级、游玩时长、计数器、标签升级数、图鉴条数。**禁止**：当前小鱼干、每秒产量、建筑数量、转生徽章、层号本身 |
| 为什么单调 | 这个条件会一直显示在按钮上。指标一旦能下降，玩家就会看到进度倒退、灰按钮闪烁 |
| `InheritBuildingRatio` 的语义 | 写在**目标层**上，表示"进入这一层时允许带进来什么"。不要写成"离开上一层时带走什么" |
| 闸门只有一个 | UI 上不要另做"下一层"按钮。一个灰按钮 + 一行原因，玩家就懂 |
| 层与内容的对应 | `UnlocksBuildings` / `UnlocksUpgrades` 只是**自查与展示用**，真正的门控写在各自定义的 `Unlock` 里 |

> **验收**：内容包新增纪元后跑 `.\tools\build.ps1`，其中 `G4_RobotWalksFromTheFirstEraToTheLast`
> 会让机器人从第一层一路走到最后一层。这条测试**证明的是内容在真实曲线下走得完**，
> 而不只是"代码能跑"——#2 就是被它抓出"每层门槛 ×1000 导致第 4 层之后走不动"的。

## 12. 图鉴：把叙事切成几十段放

世界观一次讲完就浪费了。做法是把剧情切成几十条小条目，每条挂一个释放条件，
于是故事随进度自然渗出——`UnlockCondition` 在这里不是门控工具，而是**节奏编排工具**。

```csharp
builder.AddStorylines(new StorylineDefinition
{
    Id = "door", Name = "两界之门", Theme = "它一直开着", Icon = "🚪",
    TotalEntries = 20,          // 声明总数，图鉴显示 3/20
});

builder.AddLore(new LoreEntry
{
    Id = "door_01", Title = "门在厨房后面", StorylineId = "door", Order = 1,
    Icon = "🚪",
    Body = "你以为是储藏间。推开门的时候，风是从另一边吹来的。",
    Reveal = UnlockCondition.ClicksAtLeast(1),
    Channel = LoreChannel.Popup,     // 转折点才用弹窗
});
```

**投放通道**（`LoreChannel`）：

| 通道 | 行为 | 用在哪 |
|---|---|---|
| `Log` | 进通知栏，最轻 | 默认；补充设定 |
| `Popup` | 弹窗，需要点掉 | 只给转折点 |
| `Codex` | 只进图鉴，不打扰 | 藏在后面的伏笔 |
| `EraText` | 不在此投放，走 `EraDefinition.EntryText` / `ExitText` | 层与层之间 |

**要点**：

| 事项 | 说明 |
|---|---|
| `TotalEntries` 是**声明值** | 构建期会校验它与实际条目数一致，防止"改条数忘了改声明" |
| `Order` 同线内不得重复 | 构建期校验 |
| 正文是静态文本 | 不支持 `{amount}` 占位符。**写完一条只讲一个信息点**，40~120 字 |
| 藏一半 | 未解锁条目在图鉴里显示 `???` + 条件进度（如 `4%`），既是悬念也是长期目标 |
| 前 10 分钟 ≤ 3 条 | G5 验收项。首轮咖啡馆释放了 6 条被测试抓出——**开场别把孩子一次放完** |
| 多条线要并行起步 | 九命首轮只释放 1 条，因为三条线的开场条件挤在同一处；把 3 个开场条目分别挂到点击 1 / 25 / 100 次上就解决了 |
| 消耗方式 | `UnlockCondition.LoreAtLeast(n)` 做门控，`Scaling(ScalingSource.LoreCount, ...)` 做成长 |

### 12.1 条件编排：三条硬规则

叙事条目写得好不好是文笔问题，**能不能被按顺序读到**是条件编排问题。这两件事互不相干，
而后者非常容易写错——错法还都很隐蔽，因为**图鉴不会报错，它只会显示得很难看**。

| 规则 | 为什么 |
|---|---|
| **任何两条的 `Reveal` 不得相同** | 同一个条件必然在同一瞬间一起解锁。首版 50 条里有 23 条落在 10 个重复组里，最坏 3 条同时弹；九命那边最坏 **6 条同时**。玩家看到的是"一口气倒出六段剧情"。<br>写条件时别挑圆整数：`1e9` 会被挑中三次，改成 `9e8` / `1.1e9` / `1.3e9`。**看着别扭是故意的。** |
| **每条线要有单调递增的骨架** | 同一条线内，门槛必须随 `Order` 递增，否则图鉴里会出现"第 3 条还锁着，第 4~9 条已经亮了"。<br>多指标混排必然出错：成就与建筑类里程碑全在开局一小时内触发，赚钱类要到几小时后，交替排列就倒挂。**用累计赚取当骨架定序，把建筑 / 成就 / 点击门槛降级成"顺手满足"的附加条件。** |
| **转生类条目只能放线的尾部** | 转生 / 店休的时机由**玩家**决定。`PrestigeLevelAtLeast(1)` 夹在赚钱类条目中间时，先攒钱后店休的玩家就会看到倒挂。尾部内部再用转生等级递增定序。 |

> **实测**（改前 → 改后）：九命 46 条 → 50 条，撞车组 10 → **0**，单点最多释放 6 条 → **1 条**，
> 线内倒挂 0 → 0，23.8 游戏小时读完全部 50 条。咖啡馆 50 条 → 51 条，撞车组 10 → **0**，
> 线内倒挂 **11 → 0**，生成"已读集合是前缀"（无空洞）。

**手法本身不复杂，难在它是"沉默失败"**：写错了测试不会红（G5 只管开局 10 分钟），
只有实际玩到那一段才看得出来。所以改完一定要**实测释放时间线**——把
`--panel codex` 的帧在若干时间点采样，看 `✓` / `🔒` 的分布；或者按上面的规则临时插桩
打印每条的首读时刻。**靠推理断言顺序是行不通的**，咖啡馆那 11 处倒挂就是"看起来没问题"写出来的。

### 12.2 后盘阈值：先量包络，再设值

**推论不出"这个门槛要多久才能达到"。** 数值曲线是复利的，看一眼数字觉得"挺合理"的
`3e14 + 转生 5 级`，实测是 **200 小时也读不完的死内容**。

所以给后盘条目设门槛之前，先跑一条长曲线把包络量出来：

```powershell
# 长跑并打印"历史累计 / 单轮赚取 / 转生等级"的推进曲线（临时插桩，量完删掉）
.\tools\build.ps1
```

咖啡馆的实测包络（贪心店休策略）：

| 游戏时长 | 转生等级 | 历史累计 | 单轮赚取峰值 |
|---|---|---|---|
| 20h | Lv1 | 1.0e12 | 1.6e9 |
| 60h | Lv2 | 9.1e12 | 1.1e12 |
| 80h | Lv3 | 2.7e13 | — |
| 120h | Lv3 | 5.6e13 | 2.9e13 |

两个结论都不是设计推出来的，是量出来的：

1. **转生等级是历史累计的纯函数**，不是"店休了几次"——`floor((allTime/1e12)^(1/3))`。
   想当然地以为"多店休几次就能到 Lv5"是错的：Lv5 要历史累计 1.25e14，约 180 小时。
2. **贪心店休会封住单轮赚取的上限**（每能升级就重置本轮），所以"高等级"和"单轮高产出"
   这两个条件如果同时要求，实际会比预期晚得多。

**规则**：后盘阈值一律设在实测包络之内；设完必须再跑一次复验读到多少条。

### 12.3 跨包互文：单向引用

十个内容包共用一套神话，但**每个包必须能独立玩**（这是框架主张"换内容包即换游戏"的前提）。
所以互文只能是单向的：A 包可以引用神话设定，但**不得要求玩家装了 B 包**。

现成的接口就在数据里：九命的第 2 层叫**「咖啡馆纪元」**，它的 `EntryText` 写的是
"门后是一家还没开张的店。招牌上缺一个名字"——而这正是咖啡馆包 `door_10`「第一块招牌」的情节。
两条线本来就在讲同一件事，只是谁也没提谁。**写互文时先去纪元表和别的包的 `Theme` 里找，
通常一半的桥已经造好了。**

## 13. 选择、立场与结局

一次**选择**（`ChoiceDefinition`）是一个需要玩家表态的时刻：条件达成后进入待答队列，
玩家作答前**不产生任何效果**（R6：选择不阻塞，可以一直放着）。每条**立场**
（`StanceDefinition`）是一个价值取向，累加权重；权重最高者成为**主导立场**，
它的修饰符计入产量。

```csharp
builder
    .AddStances(new StanceDefinition
    {
        Id = "divine", Name = "神性", Icon = "👁️",
        CostText = "产量 ×1.25，但金猫频率 ×0.75。",
        Modifiers = [Modifier.GlobalMultiplier(1.25), Modifier.GoldenCookieFrequency(0.75)],
    })
    .Add(new ChoiceDefinition
    {
        Id = "choice_name", Speaker = "她", Prompt = "要不要给我起个名字？",
        EraId = "life_cafe",                                  // 硬门：只在这一层出现
        Trigger = UnlockCondition.All(
            UnlockCondition.EraAtLeast(2),
            UnlockCondition.EarnedThisRunAtLeast(3.6e6)),
        Options =
        [
            new ChoiceOption
            {
                Id = "name_write", Label = "写上去。", OutcomeText = "她念了两遍。",
                StanceId = "divine", Weight = 2,
                Modifiers = [Modifier.GlobalMultiplier(1.05)],
            },
            new ChoiceOption { Id = "name_blank", Label = "先空着。", OutcomeText = "她留下一个爪印。",
                StanceId = "cat", Weight = 2, Modifiers = [Modifier.GoldenCookieReward(1.05)] },
        ],
    })
    .AddEndings(
        new EndingDefinition { Id = "end_god", Name = "成神", Text = "……", Priority = 0,
            Condition = UnlockCondition.StanceWeight("divine", 5) },
        // 兜底：不依赖任何表态，回避选择的玩家也走得到
        new EndingDefinition { Id = "end_blank", Name = "无人再读", Text = "……", Priority = 100,
            Condition = UnlockCondition.EraAtLeast(9) });
```

### 13.1 四条硬规则

| 规则 | 为什么 |
|---|---|
| **每个选项都要在数值上留痕** | 设计文档"三条件"里的**有代价**。只把代价放在"主导立场"上会延迟结算，于是前几次表态在数值上完全无感，玩家做决定时没有分量。**两层叠加**：选项当场生效 + 该立场成为主导后再叠一次 |
| **挂了 `EraId` 的选择，层内门槛必须 ≤ 该层的完成门槛** | `EraId` 是**硬门**，而"本轮累计赚取"在舍命时归零。门槛高于完成要求的话，玩家会在够条件前舍命走人，这个选择**永远**遇不到、那条立场永远攒不满、那个结局永久不可达。构建期强制校验 |
| **必须留一个兜底结局** | 构建期要求至少一个结局的条件"不依赖表态、不含取反、只引用单调不减的指标"。否则回避表态或进度不足的玩家会走完主线却没有任何结局成立 |
| **立场 id 是内容词汇，不是引擎枚举** | 核心不得出现内容 id（A1）。换包只改内容——3C 的实验室要用"道德"、公司要用"劳资"，核心一行不动 |

### 13.2 结局的两条性质

- **互斥靠 `Priority`**：判定时按优先级升序取第一个满足条件的，记下之后就再也不判。
  立场权重与图鉴条数都单调不减，达成后会一直成立，所以必须"判一次就停"。
  更稳的做法是让互斥**在构造上成立**——九命就是这样：每次表态在互斥选项间二选一，
  每条立场机会数固定，门槛卡在"必须每次都选它"。
- **不重置存档**：只写 `Counters["ending_<id>"] = 1` 与 `EndingsReached`。

> 结局成就不要用 `Never` 或 `Custom` 去绕。写 `Unlock = EndingReached("end_god")`——
> 条件树已经能表达"达成了某个结局"，引擎不需要为结局加特判。

## 14. 复查叙事与纪元节奏

改完叙事或纪元后，除了 §9 的三步，再多做两步：

```powershell
# 看前 10 分钟释放了几条（G5 要求 ≤3）
.\tools\play.ps1 --package cafe --simulate 600 --auto --no-color

# 看整局的纪元推进与图鉴收集情况（报告末尾有「纪元」与「图鉴」两节）
.\tools\play.ps1 --package ninelines --simulate 172800 --auto --no-color

# 直接截图图鉴面板，肉眼确认 ??? 遮蔽与进度百分比
.\tools\play.ps1 --package cafe --simulate 5400 --auto --frame 118x32 --panel codex --no-color
```

无头报告里 `── 图鉴 ──` 一节会按线列出 `已解锁 / 声明总数` 与进度条，
外加"待点掉的弹窗"数量；`── 纪元 ──` 一节会列出**当前层**的主线、进度、下一层与本层规则，
`── 舍命 ──` 一节给出舍命次数与若现在舍命的收益。**这两节是内容调参的主要反馈回路**：
条目挤在一起、某层进度推进异常慢，都会在这里一眼看出来。

> 想看**逐层耗时**（例如排查"第 5 命 19 小时凸起"），看 `── 舍命 ──` 的"舍命次数"
> 配合不同 `--simulate` 时长做二分即可——目前报告只展开当前层，没有逐层历史表。

