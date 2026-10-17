# start.ps1 -- 一体化启动。构建 + 起宿主，把已知的坑都堵掉。
#
# 为什么要有它（三个坑都是实测踩过的）：
#   1) `tools/web.ps1` 不带动词只做 build 就退出 —— 默认动词就是 build，
#      看起来像"启动不了"。这里把动词写死，不给你踩的机会。
#   2) 上次没退干净的宿主进程会占着 bin 里的 dll，构建报一堆
#      MSB3021 "being used by another process"。这里先清掉**本仓库自己的**残留进程
#      —— 但**保留正在服务的那个**（杀它等于把你开着的页面一起带走，见 Clear-StaleHosts）。
#   3) 执行策略会拦 .ps1。所以入口是 start.cmd（它用 -ExecutionPolicy Bypass 调用本脚本）；
#      你也可以直接用 `powershell -ExecutionPolicy Bypass -File tools\start.ps1`。
#
# 用法:
#   start.cmd                      网页那一侧（**默认**）：起 Web 宿主 + 自动开浏览器
#   start.cmd web [内容包]          同上，把动词写全
#   start.cmd play [内容包]         终端全屏界面（**不**起网页）
#   start.cmd list                 列出全部内容包
#   start.cmd stop [-Port N]       停掉这个端口上的 Web 宿主（只停**我们的**）
#   start.cmd web -NoBrowser       起宿主 / 复用已有的宿主，但**不开**浏览器
#   start.cmd web -Port 5300       换端口（等价于设环境变量 NEKO_PORT）
#   start.cmd web -SaveRoot <目录>  换存档根目录（默认 <仓库根>\saves）
#   start.cmd play -SaveRoot <目录> 终端改用另一份存档（与网页各玩各的进度）
#   start.cmd play -AllowSharedSave 明知两个宿主会互相覆盖，也要同时跑
#   start.cmd web -NoBuild         跳过构建（改的只是 wwwroot 静态文件时用）
#
# 两个"起网页"的入口已经统一到这里：`tools\play-web.ps1`（1.12.1 里那个独立脚本）
# 现在只是一层转发的兼容别名，行为与本脚本的 web 那条路逐字一致。为什么不留两份：
# 两边的默认行为已经开始分叉（一份复用已在跑的宿主、一份先杀掉再重起；一份开浏览器、
# 一份不开；日志落点也不同），而"起网页"只该有一种说法。
#
# ★ 为什么默认是"网页"而不是终端（2026-10-06 的决定；理由全是代码里的既成事实）：
#
#   两个宿主的**默认存档是同一个文件**：
#     · Web：`games/hosts/Web/Program.cs:70` 的 `--save-root` 默认值来自 `DefaultSaveRoot()`
#       （`Program.cs:689~693`：仓库根下的 saves/），`GameHost.cs:123~128` 拿它建 `FileStorage`，
#       键是 `<包 id>.json`；
#     · 终端：`games/hosts/Demo.Cli/CliOptions.cs:65` 与 `237~240` 默认 `saves\<包 id>.json`
#       （**相对路径**，而本脚本开头 Set-Location 到仓库根 ⇒ 落到同一个 saves\ 目录）。
#
#   而 `FileStorage.Write`（`engine/core/Persistence/IStorage.cs:95~132`）做的是**原子替换**：
#   它保证的是"存档不会写坏"（临时文件 → 验读 → `File.Replace` + 留 `.bak`），
#   **不是**"两个进程不会互相覆盖"——两个宿主各自按**自己内存里**的状态，
#   每 60 秒（`engine/core/Content/GameBalance.cs:34` 的 `AutoSaveInterval = 60`）整份重写一次。
#
#   ⇒ 同时跑两个宿主，结果就是**最后写的那一份赢**：在网页里攒的进度会被终端那一份静默回退
#      （不会报错、不会有提示：两个宿主写的都是"合法且读得回来"的存档，只是一份更旧）。
#      退出其中一个宿主时还会再存一次（`Program.cs:114~115` 的 ApplicationStopping → StopAll），
#      于是连"谁最后说话"都取决于谁先退出。
#
#   所以本脚本**不静默地起第二个宿主**：默认就是网页那一侧；要终端就用 `play`，
#   而 `play` 在检测到**本仓库还有别的 Web 宿主**时**拒绝启动**（盘踞在哪个端口上都算）
#   ——除非显式 `-SaveRoot <另一份存档>`，或明确接受互相覆盖的 `-AllowSharedSave`。
#   网页那条路同理：端口空闲、但本仓库别的端口上还有宿主、又没给 -SaveRoot 时，
#   它拒绝再起一个（它看不到那个宿主的存档根，而默认两边是同一份）。
#   代价写在提示里，不藏在代码里。
#
# 环境变量:
#   NEKO_PORT        默认端口（默认 5273；命令行 -Port 优先）
#   NEKO_NO_PAUSE    设了就绝不等回车（CI / 自动化用；非交互时本来就不等）
#   NEKO_DEBUG_KEY / NEKO_URL_KEY  **原样继承**：调试门 / 令牌门就是靠它们开的
#                    （`$env:NEKO_DEBUG_KEY='...'; start.cmd web` 是文档里的用法；
#                    上游的 play-web.ps1 会删掉它们，统一后**不删**）
#
# ⚠ 给调用方的一条硬约束（实测踩过，不是风格问题）：别把本脚本的输出接进**管道**。
#   `start.cmd` / 双击 / 直接在控制台里跑都没问题；但 `powershell -File tools\start.ps1 web | ...`
#   会让那根管道一直不关：Windows 建进程时带着句柄继承，而分离启动的那个长命宿主会**继承
#   调用方的 stdout 句柄**，于是"读到 EOF"要等到宿主退出才发生——调用方就一直挂着。
#   要抓它的输出就重定向进**文件**（`tools/api-test.ps1` 的 Invoke-Launcher 正是这么做的，
#   连退出码一起：`cmd /c "... > out.txt 2> err.txt"`）。

