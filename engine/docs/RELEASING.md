# 发布流程

> 这份文档回答一个问题：**要把 NekoClicker 发一个新版本，按什么顺序做什么、每一步的判据是什么。**
>
> 版本号的语义（什么改动升哪一位）见 [VERSIONING](VERSIONING.md)；当前状态见 [STATUS](../../STATUS.md)；
> 逐版本改动见 [CHANGELOG](../../CHANGELOG.md)。这里只讲**操作**：命令、顺序，以及这台机器上会咬人的地方。
>
> 写于 1.2.1 发布时（2026-10-01），并带三节**实际执行记录**（§3 的 1.2.1、§4 的 1.3.0、§5 的 1.4.0）。
> 流程文档最怕写成"应该怎样"——没跑过的那一步，下一次没人知道它是事实还是猜想。

---

## 1. 顺序（以及为什么是这个顺序）

按 [VERSIONING](VERSIONING.md) §4 的规矩，**快照重生成是最后一步**。完整的发布长这样：

| # | 做什么 | 判据 / 说明 |
|---|---|---|
| ① | 改代码（如果这次要改） | — |
| ② | 对照 VERSIONING §2 决定升哪一位 | 只增 = minor；不兼容 = major；公开 API 没动 = patch |
| ③ | `Directory.Build.props`：`Version` / `AssemblyVersion` / `FileVersion` **三处一起** | 单一事实来源；别处不许再写一份 |
| ④ | `CHANGELOG.md`：加 `## [<版本>] - YYYY-MM-DD` | 写清兼容性影响；`VersionTests` 会验"有带日期的条目" |
| ⑤ | **先构建一次**，再跑 `tools\public-api.ps1` | 快照由测试程序集打印，`ApiVersion.Current` 读程序集元数据——版本号改了没重编，快照会带**旧版本号**且看起来完全正常 |
| ⑥ | 扫一遍所有"当前版本"字样 | 清单见 §2；判据是 `git grep <旧版本号>`，不是记性 |
| ⑦ | `tools\build.ps1 -Strict` | 516 用例 + 公开 API 快照 + 版本守卫；**0 警告** |
| ⑧ | `tools\api-test.ps1` | 全部端到端检查通过（真起宿主、真读 SSE；当前 55 项，脚本收尾还会自己做检查点覆盖审计）；动了宿主/前端时必跑 |
| ⑨ | `tools\pack.ps1` + **自检产物** | 产出 `artifacts\neko-clicker-<版本>-win-x64.zip`；**解开它**，在**包根目录**起 `web\neko-clicker-web.dll`，确认 `/`、`/app.js`、`/api/ping` 与快照里的新字段。这一层没有用例守着（端到端探针只跑开发期路径）——1.3.0 的首页 404 就是这么逮到的 |
| ⑩ | `git commit` → `git tag -a v<版本>` → 推送 | 本机 HTTPS 不通，走 SSH（见 §6） |
| ⑪ | 看 CI 的两个作业，把结果写回 `STATUS.md` §6 | "本地全绿"不等于"runner 上全绿" |

第 ⑤ 步拆成两步是 1.2.1 才写清楚的细节；在此之前它只是 VERSIONING 里的一句
"跑 `tools/public-api.ps1` 更新快照"，没说什么时候跑。

---

## 2. 会写版本号的每一处（一样也别漏）

| 文件 | 写什么 |
|---|---|
| `Directory.Build.props` | **单一事实来源**：`Version` / `AssemblyVersion` / `FileVersion` |
| `engine/core/PublicApi.txt` | **不手改**：`tools\public-api.ps1` 生成，首行 `version=` 一并换掉 |
| `CHANGELOG.md` | `## [<版本>] - YYYY-MM-DD` 条目 |
| `README.md` | 顶部**第一处**反引号版本号（`VersionTests` 就锚在这里）+ 别处的"当前版本" + 代码示例的输出注释 |
| `engine/README.md` | 「当前 `x.y.z`」 |
| `engine/docs/VERSIONING.md` | 开头当前版本 + §1 的 XML 示例 + `ApiVersion` 输出示例 + 快照行数 |
| `STATUS.md` | §1 表格与"当前版本"段 |
| 其它 `*_PLAN.md` | "发布时按 patch / minor"这类句子，改成既成事实 |

