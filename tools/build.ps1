# 构建 + 测试一键脚本。
#
# 用法:
#   pwsh -File tools/build.ps1            快速构建（增量）+ 全部测试 + 前端冒烟
#   pwsh -File tools/build.ps1 -Strict    全量重编 + 警告即错误 + 全部测试 + 前端冒烟（提交前跑）
#   pwsh -File tools/build.ps1 -SkipWebSmoke   显式跳过前端冒烟（只有"这台机器没有 node"才该用它）
#
# 为什么要分两档：
#   增量编译对"这次没重编的项目"不会重新回报警告，所以输出里的 "0 Warning(s)"
#   可能只表示"这次没编译"。实测仓库里曾同时藏着 4 条警告——一条 cref 路径写错、
#   一条 XML 注释里嵌套了 <para>、一条可能的空引用——增量构建里一条都没露过面。
#   但全量重编实测要 ~41 秒（增量约 1 秒），拿它当内循环太慢，
#   所以平时用默认档求快，**提交前用 -Strict 求准**。
#
# -warnaserror 两档都加：只要项目真的被编译过，警告就不许悄悄溜过去。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$strict = $args -contains '-Strict'
$forward = @($args | Where-Object { $_ -ne '-Strict' -and $_ -ne '-SkipWebSmoke' })

# 路径一律用 Join-Path / 正斜杠拼：PowerShell 的**提供程序**在 Linux 上会把 `\` 当分隔符
# 归一化掉（所以 `& "$PSScriptRoot\dnet.ps1"` 那种调用在 Linux 上照样能跑），
# 但**传给原生进程的参数不会被归一化**——`dotnet exec <带反斜杠的 dll 路径>` 在 Linux 上
# 报的是 "The application to execute does not exist"。这是 2026-10-05 加 Linux 作业（W7）时
# 逮到的：Linux 上编译全过（0 警告 0 错误 ×2），红的只有下面这一行传出去的路径。
$buildArgs = @((Join-Path $root 'NekoClicker.sln'), '-v', 'q', '--nologo', '-warnaserror')
if ($strict) { $buildArgs += '--no-incremental' }