param(
    [Parameter(Position = 0)][string]$Mode = 'web',
    [Parameter(Position = 1)][string]$Pack = '',
    [int]$Port = 0,
    [string]$SaveRoot = '',
    [switch]$NoBrowser,
    [switch]$NoBuild,
    [switch]$AllowSharedSave
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$defaultPort = 5273

function Say([string]$m) { Write-Host $m }

# 只在"真的有人在看"的时候才等回车：非交互（CI、被重定向的 stdin、api-test 起的子进程）下
# Read-Host 会把整条命令**挂住**——而"报错退出"与"卡住不动"是完全两件事。
# 双重判据：stdin 被重定向时不等；NEKO_NO_PAUSE 设了也不等（自动化那条路上两条都成立）。
function Wait-IfInteractive([string]$Message) {
    if ($env:NEKO_NO_PAUSE) { return }
    if ([Console]::IsInputRedirected) { return }
    try { Read-Host $Message } catch { }
}

# 失败 = 说清原因 + **非 0 退出**。
function Fail([string]$m) {
    Write-Host ''
    Write-Host $m -ForegroundColor Red
    Write-Host ''
    Wait-IfInteractive '按回车关闭'
    exit 1
}

# 停掉一个宿主进程（先 `taskkill /T` 求"连子树"，被拒就 Stop-Process 兜底）。
# 两处细节都是实测的：
#   · taskkill 的 stderr 用 **2>&1**（不是 2>$null）：5.1 在 $ErrorActionPreference='Stop' 下
#     仍会把原生进程的 stderr 变成 NativeCommandError —— 实测 `taskkill … 2>$null` 直接终止脚本，
#     连"没停下来"那句都不打印；合流到 stdout 再 Out-Null 才只是字符串。
#   · 本沙箱里 taskkill 报 Access denied，而 Stop-Process 能成功 —— 所以必须有兜底，
#     而"到底停没停"最终一律以**端口是否松手**为准，不信任何一条命令的退出码。
function Stop-HostTree([int]$ProcessId) {
    if ($ProcessId -le 0) { return }
    $failed = $false
    try {
        & taskkill /PID $ProcessId /T /F 2>&1 | Out-Null
        $failed = ($LASTEXITCODE -ne 0)
    }
    catch { $failed = $true }
    if ($failed) {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction Stop } catch { }
    }
}

