# 内容包 #1：《猫娘咖啡馆》完整规格

> **状态（2026-09-24）：已落代码。** 实现在 `src/NekoClicker.Content.Cafe/`，
> 可执行 `.\tools\play.ps1 --package cafe` 试玩；验收记录见 [ROADMAP.md](ROADMAP.md) §7 阶段 0。
> 唯一与本文的差异：§10 的 50 条叙事按 R10 暂走描述字段，等阶段 2 的 S-B 到位再补。

> **这份文档有双重身份**：
> 1. 它是**内容包 #1 的可落代码规格**——按表填就能做出能玩的游戏，不需要任何引擎能力。
> 2. 它是**其余九个包的写作模板**——第 14 节列出"换一个包要改哪几行"。
>
> 关联：[NINE_LIVES_DESIGN.md](NINE_LIVES_DESIGN.md)（总体设计）、[CONTENT_AUTHORING.md](CONTENT_AUTHORING.md)（框架写法）

---

## 1. 定位与转生语义

| 项 | 内容 |
|---|---|
| 语气 | 轻松治愈。**十个包里唯一没有道德压力的那个** |
| 核心幻想 | 把一家小咖啡馆，做成连接人类世界与异世界的门 |
| 转生语义 | **重装修**——店面推倒重来，但**常客的记忆留下来** |
| 结局 | 两界桥梁（单一温暖结局，按幸福感档位有 3 个文本变体） |
| 与其它包的关系 | 世界观里的"入口与治愈层"；#2 九命轮回的玩家会把它当回家 |

**转生怎么讲**：不叫"重置"，叫"店休一个月重新装修"。玩家保留下来的不是"永久升级"，
是**常客的记忆**——他们记得你，所以回来得比别人快。这正是 `Permanent` 升级的叙事外壳。

---

## 2. 核心循环

```
点击做咖啡 ──→ 卖咖啡得小鱼干 ──→ 招猫娘（买建筑）──→ 猫娘自动招待客人（CPS）
    ↑                                                        │
    └──── 装修升级（升级）←── 收集幸福感（第二资源）←──────────┘
                                    │
                        解锁异世界门 → 重装修（保留常客记忆）→ 回到起点，但门更宽了
```

玩家的三分钟体验应该是：点几杯咖啡 → 买下第一台咖啡机 → 第一次看到猫娘自己招呼客人 →
发现"幸福感"在涨 → 想知道涨满了会发生什么。

---

## 3. 平衡参数（完整 `GameBalance`）

在 `NekoContent.BuildBalance()` 基础上做四处调整，体现"治愈系"手感：

| 参数 | 值 | 与九命轮回包的差异与理由 |
|---|---|---|
| `ClickBasePower` | 1 | 同 |
| `ClickCpsRatio` | **0.015** | 略高于基准（0.01）：咖啡馆的点咖啡应该有存在感 |
| `TickRate` | 30 | 同 |
| `MaxCatchUpSeconds` | 5 | 同 |
| `AchievementCheckInterval` | 1 | 同 |
| `AutoSaveInterval` | 60 | 同 |
| `GoldenCookieMinDelay` | **60 × 4** | 比基准（5 分钟）短：治愈系要让随机好事更常发生 |
| `GoldenCookieMaxDelay` | **60 × 12** | 同上 |
| `GoldenCookieLifetime` | **15** | 比基准（13 秒）宽容 2 秒，降低手忙脚乱 |
| `MaxConcurrentGoldenCookies` | 1 | 同 |
| `FirstGoldenCookieDelayFactor` | **0.15** | 开局第一只更早出现（基准 0.25） |
| `OfflineCapSeconds` | 3 小时 | 同 |
| `OfflineEfficiency` | 1.0 | 同 |
| `MinimumOfflineSeconds` | 60 | 同 |
| `PrestigeDivisor` | 1e12 | 同 |
| `PrestigeExponent` | 1/3 | 同 |
| `PrestigeChipsPerLevel` | 1 | 同 |
| `KeepAchievementsOnAscend` | true | 同 |
| `AllowSelling` | true | 同 |
| `DefaultSellRefundRate` | **0.6** | 比基准（0.5）高：治愈系不该惩罚试错 |
| `MaxBulkBuy` | 100000 | 同 |

> 转生货币在这里叫 **「常客的信」**，图标 `💌`。

---

## 4. 建筑表（10 座）

