# web.ps1 — 构建 / 运行 Web 前端的包装脚本。
#
# 为什么需要它（与 tools/dnet.ps1 同样的两条环境约束，加上一条"边界"约束）：
#
# 1) dotnet CLI 的 home 与 NuGet 包缓存必须重定向到仓库内（受限沙箱下 %USERPROFILE%
#    不可写），MSBuild 必须 -m:1（命名管道被禁时多节点构建会静默失败）。这两条与
#    dnet.ps1 完全一致，直接复用它的做法。
#
# 2) **Web 宿主有自己的单项目 sln**，刻意不挂进 NekoClicker.sln：它是"游戏宿主"，
#    跟引擎的发布节奏无关，挂进去只会让主 sln 多出一条与引擎无关的项目边。
#    但注意：它**不再单独覆盖目标框架**了。原先它是 net10.0，理由是"本机只有
#    Microsoft.AspNetCore.App 10.x 共享框架"——合并时实测那条不成立（本机有
#    AspNetCore.App 8.0.7，缺的是 .NET 10 的 SDK，导致它连编都编不过）。
#    现在它与整个仓库同为 net8.0，所以 tools/build.ps1 也会顺手编它一遍；
#    这个脚本仍然负责它自己的 run / clean，而**引擎那一侧的回归由
#    tools/build.ps1 单独守**（两者互不影响，改动 Web 不会动到引擎基线）。
#
# 用法:
#   powershell -File tools/web.ps1 build          构建（含全部内容包）
#   powershell -File tools/web.ps1 build -Strict  全量重编 + 警告即错误
#   powershell -File tools/web.ps1 run            构建并启动，浏览器打开 127.0.0.1:5273
#   powershell -File tools/web.ps1 clean          清理

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $root 'games\hosts\Web'
$webSln = Join-Path $webDir 'NekoClicker.Web.sln'
$url = 'http://127.0.0.1:5273'

$strict = $args -contains '-Strict'
$command = if ($args.Count -gt 0) { [string]($args | Where-Object { $_ -ne '-Strict' } | Select-Object -First 1) } else { 'build' }

# 与 dnet.ps1 相同的环境重定向。刻意不调用 dnet.ps1：它是"动词透传 + -m:1"的薄包装，
# 这里需要自己的动词分支（run 要走 dotnet run，且要带 --urls）。
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $root '.packages'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:MSBUILDDISABLENODEREUSE = '1'

New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES | Out-Null

switch ($command) {
    'build' {
        Write-Host '=== 构建 Web 宿主（含全部内容包）===' -ForegroundColor Cyan
        $buildArgs = @($webSln, '-v', 'q', '--nologo', '-warnaserror')
        if ($strict) { $buildArgs += '--no-incremental' }
        & dotnet build '-m:1' @buildArgs
        exit $LASTEXITCODE
    }
    'run' {
        Write-Host '=== 构建 Web 宿主（含全部内容包）===' -ForegroundColor Cyan
        & dotnet build '-m:1' $webSln -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host ''
        Write-Host "=== 启动：$url ===" -ForegroundColor Cyan
        Write-Host '（Ctrl+C 退出。指令：--package <id> 选择内容包、--urls <地址> 换端口。）' -ForegroundColor DarkGray

        # 后台起一个"等服务起来再开浏览器"的辅助进程，然后把控制台交给宿主本身
        # （这样 Ctrl+C 直接作用在宿主上，不留孤儿进程）。
        Start-Process -FilePath 'powershell' -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-Command',
            "for (`$i = 0; `$i -lt 60; `$i++) { Start-Sleep -Milliseconds 500; try { Invoke-WebRequest -Uri '$url/api/ping' -UseBasicParsing -TimeoutSec 2 | Out-Null; Start-Process '$url'; break } catch { } }"
        ) | Out-Null

        # 用 `dotnet run --no-build` 而不是直接起 bin 里的 exe：静态文件（wwwroot）在
        # 开发期由 Web SDK 通过 bin 里的 *.staticwebassets.endpoints.json 清单解析到
        # **源码目录**，只有 publish 时才复制进输出目录。直接起 exe 会让 /api/* 一切正常
        # 而首页 404 —— 实测踩过，编译期毫无迹象。
        & dotnet run '-m:1' --no-build --project $webDir -- '--urls' $url
        exit $LASTEXITCODE
    }
    'clean' {
        Write-Host '=== 清理 Web 宿主与内容包的构建产物 ===' -ForegroundColor Cyan
        & dotnet clean '-m:1' $webSln -v q --nologo
        exit $LASTEXITCODE
    }
    default {
        Write-Host "未知指令：$command（可用：build / run / clean）" -ForegroundColor Red
        exit 2
    }
}
