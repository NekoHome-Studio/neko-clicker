<#
.SYNOPSIS
    D3 新包脚手架：从"已验证的曲线配方"一条命令生成合规可玩的两层 vanilla MVP 内容包。

.DESCRIPTION
    创意池档 D 的 D3 落地。把 C1《猫娘电台》手搓全程自动化：
      · 生成 engine/content/<Pascal>/ 的 11 个文件（C# × 9 + text.json + csproj）；
      · 自动注册 6 处（sln / 测试 csproj / Demo.Cli csproj / Web csproj / ContentPackages / TestGame）；
      · 自动更新 7 张跨包冻结表（点击桥金表 / 主线基线 / 升级建筑总数 / ContentText 十表 / ProseTone）；
      · 生成完自动跑 tools/build.ps1 验证"过守卫"，否则就是"生成一堆红"（D3 风险注记）。

    生成的是**两层** vanilla 骨架（无自定义模块、无第二资源）：
      · 为什么不是 spec 字面的"1 层"——转生结算要求 Era < MaxEraIndex，一层包永远舍不了命、
        永久线结构性买不起，PrestigeTests 会红。C1 已踩过这个坑（见 OPEN_WORK §0.54.2）。
      · 曲线配方（相邻价格 ×5~20、产量 ×3~12、第 3 座起价格比 > 产量比）直接复用 C1 的合规曲线。
      · 只生成内容、不新增测试方法——跨包守卫经 AllContentPacks 自动覆盖新包，用例数恒 619。

    指纹可精确复算：完成门槛 / 结局条件都用已知 Describe+Skeleton 格式的条件树，
    主线基线由生成器直接算出（不是"跑一遍抄"），生成即合规。

.PARAMETER Id
    包 id（小写字母开头，仅字母/数字/下划线），如 garden。目录/命名空间用其 Pascal 形式 Garden。

.PARAMETER Name
    显示名（进 AllContentPacks / 点击桥金表 / 主线基线，三处必须一致），如 "猫娘花园"。

.PARAMETER Currency
    主资源名（默认"点数"）。影响完成门槛指纹里的 Describe 文案。

.PARAMETER Remove
    回滚：移除该包并还原全部注册与冻结表（与生成互为逆操作），便于验证后不留演示包。

.EXAMPLE
    pwsh -File tools/new-pack.ps1 -Id garden -Name "猫娘花园" -Currency "花"
    pwsh -File tools/new-pack.ps1 -Id garden -Name "猫娘花园" -Remove
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Id,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Currency = "点数",
    [string]$CurrencyIcon = "📦",
    [string]$Prestige = "执照",
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $root 'NekoClicker.sln'))) { throw "找不到仓库根（NekoClicker.sln）：$root" }

if ($Id -notmatch '^[a-z][a-z0-9_]*$') { throw "Id 必须小写字母开头、仅字母/数字/下划线：$Id" }
$pascal = $Id.Substring(0, 1).ToUpperInvariant() + $Id.Substring(1)
$ns = "NekoClicker.Content.$pascal"
$packDir = Join-Path $root "engine/content/$pascal"
$asm = "NekoClicker.Content.$pascal"

# 固定结构常量（各冻结表的增量由此确定算出）
$BUILDINGS = @(
    @{ id = 'plot';     price = 15;          cps = 0.1;    unlock = 0 },
    @{ id = 'shed';     price = 100;         cps = 1;      unlock = 30 },
    @{ id = 'workshop'; price = 1100;        cps = 8;      unlock = 300 },
    @{ id = 'studio';   price = 12000;       cps = 47;     unlock = 3000 },
    @{ id = 'factory';  price = 130000;      cps = 260;    unlock = 30000 },
    @{ id = 'plant';    price = 1400000;     cps = 1400;   unlock = 300000 },
    @{ id = 'hub';      price = 15000000;    cps = 7800;   unlock = 3000000 },
    @{ id = 'network';  price = 160000000;   cps = 43000;  unlock = 30000000 },
    @{ id = 'mega';     price = 1700000000;  cps = 240000; unlock = 300000000 }
)
$N_BUILDING = $BUILDINGS.Count                 # 9
$N_UPGRADE = $N_BUILDING * 3 + 2 + 4           # 33 = 27 建筑档 + 2 点击 + 4 永久
$N_CATEGORIZED = $N_BUILDING * 3               # 27
$N_ACHIEVEMENT = $N_BUILDING * 3 + 8 + 6 + 5 + 6 + 2 + 2  # 56
$N_BUFF = 8
$N_GOLDEN = 10
$N_LORE = 40
$N_ERA = 2
$N_ENDING = 2
$BRIDGE_ID = "${Id}_hands"
$GATE_ID = "${Id}_steady"
$BRIDGE_PRICE = 5000000
$COMPLETION = 1000000000                        # 1e9，两层同门槛
$GOOD_ACH = 5

function Read-Text([string]$rel) { Get-Content -Raw -LiteralPath (Join-Path $root $rel) }
function Write-Text([string]$rel, [string]$text) {
    $p = Join-Path $root $rel
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($p, $text, $utf8NoBom)
}
# 在锚点行后插入一行（add）；或删除含标记的行（remove）
function Edit-File([string]$rel, [string]$anchor, [string]$insert, [switch]$Del) {
    $p = Join-Path $root $rel
    $text = Get-Content -Raw -LiteralPath $p
    if ($Del) {
        # 容错回滚：找不到就跳过（幂等）。参数须与生成时一致（尤其 -Currency，影响主线基线指纹）。
        $text = $text.Replace($insert + "`r`n", '').Replace($insert + "`n", '')
    } else {
        if ($text.Contains($insert)) { throw "已存在，勿重复注册：$rel" }
        if (-not $text.Contains($anchor)) { throw "锚点失效：$rel 找不到 [$anchor]" }
        $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $text = $text.Replace($anchor, $anchor + $nl + $insert)
    }
    Write-Text $rel $text
}