数值直接沿用已验证的曲线（相邻价格 ×6.7~16.5、产量 ×5.4~10，第 3 座起价格倍率 > 产量倍率），
所以 `ContentTests` 里那条曲线回归测试不需要改参数就能通过。

| # | id | 名称 | 图标 | 基础价 | 基础产量 | 价格倍率 | 产量倍率 | 解锁条件（本轮累计） |
|---|---|---|---|---|---|---|---|---|
| 1 | `coffee_machine` | 咖啡机 | ☕ | 15 | 0.1 | — | — | 无条件 |
| 2 | `bar_counter` | 吧台 | 🪑 | 100 | 1 | ×6.67 | ×10 | 30 |
| 3 | `cat_tree` | 猫爬架 | 🐈 | 1,100 | 8 | ×11.0 | ×8.0 | 330 |
| 4 | `window_seat` | 靠窗座位 | 🪟 | 12,000 | 47 | ×10.91 | ×5.88 | 3,600 |
| 5 | `upstairs` | 二楼雅座 | 🪜 | 130,000 | 260 | ×10.83 | ×5.53 | 39,000 |
| 6 | `bakery` | 烘焙间 | 🥐 | 1,400,000 | 1,400 | ×10.77 | ×5.38 | 420,000 |
| 7 | `catgirl_staff` | 猫娘店员 | 😺 | 20,000,000 | 7,800 | ×14.29 | ×5.57 | 6,000,000 |
| 8 | `otherworld_door` | 异世界门 | 🌀 | 330,000,000 | 44,000 | ×16.5 | ×5.64 | 99,000,000 |
| 9 | `memory_roastery` | 记忆烘焙坊 | 🫘 | 5,100,000,000 | 260,000 | ×15.45 | ×5.91 | 1,530,000,000 |
| 10 | `branch_store` | 分店 | 🏬 | 75,000,000,000 | 1,600,000 | ×14.71 | ×6.15 | 22,500,000,000 |

- 全部 `PriceGrowth = 1.15`、`HiddenUntilUnlocked = true`、`SellRefundRate = null`（继承 balance 的 0.6）。
- 标签：`drink`（1-2、5）、`cat`（3、7）、`place`（4、6）、`portal`（8-10）。
- **建筑说明就是叙事通道**：每条的 `Description` 是 1~2 句带画面感的短文，不写数值。

---

## 5. 升级表（48 条）

### 5.1 建筑强化档（30 条，循环生成）

沿用 `NekoContent` 的生成器，把"更好的/加倍的/传奇的"换成咖啡馆语气：

| 档位 | 需要拥有 | 价格 = 建筑基础价 × | 效果 | 名称模板 |
|---|---|---|---|---|
| 1 | 1 | 10 | 该建筑产量 ×2 | 「熟能生巧的{建筑}」 |
| 2 | 5 | 100 | 该建筑产量 ×2 | 「小有名气的{建筑}」 |
| 3 | 25 | 500 | 该建筑产量 ×2 | 「招牌级的{建筑}」 |

### 5.2 点击线（5 条）— "做咖啡"的手艺

| id | 名称 | 价格 | 解锁 | 效果 |
|---|---|---|---|---|
| `steady_hands` | 稳定的手 | 100 | 点击 ≥ 10 | `ClickFlat(1)` |
| `latte_art` | 拉花艺术 | 5,000 | 点击 ≥ 100 | `ClickMultiplier(2)` |
| `single_origin` | 单品豆 | 500,000 | 点击 ≥ 500 且拥有 `latte_art` | `ClickMultiplier(2)` |
| `hand_drip` | 手冲技法 | 50,000,000 | 点击 ≥ 2,000 且拥有 `single_origin` | `ClickMultiplier(3)` |
| `barista_soul` | 咖啡师之魂 | 2,000,000,000 | 点击 ≥ 5,000 且拥有 `hand_drip` | `ClickPercent(0.25)` |

### 5.3 呼噜线（3 条）— 按成就数给全局加成

| id | 名称 | 价格 | 解锁 | 效果 |
|---|---|---|---|---|
| `purr_chorus` | 呼噜合唱 | 9,000,000 | 成就 ≥ 5 | `GlobalPercent(0, Scaling(AchievementCount, 0.01))` |
| `purr_symphony` | 呼噜交响 | 90,000,000,000 | 成就 ≥ 20 且拥有 `purr_chorus` | `GlobalPercent(0, Scaling(AchievementCount, 0.02))` + `ClickMultiplier(1.5)` |
| `heartbeat_of_world` | 世界心跳 | 9,000,000,000,000 | 成就 ≥ 40 且拥有 `purr_symphony` | `GlobalPercent(0, Scaling(AchievementCount, 0.03))` |

