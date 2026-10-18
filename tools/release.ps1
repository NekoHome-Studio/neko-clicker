# release.ps1 — 打完包之后，把这一版写进 feed.json（制品清单）。
#
# 用法:
#   powershell -File tools/release.ps1 -Tag v1.12.1
#
# 产物（都在 artifacts/ 下）:
#   feed.json          满足 neko-launcher/contracts/feed.schema.json
#   feed.json.sha256   清单**自身**的哈希（一个文件不可能包含自己的哈希，所以是边上文件）
#
# 为什么是"打包之后"的独立一步，而不是塞进 pack.ps1：
#   §4.1 的硬约束——Compress-Archive 把每个条目的修改时间写进归档（tools/pack.ps1:64），
#   同一个源码目录打两次包字节不同，所以 size/hash **只能**在打完包之后算。
#   顺序被钉死：pack.ps1 → 算 zip 的 sha256 → 生成 feed.json → 上传。
#
# 为什么 tag 与 Directory.Build.props 的 <Version> 必须相等、而不是让 tag 说了算：
#   <Version> 是版本号的唯一事实来源（pack.ps1:34-36 读的也是它）。让 tag 覆盖它就会出现
#   "包里是 1.12.1、清单说是 1.13.0"这种谁也拦不住的漂移。这里只**核对**，不写入。
#
# 为什么一份清单只描述**一个**版本，而不是把 artifacts/ 里的旧版也列进去：
#   契约里 artifact 没有 version / versionCode 字段（只有 kind=patch 用的 baseVersionCode），
#   所以列出来的旧版整包**没法被标注**，客户端分不清哪个是哪个。
#   ⇒ 每个 release 出自己那一份 feed.json；旧版要下就下它自己那个 tag 的资产。
#
# ponytail: channel 只会写 "release"。快照通道要先解决"同号 snapshot 的 versionCode 必须
# 大于 release"（计数器跨通道共用，P5），那需要一条 tag 形状约定加一个偏移量，
# 不是顺手能定的。等真有快照发布再加，加的时候只动这个文件。

param(
    [Parameter(Mandatory = $true)][string]$Tag,
    # 这些资产挂在哪个仓库的 release 下（同时也是清单自己的宿主）。
    [string]$Repo = 'NekoHome-Studio/neko-clicker',
    # 组件名（给人看的那个）。
    [string]$ComponentName = 'NekoClicker',
    # 默认端口的唯一出处在这里：清单是**发布侧**的声明，启动器只当数据读。
    [int]$DefaultPort = 5273
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# versionCode：单调整数，客户端**只比大小**（P5）。换算照 docs/PROTOTYPE_PLAN.md §10 第 1 条
# ——v1.12.0 -> 11200，所以 patch 位就是它自己（1.12.1 -> 11201）。
function ConvertTo-VersionCode([string]$v) {
    $m = [regex]::Match($v, '^(\d+)\.(\d+)\.(\d+)')
    if (-not $m.Success) { throw "版本号不是 major.minor.patch，无法换算 versionCode：$v" }
    return [int]$m.Groups[1].Value * 10000 + [int]$m.Groups[2].Value * 100 + [int]$m.Groups[3].Value
}

$tagVersion = $Tag -replace '^v', ''
if ([string]::IsNullOrWhiteSpace($tagVersion)) { throw "tag 是空的：'$Tag'" }

# 版本号唯一事实来源：Directory.Build.props 的 <Version>。
$propsPath = Join-Path $root 'Directory.Build.props'
$version = [regex]::Match([System.IO.File]::ReadAllText($propsPath), '<Version>([^<]+)</Version>').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) { throw "读不到版本号（$propsPath 里没有 <Version>）。" }

if ($version -ne $tagVersion) {
    throw "tag（$tagVersion）与 Directory.Build.props 的 <Version>（$version）不一致；版本号只有一处事实来源，先改 <Version> 并提交再打 tag。"
}

$versionCode = ConvertTo-VersionCode $version
$zipName = "neko-clicker-$version-win-x64.zip"
$zipPath = Join-Path $artifacts $zipName
if (-not (Test-Path $zipPath)) { throw "找不到 $zipPath；先跑 tools/pack.ps1。" }

# 这两项说的是**压缩包文件本身**，不是解包后的内容（§4.1 第 ④ 条）。
$sha = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $zipPath).Length

