# 捡到一张纸条：在虚构里教机制（方案）

> 状态：**方案 + 第一片已落地**（试点包 = 末世）。本文是这次改动的**唯一权威**：
> 呈现决定、存档决定、纸条目录、守卫、明确不做，都写在这里。
> 前置阅读：[TEXT_AS_DATA_PLAN](TEXT_AS_DATA_PLAN.md)（文本的家）、
> [VERSIONING](VERSIONING.md)（升哪一位）、[OPEN_WORK](OPEN_WORK.md) 的 **D5 / W9**（存档迁移）。

## 1. 要解决的问题

真人原话（2026-10-05）：

> 「不是要教程弹窗，是**捡到一张纸条**。」
> 「**一系列**——一个机制一张纸条：买东西 → 升级 → 舍命 → 表态 → 导出分享。」
> 「触发时机是**第一次遇到那个情况**，不是攒够多少钱。」
> 「呈现用**新建弹窗系统**。」

所以这是一个**用故事教机制**的入门路径：纸条是**别人留下的东西**，
它靠"暗示"教会玩家一件事，而不是靠"接下来你将学会……"。

**它与库里的两条成文规矩正面相撞**，方案必须在 §3 明确处置、不许含糊：

1. `SAVE_TRANSFER_PLAN.md:260`「Web：复用已有的 sheet，**不新造第二套弹窗**」
   （`index.html:37` / `app.js:633` 各有一句同义的注释）。
2. 存档格式首次真改的代价（`OPEN_WORK` **D5**：迁移机制在生产里**零实现**）。

---

## 2. 侦察事实（都带坐标）

### 2.1 弹窗：树上一套，写在三个地方

| 事实 | 坐标 |
|---|---|
| 只有一套 layer / 卡片 / 关闭 / 主按钮 / 入场动画，三张 sheet 共用 | `wwwroot/app.css:1033`（`.sheet-layer`）、`:1045`（`.sheet`）、`:1075`（`.sheet-head`）、`:1089`（`.sheet-close`）、`:1111`（`.sheet-ok`）、`:1126`（`@keyframes sheet-in`） |
| 规矩的原文 | `index.html:36~37`、`index.html:58`、`app.js:632~633`、`SAVE_TRANSFER_PLAN.md:260~264`、`SHARE_LINK_PLAN.md:268` |
| 三张 sheet | 离线收益 `index.html:26`、表态 `:43`、存档/分享 `:70` |
| 无障碍那一层 | `role="dialog" aria-modal="true" aria-labelledby`（三张都有）、四处 `role="status"` 实时区域（`index.html:67` 数得住）、焦点只在存档那张做（`app.js:651~654` 写了为什么另外两张不做） |
| 两张 sheet 的次序规则 | `app.js:210~213`、`app.js:256~265`（离线先讲完才轮到表态） |
| 键盘 | `app.js:2062` 起（Esc：表态那张） |

### 2.2 引擎里**已经**有一条"事件弹窗 + 待读队列 + 已读集合"的路

这是本次最重要的侦察结论：**要的东西八成已经在树上，只是 Web 前端从来没画过。**

| 事实 | 坐标 |
|---|---|
| 投放通道枚举（`Log` / `Popup` / `Codex` / `EraText`），注释原文就是"以什么形式**打扰**玩家" | `engine/core/Content/LoreEntry.cs:4~17` |
| `Popup` =「事件弹窗，需要玩家点掉。**只给转折点用**」 | `LoreEntry.cs:9~10` |
| 释放：`Reveal.IsMet(...)` 成立就记进 `LoreUnlocked`；通道是 `Popup` 就塞进 `PendingLorePopups` | `engine/core/Simulation/LoreSystem.cs:31~55`（`case LoreChannel.Popup:` 在 `:46~49`） |
| **一旦记进 `LoreUnlocked` 就永不再放**（`if (state.LoreUnlocked.Contains(entry.Id)) continue;`） | `LoreSystem.cs:33` |
| 点掉一条 / 点掉全部 | `LoreSystem.cs:69~80` |
| 检查频率：与成就同频，`AchievementCheckInterval` 秒一次 | `GameEngine.cs:275~277`（闸）、`:293`（`CheckLore()`） |
| 它在快照上已经有位置 | `Views.cs:697`（`GameSnapshot.PendingLore`）、`GameViewFactory.cs:375~386` |
| 宿主命令也早就有了 | `games/hosts/Web/Program.cs:304~316`（`dismissLore` / `dismissAllLore`） |
| **而 Web 前端一次都没画过它** | 整个 `app.js` 里 `pendingLore` 一个字符都没有；它躺在 `tools/web-smoke.mjs` 的 `EMPTY_ARRAY_BLIND_SPOTS`（夹具里那一格是空数组） |
| 终端画了一行 | `games/hosts/Demo.Cli/TerminalUi.cs:248~250`「📖 有 N 段新剧情待读」；在图鉴里按一下就是"读掉它"（`GameSession.cs:400~415`，**点掉时只写标题**） |

**推论**：纸条不需要新的引擎概念、不需要新的存档字段、不需要新的宿主命令。
缺的只有两件事：①"纸条"与"剧情转折"在数据上要能分开；②Web 要有一个画它的地方。

### 2.3 文本的家：`content/<包>/text.json`，`lore` 分区