# 构建失败时：**只报告**谁可能占着 bin / obj，绝不杀任何进程（STRUCTURE_OPTIMIZATION §S3）。
#
# 为什么要有它：宿主（或同一工作区里另一轮构建）占着 games\hosts\Web\bin / engine\tests\bin 时，
# dotnet build 会吐 144~170 条 MSB3021 / MSB3026 / MSB3027，而**没有一行说得出是谁占着**。
# "怎么找占用者"的判据仓库里早就有了——tools/start.ps1:39~50 的 Path 前缀筛选、
# 以及 DEVELOPMENT_SUMMARY §3.4 那三条处置——只是它们只在"启动"那条路上执行。
#
# 规矩照抄、一个字都不改：**先认人、能等就别杀**。5273 上可能正有真人在玩
# （OPEN_WORK H1 等的就是这个样本），所以这里**不做任何清理**——杀不杀是人打的决定。
# 这条路径记录过一次约 320 个 dotnet 进程 / 9.9 GB 的事件，但那只是**时间相关、没有解释**
# （AGENT_ARCHIVE:19 自己标了这条边界），所以这一节不声称因果、也不在这里处理它。
function Show-HolderDiagnosis {
    Write-Host ''
    Write-Host '=== 谁可能占着 bin（只报告，不杀任何进程）===' -ForegroundColor Yellow

    # 1) 本仓库目录下的进程：判据与 start.ps1:40~41 完全相同（映像路径以仓库根开头）。
    #    逐个进程取 Path、各自 try/catch：受保护进程读不到 Path，而这里 $ErrorActionPreference = 'Stop'。
    $mine = @()
    foreach ($p in @(Get-Process -ErrorAction SilentlyContinue)) {
        try {
            if ($p.Path -and $p.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { $mine += $p }
        }
        catch { }
    }

    if ($mine.Count -eq 0) {
        Write-Host '  本仓库目录下的进程：0 个（占着文件的可能是这个仓库之外的东西）。'
    }
    else {
        Write-Host ("  本仓库目录下的进程：{0} 个" -f $mine.Count)
        foreach ($p in $mine) {
            try {
                Write-Host ("    - {0}  PID {1}  启动 {2}（已运行 {3}）  映像 {4}" -f `
                    $p.ProcessName, $p.Id, $p.StartTime.ToString('yyyy-MM-dd HH:mm:ss'), `
                    ((Get-Date) - $p.StartTime).ToString('hh\:mm\:ss'), $p.Path)
            }
            catch {
                Write-Host ("    - PID {0}（读不到启动时间 / 映像：{1}）" -f $p.Id, $_.Exception.GetType().Name)
            }
        }
    }

    # 2) 模型宿主的端口：默认 5273（start.ps1:64），NEKO_PORT 可覆盖。这里用 netstat 而不是
    #    Get-NetTCPConnection / Get-CimInstance——后两者在这个沙箱里会抛 CimException（登记册 W2 记的就是它）。
    $ports = @(5273)
    if ($env:NEKO_PORT) { $ports += $env:NEKO_PORT }

    $listeners = @()
    try {
        foreach ($line in @(netstat -ano -p TCP 2>$null)) {
            $m = [regex]::Match($line, '^\s*TCP\s+\S+:(\d+)\s+\S+\s+LISTENING\s+(\d+)\s*$')
            if ($m.Success -and ($ports -contains [int]$m.Groups[1].Value)) { $listeners += $m.Groups[2].Value }
        }
    }
    catch { }

    if ($listeners.Count -eq 0) {
        Write-Host ("  监听端口 {0} 的进程：0 个。" -f ($ports -join ' / '))
    }
    else {
        foreach ($holderPid in ($listeners | Select-Object -Unique)) {
            $image = '?'
            $started = '?'
            try {
                $holder = Get-Process -Id ([int]$holderPid) -ErrorAction Stop
                $image = $holder.ProcessName
                $started = $holder.StartTime.ToString('yyyy-MM-dd HH:mm:ss')
            }
            catch { }
            Write-Host ("    PID {0}（{1}，监听 {2}，启动 {3}）" -f $holderPid, $image, ($ports -join ' / '), $started) -ForegroundColor Yellow
        }
    }

    Write-Host ''
    Write-Host '  怎么读（判据与 DEVELOPMENT_SUMMARY §3.4 同一条）：'
    Write-Host '    · 本仓库 1 个进程、刚起来 → 大概率是同一工作区里另一轮构建在跑：等 30 秒再跑一次。'
    Write-Host '    · 本仓库几百个同源进程、StartTime 很旧 → 是残留；要清就按严格的 StartTime 截止时间清。'
    Write-Host '    · 5273 上有监听者 → 那可能是真人在玩的宿主（OPEN_WORK H1 等它的样本）：别杀它。'
    Write-Host '  这一节只报告：杀不杀是人打的决定。'
}

Write-Host '=== 构建 ===' -ForegroundColor Cyan
if (-not $strict) {
    Write-Host '（快速档：只编改动过的项目。提交前请跑一次 -Strict——全量重编才暴露得出被增量掩盖的警告）' -ForegroundColor DarkGray
}
& "$PSScriptRoot\dnet.ps1" build @buildArgs
if ($LASTEXITCODE -ne 0) {
    # 注意：-warnaserror 会把警告报成 **error**，所以失败时的摘要常是
    # "0 Warning(s) / N Error(s)"——别看警告计数，看退出码与上面的 error 行。
    # 先把退出码抄下来：下面那段诊断会跑 netstat，那会把 $LASTEXITCODE 覆盖成它自己的退出码。
    $code = $LASTEXITCODE
    Write-Host '构建失败（若摘要显示 0 Warning(s) 却失败，那是警告被 -warnaserror 计成了 error）。' -ForegroundColor Red
    Write-Host '（上面若是成片的 MSB3021 / MSB3026 / MSB3027，那是有人占着 bin——下一段就是认人用的。）' -ForegroundColor Red
    Show-HolderDiagnosis
    exit $code
}

# Web 宿主有自己独立的单项目 sln（故意的：它不跟引擎一起发布），所以主 sln 编不到它。
# 而"编不到"的后果是**静默**的：实测它曾经长时间停在 net10.0，而本机只有 SDK 8.0.303，
# 也就是**根本编不过**——却没有任何一条命令会红，直到有人真的去编它。
# 所以这一条必须由 build.ps1 兜住：一条命令验证全部，才有资格叫"一键"。
$webSln = Join-Path $root 'games/hosts/Web/NekoClicker.Web.sln'
if (Test-Path $webSln) {
    Write-Host ''
    Write-Host '=== 构建 Web 宿主（独立 sln）===' -ForegroundColor Cyan
    & "$PSScriptRoot\dnet.ps1" build $webSln -v q --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) {
        $code = $LASTEXITCODE
        Write-Host 'Web 宿主构建失败。它有自己的 sln，主 sln 编不到它——这正是这一步存在的理由。' -ForegroundColor Red
        Show-HolderDiagnosis
        exit $code
    }
}

