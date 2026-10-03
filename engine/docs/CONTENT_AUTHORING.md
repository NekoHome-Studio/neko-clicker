# 内容作者指南

本文说明如何为这套框架写内容。所有示例都取自 `engine/content/Neko`。

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

> ⚠️ 上面的 `Name` / `Description` / `Icon` **从 2026-10-02 起不写在 C# 里**——它们住在
> `text.json` 的 `buildings` 节，代码里写 `Prose.Text("buildings", "<id>", "name")` 这样取（见 §12.0.1）。
> 这里保留字面量写法，是为了让**数值与条件**那几行一眼看得全。

**相邻层的两个倍率决定整个游戏的节奏**（这是 Cookie Clicker 的核心配方）：

| 倍率 | 参考值 | 作用 |
|---|---|---|
| 价格倍率 | **×10 ~ 13** | 太大 → 每层只买得起一两个，节奏断裂 |
| 产量倍率 | **×6 ~ 8** | 太小 → 新建筑不值得买；太大 → 老建筑瞬间报废 |

价格涨得比产量快，意味着**单看"每块钱买多少产量"，越往后越差**。
这听起来反直觉，但正是它让游戏成立：由于价格随持有量指数增长，
当你有 100 个第一层建筑时，它的边际成本已经高得离谱，
此时新建筑的第一份才是真正的性价比之王。

`engine/tests/ContentTests.cs` 里有一条测试专门守住这个区间，
改数值时它会告诉你有没有把曲线改坏。

**解锁条件建议用「本轮累计赚取」而不是「当前持有」**：玩家在快买得起时就能看到下一层，
形成"再攒一点就解锁"的牵引感；转生后重新逐层揭示，也避免开局被一长串灰色条目淹没。

## 3. 升级：五种写法覆盖绝大多数需求

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
            Category = UpgradeCategories.ForBuilding(building.Id),   // ← 见 §3(e)
            Tier = required,
        };
```

内容量上去之后，**这是唯一能维护下去的写法**：给建筑表加一行，强化档自动出现。

### (e) 建筑专属升级：这条线**有家**

`Category = UpgradeCategories.ForBuilding(id)`（就是 `"building:<id>"`）是"这一条属于哪座
建筑"的唯一通道，10 个包 312 条都在用它。它现在**被校验、被索引、也被界面用上了**：

| 环节 | 行为 |
|---|---|
| 构建期 | 建筑 id 为空 / 建筑不存在 / 分类说属于 A 而修饰符作用在 B → **抛 `GameContentValidationException`，点名升级 id 与那个建筑 id**（与"修饰符引用了不存在的建筑"同一条规矩） |
| 索引 | `GameContent.UpgradesForBuilding(id)` / `UpgradesByBuilding`；轨内**按 `Tier`、同档按声明顺序** |
| 快照 | `BuildingView.UpgradeIds`（**服务端算好的 id 列表**，前端不解析前缀） |
| 界面 | 挂在那一座建筑的 **⬆** 上（与 📖 并列；徽标是"现在买得起几条"）。**扁平的「升级」面板不再重复渲染它们**，并会写出一行"这里 N 条挂在各座建筑上" |

三条硬规则：

1. **分类与效果必须指向同一座建筑**。分类说属于 A、`Modifiers` 却写在 B 上，构建期就红
   ——玩家会在 A 名下看到它、买到的却是 B 的效果，而两端都不会报错。
2. **效果是全局的，不算违规**（"这座建筑的培训提升了所有人"是合法创作）；违规的是
   "作用在**别的**建筑上"。
3. **`Tier` 是轨内的排序键**。同一座建筑的几档要按 `Tier` 递增写（今天 11 个包的声明顺序
   与 `Tier` 顺序刚好一致，所以这是把既有事实写下来）。

> 想要"每座建筑一条可以反复买的等级线"？**不用加任何核心机制**：
> `MaxPurchases = 20` + `PriceGrowth = 2.5` + `Modifiers = [Modifier.BuildingMultiplier(id, 1.5)]`
> 就是 20 级、越买越贵、每级 ×1.5（`ModifierResolver` 按购买次数重复计入）。
> 这是**内容**决定，不是框架缺口；见 [BUILDING_UPGRADES_PLAN](BUILDING_UPGRADES_PLAN.md) §3。

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

### (f) 点击线：固定档 + 一条「点击 × 建筑」的桥（2026-10-03 起；11 个包各一条）

点击线的**固定档**是这套内容的既有约定：`ClickFlat(1)`（十几下点击）→ `ClickMultiplier(2/3/4)`
（按点击数解锁）→ 天堂/前世线里的 `×5`/`×6`。它们**不需要** `Scaling`，也不该加：
`clickBase = ClickBasePower + 总cps × ClickCpsRatio` 之后才乘百分比与乘法，
所以**每一个点击倍率都在乘"cps 那一项"**——点击收益本来就随 cps 一起涨
（末局 `click/cps` 实测 0.02~1.20）。也就是说这条线上"后期点了没用"不成立，
缺的从来不是数值。

桥补的是**形态**：让点击收益跟着"你养了多少东西"走。

```csharp
Modifiers =
[
    new Modifier(
        ModifierTarget.ClickPower,
        ModifierOperation.AdditivePercent,
        0,                                                     // 基础值：效果全部来自成长
        new Scaling(ScalingSource.TotalBuildings, 0.005,       // 每座建筑 +0.5%
                    Cap: 120)),                                // 上限：最多**计入** 120 座（+60%）
],
Unlock = UnlockCondition.All(
    UnlockCondition.TotalBuildingsAtLeast(80),                 // ← 门槛压在建筑上，不是点击数
    UnlockCondition.UpgradeOwned("<本包点击线第 2 档 id>")),