| 事实 | 坐标 |
|---|---|
| 全部玩家可见散文都在 `content/<包>/text.json`，十一类分区 | `TEXT_AS_DATA_PLAN` §12.7 |
| 图鉴条目的形状是 `lore.<id>.{title, body}`，代码侧 `Prose.Text("lore", id, "field")` | `Apocalypse/Lore.cs:63,69` |
| 缺文本 / 多文本都是**构建期抛**（不是运行期空白） | `ContentText.EnsureNoOrphans`（`ContentText.cs:189` 起），由各包 `Build()` 末尾调用（`ApocalypseContent.cs:62`） |
| 自由文本表 `$tables`（`kind: "free"`，**没人读**） | `TEXT_AS_DATA_PLAN` §13 |
| **纸条不是自由表**：代码要读它（它就是那条弹窗的正文） | 见 §3.4 |

> `$tables` 的用途是"**作者写了、今天没人读**"。纸条的正文会被引擎读进
> `PendingLorePopups` 再送到界面上——它天然是**被消费**的一类。
> 把它声明成 `free` 会当场抛（`TEXT_AS_DATA_PLAN` §13.3 最后两行），
> 所以**不新增分区、不加 `$tables` 声明**，纸条就住在既有的 `lore` 里。

### 2.4 "第一次 X 发生"今天有什么，存档那边代价是多少

| 问题 | 事实 | 坐标 |
|---|---|---|
| 今天有没有"首次"标记？ | **只有一个**：`GoldenCookieIntroduced`（bool，已进存档） | `GameState.cs:127`、`SaveData.cs:100` |
| 有没有"已弹过、别再弹"的持久集合？ | **有**：`LoreUnlocked`（HashSet） | `GameState.cs:64`、`SaveData.cs:77` |
| 有没有"弹了但还没被点掉"的持久队列？ | **有**：`PendingLorePopups`（List） | `GameState.cs:67`、`SaveData.cs:80` |
| 有没有一个已经进存档、又是自由形式的字典？ | **有**：`Metadata`（`Dictionary<string,string>`），引擎自己一处都没用过 | `SaveData.cs:129`、`GameState.cs:158`；全仓 `Metadata` 的引用只有序列化那四处 |
| 存档版本 / 迁移 | `CurrentVersion = 1`，`Migrations` 是空表；**D5 的原文**：迁移机制在生产里**零实现**；W9（用真跨版本存档验证）2026-10-05 做过一次，结论是"沉默" | `SaveSerializer.cs:19~22`、`OPEN_WORK.md:59`（D5）、`:129`（W9） |
| 缺字段会不会炸？ | 不会：`Parse` 对每个集合都有 `??= []` 兜底 | `SaveSerializer.cs:250~264` |

### 2.5 金猫（`note_cat` 的素材，全部从代码读出来）

见 §5——那是本次的另一项交付（真人问"金猫怎么拿到"，答案只能从引擎里读）。

---

## 3. 三个决定

### 3.1 呈现：**一套弹窗 + 一个"纸条"皮肤**（不是第二套弹窗）

**决定**：新增**第 4 张 sheet**（`#note-sheet`），它**整段复用**既有那一套
（`.sheet-layer` / `.sheet` / `.sheet-head` / `.sheet-close` / `.sheet-ok` / `sheet-in` / Esc / 点遮罩关闭 /
`role="dialog" aria-modal aria-labelledby`），只加一个修饰类 `.sheet-note` 换皮
（纸色底、暖色描边、正文改用衬线体、宽度收窄到 `min(22rem,100%)`、按钮文案「收好」）。
**没有第二份弹窗代码**：没有第二个 layer，没有第二份开合逻辑，没有第二个 z-index 层。

纸条与"剧情转折"（也是 `Popup` 通道）**共用这一张 sheet**，靠数据分开皮肤：
`channelName === "note"` 才套 `.sheet-note`。所以：

- **收益**：焦点 / 叠层 / 动画 / Esc / 遮罩点击 / aria 全部**只有一份**，可测面不翻倍；
  两个宿主不必各写一遍"纸条怎么出现"；将来改弹窗只改一处。
- **失去**：纸条不再"完全没有先例"——它长得像系统里别的东西（同一个卡片位、同一个入场动画）。
  视觉上的"这是捡到的、不是游戏在跟你说话"要靠**皮肤 + 文案**扛，不能靠"新代码"扛。
- **如果真做第二套**（真要的话）：多一份 layer/开合/焦点/叠层次序/Esc/遮罩/动画/aria，
  两套都会在"离线那张开着的时候"出问题（次序规则现在是硬编码在 `app.js:210~213`、`:256~265`），
  而且 `web-smoke` 的键盘与 §23 两条守卫要各写两遍——**两个能各自漂移的东西**。
  **这一条我不替人拍板**：如果人坚持要"看起来完全不像现有弹窗"，
  那就明确取舍——**要独立观感，就得接受上面这份重复**，我建议先看 `.sheet-note` 皮肤够不够。

### 3.2 "已看过"：**复用 `LoreUnlocked` + `PendingLorePopups`，存档格式零改动**

三个候选（人给的 a / b / c）与树上的事实对照：

