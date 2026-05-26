# update-codebase-map.ps1
# Called by Claude Code PostToolUse hook (Edit/Write) via stdin JSON.
# Regenerates memory/reference_codebase_map.md when a mod .cs or .xml file changes.

param([string]$StdinOverride = "")

$ModRoot  = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore"
$MemFile  = "C:\Users\Ruan\.claude\projects\c--Program-Files--x86--Steam-steamapps-common-RimWorld-Mods-FormgelCore\memory\reference_codebase_map.md"
$SrcRoot  = "$ModRoot\Source\1.6"
$DefsRoot = "$ModRoot\1.6\Defs"

# Read hook payload from stdin
$raw = if ($StdinOverride) { $StdinOverride } else { [Console]::In.ReadToEnd() }

$editedFile = ""
if ($raw.Trim()) {
    try {
        $hook = $raw | ConvertFrom-Json
        $editedFile = $hook.tool_input.file_path
        if (-not $editedFile) { $editedFile = $hook.tool_input.path }
    } catch { }
}

# Only proceed if the edited file is part of this mod's source/defs
$isCs  = $editedFile -like "$SrcRoot\*.cs"
$isXml = $editedFile -like "$DefsRoot\*"
if (-not $isCs -and -not $isXml) { exit 0 }

# Extract preserved "Invariantes criticos" section from existing file
$invariants = ""
if (Test-Path $MemFile) {
    $existing = [System.IO.File]::ReadAllText($MemFile, [System.Text.Encoding]::UTF8)
    $marker = "## Invariantes"
    $idx = $existing.IndexOf($marker)
    if ($idx -ge 0) { $invariants = $existing.Substring($idx) }
}

# Build C# section
function Build-CsSection {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("## Arquivos C# -- Source/1.6/")
    $lines.Add("")

    $files = Get-ChildItem -Path $SrcRoot -Filter "*.cs" -Recurse | Sort-Object FullName
    foreach ($f in $files) {
        $rel = $f.FullName.Replace($SrcRoot + "\", "").Replace("\", "/")
        $lines.Add("### $rel")

        $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
        $matches_ = [regex]::Matches($content, '(?m)^\s*(public|internal)\s+(static\s+)?(partial\s+)?(abstract\s+)?(class|struct|enum|interface)\s+(\w+)')
        if ($matches_.Count -gt 0) {
            foreach ($m in $matches_) {
                $kind = $m.Groups[5].Value
                $name = $m.Groups[6].Value
                $lines.Add("- $name ($kind)")
            }
        } else {
            $lines.Add("(no public types)")
        }
        $lines.Add("")
    }
    return $lines -join "`n"
}

# Build XML section
function Build-XmlSection {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("## Arquivos XML -- 1.6/Defs/")
    $lines.Add("")

    $files = Get-ChildItem -Path $DefsRoot -Filter "*.xml" -Recurse | Sort-Object FullName
    foreach ($f in $files) {
        $rel = $f.FullName.Replace($DefsRoot + "\", "").Replace("\", "/")
        $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)

        $defNames = [regex]::Matches($content, '<defName>([^<]+)</defName>') |
            ForEach-Object { $_.Groups[1].Value.Trim() } |
            Where-Object { $_ -ne "" } |
            Select-Object -Unique

        if ($defNames) {
            $joined = $defNames -join ", "
            $lines.Add("$rel -- $joined")
        } else {
            $lines.Add("$rel -- (no defNames)")
        }
    }
    $lines.Add("")
    return $lines -join "`n"
}

# Assemble final file
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"

$header = @"
---
name: reference-codebase-map
description: Mapa completo de todo arquivo C# e XML do mod -- classes, defNames, metodos publicos chave. Consulte antes de ler qualquer arquivo.
metadata:
  type: reference
---

# Mapa do Codebase -- SyntheraCore
Auto-gerado em $timestamp por update-codebase-map.ps1

"@

$csSection  = Build-CsSection
$xmlSection = Build-XmlSection

$body = $header + $csSection + "`n---`n`n" + $xmlSection

if ($invariants) {
    $body = $body.TrimEnd() + "`n`n---`n`n" + $invariants
}

[System.IO.File]::WriteAllText($MemFile, $body, [System.Text.Encoding]::UTF8)

Write-Host "[codebase-map] Updated (trigger: $([System.IO.Path]::GetFileName($editedFile)))"