# 端口上监听者的 PID（0 = 没人听）。
# 用 netstat 而不是 Get-NetTCPConnection：后者在这个受限沙箱里实测拿不到东西
# （tools/build.ps1:73~74 记过同一条判据）。而"端口上到底有没有人"是这条路上唯一的**安全**判据
# ——它决定我们是复用、还是报错退出，永远不决定"要不要杀掉别人"。
function Get-ListenerPid([int]$Port) {
    $pattern = '^\s*TCP\s+\S+:' + $Port + '\s+\S+\s+LISTENING\s+(\d+)\s*$'
    foreach ($line in @(netstat -ano -p TCP 2>$null)) {
        $match = [regex]::Match([string]$line, $pattern)
        if ($match.Success) { return [int]$match.Groups[1].Value }
    }
    return 0
}

# PID → 它正在监听的端口。一次 netstat 把整张表读进来，后面查"谁在服务"都从这里读。
# 只用来**认人**（报错 / 保留 / 停宿主），永远不用来决定"要不要杀掉别人"。
function Get-ListenerMap {
    $map = @{}
    foreach ($line in @(netstat -ano -p TCP 2>$null)) {
        $match = [regex]::Match([string]$line, '^\s*TCP\s+\S+:(\d+)\s+\S+\s+LISTENING\s+(\d+)\s*$')
        if (-not $match.Success) { continue }
        $listenPort = [int]$match.Groups[1].Value
        $listenPid = [int]$match.Groups[2].Value
        if (-not $map.ContainsKey($listenPid)) {
            $map[$listenPid] = New-Object System.Collections.Generic.List[int]
        }
        if (-not $map[$listenPid].Contains($listenPort)) { $map[$listenPid].Add($listenPort) }
    }
    return $map
}

# 端口上是不是一个 **NekoClicker Web 宿主**：问 /api/ping，正文里带 `"app":"neko-clicker-web"` 才算。
# 判据必须问"是不是我们的"，不能只看"端口通不通"——通不通只说明有人在听，
# 而"是不是我们的宿主"决定下一步是**复用它**还是**报错退出**。两件事都不许猜。
function Test-NekoWebHost([int]$Port) {
    try {
        $response = Invoke-WebRequest -Uri ('http://127.0.0.1:{0}/api/ping' -f $Port) -UseBasicParsing -TimeoutSec 2
        return ([string]$response.Content).Contains('"app":"neko-clicker-web"')
    }
    catch { return $false }
}

# 本仓库的 Web 宿主进程（映像路径以仓库根开头；判据与 tools/build.ps1 的"谁占着 bin"同源）。
# 按路径收窄、不按进程名收网：别的检出、别人的程序都不该被这条判据碰。
function Get-RepoWebHosts {
    $found = @()
    foreach ($proc in @(Get-Process -Name 'neko-clicker-web' -ErrorAction SilentlyContinue)) {
        try {
            if ($proc.Path -and $proc.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { $found += $proc }
        }
        catch { }
    }
    return $found
}

# 把宿主列表变成一句话：`PID 1234 @ 5300`；还没开始监听（正在起 / 正在失败）的端口写 `?`。
function Describe-Hosts([object[]]$Hosts, $ListenerMap) {
    $parts = @()
    foreach ($entry in @($Hosts)) {
        $ports = if ($ListenerMap.ContainsKey($entry.Id)) { ($ListenerMap[$entry.Id] -join ',') } else { '?' }
        $parts += ('PID {0} @ {1}' -f $entry.Id, $ports)
    }
    return ($parts -join '；')
}

# 失败时把日志尾部摊出来：宿主起不来的原因（端口被抢、产物缺失、运行时缺失）就在那几行里。
function Show-LogTail([string]$Path, [string]$Label, [int]$Lines = 20) {
    if (-not (Test-Path $Path)) { return }
    $tail = @(Get-Content -Path $Path -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -Last $Lines)
    if ($tail.Count -eq 0) { return }
    Write-Host ('  {0}（最后 {1} 行）:' -f $Label, $tail.Count) -ForegroundColor DarkYellow
    foreach ($line in $tail) { Write-Host "    $line" }
}

# 开浏览器。**只在确认过 /api/ping 之后调用**：指向一个还没开始服务的端口是这条路上最明显的失败。
function Open-Page([string]$Url) {
    if ($NoBrowser) {
        Write-Host "（-NoBrowser：没有开浏览器）地址：$Url" -ForegroundColor DarkGray
        return
    }
    try {
        Start-Process $Url
        Say "已在默认浏览器里打开：$Url"
    }
    catch {
        # 开不了浏览器**不算启动失败**：宿主已经在服务了，把地址粘进浏览器就行。
        Write-Host "开不了默认浏览器（$($_.Exception.Message)）——把上面那个地址粘进浏览器。" -ForegroundColor Yellow
    }
}

# 清掉会锁住 dll 的残留宿主（**只清本仓库里的**，不动你别的程序）。
# ★ 正在**服务**的进程一律保留：它是某个端口上活着的宿主，不是你上次忘退的残骸
#   （上游 play-web.ps1 的教训同源：按名字清场会把 5273 上真人正在玩的那个一起带走）。
#   它们若锁着这次要构建的那个项目的 dll，构建会报 MSB3021 —— 这时把 PID 与停它的命令
#   一起印出来，由人来决定，脚本不替你杀。
function Clear-StaleHosts {
    $map = Get-ListenerMap
    $mine = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })
    $serving = @($mine | Where-Object { $map.ContainsKey($_.Id) })
    $stale = @($mine | Where-Object { -not $map.ContainsKey($_.Id) })

    if ($serving.Count -gt 0) {
        Say ('保留 {0} 个正在服务的进程（杀它等于把你开着的页面带走）：' -f $serving.Count)
        foreach ($p in $serving) {
            Say ('  - {0} (PID {1}，端口 {2})' -f $p.ProcessName, $p.Id, ($map[$p.Id] -join ','))
        }
        Say '  它们若锁着这次要构建的 dll，构建会报 MSB3021：先停它 —— taskkill /PID <PID> /T /F'
    }

    if ($stale.Count -gt 0) {
        Say ('发现 {0} 个本仓库的残留进程，先清掉（否则构建会因文件占用失败）：' -f $stale.Count)
        foreach ($p in $stale) {
            Say ('  - {0} (PID {1})' -f $p.ProcessName, $p.Id)
            try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
}

Write-Host ''
Write-Host '=== NekoClicker 启动器 ===' -ForegroundColor Cyan
Say "仓库：$root"

# ---- 0. 装没装 .NET ----------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail '找不到 dotnet。需要 .NET 8 SDK：https://dotnet.microsoft.com/download/dotnet/8.0'
}
Say ('dotnet {0}' -f (& dotnet --version))