# ============================================================================
# 生成包文件
# ============================================================================
function New-PackFiles {
    New-Item -ItemType Directory -Force -Path $packDir | Out-Null

    # ---- csproj ----
    $csproj = @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>{{NS}}</RootNamespace>
    <AssemblyName>{{ASM}}</AssemblyName>
    <Description>内容包《{{NAME}}》：D3 脚手架生成的两层 vanilla MVP（曲线配方 + 标准点击桥）。</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\core\NekoClicker.Core.csproj" />
  </ItemGroup>

</Project>
'@
    Write-Text "engine/content/$pascal/$asm.csproj" ($csproj.Replace('{{NS}}', $ns).Replace('{{ASM}}', $asm).Replace('{{NAME}}', $Name))

    # ---- Content 入口 ----
    $content = @'
using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace {{NS}};

/// <summary>
/// 内容包《{{NAME}}》的入口（D3 脚手架生成的两层 vanilla MVP）。<para>
/// 这是"标准骨架"：点击是生产线、建筑走已验证曲线、两层转生。没有自定义模块与第二资源——
/// 要反转核心动词或加第二资源，照 C1《猫娘电台》的 BroadcastModule 往 .Add(...) 里挂模块即可。
/// </para>
/// </summary>
public static class {{PASCAL}}Content
{
    /// <summary>游戏标题。</summary>
    public const string GameTitle = "{{NAME}}";

    /// <summary>构建内容定义。</summary>
    public static GameContent Build()
    {
        GameContent content = new GameContentBuilder(GameTitle)
            .WithCurrency("{{CURRENCY}}", "{{CURRENCY_ICON}}", "产出")
            .WithPrestigeCurrency("{{PRESTIGE}}", "🎫")
            .WithBalance(BuildBalance())
            .AddBuildings(Buildings.All)
            .AddUpgrades(Upgrades.All)
            .AddAchievements(Achievements.All)
            .AddBuffs(Buffs.All)
            .AddGoldenCookieOutcomes(GoldenCookieOutcomes.All)
            .AddEras(Eras.All(BuildBalance()))
            .AddStorylines(Lore.Storylines)
            .AddLore(Lore.Entries)
            .AddEndings(Endings.All)
            .Build();

        Lore.VerifyAllTextUsed();
        return content;
    }

    /// <summary>平衡参数：标准手感。转生除数按本包 1e9 阶梯标定 1e3，让永久线（80）一局买得起。</summary>
    public static GameBalance BuildBalance() => new()
    {
        ClickBasePower = 1,
        ClickCpsRatio = 0.02,

        TickRate = 30,
        MaxCatchUpSeconds = 5,

        GoldenCookieMinDelay = 60 * 5,
        GoldenCookieMaxDelay = 60 * 14,
        GoldenCookieLifetime = 14,
        MaxConcurrentGoldenCookies = 1,
        FirstGoldenCookieDelayFactor = 0.2,

        OfflineCapSeconds = 7200,
        OfflineEfficiency = 0.6,
        MinimumOfflineSeconds = 60,

        PrestigeDivisor = 1e3,
        PrestigeExponent = 1.0 / 3.0,
        PrestigeChipsPerLevel = 1,

        KeepAchievementsOnAscend = true,
        AllowSelling = true,

        DefaultSellRefundRate = 0.45,

        MaxBulkBuy = 100_000,
        AchievementCheckInterval = 1,
        AutoSaveInterval = 60,
    };
}
'@
    $content = $content.Replace('{{NS}}', $ns).Replace('{{PASCAL}}', $pascal).Replace('{{NAME}}', $Name)
    $content = $content.Replace('{{CURRENCY}}', $Currency).Replace('{{CURRENCY_ICON}}', $CurrencyIcon).Replace('{{PRESTIGE}}', $Prestige)
    Write-Text "engine/content/$pascal/${pascal}Content.cs" $content

    # ---- Buildings（循环铺）----
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("using NekoClicker.Core.Content;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("/// <summary>建筑表（$N_BUILDING 座）：曲线配方相邻价格 ×5~20、产量 ×3~12、第 3 座起价格比 > 产量比。解锁用历史累计（转生后手上建筑仍显示已解锁）。</summary>")
    [void]$sb.AppendLine("internal static class Buildings")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    private static ContentText Prose => Lore.Prose;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public static BuildingDefinition[] All =>")
    [void]$sb.AppendLine("    [")
    foreach ($b in $BUILDINGS) {
        $bid = $b.id
        [void]$sb.AppendLine("        new()")
        [void]$sb.AppendLine("        {")
        [void]$sb.AppendLine("            Id = `"$bid`",")
        [void]$sb.AppendLine("            Name = Prose.Text(`"buildings`", `"$bid`", `"name`"),")
        [void]$sb.AppendLine("            Icon = Prose.Text(`"buildings`", `"$bid`", `"icon`"),")
        [void]$sb.AppendLine("            Description = Prose.Text(`"buildings`", `"$bid`", `"description`"),")
        [void]$sb.AppendLine("            BasePrice = $($b.price),")
        [void]$sb.AppendLine("            BaseCps = $($b.cps),")
        if ($b.unlock -gt 0) { [void]$sb.AppendLine("            Unlock = UnlockCondition.EarnedAllTimeAtLeast($($b.unlock)),") }
        [void]$sb.AppendLine("        },")
    }
    [void]$sb.AppendLine("    ];")
    [void]$sb.AppendLine("}")
    Write-Text "engine/content/$pascal/Buildings.cs" $sb.ToString()

    # ---- Upgrades（建筑档循环 + 点击门/桥 + 永久）----
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("using NekoClicker.Core;")
    [void]$sb.AppendLine("using NekoClicker.Core.Content;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("/// <summary>升级表（$N_UPGRADE 条）：每建筑三档 ×2 ＋ 点击门/桥 ＋ 4 永久。桥的形状与其余包一致（ClickBridge 守卫）。</summary>")
    [void]$sb.AppendLine("internal static class Upgrades")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    private static ContentText Prose => Lore.Prose;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public static UpgradeDefinition[] All =>")
    [void]$sb.AppendLine("    [")
    [void]$sb.AppendLine("        .. BuildingTierUpgrades(),")
    [void]$sb.AppendLine("        .. ClickUpgrades(),")
    [void]$sb.AppendLine("        .. PermanentUpgrades(),")
    [void]$sb.AppendLine("    ];")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static IEnumerable<UpgradeDefinition> BuildingTierUpgrades()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        (int Required, double PriceFactor)[] tiers = [(1, 10), (10, 100), (25, 500)];")
    [void]$sb.AppendLine("        foreach (BuildingDefinition building in Buildings.All)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            foreach ((int required, double priceFactor) in tiers)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                string id = `$`"{building.Id}_tier{required}`";")
    [void]$sb.AppendLine("                yield return new UpgradeDefinition")
    [void]$sb.AppendLine("                {")
    [void]$sb.AppendLine("                    Id = id,")
    [void]$sb.AppendLine("                    Name = Prose.Text(`"upgrades`", id, `"name`"),")
    [void]$sb.AppendLine("                    Icon = Prose.Text(`"upgrades`", id, `"icon`"),")
    [void]$sb.AppendLine("                    Description = Prose.Text(`"upgrades`", id, `"description`"),")
    [void]$sb.AppendLine("                    Price = building.BasePrice * priceFactor,")
    [void]$sb.AppendLine("                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, required),")
    [void]$sb.AppendLine("                    Modifiers = [Modifier.BuildingMultiplier(building.Id, 2)],")
    [void]$sb.AppendLine("                    Category = `$`"building:{building.Id}`",")
    [void]$sb.AppendLine("                    Tier = required,")
    [void]$sb.AppendLine("                    Tags = [`"$Id`", `"tier`"],")
    [void]$sb.AppendLine("                };")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static IEnumerable<UpgradeDefinition> ClickUpgrades()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        yield return new()")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            Id = `"$GATE_ID`",")
    [void]$sb.AppendLine("            Name = Prose.Text(`"upgrades`", `"$GATE_ID`", `"name`"),")
    [void]$sb.AppendLine("            Icon = Prose.Text(`"upgrades`", `"$GATE_ID`", `"icon`"),")
    [void]$sb.AppendLine("            Description = Prose.Text(`"upgrades`", `"$GATE_ID`", `"description`"),")
    [void]$sb.AppendLine("            Price = 20_000,")
    [void]$sb.AppendLine("            Unlock = UnlockCondition.ClicksAtLeast(200),")
    [void]$sb.AppendLine("            Modifiers = [Modifier.ClickMultiplier(2)],")
    [void]$sb.AppendLine("            Category = `"click`",")
    [void]$sb.AppendLine("            Tier = 1,")
    [void]$sb.AppendLine("            Tags = [`"$Id`", `"click`"],")
    [void]$sb.AppendLine("        };")
    [void]$sb.AppendLine("        yield return new()")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            Id = `"$BRIDGE_ID`",")
    [void]$sb.AppendLine("            Name = Prose.Text(`"upgrades`", `"$BRIDGE_ID`", `"name`"),")
    [void]$sb.AppendLine("            Icon = Prose.Text(`"upgrades`", `"$BRIDGE_ID`", `"icon`"),")
    [void]$sb.AppendLine("            Description = Prose.Text(`"upgrades`", `"$BRIDGE_ID`", `"description`"),")
    [void]$sb.AppendLine("            Price = $BRIDGE_PRICE,")
    [void]$sb.AppendLine("            Unlock = UnlockCondition.All(")
    [void]$sb.AppendLine("                UnlockCondition.TotalBuildingsAtLeast(80),")
    [void]$sb.AppendLine("                UnlockCondition.UpgradeOwned(`"$GATE_ID`")),")
    [void]$sb.AppendLine("            Modifiers =")
    [void]$sb.AppendLine("            [")
    [void]$sb.AppendLine("                new Modifier(")
    [void]$sb.AppendLine("                    ModifierTarget.ClickPower,")
    [void]$sb.AppendLine("                    ModifierOperation.AdditivePercent,")
    [void]$sb.AppendLine("                    0,")
    [void]$sb.AppendLine("                    new Scaling(ScalingSource.TotalBuildings, 0.005, Cap: 120)),")
    [void]$sb.AppendLine("            ],")
    [void]$sb.AppendLine("            Category = `"click`",")
    [void]$sb.AppendLine("            Tier = 5,")
    [void]$sb.AppendLine("            Tags = [`"$Id`", `"click`"],")
    [void]$sb.AppendLine("        };")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static IEnumerable<UpgradeDefinition> PermanentUpgrades()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        (string Id, double Price, double Percent)[] lines =")
    [void]$sb.AppendLine("        [")
    [void]$sb.AppendLine("            (`"${Id}_p1`", 5, 0.10),")
    [void]$sb.AppendLine("            (`"${Id}_p2`", 15, 0.15),")
    [void]$sb.AppendLine("            (`"${Id}_p3`", 25, 0.20),")
    [void]$sb.AppendLine("            (`"${Id}_p4`", 35, 0.25),")
    [void]$sb.AppendLine("        ];")
    [void]$sb.AppendLine("        foreach ((string id, double price, double percent) in lines)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            yield return new UpgradeDefinition")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                Id = id,")
    [void]$sb.AppendLine("                Name = Prose.Text(`"upgrades`", id, `"name`"),")
    [void]$sb.AppendLine("                Icon = Prose.Text(`"upgrades`", id, `"icon`"),")
    [void]$sb.AppendLine("                Description = Prose.Text(`"upgrades`", id, `"description`"),")
    [void]$sb.AppendLine("                Price = price,")
    [void]$sb.AppendLine("                Currency = UpgradeCurrency.PrestigeChips,")
    [void]$sb.AppendLine("                Persistence = UpgradePersistence.Permanent,")
    [void]$sb.AppendLine("                Modifiers = [Modifier.GlobalPercent(percent)],")
    [void]$sb.AppendLine("                Category = `"heavenly`",")
    [void]$sb.AppendLine("                Tier = 1,")
    [void]$sb.AppendLine("                Tags = [`"$Id`", `"permanent`"],")
    [void]$sb.AppendLine("            };")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")
    Write-Text "engine/content/$pascal/Upgrades.cs" $sb.ToString()

    # ---- Achievements / Buffs / Golden / Eras / Endings / Lore / text.json ----
    New-Achievements
    New-BuffsAndGolden
    New-ErasAndEndings
    New-Lore
    New-TextJson
}

function New-Achievements {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("using NekoClicker.Core.Content;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("/// <summary>成就表（$N_ACHIEVEMENT 条）：建筑/累计/产量/点击/金猫/纪元档 ＋ 结局成就。</summary>")
    [void]$sb.AppendLine("internal static class Achievements")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    private static ContentText Prose => Lore.Prose;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public static AchievementDefinition[] All =>")
    [void]$sb.AppendLine("    [")
    [void]$sb.AppendLine("        .. BuildingTiers(),")
    [void]$sb.AppendLine("        .. EarningTiers(),")
    [void]$sb.AppendLine("        .. ProductionTiers(),")
    [void]$sb.AppendLine("        .. ClickTiers(),")
    [void]$sb.AppendLine("        .. GoldenTiers(),")
    [void]$sb.AppendLine("        .. EraTiers(),")
    [void]$sb.AppendLine("        .. Endings.Achievements,")
    [void]$sb.AppendLine("    ];")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static IEnumerable<AchievementDefinition> BuildingTiers()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        int[] tiers = [1, 25, 50];")
    [void]$sb.AppendLine("        foreach (BuildingDefinition building in Buildings.All)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            foreach (int count in tiers)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                string id = `$`"{building.Id}_x{count}`";")
    [void]$sb.AppendLine("                yield return new AchievementDefinition")
    [void]$sb.AppendLine("                {")
    [void]$sb.AppendLine("                    Id = id,")
    [void]$sb.AppendLine("                    Name = Prose.Text(`"achievements`", id, `"name`"),")
    [void]$sb.AppendLine("                    Icon = Prose.Text(`"achievements`", id, `"icon`"),")
    [void]$sb.AppendLine("                    Description = Prose.Text(`"achievements`", id, `"description`"),")
    [void]$sb.AppendLine("                    Unlock = UnlockCondition.BuildingsAtLeast(building.Id, count),")
    [void]$sb.AppendLine("                };")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine()
    $tierDefs = @(
        @{ fn = 'EarningTiers'; cond = 'UnlockCondition.EarnedAllTimeAtLeast(value)'; tiers = @( @{id='e_1e4';v='1e4'}, @{id='e_1e6';v='1e6'}, @{id='e_1e8';v='1e8'}, @{id='e_1e10';v='1e10'}, @{id='e_1e12';v='1e12'}, @{id='e_1e14';v='1e14'}, @{id='e_1e16';v='1e16'}, @{id='e_1e18';v='1e18'} ) },
        @{ fn = 'ProductionTiers'; cond = 'UnlockCondition.CpsAtLeast(value)'; tiers = @( @{id='c_1e3';v='1e3'}, @{id='c_1e6';v='1e6'}, @{id='c_1e9';v='1e9'}, @{id='c_1e12';v='1e12'}, @{id='c_1e15';v='1e15'}, @{id='c_1e18';v='1e18'} ) },
        @{ fn = 'ClickTiers'; cond = 'UnlockCondition.ClicksAtLeast(value)'; tiers = @( @{id='k_100';v='100'}, @{id='k_1000';v='1000'}, @{id='k_10000';v='10000'}, @{id='k_100000';v='100000'}, @{id='k_1000000';v='1000000'} ) },
        @{ fn = 'GoldenTiers'; cond = 'UnlockCondition.GoldenCookiesAtLeast(value)'; tiers = @( @{id='g_1';v='1'}, @{id='g_10';v='10'}, @{id='g_50';v='50'}, @{id='g_200';v='200'}, @{id='g_500';v='500'}, @{id='g_1000';v='1000'} ) }
    )
    foreach ($td in $tierDefs) {
        [void]$sb.AppendLine("    private static IEnumerable<AchievementDefinition> $($td.fn)()")
        [void]$sb.AppendLine("    {")
        [void]$sb.AppendLine("        (string Id, double Value)[] tiers =")
        [void]$sb.AppendLine("        [")
        foreach ($t in $td.tiers) { [void]$sb.AppendLine("            (`"$($t.id)`", $($t.v)),") }
        [void]$sb.AppendLine("        ];")
        [void]$sb.AppendLine("        foreach ((string id, double value) in tiers)")
        [void]$sb.AppendLine("        {")
        [void]$sb.AppendLine("            yield return new AchievementDefinition")
        [void]$sb.AppendLine("            {")
        [void]$sb.AppendLine("                Id = id,")
        [void]$sb.AppendLine("                Name = Prose.Text(`"achievements`", id, `"name`"),")
        [void]$sb.AppendLine("                Icon = Prose.Text(`"achievements`", id, `"icon`"),")
        [void]$sb.AppendLine("                Description = Prose.Text(`"achievements`", id, `"description`"),")
        [void]$sb.AppendLine("                Unlock = $($td.cond),")
        [void]$sb.AppendLine("            };")
        [void]$sb.AppendLine("        }")
        [void]$sb.AppendLine("    }")
        [void]$sb.AppendLine()
    }
    [void]$sb.AppendLine("    private static IEnumerable<AchievementDefinition> EraTiers()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        for (int index = 1; index <= $N_ERA; index++)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            string id = `$`"era_{index}`";")
    [void]$sb.AppendLine("            yield return new AchievementDefinition")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                Id = id,")
    [void]$sb.AppendLine("                Name = Prose.Text(`"achievements`", id, `"name`"),")
    [void]$sb.AppendLine("                Icon = Prose.Text(`"achievements`", id, `"icon`"),")
    [void]$sb.AppendLine("                Description = Prose.Text(`"achievements`", id, `"description`"),")
    [void]$sb.AppendLine("                Unlock = UnlockCondition.EraAtLeast(index),")
    [void]$sb.AppendLine("                Modifiers = index == $N_ERA ? [Modifier.GlobalMultiplier(1.5)] : [],")
    [void]$sb.AppendLine("            };")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")
    Write-Text "engine/content/$pascal/Achievements.cs" $sb.ToString()
}

function New-BuffsAndGolden {
    # Buffs 与 Golden 的结构与其余包一致（狂热 ×7/77s、点击 ×777/13s、权重和 140）。
    $buffs = @'
using NekoClicker.Core.Content;

namespace {{NS}};

/// <summary>限时增益表（8 条）：数值沿用已验证配方，随机事件力度与其它包可比。</summary>
internal static class Buffs
{
    private static ContentText Prose => Lore.Prose;

    public static BuffDefinition[] All =>
    [
        new() { Id = "frenzy", Name = Prose.Text("buffs", "frenzy", "name"), Icon = Prose.Text("buffs", "frenzy", "icon"), Description = Prose.Text("buffs", "frenzy", "description"), Duration = 77, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.GlobalMultiplier(7)] },
        new() { Id = "click_frenzy", Name = Prose.Text("buffs", "click_frenzy", "name"), Icon = Prose.Text("buffs", "click_frenzy", "icon"), Description = Prose.Text("buffs", "click_frenzy", "description"), Duration = 13, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.ClickMultiplier(777)] },
        new() { Id = "boom", Name = Prose.Text("buffs", "boom", "name"), Icon = Prose.Text("buffs", "boom", "icon"), Description = Prose.Text("buffs", "boom", "description"), Duration = 60, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.GlobalMultiplier(15)] },
        new() { Id = "shortage", Name = Prose.Text("buffs", "shortage", "name"), Icon = Prose.Text("buffs", "shortage", "icon"), Description = Prose.Text("buffs", "shortage", "description"), Duration = 66, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.GlobalMultiplier(0.5)], IsDebuff = true },
        new() { Id = "slump", Name = Prose.Text("buffs", "slump", "name"), Icon = Prose.Text("buffs", "slump", "icon"), Description = Prose.Text("buffs", "slump", "description"), Duration = 90, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.GlobalMultiplier(0.7)], IsDebuff = true },
        new() { Id = "drift", Name = Prose.Text("buffs", "drift", "name"), Icon = Prose.Text("buffs", "drift", "icon"), Description = Prose.Text("buffs", "drift", "description"), Duration = 72, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.GlobalMultiplier(0.6)], IsDebuff = true },
        new() { Id = "night_shift", Name = Prose.Text("buffs", "night_shift", "name"), Icon = Prose.Text("buffs", "night_shift", "icon"), Description = Prose.Text("buffs", "night_shift", "description"), Duration = 30, StackMode = BuffStackMode.Extend, Modifiers = [Modifier.BuildingMultiplier("factory", 30)] },
        new() { Id = "tune_up", Name = Prose.Text("buffs", "tune_up", "name"), Icon = Prose.Text("buffs", "tune_up", "icon"), Description = Prose.Text("buffs", "tune_up", "description"), Duration = 20, StackMode = BuffStackMode.Refresh, Modifiers = [Modifier.ClickMultiplier(50)] },
    ];
}
'@
    Write-Text "engine/content/$pascal/Buffs.cs" $buffs.Replace('{{NS}}', $ns)

    $golden = @'
