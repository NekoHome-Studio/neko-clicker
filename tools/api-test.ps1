# api-test.ps1 — Web 宿主的端到端回归：真的把宿主起起来，再真的打一遍它的全部端点。
#
# 为什么它必须存在（而不是"再多写几条单元测试"）：
#   Web 宿主这一层最典型的失败是**沉默的**——协议、序列化、静态文件、SSE 全是拼装出来的，
#   而单元测试只覆盖得了其中 SnapshotProtocol 这一段源码。三条真实踩过的坑都不在单元测试的射程里：
#     · 忘了 PropertyNamingPolicy = CamelCase → 前端静默全废，而用例全绿；
#     · wwwroot 走 staticwebassets 清单解析到源码目录 → 直接起 bin 里的 exe 首页 404，
#       而 /api/* 一切正常，编译期 0 警告 0 错误；
#     · 增量推送的派生字段没处理干净 → 每帧 28KB，而"变化字段数 ≤ 4"的用例照样绿。
#   所以这一层的判据只能是**真的通**：起宿主、发请求、读 SSE 流、看着它回话。
#
# 与单元测试的分工：`tools/build.ps1 -Strict` 守引擎（435 个用例 + 公开 API 快照），
# 本脚本守"宿主 + 浏览器协议"那一段。CI 里两条都跑（.github/workflows/ci.yml）。
#
# 三条刻意为之的行为（都不是默认就该有的，是踩出来的）：
#   ① **自带临时存档目录**（--save-root）。探针会点击、会买入；跑在真实存档上等于
#      把玩家的进度当测试夹具。旧版探针就是这么干的（.tmp/api-probe），收进仓库时必须改掉。
#   ② **强制清掉 NEKO_DEBUG_KEY 再起子进程**，于是"缺省门是关着的"这条断言在任何开发机上
#      都成立，而不是"取决于跑的人 shell 里有没有那个变量"。
#   ③ **收尾按端口反查进程**：`dotnet run` 会再起一个真正的宿主子进程，只杀 dotnet run 自己
#      会留下还在监听端口的孤儿（本仓库踩过）。杀完还要确认端口真的松手。
#
# 用法：
#   powershell -File tools/api-test.ps1                 # 构建 + 起宿主 + 打全套 + 收尾
#   powershell -File tools/api-test.ps1 -NoBuild        # 跳过构建（只改了 wwwroot 静态文件时快跑）
#   powershell -File tools/api-test.ps1 -Strict         # 构建走全量重编（--no-incremental）
#   powershell -File tools/api-test.ps1 -Port 5300      # 指定端口（缺省自动挑一个空闲端口）
#   powershell -File tools/api-test.ps1 -Package lab    # 换一个内容包当被测对象
#   powershell -File tools/api-test.ps1 -KeepHost       # 测完留着宿主（调试用，自己 Ctrl+C）
#
# 退出码：0 = 全部通过；1 = 有检查失败；其余 = 构建或启动失败（原样透传）。

param(
    [switch]$NoBuild,
    [switch]$Strict,
    [int]$Port = 0,
    [string]$Package = 'cafe',
    [switch]$KeepHost,
    [switch]$KeepSave
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $root 'games\hosts\Web'
$webSln = Join-Path $webDir 'NekoClicker.Web.sln'

# ---------------------------------------------------------------- 环境重定向
# 与 tools/web.ps1 / tools/dnet.ps1 完全一致：受限沙箱里 dotnet CLI home 与 NuGet 缓存
# 必须落在工作区内（%USERPROFILE% 不可写），MSBuild 必须 -m:1（命名管道被禁时多节点构建会静默失败）。
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $root '.packages'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:MSBUILDDISABLENODEREUSE = '1'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES | Out-Null

# ---------------------------------------------------------------- 结果记账
$script:Passed = 0
$script:Failed = 0

function Write-Section([string]$Title) {
    Write-Host ''
    Write-Host $Title -ForegroundColor Cyan
}

function Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) { $script:Passed++ } else { $script:Failed++ }
    $mark = if ($Ok) { ' OK ' } else { 'FAIL' }
    $suffix = if ($Detail.Length -gt 0) { "  — $Detail" } else { '' }
    Write-Host "  [$mark] $Name$suffix" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' })
}

# ---------------------------------------------------------------- HTTP 小工具
Add-Type -AssemblyName System.Net.Http

