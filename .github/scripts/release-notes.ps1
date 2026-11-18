#Requires -Version 7
# The patch notes of one release, for its GitHub release page (cd.yml hands them to vpk pack --releaseNotes, and vpk upload
# github puts them in the new release's description): the sections of CHANGELOG.md whose builds came after the previous
# release, up to this one's (a section of several builds counts when one of them does), newest first as the file has them.
# A release of several builds opens with their short versions, then their details.
# Fails when the file has no section for this build, or when a section from build 75 on has no "### In short": every
# release starts with its short version (the builds before have the details only).
#   ./release-notes.ps1 -Build 76 -OutFile notes.md                the previous release is the highest v1.0.* tag below 76
#   ./release-notes.ps1 -Build 72 -Previous 69 -OutFile notes.md   builds 70 to 72
param(
    [Parameter(Mandatory)][int] $Build,
    [int] $Previous = -1,
    [Parameter(Mandatory)][string] $OutFile,
    [string] $Changelog = (Join-Path $PSScriptRoot '../../CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'
$firstShortBuild = 75

if ($Previous -lt 0) {
    # The highest release below this one, from the tags of the script's own repository. None found is an error, or a
    # checkout without its tags would quietly write every build: -Previous 0 asks for that.
    $tags = git -C $PSScriptRoot tag --list 'v1.0.*'
    if ($LASTEXITCODE) { throw "Can't read the release tags (git exit $LASTEXITCODE); pass -Previous." }
    $releases = $tags | ForEach-Object { if ($_ -match '^v1\.0\.(\d+)$' -and [int]$Matches[1] -lt $Build) { [int]$Matches[1] } }
    if ($null -eq $releases) { throw "No v1.0.* tag below build $Build (a checkout without tags?); pass -Previous (0: every build)." }
    $Previous = ($releases | Measure-Object -Maximum).Maximum
}

# "## 18 Nov 2026 · build 75", "## 10 Nov 2026 · builds 49–60" (as PatchNotes.cs reads them).
$sections = [System.Collections.Generic.List[object]]::new()
foreach ($line in Get-Content -LiteralPath $Changelog -Encoding utf8) {
    if ($line -match '^## ') {
        $section = [pscustomobject]@{ Heading = $line.TrimEnd(); From = -1; To = -1; Lines = [System.Collections.Generic.List[string]]::new() }
        if ($line -match '^##\s+\S.*?\s*·\s*builds?\s+(\d+)(?:\s*[-–—]\s*(\d+))?\s*$') {
            $section.From = [int]$Matches[1]
            $section.To = if ($Matches[2]) { [int]$Matches[2] } else { $section.From }
        }
        $sections.Add($section)
    }
    elseif ($sections.Count -gt 0) {
        $sections[-1].Lines.Add($line)
    }
}

$picked = @($sections | Where-Object { $_.From -ge 0 -and $_.From -le $Build -and $_.To -gt $Previous })
if (-not ($picked | Where-Object { $_.To -ge $Build })) {
    throw "$Changelog has no section for build $Build (a heading such as '## 18 Nov 2026 · build $Build')."
}

foreach ($section in $picked) {
    if ($section.To -ge $firstShortBuild -and -not ($section.Lines | Where-Object { $_.Trim() -eq '### In short' })) {
        throw "'$($section.Heading)' has no '### In short': every release's patch notes start with the short version, then '### In detail'."
    }
}

# A release of several builds opens with their short versions, then their details (a section before build 75 has the
# details only); a single build's section goes as written.
$short, $detail = @(), @()
foreach ($section in $picked) {
    $at = $section.Lines.FindIndex({ param($line) $line.Trim() -eq '### In detail' })
    if ($picked.Count -gt 1 -and $at -gt 0) {
        $short += @($section.Heading) + $section.Lines.GetRange(0, $at)
        $detail += @($section.Heading, '') + $section.Lines.GetRange($at, $section.Lines.Count - $at)
    }
    else { $detail += @($section.Heading) + $section.Lines }
}
$notes = ($short + $detail) -join "`n"
Set-Content -LiteralPath $OutFile -Value $notes.Trim() -Encoding utf8
Write-Host "Patch notes for build $Build (after build $Previous): $(($picked | ForEach-Object Heading) -join ', ')"
