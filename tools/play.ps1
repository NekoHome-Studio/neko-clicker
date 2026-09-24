# 构建并运行终端 Demo；后面跟的参数会原样转发给程序。
#
# 用法:
#   .\tools\play.ps1                                  交互模式
#   .\tools\play.ps1 --simulate 21600 --auto          无头模拟 6 小时
#   .\tools\play.ps1 --frame 118x32 --no-color        渲染一帧界面
#
# 为什么不用 `dotnet run`：它会触发多进程 MSBuild，而受限沙箱禁止命名管道通信，
# 结果是构建静默失败。这里改成"先 build，再 exec 产物"，行为等价且稳定。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\NekoClicker.Demo.Cli\NekoClicker.Demo.Cli.csproj'
$assembly = Join-Path $root 'src\NekoClicker.Demo.Cli\bin\Debug\net8.0\neko-clicker.dll'

& "$PSScriptRoot\dnet.ps1" build $project -v q --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Demo 构建失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

& "$PSScriptRoot\dnet.ps1" exec $assembly @args
exit $LASTEXITCODE
