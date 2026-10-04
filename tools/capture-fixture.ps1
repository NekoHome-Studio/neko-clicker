# tools/capture-fixture.ps1 —— 从**真宿主**重抓 tools/fixtures/web-snapshot.json。
#
# 为什么要有这个脚本（engine/docs/STRUCTURE_OPTIMIZATION.md §S2）：
#   web-smoke.mjs 第 14 节（"夹具不是谎话"）与第 23 节（"每个字段都要有人决定过"）
#   把这份夹具当作"线上形状"的**唯一参照**，而它的权威性来自"它真的是线上抓的"。
#   一次抓错（少一个字段、字段名大小写、`mode` 写成字符串）会让下面所有守卫退化成
#   橡皮图章——AGENT_ARCHIVE 记的那次事故就是这样：夹具写 `mode: "buy10"` 而线路送
#   `mode: 0`，**两个套件全绿，页面每帧在 render() 中段抛异常**。
#   而"重抓一次"这件事此前只存在于 .tmp/capture-fixture.ps1（已 gitignore），
#   也就是没有可复现的做法。这个脚本就是那条命令。
#
# 用法（默认就是重抓那份提交在仓库里的夹具）：
#   pwsh -File tools/capture-fixture.ps1
#   pwsh -File tools/capture-fixture.ps1 -Package apocalypse -Port 5399
#   pwsh -File tools/capture-fixture.ps1 -Output .tmp/candidate.json   # 先写到别处比对，不动夹具
#
# 它做什么：
#   1. 起**已经构建好的** Web 宿主（没构建先跑 tools\build.ps1），端口默认 5398 —— 不占用 5273；
#   2. 存档根与埋点文件都在系统临时目录里：**仓库真实的 saves/ 与 artifacts/latency.txt 一个字节都不碰**；
#   3. 先 POST 若干次 click，让通知日志非空（§14 的判据之一是"数组非空"）；
#   4. 拉 /api/snapshot 并**按响应体原始字节**落盘（不经过字符串再编码，避免把中文重新编一遍）；
#   5. 落盘前先拒绝几种"看着像快照但是坏的"结果（`mode` 是字符串、通知为空……）；
#   6. 打印字节数 / sha256 / 覆盖前后的对比 / 几个关键字段，然后停掉它自己起的那个宿主。
#
# 抓完请跑一次 `node tools/web-smoke.mjs`：§14 与 §23 会比形状，而数值漂移（金币、
# 时长、通知时间戳、阶段号）是**预期**的——那份夹具是形状的参照，不是数值的参照。
#
# ⚠️ 这是 .ps1：必须带 UTF-8 BOM（tools/*.ps1 由 Windows PowerShell 5.1 执行，
#    无 BOM 会被按 GBK 解码、整脚本 parse error）。守卫见 engine/tests/ToolingHygieneTests.cs。