# ---- 0.5 端口与模式 ----------------------------------------------------------
# ⚠ 局部变量**不能**叫 `$port`：PowerShell 的变量名不区分大小写，`$port` 与参数 `$Port`
#   是**同一个变量**——那样 `-Port 5300` 会被下面第一行悄悄覆盖回 5273，而脚本一声不吭
#   （实测踩过：api-test 的启动器那一段逮到的就是它）。所以这里一律写成 `$targetPort`。
$targetPort = $defaultPort
if ($env:NEKO_PORT) {
    $parsedPort = 0
    if ([int]::TryParse($env:NEKO_PORT, [ref]$parsedPort) -and $parsedPort -gt 0 -and $parsedPort -lt 65536) {
        $targetPort = $parsedPort
    }
    else { Fail "NEKO_PORT 不是合法端口号：<$($env:NEKO_PORT)>" }
}
if ($Port -gt 0) { $targetPort = $Port }

# 同理：`$mode` 与参数 `$Mode` 也是同一个变量（上面那条注释的同一个坑），所以用小写前先改名。
$modeName = $Mode.ToLowerInvariant()
if (@('web', 'play', 'list', 'stop') -notcontains $modeName) {
    # 以前未知的模式会**静默**落进终端界面（`start.cmd lab` 想选包，结果起了默认包）。
    # 这里明说：模式只有四个，而选包要写在模式后面。
    Fail ("未知模式：<$Mode>。可用：web（默认，网页那一侧）/ play（终端界面）/ list（列内容包）/ stop（停掉这个端口上的 Web 宿主）。`n" +
          "要选内容包就写在模式后面：start.cmd play lab ｜ start.cmd web company。")
}

# 包名与两份存档路径在这里定死一次，后面 web / play / stop 都读它。
$packId = if ($Pack) { $Pack } else { 'neko' }
$repoSave = Join-Path (Join-Path $root 'saves') "$packId.json"
# -SaveRoot：相对路径按**仓库根**解析（不是按宿主的 cwd —— 宿主是起在项目目录里的）。
# 上游 play-web.ps1 用的是 Path.GetFullPath，这里照做：显式给了什么，宿主就写哪里。
if ($SaveRoot) {
    $resolvedSaveRoot = if ([System.IO.Path]::IsPathRooted($SaveRoot)) {
        [System.IO.Path]::GetFullPath($SaveRoot)
    }
    else { [System.IO.Path]::GetFullPath((Join-Path $root $SaveRoot)) }
}
else { $resolvedSaveRoot = Join-Path $root 'saves' }
$terminalSave = Join-Path $resolvedSaveRoot "$packId.json"

