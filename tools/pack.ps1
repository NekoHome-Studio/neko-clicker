# pack.ps1 — 打出可分发的 zip。
#
# 用法:
#   powershell -File tools/pack.ps1
#
# 产物:
#   artifacts/neko-clicker-<版本>-win-x64.zip
#     说明.md       ← 拷贝自 engine/docs/PACK_README.md
#     demo/         ← 终端宿主（框架依赖发布）
#     web/          ← Web 宿主（框架依赖发布，含 wwwroot）
#
# 为什么是"框架依赖"而不是自包含单文件：自包含需要 runtime pack
# （Microsoft.NETCore.App.Runtime.win-x64），那是 NuGet 包；这个仓库的前提是
# **可能没有网络**，实测无网时 --self-contained 会以 NU1301 失败。
# 所以这里只出框架依赖包，并在说明书里写清"需要 .NET 8 运行时"。
# 要自包含包就在有网的机器上跑一次 dotnet publish --self-contained（命令写在说明书里）。
#
# 版本号只有一处事实来源：Directory.Build.props 的 <Version>。这里读它，不另写一份。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

$propsPath = Join-Path $root 'Directory.Build.props'
$version = [regex]::Match([System.IO.File]::ReadAllText($propsPath), '<Version>([^<]+)</Version>').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) { throw "读不到版本号（$propsPath 里没有 <Version>）。" }

$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts "neko-clicker-$version"
$zip = Join-Path $artifacts "neko-clicker-$version-win-x64.zip"

Write-Host "=== 打包 NekoClicker $version ===" -ForegroundColor Cyan

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'demo'), (Join-Path $stage 'web') | Out-Null

function Publish-Host([string]$project, [string]$target, [string]$label) {
    Write-Host "  发布 $label ..." -ForegroundColor DarkGray
    & "$PSScriptRoot\dnet.ps1" publish (Join-Path $root $project) -c Release -o $target -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "$label 发布失败（退出码 $LASTEXITCODE）。" }
}

Publish-Host 'games\hosts\Demo.Cli\NekoClicker.Demo.Cli.csproj' (Join-Path $stage 'demo') '终端宿主'
Publish-Host 'games\hosts\Web\NekoClicker.Web.csproj' (Join-Path $stage 'web') 'Web 宿主'

# 说明书直接拷贝仓库里的那份，而不是在脚本里写字符串：
# 中文经过"脚本字面量 -> PowerShell 解析 -> 文件"这条路出过一次事故（把 ROADMAP 写坏了 298 行）。
# 拷贝一个已有的 md 不经过那些层。
$readme = Join-Path $root 'engine\docs\PACK_README.md'
if (-not (Test-Path $readme)) { throw "找不到说明书：$readme" }
Copy-Item $readme (Join-Path $stage '说明.md') -Force

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

$size = (Get-Item $zip).Length / 1MB
$files = (Get-ChildItem $stage -Recurse -File).Count
Write-Host ''
Write-Host "已打出 $zip" -ForegroundColor Green
Write-Host ("  {0} 个文件，压缩后 {1:N1} MB（未压缩 {2:N1} MB）" -f `
    $files, $size, ((Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum / 1MB))
Write-Host '  前提：目标机器需要 .NET 8 运行时（说明书里写了）。' -ForegroundColor DarkGray
