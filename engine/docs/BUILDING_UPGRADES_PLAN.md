# 建筑升级系统（building upgrade system）

> 状态：**先写方案，再动手**。本文按仓库惯例分五段：问题 / 已经有什么（带证据）/
> 缺的是什么 / 提案与守卫 / 明确不做。
> 动手那半的落地结果与判别力证据写在 §5 末尾与 §8。

---

## 0. 问题

原话：「做一下建筑的升级系统」。

这句话至少有三种读法，实施面差得很远：

| # | 读法 | 一句话 |
|---|---|---|
| A | **每座建筑有自己的升级等级** | Lv1..LvN，越买越贵，逐级抬升这一座建筑的产量 |
| B | **每座建筑有自己的升级面板** | 把"这座建筑的升级"挂到建筑上：看得见、点得到，不再混在一张长列表里 |
| C | **建筑升级用另一种货币** | 建筑专用资源，与主货币 / 转生货币并列 |

**采用的读法是 B，并按 §1 的证据说明 A 不需要新机制、C 没有依据**（取舍见 §3）。

---

## 1. 先说清楚：已经有什么（逐条带证据）

**这套框架的升级机器已经很完整，"每座建筑专属升级"的机制也已经在树上跑了很久。**
不读这一节就会重复造轮子。

### 1.1 `UpgradeDefinition` 已经是"建筑专属升级"的完整载体

`engine/core/Content/Definitions.cs:69` 起的这个记录带齐了一个升级需要的全部东西：
`Id` / `Name` / `Price` / `Currency` / `Persistence` / `MaxPurchases` / `PriceGrowth` /
`Unlock` / `Modifiers` / `Tags` / `Category` / `Tier` / `HiddenUntilUnlocked`。

其中两条与"等级"直接相关，**今天就能用**：

- `MaxPurchases`（`:93`，"可购买次数上限（1 = 唯一升级）"）；
- `PriceGrowth`（`:96`，"重复购买时的价格增长"）。

### 1.2 每座建筑的强化档已经存在，而且是批量生成的

11 个包各有一个 `BuildingTierUpgrades()`（如 `engine/content/Apocalypse/Upgrades.cs:38-67`），
形如：

```csharp
foreach (BuildingDefinition building in Buildings.All)
    foreach ((int required, double priceFactor, string prefix) in tiers)
        yield return new UpgradeDefinition
        {
            Id = $"{building.Id}_tier{required}",
            Price = building.BasePrice * priceFactor,
            Unlock = UnlockCondition.BuildingsAtLeast(building.Id, required),
            Modifiers = [Modifier.BuildingMultiplier(building.Id, 2)],
            Category = $"building:{building.Id}",     // ← Definitions.cs:107 文档化的那条通道
            Tier = required,
        };
```

门槛与文案**逐包不同**（多数是 1/10/25，咖啡馆与猫咖是 1/5/25，梦境连价格系数都不同），
这是内容侧的刻意差异，也是 A3 想要的形态。

### 1.3 修饰符管线本来就按"单座建筑"生效

- `ModifierTarget.BuildingCps(id)`（`ModifierTarget.cs:72`）就是"只作用这一座"；
- `Modifier.BuildingMultiplier` / `BuildingPercent` / `PriceMultiplier(factor, buildingId)`
  （`Modifier.cs:187-213`）是作者用的三个快捷构造；
- 折叠公式 `v = (base + Flat) × (1 + ΣAdditivePercent) × ΠMultiplicative ^ ΠPower`
  在 `ModifierSet.cs:9`（文档）与 `ModifierAccumulator.Apply`（`:35-41`）；
- `ProductionCalculator.Compute` 逐建筑取 `ModifierTarget.BuildingCps(def.Id)`（`:83-95`）。

**关键一条**：`ModifierResolver.Build` 对已购升级是**按购买次数重复计入**的
（`ModifierSet.cs:141-150`，注释原文"按已购次数重复计入"）。
所以"买 N 次的建筑专属升级"今天就有正确的数值语义——**读法 A 不需要任何新机制**。

### 1.4 构建期已经会拦住"引用了不存在的建筑"——但只拦修饰符那一条路

`GameContentBuilder.ValidateModifiers`（`:925` 起）对 `BuildingCps` / `BuildingPrice` 目标
会检查建筑存在（`:956`：`修饰符引用了不存在的建筑「X」`）。`CONTENT_AUTHORING.md:344-345`
把这件事记成了一条原则：**"这类错误以前是静默失效（修饰符算了但没人受影响），现在拦在构建期。"**

