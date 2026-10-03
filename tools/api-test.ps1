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
# 与单元测试的分工：`tools/build.ps1 -Strict` 守引擎（493 个用例 + 公开 API 快照），
# 本脚本守"宿主 + 浏览器协议"那一段。CI 里两条都跑（.github/workflows/ci.yml）。
#
# 五条刻意为之的行为（都不是默认就该有的，是踩出来的）：
#   ① **自带临时存档目录**（--save-root）。探针会点击、会买入；跑在真实存档上等于
#      把玩家的进度当测试夹具。旧版探针就是这么干的（.tmp/api-probe），收进仓库时必须改掉。
#   ② **强制清掉 NEKO_DEBUG_KEY 再起子进程**，于是"缺省门是关着的"这条断言在任何开发机上
#      都成立，而不是"取决于跑的人 shell 里有没有那个变量"。
#   ③ **收尾按端口反查进程**：`dotnet run` 会再起一个真正的宿主子进程，只杀 dotnet run 自己
#      会留下还在监听的孤儿（本仓库踩过）。杀完还要确认端口真的松手。
#   ④ **最后一段会再起一次宿主**（同一个存档目录）：离线补发只在读档那一刻发生，而"读档"
#      没法在一次会话里伪造。所以那一段真的走一遍玩家的路——存档 → 把存档里的"上次保存时刻"
#      改老 5 小时 → 重新起宿主——验"补发出现 → 没播报之前刷新不消失 → 收下之后消失"。
#      它同时是"读档 + 存档格式 + 弹窗数据源"这三件事唯一的端到端证据。
#   ⑤ **检查点覆盖审计**（脚本收尾自己跑）：源码里有几处 `Check` 调用点，这次就该执行到几处。
#      "没执行到"的调用点会被**连行号点名**（红字）并让退出码非 0；确实跑不了的检查点用第 4 个
#      参数**显式跳过**（打印 `[SKIP]` 与理由、计入总数）。为什么非有它不可：这份脚本曾经源码里
#      写着 **52 处** `Check` 而运行器只报 **51 项**，而且**没有一处能指出少了哪个**——实测是
#      **3 处调用点从来没执行过**（`if ($clickError)` 的一个面、`if ($laterDeltas…)` 的 `else` 里
#      两处），同时 **1 处**写在 `foreach` 里跑了 3 次，一多一少正好相抵。计数对不上只是症状，
#      "某条检查悄悄没跑"才是病——所以现在由机器来数，不靠这一行注释。
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
$script:Skipped = 0
# 每个检查点被执行到的**调用点行号**，收尾的覆盖审计拿它跟本文件的语法树比对（见头部 ⑤）。
$script:ReachedLines = New-Object System.Collections.Generic.HashSet[int]

function Write-Section([string]$Title) {
    Write-Host ''
    Write-Host $Title -ForegroundColor Cyan
}

# 第 4 个参数 $Skip 非空 = **这条检查这次跑不了**，但要说出来：打印 `[SKIP]` + 理由、计入跳过数。
# 为什么不是"跑不了就干脆不写这个 Check"：静默少一条检查正是本脚本要防的那类失败（见头部 ⑤）。
function Check([string]$Name, [bool]$Ok, [string]$Detail = '', [string]$Skip = '') {
    [void]$script:ReachedLines.Add($MyInvocation.ScriptLineNumber)
    if ($Skip.Length -gt 0) {
        $script:Skipped++
        Write-Host "  [SKIP] $Name  — $Skip" -ForegroundColor DarkYellow
        return
    }
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

# 起宿主。**每次都会先摘掉 NEKO_DEBUG_KEY**：这样"缺省门是关着的"才是被测事实，
# 而不是跑的人 shell 里有没有那个变量的巧合（第二段"离线段"会再起一次，同样适用）。
function Start-TestHost([int]$Port, [string]$SaveRoot, [string]$OutLog, [string]$ErrLog, [string]$LatencyLog) {
    $hadKey = Test-Path Env:NEKO_DEBUG_KEY
    $savedKey = if ($hadKey) { $env:NEKO_DEBUG_KEY } else { $null }
    if ($hadKey) { Remove-Item Env:NEKO_DEBUG_KEY }

    try {
        # 路径一律自己加引号：Start-Process 是用空格拼接参数表的，工作区路径里有空格时不加引号会散架。
        # --latency-log 也指向临时目录：宿主默认会往仓库的 artifacts/latency.txt 追加会话行，
        # 而那份文件**是别人的真人数据**，跑一次端到端不该在上面留痕。
        $hostArgs = @(
            'run', '-m:1', '--no-build', '--project', "`"$webDir`"",
            '--', '--urls', "`"http://127.0.0.1:$Port`"", '--save-root', "`"$SaveRoot`"",
            '--latency-log', "`"$LatencyLog`""
        )
        return Start-Process -FilePath 'dotnet' -ArgumentList $hostArgs -PassThru -NoNewWindow `
            -RedirectStandardOutput $OutLog -RedirectStandardError $ErrLog
    }
    finally {
        if ($hadKey) { $env:NEKO_DEBUG_KEY = $savedKey }
    }
}

# 等宿主就绪。返回 @{ Ready; Probe }——探针文本要带回去，失败时它就是唯一的线索。
function Wait-TestHost([int]$Port, $Process, [int]$TimeoutSeconds = 60) {
    $lastProbe = ''
    $deadline = [System.Diagnostics.Stopwatch]::StartNew()
    while ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process.HasExited) { break }
        Start-Sleep -Milliseconds 400
        $probe = Invoke-Get '/api/ping'
        if ($probe.Ok -and $probe.Success) { return [pscustomobject]@{ Ready = $true; Probe = '' } }
        $lastProbe = "HTTP $($probe.Status) $($probe.Body)"
    }

    return [pscustomobject]@{ Ready = $false; Probe = $lastProbe }
}

