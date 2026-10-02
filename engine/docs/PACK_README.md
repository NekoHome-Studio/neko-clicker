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

## 存档

放在启动目录下的 `saves/`。加 `--no-save` 可以不落盘。
