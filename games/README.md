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

**骨架阶段：还没有游戏界面。**

已做的：`/api/ping`（宿主自报框架版本——阶段 6 的 `ApiVersion` 第一个真实消费者）、
`/api/packs`（运行时扫描 `NekoClicker.Content.*.dll` 发现内容包，宿主里没有包名字面量）、
一个证明"引擎 → 浏览器"这条线通了的骨架首页。

**刻意没做的**（两个未定决策，写在骨架页与 `Program.cs` 注释里，不替使用者决定）：

1. **快照怎么推**：轮询还是 SSE？引擎 tick（30Hz）与推送频率是什么关系？
2. **存档槽位与离线补算怎么接线**：用哪个槽位、`AutoSaveInterval` 走默认 60 秒还是另设。

`hosts/Web/` 自带一个单项目解决方案，**刻意不挂进 `NekoClicker.sln`**——理由写在根 README
的「环境说明」里。所以完整验证要跑两条命令：

```powershell
.\tools\build.ps1 -Strict      # 引擎 + 内容 + 404 条用例
.\tools\web.ps1   build -Strict  # Web 宿主
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
