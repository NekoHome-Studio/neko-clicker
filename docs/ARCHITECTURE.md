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
│   ├── EraDefinition.cs       纪元定义 + 闸门状态（EraGate）
│   ├── LoreEntry.cs           叙事条目 + 剧情线
│   ├── GameBalance.cs         全局平衡参数
│   ├── GameContent.cs         内容容器（含索引）
│   └── GameContentBuilder.cs  构建 + 校验
├── Simulation/                运行时
│   ├── GameState.cs           全部可变状态（含 ActiveBuff / GoldenCookieSpawn / LoreUnlocked）
│   ├── GameMetrics.cs         IGameMetrics 的引擎实现
│   ├── ModifierSet.cs         修饰符累加器 + 求解器
│   ├── ProductionCalculator.cs 生产管线
│   ├── Pricing.cs             价格闭式解（单价 / 批量 / 买满 / 出售）
│   ├── BuffSystem.cs          增益施加、叠加、到期 + 成就检查
│   ├── PrestigeSystem.cs      转生公式、预览、重置语义
│   ├── EraSystem.cs           纪元闸门判定、推进、跨层继承
│   ├── LoreSystem.cs          叙事释放判定、待读队列、图鉴查询
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

## 纪元（`Era`）与叙事（`Lore`）

这两套系统是为了让**同一个核心**承载十种完全不同世界观的内容包（见 NINE_LIVES_DESIGN）。
它们的共同约束是：核心不知道任何具体层号、任何具体条目。

### `Era` 只通过四个接缝进入引擎

| 接缝 | 作用 |
|---|---|
| `GameContent.BalanceFor(eraIndex)` | 取本层的数值规则（未覆盖则回退内容包基准） |
| `ModifierResolver.Build` 的第 4 个来源 | 本层常驻倍率，与升级 / 成就 / 增益同一条管线 |
| `NumericMetric.Era` | 让条件树能引用层号（例如"只在第 3 层解锁"） |
| `PrestigeSystem.ResetRun(..., enteringEra)` | 重置时按**进入的那一层**决定继承什么 |

这四条就是架构不变量 A2 的全部内容。`ArchitectureTests` 里有一条测试扫描核心程序集，
确保不出现 `era == 3` 这类层号比较——**"第 N 层有什么不同"必须由数据表达，不能由代码分支表达**。

### 单调性是硬约束，不是建议

`EraDefinition.Completion` 会被持续渲染在舍命按钮上（灰按钮 + 进度条 + 原因）。
如果它可以下降，玩家就会看到进度倒退。因此构建期按**白名单**校验它引用的指标：
累计赚取 / 成就数 / 点击数 / 金猫数 / 已购升级 / 游玩时长 / 计数器 / 标签升级数 / 图鉴条数可以，
当前小鱼干 / 每秒产量 / 建筑数量 / 转生徽章 / 层号本身不行。

### `Lore` 复用条件树做"节奏编排"

`LoreSystem` 在成就检查的同一时点扫描未解锁条目，`Reveal` 达成就入 `PendingLorePopups`。
`UnlockCondition` 在这里不是门控工具而是编排工具——同一个机制既拦内容也放故事。
`TotalEntries` 是**声明值**而非统计值，构建期校验一致性，这样图鉴在条目没写完时也能显示 `3 / 20`。

可达性校验（`ValidateReachability`）的不动点会把叙事节点一起算进去：
一条"需要图鉴 N 条才解锁"的升级，不会因为那些条目本身不可达而变成死内容。

## 终端渲染：为什么不是"每帧整屏重写"

全屏 TUI 的闪烁几乎总是同一个原因：**清屏 + 全量重绘之间的时间差被人眼捕捉到**。
第一版的做法已经避开了最糟糕的形态（进入备用屏幕、只在开头清一次、光标归位而不是清屏、
隐藏光标、整帧一次 `Write` 而不是逐行写），但仍然每帧重画整个窗口。

实测（118×30、60 秒 @20fps）：

| 做法 | 60 秒写入量 | 折算 |
|---|---|---|
| 整屏重写 | 36,000 行 / 4,170 KB | ~70 KB/s |
| 只写变化的行 | 1,229 行 / 137 KB | ~2.3 KB/s |

