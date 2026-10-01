# 整合 `feature/web-frontend-ui`：合并方案

> 状态：**待执行**。本文只定怎么做，还没有动过任何分支。
> 合并面是量出来的（见第 3 节），不是估的。

---

## 1. 两条线现在长什么样

分叉点：`fa4651d`（阶段 6：版本号 + 公开 API 快照守卫 + v1.0.0）。

| | main（本地＝`origin/main`） | `origin/feature/web-frontend-ui` |
|---|---|---|
| 分叉后提交 | **10** | **5** |
| 最新 | `c66f00b` 套件并行化（261s → 150s）；版本按 1.1.0 | `a252f70` 文档：新增 STATUS.md；修正 README 两处与事实不符 |
| 改动文件数 | 23 | **242**（绝大多数是移动） |

`origin/suan` **不需要合**：它已经是 `origin/main` 的祖先（0 领先），早已并入。

## 2. 它带来什么（为什么值得合）

1. **目录重构**：`src/` → `engine/` + `games/` 两层。`engine/` = 可独立提取的引擎
   （`core` / `content` / `tests` / `docs`），`games/` = 宿主（`hosts/Demo.Cli`、`hosts/Web`）。
2. **Web 前端可玩闭环**：SSE 增量协议 + 单线程状态所有权 + 零依赖界面
   （`games/hosts/Web/`，带自己的 `NekoClicker.Web.sln`）。
3. **`tools/web.ps1`**：起 Web 宿主的入口。
4. **`docs/STATUS.md`**：项目现状文档（我这边一直没有）。
5. **`app.js` 的一处真修复**：大数字"一跳一跳"的根因是**追离散目标**，不是刷新率不够。
6. **README 两处与事实不符的声明的修正**。

## 3. 合并面（实测）

```
同名双方都改：      3 个 —— CHANGELOG.md / Directory.Build.props / README.md
改名且双方都改：    3 个 —— tests/Program.cs(R079→engine/tests/)、
                          engine/docs/VERSIONING.md(R099→engine/docs/)、
                          games/docs/ROADMAP.md(R098→games/docs/)
它改我不改：        tools/*.ps1 四个（build / dnet / play / public-api）+ 新增 web.ps1
我改它只移动(R100)： 核心与测试的关键文件全部是纯改名 ——
                    engine/core/{GameEngine,Simulation/EndingSystem,Simulation/ChoiceSystem,
                    Content/GameContentBuilder,PublicApi.txt}、
                    engine/tests/{ChoiceTests,EndingTests,MiniTest,NineLivesEndingTests,PrestigeTests}、
                    games/hosts/Demo.Cli/{CliOptions,GameSession,HeadlessRunner,Program}
```

**这条是最重要的结论：我改过的核心文件在它那边全是 R100 纯移动。**
git 的三方合并会把它处理成"改名 + 我的内容修改"，**不产生冲突**。
真正要人动手的只有上面那 **6 个**文件。

## 4. 六个冲突怎么解（逐条规则）

| 文件 | 双方的改动 | 解法 |
|---|---|---|
| `Directory.Build.props` | 它：注释里的路径改成 `engine/docs`、`engine/core/PublicApi.txt`；我：版本号 → 1.1.0 | **两边都要**：取它的路径 + 我的版本号 |
| `CHANGELOG.md` | 它：1.0.0 条目；我：新增 1.1.0 条目 | 取我的全文（含 1.1.0 与"例外"说明），若它加了内容再叠 |
| `README.md` | 它：修正两处与事实不符的声明、目录结构；我：当前版本 1.1.0、用例数 411、`--timing`/`--serial` 用法 | **两边都要**：它的结构描述 + 我的版本/用例数/新工具说明 |
| `engine/tests/Program.cs` | 它：随移动重写；我：加了 `--timing` / `--serial` / `--jobs` | 以它重写后的为底，把我的三个参数解析叠回去 |
| `engine/engine/docs/VERSIONING.md` | 它：路径与结构；我：1.1.0 的"已知例外"一节、版本号 | **两边都要** |
| `games/games/docs/ROADMAP.md` | 它：路径；我：K8 风险行 | **两边都要** |

## 5. 合并之后必须补的活（不在冲突里，但漏了会静默失效）

