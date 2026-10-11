# play-web.ps1 -- 在本机把这局游戏开起来（Web 宿主），并打印要打开的地址。
#
# 为什么不是直接 start.cmd：本机**没有装 ASP.NET Core 8.x 运行时**
# （`dotnet --list-runtimes` 只有 Microsoft.AspNetCore.App 10.0.x；Microsoft.NETCore.App 8.0.22 倒是有），
# 而 games/hosts/Web 的目标框架是 net8.0 —— 直接跑会得到
# "You must install .NET to run this application / Microsoft.AspNetCore.App 8.0.0"。
# 本脚本做三件事，都是启动这一局所必需的：
#   1) DOTNET_ROLL_FORWARD=Major  —— 让 net8 的应用在 10.x 运行时上跑起来；
#   2) Logging__EventLog__LogLevel__Default=None
#      —— 这条是硬要求，不是化妆：EventLog 这个日志 provider 会去写 Windows 事件日志，
#      在本机被拒（Win32Exception 5 拒绝访问），于是**每写一条日志就把宿主打挂**
#      （实测：一句无害的 StaticFiles 警告 → AggregateException → Unhandled → 进程退出）；
#   3) ASPNETCORE_CONTENTROOT=<games/hosts/Web>
#      —— 开发期 bin 旁边没有 wwwroot（静态文件靠 staticwebassets 解析到源码目录），
#      内容根不指过去就只有 /api/* 通、首页 404（这条仓库文档里记过）。
#
# 用法:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\play-web.ps1
#   ... -Package company       换个内容包（默认 neko；**有「表态」的只有 company / lab / ninelives**）
#   ... -Port 5300             换端口
#   ... -SaveRoot <目录>       换存档根目录（默认 <仓库根>\saves）——**用来开"不影响真人进度"的实验局**
#   ... -Stop                  停掉**这个端口上**的宿主（会先请它自己退，触发一次存档）
#   ... -StopAll               停掉本机所有 neko-clicker-web（收工清场用）
#
# 可以并存：两个不同端口 + 两份存档根就能同时开两局，`-Stop` 只动自己那个端口。
#
# 退出：关掉这个窗口，或者跑 `-Stop` / `-StopAll`。

