# 架构

本文说明框架的分层、各模块职责，以及几个关键设计决策背后的原因。

## 模块地图

```
NekoClicker.Core
├── IClock.cs                  时间源抽象（SystemClock / ManualClock）
├── IGameMetrics.cs            内容层能看到的状态只读契约
├── IGameModule.cs             可插拔系统（引擎不认识的玩法）
├── GameEngine.cs              门面：时间推进 + 玩家动作 + 事件派发
├── Numbers/
│   ├── Num.cs                 饱和算术（避免溢出成 Infinity）
│   └── NumFormat.cs           数值/百分比/时长格式化（short scale + 科学计数法）
├── Randomness/
│   └── DeterministicRandom.cs xorshift128+，状态可存档、序列可复现
├── Content/                   内容定义（不可变）
│   ├── Definitions.cs         建筑 / 升级 / 成就 / 增益 / 金猫结果
│   ├── Modifier.cs            修饰符 + 成长曲线
│   ├── ModifierTarget.cs      作用目标
│   ├── UnlockCondition.cs     可组合、可量化的解锁条件树
│   ├── GameBalance.cs         全局平衡参数
│   ├── GameContent.cs         内容容器（含索引）
│   └── GameContentBuilder.cs  构建 + 校验
├── Simulation/                运行时
│   ├── GameState.cs           全部可变状态（含 ActiveBuff / GoldenCookieSpawn）
│   ├── GameMetrics.cs         IGameMetrics 的引擎实现
│   ├── ModifierSet.cs         修饰符累加器 + 求解器
│   ├── ProductionCalculator.cs 生产管线
│   ├── Pricing.cs             价格闭式解（单价 / 批量 / 买满 / 出售）
│   ├── BuffSystem.cs          增益施加、叠加、到期 + 成就检查
│   ├── PrestigeSystem.cs      转生公式、预览、重置语义
│   ├── GoldenCookieSystem.cs  随机事件：刷新、抽取、结算
│   └── ActionResults.cs       动作结果类型（PurchaseResult 等）
├── Events/                    事件总线 + 领域事件
├── Persistence/               IStorage / SaveData / SaveSerializer / SaveManager
└── Views/                     UI 只读视图 + GameSnapshot
```

依赖方向是**单向**的：`Views` → `GameEngine` → `Simulation` / `Content` → `Numbers` / `Randomness`。
内容层只能通过 `IGameMetrics` 读状态，不能改状态，因此所有数值改动都必须经过引擎的
购买/点击流程（可回放、可校验、可测试）。

## 时间模型

```
Update(deltaSeconds)          真实帧率驱动，带补算上限
  └─ 累加进 _accumulator
      └─ 按 FixedDeltaSeconds（默认 1/30 秒）切片
          └─ Step(dt) : 生产结算 → 游玩时长 → 增益到期 → 金猫刷新 → 模块 tick → 成就检查
      └─ 派发一次 TickEvent（每个 Update 一次，而不是每步一次）

Simulate(seconds)             跳跃式推进，不受补算上限约束（测试 / 无头模拟）
```

- **补算上限**（`MaxCatchUpSeconds`，默认 5 秒）是为了避免"卡帧/断点后一次算爆"。
  超出部分被丢弃——玩家不该靠卡帧白拿几小时产量，那正是离线收益上限要处理的事。
- **固定步长**保证产量计算与帧率无关；`ManualClock` 让测试可以瞬间跑完 24 小时。
- `TickEvent` 每个 `Update` 派发一次而不是每个固定步长，避免 UI 在 30fps 下被通知轰炸。

## 数值管线

```
每帧按需重算（脏标记驱动，不是每次读都算）：
  1. ModifierResolver.Build()  收集：已购升级（按次数重复）× 已解锁成就 × 生效增益（按层数）
  2. ProductionCalculator.Compute()
       buildingMult_b = (1 + Σ加法百分比_b) × Π乘法_b      （再施加乘方）
       cps_b          = (基础产量_b + 加法项_b) × buildingMult_b × count_b
       总CPS          = Σ cps_b × 全局倍率，再统一施加全局乘方
       点击收益       = (固定基础 + 总CPS × ClickCpsRatio + 加法项) × 点击倍率
  3. 缓存结果；购买 / 成就解锁 / 增益增减 / 读档都会 MarkDirty()
```

**全局乘方怎么摊回每个建筑？** 先算未施加乘方的总量 `T`，再算最终量 `T^p`，
得到一个标量因子 `T^p / T` 乘回每个建筑。这样"各建筑占比"在乘方后依然自洽。