**但它不看 `Category`。** 见 §2。

### 1.5 "这座建筑的下一个里程碑"已经算出来了，而且只有终端宿主在用

`GameViewFactory.FindNextMilestone`（`:475-500`）会扫全部升级、挑出"以这座建筑数量为解锁阈值"
的最近一条，写进 `BuildingView.NextMilestoneAt` / `NextMilestoneName`（`Views.cs:60-63`）。
消费者只有一个：`games/hosts/Demo.Cli/TerminalUi.cs:440-443`
（「再买 N 个解锁「X」」）。**Web 前端一次都没读过它。**

### 1.6 Web 前端的现状：一张扁平长列表

`app.js:1074-1116` 的 `renderUpgrades()` 把 `state.upgrades` 里 `isVisible && !isPermanent`
的全部行画成一张列表（咖啡馆 48 条，全包合计 534 条，其中大多数是建筑强化档）。
建筑行（`app.js:899-959`）只有三件：📖 看故事、卡片买建筑、故事框。

### 1.7 量出来的规模（2026-10-03，一次性探针跑真内容；探针已删）

| 事实 | 值 |
|---|---|
| 升级总数（11 包） | **534** |
| 其中声明了 `building:<id>` 的 | **312**（58%） |
| 有升级轨的建筑 | **104 / 104 座，每座恰好 3 条** |
| 分类指向不存在的建筑 | **0** |
| 分类与修饰符目标不一致（挂在 A、效果在 B） | **0** |
| 轨内 `Tier` 与声明顺序不一致 | **0** |
| 全量快照（JSON 字节） | 猫咖 48,220 / 咖啡馆 59,905 / 九命 72,436 / 其余 59.5K~63.9K |
| 那一栏移走建筑档之后还剩多少 | 每包 **13~19 条**（11 个包没有一个是空的） |
| 九命闲置一 tick 的增量 | **188 字节 / 全量 64,471 字节 = 0.3%**（判据 <5%；`buildings` 根本不进增量帧） |

**这两个 0 很重要**：它们说明 §4.1 那三条校验在今天的真内容上**一条都不会误伤**——
它们守的是将来写错的那一刻，而不是"现在就有一堆要修"。

---

## 2. 缺的是什么：一条**没有校验、没有索引、没有消费者**的关系

`UpgradeDefinition.Category` 的文档注释写的是「分组（UI 用，例如 `"building:catnap"`）」
（`Definitions.cs:107`）——**引擎自己把这条字符串定义成了"这座升级属于哪座建筑"的通道**，
11 个包都在用它。而今天：

| 环节 | 现状 | 证据 |
|---|---|---|
| 构建期校验 | **一处都没有**。`Category` 是自由字符串，写成 `building:nope` 不会报错 | `GameContentBuilder.cs` 全文没有一处读 `u.Category`；`ValidateModifiers` 只认 `ModifierTarget` |
| 索引 | **没有**。"按建筑取它的升级"这个查询在 `GameContent` 上不存在 | `GameContent.cs:77` 只有 `UpgradeById` |
| 快照 | `UpgradeView.Category` / `Tier` **上了线、没有任何消费者** | 赋值在 `GameViewFactory.cs:111-112`；`grep '\.Category' / '\.Tier'` 在 `engine/core` 与 `games/` 下只命中赋值点 |
| 前端 | `category` / `tier` / `nextMilestoneAt` **一次都没读** | `grep -E 'category\|tier\|milestone'` 在 `games/hosts/Web/wwwroot/*.js` 里 **0 命中**（2026-10-03 实测） |

**这三条合起来是一次典型的静默失效**：作者写了那条关系，引擎把它原样搬到线上，
两端都没有人读——于是"建筑专属升级"在玩家眼里根本不存在，而**没有任何东西会因此变红**。
这与 `CONTENT_AUTHORING.md:344` 记下的那次（修饰符 id 写错、以前静默失效）是同一类，
只是漏在了 `Category` 上。

代价是可量化的：**534 条升级里有 300 条左右是建筑强化档**，全部挤在同一张扁平列表里；
玩家看不出哪一条属于哪座建筑，也看不出"我现在能买的、属于这座建筑的是哪一条"。
终端宿主至少还有那一行里程碑提示，Web 连它都没有。

---

## 3. 读法的取舍