### 5.4 特色线（5 条）— 咖啡馆专属

| id | 名称 | 价格 | 解锁 | 效果 | 设计意图 |
|---|---|---|---|---|---|
| `regulars_list` | 常客名单 | 60,000 | 猫爬架 ≥ 10 且幸福感 ≥ 500 | 全部建筑 ×1.4 | 第一次把"幸福感"和产量挂上钩 |
| `secret_recipe` | 私藏配方 | 8,000,000 | 二楼雅座 ≥ 15 | `GlobalPercent(0, Scaling(BuildingCount, 0.01, Cap: 200, Id: "cat_tree"))` | 联动成长 |
| `otherworld_supply` | 异世界供货 | 900,000,000 | 异世界门 ≥ 20 | `BuildingMultiplier("otherworld_door", 3)` | 单建筑爆发 |
| `memory_blend` | 记忆拼配 | 1,000,000,000,000 | 常客的信（转生等级）≥ 3 | `GlobalPercent(0, Scaling(PurchasedUpgrades, 0.02, Cap: 50))` | 让"记得越多越强"成立 |
| `warm_light` | 暖光 | 7,777,777 | 客人（随机事件）≥ 3 | 事件奖励 ×1.25 + 事件频率 ×1.2 | 治愈系的手气加成 |

### 5.5 常客记忆（5 条，转生后保留）

用「常客的信」购买，`Persistence = Permanent`。**这是转生语义的核心载体**：
它们不是"永久升级"，是"某个客人还记得你"。

| id | 名称 | 价格（信） | 解锁 | 效果 |
|---|---|---|---|---|
| `remembers_your_name` | 他记得你的名字 | 3 | 信 ≥ 3 | 点击 ×3 |
| `usual_order` | 老样子 | 8 | 信 ≥ 8 | 全部建筑价格 −10% |
| `table_by_window` | 窗边那张桌子 | 12 | 信 ≥ 12 | 离线效率 ×1.5 |
| `birthday_cake` | 生日蛋糕 | 20 | 信 ≥ 20 | 事件频率 ×1.5、停留时间 ×1.5 |
| `still_open` | 还开着啊 | 30 | 信 ≥ 30 | 全部建筑 ×1.15 |

> 校验注意：`Permanent` 升级**必须**用 `UpgradeCurrency.PrestigeChips` 计价，否则
> `GameContentBuilder` 会直接报错。这一条正好把"常客的记忆只能用信来换"变成硬规则。

---

## 6. 成就表（45 条）

| 分类 | 条数 | 阈值设计 |
|---|---|---|
| `progress` 累计赚取 | 8 | 1e3 / 1e6 / 1e9 / 1e12 / 1e15 / 1e18 / 1e21 / 1e24 |
| `progress` 每秒产量 | 3 | 1e6 / 1e9 / 1e12 |
| `building` 建筑档位 | 10 座 × 3 档（1/25/50）= 30 条中的 **20 条**（去掉 10 座的第 3 档以控制总量） | 见下方说明 |
| `click` 点击 | 4 | 100 / 1,000 / 10,000 / 100,000 |
| `guest` 客人（随机事件） | 4 | 1 / 7 / 27 / 77 |
| `happiness` 幸福感 | 3 | 500 / 5,000 / 50,000 |
| `regular` 常客的信 | 3 | 1 / 10 / 100 |
| 合计 | **45** | |

其中两条直接给修饰符（演示另一条路径）：
- `click_10000`「万次手冲」 → `ClickMultiplier(1.5)`
- `regular_1`「第一次店休」 → `ClickMultiplier(1.2)`

隐藏成就 1 条：`happiness_50000`「？？？」（幸福感 50,000）。

---

## 7. 增益表（5 条）

| id | 名称 | 图标 | 时长 | 叠加 | 效果 |
|---|---|---|---|---|---|
| `caffeine_overload` | 咖啡因过载 | ⚡ | 77s | Refresh | 全部建筑 ×7 |
| `cat_chorus` | 猫娘合唱 | 🎶 | 13s | Refresh | 点击 ×777 |
| `boss_treats` | 老板请客 | 🎁 | 60s | Refresh | 全部建筑 ×15 |
| `failed_steam` | 打发失败 | 💨 | 66s | Refresh | 全部建筑 ×0.5（debuff） |
| `new_beans` | 新豆上市 | 🫘 | 30s | Extend | 「烘焙间」产量 ×30 |

