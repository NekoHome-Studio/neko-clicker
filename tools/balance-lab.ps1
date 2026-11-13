# ============================================================================
# balance-lab.ps1 — 数值实验室（D2 的"基线测量"形态）
#
# 它回答的问题：**"一局该多久 / 每个包的主线有多长"**——正是 OPEN_WORK 的 D2 / D9 / D10
# 三个 👤 决定项一直缺的那个数。做法是把现成的无头机器人（Demo.Cli 的 --simulate --auto）
# 横扫每个内容包，解析它打印的**带时间戳的纪元切换**，聚合成一张"到层时长"对比表。
#
# 三条边界（都别当成没看见）：
#   ① **零引擎改动**：它只是驱动现成的机器人并读它的输出，不碰 engine/core，也不改任何内容。
#      真正的"参数扫描"（改价格曲线/门槛再跑）需要内容变体机制，那是后续（见 CREATIVE_PLAN D2）。
#   ② **绝不碰真人存档**：每次跑都带 --no-save（SavePath=null，引擎完全不落盘），
#      也不写埋点（机器人作答不是人类数据）。saves/ 一个字节都不会动。
#   ③ **可复现**：固定 --seed（默认 42）。同种子同参数，结果逐次相同。
#
# 用法：
#   powershell -File tools/balance-lab.ps1                     # 全部 11 包，各跑 48h 游戏时长
#   powershell -File tools/balance-lab.ps1 -Duration 21600     # 各跑 6h
#   powershell -File tools/balance-lab.ps1 -Packages ninelines,lab -Seed 7
#
# 前提：Demo 已构建（tools/build.ps1）。报告写到 artifacts/（gitignored），并打印到控制台。
# ============================================================================

param(
    [int]$Duration = 172800,        # 每包的游戏内时长（秒），默认 48h
    [UInt64]$Seed = 42,             # 随机种子（可复现）。用 [UInt64] 全名：PS 5.1 不认 [ulong] 加速器
    [string[]]$Packages = @(),      # 空 = 全部 11 包
    [string]$Out = '',              # 报告路径（缺省 artifacts/balance-lab-<时间戳>.md）
    [string]$Exe = ''               # 显式指定 neko-clicker.exe（缺省自动找）
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# ---- 找 Demo exe（Debug 优先，其次 Release）----
if (-not $Exe) {
    $candidates = @(
        (Join-Path $root 'games/hosts/Demo.Cli/bin/Debug/net8.0/neko-clicker.exe'),
        (Join-Path $root 'games/hosts/Demo.Cli/bin/Release/net8.0/neko-clicker.exe')
    )
    $Exe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Exe -or -not (Test-Path $Exe)) {
    Write-Error '找不到 neko-clicker.exe。请先跑 tools/build.ps1 构建 Demo。'
    exit 1
}

$allPacks = @('neko','cafe','ninelines','lab','company','apocalypse','library','god','civ','cyber','dream')
# -File 调法会把 "-Packages a,b" 当成单个字符串 "a,b"：拆开，兼容数组与逗号串两种传法。
$Packages = @($Packages | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ -ne '' })
if ($Packages.Count -eq 0) { $Packages = $allPacks }

# ---- 把 NumFormat.Duration 的 "1h 38m" / "42m 25s" / "2d 3h" 解析成秒 ----
function Parse-Duration([string]$s) {
    $total = 0.0
    foreach ($m in [regex]::Matches($s, '(\d+(?:\.\d+)?)\s*([dhms])')) {
        $v = [double]$m.Groups[1].Value
        switch ($m.Groups[2].Value) {
            'd' { $total += $v * 86400 }
            'h' { $total += $v * 3600 }
            'm' { $total += $v * 60 }
            's' { $total += $v }
        }
    }
    return $total
}

function Format-Dur([double]$sec) {
    if ($sec -le 0) { return '—' }
    $h = [math]::Floor($sec / 3600)
    $m = [math]::Floor(($sec % 3600) / 60)
    if ($h -gt 0) { return ('{0}h {1:00}m' -f $h, $m) }
    return ('{0}m' -f $m)
}