- **读法 A（每座建筑自己的等级）不选**：它的**机制已经存在**（`MaxPurchases` + `PriceGrowth`
  + `ModifierResolver` 按次数重复计入，见 §1.1 / §1.3），作者今天就能写出来。
  把它做成"新的核心机制"就是重复造轮子；把它做成"给 11 个包各加一条等级线"则是
  **内容改动**，不是这次要修的缺口，而且会凭空造出 300 多条新升级（快照体积、文案外置、
  曲线回归全都要跟着动）。**留在 §6「明确不做」里，并写清它今天怎么做。**
- **读法 C（建筑专用货币）不选**：树里**没有任何依据**——没有需求记录、没有设计文档提过、
  也没有一条现成通道（`UpgradeCurrency` 只有主货币与转生货币两个成员，
  `Definitions.cs` 的 `UpgradeCurrency`）。凭空加第三种货币会同时改存档与公开 API。
- **读法 B 选**：它是**唯一一条被证据指出来的缺口**——引擎文档化了这条关系（`:107`）、
  11 个包写了它、快照把它推到了线上，而**没有一处校验它、没有一处索引它、没有一处读它**。
  补这条关系既不动数值、也不动存档，还能顺手把两个"上线了没人读"的字段
  （`UpgradeView.Category` / `Tier`）变成有消费者的东西。

> 一句话：**这不是"从零做建筑升级"，而是"把已经写下来的建筑升级关系接上"。**

---

## 4. 提案

四步，全部是纯新增，且`engine/core` 不认识任何具体包。

### 4.1 核心：把 `building:<id>` 这条既有约定变成**被校验、被索引**的

**新增公开类型** `NekoClicker.Core.Content.UpgradeCategories`：这条约定今天以
`$"building:{building.Id}"` 的形式散在 11 个包里，把它收成**一个出处**。

```csharp
public static class UpgradeCategories
{
    public const string BuildingPrefix = "building:";
    public static string ForBuilding(string buildingId);          // "building:" + id
    public static bool TryGetBuildingId(string? category, out string buildingId);
}
```

**新增公开成员** `GameContent.UpgradesByBuilding`（索引）与
`GameContent.UpgradesForBuilding(string buildingId)`（查询）。
排序规则：**先按 `Tier`，再按声明顺序**——这给了 `Tier`（"仅用于排序/展示"，
`Definitions.cs:111`）第一个真实消费者，也让"档位"这件事有一个确定的先后。
（今天 11 个包的两者顺序一致，所以这是把既有事实写下来，不是改行为；见 §5.1 的守卫。）

**新增构建期校验**（`GameContentBuilder`）：任何声明了 `building:` 前缀的升级，
只要**建筑 id 为空**或**建筑不存在**，就抛 `GameContentValidationException`，
错误信息点名**升级 id** 与**那个坏掉的建筑 id**。

**并且校验"这条关系是真的"**：一条 `Category = "building:X"` 的升级，
若它的修饰符里**没有一条**作用于建筑 X（`BuildingCps` / `BuildingPrice`），也报错。
理由：这正是那条静默失效的形态——分类指向 A、数值作用在 B，界面上它会挂在 A 名下、
而玩家买到的效果在 B 上，**两端都不会报错**。
（允许 `BuildingPrice` 是因为"只给这座建筑打折"是完全合理的建筑专属升级。）

### 4.2 核心：`BuildingView.UpgradeIds`

`BuildingView` 新增 `IReadOnlyList<string> UpgradeIds`，内容就是
`UpgradesForBuilding(Id)` 的 id 序列。

**为什么不让前端自己去按 `category` 前缀筛**：这正是仓库已经立过三次的规矩——
前端**不许解释服务端的字符串/序数**，服务端该直接给旗子或名字：

| 先例 | 位置 | 规矩 |
|---|---|---|
| `UpgradeView.UsesPrestigeCurrency` / `CurrencyName` / `CurrencyIcon` | `Views.cs:93-107`，1.4.0 | 前端不解释 `UpgradeCurrency` 的序数 |
| `GameSnapshot.ModeName` | `Views.cs:603-618`，1.7.0 | 前端不解释 `PurchaseMode` 的序数 |
| **`BuildingView.UpgradeIds`** | 本文 | 前端**不解析** `"building:<id>"` 这个约定 |

给 **id 列表而不是嵌套的完整 `UpgradeView`**：完整的行已经在 `upgrades[]` 里推过了，
再嵌一份等于同一份数据推两遍（快照体积、增量协议都不划算）。
id 是**服务端数据**，前端按 id 去 `upgrades[]` 里取行——这是取数，不是解释约定。

