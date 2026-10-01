# 发布流程

> 这份文档回答一个问题：**要把 NekoClicker 发一个新版本，按什么顺序做什么、每一步的判据是什么。**
>
> 版本号的语义（什么改动升哪一位）见 [VERSIONING](VERSIONING.md)；当前状态见 [STATUS](../../STATUS.md)；
> 逐版本改动见 [CHANGELOG](../../CHANGELOG.md)。这里只讲**操作**：命令、顺序，以及这台机器上会咬人的地方。
>
> 写于 1.2.1 发布时（2026-10-01），并带一节**实际执行记录**。流程文档最怕写成"应该怎样"——
> 没跑过的那一步，下一次没人知道它是事实还是猜想。

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
| ⑦ | `tools\build.ps1 -Strict` | 435 用例 + 公开 API 快照 + 版本守卫；**0 警告** |
| ⑧ | `tools\api-test.ps1` | 22 项端到端（真起宿主、真读 SSE）；动了宿主/前端时必跑 |
| ⑨ | `tools\pack.ps1` | 产出 `artifacts\neko-clicker-<版本>-win-x64.zip` |
| ⑩ | `git commit` → `git tag -a v<版本>` → 推送 | 本机 HTTPS 不通，走 SSH（见 §4） |
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
   STATUS §7 第 10 条与本文 §4 都已改。
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

## 4. 本机特有的坑（一条条都踩过）

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
  本仓库的编辑器工具会吃掉 BOM——改完要补，`.md` 不受影响。
- **`git` 报 dubious ownership**：用 `GIT_CONFIG_COUNT=1 / GIT_CONFIG_KEY_0=safe.directory /
  GIT_CONFIG_VALUE_0="D:/githb/neko-clicker"` 绕过。
- 其余（`-m:1`、nuget 不可用、Web 宿主必须 `dotnet run` 起）见 [STATUS](../../STATUS.md) §7。

---

## 5. 这份文档保证不了什么

- **没有独立的发布脚本**：步骤还是人来点的。`tools/pack.ps1` 只做打包，`public-api.ps1`
  只做快照——它们刻意不"一键发布"，因为 ②（升哪一位）与 ④（兼容性怎么写）是判断，不是命令。
- **`git tag` 打错没法悄悄撤销**：推上去之后要删，得同时删本地与远端（且别人可能已经拉过）。
  所以第 ⑦⑧ 步全绿之前不要打 tag。
