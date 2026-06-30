# DayZ Mask Editor

A standalone, cross-platform editor for DayZ terrain **surface masks** — no GIMP
required. Load a terrain's `layers.cfg`, satmap, and surface mask; overlay the mask
on the satmap; paint with a hard-edged pencil constrained to the fixed legend
palette; and validate against DayZ's rules (exact legend colours, 4/6 colours per
tile).

This is a standalone rewrite of the
[Gimp-DayZ-SatMask-Tools](../Gimp-DayZ-SatMask-Tools) plugin. The pure parsing and
pixel/tile logic was ported faithfully (and pinned by the same unit tests) into
`DayZ.MaskEditor.Core`.

## Features (v1)

- **Load** `layers.cfg` (Arma/DayZ config format), satmap, and mask.
- **Surface browser / fixed palette** — one swatch per legend colour, with live
  coverage %. Click to arm; only legend colours are paintable.
- **Overlay editor** — mask drawn over the satmap with an opacity slider; pan
  (middle/right-drag), zoom (wheel), fit-to-view. Renders only the visible viewport,
  so 15360²-class terrains stay responsive.
- **Hard pencil** — exact 8-bit legend colour, no anti-aliasing, adjustable size.
- **Validation (report-only)** — Check Legend Colours (stray detection + magenta
  highlight), Check Colours Per Tile (Terrain Builder geometry, ASCII grid, red tile
  highlight), Check Image Specs. Optional tile-grid overlay.
- **Shapefile overlays** — load one or more Terrain Builder `.shp` exports (roads,
  objects, areas) as read-only reference layers drawn over the satmap, so you can see
  where in-world features sit while painting. Per-layer colour/visibility/opacity. World
  coordinates are mapped to mask pixels from the **Mapframe values you enter on the
  Terrain tab** (Easting/Northing, grid size, cell size, source px, flip Y) — nothing is
  guessed. The editor blocks rendering and tells you what's wrong if the setup is
  incomplete, the loaded mask doesn't match the declared source size, or a shapefile
  falls outside the terrain extent.
- **Save** the mask back to a lossless PNG (byte-exact).
- **Auto-update** via Velopack on Windows/macOS/Linux.

## Project layout

| Project | Role |
| --- | --- |
| `src/DayZ.MaskEditor.Core` | Pure logic: cfg parser, pixel/tile engine, validation, image I/O. No UI. |
| `src/DayZ.MaskEditor.App` | Avalonia UI: surface browser, `MaskCanvas`, validation, settings, updates. |
| `tests/DayZ.MaskEditor.Core.Tests` | xUnit — ports the plugin's parity tests plus round-trip/validation tests. |
| `build/` | Velopack packaging scripts (see `build/README.md`). |
| `samples/DemoTerrain` | A tiny generated terrain (layers.cfg + satmap + mask) to try the editor. |

## Build & run

```sh
dotnet test                                   # 25 tests (parity + I/O + validation)
dotnet run --project src/DayZ.MaskEditor.App  # launch the editor
```

Then **Browse** to `samples/DemoTerrain/source/` for `layers.cfg`, `satmap.png`, and
`mask.png`, and click **Load**. To try shapefile overlays, open the **Terrain** tab and
enter the demo's Mapframe values — Easting `0`, Northing `0`, Grid size `1024`, Cell size
`1`, Source image `1024`, Flip Y on — then **Add shapefile(s)** from
`samples/DemoTerrain/source/shapes/` (`demo_roads`, `demo_objects`, `demo_areas`).

## Deferred to later releases

Auto-fix (snap-to-legend, per-tile consolidation), palette export, replace-surface,
create-new-mask, and Terrain Builder tile import/export.