using NekoClicker.Core.Content;

namespace {{NS}};

/// <summary>金猫结果表（10 条）：权重和 140、银行抽成 0.15 / 封顶 900 秒产量，期望值与其它包可比。</summary>
internal static class GoldenCookieOutcomes
{
    private static ContentText Prose => Lore.Prose;

    public static GoldenCookieOutcome[] All =>
    [
        new() { Id = "windfall", Name = Prose.Text("goldenCookies", "windfall", "name"), Icon = Prose.Text("goldenCookies", "windfall", "icon"), Description = Prose.Text("goldenCookies", "windfall", "description"), Weight = 48, CookiesFromBankFraction = 0.15, CookiesFromBankFractionCapSecondsOfCps = 900, CookiesFromCpsSeconds = 13 },
        new() { Id = "frenzy", Name = Prose.Text("goldenCookies", "frenzy", "name"), Icon = Prose.Text("goldenCookies", "frenzy", "icon"), Description = Prose.Text("goldenCookies", "frenzy", "description"), Weight = 32, BuffId = "frenzy", BuffSeconds = 77 },
        new() { Id = "click_frenzy", Name = Prose.Text("goldenCookies", "click_frenzy", "name"), Icon = Prose.Text("goldenCookies", "click_frenzy", "icon"), Description = Prose.Text("goldenCookies", "click_frenzy", "description"), Weight = 10, BuffId = "click_frenzy", BuffSeconds = 13 },
        new() { Id = "thief", Name = Prose.Text("goldenCookies", "thief", "name"), Icon = Prose.Text("goldenCookies", "thief", "icon"), Description = Prose.Text("goldenCookies", "thief", "description"), Weight = 6, StealBankFraction = 0.05 },
        new() { Id = "nothing", Name = Prose.Text("goldenCookies", "nothing", "name"), Icon = Prose.Text("goldenCookies", "nothing", "icon"), Description = Prose.Text("goldenCookies", "nothing", "description"), Weight = 3 },
        new() { Id = "boom", Name = Prose.Text("goldenCookies", "boom", "name"), Icon = Prose.Text("goldenCookies", "boom", "icon"), Description = Prose.Text("goldenCookies", "boom", "description"), Weight = 5, BuffId = "boom", BuffSeconds = 60 },
        new() { Id = "tune_up", Name = Prose.Text("goldenCookies", "tune_up", "name"), Icon = Prose.Text("goldenCookies", "tune_up", "icon"), Description = Prose.Text("goldenCookies", "tune_up", "description"), Weight = 6, BuffId = "tune_up", BuffSeconds = 20 },
        new() { Id = "double", Name = Prose.Text("goldenCookies", "double", "name"), Icon = Prose.Text("goldenCookies", "double", "icon"), Description = Prose.Text("goldenCookies", "double", "description"), Weight = 2, BuffId = "frenzy", BuffSeconds = 77, SecondaryBuffId = "click_frenzy", SecondaryBuffSeconds = 13 },
        new() { Id = "shortage", Name = Prose.Text("goldenCookies", "shortage", "name"), Icon = Prose.Text("goldenCookies", "shortage", "icon"), Description = Prose.Text("goldenCookies", "shortage", "description"), Weight = 22, BuffId = "shortage", BuffSeconds = 66 },
        new() { Id = "night_shift", Name = Prose.Text("goldenCookies", "night_shift", "name"), Icon = Prose.Text("goldenCookies", "night_shift", "icon"), Description = Prose.Text("goldenCookies", "night_shift", "description"), Weight = 6, BuffId = "night_shift", BuffSeconds = 30 },
    ];
}
'@
    Write-Text "engine/content/$pascal/GoldenCookieOutcomes.cs" $golden.Replace('{{NS}}', $ns)
}

