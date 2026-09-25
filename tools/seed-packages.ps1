# seed-packages.ps1 — 在无网络环境里补齐构建 net8.0 所需的 targeting pack。
#
# 为什么需要它：
#   仓库目标框架是 net8.0，但开发机可能只装了更新的 SDK（例如 .NET 10）。
#   这类 SDK 不自带 net8.0 的 targeting pack，restore 会去 nuget.org 下载。
#   而 tools/dnet.ps1 为了在受限沙箱里可写，把 NuGet 缓存重定向到了仓库内的 .packages，
#   于是全局缓存里已经有的包也不会被自动用到 —— 没有网络时 dotnet build 会以 NU1301 失败。
#
# 判定逻辑（这是本脚本的关键，早期版本在这里误报过）：
#   "缓存里没有某个硬编码版本" ≠ "构建会失败"。
#   SDK 自己就带着它声明需要的 targeting pack：SDK 目录下的
#   Microsoft.NETCoreSdk.BundledVersions.props 里，net8.0 的 KnownFrameworkReference
#   明确写了 TargetingPackName / TargetingPackVersion。只要 dotnet\packs 下有这个版本，
#   构建就完全不碰 NuGet —— 此时本脚本必须保持沉默。
#
#   所以流程是：
#     ① 从当前生效的 SDK 解析出 net8.0 真正需要的包与版本
#     ② SDK 的 packs 目录里已有该版本 → 已满足，静默返回
#     ③ 否则从全局 NuGet 缓存播种该精确版本
#     ④ 仍然缺 → 这时才警告（警告是真的）
#
# 用法:
#   pwsh -File tools/seed-packages.ps1                 # 自动判定，通常无输出
#   pwsh -File tools/seed-packages.ps1 -Explain        # 打印判定依据（排查用）
#   pwsh -File tools/seed-packages.ps1 -Version 8.0.29 # 强制指定版本（跳过自动解析）
#   pwsh -File tools/seed-packages.ps1 -Strict         # 有缺包时以退出码 2 结束

param(
    [string]$Version = '',
    [string]$TargetFramework = 'net8.0',
    [string]$Rid = '',
    [switch]$Strict,
    [switch]$Explain
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root '.packages'

# 诊断输出：只有 -Explain 时才打印。默认路径必须完全安静 ——
# 之前那版误报的问题，一半就出在"把诊断当汇报打"上。
function Write-Explain {
    param([string]$Message, [string]$Color = 'DarkGray')
    if (-not $Explain) { return }
    Write-Host "  [seed] $Message" -ForegroundColor $Color
}

# ---------------------------------------------------------------- 定位 SDK 与 dotnet 根目录

function Get-DotnetRoot {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    return Split-Path -Parent $command.Source
}

function Get-SdkDirectory {
    param([string]$DotnetRoot)

    $sdkRoot = Join-Path $DotnetRoot 'sdk'
    if (-not (Test-Path $sdkRoot)) { return $null }

    # 优先问 `dotnet --version`：它会尊重 global.json，而"取最高的版本目录"不会。
    $selected = $null
    try { $selected = (& dotnet --version 2>$null | Select-Object -First 1) } catch { $selected = $null }
    if ($selected) {
        $candidate = Join-Path $sdkRoot $selected.Trim()
        if (Test-Path $candidate) { return $candidate }
    }

    $fallback = Get-ChildItem $sdkRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $fallback) { return $null }
    return $fallback.FullName
}

# ---------------------------------------------------------------- 从 SDK 声明里解析所需包

function Get-KnownPackVersions {
    param([string]$SdkDirectory, [string]$Framework)

    $props = Join-Path $SdkDirectory 'Microsoft.NETCoreSdk.BundledVersions.props'
    if (-not (Test-Path $props)) { return $null }

    $text = [System.IO.File]::ReadAllText($props)
    $result = @{ TargetingPack = @(); AppHostPack = @() }

    # 属性值里不含 '>'，所以 [^>]* 足以界定元素；这些元素都是自闭合的。
    $frameworkPattern = '<KnownFrameworkReference\b[^>]*?TargetFramework="' +
                        [regex]::Escape($Framework) + '"[^>]*?/>'
    foreach ($element in [regex]::Matches($text, $frameworkPattern)) {
        $name = [regex]::Match($element.Value, 'TargetingPackName="([^"]+)"')
        $ver = [regex]::Match($element.Value, 'TargetingPackVersion="([^"]+)"')
        if ($name.Success -and $ver.Success) {
            $result.TargetingPack += [pscustomobject]@{ Name = $name.Groups[1].Value; Version = $ver.Groups[1].Value }
        }
    }

    $appHostPattern = '<KnownAppHostPack\b[^>]*?TargetFramework="' +
                      [regex]::Escape($Framework) + '"[^>]*?/>'
    foreach ($element in [regex]::Matches($text, $appHostPattern)) {
        $pattern = [regex]::Match($element.Value, 'AppHostPackNamePattern="([^"]+)"')
        $ver = [regex]::Match($element.Value, 'AppHostPackVersion="([^"]+)"')
        if ($pattern.Success -and $ver.Success) {
            $result.AppHostPack += [pscustomobject]@{ Pattern = $pattern.Groups[1].Value; Version = $ver.Groups[1].Value }
        }
    }

    return $result
}

