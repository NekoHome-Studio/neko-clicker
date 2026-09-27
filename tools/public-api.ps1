# public-api.ps1 — 重新生成 NekoClicker.Core 的公开 API 快照。
#
# 用法:
#   pwsh -File tools/public-api.ps1
#
# 什么时候用它（这是流程的**最后一步**，不是第一步）：
#   你确实有意改动公开 API 时，按 engine\docs\VERSIONING.md 的顺序做完：
#     ① 改代码  ② 决定升 minor 还是 major  ③ 改 Directory.Build.props 的 Version
#     ④ 补 CHANGELOG.md  ⑤ 才跑这个脚本更新快照
#   反过来（先跑脚本再补版本号）会让快照守卫退化成橡皮图章——守卫红的时候，
#   它想问的是"你知道自己在破坏兼容性吗"，而不是"要我帮你把红变绿吗"。
#
# 说明：
#   快照由测试程序集的 --public-api 模式打印，本脚本只负责把它写到正确的位置。
#   刻意不用 `> 文件` 重定向：在受限沙箱里那种写法会产出 0 字节文件（见 README 的环境说明）。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$testDll = Join-Path $root 'engine\tests\bin\Debug\net8.0\NekoClicker.Core.Tests.dll'
$target = Join-Path $root 'engine\core\PublicApi.txt'

if (-not (Test-Path $testDll)) {
    Write-Host '先构建一次（快照由测试程序集打印）：' -ForegroundColor Yellow
    & "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host '构建失败。' -ForegroundColor Red; exit $LASTEXITCODE }
}

# 用 cmd 的 > 重定向而不是 PowerShell 的：PowerShell 5.1 的 Set-Content 在无 BOM 时会
# 按控制台代码页写入，中文头部会被写坏（本仓库已经在这上面栽过一次）。
$lines = & "$PSScriptRoot\dnet.ps1" exec $testDll --public-api
if ($LASTEXITCODE -ne 0) { Write-Host '生成失败（--public-api 退出码非 0）。' -ForegroundColor Red; exit $LASTEXITCODE }

$tmp = "$target.tmp"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($tmp, $lines, $utf8NoBom)

# 快照进版本控制，行尾统一成 LF（与 .gitattributes 一致），避免 Windows/Linux 各生成一份。
$text = [System.IO.File]::ReadAllText($tmp).Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($tmp, $text, $utf8NoBom)
Move-Item -Force $tmp $target

$count = (Get-Content -Encoding UTF8 $target).Count
Write-Host "已更新 $target（$count 行）。" -ForegroundColor Green
Write-Host '别忘了跑一次 .\tools\build.ps1 -Strict 确认全绿。' -ForegroundColor DarkGray