| 案 | 判据 | 会发生什么 | 代价 |
|---|---|---|---|
| (a) 只在**状态跃迁**时放（会话内记） | 前端或会话内存 | **刷新就再弹一次**——`app.js:220~231` 那段注释已经把这类写法骂过一遍（去重状态放前端一定会错） | 0 存档改动，但"捡到纸条"变成"每次刷新都捡到同一张" |
| (b) 从单调进度**推导** | 无状态 | 回头的玩家**再也看不到**（进度早就过了门槛 ⇒ 条件恒真 ⇒ 无法区分"没看过"和"看过") | 0 存档改动，但等于"老玩家永远学不到" |
| **(c) 把"看过"持久化** | 存档 | 与图鉴完全同一套语义：**一旦释放就永不重放**，`PendingLorePopups` 负责"放了还没点掉"（跨刷新、跨标签页、跨天） | 见下 |

**选 (c)，但用树上已有的那两个字段**，于是"存档格式改动"这件事**根本不发生**：

- 释放 ⇒ `LoreUnlocked.Add(id)`；未点掉 ⇒ `PendingLorePopups.Add(id)`（`LoreSystem.cs:36,48`）；
- 点掉 ⇒ 从 `PendingLorePopups` 移除（`LoreSystem.cs:70`）；
- 两个字段**在 v1 存档格式里已经有了**（`SaveData.cs:77,80`），
  读写路径也已经在（`SaveSerializer.cs:88~89,126~127,259~260`）；
- 老存档没有音符条目的 id ⇒ `LoreUnlocked` 里没有它 ⇒ 补弹一次（这正是想要的：
  老玩家重开一局会**补上**没见过的纸条）；新存档拿到老版本里读，那两个字段照旧被忽略。

**代价（诚实写清）**：

- **`SaveSerializer.CurrentVersion` 不动、不加迁移**（D5 / W9 不被这次触碰）。
  代价是：纸条的"看过"状态**不能**单独回退——它和剧情共用一张表。
  想在测试里重放纸条，必须清 `LoreUnlocked`（与剧情一样），没有单独的开关。
- **`Metadata` 那条更便宜的路刻意不用**：它确实已进存档、引擎又没人用（§2.4），
  但引擎往一个"给宿主/内容自由用"的字典里塞自己的私有键，是**把别人的抽屉当自己的**；
  而且"看过集合"就变成了 `"id1,id2"` 这样的一个字符串（要自己编码/分隔/去重），
  比"多一个 HashSet"更容易写错。**记在这里，是为了说明"我知道它存在，我选了不这么做"。**

### 3.3 版本：**minor（1.10.2 → 1.11.0）**

按 `VERSIONING.md` §2 那张表：**公开 API 只增不改 ⇒ minor**。这次新增两个公开成员：

| 新增 | 为什么必须公开 | 坐标 |
|---|---|---|
| `LoreChannel.Note`（枚举成员） | 纸条与"剧情转折"在**数据**上必须能分开；通道枚举的注释原话就是"以什么形式打扰玩家"——纸条正是另一种打扰形式 | `LoreEntry.cs:4~17` |
| `LoreView.ChannelName`（string token） | **前端不许解释枚举序数**（`web-smoke.mjs:395`、`:1186` 那条守卫的原文，先例是 `mode` / `modeName`）。给前端一个 token，而不是让它读 `channel` 的序数 | `Views.cs:354~391` |

**存档格式：不动**（§3.2）。所以这次**不碰** `SaveSerializer.CurrentVersion`、**不写**迁移，
D5 / W9 两条欠账保持原样。`PublicApi.txt` 由 `tools/public-api.ps1` 重新生成（不手改）。

### 3.4 文本放哪：`lore` 分区，不新开分区

纸条 = 一条 `LoreEntry`（`Channel = Note`，属于该包的一条**纸条剧情线**）。
于是正文住在既有的 `lore.<id>.{title, body}` 里，**一个字都不用新机制**：

- 双向守卫（代码 ↔ 文件）已在（`ContentTextFileTests.EveryEntryAndStoryline_ResolvesItsProseFromTheFile`）；
- 孤儿检查已在（漏一条、多一条都会抛）；
- `storylines` 分区已有（纸条线也要一条 `storylines.note.{name,theme,icon}`）。

**代价**：纸条会**进图鉴**（`GameViewFactory.cs:339~345` 遍历的是全部条目）——
它顺便解决了"点掉之后想再看一眼"这件事（图鉴里能重读），
但也意味着图鉴里会多出一条叫「拾遗」的线。**这是刻意的**，记在这里。

---

## 4. 纸条目录：一个机制一张，触发是一个**可判定条件**

触发一律写成 `UnlockCondition`（引擎每秒扫一次，条件成立就**锁存**，永不再放——
所以"能买得起"这种**非单调**的条件也可以用：首次成立那一刻就够了）。

