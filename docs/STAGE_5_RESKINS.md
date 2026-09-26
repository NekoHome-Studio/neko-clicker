# 阶段 5 换皮批产手册

> **这份文档是给"下一个会话"的交接件。** 目标：交付内容包
> **#4 猫娘文明 / #5 赛博猫娘 / #7 猫娘神明 / #8 猫娘梦境**，四个包，
> **不得新增任何核心代码**（ROADMAP 阶段 5 的特有验收，也是架构不变量 A3 的检验）。
>
> 阶段 4 之后，这四个包要用的能力全都已经在 6 个已交付的包里跑过至少一遍：
> `Era` + `Lore` + 计数器（第二资源）+ 结局条件树。所以这一步是**批产**，不是设计——
> 真正的风险不是"做不出来"，而是**每个包各自重犯一次**阶段 2.6 / 2.7 / 4B / 4C 踩过的坑。
> 第 4 节那张表就是为了堵这个。
>
> 另外：`docs/CONTENT_AUTHORING.md` 是内容作者手册（数值节奏 / 校验规则 / 常见坑），
> 本手册只挑「这个阶段特别容易错」的部分，动手前建议先扫一遍那一份。

---

## 1. 交付定义（可证伪）

| # | 验收项 | 怎么验 |
|---|---|---|
| 1 | 四个包都能玩 | `.\tools\play.ps1 --package civ / cyber / god / dream` |
| 2 | 每个包的机器人从第 1 层走到最后一层 | 包内 `RobotWalksAll…` 用例 |
| 3 | **核心零改动** | 本阶段的 diff 里 `src/NekoClicker.Core/` 一行都不动；K1 架构测试全绿 |
| 4 | 图鉴读得完 | `--panel codex` 或机器人跑图，**40/40** |
| 5 | 结局可达且互斥、有兜底 | 包内结局用例 + 构建期 `ValidateEndings` |
| 6 | 既有 320 个用例全绿 | `.\tools\build.ps1 -Strict` 退出码 0 |

**如果某个包逼你动核心，先停下来**：那说明抽象不成立，而这件事本身比多做一个包重要——
写进 ROADMAP 的交付记录，别硬塞。

---

## 2. 现在有什么可以抄

| 参考包 | 什么时候抄它 | 路径 |
|---|---|---|
| #9 图书馆 | **首选模板**：`Era` + `Lore` + 一个计数器 + 2 个结局，而且它有"会掉的计数器"这个最复杂的形态 | `src/NekoClicker.Content.Library/` |
| #6 末世 | 只有一个第二资源、没有立场轴、3 个结局 | `src/NekoClicker.Content.Apocalypse/` |
| #2 九命 | 层数多（9 层）、每层换规则、有立场轴（本阶段四个包**不需要**） | `src/NekoClicker.Content.NineLives/` |

一个包的完整文件清单（以 #9 为例，行数是实际值，规模参考用）：

```
NekoClicker.Content.Library.csproj   20   项目文件（零 NuGet 依赖，只引 Core）
LibraryContent.cs                    99   入口：标题 / 货币 / 平衡参数 / Builder 串起来
Buildings.cs                        123   9 座建筑
Upgrades.cs                         443   47 条升级（含永久线）
Achievements.cs                     221   66 条成就
Buffs.cs                            101   8 条增益
GoldenCookieOutcomes.cs             118   10 条金猫结果（换皮）
Eras.cs                             131   5 层纪元 + FinalCompletion
Lore.cs                             603   40 条叙事（4 条线）
Endings.cs                          114   2 个结局 + 结局成就
ReadershipModule.cs                  81   第二资源（IGameModule）
```

外加上线四处（每个包都要改）：

| 位置 | 改什么 |
|---|---|
| `NekoClicker.sln` | **手工**加 7 行（见 §6.3，别用 `dotnet sln add`） |
| `src/NekoClicker.Demo.Cli/NekoClicker.Demo.Cli.csproj` | 加一条 `ProjectReference` |
| `src/NekoClicker.Demo.Cli/ContentPackages.cs` | 加一条 `ContentPackage`（id / 欢迎语 / 转生动作名 / 金猫名 / 帮助 / 报告提示） |
| `tests/…/TestGame.cs` | 加 `XxxContent` 缓存 + `CreateXxx()`，**并加进 `AllContentPacks()`**（否则通用守卫扫不到这个包） |
| `tests/…/XxxContentTests.cs` | 包内专项用例（抄 `LibraryContentTests.cs`，14 条） |

---

## 3. 四个包的设计输入（来自 NINE_LIVES_DESIGN）