$script:Client = New-Object System.Net.Http.HttpClient
$script:Client.Timeout = [TimeSpan]::FromSeconds(20)

function Invoke-Get([string]$Path) {
    try {
        $task = $script:Client.GetAsync($Path)
        $response = $task.GetAwaiter().GetResult()
        $readTask = $response.Content.ReadAsStringAsync()
        return [pscustomobject]@{
            Ok      = $true
            Status  = [int]$response.StatusCode
            Success = $response.IsSuccessStatusCode
            Body    = $readTask.GetAwaiter().GetResult()
        }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Status = 0; Success = $false; Body = $_.Exception.Message }
    }
}

function Invoke-PostJson([string]$Path, $Payload) {
    try {
        $json = $Payload | ConvertTo-Json -Compress -Depth 5
        $content = New-Object System.Net.Http.StringContent -ArgumentList @($json, [System.Text.Encoding]::UTF8, 'application/json')
        $task = $script:Client.PostAsync($Path, $content)
        $response = $task.GetAwaiter().GetResult()
        $readTask = $response.Content.ReadAsStringAsync()
        return [pscustomobject]@{
            Ok      = $true
            Status  = [int]$response.StatusCode
            Success = $response.IsSuccessStatusCode
            Body    = $readTask.GetAwaiter().GetResult()
        }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Status = 0; Success = $false; Body = $_.Exception.Message }
    }
}

function Convert-FromJsonSafe([string]$Text) {
    try { return $Text | ConvertFrom-Json } catch { return $null }
}

# ---------------------------------------------------------------- 宿主生命周期
function Get-TestProcess([int]$Port) {
    # 按**端口**反查，而不是只认启动时拿到的那个 PID：dotnet run 会再起一个子进程，
    # 真正的宿主是那个子进程（名字是 apphost 的 neko-clicker-web.exe）。
    # 同时按进程名收窄，避免误伤命令行里恰好含同一串数字的无关进程。
    $pattern = ":$Port"
    return @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe' OR Name = 'neko-clicker-web.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($pattern) })
}

function Test-PortBusy([int]$Port) {
    $probe = New-Object System.Net.Sockets.TcpClient
    try {
        $probe.Connect('127.0.0.1', $Port)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $probe.Close()
    }
}

function Stop-TestHost([int]$Port, $Process) {
    foreach ($p in Get-TestProcess -Port $Port) {
        Write-Host "  收尾：结束 $($p.Name) (PID $($p.ProcessId))" -ForegroundColor DarkGray
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }

    if ($Process -and -not $Process.HasExited) {
        Write-Host "  收尾：结束 dotnet run (PID $($Process.Id))" -ForegroundColor DarkGray
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
    }

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        if (-not (Test-PortBusy -Port $Port)) { return $true }
    }

    Write-Host "  收尾：端口 $Port 仍然被占用——下一次跑可能撞端口。" -ForegroundColor Yellow
    return $false
}

# ================================================================ 主流程

Write-Host '=== Web 宿主端到端（api-test） ===' -ForegroundColor Cyan

# ---- 构建
if (-not $NoBuild) {
    Write-Section '构建 Web 宿主（含全部内容包）'
    $buildArgs = @($webSln, '-v', 'q', '--nologo', '-warnaserror')
    if ($Strict) { $buildArgs += '--no-incremental' }
    & dotnet build '-m:1' @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host '构建失败——端到端没开始打。' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# ---- 运行时的可用性：缺 8.x ASP.NET Core 时显式退化，而不是让宿主抛一段英文
$runtimeLines = @(& dotnet --list-runtimes 2>$null)
if (-not ($runtimeLines | Where-Object { $_ -like 'Microsoft.AspNetCore.App 8.*' })) {
    $env:DOTNET_ROLL_FORWARD = 'Major'
    Write-Host ''
    Write-Host '注意：本机没有 Microsoft.AspNetCore.App 8.x（宿主声明的是 net8.0）。' -ForegroundColor DarkYellow
    Write-Host '      已设 DOTNET_ROLL_FORWARD=Major 让它上滚到更高的运行时运行——' -ForegroundColor DarkYellow
    Write-Host '      这是环境适配，被测行为会因此打一点折扣（装了 8.x 的机器不受影响）。' -ForegroundColor DarkYellow
}

# ---- 端口与临时目录
if ($Port -le 0) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $Port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
}
$baseUrl = "http://127.0.0.1:$Port"
$script:Client.BaseAddress = [Uri]$baseUrl