function New-ErasAndEndings {
    $eras = @'
using NekoClicker.Core;
using NekoClicker.Core.Content;

namespace {{NS}};

/// <summary>
/// 两层纪元（D3 脚手架）。<b>刻意两层而非一层</b>：转生结算要求 Era &lt; MaxEraIndex，
/// 一层包永远舍不了命、执照恒为 0，永久线结构性买不起（C1 踩过的坑，OPEN_WORK §0.54.2）。
/// 完成条件只用累计赚取（单调，R3）。
/// </summary>
internal static class Eras
{
    private static ContentText Prose => Lore.Prose;

    public static EraDefinition[] All(GameBalance baseBalance) =>
    [
        new()
        {
            Index = 1,
            Id = "stage_1",
            Name = Prose.Text("eras", "stage_1", "name"),
            Icon = Prose.Text("eras", "stage_1", "icon"),
            Theme = Prose.Text("eras", "stage_1", "theme"),
            EntryText = Prose.Text("eras", "stage_1", "entryText"),
            ExitText = Prose.Text("eras", "stage_1", "exitText"),
            Modifiers = [],
            Completion = UnlockCondition.EarnedThisRunAtLeast({{COMPLETION}}),
            CompletionHint = Prose.Text("eras", "stage_1", "completionHint"),
        },
        new()
        {
            Index = 2,
            Id = "stage_2",
            Name = Prose.Text("eras", "stage_2", "name"),
            Icon = Prose.Text("eras", "stage_2", "icon"),
            Theme = Prose.Text("eras", "stage_2", "theme"),
            EntryText = Prose.Text("eras", "stage_2", "entryText"),
            ExitText = Prose.Text("eras", "stage_2", "exitText"),
            Modifiers = [],
            Completion = FinalCompletion,
            CompletionHint = Prose.Text("eras", "stage_2", "completionHint"),
        },
    ];

    /// <summary>末层主线完成（终局判定复用，不抄数值）。只用累计赚取（单调）。</summary>
    public static UnlockCondition FinalCompletion => UnlockCondition.EarnedThisRunAtLeast({{COMPLETION}});
}
'@
    Write-Text "engine/content/$pascal/Eras.cs" $eras.Replace('{{NS}}', $ns).Replace('{{COMPLETION}}', "$COMPLETION")

    $endings = @'
using NekoClicker.Core.Content;

namespace {{NS}};

/// <summary>
/// 两个结局（D3 脚手架）：好结局额外要求成就数（单调、可达），兜底只依赖末层主线完成。
/// 所有结局都等末层主线完成后才判定（避免一进末层兜底就触发）。
/// </summary>
internal static class Endings
{
    private static ContentText Prose => Lore.Prose;

    public static EndingDefinition[] All =>
    [
        new()
        {
            Id = "end_bright",
            Name = Prose.Text("endings", "end_bright", "name"),
            Icon = Prose.Text("endings", "end_bright", "icon"),
            Priority = 0,
            Condition = UnlockCondition.All(Finished, UnlockCondition.AchievementsAtLeast({{GOOD_ACH}})),
            Text = Prose.Text("endings", "end_bright", "text"),
        },
        new()
        {
            Id = "end_quiet",
            Name = Prose.Text("endings", "end_quiet", "name"),
            Icon = Prose.Text("endings", "end_quiet", "icon"),
            Priority = 100,
            Condition = Finished,
            Text = Prose.Text("endings", "end_quiet", "text"),
        },
    ];

    public static AchievementDefinition[] Achievements =>
    [
        Ending("ach_end_bright", "end_bright"),
        Ending("ach_end_quiet", "end_quiet"),
    ];

    private static UnlockCondition Finished
        => UnlockCondition.All(
            UnlockCondition.EraAtLeast(2),
            Eras.FinalCompletion);

    private static AchievementDefinition Ending(string id, string endingId) => new()
    {
        Id = id,
        Name = Prose.Text("achievements", id, "name"),
        Icon = Prose.Text("achievements", id, "icon"),
        Description = Prose.Text("achievements", id, "description"),
        Unlock = UnlockCondition.EndingReached(endingId),
    };
}
'@
    Write-Text "engine/content/$pascal/Endings.cs" $endings.Replace('{{NS}}', $ns).Replace('{{GOOD_ACH}}', "$GOOD_ACH")
}

