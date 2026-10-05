# pack.ps1 — 打出可分发的 zip。
#
# 用法:
#   powershell -File tools/pack.ps1                 # 打包 + 解包自检
#   powershell -File tools/pack.ps1 -SkipSelfCheck  # 只打包，不跑产物自检
#
# 产物:
#   artifacts/neko-clicker-<版本>-win-x64.zip
#     说明.md       ← 拷贝自 engine/docs/PACK_README.md
#     demo/         ← 终端宿主（框架依赖发布）
#     web/          ← Web 宿主（框架依赖发布，含 wwwroot）
#
# 打完 zip 之后**默认会把它解开做一次产物自检**（起两个 exe、打 HTTP；判据见
# engine/docs/RELEASING.md §6.2），跳过用 -SkipSelfCheck。为什么默认开、而不是留成人工步骤：
# 端到端探针（tools/api-test.ps1）跑的是 `dotnet run` 的开发期路径，**看不见发布产物的布局**
# ——1.3.0 的"首页 404"（/api/* 全通、而 / 是 404）就是这么漏出去的。
#
# 为什么是"框架依赖"而不是自包含单文件：自包含需要 runtime pack
# （Microsoft.NETCore.App.Runtime.win-x64），那是 NuGet 包；这个仓库的前提是
# **可能没有网络**，实测无网时 --self-contained 会以 NU1301 失败。
# 所以这里只出框架依赖包，并在说明书里写清"需要 .NET 8 运行时"。
# 要自包含包就在有网的机器上跑一次 dotnet publish --self-contained（命令写在说明书里）。
#
# 版本号只有一处事实来源：Directory.Build.props 的 <Version>。这里读它，不另写一份。

param(
    [switch]$SkipSelfCheck
)

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

# ---------------------------------------------------------------- 产物自检
# 为什么默认跑、而且**必须解开到临时目录再跑**（判据逐条对着 engine/docs/RELEASING.md §6.2）：
#   · 端到端探针（tools/api-test.ps1）跑的是 `dotnet run` 的开发期路径，**看不见发布产物的布局**，
#     所以这一层一直没有东西守着——1.3.0 的首页 404 就是这么漏出去的；
#   · 1.3.0 那个缺陷**只在"从包根目录起"时才露出来**，所以不能在 artifacts\neko-clicker-<版本>\
#     这个 stage 目录里跑（那里的邻居布局与 zip 里的不一样），必须解开 zip、拿解出来的那份当包根目录。
# 自检**不改 zip**：只在临时目录里解开、起进程、打 HTTP，跑完把临时目录删掉（删不掉只警告）。
# 每条判据都打印"判据 + 实测值"，失败就 throw——一个看起来成功、其实没验过首页的打包步骤
# 比一次响亮的失败坏得多。