```

四条规矩（`engine/tests/ClickBridgeTests.cs` 逐条守着）：

1. **`Cap` 必设**，而且要想清楚它限的是**原始计数**（"最多计入 120 座"），不是加成结果。
   不设就是"建筑越多点击越强"，后期一个升级吃掉整条曲线。
2. **门槛用"持有建筑总数"，不要用点击数**。用点击数就只是"固定档的第 N 档"，
   跟"桥"这个用途无关；而 `TotalBuildings` 还会在每层纪元重新长一遍，
   所以它是"这一层里重建得越多、这一下越重"，天然跟着层内的节奏走。
3. **幅度要小、要按实测包络压门槛**。门槛必须在**最小的那个包络**之下留余量：
   一局自然游玩里的建筑总量下界是公司包 **133 座**（其余 8 个纪元包 374~2230，
   两个经典包 6 小时 526/1112），所以门槛取 80。幅度取 +60% 上限——
   实测九个纪元包的"到结局小时"改前/改后最大只差 **0.05 游戏小时（≤1%）**。
4. **描述里的数必须与代码是同一组**。"每座 +0.5%、最多算 120 座、即 +60%"这句话
   没有任何机制保证它跟 `PerUnit` / `Cap` 一致——所以守卫逐字比对这三个数。

> 想要更强的点击线？先看 [OPEN_WORK](OPEN_WORK.md) §0.16 的消融实验：机器人的节奏对点击收益极度敏感
> （把点击收入扣成 0，**没有一个包能在 30 游戏小时里走到结局**）。
> 加之前先想清楚"这是给谁的"——以及为什么"点击占 cps 的比例本身"要动 `engine/core`
> （`ClickCpsRatio` 不是修饰符目标），那是另一类改动。

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
| `PrestigeDivisor` / `Exponent` | 1e12 / 1/3 | 转生公式；1 兆 = 1 级。**别照抄——见 §7.1** |
| `MaxBulkBuy` | 100000 | `BuyMax` 单次上限，防止极端数值下的求解抖动 |

### 7.1 转生除数要照着自己包的阶梯标定，不能照抄 1e12

`PrestigeDivisor` 决定"多少历史累计换 1 级"，而永久升级（`Persistence = Permanent`）只能用
转生货币买。**所以这两个数必须一起标定，否则整条永久线就是死内容**——而且死得很安静：
它只是永远不亮，运行期没有任何报错。

两个必须一起看的机制：

| 机制 | 含义 |
|---|---|
| 等级只在**舍命那一刻**结算 | 非纪元包随时能转生，是**可重复的循环**；纪元包一局只有那几次结算，**最后一次之后就再也不会换** |
| 转生除数决定**可结算**的历史累计 | 纪元包的收益被自己的阶梯卡住，除数定高了，等级恒为 0 |

判据（阶段 4C 定的，有守卫守着）：

- **纪元包**：一次自然游玩结算出的转生货币，必须 **≥ 整条永久线的总价**。
  实测方法就是跑一次机器人，读 `State.PrestigeChips`——守卫
  `PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun` 每次都替你跑。
- **非纪元包**：包络是开放的（买不完可以再转生几次），所以不套这条判据。

标定手法：先量出"最后一次结算时的历史累计" `A`，想要 `L` 级就取
`PrestigeDivisor = A / L³`（因为 `LevelFor = floor((A/D)^(1/3))`）。
阶段 4C 取 `L ≈ 100`，然后把永久线总价压到 80 上下——留出余量，
因为 `MetaRewardMultiplier < 1` 的层会把实发货币打折。

> **反面教材**（真实发生过）：七个包全抄 `1e12`。实测五个纪元包的结算货币全为 **0**
> ——「前世技能 / 前世经验 / 余烬 / 批注」全部拿不到。修法见 ROADMAP 阶段 4C。

### 7.2 计数器的显示名要登记

计数器（`GameState.Counters`）的键是内部标识：`readership`、`morale`、`faith`……
而它会出现在三个玩家可见的地方：

- `UnlockCondition.Counter("readership", 4000)` → 升级的解锁提示
- `Scaling(ScalingSource.CustomCounter, …, Id: "readership")` → 升级效果与「本层规则」
- 成就描述（手写的，容易漏）

不登记的话渲染出来是「每点「readership」 +0.01%」。在模块里登记一次即可：

```csharp
public void Configure(GameContentBuilder builder)
    => builder.AddCounterName(CounterKey, "被阅读度");
```

渲染层（`Scaling.Describe` / `UnlockCondition.Describe`）会自动走这张表；
没登记的键回退成键本身，旧内容不会崩。守卫
`ContentTests.CounterNames_AreRegisteredForEveryReferencedCounter` 会横扫**全部内容包**，
要求"引用到的每个计数器都登记过"，并且**真的渲染一遍**，断言输出里既含显示名、又不含内部键。

## 8. 常见坑

- **`Id` 是存档键**。发布后改名 = 老存档丢内容。要改名就写 `ISaveMigration`。
- **`Scaling` 记得设 `Cap`**，否则后期一个升级就能吃掉整条曲线。
  但**`Cap` 限的是原始计数值，不是加成结果**：`Scaling.Apply = base + PerUnit × min(计数, Cap)`。
  所以"每座建筑 +2%，最多 +200%"要写成 `PerUnit = 0.02, Cap = 100`
  （0.02 × 100 = 2.0），而不是 `Cap = 200`（那会给到 +400%）。
  描述文案最好把两个数都写清楚，例如咖啡馆包的"每座「猫爬架」让全部产量 +1%（最多 200 座）"——
  **把 Cap 说成"最多多少座 / 多少点"，而不是"最多 +百分之多少"**，就不会再有歧义。
  4B 实现 #9 时就是在这里踩的坑：`Cap: 300` 让 300 点被阅读度就顶到上限，
  衰减与清零全部失去意义；而它的端点断言（0 → ×0.5、2 万 → ×1.0、6 万 → ×2.0）把它抓了出来。
  **给成长型修饰符写一条端点断言**，是这类误读唯一可靠的防线。
- **`Scaling` / 修饰符引用的 id 必须真实存在**。写错建筑 id 或增益 id 在构建期就会报错——
  这类错误以前是静默失效（修饰符算了但没人受影响），现在拦在构建期。
- **`Category = "building:<id>"` 引用的建筑同样必须真实存在**，而且**分类与修饰符要指向
  同一座建筑**（§3(e)）。这两个以前也没人查：分类写错只会让那条升级在"建筑自己的升级"
  里永远不出现，而两端都不会报错。写成 `"building:"`（前缀后面没 id）也一样报错。
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
- **中文文案里的引号写「」，不要写 ASCII 的 `"`**。C# 字符串字面量会被它截断，
  而报错要到编译期才出现（指着一行看起来完全正常的中文）。
  实测在 40 条叙事里连踩两次，两次都是「看起来完全正常」的那一行。

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

### 内容改动会不会碰到公开 API 守卫？

**正常写内容包不会。** 你只是在调用 `NekoClicker.Core` 的公开 API，
不修改它的表面，所以 `PublicApiTests` 不会因为你的内容改动而红。

只有两种情况会牵动版本号（详见 [VERSIONING.md](VERSIONING.md)）：

1. **你觉得框架少了个能力，想往核心里加一个公开成员**。这是 ROADMAP 里 A3 想拦的事：
   先停下来问"这能不能用已有的接缝表达"。#6 末世（跨转生继承）与 #9 图书馆（虚无化）
   两个"专属机制"最后都是零核心改动落地的，值得先照这个方向想一遍。
   确实非加不可时，那是 **minor** 版本，走 VERSIONING.md 第 4 节那七步。
2. **你想改 `NekoClicker.Core` 里已有的公开成员**。这是 **major**，
   而且会让全部十一个内容包一起进入需要回归的范围——先确认真的没有别的路。

顺带一提：正因为内容包**真的**只依赖公开 API，上面那句"会让全部十一个包进入回归范围"
才是可测量的。这就是本项目"核心零内容知识"主张的代价与证据。

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

> **这不是伪代码**：内容包 #1 的真实实现就在 `engine/content/Cafe/HappinessModule.cs`，
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

