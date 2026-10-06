# start.ps1 -- 一体化启动。构建 + 起宿主，把已知的坑都堵掉。
#
# 为什么要有它（三个坑都是实测踩过的）：
#   1) `tools/web.ps1` 不带动词只做 build 就退出 —— 默认动词就是 build，
#      看起来像"启动不了"。这里把动词写死，不给你踩的机会。
#   2) 上次没退干净的宿主进程会占着 bin 里的 dll，构建报一堆
#      MSB3021 "being used by another process"。这里先清掉**本仓库自己的**残留进程
#      —— 只在真的要构建时才清（见 Clear-StaleHosts：它们的害处只与构建有关）。
#   3) 执行策略会拦 .ps1。所以入口是 start.cmd（它用 -ExecutionPolicy Bypass 调用本脚本）；
#      你也可以直接用 `powershell -ExecutionPolicy Bypass -File tools\start.ps1`。
#
# 用法:
#   start.cmd                      网页那一侧（**默认**）：起 Web 宿主 + 自动开浏览器
#   start.cmd web [内容包]          同上，把动词写全
#   start.cmd play [内容包]         终端全屏界面（**不**起网页）
#   start.cmd list                 列出全部内容包
#   start.cmd web -NoBrowser       起宿主 / 复用已有的宿主，但**不开**浏览器
#   start.cmd web -Port 5300       换端口（等价于设环境变量 NEKO_PORT）
#   start.cmd web -SaveRoot <目录>  换存档根目录（默认 <仓库根>\saves）
#   start.cmd play -SaveRoot <目录> 终端改用另一份存档（与网页各玩各的进度）
#   start.cmd play -AllowSharedSave 明知两个宿主会互相覆盖，也要同时跑
#   start.cmd web -NoBuild         跳过构建（改的只是 wwwroot 静态文件时用）
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
#   所以本脚本**从不起第二个宿主**：默认就是网页那一侧；要终端就用 `play`，
#   而 `play` 在检测到同一端口上的 Web 宿主时**拒绝启动**（除非显式
#   `-SaveRoot <另一份存档>`，或明确接受互相覆盖的 `-AllowSharedSave`）。
#   代价写在提示里，不藏在代码里。
#
# 环境变量:
#   NEKO_PORT        默认端口（默认 5273；命令行 -Port 优先）
#   NEKO_NO_PAUSE    设了就绝不等回车（CI / 自动化用；非交互时本来就不等）
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
# 只在真的要构建时调用：这些进程的害处是占着 bin 里的 dll，而"复用已在跑的宿主"那条路
# 既不构建、也不该杀它——那会把玩家正开着的网页一起带走。
function Clear-StaleHosts {
    $stale = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) })

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
if ($modeName -ne 'web' -and $modeName -ne 'play' -and $modeName -ne 'list') {
    # 以前未知的模式会**静默**落进终端界面（`start.cmd lab` 想选包，结果起了默认包）。
    # 这里明说：模式只有三个，而选包要写在模式后面。
    Fail ("未知模式：<$Mode>。可用：web（默认，网页那一侧）/ play（终端界面）/ list（列内容包）。`n" +
          "要选内容包就写在模式后面：start.cmd play lab ｜ start.cmd web company。")
}

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
    Write-Host ''
    Wait-IfInteractive '按回车关闭'
    exit 0
}

