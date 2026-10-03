# Web 界面反馈：建筑的「单个产速」在界面上不存在

> 记录时间：**2026-10-03 晚** ｜ 反馈人：**用户**（在 `http://127.0.0.1:5273` 玩**末世**包时提出）
> ｜ 核实：同一次会话，对着**运行中的真宿主**取快照，再逐字段对回源码
>
> **用户的观察（原话）**：
>
> > "每个建筑只会显示各自的总产速而并没有单个建筑的产速"
>
> **这句话对着的是哪一行**：建筑卡片上那行 `已有 20 · 90/s · 占 41%`——
> 三个数分别是**持有数量、这座建筑的总产量、占总产量的比例**，
> 唯独没有"其中每一个产多少"（那三个数里，`90 ÷ 20 = 4.5`）。
> **终端宿主有这一列**（详情面板的 `单个 4.5/s`），Web 没有。
>
> **两层，代价差一个数量级**（本文把两层分开写，实现可以分两步走）：
>
> | 层 | 是什么 | 动哪里 | 版本 |
> |---|---|---|---|
> | **一** | 这个数**已经算好、已经推给前端**了，Web 只是没画 | `games/hosts/Web/wwwroot/app.js` 一处 | **patch**（公开 API 一行不动） |
> | **二** | 未持有的建筑，这个数**恒为 0**——最需要它的时候它什么都给不出 | `engine/core/`（新增公开字段） | **minor**（公开 API 只增不改） |
>
> 已登记：第一层 = [OPEN_WORK](OPEN_WORK.md)《下一步：唯一权威清单》的 **W11**，
> 第二层要先拍口径 = 同表的 **D11**。

---

## 1. 现场：那一局到底显示了什么

复现（本文所有数字都出自这条命令，宿主在 5273 上跑着）：

```powershell
$s = Invoke-RestMethod 'http://127.0.0.1:5273/api/snapshot?package=apocalypse'
$s.buildings | Select-Object id, owned, unitPrice, cpsEach, cpsContribution, cpsShare | Format-Table
```

用户那一局（末世包、纪元 **2/5「第 2 次重启 · 电」**、总产量 **218.025/s**，
生效中的加成：`oil_lamp` 所有建筑产量 ×1.5、纪元修正 所有建筑产量 ×1.5）：

| id | 名称 | owned | 单价 | `cpsEach` | `cpsContribution` | `cpsShare` |
|---|---|---|---|---|---|---|
| `ruins` | 🧱 废墟 | 9 | 52.77 | **0.225** | 2.025 | 0.93% |
| `generator` | 🔌 发电机 | 20 | 1,636.65 | **4.5** | 90 | 41.28% |
| `water_purifier` | 💧 净水器 | 7 | 2,926.02 | **18** | 126 | 57.79% |
| `shelter` | 🛖 避难所 | 0 | 12,000 | **0** | 0 | 0 |
| `greenhouse` | 🌱 温室 | 0 | 130,000 | **0** | 0 | 0 |

**Web 画出来的**（`app.js:1098`，且**只在 `owned > 0` 时这一行才出现**）：

```
已有 20 · 90/s · 占 41%
```

**终端画出来的**（`TerminalUi.cs:453-454`，建筑面板的详情行）：

```
🔌 发电机　单个 4.5/s　合计 90/s 占 41.3%　本次花费 1,636.65
```

同一份数据、同一行快照、**同一个字段**：终端画了，Web 没画。

---

## 2. 第一层：数已经在那儿了，Web 只是没画

三处互相独立的证据：

1. **引擎算好了**——`BuildingView.CpsEach`（`engine/core/Views/Views.cs:51`，"单个建筑当前的实际产量"），
   在 `GameViewFactory.cs:72` 赋值。
2. **协议推了**——`cpsEach` 就在 `/api/snapshot` 的每个 `buildings[]` 行里。
   `tools/fixtures/web-snapshot.json` 是**真宿主抓的夹具**，里面逐行都有 `"cpsEach":0`。
3. **终端画了**——`games/hosts/Demo.Cli/TerminalUi.cs:453`。

而 **Web 前端从头到尾没有引用过这个字段**：`games/hosts/Web/wwwroot/` 下 grep `cpsEach` **零命中**；
建筑卡片上也没有任何 tooltip 藏着它（`app.js` 里 9 处元素 `title=`，没有一处带产量）。
界面上唯一显示建筑产量的地方就是那行 `share`（`已有 N · X/s · 占 Y%`）。

**代价**：改一处拼字符串。按 `VERSIONING` §2 那张表，**公开 API 一行不动 = patch**。
具体摆法见 §6（那是个小决定，不是既成事实）。

---

## 3. 第二层：未持有的建筑，这个数是 0

`避难所` 标价 12,000，`cpsEach` 是 **0**；`温室` 标价 130,000，也是 **0**。