param(
    [string]$Package = 'neko',
    [int]$Port = 5273,
    [string]$SaveRoot = '',
    [switch]$Stop,
    [switch]$StopAll
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exe = Join-Path $root 'games\hosts\Web\bin\Debug\net8.0\neko-clicker-web.exe'
$url = "http://127.0.0.1:$Port"

# ⚠ 本函数只认**端口**，不认进程名。
# 第一版按进程名杀 `neko-clicker-web`，于是"起第二个宿主（另一个端口 / 另一份存档根）"
# 会把第一个杀掉——实测踩过：给人玩的那个 5273 被离线收益测试那个 5274 顺手带走。
# 端口才是"我要占的那一个"，进程名不是。
function Get-NekoHostPidsOnPort([int]$p) {
    $pattern = "^\s*TCP\s+\S+:$p\s+\S+\s+LISTENING\s+(\d+)\s*$"
    $found = @()
    foreach ($line in (netstat -ano)) {
        $m = [regex]::Match($line, $pattern)
        if ($m.Success) { $found += [int]$m.Groups[1].Value }
    }
    return $found
}

function Stop-NekoHost([int[]]$pids) {
    $procs = @()
    foreach ($procId in $pids) { $procs += Get-Process -Id $procId -ErrorAction SilentlyContinue }
    $procs = @($procs | Where-Object { $_.ProcessName -like 'neko-clicker-web*' })
    if (-not $procs) { Write-Host "端口 $Port 上没有正在跑的宿主。"; return }
    foreach ($p in $procs) {
        # 优雅一点：先请它自己退出（ApplicationStopping 会存一次盘），超时才强杀。
        $null = $p.CloseMainWindow()
    }
    foreach ($p in $procs) { if (-not $p.WaitForExit(3000)) { Stop-Process -Id $p.Id -Force } }
    Write-Host "已停止端口 $Port 上的宿主（PID $($procs.Id -join ', ')）。"
}

if ($StopAll) {
    $all = @(Get-Process neko-clicker-web -ErrorAction SilentlyContinue)
    if ($all.Count -gt 0) { $all | Stop-Process -Force; Write-Host "已停止全部 neko-clicker-web（$($all.Count) 个）。" }
    else { Write-Host '没有正在跑的 neko-clicker-web。' }
    return
}

if ($Stop) { Stop-NekoHost (Get-NekoHostPidsOnPort $Port); return }

if (-not (Test-Path $exe)) {
    Write-Host "还没有构建产物：$exe" -ForegroundColor Yellow
    Write-Host '先跑一次： powershell -NoProfile -ExecutionPolicy Bypass -File tools\dnet.ps1 build games/hosts/Web/NekoClicker.Web.csproj -c Debug'
    exit 1
}

Stop-NekoHost (Get-NekoHostPidsOnPort $Port)
New-Item -ItemType Directory -Force -Path (Join-Path $root 'artifacts') | Out-Null

# -SaveRoot：换成另一份存档根，于是这一局与库里那份真人进度**物理隔离**（实验/复现用）。
$resolvedSaveRoot = if ($SaveRoot) { [System.IO.Path]::GetFullPath($SaveRoot) } else { Join-Path $root 'saves' }
New-Item -ItemType Directory -Force -Path $resolvedSaveRoot | Out-Null

$env:DOTNET_ROLL_FORWARD = 'Major'
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:ASPNETCORE_CONTENTROOT = Join-Path $root 'games\hosts\Web'
# 时钟：引擎按**真实秒**推进，所以这一局从你启动它的那一刻开始算。
Remove-Item Env:NEKO_DEBUG_KEY -ErrorAction SilentlyContinue
Remove-Item Env:NEKO_URL_KEY -ErrorAction SilentlyContinue

# 日志与埋点**按端口分开**：
#   · 5273（默认）沿用 artifacts\host-*.log 与 artifacts\latency.txt —— 真人数据那条线一个字节都不动
#     （`OPEN_WORK` 里"artifacts\latency.txt 只会有真人的数据行"这句话靠的就是这条）；
#   · 其它端口落到 artifacts\play-<端口>\，实验局的会话行不会混进真人样本文件。
if ($Port -eq 5273) {
    $logDir = Join-Path $root 'artifacts'
    $latency = Join-Path $root 'artifacts\latency.txt'
} else {
    $logDir = Join-Path $root "artifacts\play-$Port"
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $latency = Join-Path $logDir 'latency.txt'
}
$out = Join-Path $logDir 'host-out.log'
$err = Join-Path $logDir 'host-err.log'

$proc = Start-Process -FilePath $exe `
    -WorkingDirectory $root -WindowStyle Hidden -PassThru `
    -ArgumentList @('--urls', $url, '--save-root', $resolvedSaveRoot, '--latency-log', $latency) `
    -RedirectStandardOutput $out -RedirectStandardError $err

$alive = $false
foreach ($i in 1..40) {
    Start-Sleep -Milliseconds 500
    try {
        $null = Invoke-WebRequest -Uri "$url/api/ping" -UseBasicParsing -TimeoutSec 3
        $alive = $true; break
    } catch { }
}

if (-not $alive) {
    Write-Host "宿主没起来（PID $($proc.Id)）。日志尾部：" -ForegroundColor Red
    Get-Content $err -Tail 20 -ErrorAction SilentlyContinue
    Get-Content $out -Tail 20 -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ''
Write-Host "  这一局在跑：$url/?package=$Package" -ForegroundColor Green
Write-Host "  全部内容包：$url/api/packs" -ForegroundColor DarkGray
Write-Host "  存档目录：  $resolvedSaveRoot" -ForegroundColor DarkGray
Write-Host "  埋点文件：  $latency" -ForegroundColor DarkGray
Write-Host "  宿主 PID：  $($proc.Id)（停掉：本脚本加 -Port $Port -Stop）" -ForegroundColor DarkGray
Write-Host ''
Write-Host '浏览器打开上面那条地址就能玩。存档每 60 秒落一次。' -ForegroundColor Gray