**一个容易踩的坑：模块改了计数器，产量并不会自动变脏。**
`GameEngine.Step` 的顺序是「先 `RecomputeIfDirty()` → 再结算生产 → 最后才 `module.OnTick`」，
所以模块在 tick 里改计数器时，`ModifierResolver` 已经在这次结算中用过了。
前三个第二资源模块（幸福感 / 伦理值 / 士气）都是单调、缓慢的，靠"玩家总在买东西"
把它盖住了；**如果计数器是会掉的（#9 的被阅读度），这个延迟就会变成看得见的错误**。

正确的做法是**跨过一个量子才 `MarkDirty()`**——既不要每秒都重算（那会把
`ModifierSet`"生命周期与状态变更绑定"的设计直接毁掉：30fps × 长跑 = 上千万次全量重算），
也不要一次都不重算：

```csharp
bool crossedQuantum = (long)(current / DirtyQuantum) != (long)(next / DirtyQuantum);
engine.State.SetCounter(CounterKey, next);
if (crossedQuantum) engine.MarkDirty();      // 200 点的量子 ≈ 量程的 0.2%
```

验收方式见 `LibraryContentTests.Readership_RefreshesProduction_WithoutAnyOtherEvent`：
在"只买一次、之后什么都不做"的窗口里断言产量确实跟着计数器走——
**如果窗口里还有别的脏源（金猫刷新、成就解锁），这条用例就失去判别力了**，
所以它把窗口压在第一个金猫到来之前，并且先把首批成就放掉。

另一个只对"会掉"的计数器成立的注意事项：它**不能进纪元完成条件**。
构建期只拦得住明令禁止的指标（`Cps` / 建筑数 / 当前货币），计数器在白名单里靠内容自觉。

> **这条自觉现在有守卫兜底**：`EraTests.EraGate_ProgressNeverGoesBackwards` 对"完成条件里引用了
> **内容计数器**"的包各跑一段真实游玩，断言同一层之内进度只进不退、舍命按钮不闪。
> 它会自动扫到新包（判据是内容里的条件树，不是手写清单）；判别力由故障注入用例
> `MonotonicGuard_RejectsADecayingCounterInCompletion` 证明——那条用例合成一个"完成条件挂在
> 一个只会掉的计数器上"的包，断言守卫真的会红。

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

> ⚠️ 上面那六个**文案**字段（`Name` / `Theme` / `Icon` / `EntryText` / `ExitText` / `CompletionHint`）
> **从 2026-10-03 起不写在 C# 里**——它们住在 `text.json` 的 `eras` 节，代码里写
> `Prose.Text("eras", "<id>", "entryText")` 这样取（见 §12.0.2）。这里保留字面量写法，
> 是为了让**层号、条件树、数值规则**的写法一眼看得全。

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

### 11.1 继承：跨层保留建筑（#6 末世用的能力）

`InheritBuildingRatio`（按比例）与 `InheritBuildings`（白名单，**该 id 原样全留**，
其余仍按比例）都写在目标层上。默认 0 = 全清，全项目只有 #6 末世非 0。

**开继承之前必须先想清楚一件事：解锁条件用哪个累计。**

| 条件 | 不继承的包 | 继承的包 |
|---|---|---|
| `EarnedThisRunAtLeast` | ✅ 推荐，每层重新爬一遍 | ⚠️ 重启会归零 |
| `EarnedAllTimeAtLeast` | 可用，但会让后期层失去"重新解锁"的节奏 | ✅ 推荐 |

原因是继承会打破一条**原本空成立的隐含不变量**：`拥有 ⇒ 已解锁`。用「本轮累计」时，
重启之后你手上那 50 座建筑会显示成「未解锁」——产量照算，但列表里看不到、也卖不掉。
这不是显示 bug，是**内容侧选错了指标**：她既然还记得怎么造，重启之后就该还是会的。

```csharp
// ❌ 重启归零 → 继承来的建筑变成"拥有但未解锁"
Unlock = UnlockCondition.EarnedThisRunAtLeast(500_000)

// ✅ 知道就不会忘
Unlock = UnlockCondition.EarnedAllTimeAtLeast(500_000)
```

**继承还值得利用起来**：`PrestigeSystem.ResetRun` 之后、`OnAscend` 回调之前，
引擎已经把建筑按比例写回去了——所以模块在 `OnAscend` 里数一遍 `State.BuildingCounts`，
拿到的就是"这一轮继承了多少"。末世的「记忆残片」产率直接挂在这个数上，
于是"多攒点再重启"变成一个真实的取舍，而不是一句设定。

**两条容易写反的地方**：
- `InheritBuildingRatio` 是"**进入**本层允许带进来什么"，写在目标层上。
- 白名单不是"替换比例"，而是"这几个 id 全留，其余照旧按比例"。

### 11.2 阶段：一层之内分段推进（1.9.0，`EraDefinition.Stages`）

> **这一节是"阶段"这件事的唯一出处**：它是什么、怎么写、跨过它发生什么、
> 怎么验收，以及**它不发奖励的话，靠什么撑住更长的一层**。
> 背景与实测在 [TUNING_ANALYSIS](TUNING_ANALYSIS.md) §四（含 §4.9 对"一局重来几次"的决定记录）。

#### 11.2.1 它解决的是什么问题

纪元是"**重开**"的单位：过一层就清空本轮。玩家在 §四里的原话是
「舍命之后从头再来的次数太多了」。量出来的形状是——第一次舍命只占整局 **2%~33%**
（也就是 **67%~98%** 的时长发生在舍命之后），而 8/9 个包进层是**字面意义上的从零开始**
（0 座建筑、cps 0.0、可见建筑回到 1 座），第 2 层起 **43%~100%** 的建筑购买是"再买一次"。

所以问题不在"层数"这一个数上，还在**一层之内没有分段**：一层是一条又长又平的坡，
唯一的反馈是下一次重开。阶段就是把那条坡**切成几段并且说出来**。

#### 11.2.2 阶段是"给已有的门槛起个名字"，不是一套新进度

**没做**的事：没有新资源、没有新货币、没有新状态机、没有新的存档字段。
**理由**是：内容里**早就有**层内分段了。每一条挂在
`All(EraAtLeast(n), EarnedThisRunAtLeast(m))` 上的叙事 / 表态，以及每一座用「本轮累计」
解锁的建筑，都是一条隐式的阶段线（`EarnedThisRunAtLeast` 在 11 个包里出现 **337 次**）。
缺的从来不是机制，而是**把它说出来**——玩家看得见的分段、构建期能校验的声明、守卫能断言的结构。

于是阶段是**派生**的：当前阶段 = 声明过的边界里有几条已经成立，再加 1
（第 1 阶段 = 刚进这一层）。它**不占存档位**：

- 老存档读进来**天生带着自己的阶段进度**（指标本来就在存档里）；
- 存档格式与 `SaveSerializer.CurrentVersion` **一个字都不用改**，不需要迁移；
- 内容改版最多让提示少响或多响一条（见 11.2.5），**不影响任何数值与进度**。