> 与九命轮回包的结构完全一致，只换名称与目标建筑——**这就是"同一机制十种包装"的最小样本**。

---

## 8. 随机事件表（8 条）— 「走错门的客人」

| id | 名称 | 权重 | 效果 |
|---|---|---|---|
| `lucky` | 熟客 | 42 | `min(存量15%, 产量900秒) + 产量13秒` |
| `frenzy` | 团体客 | 30 | 咖啡因过载 77s |
| `click_frenzy` | 网红打卡 | 8 | 猫娘合唱 13s |
| `ruin` | 打翻咖啡 | 4 | 扣除存量 5% |
| `blab` | 迷路的猫 | 2 | 无效果，只有一句话 |
| `building_special` | 新豆到货 | 3 | 新豆上市 30s |
| `bloodlust` | 猫神路过 | 3 | 老板请客 60s（稀有） |
| `chain` | 两界信使 | 1 | 咖啡因过载 30s + 猫娘合唱 10s（稀有） |

权重合计 93。**非负面权重 89 / 93 ≈ 95.7%，负面只有 4.3%**——治愈系包的底线。
`ruin` 也只扣 5% 存量且不会扣成负数（引擎已保证）。

---

## 9. 第二资源：幸福感

### 9.1 它是什么

不是货币，是**评分**：客人满意就涨，涨到阈值解锁内容。**不可消费**（消费型第二资源会让玩家
纠结"该不该花"，与治愈系的基调冲突）。

### 9.2 怎么实现（**不改核心**）

用 `IGameModule` 实现，正好验证这个扩展点的价值：

```csharp
internal sealed class HappinessModule : IGameModule
{
    public string Name => "happiness";

    // 每 N 只猫娘每秒 +1 幸福感；N 随"常客名单"等升级下降
    public void OnTick(GameEngine engine, double deltaSeconds)
    {
        double catgirls = engine.State.TotalBuildings();
        double rate = catgirls / 50.0 * (1 + engine.Metrics.GetCounter("happiness_bonus"));
        engine.State.AddCounter("happiness", rate * deltaSeconds);
    }
}
```

- `GameState.Counters` 已存在，且**转生时不清空** → "常客的记忆"天然被保留。
- `Scaling(ScalingSource.CustomCounter, perUnit, Id: "happiness")` 已存在 → 幸福感可以驱动修饰符。
- 模块随 `GameContent.Modules` 注册，引擎自动挂载（见 `Modules_ReceiveConfigureAttachAndTick` 测试）。

### 9.3 ✅ 引擎侧前置：`NumericMetric.Counter`（已落地）

`UnlockCondition` 曾经**没有**"自定义计数器 ≥ N"的数值条件——`ScalingSource` 有
`CustomCounter`，但 `NumericMetric` 没有对应项。这个缺口已由 C1（ROADMAP §6.1）补上：
`NumericMetric.Counter` + `UnlockCondition.Counter(key, n)` 已实现并有测试覆盖。

| 方案 | 代价 | 状态 |
|---|---|---|
| **A. 加 `NumericMetric.Counter`**（`NumericCondition.Read` 一行 + 工厂方法） | 3 行代码；幸福感解锁**有进度条**、能被构建期校验 | ✅ **已采用** |
| **B. 用 `UnlockCondition.Custom(...)`** | 零改动；但**没有进度条**（`TryGetProgress` 返回 false），且构建期无法校验 | ❌ 未采用 |

幸福感是这个包的核心体验，"还差多少"必须看得见；3 行的改动换来一个完整的进度条体系，
非常划算。**这也是本包唯一超出"零引擎改动"的地方，且它是一次通用能力投资**——
阶段 1 的 `peak_cps` 完成条件与其余包的士气 / 信仰 / 被阅读度都会复用它。

---

## 10. 叙事条目表（50 条 / 3 条线）⏸ 待 S-B 落地

> **当前状态**：本阶段按 ROADMAP R10 走既有文本通道——画面感写进了建筑 / 升级 / 成就 /
> 增益 / 事件的 `Description` 字段（见 `src/NekoClicker.Content.Cafe/`）。
> 下面 50 条独立条目需要叙事系统 S-B（`LoreEntry` / 图鉴 / 释放通道），随阶段 2 一起补。