| # | 教什么 | 触发（可判定条件） | 落在哪个包 |
|---|---|---|---|
| 1 | **买东西**（座位会自己干活） | `EarnedThisRunAtLeast(<本包第一座建筑的 BasePrice>)` = 第一次**赚够**得起的钱 | 全部 |
| 1b | **批量档**（×10 / ×100 / 买满） | `All(ClicksAtLeast(<n>), EarnedThisRunAtLeast(<10× 首座价格>))` = 第一次买得起十座 | 全部 |
| 2 | **升级**（同一座建筑会变强） | `UpgradesAtLeast(1)` = 第一次买下任何一条升级 | 全部 |
| 3 | **金猫**（会有一只限时的猫出现） | `All(Counter("goldenCookieSerial", 1), EarnedThisRunAtLeast(<首座价格>))` = 第一次真的有金猫出现在场上（**且**已经过了第 1 条） | 全部 |
| 4 | **舍命**（这一层可以重开、换来的东西留下） | `EraAtLeast(2)` = 第一次真的跨了一层 | 有纪元的 9 个包 |
| 5 | **表态**（选择会留下倾向） | `ChoiceMade(<本包第一条表态 id>)` = 第一次真的表了态 | 有表态的 3 个包 |
| 6 | **导出 / 分享**（这局可以带走、可以给别人） | `PlayTimeAtLeast(<某个整分钟数>)` **或** 一个已有的早期条件 | 全部（Web 尤其） |

**三条纪律**（前两条照抄 `LoreTests.cs:59~85` 那条"同条件的条目必然同时解锁"的教训）：

- 纸条的 `Reveal` **不得与同包任何既有条目相同**（否则同一瞬间放两条）；
- 纸条之间也不得相同；
- **同一张纸条线里的序号必须与玩家真正遇到的先后一致**——这条有守卫
  （`ApocalypseContentTests.Storylines_ReadInOrderDuringARealPlaythrough` 在真实游玩里量），
  而它第一次跑就把第 1 条与第 3 条的触发写反了：金猫最早 48 秒就来了，而"赚够一座建筑的钱"
  可能更晚，两者谁先谁后**取决于玩家**。修法**不是**改序号，而是让第 3 条的条件
  **包含**第 1 条的条件（"先学会买东西，再知道金猫"），于是顺序由构造保证、不靠运气。

**两条实现上的教训（都是量出来的，不是想出来的）**：

- **"第一次买得起"不能用 `CookiesAtLeast(价格)`**：购买恰恰发生在买得起的那一刻，
  而检查是每秒一次——"手上曾经到过这个数"在一整段里都可能被错过
  （`Storylines_ReadInOrderDuringARealPlaythrough` 逮到的就是它）。
  换成 `EarnedThisRunAtLeast(价格)`（单调、一经成立就锁存）之后语义不变、判据变硬。
- **纸条的正文会有换行**（清单、落款）。`text.json` 里其余十一类散文都是单行，
  所以纸条是**第一处**用 `\n` 的散文；界面上那一格因此必须是 `white-space: pre-wrap`
  （`pre` 会顶出横向滚动条，`pre-line` 会吃掉用于对齐的全角空格）。

**第一片只做试点包（末世）的两条：#1 与 #3。** 其余五条是方案，不是这次交付。

---

## 5. 金猫机制（从代码读出来的事实，`note_cat` 的依据）

> 这一节是回答"金猫到底怎么拿到"的**唯一**依据：全部来自引擎，不是从界面看出来的。

### 5.1 怎么出现

| 问题 | 答案 | 坐标 |
|---|---|---|
| 谁在推进它？ | 引擎每个 tick：`GameEngine.Tick` → `GoldenCookieSystem.Tick(this, deltaSeconds)` | `GameEngine.cs:269` |
| 是概率还是倒计时？ | **倒计时**。`state.GoldenCookieCountdown -= dt`，≤0 就刷 | `GoldenCookieSystem.cs:56~62` |
| 间隔怎么定？ | `RandomSource.NextDouble(min, max)`，`min=GoldenCookieMinDelay`、`max=GoldenCookieMaxDelay` | `:23~28` |
| 第一只有没有特殊照顾？ | 有：`!GoldenCookieIntroduced` 时乘 `FirstGoldenCookieDelayFactor`（钳在 0.01~1） | `:30~32` |
| 还有别的缩放吗？ | 除以 `Modifiers.Multiplier(GoldenCookieFrequency)`（升级 / 纪元 / 立场都能改） | `:34~35` |
| 场上已有一只时会怎样？ | 上限 `MaxConcurrentGoldenCookies`；**满了就跳过这次、并且照旧重置倒计时**（那一轮等于白等） | `:59~62` |
| 有没有内容门槛（纪元 / 成就 / 升级）？ | **没有**。唯一的闸是 `GoldenCookiesEnabled`（默认 true；定义了结果却关掉它会在构建期报错） | `:42`、`GameBalance.cs:54`、`GameContentBuilder.cs:345` |
| 位置 | `X ∈ [0.05,0.95]`、`Y ∈ [0.10,0.90]` 均匀取 | `:80~81` |
| 每次刷出还会发生什么？ | 计数器 `Counters["goldenCookieSerial"] + 1`（**已进存档**）、`GoldenCookieIntroduced = true`（已进存档）、`GoldenCookieSpawnedEvent`、一条通知「一只金猫出现了！快点它！」 | `:70~71`、`:86`、`:88~89` |
| 什么时候重排 | 重置时、读档 / 换状态时、舍命时 | `GameEngine.cs:108`、`:707`、`PrestigeSystem.cs:180` |

### 5.2 停留、点中、错过