**字节预算**：`UpgradeIds` 是**静态**的（只随解锁内容变），所以它**不会进挂机时的增量帧**；
它只让全量帧变大，而那条守卫（`WebSnapshotProtocolTests.IdleDelta_StaysSmallInBytes_EvenWhileProgressDrifts`，
判据 `deltaBytes * 20 < fullBytes`）是**比值**——全量变大只会让它更宽松。
§5 会另加一条"它不进闲置增量"的守卫，避免将来有人把它做成每帧漂移的派生字段。

### 4.3 宿主：Web 前端给每座建筑一条"升级轨"

建筑行从「📖 + 卡片 + 故事框」变成「📖 + ⬆ + 卡片 + 故事框 + 升级轨」：

- **⬆ 是第二个真 `<button>`**（与 📖 并列，`aria-expanded` / `aria-controls` 同规格），
  展开的是**这座建筑自己的升级列表**：每行是名字 / 价格 / 已购次数 / 效果摘要，
  点一下发 `upgrade` 命令（**不是** `buy`）。
- **没有升级的建筑不给这个按钮**（与"没有说明就不给 📖"同一条规矩：不做点开空空如也的按钮）。
- **徽标**：⬆ 上带一个数字，是这座建筑**当前买得起**的升级条数——
  于是"哪座建筑现在有东西可买"不必展开也看得见。
- **两个手势严格分开**（与上一条前端改动同一套判别力）：点 ⬆ 不发 `buy`，点卡片不展开升级轨。
- **展开状态与 DOM 复用沿用故事框那一套**（`openStories` + `buildingRows`）：状态按建筑 id 记、
  节点按 id 复用，快照每 250ms 一帧不会把展开的框抖回去。
- **扁平"升级"面板不再重复渲染建筑强化档**，改为一行指路文案。这不是新规矩，是把
  `renderPermanent()` 已经写在注释里的那条规矩（"否则一条线会被两处渲染"，
  `app.js:1066-1068`）**照做一遍**：一条线只有一个家。
  前端判断"这条升级是不是建筑专属"用**服务端给的** `upgradeIds` 求并集，
  **不解析** `category`。

### 4.4 内容：**一行都不加**

这次**不新增任何升级定义**，因此：

- `engine/content/*/text.json` **不动**（没有新的玩家可见文案；
  新增的只有宿主自己的界面字，与 `index.html` 里已有的「建筑 / 升级 / 永久 / 图鉴」同类，
  内容包的 `text.json` 里从来没有这类界面字）；
- 每包的升级条数、曲线、成就、存档格式**全部不变**；
- `ContentTests` 那些写死条数的守卫**不需要跟着改**——这本身就是"没有偷偷改内容"的证据。

---

## 5. 守卫与判别力

### 5.1 新增守卫（`engine/tests/BuildingUpgradeTests.cs`）

| 守卫 | 守什么 | 为什么它不会误绿 |
|---|---|---|
| `BuildingCategories_ResolveToRealBuildings_InEveryPack` | 11 个包逐条：`building:` 前缀必须指向真实建筑 | 直接读真内容，不是合成夹具 |
| `BuildingCategory_WithAnUnknownBuilding_FailsLoudly` | 合成内容 `building:ghost` → 异常信息里**同时**出现升级 id 与 `ghost` | 断言的是消息内容，不是"抛了异常" |
| `BuildingCategory_WithAnEmptyId_FailsLoudly` | `building:`（空 id）→ 点名升级 id | 同上 |
| `BuildingCategory_WhoseModifiersIgnoreTheBuilding_FailsLoudly` | 分类指向 A、修饰符作用在 B → 点名两者 | 这条是"静默失效"的正脸 |
| `BuildingView_CarriesTheTrack_ForEveryBuilding` | 每一座建筑的 `UpgradeIds` == `UpgradesForBuilding(id)` | 逐座比，空列表也算不符 |
| `EveryPack_HasABuildingWithATrack_AndTheTrackIsOrdered` | 每包至少一座建筑有轨；且轨内的 `Tier` 单调不减 | 防"这套东西其实是空的"（假绿） |
| `UpgradeIds_DoNotEnterTheIdleDelta` | 闲置一帧的增量里**不含** `buildings` | 防有人把它做成每帧漂移的字段 |

### 5.2 判别力（**故意改坏真树**，看具体的红，再还原）

三条注入，逐条记录**具体的红**：

1. 把某个真实包的一条 `Category = $"building:{building.Id}"` 改成 `"building:nope"`
   → 构建期异常点名该升级 id 与 `nope`；
2. 把某条改成 `"building:"` → 点名该升级 id；
3. 让 `BuildingView.UpgradeIds` 恒为空表 → `BuildingView_CarriesTheTrack_ForEveryBuilding` 红。