**为什么加法百分比与乘法分开累计？** 让"两个 +50%"得到 +100%（而不是 ×2.25），
而"两个翻倍"得到 ×4。这是玩家能直觉预期的行为，也是 Cookie Clicker 的做法。

## 价格求解

- 单价：`base × growth^owned × priceMultiplier`
- 批量 k 个：等比数列求和闭式解 `first × (growth^k − 1) / (growth − 1)`
- 买满：反解不等式 `first × (growth^k − 1)/(growth − 1) ≤ budget`
  → `k ≤ log_growth(1 + budget(growth−1)/first)`，再用实际总价回退校验消除浮点误差
- 出售：对最近买进的 k 个单价求和 × 返还比例

不用循环累加的原因：后期 `BuyMax` 要一次算出成百上千个，循环会卡；闭式解在浮点下也更稳定。

## 解锁条件树

`UnlockCondition` 是一棵可组合的树（`All` / `Any` / `Not` + 数值叶子 + 持有型叶子 + 自定义谓词），
内容定义因此是**纯数据、可在构建期校验**的。额外得到两个能力：

- `TryGetProgress()` → UI 能显示"还差 3 只猫"的进度条
- `NumericLeaves()` → 视图层能反查"下一个里程碑"，构建期能校验引用是否存在

## 存档与迁移

```
GameState  ←→  SaveData（DTO）  ←→  JSON / base64 分享码
                    ↑
              版本迁移在反序列化之前对 JSON 树执行
```

- `GameState` 可以自由重构；`SaveData` 一旦发布必须向后兼容（缺失字段取默认值）。
- 迁移在 `JsonNode` 层执行，因此迁移代码不需要认识旧的 C# 类型。
  见 `ISaveMigration` 与 `SaveSerializer.Parse`。
- 未知的内容 id **不会被丢弃**：内容包临时下线某个建筑时，存档依然无损。
- `SaveManager` 通过订阅 `TickEvent` 计时，引擎完全不知道"存档"这件事存在。

## 离线收益

以存档里的 `LastSavedAt` 为基准，受 `OfflineCapSeconds` 约束，
并且**用不含增益的产量**结算——玩家下线期间狂热早就过期了，按带增益的秒产量补发会显著高估。
时钟回拨时不做任何补发。

## 随机与可复现性

所有随机都走 `DeterministicRandom`（xorshift128+），状态随存档保存。
因此同一份存档、同一串操作必然得到同一结果——这是能对数值做回归测试与平衡验证的前提。
`System.Random` 不可用：它的内部状态无法存取，且不同 .NET 版本的算法可能变化。

## 扩展点

| 需求 | 做法 |
|---|---|
| 新增一种加成目标 | 扩展 `ModifierTargetKind` + `ModifierTarget.Describe` + 消费方读取 |
| 新增一种解锁条件 | 继承 `UnlockCondition` 并实现 `IsMet` / `Describe` / `TryGetProgress` |
| 新增一种成长曲线 | 扩展 `ScalingSource` + `Scaling.Evaluate` |
| 接入引擎不认识的玩法 | 实现 `IGameModule`（`Configure` 补内容、`OnAttach` 订阅事件、`OnTick` 推进） |
| 换存储介质 | 实现 `IStorage`（文件 / 内存 / 浏览器 localStorage / 云） |
| 换时间源 | 实现 `IClock`（真实 / 手动 / 加速） |
| 改存档格式 | 写 `ISaveMigration` 并注册进 `SaveSerializer.Migrations` |
| 换前端 | 只依赖 `GameSnapshot` 与引擎的公开命令方法 |

## 测试策略

`tests/NekoClicker.Core.Tests` 自带 200 行迷你运行器（反射扫描 `[Test]` 方法），
覆盖五类问题：

1. **数值正确性**：格式化边界、饱和算术、价格闭式解与暴力求和一致、买满的两侧夹逼。
2. **规则正确性**：购买扣款、买不起时的降级、成就阈值、增益叠加与到期、转生重置语义。
3. **契约正确性**：存档往返（含 PRNG 状态）、版本迁移、损坏存档不崩、视图行的字段。
4. **曲线活性**：6 小时贪心模拟必须"有事发生"——买到新建筑、解锁成就、点中金猫，
   且全程不出现 NaN/∞。单元测试能证明公式对，只有长跑能证明曲线是活的。
5. **架构不变量**（`ArchitectureTests`，ROADMAP K1/A1/R7）：
   核心程序集不引用任何 `NekoClicker.Content.*`、其字符串字面量里不出现任何内容 id、
   内容包之间互不引用。这三条是"核心不认识内容"这一主张的可执行版本——
   一旦被侵蚀，先红的不是文档而是 CI。