# 绝对 URL，与 feed.sample.json 的形状一致（schema 要求 urls 的每一项是完整 uri）。
# 镜像就是往这个数组后面**追加**同一份字节流的另一个基址，不是不同构建（§4.1 第 ③ 条）。
$urls = @("https://github.com/$Repo/releases/download/$Tag/$zipName")

$feed = [ordered]@{
    schema      = 1
    channel     = 'release'
    generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    sourceName  = 'github'
    components  = @(
        [ordered]@{
            id           = 'neko-clicker'
            name         = $ComponentName
            version      = $version
            versionCode  = $versionCode
            requirements = [ordered]@{
                runtimes = @([ordered]@{ id = 'dotnet-aspnet'; min = '8.0' })
                ports    = @($DefaultPort)
            }
            artifacts    = @(
                [ordered]@{
                    os     = 'win-x64'
                    kind   = 'full'
                    urls   = $urls
                    size   = $size
                    hash   = "sha256:$sha"
                    unpack = 'zip'
                    layout = [ordered]@{
                        web = [ordered]@{ exe = 'web/neko-clicker-web.exe'; defaultArgs = @() }
                        cli = [ordered]@{ exe = 'demo/neko-clicker.exe'; defaultArgs = @() }
                    }
                }
            )
        }
    )
}

$feedPath = Join-Path $artifacts 'feed.json'
[System.IO.File]::WriteAllText($feedPath, ($feed | ConvertTo-Json -Depth 10), $utf8NoBom)

# 边上文件：客户端顺序是**先验哈希、通过之后才解析 JSON**（contracts/feed.schema.json 头部）。
# 内容是纯十六进制，不换行——骨架侧的 Sidecar.Verify 按空白切开取第一段，两种写法都认。
$shaPath = "$feedPath.sha256"
[System.IO.File]::WriteAllText($shaPath, (Get-FileHash -Path $feedPath -Algorithm SHA256).Hash.ToLowerInvariant(), $utf8NoBom)

# ---------------------------------------------------------------- 自检
# 为什么必须自检、而不是"写完就完事"：§4.1 的第二条硬约束——layout 里的路径是**按解包后的
# 根**算的，打包结构一变（pack.ps1 加了/挪了一个宿主目录）layout 就必须同步改，否则下载器
# 解包后找不到 exe，那个失败会被报成"文件损坏"而不是"清单与产物不符"（P2 要求报成后者）。
# 所以这里真的把 **zip 打开看一眼**，而不是相信上面那两行硬编码的路径。
# 判据与 pack.ps1:74 的产物自检同一个理由：默认开着，且不碰真实存档。
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try { $names = @($archive.Entries | ForEach-Object { $_.FullName }) } finally { $archive.Dispose() }

$problems = @()
foreach ($declared in @('web/neko-clicker-web.exe', 'demo/neko-clicker.exe')) {
    if ($names -notcontains $declared) { $problems += "清单声明的 layout 在 zip 里不存在：$declared" }
}
if ((Get-Item $zipPath).Length -ne $size) { $problems += "zip 的字节数在算完哈希之后变了（$size -> $((Get-Item $zipPath).Length)）" }
if ([System.IO.File]::ReadAllText($shaPath).Trim() -ne (Get-FileHash -Path $feedPath -Algorithm SHA256).Hash.ToLowerInvariant()) {
    $problems += "feed.json.sha256 与 feed.json 的实际哈希不符（边上文件写错了）"
}
if ($problems.Count -gt 0) { throw "清单自检失败：`n  - " + ($problems -join "`n  - ") }

Write-Host ''
Write-Host "已写出 $feedPath" -ForegroundColor Green
Write-Host ("  {0}  versionCode {1}  {2} 字节  sha256:{3}…" -f $version, $versionCode, $size, $sha.Substring(0, 16))
Write-Host ("  自检通过：{0} 个条目，layout 声明的两个 exe 都在包里。" -f $names.Count) -ForegroundColor Green
Write-Host ''
Write-Host '清单地址（稳定，指向本仓库最新一次 release 的资产）:' -ForegroundColor Cyan
Write-Host "  https://github.com/$Repo/releases/latest/download/feed.json"
Write-Host 'neko-launcher/contracts/sources.json 里的 github 源要指向这个地址。' -ForegroundColor DarkGray