（实际执行与红的信息抄在 §7。还原后以 `git status` 为空对照。）

### 5.3 前端守卫（`tools/web-smoke.mjs`）

新增一节，照 §0.10 故事框那批的判别力规格：点 ⬆ 不发 `buy`、展开只影响这一行、
重画不丢展开态、节点被复用、没有升级就不给 ⬆、扁平列表不再出现建筑强化档、
徽标数 == 买得起的条数、真实快照推过 `render()` 不抛异常。

### 5.4 端到端（`tools/api-test.ps1`）

新增一项：真宿主的 `buildings[].upgradeIds` 是字符串数组，且其中每个 id 都能在
`upgrades[].id` 里找到（**两段各自对得上**，而不是只看键存在）。

### 5.5 夹具不是谎话（`tools/fixtures/web-snapshot.json`）

前端开始用 `upgradeIds` 之后，那份"真宿主抓下来的快照"必须**真的带上它**——
否则 `web-smoke.mjs` §14 的"夹具不是谎话"守卫就会（正确地）红。
所以夹具要在改动**之后**重新从真宿主抓一次（`tools/web-smoke.mjs` 自己写明它抓的是哪个端点）。

---

## 6. 明确不做

| 不做 | 理由 |
|---|---|
| **不给任何包新增升级** | §4.4：缺口是"关系"，不是"内容量"。加内容会把曲线回归、文案外置、写死条数的守卫一起拖进来 |
| **不新增货币** | 读法 C，无依据（§3） |
| **不把"建筑等级"做成核心机制** | 读法 A 今天就能用 `MaxPurchases` + `PriceGrowth` 表达（§1.1 / §1.3）。要它就该在**内容**里写，而不是在核心里加一个只有建筑才有的新概念 |
| **不改 `BuildingView.NextMilestoneAt` 的语义** | 它是公开 API，语义是"**按建筑数量**解锁的下一档"；把它改成"任何升级"属于"改已有成员的语义"（major）。`UpgradeIds` 是它的**完备兄弟**，两者并存：前者是一句话提示，后者是完整的一条轨 |
| **不删 `UpgradeView.Category`** | 它是 1.4.0 起的公开表面；这次只是给它一个消费者与一条校验 |
| **不把 `FindNextMilestone` 收窄到"只扫本建筑的升级"** | 它按 `NumericMetric.BuildingCount` + `condition.Id == buildingId` 匹配，与 `Category` **是两条独立的线**：收窄会漏掉"以本建筑数量解锁、但分类不指向本建筑"的升级（那是静默的行为改变，不是优化） |
| **不做终端宿主的改动** | 终端宿主已经读了 `nextMilestoneAt`（`TerminalUi.cs:440`）；`UpgradeIds` 是 Web 侧的缺口。终端要跟上是另一件事 |

---

## 7. 未决问题

1. **"建筑强化档从扁平列表移走"是不是该更保守？** 今天它们是扁平列表里的绝大多数行。
   移走 + 指路文案 + ⬆ 徽标是"一条线只有一个家"的直接读法，但**玩家第一次打开页面时
   看到的升级列表会明显变短**。这条只有真人能判；若被否，回退成本很低
   （前端两处过滤条件），且**引擎侧一行都不用动**。
2. **要不要给 `UpgradeCategories` 之外的 `Category` 值也立规矩？** 今天
   `"click"` / `"memory"` / `"era"` / `"ember"` 等是各包自己发明的自由分组，UI 也没读。
   给它们立规矩（枚举？登记？）是**另一个**方案，本文只处理 `building:`。
3. **`Tier` 的语义**：本文把它当"轨内排序键"。若将来有人给它别的意思
   （等级号？），这条排序规则要重新讨论。

---

## 8. 交付

- **版本**：公开 API 纯新增（`UpgradeCategories` 类型、`GameContent.UpgradesByBuilding` /
  `UpgradesForBuilding`、`BuildingView.UpgradeIds`）⇒ **minor**。
  具体取哪个号与证据见本次的 CHANGELOG 条目与 `OPEN_WORK.md` 的登记。
- **顺序**：按 `VERSIONING.md` §4 的七步（改代码 → 定版本 → 改三处版本字段 → CHANGELOG →
  `tools/public-api.ps1` 重生成快照 → `-Strict` → commit）。
- **A3**：`engine/core` 不引用任何内容包、不认识任何具体 id（`ArchitectureTests` 的 A1/A2
  继续守着）；新内容包仍然只需要调用公开 API。