### 10.1 分配

| 剧情线 | id | 条数 | 承担什么 |
|---|---|---|---|
| 主线：两界之门 | `door` | 20 | 咖啡馆为什么能通异世界；门越来越宽；结局 |
| 支线：常客们的记忆 | `regular` | 16 | 每个常客是一段人类记忆；他们为什么回来 |
| 支线：异世界的供货商 | `supplier` | 14 | 豆子从哪来；供货商的真实身份 |

### 10.2 释放节奏（对应 NINE_LIVES_DESIGN §4.3）

| 阶段 | 条件 | 条数 | 通道 |
|---|---|---|---|
| 开场 1 分钟 | `TotalBuildings >= 1` | 3 | 2 `Log` + 1 `Popup` |
| 前 30 分钟 | 每买满一档建筑 / 每解锁 1 个成就 | 12 | `Log` |
| 中期 | 幸福感 500 / 5,000 / 50,000 | 9 | 3 `Popup` + 6 `Log` |
| 后期 | 转生等级 1 / 3 / 10 / 30 | 8 | `Popup`（转生是包里的情绪高点） |
| 收尾 | 赚取 1e15 / 拥有异世界门 ≥ 25 | 8 | `Popup` + `Codex` |
| 结局 | 转生等级 ≥ 10 且幸福感 ≥ 50,000 | 3 | `Popup`（3 个文本变体） |
| 解锁全部 10 座建筑后补完 | — | 7 | `Codex` |
| 合计 | | **50** | |

### 10.3 条目格式（含 6 条示范正文）

| id | 线 | 标题 | 释放条件 | 通道 | 正文（示范） |
|---|---|---|---|---|---|
| `door_01` | door | 门在厨房后面 | 建筑数 ≥ 1 | Popup | 「你以为是储藏间。推开门的时候，风是从另一边吹来的。」 |
| `door_02` | door | 第一只自己走进来的猫 | 咖啡机 ≥ 10 | Log | 「她没有敲门。她只是坐在吧台上，等你把牛奶打完。」 |
| `door_05` | door | 门缝里的光 | 建筑数 ≥ 3 | Log | 「你数过：今天的门缝比昨天宽了一点。宽了大概一只猫的厚度。」 |
| `regular_01` | regular | 周三的老先生 | 成就 ≥ 3 | Log | 「他每周三来，坐同一张桌子，点同一杯。他说这里让他想起什么，但想不起来具体是什么。」 |
| `regular_07` | regular | 他记得你的名字 | 转生等级 ≥ 3 | Popup | 「店休了一个月。推门进来的时候他说：『还开着啊。』——他记得你，所以你也还记得自己。」 |
| `supplier_01` | supplier | 豆子是从门那边来的 | 异世界门 ≥ 1 | Log | 「供货单上的地址写着『门的另一边，第三棵树下』。你决定不去确认。」 |
| … | | （其余 44 条同格式，逐条填 `id` / 线 / 标题 / 释放条件 / 通道 / 正文） | | | |

**写作约束**（这四条是"不要一次讲完"的落地）：
1. 单条 40~120 字，**一条只讲一个信息点**。
2. 不解释机制，只给画面。数值由 UI 说，故事由文本说。
3. 前 10 条不出现"异世界""猫神""记忆容器"这类设定词，先建立"这家店有点不对"的氛围。
4. 结局文本只在最后 3 条出现，且**不总结，只描述一个动作**。

---

## 11. 结局

| 结局 | 触发 | 实现 |
|---|---|---|
| 两界桥梁 | 转生等级 ≥ 10 且幸福感 ≥ 50,000 | 一个隐藏 `AchievementDefinition` + 3 条 `Popup` 叙事 |

3 个文本变体按幸福感档位分岔（50,000 / 200,000 / 1,000,000），
**都是好消息**——这是唯一一个不给"坏结局"的包，它是整个宇宙的休息室。

---

## 12. 零引擎改动核对表