唯一被记下来的东西是"这一层的阶段提示播报到第几段了"，它记在计数器
`$era_stage_<纪元 id>` 上（与 `$choice_shown_<id>` 同一个套路，**没有新增存档字段**）。
**按纪元 id 分键是必须的**：计数器跨层保留（`PrestigeSystem.ResetRun` 刻意不清空），
只存一个"最高阶段"会让第 2 层一进层就以为自己已经播报过第 4 阶段。

#### 11.2.3 怎么写

写在**本层自己的** `Stages` 上，顺序即推进顺序；**空是默认值**，表示这一层不分段——
没声明的包行为与 1.8.0 逐字节相同（有守卫钉住）。

```csharp
new EraDefinition
{
    Index = 2,
    Id = "series_a",
    Completion = SeriesACompletion,
    // 阶段 = "这一层里又能买到一座新建筑"的那些线，按门槛升序。
    // 条件取那座建筑的 Unlock **同一个对象**，不是照抄一个数字——两者因此不可能各自漂移。
    Stages = StagesWithin(SeriesACompletion),
},
```

公司包用的那个内容是纯内容的写法（`engine/content/Company/Eras.cs`，**核心一行没改**）：

```csharp
private static EraStage[] StagesWithin(UnlockCondition completion)
{
    double ceiling = Ceiling(completion);          // 完成条件里的「本轮累计赚取」
    return
    [
        .. Buildings.All
            .Select(b => (b, Threshold: Threshold(b.Unlock)))
            .Where(x => x.Threshold is { } t && t < ceiling)
            .OrderBy(x => x.Threshold)
            .Select(x => new EraStage { Id = x.b.Id, Name = x.b.Name, Icon = x.b.Icon, At = x.b.Unlock }),
    ];
}
```

| 事项 | 说明 |
|---|---|
| **A3 不变量** | 给一个包加阶段**不需要改任何核心代码**（公司包就是这么加的），也不会影响别的包 |
| `At` 必须单调 | 与纪元完成条件**同一张白名单**（累计赚取 / 成就数 / 点击数 / 时长 / 计数器…）；界面上「第 k/n 阶段」不许回跳，构建期会拦 |
| `At` 不能缺 | 缺省是 `Never`（那个阶段永远打不开）——构建期报错，而不是运行期静默 |
| 层内唯一 | `Id` 层内唯一，`Name` 必填（它就显示在提示里） |
| **边界必须低于完成门槛** | 高过它，玩家在够到之前就舍命走人了，那一阶段**永远到不了**——通用守卫扫全部内容包（同 `LoreTests.EraGatedLore_StaysBelowItsEraCompletion` 的理由） |
| 一段什么都"grant"吗 | **不 grant 数值**：见 11.2.4 |

#### 11.2.4 跨过一条边界，到底发生什么？（以及"更长的一层靠什么撑住"）

**什么都不清。** 建筑、升级、货币、计数器一律照旧——阶段不是重开，只是这一层里的一段路走完了。
跨过边界时，**确切地**发生三件事，一件不多：

1. **该解锁的内容解锁了**——但那不是阶段干的，是内容自己的 `Unlock` 到点了；
   阶段只是**把那条线变成了看得见的东西**（试点里就是"又能买到一座新建筑"）。
2. **界面上的分段前进一格**：`EraView.StageIndex` / `StageName` / `StageNextName` / `StageProgress`
   → Web 的「第 k / n 阶段 · 下一阶段：X（p%）」、终端顶栏的「阶段 k/n → X」。
3. **一条通知**（`EraSystem.CheckStage`）：「本层进入第 k/n 阶段：名字」。
   它**不参与任何判定**，与成就 / 叙事同频执行（`GameEngine.Step` 的检查块）。

**它不发任何数值奖励**——没有倍率、没有情感能量、没有永久进度。
这不是"忘了"，是**刻意的**，理由有两条：

- **规矩**：本仓库"先量再改"。而"每跨一段该给多少"**没有任何测量支撑**：
  谁也不知道给 `×1.1` 是恰好还是过头，给错了就把 §1 那张"难度跨度"表整个推歪。
- **它现在不需要**：这一版的阶段只对**公司包**生效，而公司包**本来就只有 3 层 = 2 次重开**
  ——正是玩家要的那个形状。也就是说：**这一版没有减少任何一次舍命，因此没有削弱任何一次
  情感能量注入**，`舍命 → 徽章 → 永久升级 → 下一层更快`这条正反馈**密度一点没变**。

**那"更长的一层"靠什么撑住？** 这是减层（把九命的 9 层折成 3 层）那一版**必须先回答**的问题，
而不是这一版可以含糊过去的。三条我们知道的、可辩护的东西：

- **今天的一层之内本来就没有奖励**。一层的奖励函数就是"有新东西能买 / 能读"，
  而 §四量出的是：第 2 层起要 **24%~69%** 的时长才第一次买到"上一层没有的"东西，
  很多后段层**整层都没有新的**。所以"阶段不发奖励"不是把奖励拿掉了——它把**已有**的
  那几个节点从"你自己看列表发现"变成"界面说出来 + 响一声"。
- **降层的代价必须由"段"来偿还，而且要用同一个尺子量**：一局的情感能量总量按
  `ChipsOnAdvance = max(0, level - PrestigeLevel) × MetaRewardMultiplier` 算，
  而 `level` 挂在**历史累计**上——所以把 9 层折成 3 层**不会**让总量归零，
  但它会把注入**次数**从 8 次压到 2 次。要做这一步，就必须先量"少了的这几次注入值多少"，
  再决定用"每段给一次"还是"每层给更多"补回来。
- **机制上已经够用了，不需要改核心**：`EraSystem.Stage(engine)` 与
  `$era_stage_<纪元 id>` 都是公开的，所以**内容包自己的模块**（`IGameModule.OnTick`）
  可以在跨段那一刻用**已有原语**给东西——`Modifier` / `State.Counters` /
  `Scaling(ScalingSource.CustomCounter, …)` / 直接 `Notify`。
  这正是把 `EraSystem.Stage` 做成公开 API 的原因：**让包自己决定，而不是让引擎替它定一个数。**
  真要让"每段给一次徽章"变成一句声明（例如 `EraStage` 上加一个奖励字段），
  那是一次**新的 additive minor**，而且要等上面那个量出来之后。

#### 11.2.5 玩家在哪里看到它（两个宿主）

| 宿主 | 位置 | 没声明阶段时 |
|---|---|---|
| Web | 纪元面板的一行：`第 2 / 5 阶段 · 下一阶段：外包基地（45%）`，原始门槛在 `title` 里 | 整行 `hidden` |
| 终端（Demo.Cli） | 顶栏 `阶段 2/5 → 外包基地`；无头报告多一行「本层阶段」 | 不出现 |

两条硬要求：

