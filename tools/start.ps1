# start.ps1 -- 一体化启动。构建 + 起宿主，把已知的坑都堵掉。
#
# 为什么要有它（三个坑都是实测踩过的）：
#   1) `tools/web.ps1` 不带动词只做 build 就退出 —— 默认动词就是 build，
#      看起来像"启动不了"。这里把动词写死，不给你踩的机会。
#   2) 上次没退干净的宿主进程会占着 bin 里的 dll，构建报一堆
#      MSB3021 "being used by another process"。这里先清掉**本仓库自己的**残留进程。
#   3) 执行策略会拦 .ps1。所以入口是 start.cmd（它用 -ExecutionPolicy Bypass 调用本脚本）；
#      你也可以直接用 `powershell -ExecutionPolicy Bypass -File tools\start.ps1`。
#
# 用法:
#   start.cmd                终端全屏界面，默认内容包
#   start.cmd web            浏览器那一侧（会自动开浏览器）
#   start.cmd play lab       终端 + 指定内容包
#   start.cmd list           列出全部内容包

param(
    [Parameter(Position = 0)][string]$Mode = 'play',
    [Parameter(Position = 1)][string]$Pack = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Say([string]$m) { Write-Host $m }
function Fail([string]$m) { Write-Host ''; Write-Host $m -ForegroundColor Red; Write-Host ''; Read-Host '按回车关闭'; exit 1 }

Write-Host ''
Write-Host '=== NekoClicker 启动器 ===' -ForegroundColor Cyan
Say "仓库：$root"

# ---- 0. 装没装 .NET ----------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail '找不到 dotnet。需要 .NET 8 SDK：https://dotnet.microsoft.com/download/dotnet/8.0'
}
Say ("dotnet {0}" -f (& dotnet --version))

# ---- 1. 清掉会锁住 dll 的残留宿主（只清本仓库里的，不动你别的程序）----------
$stale = @(Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })

if ($stale.Count -gt 0) {
    Say ("发现 {0} 个本仓库的残留进程，先清掉（否则构建会因文件占用失败）：" -f $stale.Count)
    foreach ($p in $stale) {
        Say ("  - {0} (PID {1})" -f $p.ProcessName, $p.Id)
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { }
    }
    Start-Sleep -Milliseconds 500
}

# ---- 2. 构建 ----------------------------------------------------------------
Write-Host ''
Say '=== 构建 ===' -ForegroundColor Cyan
& "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { Fail '构建失败（上面红色的 error 行就是原因）。' }

# ---- 3. 分派 ----------------------------------------------------------------
switch ($Mode.ToLowerInvariant()) {
    'web' {
        # 端口与包名都由这里决定，然后**拼进 URL** —— Web 端的包选择走查询串
        # `?package=<id>`，不是命令行参数。原先把 --package 传给 web.ps1 是个静默失败：
        # 那个脚本不解析它，于是包选择被悄悄丢掉，玩家只会看到默认包。
        $port = if ($env:NEKO_PORT) { $env:NEKO_PORT } else { '5273' }
        $base = "http://127.0.0.1:$port/"
        $url = if ($Pack) { $base + "?package=$Pack" } else { $base }

        Write-Host ''
        Write-Host '=== 启动 Web 宿主 ===' -ForegroundColor Cyan
        Write-Host "浏览器地址：$url" -ForegroundColor Yellow
        Write-Host '宿主起来后会自动打开它；没自动开就把上面这个地址粘到浏览器。'
        Write-Host '按 Ctrl+C 停止服务。'
        Write-Host ''

        & "$PSScriptRoot\dnet.ps1" build "$root\games\hosts\Web\NekoClicker.Web.sln" -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { Fail 'Web 宿主构建失败（上面红色的 error 行就是原因）。' }

        # 自己等端口通了再开浏览器，而不是交给 web.ps1：它写死了不带参数的首页地址，
        # 那样 ?package= 就带不进去，而且会与本脚本各开一次浏览器。
        Start-Process -FilePath 'powershell' -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-Command',
            "for (`$i = 0; `$i -lt 90; `$i++) { Start-Sleep -Milliseconds 500; try { Invoke-WebRequest -Uri 'http://127.0.0.1:$port/api/ping' -UseBasicParsing -TimeoutSec 2 | Out-Null; Start-Process '$url'; break } catch { } }"
        ) | Out-Null

        # 用 dotnet run（而不是直接起 exe）：开发期静态文件靠 bin 里的
        # *.staticwebassets.endpoints.json 清单解析到源码目录，直接起 exe 会让
        # 首页 404 而 /api/* 正常。web.ps1 的注释里记了这个坑，这里照它的做法。
        & dotnet run '-m:1' --no-build --project "$root\games\hosts\Web" -- '--urls' "http://127.0.0.1:$port"
    }

    'list' {
        Write-Host ''
        Say '内容包：'
        foreach ($id in @('neko', 'cafe', 'ninelines', 'lab', 'company', 'apocalypse', 'library', 'god', 'civ', 'cyber', 'dream')) {
            Say ("  {0,-12}" -f $id)
        }
        Say ''
        Say '用法：start.cmd play lab     （终端 + 实验室包）'
        Say '      start.cmd web company （浏览器 + 公司包）'
        Write-Host ''
        Read-Host '按回车关闭'
    }

    default {
        Write-Host ''
        Say '=== 启动终端界面 ===' -ForegroundColor Cyan
        Say '操作：Tab 切面板｜数字键买建筑｜Enter 购买｜A 舍命/转生｜? 帮助｜Q 退出'
        Write-Host ''

        $dll = Join-Path $root 'games\hosts\Demo.Cli\bin\Debug\net8.0\neko-clicker.dll'
        $demoArgs = @()
        if ($Pack) { $demoArgs += @('--package', $Pack) }

        # 用 dotnet exec 直接跑已构建好的 dll：不经过 `dotnet run`，少一层进程，
        # 而且 Ctrl+C 直接作用在游戏上（run 会多一个中间进程，退出时容易留孤儿）。
        & dotnet exec $dll @demoArgs
    }
}