# 端口上现在是谁 —— **先查再动手**：这条判据同时决定"复用 / 报错"与"要不要拒绝终端"。
$holderPid = Get-ListenerPid -Port $targetPort
$holderIsOurs = $false
if ($holderPid -gt 0) { $holderIsOurs = Test-NekoWebHost -Port $targetPort }

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

    # ---- ③ 端口空闲：构建 + **分离启动** + 等它就绪 + 开浏览器 --------------------
    if (-not $NoBuild) {
        Write-Host ''
        Say '=== 构建 ==='
        Clear-StaleHosts

        & "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { Fail '构建失败（上面红色的 error 行就是原因）。' }

        & "$PSScriptRoot\dnet.ps1" build "$root\games\hosts\Web\NekoClicker.Web.sln" -v q --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { Fail 'Web 宿主构建失败（上面红色的 error 行就是原因）。' }
    }

    $artifacts = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    $outLog = Join-Path $artifacts 'host-out.log'
    $errLog = Join-Path $artifacts 'host-err.log'

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
        '--project', 'games/hosts/Web', '--', '--urls', "http://127.0.0.1:$targetPort"
    )
    if ($SaveRoot) { $hostArgs += @('--save-root', ($SaveRoot -replace '\\', '/')) }

    Write-Host ''
    Say '=== 启动 Web 宿主（后台）==='
    $launcher = Start-Process -FilePath $shell -ArgumentList $hostArgs -WorkingDirectory $root `
        -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru

    # 等它**真的开始服务**再开浏览器：60 次 × 500ms（≈30 秒；每次探测另有最多 2 秒超时）。
    # 判据与上面识别宿主时**同一条**（/api/ping 回了我们那一段），不是"端口通了"。
    Say ('等它开始服务（最多约 30 秒）…… 进程 PID {0}' -f $launcher.Id)
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        if ($launcher.HasExited) { break }
        if (Test-NekoWebHost -Port $targetPort) { $ready = $true; break }
    }

    if (-not $ready) {
        # 收掉这次**自己**起的进程树（连同 dotnet run 与它的 apphost 子进程）：
        # 起失败不该留下一个没人认领的孤儿；按树杀才杀得干净。
        & taskkill /PID $launcher.Id /T /F 2>$null | Out-Null

        Write-Host ''
        Write-Host "宿主没有在 30 秒内开始服务：http://127.0.0.1:$targetPort/api/ping 没回话。" -ForegroundColor Red
        Show-LogTail $outLog '宿主 stdout'
        Show-LogTail $errLog '宿主 stderr'
        Fail ("没能在端口 $targetPort 上把 Web 宿主起起来。`n" +
              "本次自己起的那个进程树已经收掉了；上面几行通常就是原因（端口被抢、产物缺失、运行时缺失）。`n" +
              "手工复现：pwsh -File tools/dnet.ps1 run --project games/hosts/Web -- --urls http://127.0.0.1:$targetPort")
    }

    Write-Host ''
    Write-Host "宿主已就绪：$url" -ForegroundColor Green
    Open-Page $url
    Write-Host ''
    Say ('停止：taskkill /PID {0} /T /F' -f $launcher.Id)
    Say "日志：$outLog ｜ $errLog"
    Write-Host ''
    exit 0
}

# ---- 终端界面 ----------------------------------------------------------------
# 终端与网页写的是同一份存档（见文件头那段），所以"网页正在跑"时默认**拒绝**启动终端：
# 拒绝比"跑起来之后悄悄把玩家在网页里的进度覆盖掉"便宜得多。
$packId = if ($Pack) { $Pack } else { 'neko' }
$repoSave = Join-Path (Join-Path $root 'saves') "$packId.json"
$terminalSave = if ($SaveRoot) { Join-Path $SaveRoot "$packId.json" } else { $repoSave }

if ($holderPid -gt 0 -and $holderIsOurs -and -not $AllowSharedSave -and -not $SaveRoot) {
    Fail ("端口 $targetPort 上有一个 NekoClicker Web 宿主正在跑（PID $holderPid），而终端与它写的是同一份存档：`n" +
          "  $repoSave`n" +
          "两个宿主各自按**自己内存里**的状态每 60 秒整份重写一次 ⇒ 后写的那份赢，`n" +
          "另一边的进度会被**静默回退**（FileStorage 的原子替换只保证文件不会写坏，" +
          "不保证两个进程不互相覆盖）。`n" +
          "三条出路：`n" +
          "  · 先停掉那个宿主：taskkill /PID $holderPid /T /F（推荐——它就是你现在开着的那个网页）；`n" +
          "  · 让终端用另一份存档：start.cmd play -SaveRoot <目录>（两边进度从此各走各的）；`n" +
          "  · 明确接受互相覆盖：start.cmd play -AllowSharedSave。")
}

if ($holderPid -gt 0 -and $holderIsOurs) {
    Write-Host ('注意：端口 {0} 上还有一个 NekoClicker Web 宿主（PID {1}）在跑。' -f $targetPort, $holderPid) -ForegroundColor Yellow
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
# -SaveRoot：终端改用另一份存档（`--save <目录>\<包 id>.json`）。
# 不给就用 CLI 自己的默认（saves\<包 id>.json，相对仓库根）。
if ($SaveRoot) { $demoArgs += @('--save', $terminalSave) }

# 用 dotnet exec 直接跑已构建好的 dll：不经过 `dotnet run`，少一层进程，
# 而且 Ctrl+C 直接作用在游戏上（run 会多一个中间进程，退出时容易留孤儿）。
& dotnet exec $dll @demoArgs
exit $LASTEXITCODE
