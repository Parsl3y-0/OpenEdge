param(
    [string]$OutputPath = "docs/recovery/openedge-diagnostics.md"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$parent = Split-Path -Parent $resolvedOutput
if ($parent -and -not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }

$runtimeRoot = Join-Path $repoRoot 'runtime\local\app'
$compatibilityState = Join-Path $runtimeRoot 'compatibility-state.json'
$mediaIndex = Join-Path $runtimeRoot 'media-tag-index.json'
$legacyTags = Join-Path $runtimeRoot 'tags.txt'
$debugDir = Join-Path $runtimeRoot 'debug'
$sessionTrace = Join-Path $debugDir 'session-trace.log'
$archivedSessionTraces = if (Test-Path $debugDir) { @(Get-ChildItem -Path $debugDir -Filter 'session-trace-*.log' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 10) } else { @() }
$auditTemp = Join-Path ([System.IO.Path]::GetTempPath()) ('openedge-audit-' + [Guid]::NewGuid().ToString('N') + '.md')

powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot 'docs\recovery\audit-legacy-state.ps1') -OutputPath $auditTemp | Out-Null
$audit = Get-Content $auditTemp -Raw
Remove-Item $auditTemp -ErrorAction SilentlyContinue

$compatibilityEntryCount = 0
if (Test-Path $compatibilityState) {
    $compatibilityText = Get-Content $compatibilityState -Raw
    $persistentMatch = [Regex]::Match($compatibilityText, '"PersistentEntries"\s*:\s*\{(?<body>[\s\S]*?)\}\s*[,}]')
    if ($persistentMatch.Success) {
        $compatibilityEntryCount = ([Regex]::Matches($persistentMatch.Groups['body'].Value, '"[^"]+"\s*:') | Measure-Object).Count
    }
}
$mediaIdentityCount = 0
$taggedIdentityCount = 0
if (Test-Path $mediaIndex) {
    $mediaJson = Get-Content $mediaIndex -Raw | ConvertFrom-Json
    if ($mediaJson.Items) {
        $mediaIdentityCount = @($mediaJson.Items).Count
        $taggedIdentityCount = @($mediaJson.Items | Where-Object { $_.Tags -and $_.Tags.Trim().Length -gt 0 }).Count
    }
}
$legacyTagLines = if (Test-Path $legacyTags) { @(Get-Content $legacyTags).Count } else { 0 }

