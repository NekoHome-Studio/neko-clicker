# NekoClicker Games

**旗舰示例作品**：一个引擎（`engine/`）+ 一部作品（这里）。

这个目录依赖 `engine/`，反向不依赖。它的作用是证明引擎的主张**在真实内容里成立**——
十个内容包承载十套世界观，其中四个换皮包的核心改动是 0 行。

---

## 两个前端宿主

引擎是哑的：它只产出 `GameSnapshot`（纯数据、可直接做差异比较）并接受命令
（`Click` / `BuyBuilding` / `SellBuilding` / `BuyUpgrade` / `AnswerChoice` / `DismissLorePopup` …）。
两个宿主是**同一份契约的两种消费方式**：

| 宿主 | 形态 | 怎么跑 |
|---|---|---|
| `hosts/Demo.Cli/` | 终端全屏 TUI（ANSI + 增量重绘 + 无头模拟） | `.\tools\play.ps1`，可加 `--package <id>` |
| `hosts/Web/` | 浏览器（本地 HTTP 宿主 + 静态 `wwwroot`） | `.\tools\web.ps1 run` |

**`hosts/Demo.Cli/` 有第二重身份**：它是框架的**回归基线**。`FrameRenderTests` 直接引用这个项目，
守着"渲染返回的行数必须恰好等于终端高度、每行宽度必须恰好等于终端宽度"这两条不变量——
违反它们在无头环境里完全看不出来，只会在真实终端上表现为滚屏与随机花屏。

### `hosts/Web/` 的当前状态（诚实边界）

**可玩**：点击、买建筑与升级、推进纪元、表态、看图鉴、**日志**（引擎的通知：成就 / 买升级 /
增益 / 金猫 / 舍命 / 存档失败）、**离线收益弹窗**（读档补发的那一笔：写了多少、补了多久，
被上限截断时把实际离开了多久也说清）、**永久升级线**（`#tab=permanent`：转生后保留的那条线，
钱包与"买得起几项"的角标都来自服务端，锁着的行照样列出来带解锁进度）、
金猫浮层、增益条、批量档位，
挂机不掉线，与终端 Demo **共用同一份存档**（`saves/<包 id>.json`）。

- 零前端依赖：手写 ES 模块 + 一份 CSS，`wwwroot/` 直接签进仓库，没有 npm、没有打包步骤。
- **想看某个面板长什么样、又不想手点**：把 `index.html` / `app.css` / `app.js` 内联成一张
  静态页，用桩替换 `EventSource`（构造时立刻 `onmessage` 推一帧**假快照**）与 `fetch`，
  再用无头浏览器截图即可。**2026-10-02 实测可用的那条命令**（Edge 与 Chrome 都装在本机）：

  ```powershell
  msedge --headless=new --disable-gpu --hide-scrollbars --window-size=1100,800 `
         --virtual-time-budget=2000 --screenshot=out.png "file:///<预览页的绝对路径>"
  ```

  日志面板（2026-10-01）、离线弹窗与永久线面板（2026-10-02）都是这么验的（真 CSS + 真渲染函数 +
  **假数据**）。假数据有个坑：**手写 fixture 会漏掉服务端算好的派生字段**（`isUnlocked` /
  `isMaxed` / `isAvailable` / `unlockProgress`），于是"买得起"的角标与配色全是假的——
  更省事也更准的做法是**先拿真宿主抓一份快照再改**（只改 `prestigeChips` 这类玩法状态），
  永久线面板那一版就是这么做的。
  它只看得到**布局与配色**；"和真宿主配合得对不对"归 `tools/api-test.ps1`。
  预览脚本刻意留在 `.tmp/` 不进仓库：每轮要看的东西都不一样，等它被第二个人用第二次再固化。
- 推送用 SSE 推「信封 + 变化字段」：全量 56.9 KB，增量均 1.99 KB（省 97%）。
  契约测试在 `engine/tests/WebSnapshotProtocolTests.cs`（10 条，**按字节数**守，不是按字段数）。
- 状态所有权：一条专用线程独占 `GameEngine`（引擎是单线程可变对象，ASP.NET Core 用的是线程池），
  HTTP 命令走 `Channel` 投递。
- 换包走 URL（`?package=<id>`），包是运行时扫描输出目录发现的，宿主里没有包名字面量。
- 调试用「跳层门」：默认关闭，只有设了环境变量 `NEKO_DEBUG_KEY` 才存在，
  且跳层会话**不写存档**。方案见 [../engine/docs/WEB_DEBUG_GATE_PLAN.md](../engine/docs/WEB_DEBUG_GATE_PLAN.md)。

**还没做**（都是决策，不是遗漏）：二周目界面。
永久升级线**已经铺了**（2026-10-02，随 `NekoClicker.Core` 1.4.0）：转生后保留的那条线单独成面板
（`#tab=permanent`），按 `isPermanent` 分组、按服务端给的 `usesPrestigeCurrency` 选钱包——
前端不再认识 `UpgradeCurrency` 这个枚举（老写法 `currency === 1` 一错就是静默的）。
它刻意不服从 `hiddenUntilUnlocked`：这条线要在第一次舍命之前就可见，否则玩家不知道该攒什么。
离线收益弹窗**已经铺了**（2026-10-02，随 1.3.0）：读档补发的那一笔会以
一张压屏卡片播报一次，数据来自引擎新公开的 `GameSnapshot.Offline`；"收下"发一条
`dismissOffline` 命令让**服务端**把它清掉——去重状态放前端一定会错（两次离线补发可能数值完全一样；
按标签页记则刷新不弹、新开标签页又弹）。端到端检查因此从 26 项涨到 44 项
（含永久线的货币语义三件），其中最后一段会带同一份存档**再起一次宿主**来验它。
通知面板**已经铺了**（2026-10-01）：`wwwroot/` 里多了第六个面板「日志」，
把引擎一路推来的 `GameSnapshot.Notifications` 画出来（`#tab=log` 可直接分享）；
**未读游标 / 角标没做**——那要新增状态 + 公开字段，是另一件事。
剩下几项各自的落点、坑，以及"哪一项得先定语义再动手"，见[根目录 STATUS.md 的 §8](../STATUS.md)。