**为什么这是个问题**：玩家盯着两座**还没买**的建筑比价时，能拿到的产量信息是 0。
"买哪一个划算"要的正是这个数——而它恰好在"还没买"的时候没有值。
今天未持有的建筑上能看到的，只有价格与解锁条件（`unlockHint（进度）`）。

**前端自己算不出来**：快照里 `buildings[]` 的**全部**字段是

```
id, name, icon, description, category, owned, isUnlocked, hiddenUntilUnlocked,
unlockHint, unlockProgress, unitPrice, batchAmount, batchPrice, canAfford,
cpsEach, cpsContribution, cpsShare, nextMilestoneAt, nextMilestoneName,
upgradeIds, sellRefundRate, isVisible
```

——"基础产量"（引擎内部的 `BuildingProduction.BaseCps`）**不在其中**，
前端也**不许自己乘倍率**（引擎侧既有的规矩：前端不解释服务端的字符串与序数；
产量是玩法规则，更不该在浏览器里复算一遍）。
所以这一层**必须动 `engine/core/`**：给 `BuildingView` 增一个"买下这一个能产多少"的字段。

**代价**：公开 API 只增不改 ⇒ **minor**（升版本 + 重生成 `PublicApi.txt` + 用例 + 两个宿主接线 + 文档）。
增什么字段、叫什么名字，取决于 §4 那个口径先定下来。

---

## 4. 口径：这个数到底是什么（动第二层之前必须先定）

`cpsEach` 今天在**两种口径**之间切换，而这一点从字段名上看不出来：

- **持有 > 0 时**：`bp.Cps / bp.Count` = **平均每个**的当前产量；
- **持有 == 0 时**：`EffectiveUnitCps(...)` = **0**（成因见 §5.1）。

**"平均每个"含全部加成**，不是内容包里的基础值。用上面那一局验一遍就很清楚
（`ProductionCalculator`：`单个 = (基础 + 加法项) × 建筑倍率 [× 建筑乘方]`，再 `× 全局倍率`）：

| 建筑 | 基础 `BaseCps` | 油灯（×1.5） | 纪元修正（×1.5） | 它自己的升级 | `cpsEach` 实测 |
|---|---|---|---|---|---|
| 🧱 废墟 | 0.1 | ×1.5 | ×1.5 | — | **0.225** ✓ |
| 💧 净水器 | 8 | ×1.5 | ×1.5 | — | **18** ✓ |
| 🔌 发电机 | 1 | ×1.5 | ×1.5 | `generator_tier1` ×2 | **4.5** ✓ |

### 4.1 「平均」和「边际」在这套内容里**真的会分叉**

"平均每个"（今天的 `cpsEach`）**不等于**"再买一个，总产速会多多少"。这不是理论担忧——
内容里已经有两种**按持有数量成长**的倍率（`Scaling(ScalingSource.BuildingCount, …)`，末世包自己的
`Upgrades.cs` 里就有三条）：

| 形态 | 例子 | 买下第 N 个的**真实**收益 |
|---|---|---|
| **自指全局** | `secret_recipe`（猫咖）：`GlobalPercent(0, Scaling(BuildingCount, 0.01, Cap: 200, Id: "cat_tree"))`——每个猫爬架让**所有**产量 +1%，上限 200 | `cpsEach` **＋ 全体产量的 1%** |
| **跨建筑**（link） | `ruins_to_tower`（末世）：`BuildingPercent("data_tower", 0, Scaling(BuildingCount, 0.02, Cap: 100, Id: "ruins"))`——每有一块废墟让**数据塔**产量 +2% | 废墟自己的 `cpsEach`，**外加数据塔那一份** |

也就是说：同一个"买一个"的动作，回答"我多了多少产量"要比 `cpsEach` 复杂——
它取决于**已买了哪些升级**。前端连 `Modifiers` 都看不到，这条路它自己走不了。

### 4.2 所以要定的是这个

| 口径 | 含义 | 今天的实现 | 前端要的字段 |
|---|---|---|---|
| **平均** | 这座建筑现在平均每个产多少 | `owned > 0` 时就是它 | 已经有（`cpsEach`） |
| **边际** | 再买一个，**总产速**会多多少 | **没有**（`owned == 0` 时给 0） | 要新增；而且要含对方程里所有受数量影响的倍率 |

> **建议（供拍板，不是结论）**：第二层只做**平均**口径的"买前可见"，
> 也就是"未持有时显示**如果买下 1 个、它自己**能产多少"（不含它将触发的全局联动），
> 理由有三条：① 它与今天的语义连续、与终端的 `单个 X/s` 是同一个数；
> ② 它不承诺"总产速会涨这么多"——那句话在有联动升级时是**错的**；
> ③ 真要边际值，那是另一个字段、另一次决定（而且得先想清楚"联动算不算进去"）。

---

## 5. 清单外的发现（核实第一层时顺带挖到的）