param(
    [int]$Port = 5398,
    [string]$Package = 'company',
    [int]$Clicks = 80,
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'games\hosts\Web\bin\Debug\net8.0\neko-clicker-web.exe'
# 开发构建的 bin 里没有 wwwroot，ASP.NET 会退回用**当前目录**当 ContentRoot（Program.cs:58-60）；
# Web 项目目录才是那个有 wwwroot 的地方，所以工作目录是它。
$webDir = Join-Path $root 'games\hosts\Web'
$fixture = if ($Output.Length -gt 0) { [System.IO.Path]::GetFullPath($Output) } else { Join-Path $root 'tools\fixtures\web-snapshot.json' }

if (-not (Test-Path $exe)) { throw "宿主没构建：$exe（先跑 tools\build.ps1）" }

# 运行时的可用性：缺 8.x ASP.NET Core 时显式退化，而不是让宿主抛一段英文
# （与 tools/api-test.ps1:273~281 同一条做法——这台机器上它咬过人）。
$runtimeLines = @(& dotnet --list-runtimes 2>$null)
if (-not ($runtimeLines | Where-Object { $_ -like 'Microsoft.AspNetCore.App 8.*' })) {
    $env:DOTNET_ROLL_FORWARD = 'Major'
    Write-Host '注意：本机没有 Microsoft.AspNetCore.App 8.x（宿主声明的是 net8.0），已设 DOTNET_ROLL_FORWARD=Major 上滚。' -ForegroundColor DarkYellow
}

# 临时目录：这份脚本**不往仓库里写任何临时文件**，更不碰真人数据。
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('neko-fixture-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $PID)
$saveRoot = Join-Path $temp 'saves'
$latency = Join-Path $temp 'latency.txt'
$captured = Join-Path $temp 'captured.json'
New-Item -ItemType Directory -Force -Path $saveRoot | Out-Null

$hadFixture = Test-Path $fixture
$beforeBytes = if ($hadFixture) { (Get-Item $fixture).Length } else { 0 }
$beforeHash = if ($hadFixture) { (Get-FileHash $fixture -Algorithm SHA256).Hash } else { '(原本不存在)' }

Write-Host ''
Write-Host '=== 抓夹具 ===' -ForegroundColor Cyan
Write-Host "包：$Package   端口：$Port   点击：$Clicks"
Write-Host "目标：$fixture"
Write-Host "临时存档根：$saveRoot（仓库真实 saves/ 不会被碰）"
Write-Host ''

$proc = Start-Process -FilePath $exe -PassThru -NoNewWindow -WorkingDirectory $webDir `
    -ArgumentList @('--urls', "http://127.0.0.1:$Port", '--save-root', $saveRoot, '--latency-log', $latency) `
    -RedirectStandardOutput (Join-Path $temp 'host.out.log') `
    -RedirectStandardError (Join-Path $temp 'host.err.log')

$done = $false

try {
    $ready = $false
    for ($i = 0; $i -lt 120; $i++) {
        Start-Sleep -Milliseconds 400
        if ($proc.HasExited) { break }
        try {
            $ping = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/ping" -UseBasicParsing -TimeoutSec 2
            if ($ping.StatusCode -eq 200) { $ready = $true; break }
        }
        catch { }
    }
    if (-not $ready) {
        Write-Host '宿主没起来，stderr：' -ForegroundColor Red
        Get-Content (Join-Path $temp 'host.err.log') -ErrorAction SilentlyContinue
        throw "宿主在端口 $Port 上没起来"
    }
    Write-Host '宿主已就绪'

    for ($i = 0; $i -lt $Clicks; $i++) {
        Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/command?package=$Package" -Method Post `
            -ContentType 'application/json' -Body '{"type":"click"}' -UseBasicParsing | Out-Null
    }

    # -OutFile 写的是**响应体的原始字节**：不经过"解码成字符串再编码"，
    # 中文与转义都保持线上那一个字节序列（夹具是 UTF-8 无 BOM、LF 行尾）。
    Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/snapshot?package=$Package" -OutFile $captured -UseBasicParsing

    $bytes = [System.IO.File]::ReadAllBytes($captured)
    $snapshot = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json

    # 落盘之前的几条否决：宁可这次抓取失败，也不要把一份"看着像快照"的坏输入交给两套守卫。
    if (@($snapshot.buildings).Count -lt 1) { throw '抓到的快照里 buildings 是空的——这不是可以当夹具的东西。' }
    if (@($snapshot.upgrades).Count -lt 1) { throw '抓到的快照里 upgrades 是空的——这不是可以当夹具的东西。' }
    if (@($snapshot.notifications).Count -lt 1) {
        throw '抓到的快照里 notifications 是空的：先 POST 若干次 click 再抓（-Clicks，默认 80）。'
    }
    if ($snapshot.mode -is [string]) {
        throw "抓到的快照里 mode 是字符串 <$($snapshot.mode)>：线上送的是数字。" +
              '夹具写成字符串会让两套套件全绿而页面每帧抛异常——这正是 §14 存在的原因，所以这里直接拒绝。'
    }

    [System.IO.File]::WriteAllBytes($fixture, $bytes)

    $hash = (Get-FileHash $fixture -Algorithm SHA256).Hash
    $changed = (-not $hadFixture) -or ($hash -ne $beforeHash)

    Write-Host ''
    Write-Host '=== 写好了 ===' -ForegroundColor Cyan
    Write-Host ("  文件    {0}" -f $fixture)
    Write-Host ("  字节    {0}" -f $bytes.Length)
    Write-Host ("  sha256  {0}" -f $hash)
    Write-Host ("  抓之前  {0} 字节 / {1}" -f $beforeBytes, $beforeHash)
    Write-Host ("  变了吗  {0}" -f $(if ($changed) { '变了（数值漂移是预期的，形状守卫才管对错）' } else { '没有——这次抓取与树上的字节完全相同' }))
    Write-Host ("  mode    {0}（{1}）" -f $snapshot.mode, $snapshot.modeName)
    Write-Host ("  era     {0}" -f $(if ($null -eq $snapshot.era) { 'null（这个包没有纪元）' } else { "index=$($snapshot.era.index) stageIndex=$($snapshot.era.stageIndex) stageCount=$($snapshot.era.stageCount)" }))
    Write-Host ("  通知    {0} 条" -f @($snapshot.notifications).Count)
    Write-Host ("  建筑    {0} 座 / 升级 {1} 条 / 成就 {2} 条" -f @($snapshot.buildings).Count, @($snapshot.upgrades).Count, @($snapshot.achievements).Count)
    Write-Host ''
    Write-Host '下一步：node tools/web-smoke.mjs（§14 比形状；§23 要求每个字段都有人决定过）'
    $done = $true
}
finally {
    if ($proc -and -not $proc.HasExited) {
        Write-Host "停掉本次抓取起的宿主 PID $($proc.Id)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        $proc.WaitForExit(5000) | Out-Null
    }
    if ($done) {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "临时目录保留下来便于查错：$temp"
    }
}
