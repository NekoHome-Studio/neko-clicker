# dnet.ps1 — 在工作区内运行 dotnet CLI 的包装脚本。
#
# 为什么需要它（两个环境适配，都是"不这么做就跑不起来"的硬约束）：
#
# 1) dotnet CLI 首次运行会往 %USERPROFILE%\.dotnet 写 sentinel 文件，并默认把 NuGet
#    包缓存放进 %USERPROFILE%\.nuget\packages。在只写工作区的沙箱里这两个路径都不可写，
#    于是 CLI 会抛 UnauthorizedAccessException。这里把 CLI home 与包缓存重定向到仓库内。
#
# 2) MSBuild 的多进程节点复用靠命名管道通信，而受限沙箱不允许命名管道：一旦项目之间有
#    ProjectReference，构建会**静默失败**（输出 "Build FAILED" 但 0 Error）。
#    对策是给会调用 MSBuild 的动词强制加 -m:1（单节点、全进程内执行）。
#
# 用法:
#   pwsh -File tools/dnet.ps1 build
#   pwsh -File tools/dnet.ps1 run --project src/NekoClicker.Demo.Cli
#   pwsh -File tools/dnet.ps1 test

# 注意：这里刻意不使用 param() 块。声明参数会让脚本变成 advanced script，
# PowerShell 会把 -o / -n 之类当成公共参数（-OutVariable/-Name）解析并报歧义错误。
# 用 $args 原样透传才能把任意 dotnet 参数交给 CLI。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $root '.packages'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:MSBUILDDISABLENODEREUSE = '1'

New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES | Out-Null

$msbuildVerbs = @('build', 'restore', 'msbuild', 'test', 'publish', 'pack', 'clean')
$verb = if ($args.Count -gt 0) { [string]$args[0] } else { '' }
$rest = if ($args.Count -gt 1) { $args[1..($args.Count - 1)] } else { @() }

if ($msbuildVerbs -contains $verb) {
    & dotnet $verb '-m:1' @rest
}
else {
    & dotnet @args
}
exit $LASTEXITCODE