**过去 97% 的终端写入是在重画没变的东西。** 变化的行几乎全是时间驱动的
（每秒产量、金猫倒计时），所以"纯挂机"和"持续操作"差别很小（1229 vs 1239 行）——
**静止时不写才是关键**。

现在的做法（`InteractiveLoop.FramePainter`）：

| 措施 | 作用 |
|---|---|
| 只重画内容变化的行 | 静止时写入量为 0；动起来只写那一两行 |
| 每行**从第 1 列整行覆盖** + `EL` 擦到行尾 | 行级增量因此是安全的：要么整行正确替换，要么这行没动过。不存在"只改几个格子、中文宽度算错就留脏字"的窗口 |
| 同步输出（DEC 私有模式 2026） | 夹在 `?2026h` / `?2026l` 之间的写入被终端攒成一批、整体上屏。**这才是终端上真正意义的双缓冲**；不支持的终端会忽略该序列 |
| 尺寸变化 / 每 5 秒兜底整屏重画 | 兜住终端 resize 与外部程序往屏幕上写字的情况 |
| 末行之后不补换行 | 补了会把整屏往上滚一格——那是另一种闪烁 |
| 退出时补 `SyncEnd` | 万一在同步窗口内异常退出，终端会一直攒着不上屏 |

> **不要"简化"回整屏重写。** 它看起来更简单、也确实能跑，但代价是上面那张表的 97%。
> 改动前后都请重新量一遍写入量，别靠感觉判断"应该差不多"。

## 扩展点

| 需求 | 做法 |
|---|---|
| 新增一种加成目标 | 扩展 `ModifierTargetKind` + `ModifierTarget.Describe` + 消费方读取 |
| 新增一种解锁条件 | 继承 `UnlockCondition` 并实现 `IsMet` / `Describe` / `TryGetProgress` |
| 新增一种成长曲线 | 扩展 `ScalingSource` + `Scaling.Evaluate` |
| 新增一层纪元 | 往内容包加一条 `EraDefinition`（层号连续、完成条件单调），核心零改动 |
| 新增一条叙事线 | 加 `StorylineDefinition` + 若干 `LoreEntry`，核心零改动 |
| 接入引擎不认识的玩法 | 实现 `IGameModule`（`Configure` 补内容、`OnAttach` 订阅事件、`OnTick` 推进） |
| 换存储介质 | 实现 `IStorage`（文件 / 内存 / 浏览器 localStorage / 云） |
| 换时间源 | 实现 `IClock`（真实 / 手动 / 加速） |
| 改存档格式 | 写 `ISaveMigration` 并注册进 `SaveSerializer.Migrations` |
| 换前端 | 只依赖 `GameSnapshot` 与引擎的公开命令方法 |

## 测试策略

`tests/NekoClicker.Core.Tests` 自带 200 行迷你运行器（反射扫描 `[Test]` 方法），
共 **210 个用例**，覆盖六类问题：

1. **数值正确性**：格式化边界、饱和算术、价格闭式解与暴力求和一致、买满的两侧夹逼。
2. **规则正确性**：购买扣款、买不起时的降级、成就阈值、增益叠加与到期、转生重置语义。
3. **契约正确性**：存档往返（含 PRNG 状态）、版本迁移、损坏存档不崩、视图行的字段。
4. **曲线活性**：6 小时贪心模拟必须"有事发生"——买到新建筑、解锁成就、点中金猫，
   且全程不出现 NaN/∞。单元测试能证明公式对，只有长跑能证明曲线是活的。
5. **架构不变量**（`ArchitectureTests`，ROADMAP K1/A1/R7）：
   核心程序集不引用任何 `NekoClicker.Content.*`、其字符串字面量里不出现任何内容 id、
   内容包之间互不引用、核心不含层号分支。这几条是"核心不认识内容"这一主张的可执行版本——
   一旦被侵蚀，先红的不是文档而是 CI。
6. **内容真的走得完**（`EraTests` / `LoreTests`，G4/G5）：
   `G4_RobotWalksFromTheFirstEraToTheLast` 让机器人从第 1 命走到第 9 命，
   证明**内容在真实曲线下可达**（首轮就抓出"第 4 层之后走不动"）；
   `LoreTests` 锁定图鉴总数一致、前 10 分钟释放 ≤3 条、存档往返保留解锁状态。
   这类测试证明的从来不是"代码能跑"，而是"这份内容成立"。
