#!/usr/bin/env bash
# Package DayZ-MaskEditor for macOS or Linux with Velopack.
# Prereq: dotnet tool install -g vpk
# Release notes come from the matching CHANGELOG.md section.
# Usage: ./pack-unix.sh <version> <runtime> [--publish]
#   runtime: osx-arm64 | osx-x64 | linux-x64
#   --publish pushes to GitHub Releases (needs $GITHUB_TOKEN, PAT with contents:write).
set -euo pipefail

VERSION="${1:-1.0.0}"
RUNTIME="${2:-osx-arm64}"
PUBLISH="${3:-}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/src/DayZ.MaskEditor.App/DayZ.MaskEditor.App.csproj"
PUB="$ROOT/publish/$RUNTIME"
REL="$ROOT/releases/$RUNTIME"
REPO="https://github.com/openface/DayZ-MaskEditor"

# --- Release notes: extract the "## [$VERSION]" section from CHANGELOG.md -------------
# Prints the section body (between the version heading and the next "## [" heading).
NOTES=""
if [ -f "$ROOT/CHANGELOG.md" ]; then
  NOTES="$(awk -v ver="$VERSION" '
    index($0, "## [" ver "]") == 1 {flag=1; next}
    flag && /^## \[/ {flag=0}
    flag {print}
  ' "$ROOT/CHANGELOG.md")"
fi

# Fail fast (before the long build) if publishing a version with no changelog entry.
NOTES_FILE=""
if [ -n "$(printf '%s' "$NOTES" | tr -d '[:space:]')" ]; then
  NOTES_FILE="${TMPDIR:-/tmp}/maskeditor-notes-$VERSION.md"
  printf '%s\n' "$NOTES" > "$NOTES_FILE"
elif [ "$PUBLISH" = "--publish" ]; then
  echo "ERROR: No CHANGELOG.md section for [$VERSION]. Rename '## [Unreleased]' to '## [$VERSION] - <date>' before publishing." >&2
  exit 1
else
  echo "No CHANGELOG.md section for [$VERSION]; packing without release notes."
fi

rm -rf "$PUB"
dotnet publish "$PROJ" -c Release -r "$RUNTIME" --self-contained true -o "$PUB"

# Pull the current release so vpk can compute deltas against it.
if [ "$PUBLISH" = "--publish" ]; then
  : "${GITHUB_TOKEN:?Set GITHUB_TOKEN (PAT with contents:write) to publish.}"
  mkdir -p "$REL"
  # First release has no prior release to delta against; don't let that abort the run.
  vpk download github --repoUrl "$REPO" --outputDir "$REL" --token "$GITHUB_TOKEN" \
    || echo "vpk download github failed — expected on the first release; continuing with a full release."
fi

# macOS uses the bare executable name; Linux produces an AppImage.
PACK_ARGS=(--packId DayZ.MaskEditor --packVersion "$VERSION" --packDir "$PUB" \
  --mainExe DayZ.MaskEditor --packTitle "DayZ Mask Editor" --outputDir "$REL")
[ -n "$NOTES_FILE" ] && PACK_ARGS+=(--releaseNotes "$NOTES_FILE")
vpk pack "${PACK_ARGS[@]}"

if [ "$PUBLISH" = "--publish" ]; then
  vpk upload github --repoUrl "$REPO" --outputDir "$REL" --token "$GITHUB_TOKEN" \
    --publish true --releaseName "DayZ Mask Editor $VERSION" --tag "v$VERSION"
  echo "Published v$VERSION to GitHub Releases."
else
  echo "Done. Artifacts + update feed in $REL (append --publish to release)."
fi