# ---- 逐包跑机器人并解析 ----
$results = @()
foreach ($pack in $Packages) {
    Write-Host ("跑 {0}（{1} 游戏时长，种子 {2}）…" -f $pack, (Format-Dur $Duration), $Seed)
    $cliArgs = @('--package', $pack, '--simulate', "$Duration", '--auto', '--no-save', '--seed', "$Seed")
    $output = & $Exe @cliArgs 2>&1 | Out-String
    $clean = [regex]::Replace($output, '\x1b\[[0-9;]*m', '')   # 剥 ANSI，防色码干扰解析

    # 纪元切换：`  [ 42m 25s] —— 舍命：第一命 … → 第二命 … ——`
    $transitions = @()
    foreach ($m in [regex]::Matches($clean, '\[\s*([^\]]+?)\s*\]\s*——\s*[^：]+：(.+?)\s*→\s*(.+?)\s*——')) {
        $transitions += [pscustomobject]@{
            Time = (Parse-Duration $m.Groups[1].Value)
            To   = $m.Groups[3].Value.Trim()
        }
    }

    # 终局报告里的几个字段（只认"带 / 的总数"或"第 X / Y 层"这种终局形状，避开进度行）
    $finalEra = ''; $eraTotal = ''
    $em = [regex]::Match($clean, '当前\s+第\s*(\d+)\s*/\s*(\d+)\s*层')
    if ($em.Success) { $finalEra = $em.Groups[1].Value; $eraTotal = $em.Groups[2].Value }

    $ending = ''
    $endm = [regex]::Match($clean, '(?m)^\s*结局\s+(.+)$')
    if ($endm.Success) { $ending = $endm.Groups[1].Value.Trim() }

    $ach = ''
    $am = [regex]::Match($clean, '成就\s+(\d+)\s*/\s*(\d+)')
    if ($am.Success) { $ach = ('{0}/{1}' -f $am.Groups[1].Value, $am.Groups[2].Value) }

    $codex = ''
    $cm = [regex]::Match($clean, '已读到\s+(\d+)\s*/\s*(\d+)')
    if ($cm.Success) { $codex = ('{0}/{1}' -f $cm.Groups[1].Value, $cm.Groups[2].Value) }

    $results += [pscustomobject]@{
        Pack = $pack; Transitions = $transitions; FinalEra = $finalEra; EraTotal = $eraTotal
        Ending = $ending; Ach = $ach; Codex = $codex
    }
}

# ---- 聚合成 markdown 对比表 ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# 数值实验室基线报告（D2）')
[void]$sb.AppendLine('')
[void]$sb.AppendLine(('- 每包游戏内时长：**{0}**；种子：{1}；策略：自动购买（--auto）；**不落盘（--no-save）**。' -f (Format-Dur $Duration), $Seed))
[void]$sb.AppendLine(('- 生成时间：{0}。这是"基线测量"：改参数再扫需要内容变体机制（后续）。' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| 包 | 终层 | 到各层时长（累积） | 结局 | 成就 | 图鉴 |')
[void]$sb.AppendLine('|---|---|---|---|---|---|')
foreach ($r in $results) {
    $eraStr = if ($r.FinalEra) { '{0}/{1}' -f $r.FinalEra, $r.EraTotal } else { '无纪元' }
    $timeStr = ($r.Transitions | ForEach-Object { Format-Dur $_.Time }) -join ' → '
    if (-not $timeStr) { $timeStr = '—' }
    $endStr = if ($r.Ending) { $r.Ending } else { '未达' }
    [void]$sb.AppendLine(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f $r.Pack, $eraStr, $timeStr, $endStr, $r.Ach, $r.Codex))
}
$report = $sb.ToString()

if (-not $Out) {
    $Out = Join-Path $root ('artifacts/balance-lab-{0}.md' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$outDir = Split-Path -Parent $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
[System.IO.File]::WriteAllText($Out, $report, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host $report
Write-Host ("报告已写入：{0}" -f $Out)
