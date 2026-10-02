# 夜间交接件（2026-10-02 夜 ~ 2026-10-03 09:00 前）

> **这份文件是给早上接手的人（你）看的。** 它记录"现在到哪了、下一步按什么顺序做、每条的证据在哪"。
> 等那棵被 sibling agent 改着的树**冻结之后**，把本文件的内容并入 `OPEN_WORK.md` 的 §0，
> 然后删掉本文件（避免两份状态文档各说各话）。

---

## 1. 现在的一行状态

**HEAD `aa66d13`（已提交的部分是文档）；工作区里还有 64 项未提交，属于两个切片；
`tools\build.ps1 -Strict` 我最后一次实测是 `468 通过 / 1 失败（共 469）`——那条红**已被该 agent 修好**
（是**测试的断言与驱动步骤不配套**，不是引擎缺陷；精确机制见 §3），它正在跑全量确认。**最终数字以你重跑为准。**

## 2. 为什么没提交：一条我自己定的规矩

`EndingSystem` 那个切片由 subagent `d7111db7` 负责，**它在我写这份文件时仍在运行**（持续在跑 `-Strict`，
`dotnet` 进程 CPU 持续增长）。我给自己定的规矩是：

> **一个正在被运行的 agent 改的树，不得编辑、不得提交、不得当作签收依据。**

所以：**我一个字都没碰它那 64 个文件**，只提交了我自己的两个文档（`aa66d13`）。

## 3. 那一条红是什么（**已由该 agent 修好；我最初的诊断是错的，见下**）

> ⚠️ **更正**：本节最初写的是"这是**引擎过度阻塞**"。**那个诊断是错的**——我只读到断言失败的位置就下了结论，
> 没有去查 `IsReady` 读的是什么。该 agent 给出精确机制并已修复；下面是更正后的记录。

```
✗ NineLivesEndingTests.AvoidingEveryCommitmentYieldsTheFallbackEnding
  AssertionException: 结局条件已经成立。
```

**真实原因：测试自己的断言与驱动步骤不配套，不是引擎过拦。**

- `EndingSystem.IsReady` 读的是**计数器** `$ending_ready_at_play_time`，而那个计数器**只有在
  `EndingSystem.Check` 真的跑过一次、且那一刻结局条件成立时才会被写上**。
- 那条用例（该 agent 23:17 重写的版本）把 `engine.State.Era = 9;`（**直接赋值，不经过任何检查**）
  与 `Check.True(EndingSystem.IsReady(...))` 放在一起——**它断言的是一个还没被写下的诊断值**。
- 1.5.0 的老版本之所以能过，是因为它中间调了一次 `engine.CheckEnding()`（那次调用顺手把就绪计数器写了）；
  重写时删掉了那次调用（理由是"全答完了就不该再指望『就绪但被拦』这个状态"），**却留下了 IsReady 断言**。

**修法（是收紧、不是放宽）**：删掉那条断言，改为**直接问判定本身**：

```csharp
EndingDefinition? reached = engine.CheckEnding();
Check.NotNull(reached, "六次表态都答完之后判定不该再等任何人（1.6.0 等的是作答，队列已经空了）。");
Check.Equal("end_blank", reached!.Id, "不押任何一条立场也必须有一个收场。");
Check.Equal("end_blank", engine.ReachedEnding?.Id, "落定的结局要能被读到。");
```

"就绪但一直没落定"那半由同文件的 `NeverAnsweringMeansNoEndingAtAllAndThatIsDeliberate` 覆盖
（它先 `Simulate`，所以 `IsReady` 在那里**是**有效的）。文件注释写清了这里**为什么不用** `IsReady`，
免得下一个人再加回去。

**该 agent 另外逐条查了 `EndingGraceTests` / `LabEndingTests` 里其余 7 处 `IsReady` 断言**：
都排在 `Simulate` / 跑图循环之后，**没有第二处同类不配套**。

删除 Lab 的 `incubator` 那次（建筑切片）与本条无关；本条只需按 §4 重新跑一次全量确认。


## 4. 早上按这个顺序做

1. **确认 `d7111db7` 已停**（`list_agents` 或直接看工作区是否还在变、有无 `dotnet` 进程）。
2. **跑 `powershell -NoProfile -File tools\build.ps1 -Strict`** → 期望全绿。
   - 若仍红：**先读那条断言与 `EndingSystem.Check` / `ChoiceSystem.AnswerablePendingCount`**，
     弄清新闸门在这个状态下的实际取值，再决定改引擎还是改用例。
     **不要为了让套件变绿而放宽断言。**