# ---------------------------------------------------------------- 主机 RID（用于 apphost 包名）

function Get-HostRid {
    if ($Rid) { return $Rid }
    if ($env:OS -ne 'Windows_NT') { return '' }

    switch ($env:PROCESSOR_ARCHITECTURE) {
        'AMD64' { return 'win-x64' }
        'ARM64' { return 'win-arm64' }
        'x86' { return 'win-x86' }
        default { return '' }
    }
}

# ---------------------------------------------------------------- 组装"需要什么"

$dotnetRoot = Get-DotnetRoot
$sdkDirectory = if ($dotnetRoot) { Get-SdkDirectory -DotnetRoot $dotnetRoot } else { $null }
$packsRoot = if ($dotnetRoot) { Join-Path $dotnetRoot 'packs' } else { $null }

# 必需：编译器要用 targeting pack，Exe 项目还要 apphost 包。
# 可选：AspNetCore / WindowsDesktop —— 本仓库没有 Web 或 WPF 项目，缺了也不该报警，
#       所以它们只"能播种就播种"，从不进 missing。
$required = @()
$optional = @()

if ($Version) {
    # 显式指定：保留旧行为，不依赖 SDK 声明。
    $required += [pscustomobject]@{ Id = 'microsoft.netcore.app.ref'; PackName = 'Microsoft.NETCore.App.Ref'; Version = $Version }
    $hostRid = Get-HostRid
    if ($hostRid) {
        $required += [pscustomobject]@{ Id = "microsoft.netcore.app.host.$hostRid"; PackName = "Microsoft.NETCore.App.Host.$hostRid"; Version = $Version }
    }
    $optional += [pscustomobject]@{ Id = 'microsoft.aspnetcore.app.ref'; PackName = 'Microsoft.AspNetCore.App.Ref'; Version = $Version }
    $optional += [pscustomobject]@{ Id = 'microsoft.windowsdesktop.app.ref'; PackName = 'Microsoft.WindowsDesktop.App.Ref'; Version = $Version }
    Write-Explain "显式指定版本 $Version（跳过 SDK 声明解析）"
}
elseif ($sdkDirectory) {
    $known = Get-KnownPackVersions -SdkDirectory $sdkDirectory -Framework $TargetFramework
    if ($null -eq $known -or $known.TargetingPack.Count -eq 0) {
        # 解析失败时退回旧行为：宁可多一个警告，也不要让"本该播种"的场景静默失败。
        Write-Explain "无法从 $sdkDirectory 解析 $TargetFramework 的 targeting pack 声明，退回默认版本 8.0.30" 'Yellow'
        $required += [pscustomobject]@{ Id = 'microsoft.netcore.app.ref'; PackName = 'Microsoft.NETCore.App.Ref'; Version = '8.0.30' }
        $hostRid = Get-HostRid
        if ($hostRid) {
            $required += [pscustomobject]@{ Id = "microsoft.netcore.app.host.$hostRid"; PackName = "Microsoft.NETCore.App.Host.$hostRid"; Version = '8.0.30' }
        }
    }
    else {
        Write-Explain "SDK $([System.IO.Path]::GetFileName($sdkDirectory)) 声明 $TargetFramework 需要："
        foreach ($pack in $known.TargetingPack | Select-Object -Unique Name, Version) {
            Write-Explain "  $($pack.Name) $($pack.Version)"
        }
        foreach ($pack in $known.AppHostPack | Select-Object -Unique Pattern, Version) {
            Write-Explain "  $($pack.Pattern) $($pack.Version)"
        }

        # 不依赖声明顺序：显式挑出 NETCore.App 那一条（AspNetCore / WindowsDesktop 也带 net8.0 条目）。
        $corePack = $known.TargetingPack | Where-Object { $_.Name -eq 'Microsoft.NETCore.App.Ref' } | Select-Object -First 1
        if ($null -eq $corePack) { $corePack = $known.TargetingPack[0] }
        $required += [pscustomobject]@{ Id = 'microsoft.netcore.app.ref'; PackName = $corePack.Name; Version = $corePack.Version }

        $hostRid = Get-HostRid
        foreach ($pack in $known.AppHostPack) {
            if (-not $hostRid) { continue }
            $name = $pack.Pattern -replace '\*\*RID\*\*', $hostRid
            $required += [pscustomobject]@{ Id = $name.ToLowerInvariant(); PackName = $name; Version = $pack.Version }
        }

        foreach ($pack in $known.TargetingPack) {
            if ($pack.Name -eq 'Microsoft.NETCore.App.Ref') { continue }
            $optional += [pscustomobject]@{ Id = $pack.Name.ToLowerInvariant(); PackName = $pack.Name; Version = $pack.Version }
        }
    }
}
else {
    Write-Explain '找不到 dotnet 安装目录，跳过播种。' 'Yellow'
    exit 0
}

