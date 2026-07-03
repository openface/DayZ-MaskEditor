# Changelog

All notable changes to DayZ Mask Editor are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While working on `master`, add entries under **[Unreleased]**. To cut a release, rename
that heading to the new version (e.g. `## [1.0.1] - 2026-07-10`), add a fresh empty
`## [Unreleased]` above it, then run `build\pack-windows.ps1 -Version 1.0.1 -Publish`.
The pack script feeds that version's section to Velopack as the release notes.

## [Unreleased]

## [1.0.0] - 2026-07-03

First public release — a standalone, cross-platform rewrite of the GIMP *DayZ SatMask
Tools* plugin. No GIMP required.

### Added
- Load `layers.cfg`, satmap, and surface mask; mask overlaid on the satmap with an
  opacity slider, pan/zoom/fit, responsive on 15360²-class terrains.
- Surface browser / fixed legend palette with live coverage %, texture-thumbnail
  previews, and an eyedropper to arm a surface from the mask.
- Hard pencil locked to exact 8-bit legend colours, with adjustable size and undo/redo.
- Validation: stray-colour check, per-tile colour-budget check against Terrain Builder
  geometry, and image-spec checks, with an optional tile-grid overlay.
- Auto-fix: snap strays to the nearest legend colour, and consolidate over-limit tiles.
- Read-only Terrain Builder shapefile overlays, mapped to mask pixels from the Mapframe
  values entered on the Terrain tab.
- Byte-exact PNG save.
- Auto-updates via Velopack (Windows/macOS/Linux) from GitHub Releases.