**只改"现在时"，不改"历史"**：CHANGELOG 里的旧版本条目、ROADMAP 的交付记录说的都是当时的事，
按设计就该留着旧版本号。所以判据不是"全仓库搜不到旧版本号"，而是"每一处旧版本号都确实在讲历史"：

```powershell
git grep -n "1\.2\.0"        # 换成你刚发完的那个版本号
```

---

## 3. 执行记录：1.2.1（2026-10-01）

### 3.1 结果

| 项 | 结果 |
|---|---|
| 升位 | **patch**（公开 API 一行没动） |
| 实质 | 421 条剧情散文铺满十个包 + `ContentText` 并发修复——补的是 **`v1.2.0` 已经发出去的**缺陷 |
| 快照 diff | 只有首行 `version=1.2.0` → `version=1.2.1`（1909 行不变）——这就是"公开表面没动"的证据 |
| 验收 | `build.ps1 -Strict`：**435 全绿、0 警告**；`api-test.ps1`：**22 项全过**（本机缺 ASP.NET 8 运行时，脚本自动设 `DOTNET_ROLL_FORWARD=Major` 上滚到 10.0.11 并打印出来） |
| 产物 | `artifacts/neko-clicker-1.2.1-win-x64.zip` |
| tag | `v1.2.1`，提交与 tag 均以 SSH 推送 |

### 3.2 清单外看见的三件事

1. **README 里有两行过期信息不在守卫射程内**：代码示例的输出注释仍写着 `"1.2.0"` /
   `1.2.0.0`，还有一句"1897 行的公开表面清单"——`ContentText` 公开后实际已是 **1909 行**。
   发布清单只列了"两处当前版本"，是 `git grep` 把它们翻出来的。
   **教训：清单列的是"守卫会红的地方"，不是"错信息存在的地方"。**
2. **"工具与 CI 要不要进 CHANGELOG"这个取舍，这次定的是"收录"**：它不改变 API 版本语义。
   理由写在 1.2.1 条目那节的开头——本仓库的 CHANGELOG 同时充当**改动记录**
   （1.2.0 条目里也记了 Web 前端、启动器与分发脚本），版本号本身仍然只描述公开 API。
3. **构建在这台机器上要先解决权限**：受限沙箱下 MSBuild 的 Roslyn 调用会被拒绝
   （`MSB3883 ... 拒绝访问`，0 Warning / 2 Error，看起来像编译器坏了）。这是环境问题，
   不是代码问题——见 [STATUS](../../STATUS.md) §7 第 5 条。
4. **STATUS 里教的推送命令是错的，这次才踩到**：原先那一版写的是一次性 URL 重写
   `git -c url.ssh://git@github.com/.insteadOf=https://github.com/ push origin main`，
   重写出来的 `ssh://git@github.com/NekoHome-Studio/...` **缺前导斜杠**，GitHub 拒收
   （`... is not a valid repository name`，5 次重试全是这个错——它不是网络抖动，重试没用）。
   换成**显式 URL** 第一次就成功：`git push git@github.com:NekoHome-Studio/neko-clicker.git main`。
   STATUS §7 第 10 条与本文 §6 都已改。
   **教训：文档里的命令如果从没在真操作里跑过，它就不是"已验证"，只是"看起来对"。**

### 3.3 CI 首跑

本仓库的 CI 其实在 `c84c478`（workflow 刚配好那次）就已经在远端跑过——但 STATUS §6 那条
"还没在远端跑过一次"一直没人回填。这次一次性拿到三次数据：

| 运行 | 提交 | 结果 |
|---|---|---|
| #1 | `c84c478`（workflow 首跑） | 两个作业都 success |
| #2 | `4bb2690` | 两个作业都 success |
| #3 | `67a1f22`（1.2.1 发布） | 构建+用例 success；Web 宿主端到端 success |

观测方式：`github.com:443` 不通，但 **`api.github.com:443` 是通的**（1.3 秒返回 200，
公开仓库只读、不要 token）；`curl` 或本机已装的 `gh` 都能看。

---

## 4. 执行记录：1.3.0（2026-10-02）

### 4.1 做到哪一步了