# ---------------------------------------------------------------- 判定：SDK 已满足则不播种

function Test-SatisfiedBySdk {
    param([string]$PackName, [string]$PackVersion)
    if (-not $packsRoot) { return $false }
    return Test-Path (Join-Path (Join-Path $packsRoot $PackName) $PackVersion)
}

if (-not $Version) {
    $unmetRequired = @($required | Where-Object { -not (Test-SatisfiedBySdk -PackName $_.PackName -PackVersion $_.Version) })
    if ($unmetRequired.Count -eq 0) {
        # 这是本脚本最常见的路径：SDK 自带所需 pack，构建根本不碰 NuGet。
        # 早期版本在这里会打印"首次构建需要联网"，那是误报 —— 必须静默。
        Write-Explain "$TargetFramework 所需 pack 已由 SDK 自带，无需播种。"
        exit 0
    }
    $required = $unmetRequired
}

# ---------------------------------------------------------------- 播种

$sources = @()
if ($env:USERPROFILE) { $sources += (Join-Path $env:USERPROFILE '.nuget\packages') }
if ($env:NUGET_PACKAGES) { $sources += $env:NUGET_PACKAGES }
$sources = @($sources | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)

New-Item -ItemType Directory -Force -Path $target | Out-Null

function Invoke-Seed {
    param([pscustomobject]$Package)

    $packageFile = "$($Package.Id).$($Package.Version).nupkg"
    $destination = Join-Path $target "$($Package.Id)\$($Package.Version)"

    if (Test-Path (Join-Path $destination $packageFile)) { return 'present' }

    foreach ($candidateRoot in $sources) {
        $candidate = Join-Path $candidateRoot "$($Package.Id)\$($Package.Version)"
        if (-not (Test-Path (Join-Path $candidate $packageFile))) { continue }
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Copy-Item -Path (Join-Path $candidate '*') -Destination $destination -Recurse -Force
        Write-Host "  已播种 $($Package.Id) $($Package.Version)" -ForegroundColor DarkGray
        return 'seeded'
    }

    return 'missing'
}

$seeded = 0
$missing = @()

foreach ($package in $required) {
    switch (Invoke-Seed -Package $package) {
        'seeded' { $seeded++ }
        'missing' { $missing += "$($package.Id) $($package.Version)" }
    }
}

# 可选的包只在缓存里现成有时才播种，缺了既不报警也不影响退出码。
foreach ($package in $optional) {
    if ((Invoke-Seed -Package $package) -eq 'seeded') { $seeded++ }
}

# ---------------------------------------------------------------- 汇报（能沉默就沉默）

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "缺少构建 $TargetFramework 所需的包：$($missing -join '、')" -ForegroundColor Yellow
    Write-Host "SDK 的 packs 目录与全局 NuGet 缓存里都没有；首次构建需要联网让 NuGet 恢复，" -ForegroundColor Yellow
    Write-Host "或手动把对应版本放进 .packages\<包 id>\<版本>。" -ForegroundColor Yellow
    if ($Strict) { exit 2 }
}
elseif ($seeded -gt 0) {
    Write-Host "NuGet 缓存已就绪：$seeded 个包已复制到 .packages。" -ForegroundColor DarkGray
}

exit 0
