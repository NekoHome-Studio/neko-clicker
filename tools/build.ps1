# 构建 + 测试一键脚本。
#
# 用法:
#   pwsh -File tools/build.ps1            快速构建（增量）+ 全部测试
#   pwsh -File tools/build.ps1 -Strict    全量重编 + 警告即错误 + 全部测试（提交前跑）
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
$forward = @($args | Where-Object { $_ -ne '-Strict' })

$buildArgs = @("$root\NekoClicker.sln", '-v', 'q', '--nologo', '-warnaserror')
if ($strict) { $buildArgs += '--no-incremental' }

Write-Host '=== 构建 ===' -ForegroundColor Cyan
if (-not $strict) {
    Write-Host '（快速档：只编改动过的项目。提交前请跑一次 -Strict——全量重编才暴露得出被增量掩盖的警告）' -ForegroundColor DarkGray
}
& "$PSScriptRoot\dnet.ps1" build @buildArgs
if ($LASTEXITCODE -ne 0) {
    # 注意：-warnaserror 会把警告报成 **error**，所以失败时的摘要常是
    # "0 Warning(s) / N Error(s)"——别看警告计数，看退出码与上面的 error 行。
    Write-Host '构建失败（若摘要显示 0 Warning(s) 却失败，那是警告被 -warnaserror 计成了 error）。' -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host '=== 测试 ===' -ForegroundColor Cyan
& "$PSScriptRoot\dnet.ps1" exec "$root\tests\NekoClicker.Core.Tests\bin\Debug\net8.0\NekoClicker.Core.Tests.dll" @forward
exit $LASTEXITCODE
