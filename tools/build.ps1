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

# Web 宿主有自己独立的单项目 sln（故意的：它不跟引擎一起发布），所以主 sln 编不到它。
# 而"编不到"的后果是**静默**的：实测它曾经长时间停在 net10.0，而本机只有 SDK 8.0.303，
# 也就是**根本编不过**——却没有任何一条命令会红，直到有人真的去编它。
# 所以这一条必须由 build.ps1 兜住：一条命令验证全部，才有资格叫"一键"。
$webSln = Join-Path $root 'games\hosts\Web\NekoClicker.Web.sln'
if (Test-Path $webSln) {
    Write-Host ''
    Write-Host '=== 构建 Web 宿主（独立 sln）===' -ForegroundColor Cyan
    & "$PSScriptRoot\dnet.ps1" build $webSln -v q --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Web 宿主构建失败。它有自己的 sln，主 sln 编不到它——这正是这一步存在的理由。' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# 前端冒烟（tools/web-smoke.mjs）：无头 DOM 桩件把 wwwroot/app.js **真的跑一遍**。
#
# 为什么它必须在这一条命令里（STRUCTURE_OPTIMIZATION §S1）：它自 2026-10 起有 162 条断言（135 → 162
# 是 2026-10-04 加的存档导出/导入窗口那一节），
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
& "$PSScriptRoot\dnet.ps1" exec "$root\engine\tests\bin\Debug\net8.0\NekoClicker.Core.Tests.dll" @forward
exit $LASTEXITCODE