| 问题 | 答案 | 坐标 |
|---|---|---|
| 停多久？ | `GoldenCookieLifetime × max(0.05, Modifiers.Multiplier(GoldenCookieDuration))` | `:193~197` |
| 会错过吗？ | **会**。剩余秒数 ≤0 就被移除并发 `GoldenCookieExpiredEvent`，**没有任何地方记"错过"** | `:46~54` |
| 点中怎么结算？ | 结果由 `ForcedOutcomeId` 或**按 `Weight` 加权随机**选出 | `:179~191` |
| 奖励怎么算？ | `净 = (CookiesFlat + cps×CookiesFromCpsSeconds + 存量×CookiesFromBankFraction（可被"cps 秒数"封顶）) × GoldenCookieReward倍率 − 存量×StealBankFraction`；净 ≥0 计入存量与两个"累计赚取"，净 <0 只扣存量（**不算负收入**，且不为负） | `:113~142` |
| 还会给什么？ | 至多两个增益（`BuffId` / `SecondaryBuffId`，时长受增益时长修饰符缩放） | `:145~155` |
| 点中之后记什么？ | `GoldenCookiesClicked += 1`（这就是 `UnlockCondition.GoldenCookiesAtLeast(n)` 读的那个数） | `:159`、`GameMetrics.cs:41` |
| 点晚了会怎样？ | `Click` 找不到那个实例 ⇒ `Fail("这只金猫已经不在了。")` | `:99~105` |

### 5.3 每个包都不一样（这就是"能不能调"的入口）

| 包 | 间隔 min | 间隔 max | 停留 | 场上上限 | 第一只缩放 | 第一只实际窗口 |
|---|---|---|---|---|---|---|
| 猫咖物语 Neko | 5 min | 15 min | 13 s | 1 | 0.20 | 60~180 s |
| 咖啡馆 Cafe | 4 min | 12 min | **15 s** | 1 | **0.15** | **36~108 s** |
| 九命 NineLives | 5 min | 15 min | 13 s | 1 | 0.20 | 60~180 s |
| 实验室 Lab | 5 min | 15 min | **11 s** | 1 | 0.20 | 60~180 s |
| **公司 Company** | **4 min** | **12 min** | **12 s** | 1 | **0.20** | **48~144 s** |
| **末世 Apocalypse** | **4 min** | **12 min** | **12 s** | 1 | **0.20** | **48~144 s** |
| 图书馆 Library | 5 min | 14 min | 14 s | 1 | 0.20 | 60~168 s |
| 神明 God | 4 min | 12 min | 12 s | 1 | 0.20 | 48~144 s |
| 文明 Civ | 5 min | 14 min | 13 s | 1 | 0.20 | 60~168 s |
| 赛博 Cyber | **2 min** | **6 min** | 12 s | **2** | **0.15** | **18~54 s** |
| 梦幻 Dream | 6 min | 16 min | 13 s | 1 | **0.22** | 79~211 s |

（`GameBalance` 的默认值是 5 / 15 / 13 / 1 / 0.25；上表逐包读的是各包的 `BuildBalance()`。）

**纪元还会再改一遍间隔**：文明 ×0.7 / ×0.8 / ×1.25 / ×2 / ×0.5（`Civ/Eras.cs:58,80,104,130,155`）、
赛博 ×0.5 且上限 2 → ×0.45 且上限 **3**（`Cyber/Eras.cs:70,117`）、公司 ×0.7（`Company/Eras.cs:68`）、
神明 ×0.5（`God/Eras.cs:102`）、实验室 ×0.6（`Lab/Eras.cs:72`）、图书馆 ×1.4（`Library/Eras.cs:77`）、
九命 ×0.5（`NineLives/Eras.cs:56`）、梦幻有一层带 `GoldenCookieFrequency(0.75)`（`Dream/Eras.cs:93`）。

**结果表**：11 个包共 **109** 条 `GoldenCookieOutcome`（`TEXT_AS_DATA_PLAN` §12.1；
逐包 10/8/10/10/10/10/10/10/11/10/10）。每条有 `Weight`（加权抽）、`IsRare`、
上面那些货币字段、可选的增益、可选的"偷存量"比例；名字 / 说明 / 图标在
`text.json` 的 `goldenCookies.<id>.{name,description,icon}`。
**内容里已经有以"点中金猫次数"为门槛的东西**：成就（每包一条 `GoldenCookiesAtLeast(n)`）、
赛博的整条剧情线（1 / 4 / 10 / 20 / 32 / 44 / 56 / 70 次）、咖啡馆与赛博的升级。

### 5.4 `note_cat` 的触发选哪一个，为什么

| 候选 | 能不能判定 | 问题 |
|---|---|---|
| 第一次**看见**（刷出来了） | 能：`Counter("goldenCookieSerial", 1)`（引擎刷出时自己写的计数器，已进存档） | **它正好和猫同时出现**——纸条是要盖在画面上的，而那只猫只停 12 秒。玩家在看纸条，猫在纸条后面过期。**这条会把要教的东西毁掉。** |
| 第一次**点中** | 能：`GoldenCookiesAtLeast(1)` | 安全，但"你已经抓到了"再教"怎么抓"晚了一步 |
| 第一次**错过** | **判不了**：没有"错过"的计数（`GoldenCookieExpiredEvent` 只发事件，不进存档） | 要新增公开指标（又是一次 minor），而且教"你错过了"是责备语气 |