1. **`--timing` / `--serial` / `--jobs` 的参数转发还活着吗。**
   它**重写过 `tools/build.ps1`**，而这三个开关是我在它重写之后加的。
   这是本次最危险的静默点：转发断了不会红，只会"参数被忽略"。
   → 合并后必须实跑 `build.ps1 --timing` 与 `--serial` 各一次，确认行为真的变了。
2. **两个 sln 的构建覆盖。** 仓库里现在有两个：`NekoClicker.sln` 与
   `games/hosts/Web/NekoClicker.Web.sln`。若 `build.ps1 -Strict` 只编前者，
   那么"一条命令验证全部"这个性质就断了——Web 宿主坏了不会有人知道。
3. **快照路径。** `engine/core/PublicApi.txt` → `engine/core/PublicApi.txt`；
   `tools/public-api.ps1` 的目标与 `PublicApiSnapshot` 的嵌入资源路径都要对得上。
   内容应保留我这边 1.1.0 的那份（它那边这份没动过）。
4. **文档与注释里的旧路径全量更新**：`engine/docs/ENDING_GRACE_PLAN.md`（我写的）、
   `Directory.Build.props` 注释、README、ROADMAP、VERSIONING、
   以及测试里的 `RepositoryRoot()` 之类按路径找文件的代码。
5. **我那份设计文档放哪。** 它是**引擎级**设计文档，按新分层应搬到 `engine/docs/`；
   合并后它会孤零零留在顶层 `docs/`（该目录在它那边只剩 `STATUS.md`）。
6. **版本号**：保持 1.1.0，不要被它带回 1.0.0。

## 6. 执行步骤

```
① 开分支：git switch -c integrate/web-frontend-ui main     # 不在 main 上直接合
② 打备份：git branch backup/pre-merge main                 # 回退用
③ 合并：  git merge origin/feature/web-frontend-ui
④ 按第 4 节解 6 个冲突；按第 5 节补活
⑤ 验证：  见第 7 节
⑥ 全绿后：git switch main && git merge --ff-only integrate/web-frontend-ui
```

**不在 main 上直接合**：这是一次 242 文件的树重构，中途失败时 main 必须保持可用。

## 7. 验收（缺一条就不算合完）

- [ ] `tools/build.ps1 -Strict` 退出码 0、0 警告
- [ ] 411 条用例全绿（并行默认）
- [ ] `build.ps1 --timing` 真的逐条计时；`--serial` 真的串行；`--jobs 2` 真的限流
- [ ] 公开 API 快照头 `version=1.1.0`，且 `PublicApiTests` 四条全绿
- [ ] `NekoClicker.sln` 全项目可编（含 Demo.Cli 的新路径）
- [ ] `games/hosts/Web/NekoClicker.Web.sln` 可编，`tools/web.ps1` 能起
- [ ] 终端演示实跑一次（`--package lab --simulate`），确认没有旧路径残留导致的运行期失败
- [ ] 仓库里没有指向 `src/`、`tests/`、裸 `engine/docs/VERSIONING.md` 的**失效引用**
      （用 grep 扫一遍，别靠眼睛）

## 8. 风险与回退

| 风险 | 处置 |
|---|---|
| 树重构把某条路径搞漏，表现为"某些项目不再被编译" | 第 7 节的"两个 sln 都可编"是专门防它的 |
| 我的三个测试开关静默失效 | 第 5 节第 1 条，必须实跑 |
| 合并结果不可用 | `git merge --abort`（未提交时）或 `git reset --hard backup/pre-merge`（已提交时） |
| 队友分支后续还会更新 | 本次合并成一个普通合并提交；它之后再动就再合一次 |

## 9. 需要先定的一件事

**顶层 `docs/` 保不保留？** 它那边顶层 `docs/` 只剩 `STATUS.md`，其余都进了
`engine/docs/` 或 `games/docs/`。两个选择：

- **(a) 跟着它的分层走（推荐）**：我的 `ENDING_GRACE_PLAN.md` 搬进 `engine/docs/`，
  `STATUS.md` 也一并搬，顶层不再留 `docs/`。好处是分层彻底、没有"例外目录"。
- **(b) 顶层保留 `docs/`** 作为跨层文档（现状 + 我的方案）。好处是不用动我那份文档的路径，
  代价是同一类东西有两个地方能找到——下一个人会猜错。

选 (a) 的话我会顺手把 `Directory.Build.props`、各 README 里提到的文档路径一起对齐。