| 包 | id 建议 | 语气 | 转生语义 | 建筑线（6 座骨架，扩到 9） | 金猫换皮 | 第二资源 | 结局集合 |
|---|---|---|---|---|---|---|---|
| #4 猫娘文明 | `civ` | 文明演进 | 时代更替至星际 | 猫窝 → 村庄 → 城墙 → 集市 → 学院 → 神殿 → 星港 | 「天灾」 | 文化 | 星际文明 / 停滞 / 自我毁灭（3） |
| #5 赛博猫娘 | `cyber` | 数字层 | 迁服务器 | 进程 → 容器 → 集群 → 机房 → 防火墙 → 根服务器 | 「病毒入侵」 | 算力 | 互联网守护猫 / 找到主人的数据残影（2） |
| #7 猫娘神明 | `god` | 轻松搞笑 meta | 切换神话体系 | 神龛 → 神殿 → 祭坛 → 直播间 → 周边工厂 | 「神迹」 | 信仰 | 成为主神 / 被遗忘 / 变成 meme（3） |
| #8 猫娘梦境 | `dream` | 梦层 | 梦醒 / 入梦嵌套 | 枕头 → 梦层 → 噩梦巢 → 清醒区 → 梦核 | 「梦魇」 | 梦境能量 | 叫醒梦者 / 永远留在梦里（2） |

**#7 神明是四个包里最容易的第一个**：设计文档 §3.4 已经给了它完整的 5 层表
（家猫神 → 埃及猫神 → 希腊猫神 → 北欧猫神 → 克苏鲁猫），
每层的规则变化与完成条件都写好了，照着落就行。建议**先把它做完做透**，再把另外三个套上去。

**每个包建议的规模**（与已交付的四个"大包"对齐，也是通用守卫的阈值）：

- 9 座建筑 / ≥40 条升级 / ≥60 条成就 / ≥6 条增益 / ≥8 条金猫结果
- 40 条叙事 = 4 条线 × 10（或 12+10+10+8，像 #6/#9 那样）
- 5 层纪元（`Index` 从 1 连续），每层 2 条完成条件
- 2~3 个结局，**其中必须有兜底**
- 1 个 `IGameModule` 提供第二资源

---

## 4. 必须遵守的规则（这一节是本文档的重点）

每条都对应一个真实踩过的坑，括号里是详细出处。

| # | 规则 | 出处 / 守卫 |
|---|---|---|
| 1 | **数值曲线照抄配方**：相邻价格 ×6.7~16.5、产量 ×5.4~10，且第 3 座起价格倍率 > 产量倍率。换包换的是叙事，不是手感 | `ContentTests.NekoContent_BuildingCurveIsSane` |
| 2 | **纪元完成条件必须单调**：只用累计赚取 / 成就数 / 点击数 / 金猫数 / 已购升级 / 时长 / **单调的**计数器 / 标签升级 / 图鉴数 | 构建期白名单校验 |
| 3 | **每层门槛要摊平**：别把产量爬坡全压在某一层（#2 曾出现"第 5 命 19.2 小时、邻居 1.8 小时"） | ROADMAP §7 阶段 2.7 |
| 4 | **叙事四条纪律**：任意两条 `Reveal` 不同；线内顺序单调；转生类条目放线尾；**层内门槛 < 本层完成门槛** | 前三条是 `LoreTests` 的通用守卫，第四条 `EraGatedLore_StaysBelowItsEraCompletion` 也是通用的；「线内顺序」另需**包内**那条真跑用例（抄 `LibraryContentTests.Storylines_ReadInOrderDuringARealPlaythrough`） |
| 5 | **开局 10 分钟 ≤3 条**：三条线各用一个不同的点击小门槛开篇（1 / 25 / 100），第四条放到 240 次之后 | `G5_FirstTenMinutesRevealAtMostThreeEntries` |
| 6 | **所有阈值先量包络再设值**，不要推理。跑完机器人看图鉴是不是 40/40、结局拿不拿得到 | ROADMAP §7 阶段 2.7 / 4B |
| 7 | **`Scaling.Cap` 限的是原始计数值，不是加成结果**（`Apply = base + PerUnit × min(计数, Cap)`）。"每座建筑 +2%、最多 +200%" = `PerUnit 0.02, Cap 100` | `CONTENT_AUTHORING` §8；**给成长型修饰符写一条端点断言** |
| 8 | **计数器必须在 `Configure` 里登记显示名**，否则玩家看到 `每点「readership」` | `ContentTests.CounterNames_AreRegisteredForEveryReferencedCounter`（会真渲染一遍） |
| 9 | **计数器驱动产量不需要新来源**：`Scaling(ScalingSource.CustomCounter, …, Id: 键)` 就够 | ARCHITECTURE「扩展点」 |
| 10 | **会掉的计数器要按量子 `MarkDirty()`**（`Step()` 是先重算再 tick，模块改计数器不会自动让产量变脏），而且**不能进完成条件** | `CONTENT_AUTHORING` §10 |
| 11 | **转生除数按自己包的阶梯标定**（目标"最后一次结算落在 ~100 级"），**永久线总价 ≤ 一次游玩结算出的货币** | `CONTENT_AUTHORING` §7.1；`PrestigeTests.EraPacks_PermanentUpgradesAreAffordableWithinOneRun` |
| 12 | **结局必须有兜底**，且所有结局都要 `EraAtLeast(末层) + 末层完成条件`（否则一进末层兜底结局就抢答了） | `ValidateEndings` + `LabEndingTests` 的教训 |
| 13 | **建筑解锁**：不做继承的包用 `EarnedThisRunAtLeast` 是**有意的**（每层重新揭示）；**只有做继承的包**才必须换成 `EarnedAllTimeAtLeast`，否则"拥有但未解锁" | `CONTENT_AUTHORING` §11.1 |
| 14 | **永久升级必须用转生货币计价**，否则构建期直接报错 | `GameContentBuilder` 校验 |
| 15 | **别新增第二种"衰减 / 无人读就消失"机制**——那是 #9 专属的，且已交付 | NINE_LIVES_DESIGN §10 |

