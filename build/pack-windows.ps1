# Package DayZ-MaskEditor for Windows with Velopack.
# Prereq: dotnet tool install -g vpk
param(
  [string]$Version = "1.0.0",
  [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\DayZ.MaskEditor.App\DayZ.MaskEditor.App.csproj"
$pub  = Join-Path $root "publish\$Runtime"
$rel  = Join-Path $root "releases\$Runtime"

if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
dotnet publish $proj -c Release -r $Runtime --self-contained true -o $pub `
  /p:PublishSingleFile=false

vpk pack `
  --packId DayZ.MaskEditor `
  --packVersion $Version `
  --packDir $pub `
  --mainExe DayZ.MaskEditor.exe `
  --packTitle "DayZ Mask Editor" `
  --outputDir $rel

Write-Host "Done. Installer + update feed in $rel" -ForegroundColor Green