3. **两个提交**（提交信息已备好，用 `-F` 走文件，**不要**把中文写进命令行字面量）：
   ```
   git add <引擎那几个文件>   # engine/core/GameEngine.cs, Simulation/ChoiceSystem.cs,
                              # Simulation/EndingSystem.cs, PublicApi.txt, Directory.Build.props,
                              # CHANGELOG.md, engine/docs/VERSIONING.md,
                              # engine/docs/OPEN_WORK.md, engine/docs/ENDING_GRACE_PLAN.md,
                              # engine/README.md, README.md, STATUS.md, engine/tests/*
   git commit -F .git/COMMIT_MSG_ENDING.txt

   git add engine/content engine/tests/ContentTextFileTests.cs engine/docs/TEXT_AS_DATA_PLAN.md
   git commit -F .git/COMMIT_MSG_BUILDINGS.txt
   ```
   ⚠️ 两个切片的文件**有交叠**（`engine/tests/`、文档），`git add` 时请**按文件而不是按目录**，
   或者接受"文档跟着第一个提交走"。
4. **推送**（需要审批，所以只能在你在的时候做）：
   ```
   git push origin main
   git ls-remote origin refs/heads/main     # 服务器侧核对，不信本地缓存
   ```
5. 把本文件并入 `OPEN_WORK.md` §0，并补上第 5 节那三处文档缺口。

## 5. 三处**已知的**文档缺口（不许留着不说）

1. **`OPEN_WORK.md` 的过时措辞**：J 条与 §5 里"Web 宿主在**表态面板显示时**上报 `choicesShown`"
   ——1.6.0 之后 `choicesShown` **不再是闸门**（判据变成"答完"），措辞要改。
2. **`Icon` 要不要跟着外置**：`storylines` **连 icon 一起外置**了（`CONTENT_AUTHORING.md` §12.0 里
   就有 `Icon = Prose.Text("storylines", id, "icon")`），**建筑没有**——`Icon` 还留在代码里。
   两者不一致，需要你定：搬出去，还是搬回来。**在此之前新写建筑就照现状。**
   （已记在 `CONTENT_AUTHORING.md` 新增的 §12.0.1 与 `SETTING.md` 第五节。）
3. **6 处用例数漂移仍未修**：`README.md`（2 处）、`STATUS.md`（3 处）、`RELEASING.md`（1 处）
   写着 `441 / 439`，而实际已 `470`（1.6.0 收尾后请以**实测**为准）。
   **两条历史 CI 记录（`STATUS.md:222` 的 435、`RELEASING.md:120` 的 439）不要动**——那是历史。
   ⚠️ 批量替换数字**不安全**：我曾试过，被自己设的"命中数与预期不符就不动"保险拦下
   （那些文件里同样的数字还有别的出处）。请**带上下文逐处改**。

## 6. 我这一夜**已经独立核对过**的事实（可直接引用，不必重跑）

| 核对项 | 结论 | 怎么自己复核 |
|---|---|---|
| `PublicApi.txt` 只变版本行 | ✅ `git diff --numstat` = `1 1`，diff 内容只有 `version=1.5.0 → 1.6.0` | `git diff engine/core/PublicApi.txt` |
| `MarkPendingChoicesShown()` 未被删 | ✅ 仍在公开快照里（降级为诊断） | `Select-String MarkPendingChoicesShown engine/core/PublicApi.txt` |
| `AnswerablePendingCount` 是 `internal` | ✅ 不在公开快照里 | 同上 |
| `VERSIONING` §2 记了第三次 minor 例外 | ✅ 并把判据三代演进写清（30 秒 → 1.5.0「被展示过」→ 1.6.0「答完」） | 读 `engine/docs/VERSIONING.md` 开头 |
| `CHANGELOG` 有带日期的 1.6.0 | ✅ `## [1.6.0] - 2026-10-02` | 读 `CHANGELOG.md` |
| 建筑**确实是 104 座**（不是 93） | ✅ 我**从 `text.json` 独立数了一遍**：NineLives 12 / Cafe 10 / Neko 10 / 其余各 9 | 遍历各包 `text.json` 的 `buildings` 节 |
| 每座建筑的 `name` 与 `description` **都齐全** | ✅ 0 处缺失（没有"迁了 id 却没迁文本"的半迁移条目） | 同上 |
| `saves/` **一个字节都没被写过** | ✅ 11 个文件（含 `.bak`）跨轮次比对：大小 + sha256 与开工前基线**逐项一致** | `Get-FileHash saves\*` |

## 7. 我自己犯过、值得记下的几个错（免得重复）

- **数错建筑数**：从源码里数 `new() {` 的出现次数得出 93，把 `NineLives` 数成 1 座。
  **要量就从跑起来的引擎或数据文件里量。**
- **误判那条红用例**：没读代码就先给它安了个"旧语义残留"的解释；读了那 10 行之后**结论完全相反**
  （是引擎过度阻塞）。**先读，再解释。**
- **把中文写进 shell 字符串字面量**：导致 `ParserError`、整条命令没执行（一夜之间第三次同类）。
- **`edit` 工具要求先用 `read` 读过文件**：用 pwsh 扫行**不算读过**；读一个 10 行窗口即可。
