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
        Write-Host ''
        Say '=== 启动 Web 宿主（会自动开浏览器）===' -ForegroundColor Cyan
        Say '按 Ctrl+C 退出。'
        Write-Host ''

        if ($Pack) { & "$PSScriptRoot\web.ps1" run "--package" $Pack }
        else { & "$PSScriptRoot\web.ps1" run }
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
