# Package DayZ-MaskEditor for Windows with Velopack.
# Prereq: dotnet tool install -g vpk
# Release notes come from the matching CHANGELOG.md section (see below).
# Pass -Publish to push the release to GitHub Releases (needs $env:GITHUB_TOKEN,
# a PAT with contents:write on openface/DayZ-MaskEditor).
param(
  [string]$Version = "1.0.0",
  [string]$Runtime = "win-x64",
  [switch]$Publish
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\DayZ.MaskEditor.App\DayZ.MaskEditor.App.csproj"
$pub  = Join-Path $root "publish\$Runtime"
$rel  = Join-Path $root "releases\$Runtime"
$repo = "https://github.com/openface/DayZ-MaskEditor"

# --- Release notes: extract the "## [$Version]" section from CHANGELOG.md ------------
# Returns the section body (between the version heading and the next "## [" heading),
# or $null if there is no such section.
function Get-ChangelogSection([string]$path, [string]$version) {
  if (-not (Test-Path $path)) { return $null }
  $lines = Get-Content -LiteralPath $path
  $start = -1
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match ("^##\s+\[" + [regex]::Escape($version) + "\]")) { $start = $i; break }
  }
  if ($start -lt 0) { return $null }
  $body = New-Object System.Collections.Generic.List[string]
  for ($j = $start + 1; $j -lt $lines.Count; $j++) {
    if ($lines[$j] -match "^##\s+\[") { break }
    $body.Add($lines[$j])
  }
  ($body -join "`n").Trim()
}

# Fail fast (before the long build) if publishing a version with no changelog entry.
$notes = Get-ChangelogSection (Join-Path $root "CHANGELOG.md") $Version
$notesFile = $null
if ($notes) {
  $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "maskeditor-notes-$Version.md"
  [System.IO.File]::WriteAllText($notesFile, $notes, (New-Object System.Text.UTF8Encoding $false))
} elseif ($Publish) {
  throw "No CHANGELOG.md section for [$Version]. Rename '## [Unreleased]' to '## [$Version] - <date>' before publishing."
} else {
  Write-Warning "No CHANGELOG.md section for [$Version]; packing without release notes."
}

if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
dotnet publish $proj -c Release -r $Runtime --self-contained true -o $pub `
  /p:PublishSingleFile=false

# Pull the current release so vpk can compute deltas against it.
if ($Publish) {
  if (-not $env:GITHUB_TOKEN) { throw "Set `$env:GITHUB_TOKEN (PAT with contents:write) to publish." }
  New-Item -ItemType Directory -Force -Path $rel | Out-Null
  vpk download github --repoUrl $repo --outputDir $rel --token $env:GITHUB_TOKEN
  if ($LASTEXITCODE -ne 0) {
    Write-Warning "vpk download github failed — expected on the first release (no prior release to delta against). Continuing with a full release."
  }
}

$packArgs = @(
  "pack",
  "--packId",      "DayZ.MaskEditor",
  "--packVersion", $Version,
  "--packDir",     $pub,
  "--mainExe",     "DayZ.MaskEditor.exe",
  "--packTitle",   "DayZ Mask Editor",
  "--outputDir",   $rel
)
if ($notesFile) { $packArgs += @("--releaseNotes", $notesFile) }
vpk @packArgs

if ($Publish) {
  vpk upload github --repoUrl $repo --outputDir $rel --token $env:GITHUB_TOKEN `
    --publish true --releaseName "DayZ Mask Editor $Version" --tag "v$Version"
  Write-Host "Published v$Version to GitHub Releases." -ForegroundColor Green
} else {
  Write-Host "Done. Installer + update feed in $rel (re-run with -Publish to release)." -ForegroundColor Green
}
