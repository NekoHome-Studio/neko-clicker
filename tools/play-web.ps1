# play-web.ps1 -- 兼容别名：现在它就是 `tools\start.ps1`（`start.cmd`）的 web 那条路。
#
# 它原本是 1.12.1 里一个独立的启动脚本（直接起 bin 里的 exe、自己挑日志落点、-Stop 按端口停）。
# 那套能力现在**全部**在 tools\start.ps1 里，而两份实现已经开始分叉（一份复用已在跑的宿主、
# 一份先杀掉再重起；一份开浏览器、一份只打印地址；日志落点也不同）——"起网页"只该有一种说法，
# 所以这里只留一层转发：**行为与 `start.cmd web` 逐字一致**。
# 以后直接用 `start.cmd`（或 `start.cmd web`），不要从这里开始新工作。
#
# 用法（旧写法照旧可用）:
#   tools\play-web.ps1 -Package company        网页 + 公司包（= start.cmd web company）
#   tools\play-web.ps1 -Port 5300 -SaveRoot <目录>
#   tools\play-web.ps1 -Stop -Port 5300        停掉这个端口上的宿主（= start.cmd stop -Port 5300）
#   tools\play-web.ps1 -NoBrowser / -NoBuild   透传给统一入口
#   tools\play-web.ps1 -StopAll                **已移除**：会明确报错（见下）
#
# 与 1.12.1 那版的差别（都是统一后的行为，逐条写在 CHANGELOG 的 [未发布] 里）:
#   · 默认**会构建**（旧版要求先手工构建）。只改了 wwwroot 静态文件时用 -NoBuild。
#   · 端口上已经有**我们的**宿主时**复用它**，不再先杀掉再重起（重起会丢最多 60 秒的进度）。
#   · 默认**会开浏览器**（旧版只打印地址）。不想开就 -NoBrowser。
#   · 日志/埋点按端口分：5273 走 artifacts\host-*.log 与 artifacts\latency.txt，
#     其它端口走 artifacts\play-<端口>\（这一条是 1.12.1 先做的，保留）。
#   · -StopAll 已移除：按进程名清场会顺手带走 5273 上真人正在玩的那个宿主。

param(
    [string]$Package = 'neko',
    [int]$Port = 0,
    [string]$SaveRoot = '',
    [switch]$Stop,
    [switch]$StopAll,
    [switch]$NoBrowser,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host '（play-web.ps1 是兼容别名：实际执行的是 start.cmd web；以后请直接用 start.cmd。）' -ForegroundColor DarkGray

if ($StopAll) {
    Write-Host ''
    Write-Host '-StopAll 已移除：它按进程名杀掉本机所有 neko-clicker-web，其中包括 5273 上真人正在玩的那个。' -ForegroundColor Red
    Write-Host '按端口停：play-web.ps1 -Stop -Port <端口>（或 start.cmd stop -Port <端口>）。' -ForegroundColor Red
    Write-Host ''
    exit 2
}

# 用**哈希表** splat 而不是数组 splat：数组 splat 只按位置传值，'-NoBrowser' 那种会被当成
# 一个位置参数（实测报 `A positional parameter cannot be found that accepts argument '-Port'`）。
$mode = 'web'
if ($Stop) { $mode = 'stop' }
$forward = @{ Mode = $mode }
if (-not $Stop) { $forward['Pack'] = $Package }
if ($Port -gt 0) { $forward['Port'] = $Port }
if ($SaveRoot) { $forward['SaveRoot'] = $SaveRoot }
if ($NoBrowser) { $forward['NoBrowser'] = $true }
if ($NoBuild) { $forward['NoBuild'] = $true }

& (Join-Path $PSScriptRoot 'start.ps1') @forward
exit $LASTEXITCODE