# 前端冒烟（tools/web-smoke.mjs）：无头 DOM 桩件把 wwwroot/app.js **真的跑一遍**。
#
# 为什么它必须在这一条命令里（STRUCTURE_OPTIMIZATION §S1）：它自 2026-10 起有 **202** 条断言
# （135 → 162 是 2026-10-04 加的存档导出/导入窗口那一节；162 → 193 → 199 → 202 是此后四轮
# 界面改动各加的——这个数会随守卫增长，以它自己打印的那一行为准），
# 其中 210 行专为"快照里多了一个没人画的字段"这个**复发过五次**的 bug 类而写——
# 而它此前不在任何自动闸门上（build.ps1 不跑、CI 不跑），也就是说那道守卫只在
# "有人记得"的时候才说话。这跟没有守卫的区别，只在于心理。
#
# 它不需要 .NET、不需要宿主、不需要网络，只要 node。
#
# **没有 node 时怎么办**：刻意**不允许静默跳过**——静默跳过正是 S1 这条欠账的形态本身。
# 所以默认行为是**红**：清楚地说出"缺 node、前端这一层没被验证"，并给出两条出路
# （装 Node；或显式加 -SkipWebSmoke）。跳过必须是有人打出来的决定，不是默认值。
# （本脚本自己的注释原先写着"刻意不进来，因为会让『没有 node 的机器上还能不能过』变成新问题"。
#  那个问题是真的，答案是：让它红，并让退出方式只有一个人工开关。）
if ($args -contains '-SkipWebSmoke') {
    Write-Host ''
    Write-Host '⚠ 已显式跳过前端冒烟（-SkipWebSmoke）：这次构建**没有**验证 wwwroot（app.js / index.html / app.css）。' -ForegroundColor Yellow
}
else {
    Write-Host ''
    Write-Host '=== 前端冒烟（tools/web-smoke.mjs）===' -ForegroundColor Cyan
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Host '找不到 node —— 前端冒烟套件跑不了。' -ForegroundColor Red
        Write-Host '这里刻意不静默跳过：前端回归正是"没人自动跑"才复发过五次。' -ForegroundColor Red
        Write-Host '装 Node（https://nodejs.org）后重跑；本机确实没有 node、且这次改动与 wwwroot 无关时，' -ForegroundColor Yellow
        Write-Host '显式加 -SkipWebSmoke —— 跳过是有人打出来的决定，不是默认行为。' -ForegroundColor Yellow
        exit 1
    }
    & node "$PSScriptRoot\web-smoke.mjs"
    if ($LASTEXITCODE -ne 0) {
        Write-Host '前端冒烟失败：wwwroot 那一层有回归（上面每一条 ✘ 就是原因）。' -ForegroundColor Red
        Write-Host '它不碰引擎——红了说明页面这一侧坏了，而不是 C# 那侧。' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host ''
Write-Host '=== 测试 ===' -ForegroundColor Cyan
$testDll = Join-Path $root 'engine/tests/bin/Debug/net8.0/NekoClicker.Core.Tests.dll'
& "$PSScriptRoot\dnet.ps1" exec $testDll @forward
exit $LASTEXITCODE
