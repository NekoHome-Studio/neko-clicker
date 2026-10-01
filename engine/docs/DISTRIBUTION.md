# 分发形态：现在能发什么、还差什么

> 状态：**已量**，结论待你确认。本文记录的是**实测**，不是设想。

---

## 1. 今天"陌生人拿到仓库到玩上"要几步

```
① 装 .NET 8 SDK
② git clone
③ pwsh -File tools/play.ps1          终端那侧即可开玩（不需要联网）
④ pwsh -File tools/web.ps1 run        Web 那侧：构建 + 起服务 + 自动开浏览器
```

第 ③ 步**不需要网络**：仓库零第三方依赖，`tools/dnet.ps1` 把 CLI home 与 NuGet 缓存
重定向到仓库内（受限环境也能构建）。所以"能玩"的门槛是**一个 .NET 8 SDK**，不是网络。

## 2. 发布产物：实测两条路，一条通一条不通

### ✅ 框架依赖发布 —— 通，且产物能跑

```
tools\dnet.ps1 publish games\hosts\Demo.Cli\NekoClicker.Demo.Cli.csproj -c Release -o <目录>
```

- 退出码 0，**41 个文件 / 2.1 MB**，含 `neko-clicker.exe`（apphost）与全部 12 个内容包 dll。
- **直接起 `neko-clicker.exe`（不经 `dotnet run`）跑完一小时模拟，退出码 0** ——
  也就是说发布产物是可用的，不是"编出来但起不来"。
- 前提：目标机器要有 **.NET 8 运行时**。

### ❌ 自包含发布（`-r win-x64 --self-contained`）—— 在这台机器上结构性做不到

```
error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json
（每一个项目都报同一条）
```

原因不是配置写错：**自包含需要 runtime pack（`Microsoft.NETCore.App.Runtime.win-x64`），
那是 NuGet 包**，无网就是没有。而 `tools/seed-packages.ps1` 只从本机 SDK 的 `packs/`
目录种 **TargetingPack** 与 **AppHostPack**，实测 `.packages` 里**一个 runtime pack 都没有**。

所以"单文件、零前提"这条分发路线**只能在有网的机器上出**，本机出不来——
这一点必须写清楚，否则下一个人会以为是参数写错了。

## 3. 结论：两条可选形态

| 形态 | 产物 | 前提 | 现在能不能出 |
|---|---|---|---|
| **A. 框架依赖 zip（推荐）** | 2.1 MB（Demo）/ 加上 Web 也就几 MB | 目标机器装 .NET 8 运行时 | **能，已实测** |
| B. 自包含 zip | 约 70 MB | 无（双击即玩） | **不能**，需要在有网的机器上出 |

推荐 A：它今天就能出、体积小，而"装个 .NET 8 运行时"对一个会 clone 仓库的人来说
不是门槛。B 的价值在"发给完全不装开发环境的人"，那是有网机器上的一条命令的事，
不该为它把仓库的结构改了。

## 4. 已补验（原先"还没量"的三条）

> 三条在本轮补验完毕，原始待验清单存档在下面，不再改动（保留当时的判断过程）。
>
> - **① wwwroot 会不会漏 → 不会。** `publish` 产物里有 `wwwroot/app.css`、`app.js`、
>   `index.html`；而且**直接起发布产物**（`web\neko-clicker-web.exe --urls ...`，不经
>   `dotnet run`）：`/api/ping` 正确、`/` **HTTP 200**、`/app.js` **HTTP 200**。
>   那条"直接起 exe 首页会 404"的警告只适用于 `bin` 构建产物，不适用于 publish。
> - **② `tools/pack.ps1` → 已补。** 它读 `Directory.Build.props` 的 `<Version>` 作为
>   版本号唯一来源，发布两个宿主、把 `engine/docs/PACK_README.md` 作为 `说明.md` 拷进去、
>   压成一个 zip。实测产物：**87 个文件、压缩后 1.6 MB**（未压缩 4.3 MB）。
>   说明书里写清了"需要 .NET 8 运行时"以及"要自包含包就在有网机器上跑那条命令"。
> - **③ 逐个包加载 → 仍未逐个验。** 只抽查过 `cafe`。这条不影响发布，但别写成"已验证"。

原始待验清单（当时写的，保留）：

1. **Web 宿主的 publish 是否带上 `wwwroot`。**
   `tools/web.ps1` 的注释说开发期静态文件由 `bin` 里的 `*.staticwebassets.endpoints.json`
   解析到**源码目录**，只有 publish 才复制进输出目录——也就是说**直接发布产物起服务，
   首页可能会 404**。这条我还没验（我只验了发布 Demo、以及用 `dotnet run` 起 Web）。
   发货前必须验：**publish 出来直接起，首页与 /api 都要通**。
2. **`tools/pack.ps1` 还不存在。** 现在要出 zip 得手敲 publish 命令 + 手动压缩。
   应该补一个脚本，把 Demo 与 Web 都发出来、压成一个带版本的 zip。
3. **发布产物的 `--package` 全包可用性**：只抽查了几个包（cafe 跑通），
   11 个包是否都能在发布产物里加载没逐个验。

## 5. 一句话

**"能玩"这一格的真正缺口不是代码，是一层打包 + 一句前提说明。**
产物本身已经能用（实测 `neko-clicker.exe` 跑通），缺的是把它做成可交付的 zip
并写清"需要 .NET 8 运行时"。