- **前端不许自己算阶段。** 门槛是内容的事，服务端把 `stageIndex` / `stageCount` /
  `stageNextName` / `stageProgress` 算好给它（与"前端不许解释枚举序数 / 字符串"同一条规矩）。
- **老引擎 + 新前端必须安全。** 宿主的 `wwwroot` 是**源码目录**，所以页面可能比引擎先更新；
  那时快照里根本没有 `stageCount` 这几个字段。前端必须把它当"没有阶段"处理——
  `tools/web-smoke.mjs` 里有一条专门钉这件事（§16）。
  `render()` 曾经因为一个字段的形状错而在中段抛异常、后面所有面板一次都没画出来过（`39503d6`），
  所以**这一段路径由无头用例覆盖**，不是靠肉眼。

增量协议那一侧：`stageProgress` 按 1% **量化**、`stageProgressText` **不进增量帧**
（`SnapshotProtocol` 的 `QuantizedFields` / `ExcludedFromDelta`）——不这么做，
`era` 会因为一个派生量在每一帧重发。有一条字节预算的用例守着（`IdleDelta_StaysSmallInBytes…`）。

#### 11.2.6 哪些包、每层几段（实测，2026-10-03）

**判据**：一段 = "这一层里又有一座新建筑能买了"。数法就是
"该层的 `Completion` 里那条「本轮累计赚取」门槛之下、有几座建筑的 `Unlock` 也是同类门槛"，
再加 1（开场那一段）。**这张表是候选值，不是已落地的值**——只有公司包落地了。

| 包 | 层数 | 每层段数（第 1 层 → 第 N 层） | 说明 |
|---|---|---|---|
| **公司** | 3 | **5 / 8 / 8** ✅ 已落地 | 就是这一版的试点；第 3 层最后那座 `headquarters`（1e9）高过本轮完成门槛 5e8，**不是**一段 |
| 实验室 | 7 | 5 / 7 / 8×5 | |
| 九命 | 9 | 6 / 8 / 9×7 | 第 9 层的完成只在 1.1 秒内发生（§4.2），9 段在那里没有意义 |
| 神明 | 5 | 5 / 7 / 8 / 9 / 9 | |
| 文明 | 5 | 5 / 7 / 8 / 9 / 9 | |
| 赛博 | 5 | 5 / 7 / 9 / 9 / 9 | |
| 梦境 | 5 | 5 / 9 / 9 / 9 / 9 | 第 5 层是本包最长的一层（314 分钟，占整局 87%） |
| **末世** | 5 | **判据给不出结果**（全是 1） | 它的建筑挂在**历史累计**上（§11.1），本判据只认「本轮累计」——要一条"全历史门槛落在哪一层"的规则才行 |
| **图书馆** | 5 | **同上** | 同上（它也是历史累计那一档） |

三件必须说清楚的事：

1. **这些数是"把每座新建筑都当一段"的上界。** 上界不等于该取的值：`9` 段的一层
   （神明 / 文明 / 赛博 / 梦境的第 4、5 层）意味着几乎每买一座建筑就"进一段"，
   响得可能太密。真要一个更小的 n，就是把相邻的几座**并成一段**——那是内容决定，
   而且要按包量（每段之间隔多久）。
2. **末世 / 图书馆要另一条规则**，不是"照抄"能解决的：它们的解锁门用的是历史累计，
   而阶段边界必须仍然**落在这一层之内**（否则第 1 层就会把第 5 层的线全宣布一遍）。
   一条可行方向是"进层那一刻已经成立的历史门槛不算，之后跨过的才算"——但那与
   **继承**（`InheritBuildingRatio`）交织（末世进层就带 15~183 座），必须先量。
3. **减层与分段是两件事，顺序不能反。** 玩家要的形状是"重来两次，但每层 n 个阶段"。
   公司包**已经是** 3 层 = 2 次重开，所以这一版直接把段加了上去；
   其余 5~9 层的包要减到 3 层，那会**重新切一遍每层的门槛**，
   于是 n 也会跟着变。所以：**先把"少掉的几次舍命值多少"量出来（11.2.4），
   再减层、再定 n**——反过来做会白量一遍。

#### 11.2.7 验收

```powershell
pwsh -File tools/build.ps1 -Strict     # 含 EraStageTests 15 条
node tools/web-smoke.mjs               # 含阶段那一行的 5 条（远端夹具是公司包）
```

`EraStageTests` 里每一条都对着一种**沉默**：

| 用例 | 守的沉默 |
|---|---|
| `Company_StagesAreExactlyTheBuildingsThatOpenWithinEachEra` | 内容里多/少一座建筑而阶段表没跟上；阶段条件与 `building.Unlock` 各自漂移 |
| `Company_StageCountsPerEra_AreFiveEightEight` | 文档里写的 5 / 8 / 8 不再是真值 |
| `PacksWithoutStages_HaveNoneAnywhere` | 没声明阶段的包被这次改动**牵连**（自动扫全部包） |
| `StageGates_StayBelowTheirEraCompletion` | 阶段门槛高过本层完成门槛 → 那一段永远到不了 |
| `StageGateGuard_RejectsAStageAboveItsEraCompletion` | **判别力**：合成一个坏包，证明上一条真的会红 |
| `StageIndex_FollowsTheDeclaredThresholds` | 派生算错（门槛一到却不算跨过、或提前跨过） |
| `Stage_NeverGoesBackwards_AndIsFinishedBeforeTheEraIs` | 阶段回退；或者有一条线在本层根本走不完 |
| `StageAnnouncement_FiresOncePerBoundary` | 不播报 / 重复播报 |
| `StageProgress_SurvivesALoad_AndIsNotReplayed` | 读档丢进度；读档补发一串通知 |
| `StageAnnouncement_IsScopedToItsEra` | 播报记录只存一个"最高阶段"，跨层串味 |
| 三条构建期用例 | 阶段没写条件 / 挂在会掉的指标上 / id 重复 |
| `EraView_CarriesTheStage_AndHidesItWhenThereAreNone` | 宿主拿不到它，或者没阶段的包多出一行 |

#### 11.2.8 明确**不做**的，与留给人的问题

**不做**（都在这一版之外，而且都不是"顺手就能加"）：

- **不减层、不改任何门槛与倍率**：这一版一行数值都没动（`git show --stat` 里没有一个数值）。
- **不给阶段发奖励**（理由见 11.2.4）——机制留给包自己用模块做。
- **不给末世 / 图书馆加阶段**（判据还不够，见 11.2.6 第 2 条）。
- **不动 `BuildingView.NextMilestoneAt`**。它是**单座建筑**的"再买 N 个解锁 X"提示
  （`GameViewFactory.FindNextMilestone`），与"整层分几段"是两件事，**语义一字未动**；
  界面上也刻意用两个词：那个叫「再买 N 个解锁 X」，阶段叫「第 k/n 阶段」。