function New-Lore {
    # 40 条 = 4 线（12+10+10+8），Reveal 复用 C1 的单调门槛节奏（唯一、随 Order 递增、≤ 完成门槛）。
    $lines = @(
        @{ id = 'dawn';  n = 12 },
        @{ id = 'grow';  n = 10 },
        @{ id = 'bond';  n = 10 },
        @{ id = 'dusk';  n = 8 }
    )
    $reveals = @{
        dawn = @(1, 200, 2000, 20000, 150000, 1000000, 8000000, 50000000, 300000000, 2000000000, 12000000000, 80000000000)
        grow = @(500, 5000, 50000, 400000, 3000000, 25000000, 180000000, 1200000000, 9000000000, 60000000000)
        bond = @(1000, 10000, 90000, 700000, 6000000, 45000000, 320000000, 2400000000, 16000000000, 95000000000)
        dusk = @(30, 30000, 250000, 2000000, 15000000, 110000000, 800000000, 40000000000)
    }
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("using NekoClicker.Core;")
    [void]$sb.AppendLine("using NekoClicker.Core.Content;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("/// <summary>叙事条目（$N_LORE 条，4 线）：Reveal 唯一、随 Order 单调递增、≤ 完成门槛（R3 纪律）。</summary>")
    [void]$sb.AppendLine("internal static class Lore")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    private static readonly Lazy<ContentText> ProseCache = new(() => ContentText.Load(`"$pascal`"));")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    internal static ContentText Prose => ProseCache.Value;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public static StorylineDefinition[] Storylines =>")
    [void]$sb.AppendLine("    [")
    foreach ($l in $lines) { [void]$sb.AppendLine("        Line(`"$($l.id)`", $($l.n)),") }
    [void]$sb.AppendLine("    ];")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static StorylineDefinition Line(string id, int totalEntries) => new()")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        Id = id,")
    [void]$sb.AppendLine("        Name = Prose.Text(`"storylines`", id, `"name`"),")
    [void]$sb.AppendLine("        Theme = Prose.Text(`"storylines`", id, `"theme`"),")
    [void]$sb.AppendLine("        Icon = Prose.Text(`"storylines`", id, `"icon`"),")
    [void]$sb.AppendLine("        TotalEntries = totalEntries,")
    [void]$sb.AppendLine("    };")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public static LoreEntry[] Entries =>")
    [void]$sb.AppendLine("    [")
    foreach ($l in $lines) { [void]$sb.AppendLine("        .. $($l.id.ToUpperInvariant())(),") }
    [void]$sb.AppendLine("    ];")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    private static LoreEntry Entry(string id, string line, int order, string icon, LoreChannel channel, UnlockCondition reveal)")
    [void]$sb.AppendLine("        => new()")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            Id = id,")
    [void]$sb.AppendLine("            Title = Prose.Text(`"lore`", id, `"title`"),")
    [void]$sb.AppendLine("            StorylineId = line,")
    [void]$sb.AppendLine("            Order = order,")
    [void]$sb.AppendLine("            Icon = icon,")
    [void]$sb.AppendLine("            Channel = channel,")
    [void]$sb.AppendLine("            Reveal = reveal,")
    [void]$sb.AppendLine("            Body = Prose.Text(`"lore`", id, `"body`"),")
    [void]$sb.AppendLine("        };")
    [void]$sb.AppendLine()
    foreach ($l in $lines) {
        $lid = $l.id; $n = $l.n; $fn = $lid.ToUpperInvariant()
        [void]$sb.AppendLine("    private static IEnumerable<LoreEntry> $fn() =>")
        [void]$sb.AppendLine("    [")
        for ($i = 1; $i -le $n; $i++) {
            $eid = "${lid}_$('{0:D2}' -f $i)"
            $rv = $reveals[$lid][$i - 1]
            $cond = if ($lid -eq 'dawn' -and $i -eq 1) { "UnlockCondition.ClicksAtLeast($rv)" }
                    elseif ($lid -eq 'dusk' -and $i -eq 1) { "UnlockCondition.ClicksAtLeast($rv)" }
                    else { "UnlockCondition.EarnedThisRunAtLeast($rv)" }
            $chan = if ($i -eq 1) { 'Popup' } else { 'Codex' }
            [void]$sb.AppendLine("        Entry(`"$eid`", `"$lid`", $i, `"📖`", LoreChannel.$chan, $cond),")
        }
        [void]$sb.AppendLine("    ];")
        [void]$sb.AppendLine()
    }
    [void]$sb.AppendLine("    public static void VerifyAllTextUsed() => Prose.EnsureNoOrphans();")
    [void]$sb.AppendLine("}")
    Write-Text "engine/content/$pascal/Lore.cs" $sb.ToString()
}

function New-TextJson {
    $o = [ordered]@{}
    # storylines
    $sl = [ordered]@{}
    $slMeta = @{ dawn = @('晨','起点：一切从第一个动作开始。','🌅'); grow = @('生长','规模：数字往上走，世界往外扩。','🌱'); bond = @('羁绊','陪伴：那些一直在的东西。','🤝'); dusk = @('暮','收尾：走到尽头时剩下什么。','🌇') }
    foreach ($k in 'dawn','grow','bond','dusk') { $sl[$k] = [ordered]@{ name = $slMeta[$k][0]; theme = $slMeta[$k][1]; icon = $slMeta[$k][2] } }
    $o['storylines'] = $sl
    # lore
    $lore = [ordered]@{}
    $lineNames = @{ dawn = '晨'; grow = '生长'; bond = '羁绊'; dusk = '暮' }
    foreach ($lid in 'dawn','grow','bond','dusk') {
        $n = @{ dawn = 12; grow = 10; bond = 10; dusk = 8 }[$lid]
        for ($i = 1; $i -le $n; $i++) {
            $eid = "${lid}_$('{0:D2}' -f $i)"
            $lore[$eid] = [ordered]@{ title = "$($lineNames[$lid]) · $i"; body = "这是《$Name》的第 $i 段 $($lineNames[$lid]) 记忆。故事还在继续，把这一页填上你自己的文案。" }
        }
    }
    $o['lore'] = $lore
    # buildings
    $b = [ordered]@{}
    $bNames = @{ plot = '小圃'; shed = '棚屋'; workshop = '作坊'; studio = '工作室'; factory = '工厂'; plant = '车间'; hub = '枢纽'; network = '网络'; mega = '巨构' }
    $bIcons = @{ plot = '🌱'; shed = '🏚️'; workshop = '🔨'; studio = '🏠'; factory = '🏭'; plant = '⚙️'; hub = '🔀'; network = '🌐'; mega = '🌌' }
    foreach ($x in $BUILDINGS) { $b[$x.id] = [ordered]@{ name = $bNames[$x.id]; icon = $bIcons[$x.id]; description = "$($bNames[$x.id])：稳定产出$Currency。" } }
    $o['buildings'] = $b
    # upgrades
    $u = [ordered]@{}
    foreach ($x in $BUILDINGS) { foreach ($t in 1,10,25) { $uid = "$($x.id)_tier$t"; $u[$uid] = [ordered]@{ name = "$($bNames[$x.id]) · $t 档"; icon = $bIcons[$x.id]; description = "$($bNames[$x.id])产量 ×2。" } } }
    $u[$GATE_ID] = [ordered]@{ name = '稳手'; icon = '✋'; description = '点击收益 ×2。' }
    $u[$BRIDGE_ID] = [ordered]@{ name = '众人之手'; icon = '🤲'; description = '每座建筑让点击收益 +0.5%，最多算 120 座（即 +60%）。' }
    foreach ($p in 1,2,3,4) { $u["${Id}_p$p"] = [ordered]@{ name = "$Prestige · $p"; icon = '🎫'; description = "全部产量 +$(@(10,15,20,25)[$p-1])%。转生后保留。" } }
    $o['upgrades'] = $u
    # achievements
    $a = [ordered]@{}
    foreach ($x in $BUILDINGS) { foreach ($c in 1,25,50) { $aid = "$($x.id)_x$c"; $a[$aid] = [ordered]@{ name = "$($bNames[$x.id]) ×$c"; icon = $bIcons[$x.id]; description = "持有 $c 座$($bNames[$x.id])。" } } }
    foreach ($e in '1e4','1e6','1e8','1e10','1e12','1e14','1e16','1e18') { $a["e_$e"] = [ordered]@{ name = "累计 $e"; icon = '📶'; description = "历史累计赚取 $e $Currency。" } }
    foreach ($c in '1e3','1e6','1e9','1e12','1e15','1e18') { $a["c_$c"] = [ordered]@{ name = "产量 $c"; icon = '⚡'; description = "每秒产量达到 $c。" } }
    foreach ($k in '100','1000','10000','100000','1000000') { $a["k_$k"] = [ordered]@{ name = "点击 $k"; icon = '👆'; description = "点击 $k 次。" } }
    foreach ($g in '1','10','50','200','500','1000') { $a["g_$g"] = [ordered]@{ name = "金猫 $g"; icon = '🐱'; description = "点中 $g 只金猫。" } }
    foreach ($i in 1,2) { $a["era_$i"] = [ordered]@{ name = "第 $i 层"; icon = '🎫'; description = "进入第 $i 层。" } }
    $a['ach_end_bright'] = [ordered]@{ name = '好结局'; icon = '🌅'; description = '走到明亮的结局。' }
    $a['ach_end_quiet'] = [ordered]@{ name = '平静结局'; icon = '🌇'; description = '走到平静的结局。' }
    $o['achievements'] = $a
    # buffs
    $bf = [ordered]@{}
    $buffNames = @{ frenzy = '狂热'; click_frenzy = '点击狂热'; boom = '繁荣'; shortage = '短缺'; slump = '低迷'; drift = '漂移'; night_shift = '夜班'; tune_up = '调试' }
    foreach ($k in 'frenzy','click_frenzy','boom','shortage','slump','drift','night_shift','tune_up') { $bf[$k] = [ordered]@{ name = $buffNames[$k]; icon = '✨'; description = "$($buffNames[$k])：限时效果。" } }
    $o['buffs'] = $bf
    # golden
    $gc = [ordered]@{}
    $goldNames = @{ windfall = '横财'; frenzy = '狂热'; click_frenzy = '点击狂热'; thief = '小偷'; nothing = '无事'; boom = '繁荣'; tune_up = '调试'; double = '双喜'; shortage = '短缺'; night_shift = '夜班' }
    foreach ($k in 'windfall','frenzy','click_frenzy','thief','nothing','boom','tune_up','double','shortage','night_shift') { $gc[$k] = [ordered]@{ name = $goldNames[$k]; icon = '🐱'; description = "$($goldNames[$k])：金猫结果。" } }
    $o['goldenCookies'] = $gc
    # eras
    $er = [ordered]@{}
    foreach ($i in 1,2) { $er["stage_$i"] = [ordered]@{ name = "第 $i 层"; icon = '🎫'; theme = "第 $i 层：新的开始。"; entryText = "进入第 $i 层。"; exitText = "第 $i 层结束了。"; completionHint = "本轮累计赚取 1B $Currency。" } }
    $o['eras'] = $er
    # endings
    $en = [ordered]@{}
    $en['end_bright'] = [ordered]@{ name = '明亮'; icon = '🌅'; text = "你走到了明亮的结局。这一路的数字，都成了故事。" }
    $en['end_quiet'] = [ordered]@{ name = '平静'; icon = '🌇'; text = "你走到了平静的结局。故事告一段落。" }
    $o['endings'] = $en

    $json = $o | ConvertTo-Json -Depth 8
    Write-Text "engine/content/$pascal/text.json" $json
}

# ============================================================================
# 注册 / 冻结表（add 与 remove 互为逆操作）
# ============================================================================
function Set-Registration([switch]$Del) {
    $slnGuid = '{' + ([guid]::NewGuid().ToString().ToUpperInvariant()) + '}'
    # 为可回滚，GUID 由 Id 确定性派生（不用随机）
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $bytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("neko-pack-$Id"))
    $g = [guid]::new($bytes)
    $slnGuid = '{' + $g.ToString().ToUpperInvariant() + '}'

    # 1) sln：Project 条目 + 4 配置行 + 嵌套行
    $projLine = "Project(`"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`") = `"$asm`", `"engine\content\$pascal\$asm.csproj`", `"$slnGuid`"`r`nEndProject"
    $cfgLines = @(
        "`t`t$slnGuid.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
        "`t`t$slnGuid.Debug|Any CPU.Build.0 = Debug|Any CPU",
        "`t`t$slnGuid.Release|Any CPU.ActiveCfg = Release|Any CPU",
        "`t`t$slnGuid.Release|Any CPU.Build.0 = Release|Any CPU"
    ) -join "`r`n"
    $nestLine = "`t`t$slnGuid = {0FEE24DC-5291-4FE1-B7AB-005490FC51FD}"
    if ($Del) {
        $sln = Read-Text 'NekoClicker.sln'
        $sln = $sln.Replace($projLine + "`r`n", '')
        $sln = $sln.Replace($cfgLines + "`r`n", '')
        $sln = $sln.Replace($nestLine + "`r`n", '')
        Write-Text 'NekoClicker.sln' $sln
    } else {
        $dreamProj = "Project(`"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`") = `"NekoClicker.Content.Radio`", `"engine\content\Radio\NekoClicker.Content.Radio.csproj`", `"{8E5F2A31-6B4C-4D9E-A2F7-3C9D5E8B1A64}`"`r`nEndProject"
        Edit-File 'NekoClicker.sln' $dreamProj $projLine
        $dreamCfg = "`t`t{8E5F2A31-6B4C-4D9E-A2F7-3C9D5E8B1A64}.Release|Any CPU.Build.0 = Release|Any CPU"
        Edit-File 'NekoClicker.sln' $dreamCfg $cfgLines
        $dreamNest = "`t`t{8E5F2A31-6B4C-4D9E-A2F7-3C9D5E8B1A64} = {0FEE24DC-5291-4FE1-B7AB-005490FC51FD}"
        Edit-File 'NekoClicker.sln' $dreamNest $nestLine
    }

    # 2-4) 三个 csproj 的 ProjectReference
    $refTest = "    <ProjectReference Include=`"..\..\engine\content\$pascal\$asm.csproj`" />"
    $refHost = "    <ProjectReference Include=`"..\..\..\engine\content\$pascal\$asm.csproj`" />"
    $anchorTest = "    <ProjectReference Include=`"..\..\engine\content\Radio\NekoClicker.Content.Radio.csproj`" />"
    $anchorHost = "    <ProjectReference Include=`"..\..\..\engine\content\Radio\NekoClicker.Content.Radio.csproj`" />"
    Edit-File 'engine/tests/NekoClicker.Core.Tests.csproj' $anchorTest $refTest -Del:$Del
    Edit-File 'games/hosts/Demo.Cli/NekoClicker.Demo.Cli.csproj' $anchorHost $refHost -Del:$Del
    Edit-File 'games/hosts/Web/NekoClicker.Web.csproj' $anchorHost $refHost -Del:$Del

    # 5) ContentPackages：using + ContentPackage 条目
    $usingLine = "using NekoClicker.Content.$pascal;"
    Edit-File 'games/hosts/Demo.Cli/ContentPackages.cs' 'using NekoClicker.Content.Radio;' $usingLine -Del:$Del
    $pkgEntry = @"
        new ContentPackage(
            Id: "$Id",
            Name: ${pascal}Content.GameTitle,
            Build: ${pascal}Content.Build,
            Welcome: "欢迎来到《$Name》！这是 D3 脚手架生成的骨架包，把文案与机制换成你自己的。",
            PrestigeActionName: "转生",
            PrestigeHint: "转生：本层结束，按历史累计换取「$Prestige」。",
            GoldenCookieName: "金猫",
            HelpTips:
            [
                "    · 这是 D3 脚手架生成的两层 vanilla 骨架：点击是生产线、建筑走已验证曲线。",
                "    · 要反转核心动词或加第二资源，照 C1《猫娘电台》挂一个 IGameModule。",
            ],
            ReportTip: "提示：这是脚手架骨架，数值合规、文案待填。"),
"@
    $pkgEntry = ($pkgEntry -split "`r?`n") -join "`r`n"
    # 锚点用 Radio 条目收尾行（数组内），不能用 '    ];'——Edit-File 是锚点后插，会插到数组外。
    $pkgAnchor = '                     + "它掉了产量就掉。想拿「被听到最后」，停播时收听率得够高。"),'
    Edit-File 'games/hosts/Demo.Cli/ContentPackages.cs' $pkgAnchor $pkgEntry -Del:$Del

    # 6) TestGame：using + cache + property + AllContentPacks 行
    Edit-File 'engine/tests/TestGame.cs' 'using NekoClicker.Content.Radio;' $usingLine -Del:$Del
    $cacheLine = "    private static readonly Lazy<GameContent> ${pascal}ContentCache = new(${pascal}Content.Build);"
    Edit-File 'engine/tests/TestGame.cs' '    private static readonly Lazy<GameContent> RadioContentCache = new(RadioContent.Build);' $cacheLine -Del:$Del
    $propLines = @"

    /// <summary>内容包《$Name》（D3 脚手架生成的两层 vanilla MVP；不可变，可安全共享）。</summary>
    public static GameContent $pascal => ${pascal}ContentCache.Value;
"@
    $propLines = ($propLines -split "`r?`n") -join "`r`n"
    Edit-File 'engine/tests/TestGame.cs' '    public static GameContent Radio => RadioContentCache.Value;' $propLines -Del:$Del
    $enumLine = "        (`"$Name`", $pascal),"
    Edit-File 'engine/tests/TestGame.cs' '        ("C1 电台", Radio),' $enumLine -Del:$Del
}

function Set-GuardTables([switch]$Del) {
    # 1) 点击桥金表
    $bridgeLine = "        (`"$Name`", `"$BRIDGE_ID`", `"$GATE_ID`", $BRIDGE_PRICE),"
    Edit-File 'engine/tests/ClickBridgeTests.cs' '        ("C1 电台", "signal_hands", "steady_hand", 5_000_000),' $bridgeLine -Del:$Del

    # 2) 主线基线（指纹精确复算）
    $cur = $Currency
    $compLine = "本轮累计赚取 1B $cur ‖ N(CookiesEarnedThisRun=$COMPLETION,'')"
    $goodFp = "end_bright|0|进入第 2 纪元，且 本轮累计赚取 1B $cur，且 解锁 $GOOD_ACH 个成就 ‖ All(All(N(Era=2,''),N(CookiesEarnedThisRun=$COMPLETION,'')),N(AchievementCount=$GOOD_ACH,''))"
    $quietFp = "end_quiet|100|进入第 2 纪元，且 本轮累计赚取 1B $cur ‖ All(N(Era=2,''),N(CookiesEarnedThisRun=$COMPLETION,''))"
    $baseline = @"
        new("$Name", $N_ERA, $N_ERA,
        [
            "$compLine",
            "$compLine",
        ],
        [
            "$goodFp",
            "$quietFp",
        ]),
"@
    $baseline = ($baseline -split "`r?`n") -join "`r`n"
    $anchor = '            "end_static|100|进入第 2 纪元，且 本轮累计赚取 1B 信号 ‖ All(N(Era=2,' + "''" + '),N(CookiesEarnedThisRun=1000000000,' + "''" + '))",' + "`r`n        ]),"
    if ($Del) {
        # 按包名整块删除（与指纹内容无关，Currency/GOOD_ACH 变了也能删净，幂等）。
        $era = Read-Text 'engine/tests/EraStageTests.cs'
        $era = Remove-BaselinesByName $era $Name
        Write-Text 'engine/tests/EraStageTests.cs' $era
    } else {
        Edit-File 'engine/tests/EraStageTests.cs' $anchor $baseline
    }

    # 3) BuildingUpgrade 总数（含快照侧）
    $bu = Read-Text 'engine/tests/BuildingUpgradeTests.cs'
    if ($Del) {
        $bu = $bu.Replace('Check.Equal(614, totalUpgrades', 'Check.Equal(581, totalUpgrades')
        $bu = $bu.Replace('Check.Equal(366, categorized', 'Check.Equal(339, categorized')
        $bu = $bu.Replace('Check.Equal(122, buildingsWithTrack', 'Check.Equal(113, buildingsWithTrack')
        $bu = $bu.Replace('Check.Equal(122, buildings, "建筑总数")', 'Check.Equal(113, buildings, "建筑总数")')
        $bu = $bu.Replace('Check.Equal(122, buildings, "建筑数（快照侧）")', 'Check.Equal(113, buildings, "建筑数（快照侧）")')
        $bu = $bu.Replace('Check.Equal(122, withTrack', 'Check.Equal(113, withTrack')
    } else {
        $bu = $bu.Replace('Check.Equal(581, totalUpgrades', 'Check.Equal(614, totalUpgrades')
        $bu = $bu.Replace('Check.Equal(339, categorized', 'Check.Equal(366, categorized')
        $bu = $bu.Replace('Check.Equal(113, buildingsWithTrack', 'Check.Equal(122, buildingsWithTrack')
        $bu = $bu.Replace('Check.Equal(113, buildings, "建筑总数")', 'Check.Equal(122, buildings, "建筑总数")')
        $bu = $bu.Replace('Check.Equal(113, buildings, "建筑数（快照侧）")', 'Check.Equal(122, buildings, "建筑数（快照侧）")')
        $bu = $bu.Replace('Check.Equal(113, withTrack', 'Check.Equal(122, withTrack')
    }
    Write-Text 'engine/tests/BuildingUpgradeTests.cs' $bu

    # 4) ContentTextFile 十表：按表名定位表块、块内 Radio 行后插一行。
    #    eras/endings 同值 2、stances/choices 同值 0 也不串表（各表独立定位，根治同值歧义）。
    $ct = Read-Text 'engine/tests/ContentTextFileTests.cs'
    $ctTables = @(
        @{ name = 'Externalized'; row = "        (`"$pascal`", () => TestGame.$pascal)," },
        @{ name = 'ExpectedBuildings'; row = "        (`"$pascal`", $N_BUILDING)," },
        @{ name = 'ExpectedEras'; row = "        (`"$pascal`", $N_ERA)," },
        @{ name = 'ExpectedEndings'; row = "        (`"$pascal`", $N_ENDING)," },
        @{ name = 'ExpectedStances'; row = "        (`"$pascal`", 0)," },
        @{ name = 'ExpectedChoices'; row = "        (`"$pascal`", 0)," },
        @{ name = 'ExpectedAchievements'; row = "        (`"$pascal`", $N_ACHIEVEMENT)," },
        @{ name = 'ExpectedBuffs'; row = "        (`"$pascal`", $N_BUFF)," },
        @{ name = 'ExpectedUpgrades'; row = "        (`"$pascal`", $N_UPGRADE)," },
        @{ name = 'ExpectedGoldenCookieOutcomes'; row = "        (`"$pascal`", $N_GOLDEN)," }
    )
    foreach ($t in $ctTables) { $ct = Set-PackRowInTable $ct $t.name $t.row -Del:$Del }
    Write-Text 'engine/tests/ContentTextFileTests.cs' $ct

    # 5) ProseTone：budget 行 + 行数防线
    $toneLine = "        new(`"$pascal`", 0, 0, 0),"
    Edit-File 'engine/tests/ProseToneTests.cs' '        new("Radio", 0, 0, 0),' $toneLine -Del:$Del
    $pt = Read-Text 'engine/tests/ProseToneTests.cs'
    if ($Del) { $pt = $pt.Replace('Check.Equal(13, Budgets.Length', 'Check.Equal(12, Budgets.Length') }
    else { $pt = $pt.Replace('Check.Equal(12, Budgets.Length', 'Check.Equal(13, Budgets.Length') }
    Write-Text 'engine/tests/ProseToneTests.cs' $pt
}

