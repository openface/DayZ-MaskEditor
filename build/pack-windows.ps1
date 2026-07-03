# Package DayZ-MaskEditor for Windows with Velopack.
# Prereq: dotnet tool install -g vpk
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

if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
dotnet publish $proj -c Release -r $Runtime --self-contained true -o $pub `
  /p:PublishSingleFile=false

# Pull the current release so vpk can compute deltas against it.
if ($Publish) {
  if (-not $env:GITHUB_TOKEN) { throw "Set `$env:GITHUB_TOKEN (PAT with contents:write) to publish." }
  New-Item -ItemType Directory -Force -Path $rel | Out-Null
  vpk download github --repoUrl $repo --outputDir $rel --token $env:GITHUB_TOKEN
}

vpk pack `
  --packId DayZ.MaskEditor `
  --packVersion $Version `
  --packDir $pub `
  --mainExe DayZ.MaskEditor.exe `
  --packTitle "DayZ Mask Editor" `
  --outputDir $rel

if ($Publish) {
  vpk upload github --repoUrl $repo --outputDir $rel --token $env:GITHUB_TOKEN `
    --publish --releaseName "DayZ Mask Editor $Version" --tag "v$Version"
  Write-Host "Published v$Version to GitHub Releases." -ForegroundColor Green
} else {
  Write-Host "Done. Installer + update feed in $rel (re-run with -Publish to release)." -ForegroundColor Green
}