- **不把阶段塞进 `EraGate`**（`Progress` / `BlockedReason` 的语义是"能不能舍命"，
  与"这一层走到哪一段了"无关）。

**留给人的问题**：

1. **每层该有几段？** 上界见 11.2.6；要不要压到 4~6 段、以及"并段"的判据是时间还是建筑数。
2. **减层与分段的顺序**：先减层（重切门槛）还是先把阶段铺满 9 个包？
3. **要不要"每段给一次"**（徽章 / 短暂倍率 / 永久进度），若要，给多少——
   这一条必须先量（11.2.4），量法沿用 §4.1 那条机器人。
4. **阶段要不要有"名字之外的叙事"**（每段一句话 / 一条图鉴条目）——现在是复用建筑名与图标。
5. **末世 / 图书馆那两包**的历史累计边界规则（11.2.6 第 2 条）。

## 12. 图鉴：把叙事切成几十段放

世界观一次讲完就浪费了。做法是把剧情切成几十条小条目，每条挂一个释放条件，
于是故事随进度自然渗出——`UnlockCondition` 在这里不是门控工具，而是**节奏编排工具**。

### 12.0 散文住在 `text.json` 里，代码只留结构（2026-10-01 起）

`Title` / `Body`（以及剧情线的 `Name` / `Theme` / `Icon`）**不写在 C# 里**，
写在内容包目录下的 `text.json`；代码里只留 id、条件、序号与通道。
十个包已经全部迁完，随便挑一个现成样板：
`engine/content/Cafe/Lore.cs` + `engine/content/Cafe/text.json`（51 条，真实规模）。

```csharp
// Lore.cs —— 散文从文件取，条件、序号、通道留在代码里
private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load("Cafe"));
private static ContentText Prose => ProseCache.Value;

public static StorylineDefinition[] Storylines => [Line("door", 20)];

private static StorylineDefinition Line(string id, int totalEntries) => new()
{
    Id = id,
    Name = Prose.Text("storylines", id, "name"),
    Theme = Prose.Text("storylines", id, "theme"),
    Icon = Prose.Text("storylines", id, "icon"),
    TotalEntries = totalEntries,      // 声明总数，图鉴显示 3/20
};

private static LoreEntry Popup(string id, int order, UnlockCondition reveal)
    => Make(id, order, reveal, LoreChannel.Popup);

private static LoreEntry Make(string id, int order, UnlockCondition reveal, LoreChannel channel)
    => new()
    {
        Id = id,
        Title = Prose.Text("lore", id, "title"),
        Body = Prose.Text("lore", id, "body"),
        StorylineId = "door",
        Order = order,
        Reveal = reveal,
        Channel = channel,
    };
```

```jsonc
// engine/content/Cafe/text.json —— 同一份 id 空间
{
  "storylines": { "door": { "name": "两界之门", "theme": "它一直开着", "icon": "🚪" } },
  "lore": {
    "door_01": { "title": "门在厨房后面",
                 "body": "你以为是储藏间。推开门的时候，风是从另一边吹来的。" }
  }
}
```

- **复制是自动的**：内容包目录里有 `text.json`，仓库根的 `Directory.Build.props` 就会把它
  复制到 `content/<目录名>/text.json`——引用这个包的项目（Demo / Web / 测试）都会拿到，
  publish 也带上。目录名就是 `ContentText.Load` 的参数。
- **三种沉默失败都会当场抛**，而不是变成一本空白图鉴：id 对不上（取文本时抛）、
  文件少一条（同上）、文件多一条（包在 `Build()` 末尾调 `Lore.VerifyAllTextUsed()` 时抛）。
- **改文案只改 `text.json`，不用重编**；改条件只改 `Lore.cs`。两边靠 id 关联。
- 守卫是横扫全部已外部化包的 `ContentTextFileTests`，其中一条专门盯着
  "有 `text.json` 的包必须都在守卫表里"（加了包忘加守卫是沉默的）。

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

### 12.0.1 建筑的 name / description / icon 也外置了（2026-10-02 起，icon 于 2026-10-03 补上）

§12.0 讲的是**剧情散文**（`storylines` / `lore`）。同一套机制现在也覆盖**建筑**：
`BuildingDefinition.Name` / `Description` / `Icon` 搬进了同一个 `text.json` 的根节 `buildings`。

- **形状**：`"<建筑 id>": { "name": …, "description": …, "icon": … }`——与 `storylines` / `lore`
  同一份 id 空间、同一份文件。
- **代码侧**：`Name = Prose.Text("buildings", "<id>", "name")` /
  `Description = Prose.Text("buildings", "<id>", "description")` /
  `Icon = Prose.Text("buildings", "<id>", "icon")`。
  **id 用建筑自己的 id**（也就是 `BuildingDefinition.Id`、存档键），不是序号。
- **`Icon` 一开始留在代码里，后来补搬了**：`storylines` 连 icon 一起外置、建筑却没有，
  这处不一致当时记成 `OPEN_WORK.md` 的未决项 L。**结论是"搬出去"**（一切玩家可见的东西都进文件），
  L 条已结案——`Icon` 不是"符号属于代码"的例外，它和剧情线的 icon 是同一类东西。
  九命那 12 座走的是工厂 `Make(id, price, cps, era:)`，图标同样按 id 从文件取。
- **仍然留在代码里的**：全部逻辑/数值字段（`BasePrice` / `BaseCps` / `PriceGrowth` /
  `Unlock` / `Category` / `Tags` / `HiddenUntilUnlocked` / `SellRefundRate`）与 **id**——它们是逻辑，不是文案。

#### ⚠️ 一个包只能有**一个** `ContentText` 实例

这条是**负载性**的，不是风格问题：`EnsureNoOrphans` 会遍历**整个文件的所有类别**，所以两个实例会
**互相把对方的条目报成孤儿**（`buildings` 说 `lore` 是孤儿，`lore` 说 `buildings` 是孤儿）。

- 有 `Lore` 的包：`Lore.Prose` 由 `private` 改为 `internal`，再 `Buildings.Prose => Lore.Prose`。
- 没有 `Lore` 的包（`Neko`）：自己持有一个 `Lazy`，并在 `NekoContent.Build()` 末尾调用
  `Buildings.VerifyAllTextUsed()`——否则没有 `Lore` 那条路径替它做孤儿检查。

#### 守卫

`ContentTextFileTests` 里与建筑有关的条目：

- `ExpectedBuildings`：**每包硬编码的建筑数**。这是唯一能发现"代码与 JSON **同时**删掉一座"的守卫
  （纯双向比对发现不了：两边都少了一座，看起来仍然一致）。
- `BuildingCountTable_CoversExactlyTheGuardTable`：新包**不可能忘记登记**期望数。
- `EveryBuilding_ResolvesItsTextFromTheFile` 逐座比对 **name / description / icon** 三个字段
  （代码值 == 文件值，两个方向）。

### 12.0.2 纪元的六个字段也外置了（2026-10-03 起）