if ($modeName -eq 'list') {
    Write-Host ''
    Say '内容包：'
    foreach ($id in @('neko', 'cafe', 'ninelines', 'lab', 'company', 'apocalypse', 'library', 'god', 'civ', 'cyber', 'dream')) {
        Say ('  {0,-12}' -f $id)
    }
    Say ''
    Say '用法：start.cmd              （网页 + 默认内容包）'
    Say '      start.cmd web company  （网页 + 公司包）'
    Say '      start.cmd play lab     （终端 + 实验室包）'
    Say '      start.cmd stop         （停掉 5273 上的 Web 宿主）'
    Write-Host ''
    Wait-IfInteractive '按回车关闭'
    exit 0
}

# 端口上现在是谁、本仓库还有多少个宿主 —— **先查再动手**：
# 这条判据同时决定"复用 / 报错 / 拒绝"，而**从不**决定"要不要杀掉别人"。
$listenerMap = Get-ListenerMap
$holderPid = Get-ListenerPid -Port $targetPort
$holderIsOurs = $false
if ($holderPid -gt 0) { $holderIsOurs = Test-NekoWebHost -Port $targetPort }
$repoHosts = @(Get-RepoWebHosts)

# ---- stop：只停**这个端口上的、我们的**宿主 ------------------------------------
# 为什么不做 `-StopAll`：按进程名清场会顺手带走 5273 上真人正在玩的那个（上游 play-web.ps1
# 自己的注释里就记着这次事故）。按端口停是唯一有边界的说法；要清场就一个端口一个端口停。
if ($modeName -eq 'stop') {
    if ($holderPid -eq 0) {
        Say "端口 $targetPort 上没有监听者，不用停。"
        exit 0
    }
    if (-not $holderIsOurs) {
        Fail ("端口 $targetPort 被别的程序占着（PID $holderPid），而它不是一个 NekoClicker Web 宿主。`n" +
              "这里刻意不替你动它：先认人，能等就别杀。")
    }

    Say ('停止端口 {0} 上的宿主：taskkill /PID {1} /T /F' -f $targetPort, $holderPid)
    Stop-HostTree $holderPid

    $gone = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ((Get-ListenerPid -Port $targetPort) -eq 0) { $gone = $true; break }
    }
    if (-not $gone) {
        Fail ("没停下来：端口 {0} 上还有监听者（PID {1}）。" -f $targetPort, (Get-ListenerPid -Port $targetPort))
    }
    Say '已停止。这条是强杀：宿主的 ApplicationStopping 不会跑，最多丢 60 秒的进度。'
    exit 0
}

