param(
    [string]$ProjectRoot = "C:\Metin4\Metin3 Test",
    [string]$Exporter = "$env:USERPROFILE\Desktop\GrannyExporter\Compiled\GrannyExporter.exe",
    [switch]$ScanOnly,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$clientRoot = Join-Path $ProjectRoot "Metin2,5"
$extractedRoot = Join-Path $clientRoot "Extracted"
$outputRoot = Join-Path $clientRoot "PlayerConverted"
$runId = [guid]::NewGuid().ToString("N")
$tempRoot = Join-Path $env:TEMP ("Metin2Player_" + $runId)
$logPath = Join-Path $outputRoot "ConversionReport.txt"

function Ensure-Directory([string]$Path) {
    [IO.Directory]::CreateDirectory($Path) | Out-Null
}

function Read-MsaValue([string]$Text, [string]$Name) {
    $pattern = '(?im)^\s*' + [regex]::Escape($Name) + '\s+(?:"([^"]*)"|([^\s#]+))'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { return "" }
    if ($match.Groups[1].Success) { return $match.Groups[1].Value }
    return $match.Groups[2].Value
}

function Read-MsaLineValue([string]$Text, [string]$Name) {
    $pattern = '(?im)^\s*' + [regex]::Escape($Name) + '\s+(.+?)\s*$'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { return "" }
    $value = $match.Groups[1].Value.Trim()
    if ($value.Length -ge 2 -and $value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') {
        return $value.Substring(1, $value.Length - 2)
    }
    return $value
}

function Resolve-Motion([string]$Reference, [string]$DefaultPack, [string]$MsaDirectory) {
    if ([string]::IsNullOrWhiteSpace($Reference)) { return $null }
    $normalized = $Reference.Replace('/', '\').Trim().Trim('"')
    $lower = $normalized.ToLowerInvariant()
    foreach ($sourcePack in @('PC2', 'PC')) {
        $sourceTree = if ($sourcePack -eq 'PC2') { 'pc2' } else { 'pc' }
        $marker = "\ymir work\$sourceTree\"
        $markerIndex = $lower.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase)
        if ($markerIndex -ge 0) {
            $relative = $normalized.Substring($markerIndex + $marker.Length)
            return Join-Path (Join-Path (Join-Path $extractedRoot $sourcePack) "ymir work\$sourceTree") $relative
        }
    }
    $defaultTree = if ($DefaultPack -eq 'PC2') { 'pc2' } else { 'pc' }
    $relativeToMsa = Join-Path $MsaDirectory $normalized
    if (Test-Path -LiteralPath $relativeToMsa) { return $relativeToMsa }
    return Join-Path (Join-Path (Join-Path $extractedRoot $DefaultPack) "ymir work\$defaultTree") $normalized.TrimStart('\')
}

function Relative-Path([string]$Base, [string]$Path) {
    $baseUri = [Uri](([IO.Path]::GetFullPath($Base).TrimEnd('\') + '\'))
    $pathUri = [Uri][IO.Path]::GetFullPath($Path)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

if (-not (Test-Path -LiteralPath $extractedRoot)) { throw "Extracted root not found: $extractedRoot" }
if (-not (Test-Path -LiteralPath $Exporter)) { throw "GrannyExporter not found: $Exporter" }

Ensure-Directory $outputRoot
$messages = [Collections.Generic.List[string]]::new()
$missing = [Collections.Generic.List[string]]::new()
$failed = [Collections.Generic.List[string]]::new()
$converted = 0
$skipped = 0
$clipCount = 0
$groups = @{}

foreach ($pack in @('PC', 'PC2')) {
    $treeName = if ($pack -eq 'PC2') { 'pc2' } else { 'pc' }
    $packTree = Join-Path (Join-Path $extractedRoot $pack) "ymir work\$treeName"
    if (-not (Test-Path -LiteralPath $packTree)) { $missing.Add("PACK | $packTree"); continue }
    foreach ($msa in Get-ChildItem -LiteralPath $packTree -Recurse -Filter '*.msa' -File) {
        $text = [IO.File]::ReadAllText($msa.FullName)
        $motionReference = Read-MsaLineValue $text 'MotionFileName'
        if ([string]::IsNullOrWhiteSpace($motionReference)) { continue }
        $motionPath = Resolve-Motion $motionReference $pack $msa.DirectoryName
        if (-not (Test-Path -LiteralPath $motionPath)) {
            $missing.Add("MOTION | $($msa.FullName) | $motionReference")
            continue
        }
        $motionPack = if ($motionPath.IndexOf('\PC2\', [StringComparison]::OrdinalIgnoreCase) -ge 0) { 'PC2' } else { 'PC' }
        $motionTreeName = if ($motionPack -eq 'PC2') { 'pc2' } else { 'pc' }
        $motionPackTree = Join-Path (Join-Path $extractedRoot $motionPack) "ymir work\$motionTreeName"
        $relativeMotion = Relative-Path $motionPackTree $motionPath
        $segments = $relativeMotion.Split('\')
        if ($segments.Length -lt 2) { $missing.Add("CLASS | $motionPath"); continue }
        $className = $segments[0]
        $relativeDirectory = Split-Path $relativeMotion -Parent
        $key = "$motionPack|$className|$relativeDirectory"
        if (-not $groups.ContainsKey($key)) {
            $groups[$key] = [ordered]@{
                Pack = $motionPack
                ClassName = $className
                PackTree = $motionPackTree
                RelativeDirectory = $relativeDirectory
                Motions = [ordered]@{}
            }
        }
        $motionKey = [IO.Path]::GetFullPath($motionPath).ToLowerInvariant()
        if (-not $groups[$key].Motions.Contains($motionKey)) {
            $groups[$key].Motions[$motionKey] = [ordered]@{
                MotionPath = [IO.Path]::GetFullPath($motionPath)
                MotionReference = $motionReference.Replace('\', '/')
                Definitions = [Collections.Generic.List[object]]::new()
            }
        }
        $groups[$key].Motions[$motionKey].Definitions.Add([ordered]@{
            name = [IO.Path]::GetFileNameWithoutExtension($msa.Name)
            msa = Relative-Path $motionPackTree $msa.FullName
            duration = Read-MsaValue $text 'MotionDuration'
            accumulation = Read-MsaLineValue $text 'Accumulation'
        })
    }
}

$orderedGroups = @($groups.Values | Sort-Object Pack, ClassName, RelativeDirectory)
if ($ScanOnly) {
    $scanClips = ($orderedGroups | ForEach-Object { $_.Motions.Count } | Measure-Object -Sum).Sum
    Write-Host "Motion groups: $($orderedGroups.Count)"
    Write-Host "Unique referenced clips: $scanClips"
    Write-Host "Missing references: $($missing.Count)"
    $orderedGroups | ForEach-Object { Write-Host ("- {0}/{1}: {2} clips" -f $_.Pack, $_.RelativeDirectory, $_.Motions.Count) }
    foreach ($item in $missing) { Write-Host ("MISSING " + $item) }
    exit 0
}
Ensure-Directory $tempRoot
$groupNo = 0
foreach ($group in $orderedGroups) {
    $groupNo++
    $classRoot = Join-Path $group.PackTree $group.ClassName
    $baseCandidates = @(
        (Join-Path $classRoot ($group.ClassName + '_novice.gr2'))
        (Join-Path $classRoot ($group.ClassName + '.gr2'))
    )
    $baseModel = $baseCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $baseModel) {
        $baseModel = Get-ChildItem -LiteralPath $classRoot -Filter '*novice*.gr2' -File -ErrorAction SilentlyContinue |
            Sort-Object Name | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $baseModel) {
        $failed.Add("BASE | $($group.Pack)/$($group.ClassName) | $classRoot")
        continue
    }

    $outputDirectory = Join-Path (Join-Path (Join-Path $outputRoot $group.Pack) $group.ClassName) $group.RelativeDirectory
    Ensure-Directory $outputDirectory
    $safeGroup = ($group.RelativeDirectory -replace '[^a-zA-Z0-9_-]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($safeGroup)) { $safeGroup = 'general' }
    $bundlePath = Join-Path $outputDirectory ($safeGroup + '_motions.fbx')
    $manifestPath = [IO.Path]::ChangeExtension($bundlePath, '.json')
    $motionEntries = @($group.Motions.Values)
    $clipCount += $motionEntries.Count

    $latestSource = ($motionEntries | ForEach-Object { Get-Item -LiteralPath $_.MotionPath } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
    if (-not $Force -and (Test-Path -LiteralPath $bundlePath) -and (Test-Path -LiteralPath $manifestPath) -and (Get-Item -LiteralPath $bundlePath).LastWriteTimeUtc -ge $latestSource) {
        $skipped++
        Write-Host "[$groupNo/$($orderedGroups.Count)] SKIP $($group.Pack)/$($group.RelativeDirectory) ($($motionEntries.Count) clips)"
        continue
    }

    $jobDirectory = Join-Path $tempRoot ('job_' + $groupNo.ToString('D4'))
    Ensure-Directory $jobDirectory
    $arguments = [Collections.Generic.List[string]]::new()
    $clips = [Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $motionEntries.Count; $i++) {
        $entry = $motionEntries[$i]
        $tempName = if ($i -eq 0) { 'bundle.gr2' } else { 'motion_' + $i.ToString('D4') + '.gr2' }
        $tempMotion = Join-Path $jobDirectory $tempName
        Copy-Item -LiteralPath $entry.MotionPath -Destination $tempMotion -Force
        $arguments.Add($tempMotion)
        $clips.Add([ordered]@{
            index = $i
            unityClip = 'animation_' + $i
            motion = Relative-Path $group.PackTree $entry.MotionPath
            reference = $entry.MotionReference
            definitions = @($entry.Definitions)
        })
    }
    $tempBase = Join-Path $jobDirectory 'base_model.gr2'
    Copy-Item -LiteralPath $baseModel -Destination $tempBase -Force
    $arguments.Add($tempBase)
    $produced = Join-Path $jobDirectory 'bundle.fbx'
    try {
        $exportOutput = & $Exporter $arguments 2>&1
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $produced)) {
            $failed.Add("EXPORT | $($group.Pack)/$($group.RelativeDirectory) | exit=$LASTEXITCODE | $($exportOutput -join ' ')")
            continue
        }
        Move-Item -LiteralPath $produced -Destination $bundlePath -Force
        $manifest = [ordered]@{
            version = 1
            pack = $group.Pack
            className = $group.ClassName
            motionDirectory = $group.RelativeDirectory.Replace('\', '/')
            baseModel = (Relative-Path $group.PackTree $baseModel).Replace('\', '/')
            bundle = [IO.Path]::GetFileName($bundlePath)
            clips = @($clips)
        }
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        $converted++
        Write-Host "[$groupNo/$($orderedGroups.Count)] OK   $($group.Pack)/$($group.RelativeDirectory) ($($motionEntries.Count) clips)"
    } catch {
        $failed.Add("EXCEPTION | $($group.Pack)/$($group.RelativeDirectory) | $($_.Exception.Message)")
    }
}

$messages.Add("Metin2 Player Conversion Report")
$messages.Add((Get-Date).ToString('u'))
$messages.Add("Project: $ProjectRoot")
$messages.Add("Exporter: $Exporter")
$messages.Add("Motion groups: $($orderedGroups.Count)")
$messages.Add("Referenced animation clips: $clipCount")
$messages.Add("Bundles converted: $converted")
$messages.Add("Bundles current/skipped: $skipped")
$messages.Add("Missing references: $($missing.Count)")
$messages.Add("Failed bundles: $($failed.Count)")
$messages.Add("")
$messages.Add("Missing references:")
foreach ($item in $missing) { $messages.Add("- " + $item) }
$messages.Add("")
$messages.Add("Failed bundles:")
foreach ($item in $failed) { $messages.Add("- " + $item) }
$messages | Set-Content -LiteralPath $logPath -Encoding UTF8

if (Test-Path -LiteralPath $tempRoot) {
    $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
    $expectedPrefix = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\Metin2Player_'
    if ($resolvedTemp.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}

Write-Host ""
Write-Host "Conversion complete. Bundles=$converted skipped=$skipped clips=$clipCount missing=$($missing.Count) failed=$($failed.Count)"
Write-Host "Report: $logPath"
if ($failed.Count -gt 0) { exit 2 }