**决定**：触发用**第一次看见**（`Counter(GoldenCookieSystem.SerialCounterKey, 1)`，
再与"已经赚够第一座建筑的钱"取交，见 §4 第三条纪律），
但**呈现加一条次序规则**：**场上有金猫时，纸条让位**（`goldenCookies.length > 0` ⇒ 先不弹），
猫走了或被抓了，纸条立刻出现。这样：

- 触发仍然是**真正意义上的"第一次遇到"**（人原话的那条）；
- "纸条盖住猫"这个伤害被一条**一行就能实现的次序规则**消掉，
  与既有那条"离线先讲完才轮到表态"（`app.js:210~213`）是同一类规则、同一个位置。

**为什么不用"第一次点中"**：它把纸条变成"事后说明书"。金猫机制的**真正缺失信息**是
"这东西会再来、而且错过也没关系"——而那句话在**第一次看见之后**说出来最有用。

### 5.5 如果纸条系统没被采纳，金猫靠什么教（兜底）

**兜底就一行**：把引擎已经发的那条通知从「一只金猫出现了！快点它！」
（`GoldenCookieSystem.cs:89`，**它今天只是一条 2.6 秒就消失的 toast / 通知栏文本**）
在**第一次**出现时改成一句带"会再来、错过也没关系"的话——
判据用现成的 `state.GoldenCookieIntroduced`
（`GoldenCookieSystem.cs:31` 已经在读它，`SaveData.cs:100` 已经进了存档）。
这条兜底**不需要纸条、不需要新弹窗、不需要新存档字段**，今天就能做。

---

## 6. 这次落地的第一片

| 面 | 改了什么 | 坐标 |
|---|---|---|
| 引擎 | `LoreChannel.Note`（新通道成员）+ `LoreChannelNames.WireName()`；`LoreSystem.Check` 把它与 `Popup` 走同一条待读队列；`LoreView.ChannelName`（token，前端只认它）；`GoldenCookieSystem.SerialCounterKey`（金猫出现计数器的键从字面量提升成公开常量） | `LoreEntry.cs`、`LoreSystem.cs`、`Views.cs`、`GameViewFactory.cs`、`GoldenCookieSystem.cs` |
| 内容（试点 = 末世） | 一条纸条剧情线 + **2 张纸条**：`note_buy`（§4 第 1 条）、`note_cat`（§4 第 3 条） | `Apocalypse/Lore.cs` |
| 文本 | `storylines.note.{name,theme,icon}` + `lore.note_buy.*` + `lore.note_cat.*`（**只追加，不动既有分区，不重排版**；纸条是 `text.json` 里第一处正文带 `\n` 的散文） | `Apocalypse/text.json` |
| Web | 第 4 张 sheet `#note-sheet` + `.sheet-note` 皮肤（`.sheet-layer` / `.sheet` / `.sheet-head` / `.sheet-close` / `.sheet-ok` / `sheet-in` / Esc / 点遮罩**整段复用**）；次序：离线 → **金猫不在场** → 纸条 → 表态 | `wwwroot/index.html`、`app.css`、`app.js` |
| 终端 | **不新增弹窗**；纸条走既有的"📖 有 N 段新剧情待读"状态行 + 图鉴。**一处小修**：点掉一条纸条时连正文一起写进日志 | `Demo.Cli/GameSession.cs` |
| 版本 | minor **1.10.2 → 1.11.0**（公开 API +5 / −0）：`Directory.Build.props`、`CHANGELOG.md`、`README.md`、`engine/core/PublicApi.txt`（由 `tools/public-api.ps1` 生成，不手改） | — |
| 用例 | 586 → **590**（`LoreTests` +4） | — |

**试点包为什么是末世**：真人正在玩；它有建筑 / 升级 / 纪元 / 结局 / 剧情 / 金猫，
**没有**表态（设计矩阵里它是唯一没有立场轴的包）——所以 #5 那类纸条它不适用，
这一点本身就是"目录要按包裁剪"的证据。公司包留给下一片（它有表态与选择）。

---

## 7. 守卫与判别力

### 7.1 已有的守卫自动覆盖纸条（一个字都没为它写）

| 守卫 | 守什么 |
|---|---|
| `ContentTextFileTests.EveryEntryAndStoryline_ResolvesItsProseFromTheFile` | 纸条文本**双向**逐字（代码 ↔ 文件）+ 剧情线声明条数与非空 |
| `ContentText.EnsureNoOrphans`（各包 `Build()` 末尾） | 文件里多一条 / 代码里少一条，**当场抛**并点名 id |
| `LoreTests.Reveal_FiresOnce_AndPublishesEvent` / `PopupChannel_IsDismissable_AndNotLogged` / `PendingLore_AppearsInSnapshot` / `SaveRoundTrip_PreservesLoreAndPopups` | 释放一次、可点掉、进快照、**存档往返**——纸条走同一条队列，所以这几条**直接**覆盖了它 |
| `LoreTests.Validator_Rejects*`（5 条） | 坏内容构建期就抛（恒假条件 / 未登记计数器名 / 空剧情线…） |
| `ContentTests.CounterNames_AreRegisteredForEveryReferencedCounter` | 纸条引用了 `goldenCookieSerial`，所以它的显示名必须登记——**这条守卫逼着**那一句 `AddCounterName` 存在 |
| `ApocalypseContentTests.LoreRevealsAreUnique` | 同包内两条纸条不得同条件 |
| `ApocalypseContentTests.Storylines_ReadInOrderDuringARealPlaythrough` | 纸条线内序号必须与真实游玩的先后一致 |
| `web-smoke` §23「每个线上字段都要有人决定过」/ §24「恰好四条实时区域」 | 新 token 要么被画、要么写进 `NOT_DRAWN`；纸条那张 sheet **没有**新增实时区域 |

