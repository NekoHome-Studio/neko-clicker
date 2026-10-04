<#
  push-retry.ps1 —— 反复尝试把 main 与本地 tag 推到远端，直到「服务器侧核对」通过为止。

  为什么不是一句 git push：
    这台机器到 github 的连接时通时断（直连常常 21 秒超时，偶尔一次就过），
    而 push 自己打印的 "main -> main" 不算证据 —— 唯一证据是 git ls-remote
    问回来的哈希。所以每次尝试之后都会**去问服务器**，只有哈希对上才算成功。
    （本仓库的规矩：不拿「命令说自己成功了」当结论。）

  用法：
    powershell -NoProfile -File tools\push-retry.ps1
    powershell -NoProfile -File tools\push-retry.ps1 -DeadlineMinutes 60 -DelaySeconds 20

  退出码：0 = 服务器侧已与本地一致；1 = 到期或次数用尽仍未成功（日志里留着每次的失败原文）。
  日志：artifacts\push-retry.log（artifacts 已被 .gitignore 忽略）。
#>
[CmdletBinding()]
param(
  [int]$MaxAttempts     = 400,   # 最多尝试多少次
  [int]$TimeoutSeconds  = 100,   # 单次 git 调用的超时（连接卡住时靠它掐断）
  [int]$DelaySeconds    = 30,    # 两次尝试之间的间隔
  [int]$DeadlineMinutes = 480    # 总时限；到点就停，不会无限跑
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root 'artifacts'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Force -Path $logDir | Out-Null }
$log = Join-Path $logDir 'push-retry.log'

function Write-Log([string]$Message) {
  $line = '{0}  {1}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $Message
  Write-Host $line
  Add-Content -Path $log -Value $line -Encoding UTF8
}

# 在子作业里跑 git，带超时：连接卡死时不能让整个循环挂住。
function Invoke-Git {
  param([string[]]$GitArgs, [int]$Timeout = $TimeoutSeconds)
  $job = Start-Job -ScriptBlock {
    param($root, $a)
    Set-Location $root
    $out = & git @a 2>&1
    [pscustomobject]@{ Out = ($out | Out-String); Code = $LASTEXITCODE }
  } -ArgumentList $root, $GitArgs
  if (Wait-Job $job -Timeout $Timeout) {
    $r = Receive-Job $job
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    if ($null -eq $r) { return [pscustomobject]@{ Out = ''; Code = 1; TimedOut = $false } }
    return [pscustomobject]@{ Out = $r.Out; Code = $r.Code; TimedOut = $false }
  }
  Stop-Job $job -ErrorAction SilentlyContinue
  Remove-Job $job -Force -ErrorAction SilentlyContinue
  return [pscustomobject]@{ Out = "（超时 $Timeout 秒）"; Code = 124; TimedOut = $true }
}

function Get-LastErrorLine([string]$Text) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return '(无输出)' }
  $lines = $Text -split "`r?`n" | Where-Object { $_.Trim() -ne '' }
  if ($lines.Count -eq 0) { return '(无输出)' }
  return $lines[-1].Trim()
}

function Get-RemoteMain {
  $r = Invoke-Git -GitArgs @('ls-remote', 'origin', 'refs/heads/main')
  if ($r.Code -ne 0) { return $null }
  $m = [regex]::Match($r.Out, '([0-9a-f]{40})')
  if ($m.Success) { return $m.Groups[1].Value }
  return $null
}

function Get-RemoteTagNames {
  $r = Invoke-Git -GitArgs @('ls-remote', '--tags', 'origin')
  if ($r.Code -ne 0) { return $null }
  $names = @()
  foreach ($line in ($r.Out -split "`r?`n")) {
    $m = [regex]::Match($line, 'refs/tags/(\S+)')
    if ($m.Success) { $names += $m.Groups[1].Value }
  }
  return $names
}

# ---- 起点状态 ----------------------------------------------------------------
$localHead = (& git -C $root rev-parse HEAD).Trim()
$localShort = $localHead.Substring(0, 7)
$localTags = @(& git -C $root tag)
$pendingCommits = @(& git -C $root rev-list --count 'origin/main..HEAD')
Write-Log ("开始：本地 HEAD={0}  待推提交={1}  本地 tag={2} 个" -f $localShort, $pendingCommits, $localTags.Count)
Write-Log ("参数：最多 {0} 次 · 单次超时 {1}s · 间隔 {2}s · 时限 {3} 分钟" -f $MaxAttempts, $TimeoutSeconds, $DelaySeconds, $DeadlineMinutes)
if ($pendingCommits -eq 0 -and $localTags.Count -eq 0) { Write-Log '没有待推的东西。'; exit 0 }

$deadline = (Get-Date).AddMinutes($DeadlineMinutes)
$attempt = 0
while ($attempt -lt $MaxAttempts -and (Get-Date) -lt $deadline) {
  $attempt++
  Write-Log ("—— 第 {0} 次尝试 ——" -f $attempt)

  # 1) main
  $p = Invoke-Git -GitArgs @('push', 'origin', 'main')
  if ($p.Code -eq 0) {
    Write-Log ('  push main：' + (Get-LastErrorLine $p.Out))
  } else {
    Write-Log ("  push main 失败（exit {0}）：{1}" -f $p.Code, (Get-LastErrorLine $p.Out))
  }

  # 2) tag：只推服务器上还没有的
  $remoteTags = Get-RemoteTagNames
  if ($null -eq $remoteTags) {
    Write-Log '  取远端 tag 列表失败（连接问题），这一步跳过。'
  } else {
    $missing = @($localTags | Where-Object { $remoteTags -notcontains $_ })
    if ($missing.Count -gt 0) {
      $t = Invoke-Git -GitArgs (@('push', 'origin') + $missing)
      if ($t.Code -eq 0) {
        Write-Log ("  推 tag：{0} —— {1}" -f ($missing -join ' '), (Get-LastErrorLine $t.Out))
      } else {
        Write-Log ("  推 tag 失败（exit {0}）：{1}" -f $t.Code, (Get-LastErrorLine $t.Out))
      }
    } else {
      Write-Log '  远端 tag 已齐。'
    }
  }

  # 3) 唯一证据：问服务器
  Start-Sleep -Seconds 2
  $remoteMain = Get-RemoteMain
  if ($null -ne $remoteMain -and $remoteMain -eq $localHead) {
    $rt = Get-RemoteTagNames
    $stillMissing = @()
    if ($null -ne $rt) { $stillMissing = @($localTags | Where-Object { $rt -notcontains $_ }) }
    if ($stillMissing.Count -eq 0) {
      Write-Log ("✔ 服务器侧核对通过：origin/main = {0}（与本地一致），{1} 个 tag 全部在位。" -f $remoteMain, $localTags.Count)
      exit 0
    }
    Write-Log ("  main 已对上（{0}），但还缺 tag：{1}" -f $remoteMain, ($stillMissing -join ' '))
  } else {
    if ($null -eq $remoteMain) { Write-Log '  服务器侧核对失败：连不上，读不到远端 main。' }
    else { Write-Log ("  服务器侧核对失败：远端 main = {0}，本地 = {1}" -f $remoteMain.Substring(0,7), $localShort) }
  }

  if ($attempt -lt $MaxAttempts -and (Get-Date).AddSeconds($DelaySeconds) -lt $deadline) {
    Start-Sleep -Seconds $DelaySeconds
  }
}

Write-Log ("✘ 停了：{0} 次尝试 / 时限内仍未通过服务器侧核对。日志见 {1}" -f $attempt, $log)
exit 1