### 5.1 `EffectiveUnitCps` 是个死分支，三个参数有两个没人用

`GameViewFactory.cs:72`：

```csharp
CpsEach = bp.Count > 0 ? bp.Cps / bp.Count : EffectiveUnitCps(bp, priceMultiplier, production),
```

`GameViewFactory.cs:504-505`：

```csharp
private static double EffectiveUnitCps(BuildingProduction production, double priceMultiplier, ProductionBreakdown breakdown)
    => production.Count > 0 ? production.Cps / production.Count : 0;
```

对着看：`Count > 0` 时，三元的两边**算出来是同一个值**（都是 `Cps / Count`）；
`Count == 0` 时右边给 0。所以整个表达式**等价于** `bp.Count > 0 ? bp.Cps / bp.Count : 0`，
即 `EffectiveUnitCps(...)` 自身——那个三元是**冗余**的。

而 `EffectiveUnitCps` 的**三个参数里有两个（`priceMultiplier`、`breakdown`）一次都没被用过**。
签名像是一个"**算下一个买下去值多少**"的投影（连价格倍率都在参数里了），
函数体却只做了平均——§4.2 的"边际"口径在这里是**打算过、没做完**的痕迹。

### 5.2 这个字段没有任何守卫

全仓库引用 `CpsEach` 的地方只有三处：定义（`Views.cs:51`）、赋值（`GameViewFactory.cs:72`）、
终端渲染（`TerminalUi.cs:453`），加上生成的 `PublicApi.txt` 与真宿主抓的夹具。
**`engine/tests/` 里零命中——没有一条用例断言过它的值。**

含义有两面：

- "未持有恒为 0"这件事**没有任何东西会发现**——它不是有人决定"未持有就显示 0"，是没人看过；
- 反过来，动 §3 那个字段时也**没有现成的守卫会红**：第二层落地时那条用例要**新写**，
  而且按本仓库的既有教训（`UpgradeRows_ReportTheRightWalletForEveryPack`），
  它该是**横扫全部包**的那一类——单包逐项会漏掉别的包。

---

## 6. 第一层建议的形态（供拍板）

三种摆法，代价都是"改一处字"：

| 摆法 | 长什么样 | 好处 | 代价 |
|---|---|---|---|
| **A（建议）** 并进现有那行 | `已有 20 · 单个 4.5/s · 合计 90/s · 占 41%` | 一行说全；与终端详情面板**逐项对齐** | 那行只在 `owned > 0` 时出现——**未持有照样看不到**（那是第二层的事） |
| **B** 单独常显一行 | 未持有的建筑也画一行 | 位置固定、买前可见 | 今天它会画出 `单个 0/s`——一句看着像"这建筑不产钱"的**假话**。要么先做第二层，要么这行在 `owned == 0` 时写别的（如 `—`） |
| **C** 卡片 tooltip | 悬停才显示 | 不占版面 | 摸不到、读屏读不到；"观感没人看过"这条（`STATUS` §6）会更难验 |

---

## 7. 没做的 / 没验证的（诚实清单）

- **没量版面**：加上这一段字之后卡片会不会挤、窄屏会不会折行，**没有看过**——
  本机起不了浏览器（`STATUS` §6），"好不好看"在这仓库里没有守卫。
- **没在真浏览器里看过**：这一层落地的验收只能是 `node tools/web-smoke.mjs`（DOM 桩）
  ＋ `tools/api-test.ps1`（真宿主）＋ **一双眼睛**。
- **没逐包看过**：本文的实测数字全部来自**末世包那一局**。别的包的 `cpsEach` 形态
  （尤其"第二资源"型包）没有逐个核对过；§4.1 的两条例子是**读内容定义**读出来的，不是在局里验的。
- **没量第二层的实现代价**：§3 只说"要动 `engine/core/`、是 minor"，
  没有写具体字段设计——那要等 §4.2 的口径定了才有意义。
- **没解释 `priceMultiplier` 为什么会在 `EffectiveUnitCps` 的签名里**：只能看出它像个未完成的打算，
  查不到它的来处（本仓库没有那段历史）。

---

## 8. 怎么复现本文的证据

```powershell
# 宿主（本机缺 ASP.NET 8 运行时，必须带这个变量，见 STATUS §7 第 8 条）
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet run --no-build --project games\hosts\Web -- --urls http://127.0.0.1:5273

# 那一局的快照（§1 的表）
$s = Invoke-RestMethod 'http://127.0.0.1:5273/api/snapshot?package=apocalypse'
$s.buildings | Select-Object id, owned, unitPrice, cpsEach, cpsContribution, cpsShare | Format-Table
$s.upgrades  | Where-Object owned -gt 0 | Select-Object id, effectSummary

# "这个字段没人画、也没人守"
#   grep cpsEach games/hosts/Web/wwwroot/     → 零命中
#   grep CpsEach engine/tests/                → 零命中
```