### 7.2 新增的 4 条（都在 `LoreTests`）

| 用例 | 守什么 |
|---|---|
| `NoteChannel_UsesTheSamePendingQueue_AndTheSameSeenSet` | 纸条**没有**自己的队列、也没有自己的"看过"集合——一旦有人给它开第二套状态（那就要改存档格式），这条红 |
| `NoteChannel_CarriesItsOwnWireToken_SoTheFrontendNeedNotReadOrdinals` | 线上 token 是 `note`，而剧情弹窗仍是 `popup`（分不开就只能靠剧情线 id 猜 = 把内容知识写进前端） |
| `GoldenCatNote_FiresOnTheFirstSighting_NotOnTheFirstCatch` | "看见"就放、"没赚够钱"不放、**不点猫**也放、且同线内先来的排在前面 |
| `NoteChannel_TextIsALeafObject_WithLineBreaks` | 纸条正文必须带换行（它是清单/落款，不是一句话说明） |

### 7.3 一处**刻意的口径收窄**（要人知道）

`ApocalypseContentTests.G5_FirstTenMinutesRevealAtMostThreeEntries` 原本数的是
`LoreUnlocked.Count`——纸条一进来它就红（**实测：3 → 5**）。它的失败原文写着自己的意图：
"**世界观**被一次性讲掉了"。纸条不是世界观，它是**入门**，而且按真人原话就该在
"第一次遇到那个情况"时出现——两条需求在这里是**直接互斥**的：把纸条算进去，
就等于"要不许教机制、要不许早教"。

所以口径收窄成：**那一条只数非纸条的条目**，代价是**补上另一半断言**——
"开局十分钟里两张入门纸条必须都已经到手"。后半条比原来更贴纸条的设计意图：
**漏掉一张 = 那个机制没人教**，而那是沉默失败（界面上不会报错，玩家只是不知道）。
两半合起来管的东西**比原来多**，不是少。

> 这一处是我不替人拍板的地方之一：如果人更愿意"纸条也算进 G5"，
> 那就必须放弃"第一次遇到就教"这条设计——**两者不能都要**。

### 7.4 判别力：三处故意改坏（都在真文件 / 真守卫上做，逐处还原并核对哈希）

> 判据是先备份 + SHA-256 记账，改坏 → 重编（**必须重编**：引擎读的是输出目录里那份
> 复制过去的 `text.json`，不是源码树那一份——第一次做这条证明时就是在这里白跑了一轮）→
> 跑守卫 → 还原 → 再核哈希。

| # | 故意改坏 | 红在哪 | 原文（截取） |
|---|---|---|---|
| 1 | 真文件 `Apocalypse/text.json` 里**删掉 `note_buy` 整条**（标题 + 正文） | 包 `Build()` 当场抛，**12 条末世用例全红** | `…\bin\Debug\net8.0\content\Apocalypse\text.json：lore 里没有 id「note_buy」。` |
| 2 | 真文件里给 `lore` 加一条 `zz_orphan_note`（代码从不取用） | 孤儿检查点名 | `内容包「Apocalypse」的剧情文本里有 1 条没人取用（孤儿条目）：lore/zz_orphan_note。…` |
| 3 | `Apocalypse/Lore.cs` 里把 `note_buy` 的通道从 `Note` 改成默认的 `Log`（**皮与数据不一致**） | **4 条**红：3 条 `NoteChannel_*` + `Structure_IsComplete` | `0 通过 / 3 失败（共 3）`、`7 通过 / 1 失败（共 8）` |

三处都还原：`text.json` = `A6FF1855A25335829D7C8F0B279F475FED47BD5976AC8EE47DA35193F4DF64EB`、
`Lore.cs` = `6A778C97F460CB27FEA4DB3B3E4DB05E0638BCB99BE4601605DB6D990A4B90A2`，
与改坏前逐一相同；还原后 `-Strict` **590/590 + 冒烟 224/224 全绿、退出码 0**。

> 第 3 处最说明问题：**内容、文本、通道三者只要有一个不一致，红的是"这一条到底算不算纸条"**——
> 而不是"它长得对不对"。皮肤那部分（`wwwroot`）由 `web-smoke` 的 §23 / §24 与三条既有
> sheet 断言守着，它管的是"界面有没有跑偏"，管不了"这条该不该是纸"。

---

## 8. 明确不做

- **不做第二套弹窗**（§3.1）：不新增第二个 layer、不写第二份开合 / 焦点 / aria 逻辑。
- **不动存档格式**：不加 `SaveData` 字段、不动 `CurrentVersion`、不写迁移（§3.2）；
  **D5 / W9 两条欠账保持原样**，这次不拿它们练兵。
- **不用 `Metadata` 存"看过"**（§3.2 末）。
- **不动 `$tables`**：纸条是被消费的文本，不是自由表（§2.3）。
- **不做模板 / 占位符**：纸条正文里不插数值（与 `TEXT_AS_DATA_PLAN` §12.2 第 3 条同一条判断）。
- **不给纸条加"未读角标"**（那是 `OPEN_WORK` D7 的另一件事）。
- **不改 `Popup` 通道既有的剧情条目**：它们在 Web 上仍然是"没画"的现状，
  这次只让**纸条**有落脚点——把剧情转折也画出来是另一件事，见 §9。