function Read-JsonOrNull([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    try { return Get-Content $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Get-JsonItemArray($Value) {
    if ($null -eq $Value) { return @() }
    return @($Value)
}

function Get-ModDiagnosticsLines {
    $result = New-Object System.Collections.Generic.List[string]
    $modsDir = Join-Path $runtimeRoot 'mods'
    $modLoadOrder = Join-Path $runtimeRoot 'mod-load-order.json'
    $result.Add("- Mods directory exists: $(Test-Path $modsDir)")
    $result.Add("- Mod load order file exists: $(Test-Path $modLoadOrder)")
    if (-not (Test-Path $modsDir)) { return $result }

    $modDirs = @(Get-ChildItem -Path $modsDir -Directory | Sort-Object Name)
    $result.Add("- Mods detected: $($modDirs.Count)")
    $lineRoots = New-Object System.Collections.Generic.List[string]
    $lineRoots.Add((Join-Path $runtimeRoot 'lines'))
    foreach ($dir in $modDirs) { $lineRoots.Add((Join-Path $dir.FullName 'lines')) }
    $exclusiveHookCounts = @{}
    foreach ($dir in $modDirs) {
        $manifest = Read-JsonOrNull (Join-Path $dir.FullName 'mod.json')
        $enabled = if ($manifest -and ($null -ne $manifest.enabled)) { [bool]$manifest.enabled } else { $true }
        if (-not $enabled) { continue }
        $hooksFile = Read-JsonOrNull (Join-Path $dir.FullName 'hooks\hooks.json')
        $hooks = if ($hooksFile) { Get-JsonItemArray $hooksFile.hooks } else { @() }
        foreach ($hook in $hooks) {
            $hookName = if ($hook.hook) { [string]$hook.hook } else { '' }
            $mode = if ($hook.mode) { [string]$hook.mode } else { '' }
            if (-not [string]::IsNullOrWhiteSpace($hookName) -and (@('exclusive','replace') -contains $mode.ToLowerInvariant())) {
                if (-not $exclusiveHookCounts.ContainsKey($hookName)) { $exclusiveHookCounts[$hookName] = 0 }
                $exclusiveHookCounts[$hookName]++
            }
        }
    }

    foreach ($dir in $modDirs) {
        $manifest = Read-JsonOrNull (Join-Path $dir.FullName 'mod.json')
        $id = if ($manifest -and $manifest.id) { [string]$manifest.id } else { $dir.Name }
        $name = if ($manifest -and $manifest.name) { [string]$manifest.name } else { $id }
        $version = if ($manifest -and $manifest.version) { [string]$manifest.version } else { 'not specified' }
        $enabled = if ($manifest -and ($null -ne $manifest.enabled)) { [bool]$manifest.enabled } else { $true }
        $hooksFile = Read-JsonOrNull (Join-Path $dir.FullName 'hooks\hooks.json')
        $outcomesFile = Read-JsonOrNull (Join-Path $dir.FullName 'outcomes\outcomes.json')
        $settingsFile = Read-JsonOrNull (Join-Path $dir.FullName 'settings\settings.json')
        $tagsFile = Read-JsonOrNull (Join-Path $dir.FullName 'tags\tags.json')
        $contextsFile = Read-JsonOrNull (Join-Path $dir.FullName 'contexts\contexts.json')
        $settings = if ($settingsFile) { Get-JsonItemArray $settingsFile.settings } else { @() }
        $tags = if ($tagsFile) { Get-JsonItemArray $tagsFile.tags } else { @() }
        $contexts = if ($contextsFile) { Get-JsonItemArray $contextsFile.contexts } else { @() }
        $hooks = if ($hooksFile) { Get-JsonItemArray $hooksFile.hooks } else { @() }
        $outcomes = if ($outcomesFile) { Get-JsonItemArray $outcomesFile.outcomes } else { @() }
        $warnings = New-Object System.Collections.Generic.List[string]
        foreach ($hook in $hooks) {
            $hookName = if ($hook.hook) { [string]$hook.hook } else { '' }
            $script = if ($hook.script) { [string]$hook.script } else { '' }
            $mode = if ($hook.mode) { [string]$hook.mode } else { 'additive' }
            if ([string]::IsNullOrWhiteSpace($hookName)) { $warnings.Add('hook with missing name') }
            if ([string]::IsNullOrWhiteSpace($script)) { $warnings.Add('hook with missing script') }
            if ($mode -and @('additive','exclusive','fallback','replace') -notcontains $mode.ToLowerInvariant()) { $warnings.Add('unknown hook mode') }
            if ($enabled -and -not [string]::IsNullOrWhiteSpace($hookName) -and (@('exclusive','replace') -contains $mode.ToLowerInvariant()) -and $exclusiveHookCounts.ContainsKey($hookName) -and $exclusiveHookCounts[$hookName] -gt 1) { $warnings.Add("exclusive/replace hook conflict resolved by priority: $hookName") }
            if (-not [string]::IsNullOrWhiteSpace($script)) {
                $exists = $false
                foreach ($root in $lineRoots) {
                    if ((Test-Path (Join-Path $root "Scripts\Base\$script.txt")) -or (Test-Path (Join-Path $root "Scripts\Extend\$script.txt"))) { $exists = $true; break }
                }
                if (-not $exists) { $warnings.Add("missing hook script: $script") }
            }
        }
        foreach ($outcome in $outcomes) {
            if (-not $outcome.kind) { $warnings.Add('outcome with missing kind') }
            if (-not $outcome.key) { $warnings.Add('outcome with missing key') }
        }
        $hookCount = @($hooks).Count
        $outcomeCount = @($outcomes).Count
        $overrideCount = @($hooks | Where-Object { $_.mode -and (@('exclusive','replace') -contains ([string]$_.mode).ToLowerInvariant()) }).Count
        $linesExists = (Test-Path (Join-Path $dir.FullName 'lines\Scripts')) -and (Test-Path (Join-Path $dir.FullName 'lines\Vocab'))
        $result.Add('')
        $result.Add("### $name")
        $result.Add("- Id: $id")
        $result.Add("- Version: $version")
        $result.Add("- Enabled: $enabled")
        $result.Add("- Counts: settings=$(@($settings).Count), tags=$(@($tags).Count), contexts=$(@($contexts).Count), hooks=$hookCount, overrides=$overrideCount, outcomes=$outcomeCount, lines=$linesExists")
        $result.Add("- Warnings: $(if ($warnings.Count -eq 0) { 'none' } else { (($warnings | Select-Object -Unique) -join '; ') })")
        if ($hookCount -gt 0) {
            $result.Add('- Hooks:')
            foreach ($hook in $hooks) { $result.Add("  - hook=$($hook.hook), mode=$($hook.mode), script=$($hook.script), weight=$($hook.weight), allowedWhileChaste=$($hook.allowedWhileChaste)") }
        }
        if ($outcomeCount -gt 0) {
            $result.Add('- Outcomes:')
            foreach ($outcome in $outcomes) { $result.Add("  - kind=$($outcome.kind), key=$($outcome.key), allowedWhileChaste=$($outcome.allowedWhileChaste)") }
        }
    }
    return $result
}

$modDiagnosticsLines = Get-ModDiagnosticsLines

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# OpenEdge diagnostics export')
$lines.Add('')
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add('')
$lines.Add('## Compatibility state')
$lines.Add('')
$lines.Add("- Compatibility state file exists: $(Test-Path $compatibilityState)")
$lines.Add("- Persistent compatibility entries: $compatibilityEntryCount")
$lines.Add('')
$lines.Add('## Media tags')
$lines.Add('')
$lines.Add("- Media identity index exists: $(Test-Path $mediaIndex)")
$lines.Add("- Media identity records: $mediaIdentityCount")
$lines.Add("- Tagged identity records: $taggedIdentityCount")
$lines.Add("- Legacy tags.txt lines: $legacyTagLines")
$lines.Add('')
$lines.Add('## Mod diagnostics')
$lines.Add('')
foreach ($line in $modDiagnosticsLines) { $lines.Add($line) }
$lines.Add('')
$lines.Add('## Runtime logs')
$lines.Add('')
$lines.Add("- Current session trace exists: $(Test-Path $sessionTrace)")
$lines.Add("- Retained session trace archives: $(@($archivedSessionTraces).Count)")
foreach ($trace in $archivedSessionTraces) {
    $lines.Add("  - $($trace.Name) ($([Math]::Round($trace.Length / 1KB, 1)) KB, $($trace.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')))" )
}
$lines.Add('')
$lines.Add('## Script migration audit')
$lines.Add('')
$lines.Add($audit)

Set-Content -Path $resolvedOutput -Value ($lines -join [Environment]::NewLine) -Encoding UTF8
Write-Host "Wrote diagnostics report: $resolvedOutput"