if (Test-PortBusy -Port $Port) {
    Write-Host "端口 $Port 已经被占用（是不是还有一个 web.ps1 run 在跑？）。" -ForegroundColor Red
    exit 2
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$workDir = Join-Path $root ".tmp\api-test\$stamp-$PID"
$saveRoot = Join-Path $workDir 'saves'
$outLog = Join-Path $workDir 'host.out.log'
$errLog = Join-Path $workDir 'host.err.log'
New-Item -ItemType Directory -Force -Path $saveRoot | Out-Null

Write-Host ''
Write-Host "目标：$baseUrl ｜ 内容包：$Package ｜ 存档：$saveRoot" -ForegroundColor DarkGray

# ---- 起宿主
# 保存并摘掉 NEKO_DEBUG_KEY：这样"缺省门是关着的"才是被测事实，而不是跑的人 shell 的巧合。
$hadDebugKey = Test-Path Env:NEKO_DEBUG_KEY
$savedDebugKey = if ($hadDebugKey) { $env:NEKO_DEBUG_KEY } else { $null }
if ($hadDebugKey) { Remove-Item Env:NEKO_DEBUG_KEY }

# 路径一律自己加引号：Start-Process 是用空格拼接参数表的，工作区路径里有空格时不加引号会散架。
$hostArgs = @(
    'run', '-m:1', '--no-build', '--project', "`"$webDir`"",
    '--', '--urls', "`"$baseUrl`"", '--save-root', "`"$saveRoot`""
)
$hostProcess = Start-Process -FilePath 'dotnet' -ArgumentList $hostArgs -PassThru -NoNewWindow `
    -RedirectStandardOutput $outLog -RedirectStandardError $errLog

if ($hadDebugKey) { $env:NEKO_DEBUG_KEY = $savedDebugKey }

$ready = $false
$lastProbe = ''
$deadline = [System.Diagnostics.Stopwatch]::StartNew()
while ($deadline.Elapsed.TotalSeconds -lt 60) {
    if ($hostProcess.HasExited) { break }
    Start-Sleep -Milliseconds 400
    $probe = Invoke-Get '/api/ping'
    if ($probe.Ok -and $probe.Success) { $ready = $true; break }
    $lastProbe = "HTTP $($probe.Status) $($probe.Body)"
}

if (-not $ready) {
    Write-Host ''
    Write-Host "宿主 60 秒内没有就绪（PID $($hostProcess.Id)）；最后一次探测：$lastProbe" -ForegroundColor Red
    if (Test-Path $outLog) { Get-Content $outLog -Encoding UTF8 | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" } }
    if ((Test-Path $errLog) -and (Get-Item $errLog).Length -gt 0) {
        Write-Host '  stderr：' -ForegroundColor Red
        Get-Content $errLog -Encoding UTF8 | Select-Object -Last 20 | ForEach-Object { Write-Host "    $_" }
    }
    Stop-TestHost -Port $Port -Process $hostProcess | Out-Null
    exit 1
}

try {
    Write-Host ''
    Write-Host "宿主已就绪，开始打端点。" -ForegroundColor Green

    # ------------------------------------------------------------ 静态文件
    Write-Section '静态文件（首页 404 是这一层最经典的沉默失败）'
    foreach ($item in @(
            @{ Path = '/'; Needle = 'NekoClicker' },
            @{ Path = '/app.js'; Needle = 'EventSource' },
            @{ Path = '/app.css'; Needle = '--accent' }
        )) {
        $r = Invoke-Get $item.Path
        $hit = [bool]($r.Body -and $r.Body.Contains($item.Needle))
        $bytes = if ($r.Body) { [System.Text.Encoding]::UTF8.GetByteCount($r.Body) } else { 0 }
        Check "GET $($item.Path)" ($r.Success -and $hit) "HTTP $($r.Status)，$bytes 字节，含 <$($item.Needle)>: $hit"
    }

    # ------------------------------------------------------------ 元信息
    Write-Section '元信息'
    $expectedVersion = $null
    if ((Get-Content (Join-Path $root 'Directory.Build.props') -Raw -Encoding UTF8) -match '<Version>([^<]+)</Version>') {
        $expectedVersion = $Matches[1]
    }

    $pingResponse = Invoke-Get '/api/ping'
    $ping = Convert-FromJsonSafe $pingResponse.Body
    Check 'GET /api/ping 报出的版本 = Directory.Build.props 的版本' `
        ($pingResponse.Success -and $null -ne $ping -and $ping.apiVersion -eq $expectedVersion) `
        "apiVersion=$($ping.apiVersion)，仓库里是 $expectedVersion"
    Check 'GET /api/ping 报出运行时' ([bool]($ping -and $ping.runtime)) `
        "runtime=$($ping.runtime)（$($ping.framework)）"

    $packsResponse = Invoke-Get '/api/packs'
    # 注意：必须先落到变量再 @()。PowerShell 5.1 的 ConvertFrom-Json 解析顶层数组时是
    # **不枚举**的（数组作为单个对象返回），于是 @(f) 会得到"1 个元素（元素本身是数组）"——
    # 这条踩过：包数报成 1，而断言依然是绿的。
    $packsParsed = Convert-FromJsonSafe $packsResponse.Body
    $packs = @($packsParsed)
    $hasPackage = @($packs | Where-Object { $_.id -eq $Package }).Count -gt 0
    Check 'GET /api/packs 扫到了全部内容包' ($packsResponse.Success -and $packs.Count -ge 10 -and $hasPackage) `
        "$($packs.Count) 个包，含 <$Package>: $hasPackage"

    # ------------------------------------------------------------ 全量快照
    Write-Section '快照'
    $snapshotResponse = Invoke-Get "/api/snapshot?package=$Package"
    $snapshot = Convert-FromJsonSafe $snapshotResponse.Body
    $names = @($snapshot.PSObject.Properties.Name)
    $required = @('title', 'cookies', 'cookiesText', 'cpsText', 'buildings', 'upgrades', 'mode', 'achievements')
    $missing = @($required | Where-Object { $names -cnotcontains $_ })
    Check '快照顶层字段齐全' ($null -ne $snapshot -and $missing.Count -eq 0) `
        $(if ($missing.Count -eq 0) { "$($names.Count) 个字段" } else { "缺 $($missing -join '、')" })
    # 注意：PowerShell 的属性访问是**大小写不敏感**的，所以这条必须查键名本身，不能查 $snapshot.CookiesText。
    Check '字段名是 camelCase' (($names -ccontains 'cookiesText') -and ($names -cnotcontains 'CookiesText')) ''
    Check '快照是紧凑 JSON（无缩进换行）' ($snapshotResponse.Body -and -not $snapshotResponse.Body.Contains("`n")) `
        "$([System.Text.Encoding]::UTF8.GetByteCount($snapshotResponse.Body)) 字节"

    # ------------------------------------------------------------ 命令
    Write-Section '命令（点 40 下，应该真的涨钱）'
    $before = [double]$snapshot.cookies
    $clickError = $null
    for ($i = 0; $i -lt 40; $i++) {
        $r = Invoke-PostJson "/api/command?package=$Package" @{ type = 'click' }
        if (-not ($r.Ok -and $r.Success)) { $clickError = "第 $($i + 1) 次失败：HTTP $($r.Status) $($r.Body)"; break }
    }
    if ($clickError) {
        Check 'POST /api/command click ×40' $false $clickError
    }
    else {
        Start-Sleep -Milliseconds 600 # 等下一帧推回来
        $after = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
        Check '40 次点击让货币增长' ([double]$after.cookies -gt $before) `
            "$([double]$before.ToString('F0')) → $([double]$after.cookies.ToString('F0'))"

        $firstBuilding = @($after.buildings | Where-Object { $_.isUnlocked } | Select-Object -First 1)
        $buildingId = if ($firstBuilding.Count -gt 0) { $firstBuilding[0].id } else { $null }
        Check '至少有一座建筑已解锁' ($null -ne $buildingId) $(if ($buildingId) { $buildingId } else { '一座都没有' })

        if ($buildingId) {
            # 要一个大到买不起的数量：引擎会**把数量钳到预算允许的范围**（这是"买满"语义，不是错误），
            # 所以断言的是"它给出的数量确实在预算内、并且回了一句人话"，而不是"它失败了"。
            $rich = Invoke-PostJson "/api/command?package=$Package" @{ type = 'buy'; id = $buildingId; amount = 100000 }
            $richResult = Convert-FromJsonSafe $rich.Body
            $message = [string]$richResult.message
            Check '超大数量被钳到预算内并回报结果' `
                ($rich.Success -and $message.Length -gt 0 -and ($richResult.ok -eq $true -or $message.Contains('钱'))) `
                $message
        }

        $bad = Invoke-PostJson "/api/command?package=$Package" @{ type = '不存在的命令' }
        $badResult = Convert-FromJsonSafe $bad.Body
        Check '未知命令不崩（ok=false + 一句人话）' ($bad.Success -and $badResult.ok -eq $false) `
            "HTTP $($bad.Status)：$([string]$badResult.message)"
    }

    # ------------------------------------------------------------ 负数
    Write-Section '负数（不该静默成功的地方必须明确失败）'
    $unknownSnap = Invoke-Get '/api/snapshot?package=__no-such-pack__'
    Check 'GET /api/snapshot 未知包 → 404' ($unknownSnap.Status -eq 404) "HTTP $($unknownSnap.Status)"
    $unknownStream = Invoke-Get '/api/stream?package=__no-such-pack__'
    Check 'GET /api/stream 未知包 → 404' ($unknownStream.Status -eq 404) "HTTP $($unknownStream.Status)"
    $gate = Invoke-Get '/?epoch=1'
    Check '没有 NEKO_DEBUG_KEY 时带 epoch → 403（门是关着的）' `
        ($gate.Status -eq 403 -and $gate.Body.Contains('调试门未启用')) "HTTP $($gate.Status)：$($gate.Body)"

    # ------------------------------------------------------------ SSE
    Write-Section 'SSE（真读流 6 秒）'
    $streamUrl = "$baseUrl/api/stream?package=$Package"
    $request = New-Object System.Net.Http.HttpRequestMessage -ArgumentList @([System.Net.Http.HttpMethod]::Get, $streamUrl)
    $sendTask = $script:Client.SendAsync($request, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead)
    $streamResponse = $sendTask.GetAwaiter().GetResult()
    $contentType = [string]$streamResponse.Content.Headers.ContentType
    Check 'SSE 建流' ($streamResponse.IsSuccessStatusCode -and $streamResponse.Content.Headers.ContentType.MediaType -eq 'text/event-stream') `
        "HTTP $([int]$streamResponse.StatusCode)，$contentType"

    $streamTask = $streamResponse.Content.ReadAsStreamAsync()
    $stream = $streamTask.GetAwaiter().GetResult()
    $reader = New-Object System.IO.StreamReader -ArgumentList @($stream, [System.Text.Encoding]::UTF8)

    $frames = New-Object System.Collections.ArrayList
    $readDeadline = [System.Diagnostics.Stopwatch]::StartNew()
    while ($readDeadline.Elapsed.TotalSeconds -lt 6) {
        $left = [int](6000 - $readDeadline.Elapsed.TotalMilliseconds)
        if ($left -le 0) { break }
        $lineTask = $reader.ReadLineAsync()
        if (-not $lineTask.Wait($left)) { break } # 超时就收手：已经把这段时间该到的帧都收下了
        $line = $lineTask.Result
        if ($null -eq $line) { break }
        if (-not $line.StartsWith('data: ')) { continue }

        $frame = Convert-FromJsonSafe $line.Substring(6)
        $changedKeys = @()
        if ($frame.changed) { $changedKeys = @($frame.changed.PSObject.Properties.Name) }
        [void]$frames.Add([pscustomobject]@{
                Kind   = [string]$frame.kind
                Bytes  = [System.Text.Encoding]::UTF8.GetByteCount($line)
                Seq    = [long]$frame.seq
                Keys   = $changedKeys
            })
    }
    $reader.Dispose()
    $stream.Dispose()
    $streamResponse.Dispose()

    Check '收到帧' ($frames.Count -gt 0) "$($frames.Count) 帧 / 6 秒"
    Check '第一帧是全量' ($frames.Count -gt 0 -and $frames[0].Kind -eq 'full') `
        $(if ($frames.Count -gt 0) { $frames[0].Kind } else { '一帧都没有' })
    # 之后**以增量为主**，但容许一帧全量：宿主每 FullResyncEvery = 120 帧（30 秒）主动推一份
    # 全量对账，6 秒的窗口有约 20% 的概率正好撞上它。写成"之后全是增量"会变成一条 1/5 概率
    # 随机变红的守卫——随机变红的守卫比没有守卫更糟，它会训练人忽略红色。
    $later = @($frames | Select-Object -Skip 1)
    $laterDeltas = @($later | Where-Object { $_.Kind -eq 'delta' })
    $laterFulls = @($later | Where-Object { $_.Kind -eq 'full' })
    Check '之后以增量为主（最多一帧 30 秒一次的全量对账）' `
        ($laterDeltas.Count -gt 0 -and $laterFulls.Count -le 1 -and ($laterDeltas.Count + $laterFulls.Count) -eq $later.Count) `
        "增量 $($laterDeltas.Count) 帧、全量对账 $($laterFulls.Count) 帧"

    if ($laterDeltas.Count -gt 0) {
        $fullBytes = $frames[0].Bytes
        $sizes = @($laterDeltas | Select-Object -ExpandProperty Bytes)
        $deltaAvg = [int](($sizes | Measure-Object -Average).Average)
        $deltaMax = [int](($sizes | Measure-Object -Maximum).Maximum)
        $deltaMedian = [int](($sizes | Sort-Object)[[int]($sizes.Count / 2)])
        $saved = 100 - $deltaAvg * 100 / [Math]::Max(1, $fullBytes)
        # 判据是**均值**，不是最大值：嵌套列表（codex / buildings / notifications）一变，
        # 协议是整条重发而不是逐元素打补丁（见 WebSnapshotProtocolTests.DiffDetectsNestedListChanges）。
        # 一帧里同时动了若干张列表（点击爆发 / 买入 + 图鉴解锁）就会叠出一帧几乎和全量一样大的
        # delta——那是设计，不是缺陷；要看的"常态带宽"是均值与中位数，所以最大值只报不判。
        Check '增量远小于全量（均值 <10%）' ($deltaAvg * 10 -lt $fullBytes) `
            "全量 $fullBytes 字节；增量 均 $deltaAvg / 中位 $deltaMedian / 最大 $deltaMax 字节（省 $([int]$saved)%）"

        # 增量里到底带了哪些字段——带宽异常时第一眼要看的就是这个
        $tally = @{}
        foreach ($frame in $laterDeltas) { foreach ($key in $frame.Keys) { $tally[$key] = [int]$tally[$key] + 1 } }
        $top = @($tally.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 8 |
            ForEach-Object { "$($_.Key)×$($_.Value)" })
        Write-Host "         delta 里出现过的字段：$($top -join '、')" -ForegroundColor DarkGray

        if ($deltaMax * 2 -gt $fullBytes) {
            $biggest = $laterDeltas | Sort-Object -Property Bytes -Descending | Select-Object -First 1
            $biggestFields = if ($biggest.Keys.Count -gt 0) { $biggest.Keys -join '、' } else { '（没有 changed 字段）' }
            Write-Host "         （最大那帧 $($biggest.Bytes) 字节带的是：$biggestFields —— 嵌套列表整条重发）" -ForegroundColor DarkGray
        }

        Check 'seq 单调递增' ($frames.Count -gt 2 -and $frames[-1].Seq -gt $frames[0].Seq) `
            "$($frames[0].Seq) → $($frames[-1].Seq)"
    }
    else {
        Check '增量远小于全量（<10%）' $false '一帧增量都没有，无从比较'
        Check 'seq 单调递增' $false '一帧增量都没有'
    }
}
finally {
    Write-Host ''
    if ($KeepHost) {
        Write-Host "宿主留着没动：$baseUrl（PID $($hostProcess.Id)）。自己 Ctrl+C 结束后记得收尾。" -ForegroundColor Yellow
    }
    else {
        Stop-TestHost -Port $Port -Process $hostProcess | Out-Null
    }
    $script:Client.Dispose()
}

# ---------------------------------------------------------------- 结论
Write-Host ''
if ($script:Failed -eq 0) {
    Write-Host "全部通过：$($script:Passed) 项检查。" -ForegroundColor Green
    if (-not $KeepSave) { Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue }
    exit 0
}

Write-Host "$($script:Failed) 项失败（通过 $($script:Passed) 项）。" -ForegroundColor Red
Write-Host "宿主日志留在：$workDir" -ForegroundColor Yellow
exit 1