| 步 | 结果 |
|---|---|
| ② 升位 | **minor**——公开 API 只增不改（`GameSnapshot.Offline`、`OfflineView`、`GameEngine.PendingOfflineProgress`、`GameEngine.DismissOfflineProgress()`） |
| ③ 版本号 | `Directory.Build.props` 三处一起：`1.2.1` → `1.3.0`、`1.2.1.0` → `1.3.0.0` |
| ④ CHANGELOG | `## [1.3.0] - 2026-10-02`，并把 `[未发布]` 里攒着的宿主侧改动（Web 日志面板）收进这一版 |
| ⑤ 快照 | **先构建再生成**：1909 → **1927 行**，diff 除首行 `version=` 外只有那四个新增成员——"只增不改"的证据 |
| ⑦ 验收 | `build.ps1 -Strict`：**439 全绿、0 警告**；`api-test.ps1`：**39 项全过** |
| ⑨ 打包 | `pack.ps1` → `artifacts/neko-clicker-1.3.0-win-x64.zip`（108 文件、压缩后 1.6 MB）；**解包后在包根目录自检**：`/` 200（含弹窗挂载点）、`/app.js` 200（含 `renderOffline`）、`/app.css` 200、`/api/ping` 报 1.3.0、快照带 `offline` |
| ⑩ tag + 推送 | tag `v1.3.0` 打在 `ed25c21`（三个提交：功能 / 文档回填 / 产物修复）；提交与 tag 都用 SSH 显式 URL 推，**都是一次成功** |
| ⑪ CI | 运行 [#36975133007](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36975133007)：两个作业都 success，**209 秒**（构建 + 439 用例 / Web 宿主端到端 39 项，含那段"重启一次宿主"） |

### 4.2 这一轮逮到的三件事

1. **`[未发布]` 与版本号是两套节奏，得说清收谁**。1.2.1 的经验是"宿主侧改动攒在 `[未发布]`"，
   而这一轮引擎侧动了公开 API——按 §1 的第 ③ 步必须当场升版本，不能等。
   于是这一版同时包含 minor 的引擎新增与纯宿主的日志面板。
   写法沿用 1.2.1 定下的取舍（CHANGELOG 同时充当改动记录），并在 1.3.0 条目的开头写明。
2. **快照行数是最省事的"只增不改"证据**：1909 → 1927（+18 行 = 4 个新成员加 `OfflineView`
   这个新类型的合成成员）。反过来，如果 diff 里出现了删除行，那就不是 minor 了——
   这条判据比读一遍 diff 快，也比"我觉得没删东西"可靠。
3. **第 ⑨ 步的"自检产物"不是形式主义——它逮到了一个从 1.2.0 就在的缺陷。**
   解开 zip、在**包根目录**（说明书里那条命令的真实情形）起发布产物，得到的是
   `/api/*` 全通、首页与静态资源全 404。根因是 ASP.NET Core 默认拿**当前工作目录**当内容根，
   而 `wwwroot` 在 `web\` 下；修法是宿主自己找（程序集旁边有 `wwwroot` 就用它当内容根）。
   三条可复用的：① **开发期路径全绿 ≠ 产物能用**——端到端探针跑的是 `dotnet run`，这一层它看不见；
   ② 修的时候差点把开发期路径弄坏（无条件指向程序集目录 → 开发期 `bin` 里没有 `wwwroot`，
   静态文件三件全红），是 `api-test.ps1` 当场抓住的——所以"两种布局各跑一遍"要当成同一步；
   ③ 这一层目前仍是**人工**步骤，把它收进 `tools/pack.ps1` 是下一步的候选（STATUS §8.6）。

### 4.3 发布完成（2026-10-02）

打包产物、tag、推送、CI 都验过了：`main` 的 `ed25c21` 与 tag `v1.3.0` 都在远端
（`git ls-remote` 复核过，不是靠 push 的退出码），CI 两个作业 success。

顺带一条观测：**这次推送一次就成功了**，与 STATUS §7 第 10 条记的"要重试"不同——
但那条讲的是 `github.com:443` 的结构性问题（历史上一连 8 轮不通），失败模式是"连不上"，
而这次是 SSH 且一次通。结论不变：**push 仍然要带重试**，只是别把"一次就成"当成
"以后都不会失败"。

---

## 5. 执行记录：1.4.0（2026-10-02）

### 5.1 结果

| 步 | 结果 |
|---|---|
| ② 升位 | **minor**——`UpgradeView` 新增 `UsesPrestigeCurrency` / `CurrencyName` / `CurrencyIcon` |
| ③ 版本号 | `1.3.0` → `1.4.0`（三处一起） |
| ④ CHANGELOG | `## [1.4.0] - 2026-10-02` |
| ⑤ 快照 | 1927 → **1930 行**，diff 除首行 `version=` 外只有那三个属性——又是一次"只增不改" |
| ⑦ 验收 | `build.ps1 -Strict`：**441 全绿、0 警告**；`api-test.ps1`：**44 项全过** |
| ⑨ 打包 + 自检 | `artifacts/neko-clicker-1.4.0-win-x64.zip`（108 文件、1.6 MB）；**解包后在包根目录自检**：`/` 200（含 `data-panel="permanent"`）、`/app.js` 200（含 `renderPermanent`）、`/app.css` 200、`/api/ping` 报 **1.4.0**、快照里 47 条升级全带 `currencyIcon` 且 5 条永久行的货币图标等于快照的转生货币 |
| ⑩ tag + 推送 | tag `v1.4.0` 打在 `b418fbd`（两个提交：功能 / 文档回填）；提交与 tag 都是**一次推送成功** |
| ⑪ CI | 运行 [#11 / 36983736167](https://github.com/NekoHome-Studio/neko-clicker/actions/runs/36983736167)：两个作业 success，**229 秒** |

### 5.2 这一轮的记录

**没有新坑**——这是第一次"照 §1 的表走完、每一步都一次过"的发布：构建、快照、用例、
端到端、打包、自检、推送、CI 全部一次绿。前两轮攒下来的东西这次都吃上了：
打包后自检（§4.2 第 3 条）、"两种布局各跑一遍"、SSH 显式 URL、`api.github.com` 看 CI。

一条仍然成立的提醒：**打包自检还是人工的**。它的判据这次写进了 §1 的 ⑨（含要看的字段名），
但脚本没替人跑——把它收进 `tools/pack.ps1` 仍然是 STATUS §8.6 的候选。

---

## 6. 本机特有的坑（一条条都踩过）

- **推送走 SSH**：`github.com:443` 结构性不通，重试不会好；SSH 正常。**用显式 URL**：

  ```powershell
  git push git@github.com:NekoHome-Studio/neko-clicker.git main
  git push git@github.com:NekoHome-Studio/neko-clicker.git v1.2.1
  ```

  > 不要用 `-c url.ssh://git@github.com/.insteadOf=...` 那种一次性重写：重写出的 URL
  > 缺前导斜杠，GitHub 拒收（1.2.1 实测，见 §3.2 第 4 条）。

  想永久改：`git remote set-url origin git@github.com:NekoHome-Studio/neko-clicker.git`。
- **看 CI 走 `api.github.com`**：`github.com:443` 不通不妨碍 REST API 返回 200（公开仓库只读）。
- **`tools/*.ps1` 必须 UTF-8 with BOM**（Windows PowerShell 5.1 会按 GBK 解析无 BOM 的中文）。
  本仓库的编辑器工具会吃掉 BOM——**而且会顺手把 CRLF 变成 LF**（2026-10-02 实测，
  `git` 随后警告 `LF will be replaced by CRLF`）。两样一起补：按 `\r\n` 归一化后用
  `UTF8Encoding($true)` 写回。`.md` 不受影响。
- **`git` 报 dubious ownership**：用 `GIT_CONFIG_COUNT=1 / GIT_CONFIG_KEY_0=safe.directory /
  GIT_CONFIG_VALUE_0="D:/githb/neko-clicker"` 绕过。
- 其余（`-m:1`、nuget 不可用、Web 宿主必须 `dotnet run` 起）见 [STATUS](../../STATUS.md) §7。

---

## 7. 这份文档保证不了什么

- **没有独立的发布脚本**：步骤还是人来点的。`tools/pack.ps1` 只做打包，`public-api.ps1`
  只做快照——它们刻意不"一键发布"，因为 ②（升哪一位）与 ④（兼容性怎么写）是判断，不是命令。
- **`git tag` 打错没法悄悄撤销**：推上去之后要删，得同时删本地与远端（且别人可能已经拉过）。
  所以第 ⑦⑧ 步全绿之前不要打 tag。
