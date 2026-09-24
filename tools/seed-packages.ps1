# seed-packages.ps1 — 把 .NET 8 targeting pack 播种进仓库内的 NuGet 缓存。
#
# 为什么需要它：
#   仓库目标框架是 net8.0，但开发机可能只装了更新的 SDK（例如 .NET 10）。
#   这类 SDK 不自带 net8.0 的 targeting pack，restore 会去 nuget.org 下载。
#   而 tools/dnet.ps1 为了在受限沙箱里可写，把 NuGet 缓存重定向到了仓库内的 .packages，
#   于是全局 ~/.nuget/packages 里已经有的包也不会被自动用到 —— 没有网络时
#   dotnet build 会以 NU1301（无法加载服务索引）失败。
#
#   本脚本把全局 NuGet 缓存里已有的 8.0.x targeting pack 复制进 .packages，
#   让构建在没有网络的环境里也能跑通。找不到就跳过（restore 会照常走网络，
#   行为与不加这个脚本时完全一致），所以它是安全的前置步骤。
#
# 用法:
#   pwsh -File tools/seed-packages.ps1                 # 默认播种 8.0.30
#   pwsh -File tools/seed-packages.ps1 -Version 8.0.29 # 指定其它补丁版本
#   pwsh -File tools/seed-packages.ps1 -Strict         # 有缺包时以退出码 2 结束

param(
    [string]$Version = '8.0.30',
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root '.packages'
$strict = [bool]$Strict

# SDK 解析 net8.0 时要求"精确补丁版本"的 ref pack；exe 项目还需要 apphost 包。
$packageIds = @(
    'microsoft.netcore.app.ref',
    'microsoft.aspnetcore.app.ref',
    'microsoft.windowsdesktop.app.ref',
    'microsoft.netcore.app.host.win-x64'
)

$sources = @()
if ($env:USERPROFILE) { $sources += (Join-Path $env:USERPROFILE '.nuget\packages') }
if ($env:NUGET_PACKAGES) { $sources += $env:NUGET_PACKAGES }
$sources = @($sources | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)

New-Item -ItemType Directory -Force -Path $target | Out-Null

$seeded = 0
$missing = @()

foreach ($id in $packageIds) {
    $packageFile = "$id.$version.nupkg"
    $destination = Join-Path $target "$id\$version"

    if (Test-Path (Join-Path $destination $packageFile)) {
        continue  # 已播种，幂等
    }

    $source = $null
    foreach ($candidateRoot in $sources) {
        $candidate = Join-Path $candidateRoot "$id\$version"
        if (Test-Path (Join-Path $candidate $packageFile)) {
            $source = $candidate
            break
        }
    }

    if ($null -eq $source) {
        $missing += "$id $version"
        continue
    }

    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
    $seeded++
    Write-Host "  已播种 $id $version" -ForegroundColor DarkGray
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "未找到以下包（本机全局缓存里没有）：$($missing -join '、')" -ForegroundColor Yellow
    Write-Host "首次构建需要联网让 NuGet 恢复它们；或手动把对应版本放进 .packages\<id>\<version>。" -ForegroundColor Yellow
    if ($strict) { exit 2 }
}
elseif ($seeded -gt 0) {
    Write-Host "NuGet 缓存已就绪：$seeded 个 targeting pack 已复制到 .packages。" -ForegroundColor DarkGray
}

exit 0