function Test-ArtifactPortBusy([int]$Port) {
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

function Get-ArtifactResponse([string]$Url) {
    try {
        $response = $script:SelfCheckHttp.GetAsync($Url).GetAwaiter().GetResult()
        return [pscustomobject]@{
            Ok     = $true
            Status = [int]$response.StatusCode
            Body   = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Status = 0; Body = $_.Exception.Message }
    }
}

function Show-ArtifactLog([string]$Path, [string]$Label) {
    if (-not (Test-Path $Path)) { return }
    Write-Host "  $Label（尾部 20 行）：" -ForegroundColor Red
    Get-Content $Path -Encoding UTF8 | Select-Object -Last 20 | ForEach-Object { Write-Host "    $_" }
}

# 起发布产物里的一个 exe 并等它结束，把 stdout / stderr 一并收回来。
# 刻意用 Start-Process 重定向到**文件**再读回，而不是 `& exe ...` 直接捕获：受限沙箱禁命名管道，
# 原生命令的 stdout 走管道会整段拿不回来（本机实测 `& cmd /c echo` 一行都读不到），
# 而重定向到文件那条路是通的（tools/api-test.ps1 起宿主用的也是这一条）。
function Invoke-ArtifactProcess([string]$Exe, [string[]]$Arguments, [string]$WorkingDirectory, [string]$Tag) {
    $outLog = Join-Path $selfCheckRoot "$Tag.out.log"
    $errLog = Join-Path $selfCheckRoot "$Tag.err.log"
    $proc = Start-Process -FilePath $Exe -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory `
        -PassThru -Wait -NoNewWindow -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    # 帮助文本可能走 stdout，也可能走 stderr（不同 CLI 不一样），两路都收进来一起判。
    $text = ''
    if (Test-Path $outLog) { $text += [string](Get-Content $outLog -Raw -Encoding UTF8) }
    if (Test-Path $errLog) { $text += [string](Get-Content $errLog -Raw -Encoding UTF8) }
    return [pscustomobject]@{ ExitCode = $proc.ExitCode; Text = $text; OutLog = $outLog; ErrLog = $errLog }
}

if ($SkipSelfCheck) {
    Write-Host ''
    Write-Host '产物自检：已跳过（-SkipSelfCheck）——这个 zip 里首页到底通不通，这次没人验过。' -ForegroundColor Yellow
}
else {
    Write-Host ''
    Write-Host '产物自检（解开 zip 到临时目录再跑；判据见 engine/docs/RELEASING.md §6.2）' -ForegroundColor Cyan

    # ---- 运行时探测（抄自 tools/api-test.ps1 的同名一段）：产物声明的是 net8.0，本机只有更高的
    # 运行时，不显式退化的话宿主会以一段英文的"找不到框架"退出——那是环境适配，不是产物缺陷，
    # 所以要说清楚是自己设的。读不到任何一行时（受限沙箱里原生命令的管道捕获会整段失败）
    # 也按"缺 8.x"处理：上滚是安全方向，反过来才会让自检因为环境而不是产物变红。
    $runtimeLines = @(& dotnet --list-runtimes 2>$null)
    if (-not ($runtimeLines | Where-Object { $_ -like 'Microsoft.AspNetCore.App 8.*' })) {
        $env:DOTNET_ROLL_FORWARD = 'Major'
        Write-Host "  运行时探测：没有 Microsoft.AspNetCore.App 8.*（dotnet --list-runtimes 读到 $($runtimeLines.Count) 行）" -ForegroundColor DarkYellow
        Write-Host '    → 已设 DOTNET_ROLL_FORWARD=Major，让产物上滚到本机更高的运行时上跑。' -ForegroundColor DarkYellow
    }
    else {
        Write-Host '  运行时探测：本机有 Microsoft.AspNetCore.App 8.x，不设 DOTNET_ROLL_FORWARD。' -ForegroundColor DarkGray
    }

    Add-Type -AssemblyName System.Net.Http
    $script:SelfCheckHttp = New-Object System.Net.Http.HttpClient
    $script:SelfCheckHttp.Timeout = [TimeSpan]::FromSeconds(20)

    # 临时目录取系统 temp 下的一个唯一子目录：**不碰 stage 目录**（见这一节开头），也不碰仓库里
    # 的任何东西——自检跑完，仓库该是什么样还是什么样（zip 更是原样）。
    $selfCheckRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("neko-clicker-selfcheck-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $checkRoot = Join-Path $selfCheckRoot 'package'
    $port = 0
    $webProcess = $null
    $portReleased = $false
    $failure = $null

    Write-Host "  解开到：$checkRoot" -ForegroundColor DarkGray
    try {
        New-Item -ItemType Directory -Force -Path $checkRoot | Out-Null
        Expand-Archive -Path $zip -DestinationPath $checkRoot -Force

        # ---- ① 终端宿主的帮助文本：退出码 0，且输出里含 --simulate。
        $demoExe = Join-Path $checkRoot 'demo\neko-clicker.exe'
        if (-not (Test-Path $demoExe)) { throw "解出来的包里没有 demo\neko-clicker.exe——zip 布局与说明书不符。" }

        $helpRun = Invoke-ArtifactProcess -Exe $demoExe -Arguments @('--help') -WorkingDirectory $checkRoot -Tag 'demo-help'
        $helpHasSimulate = $helpRun.Text.Contains('--simulate')
        $helpLines = @($helpRun.Text -split "`n").Count
        Write-Host "  [判据] demo --help：退出码 0 且输出含 --simulate ｜ [实测] 退出码 $($helpRun.ExitCode)，含 --simulate：$helpHasSimulate（$helpLines 行）" -ForegroundColor DarkGray
        if ($helpRun.ExitCode -ne 0) {
            Show-ArtifactLog $helpRun.ErrLog 'stderr'
            throw "demo\neko-clicker.exe --help 退出码 $($helpRun.ExitCode)（期望 0）。"
        }
        if (-not $helpHasSimulate) { throw 'demo --help 的输出里没有 --simulate——帮助文本没送到 stdout/stderr，或者产物里缺了这条选项。' }

        # ---- ② 终端宿主真的能模拟：退出码 0（--no-save 保证它不碰任何真实存档，
        #         --no-latency-log 保证不往 artifacts/latency.txt 那份真人数据上追加）。
        $simArgs = @('--simulate', '300', '--auto', '--no-save', '--no-latency-log', '--no-color')
        $simRun = Invoke-ArtifactProcess -Exe $demoExe -Arguments $simArgs -WorkingDirectory $checkRoot -Tag 'demo-simulate'
        $simLines = @($simRun.Text -split "`n").Count
        Write-Host "  [判据] demo --simulate 300 --auto --no-save --no-latency-log --no-color：退出码 0 ｜ [实测] 退出码 $($simRun.ExitCode)（$simLines 行输出）" -ForegroundColor DarkGray
        if ($simRun.ExitCode -ne 0) {
            Show-ArtifactLog $simRun.OutLog 'stdout'
            Show-ArtifactLog $simRun.ErrLog 'stderr'
            throw "demo\neko-clicker.exe --simulate 退出码 $($simRun.ExitCode)（期望 0）——产物里的终端宿主跑不起来。"
        }

        # ---- ③ Web 宿主真的能起。端口**不写死 5273**（那是真人试玩用的），从高位随机取一个、
        #         并且先真的绑一下确认空闲；绑不上就换一个，别去猜它什么时候松手。
        for ($attempt = 0; $attempt -lt 20 -and $port -le 0; $attempt++) {
            $candidate = Get-Random -Minimum 49152 -Maximum 65000
            $listener = $null
            try {
                $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidate)
                $listener.Start()
                $port = $candidate
            }
            catch {
                $port = 0
            }
            finally {
                if ($null -ne $listener) { $listener.Stop() }
            }
        }
        if ($port -le 0) { throw '连续 20 个高位随机端口都探测到被占用——本机端口环境异常，自检没法公平地跑。' }

        $webExe = Join-Path $checkRoot 'web\neko-clicker-web.exe'
        if (-not (Test-Path $webExe)) { throw "解出来的包里没有 web\neko-clicker-web.exe——zip 布局与说明书不符。" }
        $webSaveRoot = Join-Path $selfCheckRoot 'saves'
        $webLatencyLog = Join-Path $selfCheckRoot 'latency.txt'
        $webOutLog = Join-Path $selfCheckRoot 'web.out.log'
        $webErrLog = Join-Path $selfCheckRoot 'web.err.log'
        New-Item -ItemType Directory -Force -Path $webSaveRoot | Out-Null

        # 工作目录设成**包根目录**：1.3.0 的首页 404 正是这个条件下才露出来的（见这一节开头）。
        # 路径一律自己加引号：Start-Process 是用空格拼接参数表的，工作区路径里有空格时会散架
        # （与 tools/api-test.ps1 起宿主时同一条理由）。
        $webArgs = @('--urls', "`"http://127.0.0.1:$port`"", '--save-root', "`"$webSaveRoot`"", '--latency-log', "`"$webLatencyLog`"")
        $webProcess = Start-Process -FilePath $webExe -ArgumentList $webArgs -WorkingDirectory $checkRoot `
            -PassThru -NoNewWindow -RedirectStandardOutput $webOutLog -RedirectStandardError $webErrLog

        $ping = $null
        $lastProbe = ''
        $deadline = [System.Diagnostics.Stopwatch]::StartNew()
        while ($deadline.Elapsed.TotalSeconds -lt 60) {
            if ($webProcess.HasExited) { break }
            Start-Sleep -Milliseconds 400
            $probe = Get-ArtifactResponse "http://127.0.0.1:$port/api/ping"
            if ($probe.Status -eq 200) {
                $parsed = $null
                try { $parsed = $probe.Body | ConvertFrom-Json } catch { $parsed = $null }
                if ($null -ne $parsed) { $ping = $parsed; break }
                $lastProbe = 'HTTP 200，但正文不是 JSON'
                continue
            }
            $lastProbe = "HTTP $($probe.Status) $($probe.Body)"
        }

        if ($null -eq $ping) {
            # 起不来时唯一的线索就是它自己的输出——先摊开再 throw。
            Show-ArtifactLog $webOutLog 'web 宿主的 stdout'
            Show-ArtifactLog $webErrLog 'web 宿主的 stderr'
            throw "web\neko-clicker-web.exe 60 秒内没有就绪（最后一次探测：$lastProbe）。"
        }

        $apiVersion = [string]$ping.apiVersion
        Write-Host "  [判据] /api/ping 的 apiVersion = Directory.Build.props 的版本（$version） ｜ [实测] apiVersion=$apiVersion，runtime=$($ping.runtime)" -ForegroundColor DarkGray
        if ($apiVersion -cne $version) { throw "产物报的 apiVersion=$apiVersion，仓库里是 $version——包里的不是这一次发布的产物。" }

        # ---- ④ 静态资源：这一条守的正是"发布产物里 /api/* 全通、而首页 404"那个形态（1.3.0）。
        #         判据分两层：三个状态码都是 200，而且**正文里真的有那些挂载点 / 渲染函数**
        #         ——200 但送回来的是别的东西（比如一张错误页）同样不算数。
        $indexResponse = Get-ArtifactResponse "http://127.0.0.1:$port/"
        $scriptResponse = Get-ArtifactResponse "http://127.0.0.1:$port/app.js"
        $styleResponse = Get-ArtifactResponse "http://127.0.0.1:$port/app.css"

        $mountSheet = [bool]($indexResponse.Body -and $indexResponse.Body.Contains('save-sheet'))
        $mountOffline = [bool]($indexResponse.Body -and $indexResponse.Body.Contains('offline'))
        $rendersOffline = [bool]($scriptResponse.Body -and $scriptResponse.Body.Contains('renderOffline'))
        $indexBytes = if ($indexResponse.Body) { [System.Text.Encoding]::UTF8.GetByteCount([string]$indexResponse.Body) } else { 0 }
        Write-Host "  [判据] GET / 与 /app.js 与 /app.css 全 200，且首页含挂载点（save-sheet 或 offline）、app.js 含 renderOffline" -ForegroundColor DarkGray
        Write-Host "  [实测] / → HTTP $($indexResponse.Status)、$indexBytes 字节（save-sheet：$mountSheet ｜ offline：$mountOffline）；/app.js → HTTP $($scriptResponse.Status)（renderOffline：$rendersOffline）；/app.css → HTTP $($styleResponse.Status)" -ForegroundColor DarkGray

        foreach ($item in @(
                @{ Name = 'GET /'; Response = $indexResponse },
                @{ Name = 'GET /app.js'; Response = $scriptResponse },
                @{ Name = 'GET /app.css'; Response = $styleResponse }
            )) {
            if ($item.Response.Status -ne 200) { throw "$($item.Name) → HTTP $($item.Response.Status)（期望 200）——发布产物里的静态文件没在位。" }
        }
        if (-not ($mountSheet -or $mountOffline)) { throw '首页正文里既没有 save-sheet 也没有 offline 挂载点——静态文件在产物里 404，或者发布出去的首页不是这一版。' }
        if (-not $rendersOffline) { throw 'app.js 正文里没有 renderOffline——送到浏览器的不是这一版前端。' }

        Write-Host '  产物自检通过：两个 exe 都真的跑起来了，静态资源与版本号都对得上。' -ForegroundColor Green
    }
    catch {
        $failure = $_
    }
    finally {
        # 收尾**只认第 ③ 步拿到的那一个 PID**（Start-Process -PassThru 的返回值），不做端口反查：
        # 反查那一套在受限环境里会整体失效（见 tools/api-test.ps1 头部 ③），而这里恰好有一个
        # 可靠的 PID 可用——用它，然后用端口真的松手来确认它确实死了。
        if ($null -ne $webProcess) {
            if (-not $webProcess.HasExited) {
                Stop-Process -Id $webProcess.Id -Force -ErrorAction SilentlyContinue
            }
            # 进程退出到端口真正松手之间有一小段（Kestrel 收监听），所以轮询而不是立刻断言。
            for ($i = 0; $i -lt 40; $i++) {
                Start-Sleep -Milliseconds 250
                if (-not (Test-ArtifactPortBusy -Port $port)) { $portReleased = $true; break }
            }
        }

        if ($null -ne $script:SelfCheckHttp) { $script:SelfCheckHttp.Dispose() }

        # 临时目录清理：失败只警告——它不该把一个已经跑完的自检变成红的（zip 不受影响）。
        try {
            Remove-Item $selfCheckRoot -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Host "  [警告] 临时目录没删掉：$selfCheckRoot（$($_.Exception.Message)）" -ForegroundColor Yellow
        }
    }

    if ($null -ne $failure) {
        Write-Host "  [结论] 产物自检失败——**这个 zip 不要发出去**：$zip" -ForegroundColor Red
        throw $failure
    }
    if ($null -ne $webProcess -and -not $portReleased) {
        throw "自检收尾：宿主（PID $($webProcess.Id)）结束了，但端口 $port 在 10 秒内没有松手——本机上多了一个还占着端口的孤儿。"
    }
}