if ($modeName -eq 'web') {
    # 端口与包名都由这里决定，然后**拼进 URL** —— Web 端的包选择走查询串
    # `?package=<id>`，不是命令行参数。原先把 --package 传给 web.ps1 是个静默失败：
    # 那个脚本不解析它，于是包选择被悄悄丢掉，玩家只会看到默认包。
    $base = "http://127.0.0.1:$targetPort/"
    $url = if ($Pack) { $base + "?package=$Pack" } else { $base }

    # ---- ① 端口上已经有一个**我们的**宿主：复用它，绝不起第二个 ------------------
    # 这一段就是"再点一次 start.cmd"的路径，它必须幂等。它**不构建、不清残留、不杀任何进程**：
    # 文件头那段"存档互写"的注释就是两个宿主同时跑的代价，而这里连构建都不做，
    # 因为构建前那一步会清掉"本仓库的残留进程"——那会把这个宿主连同玩家的网页一起带走。
    if ($holderPid -gt 0 -and $holderIsOurs) {
        Write-Host ''
        Write-Host "端口 $targetPort 上已经有一个 NekoClicker Web 宿主（PID $holderPid）——复用它，没有再起第二个。" -ForegroundColor Yellow
        Write-Host "浏览器地址：$url" -ForegroundColor Yellow
        Open-Page $url
        Write-Host ''
        exit 0
    }

    # ---- ② 端口上有人，但不是我们的宿主：点名报错，不猜也不杀 --------------------
    if ($holderPid -gt 0) {
        Fail ("端口 $targetPort 被别的程序占着（PID $holderPid），而它不是一个 NekoClicker Web 宿主`n" +
              "（GET http://127.0.0.1:$targetPort/api/ping 没有回 `"app`":`"neko-clicker-web`" 那一段）。`n" +
              "这里刻意不替你动它：先认人，能等就别杀。`n" +
              "换一个端口：start.cmd web -Port 5300（或设环境变量 NEKO_PORT）。")
    }

    # ---- ③ 端口空闲，但别的端口上还有本仓库的宿主：保守拒绝 ----------------------
    # 判据只能保守：宿主没有"报出自己存档根"的接口（/api/ping 不说），所以"它写哪份存档"
    # 我们看不到。而默认情况下两边都是 <仓库>\saves\<包>.json —— 那正好是"后写的赢"。
    # 给了 -SaveRoot = 显式声明这一局是隔离的；-AllowSharedSave = 明知会互相覆盖还要开。
    $otherHosts = @($repoHosts | Where-Object { $_.Id -ne $holderPid })
    if ($otherHosts.Count -gt 0 -and -not $SaveRoot -and -not $AllowSharedSave) {
        Fail ("本仓库还有 $($otherHosts.Count) 个 Web 宿主在跑（$(Describe-Hosts $otherHosts $listenerMap)），而这次没给 -SaveRoot。`n" +
              "它们和你这次要起的宿主默认写的是同一份存档：`n" +
              "  $repoSave`n" +
              "两个宿主每 60 秒各自按**自己内存里**的状态整份重写 ⇒ 后写的赢，另一边的进度会被静默回退。`n" +
              "三条出路（给一条就行）：`n" +
              "  · 先停掉它们：taskkill /PID <上面那个 PID> /T /F（或 start.cmd stop -Port <端口>）；`n" +
              "  · 这次换一份存档：start.cmd web -SaveRoot <目录>；`n" +
              "  · 明确接受互相覆盖：start.cmd web -AllowSharedSave。")
    }

    # ---- ③.5 -NoBuild 时产物必须先存在：否则失败形态是 30 秒超时，而不是一句话 ----
    $webDll = Join-Path $root 'games\hosts\Web\bin\Debug\net8.0\neko-clicker-web.dll'
    if ($NoBuild -and -not (Test-Path $webDll)) {
        Fail ("-NoBuild：没有找到已构建的 Web 宿主（$webDll）。`n" +
              "先构建一次（去掉 -NoBuild 再跑），或手工：pwsh -File tools\dnet.ps1 build games/hosts/Web/NekoClicker.Web.sln")
    }

    # ---- ④ 端口空闲：构建 + **分离启动** + 等它就绪 + 开浏览器 --------------------
    if (-not $NoBuild) {
        Write-Host ''
        Say '=== 构建 ==='
        Clear-StaleHosts

        & "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { Fail '构建失败（上面红色的 error 行就是原因）。' }

        & "$PSScriptRoot\dnet.ps1" build "$root\games\hosts\Web\NekoClicker.Web.sln" -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { Fail 'Web 宿主构建失败（上面红色的 error 行就是原因）。' }
    }

    # 宿主进程的环境（上游 play-web.ps1 用实测换来的三条，统一后照收）：
    #   · DOTNET_ROLL_FORWARD=Major —— 机器上只有更高的运行时（net8 应用默认只前滚 Minor）时也能起来；
    #   · Logging__EventLog__LogLevel__Default=None —— EventLog provider 写事件日志被拒时，
    #     **每写一条日志就把宿主打挂**（实测：一句无害的 StaticFiles 警告 → 进程退出）；
    #   · ASPNETCORE_CONTENTROOT —— 内容根指到源码目录：开发期 bin 旁边没有 wwwroot，
    #     不指过去就只有 /api/* 通、首页 404。走 `dotnet run` 时工作目录本来就是项目目录，
    #     但把它写死就不必依赖"dotnet run 的行为"。
    $env:DOTNET_ROLL_FORWARD = 'Major'
    $env:Logging__EventLog__LogLevel__Default = 'None'
    $env:ASPNETCORE_CONTENTROOT = Join-Path $root 'games\hosts\Web'

    # 日志与埋点**按端口分开**（沿用上游 play-web.ps1 的规矩）：
    #   · 5273 是真人那条线：日志 artifacts\host-*.log、埋点 artifacts\latency.txt 一个字节都不挪；
    #   · 其它端口是实验局：全部落到 artifacts\play-<端口>\，实验的会话行不会混进真人样本文件。
    $artifacts = Join-Path $root 'artifacts'
    $logDir = if ($targetPort -eq $defaultPort) { $artifacts } else { Join-Path $artifacts "play-$targetPort" }
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $outLog = Join-Path $logDir 'host-out.log'
    $errLog = Join-Path $logDir 'host-err.log'
    $latencyLog = if ($targetPort -eq $defaultPort) {
        Join-Path $artifacts 'latency.txt'
    }
    else { Join-Path $logDir 'latency.txt' }

    # 分离启动：本脚本退出之后宿主**要活着**（它就是"关掉浏览器也算挂机"的那条命）。
    # 为什么绕一层 `pwsh -File tools/dnet.ps1`（而不是裸 Start-Process dotnet）：
    # dnet.ps1 里那套环境重定向（DOTNET_CLI_HOME / NUGET_PACKAGES 落在仓库内、MSBuild -m:1）
    # 是受限沙箱下"跑得起来"的前提，而**裸 Start-Process 起的进程是另一套环境**——
    # 实测那样起的宿主活不下来。没有 pwsh 就退回 Windows PowerShell（dnet.ps1 本来就兼容 5.1）。
    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }

    # 路径用**正斜杠**：这些参数要原样交给原生进程（pwsh / dotnet），而 PowerShell 的提供程序
    # 只在**自己的 cmdlet** 上把 `\` 归一化——传出去的那一份不会被归一化（登记册 W16）。
    $hostArgs = @(
        '-NoProfile', '-File', 'tools/dnet.ps1', 'run', '--no-build',
        '--project', 'games/hosts/Web', '--', '--urls', "http://127.0.0.1:$targetPort",
        '--save-root', ($resolvedSaveRoot -replace '\\', '/'),
        '--latency-log', ($latencyLog -replace '\\', '/')
    )

    Write-Host ''
    Say '=== 启动 Web 宿主（后台）==='
    $launcher = Start-Process -FilePath $shell -ArgumentList $hostArgs -WorkingDirectory $root `
        -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru

    # 等它**真的开始服务**再开浏览器：60 次 × 500ms（≈30 秒；每次探测另有最多 2 秒超时）。
    # 判据与上面识别宿主时**同一条**（/api/ping 回了我们那一段），不是"端口通了"。
    Say ('等它开始服务（最多约 30 秒）…… 启动包装进程 PID {0}' -f $launcher.Id)
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        if ($launcher.HasExited) { break }
        if (Test-NekoWebHost -Port $targetPort) { $ready = $true; break }
    }

    if (-not $ready) {
        # 收掉这次**自己**起的那个东西：它可能已经起了 apphost（只是还没开始服务），
        # 所以先按端口认人、再连同包装进程一起收 —— 起失败不该留下一个没人认领的孤儿。
        $failedPid = Get-ListenerPid -Port $targetPort
        if ($failedPid -gt 0) { Stop-HostTree $failedPid }
        Stop-HostTree $launcher.Id

        Write-Host ''
        Write-Host "宿主没有在 30 秒内开始服务：http://127.0.0.1:$targetPort/api/ping 没回话。" -ForegroundColor Red
        Show-LogTail $outLog '宿主 stdout'
        Show-LogTail $errLog '宿主 stderr'
        Fail ("没能在端口 $targetPort 上把 Web 宿主起起来。`n" +
              "本次自己起的那个进程树已经收掉了；上面几行通常就是原因（端口被抢、产物缺失、运行时缺失）。`n" +
              "手工复现：pwsh -File tools/dnet.ps1 run --project games/hosts/Web -- --urls http://127.0.0.1:$targetPort")
    }

    $livePid = Get-ListenerPid -Port $targetPort
    if ($livePid -le 0) { $livePid = $launcher.Id }

    Write-Host ''
    Write-Host "宿主已就绪：$url" -ForegroundColor Green
    Open-Page $url
    Write-Host ''
    # 停哪一条：真正在服务的是**监听端口的那一个**，不是启动器（它下面还有 dnet.ps1 → dotnet run
    # → apphost 两层）。上一版把包装进程的 PID 印成"停止：taskkill …"，那个进程退出之后就指向
    # 一个不存在的 PID 了——这里改成按端口认出来的那个，包装进程单独注明。
    Say ('停止：taskkill /PID {0} /T /F（实际在服务的就是它；也可以 start.cmd stop -Port {1}）' -f $livePid, $targetPort)
    Say ('启动包装进程 PID {0}（不用记：宿主走了它会自己退）' -f $launcher.Id)
    Say "存档：$resolvedSaveRoot"
    Say "日志：$outLog ｜ $errLog"
    Say "埋点：$latencyLog"
    Write-Host ''
    exit 0
}