---

## 5. 验收流程（命令级）

每一步都从 **bash** 侧驱动（原因见 §6.1）：

```bash
# 1) 全量重编 + 全部测试。改完每个包都跑一次，提交前必跑。
powershell.exe -NoProfile -File tools/build.ps1 -Strict

# 2) 真实跑图：新存档！旧存档会把上一轮的终局状态读进来（见 §6.2）
powershell.exe -NoProfile -File tools/play.ps1 --package god \
    --simulate 43200 --auto --save .tmp/god.json

# 3) 图鉴面板截图，肉眼确认 ??? 遮蔽与进度
powershell.exe -NoProfile -File tools/play.ps1 --package god \
    --simulate 21600 --auto --frame 118x32 --panel codex --no-color
```

调参循环（**顺序不要颠倒**）：

1. 先让 `RobotWalksAll…` 走通（层与层之间的门槛在真实曲线下够得着）；
2. 再看图鉴是不是 40/40，不够就按实测包络下调那几条的门槛；
3. 最后看结局：自然跑图应当落到"承诺型"结局，回避型落到兜底；
4. 每改一次数字都重跑 `-Strict`。

---

## 6. 环境与已知坑（本会话真实踩过）

### 6.1 构建必须从 bash 侧驱动

`pwsh` 工具的沙箱**禁用命名管道 / 重定向 stdio**，而 MSBuild 启动 `csc.exe` 正是这么做的。
表现是一个极具误导性的 `MSB3883: Unexpected exception: 拒绝访问`——
而且**增量构建能过、`-Strict` 必挂**（增量时不重编就碰不到编译器）。
正确姿势：

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/build.ps1 -Strict
```

### 6.2 Demo 会读写 `saves/<包>.json`

`--simulate` 默认会**加载**那个文件。旧档里 `Era` 可能已经是最后一层、
`Ascensions` 是历史记录，于是报告里的「当前等级 / 转生货币」显示的是旧状态——
本会话被这个骗过一次（报告"等级 0"，其实是上一轮的终局存档）。
**量任何包的现状，一律 `--save .tmp/<包>.json`。**

### 6.3 `.sln` 手工加，别用 `dotnet sln add`

`dotnet sln add` 会把整个文件重写：加 BOM、补 `x64/x86` 配置、重排段落。
手工加 7 行即可（照抄 Apocalypse / Library 那两段）：

- 一个 `Project(...) = "NekoClicker.Content.Xxx", "src\…\…csproj", "{新 GUID}"` + `EndProject`
- `GlobalSection(ProjectConfigurationPlatforms)` 里 4 行（Debug/Release × ActiveCfg/Build.0）
- `GlobalSection(NestedProjects)` 里 1 行（挂到 `src` 文件夹的 GUID `{0FEE24DC-…}` 上）

### 6.4 改文件的脚本：先算后写

`io.open(path, 'w')` 在参数求值**之前**就把文件截断了。本会话把 `README.md` 写空过一次
（好在 git 里有）。先构造好完整内容，再打开写入。

### 6.5 中文文案里的引号用「」

ASCII 双引号会截断 C# 字符串字面量，只能在编译期发现。`docs/CONTENT_AUTHORING.md` §8 有完整清单。

---

## 7. 交付清单（DoD）

- [ ] 四个包的项目 / sln / Demo 引用 / `ContentPackages` 注册 / `TestGame.AllContentPacks()` 全部就位
- [ ] 每个包：9 建筑 / ≥40 升级 / ≥60 成就 / ≥6 增益 / ≥8 金猫结果 / 40 条叙事 / 5 层 / 2~3 结局
- [ ] 每个包的专项用例（抄 `LibraryContentTests`）：结构 / 叙事唯一 / 真跑顺序 / G5 / 第二资源 / 机器人可达 / 结局互斥
- [ ] `tools/build.ps1 -Strict` 全绿、0 警告
- [ ] `src/NekoClicker.Core/` **零改动**（`git diff --stat` 自查）
- [ ] 四个包各跑一次 `--simulate 43200 --auto --save .tmp/<包>.json`，图鉴 40/40
- [ ] README 的包清单 / 用例数 / 快速开始命令；ROADMAP 阶段 5 交付记录；NINE_LIVES_DESIGN §2 矩阵的 "❌" 改成 "✅ 已落地"
- [ ] 提交信息按仓库风格：现象 → 处理 → 验证，写清"零核心改动"这条证据

---

## 8. 做完之后

十个包就全交付了。那时 ROADMAP 的 G1~G6 应当全部可证伪地成立，
唯一还没做的是 §10 明确不做的那些（图形前端、本地化、云存档、排行榜、反作弊）——
它们被设计为**引擎外部的宿主职责**，不是这个仓库的范围。