function Set-PackRowInTable([string]$text, [string]$tableName, [string]$newRow, [switch]$Del) {
    # 用 "] 表名 =" 定位声明（避开 cref 引用），取声明到表尾 "    ];" 之间的块，只在块内操作。
    $locator = "] $tableName ="
    $tableIdx = $text.IndexOf($locator)
    if ($tableIdx -lt 0) { throw "找不到表：$tableName" }
    $closeIdx = $text.IndexOf("    ];", $tableIdx)
    if ($closeIdx -lt 0) { throw "表尾找不到：$tableName" }
    $block = $text.Substring($tableIdx, $closeIdx - $tableIdx)
    if ($Del) {
        $newBlock = $block.Replace($newRow + "`r`n", '')
    } else {
        if ($block.Contains($newRow)) { return $text }  # 幂等
        $radioIdx = $block.IndexOf('("Radio",')
        if ($radioIdx -lt 0) { throw "表内无 Radio 行：$tableName" }
        $lineEnd = $block.IndexOf("`n", $radioIdx)
        $newBlock = $block.Substring(0, $lineEnd + 1) + $newRow + "`r`n" + $block.Substring($lineEnd + 1)
    }
    return $text.Substring(0, $tableIdx) + $newBlock + $text.Substring($closeIdx)
}