# ---- 终端界面 ----------------------------------------------------------------
# 终端与网页写的是同一份存档（见文件头那段），所以"本仓库还有 Web 宿主在跑"时默认**拒绝**
# 启动终端：拒绝比"跑起来之后悄悄把玩家在网页里的进度覆盖掉"便宜得多。
# 判据是**所有端口**上的本仓库宿主，不只是这次指定的端口——宿主换个端口照样写同一份存档。
$conflictHosts = @($repoHosts)

if ($conflictHosts.Count -gt 0 -and -not $AllowSharedSave -and -not $SaveRoot) {
    Fail ("本仓库还有 $($conflictHosts.Count) 个 Web 宿主在跑（$(Describe-Hosts $conflictHosts $listenerMap)），而终端这次写的是默认存档：`n" +
          "  $repoSave`n" +
          "它们的存档根我们看不到（没有接口），而默认情况下两边就是同一份。两个宿主各自按**自己内存里**的状态`n" +
          "每 60 秒整份重写一次 ⇒ 后写的那份赢，另一边的进度会被**静默回退**（FileStorage 的原子替换只保证`n" +
          "文件不会写坏，不保证两个进程不互相覆盖）。`n" +
          "三条出路：`n" +
          "  · 先停掉它们：taskkill /PID <上面那个 PID> /T /F（或 start.cmd stop -Port <端口>）；`n" +
          "  · 让终端用另一份存档：start.cmd play -SaveRoot <目录>（两边进度从此各走各的）；`n" +
          "  · 明确接受互相覆盖：start.cmd play -AllowSharedSave。")
}