`EraDefinition` 里**给玩家读**的六个字符串搬进同一个 `text.json` 的根节 `eras`：
`Name` / `Theme` / `Icon` / `EntryText` / `ExitText` / `CompletionHint`。

- **形状**：`"<纪元 id>": { "name": …, "theme": …, "icon": …, "entryText": …, "exitText": …,
  "completionHint": … }`。id 就是 `EraDefinition.Id`（存档与叙事引用用的那个）。
- **代码侧**：六项一律 `Prose.Text("eras", "<id>", "<field>")`——字段名的规则是
  "C# 属性名首字母小写"（`EntryText` → `entryText`）。
- **没搬的**：`Index`（层号，必须连续的校验依据）、`Completion`（条件树）、`Balance` /
  `Modifiers` / `MetaRewardMultiplier`（数值与规则）、`InheritBuildingRatio` /
  `InheritBuildings` / `UnlocksBuildings` / `UnlocksUpgrades`（比例、白名单、id 清单）。
  判据与 §12.0 同一条：**能进数据的只有散文**。
- **`CompletionHint` 特殊**：它在 C# 里的默认是空串，**空 ⇒ 界面回退成条件树的自动描述**——
  一条静默路径。所以搬过来时用 `Text(...)`（缺失即抛）而**不是** `TextOr`，
  守卫里也断言六项**都非空**。
- **`Neko` 与 `Cafe` 没有纪元**（连 `Eras.cs` 都没有），它们的 `text.json` 里**不许有** `eras` 分区：
  期望条数表里这两个包写 **0**，守卫按"期望 0 ⇒ 文件里也不许有"检查。
- **守卫**：`ExpectedEras`（写死每包层数）+ `EraCountTable_CoversExactlyTheGuardTable` +
  `EveryEra_ResolvesItsTextFromTheFile`（六字段双向 + 非空）+
  `EditingTheEraTextWrongly_FailsLoudly`（少一层 / 多一层都要响）。

### 12.0.3 结局 / 表态 / 立场 / 成就也外置了（2026-10-03 起）

第四轮把剩下四类"面向玩家的字符串"搬进同一个 `text.json`，用四个新的根节：
`endings` / `stances` / `choices` / `achievements`。判据仍是同一条：**能进数据的只有散文**。

| 分区 | 形状 | 代码侧 | 留在代码里的 |
|---|---|---|---|
| `endings` | `"<结局 id>": { "name", "icon", "text" }` | `Prose.Text("endings", id, "text")` | `Id` / `Priority` / `Condition` |
| `stances` | `"<立场 id>": { "name", "theme", "icon", "costText" }` | `Prose.Text("stances", id, "theme")` | `Id` / `Modifiers` |
| `choices` | `"<表态 id>": { "speaker", "prompt", "options": { "<选项 id>": { "label", "outcomeText" } } }` | 问句 `Prose.Text("choices", id, "prompt")`，选项 `Prose.Text("choices", "表态id/选项id", "label")` | 表态的 `Id` / `EraId` / `Trigger`；选项的 `Id` / `StanceId` / `Weight` / `Modifiers` |
| `achievements` | `"<成就 id>": { "name", "icon", "description" }` | `Prose.Text("achievements", id, "name")` | `Id` / `Unlock` / `Modifiers` / `Category` / `Tier` / `Hidden` |

三处与前三轮不同的地方，值得记住：

1. **`choices` 的 id 是两层**：`ContentText` 支持 `"表态id/选项id"`，`EnsureNoOrphans`
   也会钻进 `options` 逐条查孤儿——所以选项漏一条、多一条都会响。
2. **成就的文案是"按 id 展开后的成品"，不是模板**。成就表大多由循环铺出来
   （"每座建筑三档"），名字里带建筑名、说明里带数字；外置的机制只有"id → 字段"这一种，
   所以写进文件的是**展开之后**的那一条条文本，id 仍由代码算（`$"{building.Id}_x{count}"`）。
   换一座建筑的名字，那三条成就的文案**不会**跟着变——这正是"按 id 展开"的代价，
   也是守卫要逐条比对"代码值 == 文件值"的原因。
3. **没有这一类内容的包，文件里不许有那个分区**：`Neko`（示例包）没有结局、立场、表态，
   `Cafe` 也没有；期望条数表里都写 **0**。**但十一个包都有成就**。

- **守卫**：四张写死的条数表（`ExpectedEndings` / `ExpectedStances` / `ExpectedChoices` /
  `ExpectedAchievements`）+ 四条 `…CountTable_CoversExactlyTheGuardTable` +
  四条 `Every…_ResolvesItsTextFromTheFile`（双向 + 非空）+
  四条 `EditingThe…TextWrongly_FailsLoudly`（少一条 / 多一条都要响）。
  条数表是**唯一**能发现"代码与文件同时少一条"的守卫。
- **写作形态**之外还有一条给内容作者的提醒：这四类的文案**不参与**条件判断，
  改文案不会影响任何解锁——条件树、权重、修饰符留在代码里，两边只靠 id 关联。

### 12.0.4 增益 / 升级 / 金猫结果也外置了（2026-10-03 起，第五轮也是最后一轮）

第五轮把"面向玩家的文案"这一类里**剩下的全部**搬进同一个 `text.json`，用三个新根节：
`buffs` / `upgrades` / `goldenCookies`。判据仍是同一条：**能进数据的只有散文**。
做完这一轮，十一类文案**全部**在文件里了。

| 分区 | 形状 | 代码侧 | 留在代码里的 |
|---|---|---|---|
| `buffs` | `"<增益 id>": { "name", "description", "icon" }` | `Prose.Text("buffs", id, "description")` | `Id` / `Duration` / `MaxStacks` / `StackMode` / `Modifiers` / `IsDebuff` / `Dispellable` |
| `upgrades` | `"<升级 id>": { "name", "description", "icon" }` | `Prose.Text("upgrades", id, "name")` | `Id` / `Price` / `Currency` / `Persistence` / `MaxPurchases` / `PriceGrowth` / `Unlock` / `Modifiers` / `Tags` / `Category` / `Tier` / `HiddenUntilUnlocked` |
| `goldenCookies` | `"<结果 id>": { "name", "description", "icon" }` | `Prose.Text("goldenCookies", id, "description")` | `Id` / `Weight` / 全部 `Cookies*` / `StealBankFraction` / `BuffId` / `BuffSeconds` / `SecondaryBuffId` / `SecondaryBuffSeconds` / `IsRare` |

四件这一轮与前面几轮不同、值得记住的事：

1. **十一个包都有这三类内容**，三张条数表里**一个 0 都没有**（纪元 / 结局那样"某个包没有"的
   情形在这里不存在）。条数表仍然是**唯一**能发现"代码与文件同时少一条"的守卫。