- **不重排版 / 不重构 `text.json`**：只追加分区，行尾与既有文件一致。
- **不碰仓库真实 `saves/` 与 `artifacts/latency.txt`**。

---

## 9. 待定（不替人拍板）

1. **纸条要不要独立观感**（§3.1 末）：`.sheet-note` 皮肤够不够？若要"完全不像现有弹窗"，
   代价是重复一整套弹窗机械——**要先拍这一句，我才好决定要不要动 `app.js` 的次序逻辑。**
2. **剧情转折（`Popup`）要不要也画出来**：它们今天在 Web 上是"放了但没人看得见"。
   我的第一片只画纸条；把 `Popup` 也画上是**顺手**的事，但它会改变既有 11 个包的行为
   （玩家会开始看到剧情弹窗），值得单独拍一句。
3. **纸条要不要在图鉴里单列**（§3.4）：今天它会进图鉴（好处：可重读）。
   若要"纸条只在捡到那一刻存在"，那要另一条路（也是另一份存档字段）。
4. **#6（导出 / 分享）的触发**：`PlayTimeAtLeast(n)` 是个"没话找话"的条件。
   如果人更想要"第一次打开存档窗口"那一刻，那需要一个宿主侧信号——**这是待定项**。
5. **第二片做公司包**（表态 / 选择那两张纸条）：要不要做、什么时候做。

---

## 10. 交付记录（2026-10-05，实际是怎么做的）

| 项 | 结果 |
|---|---|
| 落地面 | `engine/core` 5 个文件（`LoreEntry.cs` / `LoreSystem.cs` / `Views.cs` / `GameViewFactory.cs` / `GoldenCookieSystem.cs`）、末世包 3 个文件（`Lore.cs` / `text.json` / `ApocalypseContent.cs`）、终端 1 个（`GameSession.cs`）、Web 3 个（`index.html` / `app.css` / `app.js`）、测试 2 个（`LoreTests.cs` / `ApocalypseContentTests.cs`） |
| 公开表面 | **+5 项 / −0 项**（`Note` 枚举成员、`LoreChannelNames` 类型、`WireName`、`SerialCounterKey`、`LoreView.ChannelName`）⇒ 按 `VERSIONING.md` §2 是 **minor：1.10.2 → 1.11.0**；快照由 `tools/public-api.ps1` 重生成（不手改），`git diff --numstat` = **`7 1`**（`1` 是首行 `version=`） |
| 存档 | **一个字没改**：`SaveSerializer.CurrentVersion` 仍是 `1`，没有新字段、没有 `ISaveMigration`；D5 / W9 原样 |
| 用例 | **586 → 590**（`LoreTests` +4）；`ApocalypseContentTests` 两条口径改动见 §7.3 |
| 实测全绿 | `tools/build.ps1 -Strict`：两个 sln **0 警告 0 错误**、**590/590**、前端冒烟 **224/224**，退出码 0；`tools/api-test.ps1`（真宿主端到端）**85/85**，退出码 0 |
| 与另一轮的语气棘轮对过 | 把当时主树那份 `ProseToneTests`（`CONTENT_AUTHORING` §12.4 的棘轮，**逐项等值**不是"不超过"）临时拷进来跑：**Apocalypse 三项一个都没涨**（`EndQuote` 17 / `NegPivot` 5 / `Significance` 3 与表里相等），也就是说两张纸条的正文**没有新增任何被判为"AI 腔"的形状**。唯一红的是 `Company.EndQuote`——那是"本分支的基线还没做那次试点"造成的，与本轮无关。跑完即删。 |
| 判别力 | §7.4 三处故意改坏，三处都还原并核过哈希 |
| 改坏前后哈希 | `Apocalypse/text.json` = `A6FF1855…4DF64EB`、`Apocalypse/Lore.cs` = `6A778C97…A4B90A2`（改坏前后逐一相同） |
| 人的数据 | 仓库真实 `saves/`（含 `.bak`）与 `artifacts/latency.txt` **一个字节没碰**（收尾按大小 + mtime + sha256 逐文件核对）；隔离 worktree 里跑的构建与守卫只读运行期对象 |
| 版本动作 | **没有推送、没有打 tag**（按约定由上层做） |

**已知的合并冲突（要人处理）**：`games/hosts/Web/wwwroot/{app.js,app.css,index.html}` 同时被
另一轮（前端刻度名 / 买满 / 焦点那四件）改过——那份改动已经以 `966358b` 进了 `main`，
而本分支是在 `d3f9c55` 上做的。冲突点预计三处：
① `app.js` 的 `render()` 里那几行渲染次序（我插了 `renderNoteSheet()`，它可能也动了邻近行）；
② `app.js` 的 keydown 段（我插了纸条那一段）；
③ `app.css` 的 sheet 段（我插了一整节 `.sheet-note`）。
`index.html` 我插的是 `#offline` 与 `#choices-sheet` 之间的新块，冲突面最小。
`engine/content/*/text.json` **不冲突**（我只追加，那份"去中二"的另一轮动的是别处，且未提交到本分支的基线）。