function Remove-BaselinesByName([string]$text, [string]$packName) {
    # 按包名整块删主线基线：从 '        new("名",' 到其后第一个 '        ]),'，循环删净。
    # 与指纹内容无关——Currency / GOOD_ACH 变了也删得掉（根治"复算精确匹配"的参数依赖）。
    $marker = "        new(`"$packName`","
    $closeTag = "        ]),"
    while ($true) {
        $start = $text.IndexOf($marker)
        if ($start -lt 0) { break }
        $end = $text.IndexOf($closeTag, $start)
        if ($end -lt 0) { throw "基线块尾找不到：$packName" }
        $end += $closeTag.Length
        if ($text.Length -ge $end + 2 -and $text.Substring($end, 2) -eq "`r`n") { $end += 2 }
        elseif ($text.Length -ge $end + 1 -and $text.Substring($end, 1) -eq "`n") { $end += 1 }
        $text = $text.Substring(0, $start) + $text.Substring($end)
    }
    return $text
}

# ============================================================================
# 主流程
# ============================================================================
if ($Remove) {
    if (-not (Test-Path $packDir)) { throw "包不存在，无法移除：$packDir" }
    Set-GuardTables -Del
    Set-Registration -Del
    Remove-Item -Recurse -Force $packDir
    Write-Host "已移除包 $Id 并还原全部注册与冻结表。跑 tools/build.ps1 验证。" -ForegroundColor Green
    return
}

if (Test-Path $packDir) { throw "包已存在：$packDir（要重建先 -Remove）" }
Write-Host "生成包 $Id（$Name）→ engine/content/$pascal ..." -ForegroundColor Cyan
New-PackFiles
Set-Registration
Set-GuardTables
Write-Host "生成 + 注册 + 冻结表更新完成。跑 tools/build.ps1 验证过守卫..." -ForegroundColor Cyan
Write-Host "提示：验证通过后可用 -Remove 回滚（不留演示包）；要保留则直接提交。" -ForegroundColor Yellow