2. **升级的文案大多是"算出来的"**：每个包都有一段"每座建筑三档"的循环，名字里带建筑名、
   说明里带建筑名、图标就是建筑的图标。外置机制只有"id → 字段"这一种，所以文件里存的是
   **按 id 展开后的成品**（运行时 **545** 条各自一条文本），id 仍由代码算
   （`$"{building.Id}_tier{required}"`）。**没有模板引擎**。
   **代价**：改一座建筑的名字，那三档升级的文案**不会**跟着变——
   这是"按 id 展开"的代价，也是守卫要逐条比对"代码值 == 文件值"的原因。
   同一份值现在存在两处（`buildings.<建筑>.name` 与 `upgrades.<建筑>_tier1.name`），
   守卫保证"代码读到的 == 文件里的"，但**不保证两份文件值一致**——
   与 `endings.<id>.name` 对 `achievements.ach_end_<id>.name` 的重合是同一性质，**已知且刻意接受**。
3. **`goldenCookies.<id>.description` 里可以有 `{amount}` / `{duration}` 占位符**——
   它们是**渲染期**替换的，替换发生在核心代码（`GoldenCookieSystem.Describe`）里，
   文件里写的就是模板本身那一条条文本，不是渲染结果。
   **这一条文案有一条静默降级路径**：`description` 为空（或全空白）时，界面会**悄悄改用名称**。
   所以它用的是 `Text(...)`（缺失即抛）而**不是** `TextOr`，守卫里也断言它非空——
   "没写说明"从"悄悄换一套文案"变成"当场抛"。
4. **其余字段一律不搬**：数值、条件树、修饰符、标签、分组、档位、排序、存档键都不是散文。
   改这些只改对应 `.cs`，改文案只改 `text.json`，两边靠 id 关联。

- **守卫**：三张写死的条数表（`ExpectedBuffs` / `ExpectedUpgrades` /
  `ExpectedGoldenCookieOutcomes`）+ 三条 `…CountTable_CoversExactlyTheGuardTable` +
  三条 `Every…_ResolvesItsTextFromTheFile`（双向 + 非空）+
  三条 `EditingThe…TextWrongly_FailsLoudly`（少一条 / 多一条都要响）。
- **一个包只能有一个 `ContentText` 实例**（这条从 §12.0.1 起就是**负载性**的）：
  本轮新加的读写点同样共用 `Lore.Prose`（示例包 `Neko` 用 `Buildings.Prose`），
  没有新建第二个实例。

### 12.0.5 自由文本表：**没有人读**的表也可以放进去（2026-10-04 起）

前五节讲的都是"把已经写在 C# 里的文案搬出来"。这一节讲另一种东西：**纯粹给作者的地方**——
一段文字现在还没有任何代码读它，但作者想先写下来。它叫**自由文本表**，
**加一张、删一张都不需要改任何 C#**。

```jsonc
{
  "$tables": { "rumours": { "kind": "free" } },   // 清单：这张表没有人读
  "rumours": {                                     // 表本体：与其它分区同一层
    "rumour_41st": { "text": "值班记录本上第 41 页被撕掉了，撕口是新的。" }
  }
}
```

- **`$tables` 是根节点上唯一的保留键**（`$` 开头即保留）。`kind` 只有两个值：
  `free`（**没有人读**，因此豁免孤儿检查）与 `consumed`（代码消费它，与不声明**等价**）。
- **没出现在清单里的分区 = `consumed`**：十一类分区一个字节都不用改，
  没有 `$tables` 的 `text.json` 与以前完全一样。
- **自由表的条目恰好一个字段 `text`**（非空字符串）。**形状与别的分区完全一样**
  （`id → { 字段: 文本 }`）——这是刻意的：将来真有人读它时，把声明改成 `consumed`，
  再用 `Prose.Text("<表名>", "<id>", "text")` 就能取，**永远不需要新的公开 API**。
- **改文案只改 `text.json`**；要读它就"改声明 + 写调用点"，两边靠 id 关联（同 §12.0）。
- **守卫**：加载时查清单合法、`kind` 认识、声明的表真的存在、自由表非空、条目是
  `{text: 非空}`、**全文没有重复键**、没声明的分区里不许出现非对象条目；
  `Build()` 末尾的孤儿检查再查一条"**声明为 `free` 的表不许被代码取用**"——
  取用了当场抛，并点名表与键（改声明是改数据，不是改代码）。
- **刻意不查**：自由表**没有条数表**（它没有"代码那一侧"，§12.0.1 说的那个
  "代码与文件同时少一条"的盲区在结构上不成立）。代价写清楚：
  **删掉一整张自由表不会有任何用例变红**——因为那是合法操作。
- **v1 没有人读它，也没有界面**：把自由表渲染到界面上属于
  [WEB_EXTENSION_PLAN](WEB_EXTENSION_PLAN.md)（**已搁置**）那一侧的问题，本机制不为它预埋任何东西。
  完整设计与判别力证据见 [TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md) §13。

> 示例（真的在仓库里）：`engine/content/Lab/text.json` 的 `rumours`（3 条），
> 清单在同一个文件的开头。

---

### 12.1 条件编排：三条硬规则

叙事条目写得好不好是文笔问题，**能不能被按顺序读到**是条件编排问题。这两件事互不相干，
而后者非常容易写错——错法还都很隐蔽，因为**图鉴不会报错，它只会显示得很难看**。

| 规则 | 为什么 |
|---|---|
| **任何两条的 `Reveal` 不得相同** | 同一个条件必然在同一瞬间一起解锁。首版 50 条里有 23 条落在 10 个重复组里，最坏 3 条同时弹；九命那边最坏 **6 条同时**。玩家看到的是"一口气倒出六段剧情"。<br>写条件时别挑圆整数：`1e9` 会被挑中三次，改成 `9e8` / `1.1e9` / `1.3e9`。**看着别扭是故意的。** |
| **每条线要有单调递增的骨架** | 同一条线内，门槛必须随 `Order` 递增，否则图鉴里会出现"第 3 条还锁着，第 4~9 条已经亮了"。<br>多指标混排必然出错：成就与建筑类里程碑全在开局一小时内触发，赚钱类要到几小时后，交替排列就倒挂。**用累计赚取当骨架定序，把建筑 / 成就 / 点击门槛降级成"顺手满足"的附加条件。** |
| **转生类条目只能放线的尾部** | 转生 / 店休的时机由**玩家**决定。`PrestigeLevelAtLeast(1)` 夹在赚钱类条目中间时，先攒钱后店休的玩家就会看到倒挂。尾部内部再用转生等级递增定序。 |
| **层内门槛必须低于本层的完成门槛** | 挂 `EraAtLeast(n)` 的条目，其层内里程碑若 ≥ 第 n 层的完成门槛，玩家会在够条件前就走人——这条**结构上读不到**，运行期看不出来（它只是"一直没出现"）。<br>这条由 `LoreTests.EraGatedLore_StaysBelowItsEraCompletion` 通用守卫（对全部内容包生效）。#9 首版真的写坏了两条：`she_12` 挂在 1.4e11，而第 5 本的完成门槛是 1e11。 |

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

