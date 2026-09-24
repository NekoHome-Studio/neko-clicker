# 构建 + 测试一键脚本。
#
# 用法: pwsh -File tools/build.ps1
# 退出码 0 表示构建成功且全部测试通过。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host '=== 构建 ===' -ForegroundColor Cyan
& "$PSScriptRoot\dnet.ps1" build "$root\NekoClicker.sln" -v q --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host '构建失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host '=== 测试 ===' -ForegroundColor Cyan
& "$PSScriptRoot\dnet.ps1" exec "$root\tests\NekoClicker.Core.Tests\bin\Debug\net8.0\NekoClicker.Core.Tests.dll" @args
exit $LASTEXITCODE