if ($conflictHosts.Count -gt 0) {
    Write-Host ('注意：本仓库还有 {0} 个 Web 宿主在跑（{1}）。' -f $conflictHosts.Count, (Describe-Hosts $conflictHosts $listenerMap)) -ForegroundColor Yellow
    if ($SaveRoot) {
        Write-Host ("      终端这次写 {0}（你显式给了 -SaveRoot），两边各玩各的进度。" -f $terminalSave) -ForegroundColor Yellow
    }
    else {
        Write-Host '      你显式接受了 -AllowSharedSave：两边都会整份重写同一份存档，进度会互相覆盖。' -ForegroundColor Yellow
    }
}

Write-Host ''
Write-Host '=== 启动终端界面 ===' -ForegroundColor Cyan
Say '操作：Tab 切面板｜数字键买建筑｜Enter 购买｜A 舍命/转生｜? 帮助｜Q 退出'
Write-Host ''

$dll = Join-Path $root 'games\hosts\Demo.Cli\bin\Debug\net8.0\neko-clicker.dll'

if (-not $NoBuild) {
    Write-Host '=== 构建 ===' -ForegroundColor Cyan
    Clear-StaleHosts

    & "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { Fail '构建失败（上面红色的 error 行就是原因）。' }
}

$demoArgs = @()
if ($Pack) { $demoArgs += @('--package', $Pack) }
# 存档路径显式传给终端：解析规则与网页那条路**同一条**（相对路径按仓库根、-SaveRoot 换根），
# 于是"两个宿主写的是哪一份"不再取决于两个程序各自的默认值。
$demoArgs += @('--save', $terminalSave)

# 用 dotnet exec 直接跑已构建好的 dll：不经过 `dotnet run`，少一层进程，
# 而且 Ctrl+C 直接作用在游戏上（run 会多一个中间进程，退出时容易留孤儿）。
& dotnet exec $dll @demoArgs
exit $LASTEXITCODE
