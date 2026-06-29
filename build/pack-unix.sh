#!/usr/bin/env bash
# Package DayZ-MaskEditor for macOS or Linux with Velopack.
# Prereq: dotnet tool install -g vpk
# Usage: ./pack-unix.sh <version> <runtime>
#   runtime: osx-arm64 | osx-x64 | linux-x64
set -euo pipefail

VERSION="${1:-1.0.0}"
RUNTIME="${2:-osx-arm64}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/src/DayZ.MaskEditor.App/DayZ.MaskEditor.App.csproj"
PUB="$ROOT/publish/$RUNTIME"
REL="$ROOT/releases/$RUNTIME"

rm -rf "$PUB"
dotnet publish "$PROJ" -c Release -r "$RUNTIME" --self-contained true -o "$PUB"

# macOS uses the bare executable name; Linux produces an AppImage.
vpk pack \
  --packId DayZ.MaskEditor \
  --packVersion "$VERSION" \
  --packDir "$PUB" \
  --mainExe DayZ.MaskEditor \
  --packTitle "DayZ Mask Editor" \
  --outputDir "$REL"

echo "Done. Artifacts + update feed in $REL"