| 需要的能力 | 现状 | 结论 |
|---|---|---|
| 建筑 / 升级 / 成就 / 增益 / 随机事件 | ✅ 全有 | 直接填表 |
| 转生（重装修）| ✅ `PrestigeSystem` | 改文案即可 |
| 常客记忆（跨转生保留）| ✅ `UpgradePersistence.Permanent` | 直接用 |
| 离屏收益 / 存档 / 事件 / 通知 | ✅ | 直接用 |
| 第二资源"幸福感" | ✅ `Counters` + `IGameModule` | **零核心改动** |
| 幸福感驱动修饰符 | ✅ `ScalingSource.CustomCounter` | 直接用 |
| 幸福感作为解锁条件 | ✅ `NumericMetric.Counter`（C1 已落地） | 直接用，**有进度条** |
| 结局 | ✅ 用成就 + 叙事实现 | 直接用 |

**合计：48 条升级 + 45 条成就 + 10 座建筑 + 5 条增益 + 8 条事件 + 50 条叙事（待 S-B）+ 1 个模块。**

---

## 13. 验收清单

### 既有测试会守住什么（不需要改测试）

| 测试 | 对本包的约束 |
|---|---|
| `ContentTests.NekoContent_BuildingCurveIsSane` | 相邻价格倍率 5~20、产量倍率 3~12、第 3 座起价格倍率 > 产量倍率 |
| `NekoContent_UnlockThresholdsRiseWithTier` | 建筑解锁门槛必须递增 |
| `NekoContent_HeavenlyUpgradesArePermanentAndChipPriced` | `Permanent` 升级必须用转生货币 |
| `NekoContent_IdsAreGloballyConsistent` | id 唯一 |
| `DanglingReferences_AreRejected` | 解锁条件不得引用不存在的 id |
| `SimulationTests.SixHourGreedyRun_IsStableAndProgresses` | 6 小时模拟必须"有事发生"且无 NaN/∞ |

### 新增测试（✅ 已实现于 `CafeContentTests`，2026-09-24）

| 计划测试 | 实际用例 | 断言 |
|---|---|---|
| `Cafe_HappinessModuleAccumulates` | `Cafe_HappinessModuleAccumulates` | `Simulate(600)` 后 `Counters["happiness"] ≥ 500` |
| `Cafe_HappinessSurvivesAscension` | `Cafe_HappinessSurvivesAscension` | 店休后幸福感保留、建筑清空 |
| `Cafe_PermanentUpgradesNeedPrestigeCurrency` | `CafeContent_MemoryUpgradesArePermanentAndChipPriced` | 5 条常客记忆的 `Currency` 全为 `PrestigeChips` |
| `Cafe_EventWeightsArePositiveFeedbackDominant` | `CafeContent_EventWeightsArePositiveFeedbackDominant` | 正反馈权重 / 总权重 ≥ 0.85 |
| `Cafe_NarrativeRevealConditionsAreReachable` | ⏸ 待 S-B（叙事系统落地后补） | 每条叙事的 `Reveal` 不含 `ConstantCondition(false)` |

另外补了规模基线、id 一致性、曲线区间、解锁递增、平衡参数、增益引用、
离线补算（`Cafe_HappinessModuleAccruesOffline`）、幸福感进度条（`Cafe_HappinessGatesUpgradesWithProgressBar`）
与 6 小时长跑（`SimulationTests.CafeSixHourGreedyRun_IsStableAndProgresses`）。

### 手动验收

```powershell
.\tools\build.ps1                       # 构建 + 全部测试
.\tools\play.ps1 --package cafe         # 交互试玩
.\tools\play.ps1 --package cafe --simulate 21600 --auto   # 6 小时曲线报告
```

---

## 14. 模板用法：换一个包要改哪几行

把本包复制成 #2~#10 时，需要动的只有这些（**都不涉及引擎**）：

| 改动点 | 说明 |
|---|---|
| `WithCurrency` / `WithPrestigeCurrency` | 货币名与转生货币名（小鱼干永不变；"常客的信"按包换） |
| `Balance` | 该包的数值性格（见 §3 的差异说明） |
| 建筑表 | 换 id/名/图标/描述；**价格与产量的比值必须保持在验证过的区间内** |
| 升级表 | 换文案；强化档的生成规则可以复用不改 |
| 成就表 | 换分类名与阈值 |
| 增益 / 事件表 | 换名称与目标建筑 |
| 叙事表 | 全部重写（这是主要工作量） |
| `IGameModule` | 只有需要第二资源的包才要（咖啡馆=幸福感，公司=士气，图书馆=被阅读度） |
| `EraDefinition`（若该包需要分层） | 只有 #2~#10 需要；#1 不需要 |

**唯一有技术含量的部分是建筑价格/产量比值**——它决定整个包的节奏，且必须落进回归测试的区间。
其余全是文案与表格。
