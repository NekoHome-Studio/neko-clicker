# 怎么玩这个包

## 前提

**需要 .NET 8 运行时**（不是 SDK，运行时就够）：
<https://dotnet.microsoft.com/download/dotnet/8.0>

这一版是**框架依赖发布**：包小（几 MB），但目标机器要有 .NET 8。
之所以不做成"双击即玩"的自包含单文件，是因为那需要 `Microsoft.NETCore.App.Runtime.win-x64`
这个 NuGet 包，而打包环境没有网络取它。若你需要零前提的包，在一台有网的机器上跑：

```
dotnet publish games\hosts\Demo.Cli\NekoClicker.Demo.Cli.csproj -c Release -r win-x64 --self-contained true
```

## 两个宿主

### 终端（推荐先玩这个）

```
demo\neko-clicker.exe
```

启动后是全屏终端界面。**默认是「猫咖物语」**（框架的技术演示）。换内容包：

```
demo\neko-clicker.exe --package lab
demo\neko-clicker.exe --package ninelines
demo\neko-clicker.exe --help          # 全部参数与内容包清单
```

十一个包：`neko` / `cafe` / `ninelines` / `lab` / `company` / `apocalypse` /
`library` / `god` / `civ` / `cyber` / `dream`。

不想自己点？无头跑一段并打印报告：

```
demo\neko-clicker.exe --package cafe --simulate 21600 --auto
```

### Web

```
web\neko-clicker-web.exe --urls http://127.0.0.1:5273
```

然后浏览器打开 <http://127.0.0.1:5273>。换内容包加 `--package <id>`。
从哪个目录启动都行——宿主自己会找到它旁边的 `wwwroot`（1.3.0 起；更早的包必须在
`web\` 目录里启动，否则首页 404 而 `/api/*` 正常）。

## 链接：分享存档 / 跳到某一层

**分享存档**：打开游戏里的「存档」窗口，在「分享链接」一节设一个密码（**至少 4 个字符**）
生成链接，把链接和密码分别发给对方。对方打开链接、在出现的那一行输入同一个密码就能导入；
**密码不在链接里**（链接装的是密文）。链接装不下（超过 8000 字符）时会拒绝生成，改用
「导出」的复制粘贴或下载 `.json`。

**跳到第 N 层**（调试用）要先设一把钥匙 `NEKO_URL_KEY`（与调试门的 `NEKO_DEBUG_KEY`
是两把，互不通用），**起宿主的那个 shell 与铸链接的那个 shell 要用同一把**：

```
$env:NEKO_URL_KEY = '随便一段短的'
web\neko-clicker-web.exe --mint-link --package lab --era 7 --expires-hours 24
```

链接走 stdout、说明走 stderr；参数不对、包不存在、层号越界，或者这个 shell 里没设
`NEKO_URL_KEY`，都会**拒绝铸造**（退出码 2、不产出链接）。宿主那侧没设同一把钥匙时，
带 `k` 的请求一律 403。
**终端宿主不做链接**（终端没有地址栏）——那侧继续用「导出到文件 → `I` 导入」。

## 存档

放在启动目录下的 `saves/`。加 `--no-save` 可以不落盘。
