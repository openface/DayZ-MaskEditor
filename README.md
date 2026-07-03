# DayZ Mask Editor

[![tests](https://github.com/openface/DayZ-MaskEditor/actions/workflows/tests.yml/badge.svg)](https://github.com/openface/DayZ-MaskEditor/actions/workflows/tests.yml)
[![Website](https://img.shields.io/badge/website-openface.github.io-00a8e8)](https://openface.github.io/DayZ-MaskEditor/)
[![Download for Windows](https://img.shields.io/badge/download-Windows-008cc3?logo=windows)](https://github.com/openface/DayZ-MaskEditor/releases/latest/download/DayZ.MaskEditor-win-Setup.exe)
[![Latest release](https://img.shields.io/github/v/release/openface/DayZ-MaskEditor)](https://github.com/openface/DayZ-MaskEditor/releases/latest)

A standalone, cross-platform editor for DayZ terrain **surface masks** — no GIMP required.
Load a terrain's `layers.cfg`, satmap, and surface mask; overlay the mask on the satmap;
paint with a hard-edged pencil constrained to the fixed legend palette; and validate against
DayZ's rules (exact legend colours, 4/6 colours per tile) before you run Terrain Builder's
**Generate Layers**.

It's a standalone rewrite of the
[Gimp-DayZ-SatMask-Tools](https://github.com/openface/Gimp-DayZ-SatMask-Tools) plugin — the
parsing and pixel/tile logic was ported faithfully, and pinned by the same unit tests, into
`DayZ.MaskEditor.Core`.

> **What it does and how to use it** lives in the
> **[documentation & usage guide →](https://openface.github.io/DayZ-MaskEditor/)**.

## Install

**Windows** — [download the installer](https://github.com/openface/DayZ-MaskEditor/releases/latest/download/DayZ.MaskEditor-win-Setup.exe)
and run it. One click, no .NET needed (the app is self-contained); it installs per-user with
Start-menu and desktop shortcuts.

**macOS / Linux** — the app is cross-platform, but prebuilt installers aren't published for
every release. Grab an asset from the
[Releases](https://github.com/openface/DayZ-MaskEditor/releases) page if one is present, or
build from source (below).

## Updating

The app **auto-updates**: on launch it checks
[GitHub Releases](https://github.com/openface/DayZ-MaskEditor/releases), downloads any newer
version in the background, and applies it on the next start — no reinstall needed. To point
the updater at a different repo, set the `DAYZMASK_UPDATE_URL` environment variable.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet test                                   # 38 tests (parity + I/O + validation + geometry)
dotnet run --project src/DayZ.MaskEditor.App  # launch the editor
```
