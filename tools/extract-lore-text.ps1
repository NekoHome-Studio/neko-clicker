# extract-lore-text.ps1 -- pull lore prose out of a pack's Lore.cs into text.json.
#
# STATUS (2026-10-01): MISSION ACCOMPLISHED -- kept for the record, not for routine use.
#   All ten packs with lore are externalized now. This script only understands the
#   factory style (Popup/Log/Codex(...)); the six packs written with object initializers
#   were migrated by a one-off scanner, and the fidelity proof for the whole migration
#   was a before/after runtime dump compared byte for byte (see CHANGELOG, [unreleased]).
#
# Usage:
#   powershell -File tools/extract-lore-text.ps1 -Pack lab
#
# Why this exists: moving ~40 entries of Chinese prose by hand invites transcription
# errors, and hand-editing Chinese through the shell has already corrupted a file once
# in this repo (a script literal got mis-decoded and rewrote 298 lines). So: extract by
# machine, then PROVE the extraction is faithful.
#
# ASCII-only on purpose: every Chinese character here comes from reading the source file
# through .NET, never from a literal in this script. That keeps the whole encoding class
# of bugs out of the picture.
#
# Emits: engine/content/<Pack>/text.json  (UTF-8, no BOM, non-ASCII left unescaped so the
# file stays readable by a human -- which is the entire point of the exercise).

param(
    [Parameter(Mandatory = $true)][string]$Pack
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "engine\content\$Pack\Lore.cs"
$target = Join-Path $root "engine\content\$Pack\text.json"

if (-not (Test-Path $source)) { throw "no such file: $source" }
$text = [System.IO.File]::ReadAllText($source)

function Escape([string]$s) {
    # Only what a JSON string needs. The source has no escaped quotes and no verbatim
    # strings (verified: 0 of each), so this stays small.
    $s = $s.Replace('\', '\\')
    $s = $s.Replace('"', '\"')
    $s = $s.Replace("`r", '')
    $s = $s.Replace("`n", '\n')
    $s = $s.Replace("`t", '\t')
    return $s
}

# ---- storylines: Id = "..", Name = "..", Theme = "..", Icon = ".." (order fixed) ----
$storylines = [ordered]@{}
$slPattern = 'Id\s*=\s*"(?<id>[^"]*)"\s*,\s*Name\s*=\s*"(?<name>[^"]*)"\s*,\s*Theme\s*=\s*"(?<theme>[^"]*)"\s*,\s*Icon\s*=\s*"(?<icon>[^"]*)"'
foreach ($m in [regex]::Matches($text, $slPattern)) {
    $storylines[$m.Groups['id'].Value] = [ordered]@{
        name  = $m.Groups['name'].Value
        theme = $m.Groups['theme'].Value
        icon  = $m.Groups['icon'].Value
    }
}

# ---- entries: Popup|Log|Codex("id", order, "title", "body", condition) ----
$entries = [ordered]@{}
$ePattern = '(?:Popup|Log|Codex)\("(?<id>[^"]+)"\s*,\s*(?<order>\d+)\s*,\s*"(?<title>[^"]*)"\s*,\s*"(?<body>[^"]*)"'
foreach ($m in [regex]::Matches($text, $ePattern)) {
    $id = $m.Groups['id'].Value
    if ($entries.Contains($id)) { throw "duplicate lore id in source: $id" }
    $entries[$id] = [ordered]@{
        title = $m.Groups['title'].Value
        body  = $m.Groups['body'].Value
    }
}

if ($entries.Count -eq 0) { throw "extracted 0 entries from $source -- the call pattern changed?" }

# ---- emit ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("{`n")

[void]$sb.Append("  `"storylines`": {`n")
$keys = @($storylines.Keys)
for ($i = 0; $i -lt $keys.Count; $i++) {
    $k = $keys[$i]
    $v = $storylines[$k]
    [void]$sb.Append("    `"$(Escape $k)`": { `"name`": `"$(Escape $v.name)`", `"theme`": `"$(Escape $v.theme)`", `"icon`": `"$(Escape $v.icon)`" }")
    [void]$sb.Append($(if ($i -lt $keys.Count - 1) { ",`n" } else { "`n" }))
}
[void]$sb.Append("  },`n")

[void]$sb.Append("  `"lore`": {`n")
$keys = @($entries.Keys)
for ($i = 0; $i -lt $keys.Count; $i++) {
    $k = $keys[$i]
    $v = $entries[$k]
    [void]$sb.Append("    `"$(Escape $k)`": {`n")
    [void]$sb.Append("      `"title`": `"$(Escape $v.title)`",`n")
    [void]$sb.Append("      `"body`": `"$(Escape $v.body)`"`n")
    [void]$sb.Append("    }")
    [void]$sb.Append($(if ($i -lt $keys.Count - 1) { ",`n" } else { "`n" }))
}
[void]$sb.Append("  }`n")

[void]$sb.Append("}`n")

[System.IO.File]::WriteAllText($target, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Host ("extracted {0} lore entries and {1} storylines -> {2}" -f $entries.Count, $storylines.Count, $target)
Write-Host ("file size: {0:N0} bytes" -f (Get-Item $target).Length)