`hosts/Web/` 自带一个单项目解决方案，**刻意不挂进 `NekoClicker.sln`**——理由写在根 README
的「环境说明」里。代价是主 sln 编不到它，所以 `tools/build.ps1` 两条都编，
一条命令即可验收全仓库：

```powershell
.\tools\build.ps1 -Strict      # 引擎 + 内容 + 484 条用例 + Web 宿主
.\tools\web.ps1   build -Strict  # 只编 Web 宿主时用它
```

---

## 文档

| 文档 | 是什么 |
|---|---|
| [docs/ROADMAP.md](docs/ROADMAP.md) | 实施规划与**决策记录**：11 项已定决策（标注可逆性）、4 条架构不变量、阶段 0~6 的交付与复盘 |
| [docs/NINE_LIVES_DESIGN.md](docs/NINE_LIVES_DESIGN.md) | 《九命猫娘》世界观映射：1 个共享核心 + 10 个内容包 |
| [docs/PACK_01_CAT_CAFE.md](docs/PACK_01_CAT_CAFE.md) | #1《猫娘咖啡馆》的完整内容规格，也是其余九个包的模板 |
| [docs/STAGE_5_RESKINS.md](docs/STAGE_5_RESKINS.md) | 换皮批产手册：怎么写第五个换皮包、验收命令、已知坑 |

引擎侧的文档（架构、内容作者指南、版本承诺）在 [`engine/docs/`](../engine/docs/)。
`README.md` 与 `CHANGELOG.md` 留在仓库根——版本守卫直接读它们。

---

## 这个目录体现的一条判断

`games/docs/ROADMAP.md` 里反复出现同一句话：**代码不是瓶颈，文案才是**。

引擎侧总量约 4 个 M 级系统；内容侧每个包都是一次 L 级投入，而真正吃时间的**不是写**，
是**条件编排**——40 条叙事的释放条件要两两不撞车、线内不能倒挂、门槛不能超过本层完成门槛。
这类错误全是**沉默失败**：测试不会红，运行时不报错，只有玩到那一段才看得出来
（首版实测 72% 的条目撞车、11 处序号倒挂）。

所以这里的守卫不是"能跑就行"，而是**用故障注入证明守卫真的会红**——
一条永远不报警的守卫与一条正确的守卫，在测试输出里长得一模一样。