function Show-HostLogs([string]$OutLog, [string]$ErrLog) {
    if (Test-Path $OutLog) { Get-Content $OutLog -Encoding UTF8 | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" } }
    if ((Test-Path $ErrLog) -and (Get-Item $ErrLog).Length -gt 0) {
        Write-Host '  stderr：' -ForegroundColor Red
        Get-Content $ErrLog -Encoding UTF8 | Select-Object -Last 20 | ForEach-Object { Write-Host "    $_" }
    }
}

# ---------------------------------------------------------------- 检查点覆盖审计
# 拿本文件的语法树数出 `Check` 的调用点，跟运行时记下的调用点行号比：**少了的那几处点名报红**。
# 判据是"调用点全都到达过"——不是"总数看起来对"。总数对了而某处没跑，正是这个审计要抓的形态
# （所以每一处 Check 都写成无条件执行，条件性用第 4 个参数表达；循环里的检查点也拆成独立调用）。
function Get-CheckPointCoverage {
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$tokens, [ref]$errors)
    $sites = @($ast.FindAll({
                param($node)
                $node -is [System.Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Check'
            }, $true))
    $unreached = @()
    foreach ($site in $sites) {
        $hit = $false
        for ($line = $site.Extent.StartLineNumber; $line -le $site.Extent.EndLineNumber; $line++) {
            if ($script:ReachedLines.Contains($line)) { $hit = $true; break }
        }
        if (-not $hit) { $unreached += $site }
    }
    return [pscustomobject]@{ Sites = $sites.Count; Unreached = $unreached }
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
# 离线段要重启一次宿主（见下面"离线收益"一节），第二段另开两个日志文件，
# 不然 Start-Process 的重定向会把第一段的日志覆盖掉——失败时最要紧的就是它。
$outLog2 = Join-Path $workDir 'host2.out.log'
$errLog2 = Join-Path $workDir 'host2.err.log'
# 埋点也落在这里：仓库的 artifacts/latency.txt 是别人的真人数据，端到端不该往上写。
$latencyLog = Join-Path $workDir 'latency.txt'
New-Item -ItemType Directory -Force -Path $saveRoot | Out-Null

Write-Host ''
Write-Host "目标：$baseUrl ｜ 内容包：$Package ｜ 存档：$saveRoot" -ForegroundColor DarkGray

# ---- 起宿主
$hostProcess = Start-TestHost -Port $Port -SaveRoot $saveRoot -OutLog $outLog -ErrLog $errLog -LatencyLog $latencyLog
$wait = Wait-TestHost -Port $Port -Process $hostProcess

if (-not $wait.Ready) {
    Write-Host ''
    Write-Host "宿主 60 秒内没有就绪（PID $($hostProcess.Id)）；最后一次探测：$($wait.Probe)" -ForegroundColor Red
    Show-HostLogs -OutLog $outLog -ErrLog $errLog
    Stop-TestHost -Port $Port -Process $hostProcess | Out-Null
    exit 1
}

try {
    Write-Host ''
    Write-Host "宿主已就绪，开始打端点。" -ForegroundColor Green

    # ------------------------------------------------------------ 静态文件
    Write-Section '静态文件（首页 404 是这一层最经典的沉默失败）'
    $staticFiles = @(foreach ($item in @(
                @{ Path = '/'; Needle = 'NekoClicker' },
                @{ Path = '/app.js'; Needle = 'EventSource' },
                @{ Path = '/app.css'; Needle = '--accent' }
            )) {
            $r = Invoke-Get $item.Path
            $hit = [bool]($r.Body -and $r.Body.Contains($item.Needle))
            $bytes = if ($r.Body) { [System.Text.Encoding]::UTF8.GetByteCount($r.Body) } else { 0 }
            [pscustomobject]@{
                Ok     = ($r.Success -and $hit)
                Detail = "HTTP $($r.Status)，$bytes 字节，含 <$($item.Needle)>: $hit"
            }
        })
    # 三处**分开写**是刻意的：写在循环里的话，一处调用点会执行三次，覆盖审计的账就永远对不上
    # （"源码 52 处 / 运行 51 项"就是这么来的）。
    Check 'GET /' $staticFiles[0].Ok $staticFiles[0].Detail
    Check 'GET /app.js' $staticFiles[1].Ok $staticFiles[1].Detail
    Check 'GET /app.css' $staticFiles[2].Ok $staticFiles[2].Detail

    # 前端面板：浏览器拿到的东西里真的有那个挂载点与渲染函数。
    # 判据只能是"送到浏览器的那份文本里有它"——本脚本不跑 JS，"好不好看"没有守卫（STATUS §6）。
    Write-Section '前端面板（挂载点与渲染函数真的送到了浏览器）'
    $indexPage = Invoke-Get '/'
    $logMount = [bool]($indexPage.Body -and $indexPage.Body.Contains('data-panel="log"'))
    Check '首页里有日志面板的挂载点' ($indexPage.Success -and $logMount) "含 <data-panel=`"log`">: $logMount"

    $appScript = Invoke-Get '/app.js'
    $logRender = [bool]($appScript.Body -and $appScript.Body.Contains('renderNotifications'))
    Check 'app.js 里有日志渲染函数' ($appScript.Success -and $logRender) "含 <renderNotifications>: $logRender"

    # 离线收益弹窗：同一层判据——挂在首页上的容器与画它的那个函数真的送到了浏览器。
    $offlineMount = [bool]($indexPage.Body -and $indexPage.Body.Contains('id="offline"'))
    Check '首页里有离线收益弹窗的挂载点' ($indexPage.Success -and $offlineMount) "含 <id=`"offline`">: $offlineMount"

    $offlineRender = [bool]($appScript.Body -and $appScript.Body.Contains('renderOffline'))
    Check 'app.js 里有离线收益渲染函数' ($appScript.Success -and $offlineRender) "含 <renderOffline>: $offlineRender"

    # 永久线面板：同一层判据（挂载点 + 渲染函数真的送到了浏览器）
    $permanentMount = [bool]($indexPage.Body -and $indexPage.Body.Contains('data-panel="permanent"'))
    Check '首页里有永久线面板的挂载点' ($indexPage.Success -and $permanentMount) "含 <data-panel=`"permanent`">: $permanentMount"

    $permanentRender = [bool]($appScript.Body -and $appScript.Body.Contains('renderPermanent'))
    Check 'app.js 里有永久线渲染函数' ($appScript.Success -and $permanentRender) "含 <renderPermanent>: $permanentRender"

    # 「表态已经展示过」这条报告（1.5.0 那会儿是结局的落定条件；1.6.0 起只是诊断信号，
    # 落定看的是作答）。前端那一半的判据只能是"送到浏览器的文本里有这个命令"
    # ——本脚本不跑 JS（见上面的说明）。
    $choicesShownSend = [bool]($appScript.Body -and $appScript.Body.Contains('"choicesShown"'))
    Check 'app.js 会报告「表态已经展示过」（诊断信号）' ($appScript.Success -and $choicesShownSend) "含 <`"choicesShown`">: $choicesShownSend"

    # 前端必须能把表态答掉：1.6.0 起"还有答得上的待答表态"就是结局不落定的唯一原因，
    # 所以"浏览器能不能发出 answer"是承重的（只测文本，不跑 JS）。
    $answerSend = [bool]($appScript.Body -and $appScript.Body.Contains('"answer"'))
    Check 'app.js 会发出 answer（作答才是解除等待的动作）' ($appScript.Success -and $answerSend) "含 <`"answer`">: $answerSend"

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

    # 档位的**形状**，而不只是'键在不在'。前端认的是 modeName（服务端给的名字），
    # 不是 mode 那个枚举序数：曾经前端写 (state.mode ?? "").toLowerCase()，而线上 mode 是数字，
    # 于是真页面上每次 render() 都在中段抛 TypeError，后面所有面板一次都没画出来过
    # （批量档位按钮 / 离线收益 / 表态 sheet / 图鉴 / 成就 / 日志）。见 OPEN_WORK 的 N 条。
    $modeVocabulary = @('buy1', 'buy10', 'buy100', 'buymax', 'sell1', 'sell10', 'sellmax')
    $modeNameIsString = $snapshot.modeName -is [string]
    Check 'modeName 是字符串，且在前端认的词表里' `
        ($modeNameIsString -and ($modeVocabulary -ccontains $snapshot.modeName)) `
        "modeName = <$($snapshot.modeName)>（类型 $(if ($modeNameIsString) { 'string' } else { $snapshot.modeName.GetType().Name })）"

    # 纯新增：数字形的 mode 原样留着，别的消费者不会因为我们加了名字而被打断。
    Check 'mode 仍是数字（没有改掉老字段的形状）' `
        ($null -ne $snapshot.mode -and -not ($snapshot.mode -is [string])) "mode = <$($snapshot.mode)>"

    # 档位名的**往返**：把快照给的每个 token 原样发回命令侧，再读一次快照，必须原样回来。
    # 这比'名字看起来对'强：它证明这几个字面量在真宿主上真的收得下（GameHost.ParseMode），
    # 也证明 modeName 确实跟着模式走，而不是一个写死的常量。跑完还原成原来的档位，
    # 免得影响后面那些'买入/买满'的断言。
    $originalMode = $snapshot.modeName
    $roundTripFailures = @()
    foreach ($token in $modeVocabulary) {
        $modeResult = Invoke-PostJson "/api/command?package=$Package" @{ type = 'mode'; mode = $token }
        if (-not ($modeResult.Ok -and $modeResult.Success)) { $roundTripFailures += "$token：命令被拒"; continue }
        Start-Sleep -Milliseconds 350 # 等下一帧推回来（推送周期 250ms）
        $echo = (Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body).modeName
        if ($echo -cne $token) { $roundTripFailures += "$token -> 回读 $echo" }
    }
    Invoke-PostJson "/api/command?package=$Package" @{ type = 'mode'; mode = $originalMode } | Out-Null
    Start-Sleep -Milliseconds 350
    Check '七个档位名都能原样发回去、并被快照回读' ($roundTripFailures.Count -eq 0) `
        $(if ($roundTripFailures.Count -eq 0) { "7 个 token 往返一致（已还原为 $originalMode）" } else { $roundTripFailures -join '；' })
    Check '快照是紧凑 JSON（无缩进换行）' ($snapshotResponse.Body -and -not $snapshotResponse.Body.Contains("`n")) `
        "$([System.Text.Encoding]::UTF8.GetByteCount($snapshotResponse.Body)) 字节"

    # 升级行的货币语义。前端不再解释枚举序数（老写法 `currency === 1` 一错就是静默的），
    # 所以服务端必须把"这一行花哪个钱包、货币叫什么/什么图标"说全——这就是永久线面板的全部依据。
    Write-Section '升级行的货币语义（永久线面板的依据）'
    $upgrades = @($snapshot.upgrades)
    $missingFields = @()
    $wrongIcon = @()
    $wrongWallet = @()
    $permanentCount = 0
    $prestigeRows = 0
    foreach ($u in $upgrades) {
        $fields = @($u.PSObject.Properties.Name)
        foreach ($f in @('currencyIcon', 'currencyName', 'usesPrestigeCurrency', 'isPermanent', 'price')) {
            if ($fields -cnotcontains $f) { $missingFields += "$($u.id):$f" }
        }
        if ([bool]$u.usesPrestigeCurrency) {
            $prestigeRows++
            if ($u.currencyIcon -ne $snapshot.prestigeCurrencyIcon) { $wrongIcon += $u.id }
        }
        elseif ($u.currencyIcon -ne $snapshot.currencyIcon) { $wrongIcon += $u.id }

        if ([bool]$u.isPermanent) {
            $permanentCount++
            if (-not [bool]$u.usesPrestigeCurrency) { $wrongWallet += $u.id }
        }
    }
    $missingDetail = if ($missingFields.Count -eq 0) { "$($upgrades.Count) 行都齐" } else { "缺：" + (($missingFields | Select-Object -First 4) -join '、') }
    Check '每条升级行都带货币语义字段' ($upgrades.Count -gt 0 -and $missingFields.Count -eq 0) $missingDetail

    $iconDetail = if ($wrongIcon.Count -eq 0) { "普通货币 $($upgrades.Count - $prestigeRows) 行、转生货币 $prestigeRows 行" } else { "对不上：" + (($wrongIcon | Select-Object -First 4) -join '、') }
    Check '货币图标与该行所用的钱包一致' ($upgrades.Count -gt 0 -and $wrongIcon.Count -eq 0) $iconDetail

    # 这个包必须有永久线，否则下面那条（以及前端那个面板）都是空的。
    Check '永久线全部花转生货币（前端按这个旗子选钱包）' ($permanentCount -ge 1 -and $wrongWallet.Count -eq 0) `
        "永久升级 $permanentCount 条$(if ($wrongWallet.Count -gt 0) { "，其中 " + ($wrongWallet -join '、') + " 用了普通货币" })"

    # ------------------------------------------------------------ 命令
    Write-Section '命令（点 40 下，应该真的涨钱）'
    $before = [double]$snapshot.cookies
    $clickError = $null
    for ($i = 0; $i -lt 40; $i++) {
        $r = Invoke-PostJson "/api/command?package=$Package" @{ type = 'click' }
        if (-not ($r.Ok -and $r.Success)) { $clickError = "第 $($i + 1) 次失败：HTTP $($r.Status) $($r.Body)"; break }
    }
    # 成功 / 失败是**同一个检查点的两个面**：写成 if / else，两个面里必有一处 Check 永远不执行
    # （实测就是这里，见头部 ⑤），所以合并成一处，把失败原因放进 detail。
    Check 'POST /api/command click ×40 全部被接受' ($null -eq $clickError) $(if ($clickError) { $clickError } else { '40 / 40' })

    $after = $null
    if (-not $clickError) {
        Start-Sleep -Milliseconds 600 # 等下一帧推回来
        $after = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
    }
    $afterOk = ($null -ne $after)
    Check '40 次点击让货币增长' ($afterOk -and [double]$after.cookies -gt $before) `
        $(if ($afterOk) { "$([double]$before.ToString('F0')) → $([double]$after.cookies.ToString('F0'))" } else { '—' }) `
        $(if ($afterOk) { '' } else { '点击批次失败，读不到点击后的快照' })

    $buildingId = $null
    if ($afterOk) {
        $firstBuilding = @($after.buildings | Where-Object { $_.isUnlocked } | Select-Object -First 1)
        $buildingId = if ($firstBuilding.Count -gt 0) { $firstBuilding[0].id } else { $null }
    }
    Check '至少有一座建筑已解锁' ($afterOk -and $null -ne $buildingId) `
        $(if ($afterOk) { $(if ($buildingId) { $buildingId } else { '一座都没有' }) } else { '—' }) `
        $(if ($afterOk) { '' } else { '点击批次失败，拿不到建筑列表' })

    # 要一个大到买不起的数量：引擎会**把数量钳到预算允许的范围**（这是"买满"语义，不是错误），
    # 所以断言的是"它给出的数量确实在预算内、并且回了一句人话"，而不是"它失败了"。
    $clampOk = $false
    $clampDetail = '—'
    if ($buildingId) {
        $rich = Invoke-PostJson "/api/command?package=$Package" @{ type = 'buy'; id = $buildingId; amount = 100000 }
        $richResult = Convert-FromJsonSafe $rich.Body
        $clampDetail = [string]$richResult.message
        $clampOk = ($rich.Success -and $clampDetail.Length -gt 0 -and ($richResult.ok -eq $true -or $clampDetail.Contains('钱')))
    }
    Check '超大数量被钳到预算内并回报结果' $clampOk $clampDetail `
        $(if ($buildingId) { '' } else { '没有已解锁的建筑可买（上一条已经报了红）' })

    # 通知面板的数据源：脚本做过的这些动作应该已经让引擎往快照里推过消息了。
    # 前端只能画它拿到的东西，所以这里守的是"消息真的从引擎走到了 JSON"。
    # 刻意**不点名某一条文案**：买建筑不发通知（只有买升级 / 成就解锁 / 增益 /
    # 金猫 / 舍命会发），而这里跑的是"点击 + 买建筑"，产出的多半是成就解锁。
    # 这里刻意**不看 $buildingId**：通知里那多半是"点击解锁的成就"，买不买得成建筑与它无关，
    # 所以它只在"读不到快照"时才跳过。
    $afterBuy = $null
    $notes = @()
    if ($afterOk) {
        Start-Sleep -Milliseconds 400
        $afterBuy = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
        if ($null -ne $afterBuy -and $null -ne $afterBuy.notifications) { $notes = @($afterBuy.notifications) }
    }
    $noteDetail = if ($notes.Count -gt 0) {
        ($notes | Select-Object -First 2 | ForEach-Object { $_.message }) -join ' ｜ '
    } else { '（一条都没有）' }
    Check '动作之后快照的 notifications 里有真消息' ($afterOk -and $notes.Count -ge 1) "$($notes.Count) 条：$noteDetail" `
        $(if ($afterOk) { '' } else { '点击批次失败，读不到快照' })

    $shaped = $true
    foreach ($note in $notes) {
        $noteFields = @($note.PSObject.Properties.Name)
        foreach ($field in @('message', 'icon', 'kind', 'timestamp')) {
            if ($noteFields -cnotcontains $field) { $shaped = $false }
        }
        if ([int]$note.kind -lt 0 -or [int]$note.kind -gt 3) { $shaped = $false }
        # 时间戳是"产生时的游戏时间"，不该跑到当前游戏时间之后。
        if ([double]$note.timestamp -gt [double]$afterBuy.playTimeSeconds + 1) { $shaped = $false }
    }
    Check '每条通知字段齐全、kind ∈ 0..3、时间戳不超过当前游戏时间' ($shaped -and $notes.Count -gt 0) `
        "$($notes.Count) 条$(if ($notes.Count -gt 0) { "；第一条 kind=$($notes[0].kind)（数字枚举，前端按序数上色），timestamp=$($notes[0].timestamp)" })" `
        $(if ($notes.Count -gt 0) { '' } else { '一条通知都没有，无形状可判（上一条已经报了红）' })

    # 下面三条**不依赖点击 / 买入的结果**（只要宿主还在，它们就能跑），所以从原来的 if / else
    # 里挪到外面：留在分支里的话，上游一失败它们就跟着从账上消失——那正是要防的静默。
    $bad = Invoke-PostJson "/api/command?package=$Package" @{ type = '不存在的命令' }
    $badResult = Convert-FromJsonSafe $bad.Body
    Check '未知命令不崩（ok=false + 一句人话）' ($bad.Success -and $badResult.ok -eq $false) `
        "HTTP $($bad.Status)：$([string]$badResult.message)"

    # 「表态已经被展示过」这条报告：1.5.0 那会儿它是结局的落定条件，1.6.0 起只是**诊断信号**
    # （落定看的是作答）。它必须**幂等**：前端每渲染一帧就可能发一次，第二发不是错误、
    # 也不该改坏状态。这里刻意不要求"有表态挂着"——本脚本跑的是真实游玩的前几十秒，
    # 多半还没触发表态，而那条命令在没有任何待答表态时也必须安全返回（这正是幂等的第一层含义）。
    $shown1 = Invoke-PostJson "/api/command?package=$Package" @{ type = 'choicesShown' }
    $shown1Result = Convert-FromJsonSafe $shown1.Body
    $shown2 = Invoke-PostJson "/api/command?package=$Package" @{ type = 'choicesShown' }
    $shown2Result = Convert-FromJsonSafe $shown2.Body
    Check 'POST choicesShown 可用且幂等' `
        ($shown1.Success -and $shown1Result.ok -eq $true -and $shown2.Success -and $shown2Result.ok -eq $true) `
        "第一次：$([string]$shown1Result.message)；第二次：$([string]$shown2Result.message)"

    # 作答这条路必须真的通到引擎：**1.6.0 起它是唯一能解除结局等待的动作**，
    # 而"回答一个不存在的表态"必须明确失败（ok=false）而不是静默当成功——
    # 静默成功会让玩家以为处理完了，而结局照旧一直等着。
    $badAnswer = Invoke-PostJson "/api/command?package=$Package" `
        @{ type = 'answer'; id = '__no_such_choice__'; optionId = '__no_such_option__' }
    $badAnswerResult = Convert-FromJsonSafe $badAnswer.Body
    Check 'POST answer 对不存在的表态明确失败（ok=false + 一句人话）' `
        ($badAnswer.Success -and $badAnswerResult.ok -eq $false -and ([string]$badAnswerResult.message).Length -gt 0) `
        "HTTP $($badAnswer.Status)：ok=$($badAnswerResult.ok)；$([string]$badAnswerResult.message)"

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

    $fullBytes = if ($frames.Count -gt 0) { $frames[0].Bytes } else { 0 }
    $deltaAvg = -1
    $deltaMedian = -1
    $deltaMax = -1
    if ($laterDeltas.Count -gt 0) {
        $sizes = @($laterDeltas | Select-Object -ExpandProperty Bytes)
        $deltaAvg = [int](($sizes | Measure-Object -Average).Average)
        $deltaMax = [int](($sizes | Measure-Object -Maximum).Maximum)
        $deltaMedian = [int](($sizes | Sort-Object)[[int]($sizes.Count / 2)])

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
    }

    # 判据是**均值**，不是最大值：嵌套列表（codex / buildings / notifications）一变，
    # 协议是整条重发而不是逐元素打补丁（见 WebSnapshotProtocolTests.DiffDetectsNestedListChanges）。
    # 一帧里同时动了若干张列表（点击爆发 / 买入 + 图鉴解锁）就会叠出一帧几乎和全量一样大的
    # delta——那是设计，不是缺陷；要看的"常态带宽"是均值与中位数，所以最大值只报不判。
    # 两处 Check **都写在 if 外面**是刻意的：写好增量分支里的话，"一帧增量都没有"这种环境
    # 就会把两处调用点整块从账上抹掉（实测这就是 52 / 51 里的那两处）。判据本身不需要那个前提：
    # 没有增量时，均值这条会照着自己红，而 seq 单调性照样是可判的真话。
    $saved = if ($deltaAvg -ge 0) { 100 - $deltaAvg * 100 / [Math]::Max(1, $fullBytes) } else { 0 }
    Check '增量远小于全量（均值 <10%）' ($deltaAvg -ge 0 -and $deltaAvg * 10 -lt $fullBytes) `
        $(if ($deltaAvg -ge 0) { "全量 $fullBytes 字节；增量 均 $deltaAvg / 中位 $deltaMedian / 最大 $deltaMax 字节（省 $([int]$saved)%）" } else { '一帧增量都没有，无从比较' })
    Check 'seq 单调递增' ($frames.Count -gt 2 -and $frames[-1].Seq -gt $frames[0].Seq) `
        $(if ($frames.Count -gt 0) { "$($frames[0].Seq) → $($frames[-1].Seq)" } else { '一帧都没有' })

    # ------------------------------------------------------------ 离线收益
    # 这一段刻意**重启一次宿主**：离线补发只在读档的那一刻发生，而"读档"没法在一次会话里伪造。
    # 走的是玩家真实的路：存档 → 把存档里的"上次保存时刻"改老 5 小时 → 重新起宿主。
    # 三条判据缺一不可：读档后它出现（玩家看得到自己离线赚了多少）、没播报之前刷新不消失
    # （否则"弹一次"退化成"弹 0 次"）、收下之后消失（否则刷新一次就弹一次）。
    Write-Section '离线收益（真的重启一次宿主：存档 → 改老 → 读档 → 弹窗数据）'

    # 前置条件：离线补发按**基础产量**结算，产量为 0 就什么都补不出来（报告也不会出现）。
    # 上面的买入如果其实没买成（钱不够），这里会看得出来，而不是让后面的断言莫名其妙地红。
    $preSave = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
    if ($null -ne $preSave -and [double]$preSave.cookiesPerSecond -le 0) {
        for ($i = 0; $i -lt 20; $i++) { Invoke-PostJson "/api/command?package=$Package" @{ type = 'click' } | Out-Null }
        $firstUnlocked = @($preSave.buildings | Where-Object { $_.isUnlocked } | Select-Object -First 1)
        if ($firstUnlocked.Count -gt 0) {
            Invoke-PostJson "/api/command?package=$Package" @{ type = 'buy'; id = $firstUnlocked[0].id } | Out-Null
        }
        Start-Sleep -Milliseconds 400
        $preSave = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
    }
    $preCps = if ($null -ne $preSave) { [double]$preSave.cookiesPerSecond } else { -1 }
    Check '存档之前已经有产量（离线补发的前提）' ($preCps -gt 0) "cookiesPerSecond=$preCps"

    $saveCommand = Invoke-PostJson "/api/command?package=$Package" @{ type = 'save' }
    $saveResult = Convert-FromJsonSafe $saveCommand.Body
    Check 'POST /api/command save 成功' ($saveCommand.Success -and $saveResult.ok -eq $true) `
        "HTTP $($saveCommand.Status)：$([string]$saveResult.message)"

    Stop-TestHost -Port $Port -Process $hostProcess | Out-Null

    $saveFiles = @(Get-ChildItem -Path $saveRoot -Filter *.json -File -ErrorAction SilentlyContinue)
    $oneSave = ($saveFiles.Count -eq 1)
    Check '存档文件真的落在了临时目录里' $oneSave "$($saveFiles.Count) 个 .json 在 $saveRoot"

    $agedAt = [DateTimeOffset]::Now.AddHours(-5).ToString('o')
    $rawSave = $null
    $patched = $null
    if ($oneSave) {
        $savePath = $saveFiles[0].FullName
        $rawSave = Get-Content $savePath -Raw -Encoding UTF8
        $patched = $rawSave -replace '"LastSavedAt":"[^"]*"', "`"LastSavedAt`":`"$agedAt`""
        # 刻意用 .NET 写回（不带 BOM）：这是存档，不该由测试脚本顺手改掉它的字节形态。
        if ($patched -ne $rawSave) { [System.IO.File]::WriteAllText($savePath, $patched) }
    }
    $patchDetail = if (-not $oneSave) { '（没有唯一的存档文件可改）' }
    elseif ($patched -ne $rawSave) { "LastSavedAt → $agedAt" }
    else { '存档里没有可改的 LastSavedAt 字段' }
    Check '存档里写着上次保存时刻（能被改老）' ($oneSave -and $patched -ne $rawSave) $patchDetail

    # 第二段宿主**无条件起**：就算第一段没存下档，这条路也要走完——写得跟着"存档存在"走的话，
    # 上游一失败，后面八处检查点会一起从账上消失（那是这个脚本最老的那种静默）。改老存档的
    # 那一步仍然只在 $oneSave 时做，所以"没有存档"时这里起的是一个没有存档的宿主，如实报红。
    $hostProcess = Start-TestHost -Port $Port -SaveRoot $saveRoot -OutLog $outLog2 -ErrLog $errLog2 -LatencyLog $latencyLog
    $wait2 = Wait-TestHost -Port $Port -Process $hostProcess
    Check '第二段宿主带着那份存档起来了' $wait2.Ready $wait2.Probe

    $host2Missing = if ($wait2.Ready) { '' } else { '第二段宿主没起来，这条路没走到' }
    $afterLoad = if ($wait2.Ready) { Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body } else { $null }
    $offline = if ($null -ne $afterLoad) { $afterLoad.offline } else { $null }
    $offlineFields = if ($null -ne $offline) { @($offline.PSObject.Properties.Name) } else { @() }
    $wanted = @('elapsedSeconds', 'creditedSeconds', 'cookiesGained', 'wasCapped', 'durationText', 'cookiesText')
    $missingOffline = @($wanted | Where-Object { $offlineFields -cnotcontains $_ })
    $shapeOk = ($null -ne $offline) -and ($missingOffline.Count -eq 0)

    $shapeDetail = if (-not $wait2.Ready) { '第二段宿主没起来' }
    elseif ($null -eq $offline) { 'offline 是 null——补发根本没发生' }
    elseif ($missingOffline.Count -gt 0) { "缺 $($missingOffline -join '、')" }
    else { "补了 $($offline.cookiesText)，时长 $($offline.durationText)" }
    Check '读档之后快照里出现待播报的离线收益' $shapeOk $shapeDetail

    # 从这里往下，判据要用到"待播报的那份离线收益"本体：本体不在时**显式跳过**（`[SKIP]` +
    # 理由 + 计入总数），而不是像老写法那样留在 if 里、悄悄从账上消失。"收下之后不再有它"
    # 那一条尤其要跳：本体不存在时它会**空过**（假绿），而假绿比缺失更坏。
    $offlineMissing = if ($shapeOk) { '' } else { '快照里没有待播报的离线收益，无从判定' }
    $subjectMissing = if ($host2Missing) { $host2Missing } else { $offlineMissing }

    $elapsed = 0
    $credited = 0
    $sane = $false
    $saneDetail = '—'
    if ($shapeOk) {
        $elapsed = [double]$offline.elapsedSeconds
        $credited = [double]$offline.creditedSeconds
        # 改老了 5 小时，所以：确实离开了 5 小时上下、计入的不超过离开的、被标记为截断、
        # 补发量为正。上限具体是多少归内容包管，这里不写死。
        $sane = ([double]$offline.cookiesGained -gt 0) -and ($credited -gt 0) `
            -and ($credited -le $elapsed) -and ($elapsed -ge 4.5 * 3600) -and [bool]$offline.wasCapped
        $saneDetail = "离开 $([math]::Round($elapsed / 3600, 2))h、计入 $([math]::Round($credited / 3600, 2))h、补发 $($offline.cookiesGained)、wasCapped=$($offline.wasCapped)"
    }
    Check '离线时长与补发量自洽（5 小时超过上限，应标记被截断）' $sane $saneDetail $subjectMissing

    # 刷新页面 = 重新取一份全量快照。没播报之前它必须还在（前端就是靠全量帧拿到它的）。
    Start-Sleep -Milliseconds 300
    $reloaded = if ($wait2.Ready) { Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body } else { $null }
    $reloadDetail = if ($null -eq $reloaded) { '快照读不出来' }
    elseif ($null -eq $reloaded.offline) { 'offline 已经没了（补发根本没发生，或被提前清掉了）' }
    else { '再取一次仍在' }
    Check '没播报之前刷新（再取一次全量）不会让它消失' ($null -ne $reloaded -and $null -ne $reloaded.offline) `
        $reloadDetail $host2Missing

    $dismiss = if ($wait2.Ready) { Invoke-PostJson "/api/command?package=$Package" @{ type = 'dismissOffline' } } else { $null }
    $dismissResult = if ($null -ne $dismiss) { Convert-FromJsonSafe $dismiss.Body } else { $null }
    Check 'POST /api/command dismissOffline 成功' ($null -ne $dismiss -and $dismiss.Success -and $dismissResult.ok -eq $true) `
        $(if ($null -ne $dismiss) { "HTTP $($dismiss.Status)：$([string]$dismissResult.message)" } else { '—' }) `
        $host2Missing

    # 快照由游戏线程按 250ms 的节拍重算，所以这里轮询而不是立刻断言。
    $cleared = $null
    if ($wait2.Ready) {
        for ($i = 0; $i -lt 12; $i++) {
            Start-Sleep -Milliseconds 250
            $cleared = Convert-FromJsonSafe (Invoke-Get "/api/snapshot?package=$Package").Body
            if ($null -eq $cleared.offline) { break }
        }
    }
    $clearedDetail = if ($null -eq $cleared) { '快照读不出来' }
    elseif ($null -eq $cleared.offline) { 'offline 已是 null' }
    else { '收下之后它还在' }
    Check '收下之后快照里不再有它（刷新不会再弹）' ($null -ne $cleared -and $null -eq $cleared.offline) `
        $clearedDetail $subjectMissing

    # 两个标签页都会发这条命令，所以第二发必须是 ok，而不是"你已经点过了"这种错误。
    $again = if ($wait2.Ready) { Invoke-PostJson "/api/command?package=$Package" @{ type = 'dismissOffline' } } else { $null }
    $againResult = if ($null -ne $again) { Convert-FromJsonSafe $again.Body } else { $null }
    Check '重复收下是幂等的（第二个标签页也会发它）' ($null -ne $again -and $again.Success -and $againResult.ok -eq $true) `
        $(if ($null -ne $again) { "HTTP $($again.Status)：$([string]$againResult.message)" } else { '—' }) `
        $host2Missing
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
# 先跑覆盖审计，再报结论：**计数对不上不算通过**。源码里有几处 Check 调用点，这次就该执行到
# 几处；少了的那几处连行号一起点名（见头部 ⑤）。跳过的那几条 `[SKIP]` 已经计进总数。
$coverage = Get-CheckPointCoverage
$unreached = @($coverage.Unreached)
Write-Host ''
Write-Host "检查点覆盖：源码 $($coverage.Sites) 处 ｜ 执行到 $($coverage.Sites - $unreached.Count) 处 ｜ 通过 $($script:Passed) ｜ 失败 $($script:Failed) ｜ 跳过 $($script:Skipped)" `
    -ForegroundColor $(if ($unreached.Count -gt 0) { 'Red' } else { 'DarkGray' })
if ($unreached.Count -gt 0) {
    Write-Host "有 $($unreached.Count) 处检查点这次**没有执行到**——不许静默发生（「源码 N 处 / 运行 M 项」就是这么来的）：" -ForegroundColor Red
    foreach ($site in $unreached) {
        Write-Host "    第 $($site.Extent.StartLineNumber) 行：$((($site.Extent.Text -split "`n")[0]).Trim())" -ForegroundColor Red
    }
}

if ($script:Failed -eq 0 -and $unreached.Count -eq 0) {
    # "N 项检查"报的是**检查点总数**（通过 + 跳过），不是只报通过数——这样跳过一条也不会让
    # 别人以为这份脚本只有 51 项（"源码 52 处 / 运行 51 项"那个坑的另一半就在这里）。
    $total = $script:Passed + $script:Failed + $script:Skipped
    Write-Host "全部通过：$total 项检查（通过 $($script:Passed) ｜ 失败 0 ｜ 跳过 $($script:Skipped)）。" -ForegroundColor Green
    if (-not $KeepSave) { Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue }
    exit 0
}

if ($unreached.Count -gt 0) {
    Write-Host "$($script:Failed) 项失败、$($unreached.Count) 处检查点没有执行（通过 $($script:Passed) ｜ 跳过 $($script:Skipped)）。" -ForegroundColor Red
}
else {
    Write-Host "$($script:Failed) 项失败（通过 $($script:Passed) ｜ 跳过 $($script:Skipped)）。" -ForegroundColor Red
}
Write-Host "宿主日志留在：$workDir" -ForegroundColor Yellow
exit 1
