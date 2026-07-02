# Packaging & Releases (Velopack)

DayZ-MaskEditor ships via [Velopack](https://velopack.io): one-click installers and
delta auto-updates on Windows, macOS, and Linux.

## One-time setup

```sh
dotnet tool install -g vpk
```

## Build an installer + update feed

The app calls `VelopackApp.Build().Run()` first thing in `Program.cs`, and checks
for updates on startup via `Services/UpdateService.cs`. Update the feed URL there
(`DefaultFeedUrl`) or set the `DAYZMASK_UPDATE_URL` env var.

**Windows**

```powershell
build\pack-windows.ps1 -Version 1.0.0
```

**macOS / Linux**

```sh
build/pack-unix.sh 1.0.0 osx-arm64    # or osx-x64, linux-x64
```

Each produces, under `releases/<runtime>/`:

- a **Setup** installer (`-Setup.exe`, `.dmg`/`.pkg`, or `.AppImage`)
- the **release feed** (`RELEASES`, `*-full.nupkg`, and delta packages)

## Publishing a release / enabling auto-update

The update feed is a **static directory served from the devtwo.com VPS** at
`https://devtwo.com/downloads/dayz-mask-editor/`. The app reads it via Velopack's
`SimpleWebSource` (`UpdateService.DefaultFeedUrl`; override per-machine with
`DAYZMASK_UPDATE_URL`). Publishing = pack locally, then **FTP the feed folder up**.

1. Bump the version and pack (Windows):

   ```powershell
   build\pack-windows.ps1 -Version X.Y.Z
   ```

   This appends to `releases\win-x64\`, producing `RELEASES`,
   `DayZ.MaskEditor-X.Y.Z-full.nupkg` (plus delta `.nupkg` packages against the previous
   release), `DayZ.MaskEditor-win-Setup.exe`, and `DayZ.MaskEditor-win-Portable.zip`.

   > Keep `releases\win-x64\` between releases — `vpk` needs the prior `*-full.nupkg` there
   > to compute deltas. (The pack script clears `publish\`, not `releases\`.)

2. **FTP the entire `releases\win-x64\` contents** to
   `devtwo.com/downloads/dayz-mask-editor/` — the `RELEASES` index and **every** `.nupkg`
   must be uploaded (not just the installer), or Velopack can't compute the update.

3. Installed clients call `UpdateService.CheckAndApplyAsync` on launch, read `RELEASES`,
   download the delta, and restart. New users download from the public page
   (<https://devtwo.com/projects/dayz-mask-editor/>), whose button points at
   `https://devtwo.com/downloads/dayz-mask-editor/DayZ.MaskEditor-win-Setup.exe`.

> The Setup filename follows `--packId`, so it stays stable across releases and the
> download link keeps working. Confirm the exact name on the first `vpk pack`.

## Notes

- **macOS signing/notarization** is optional for v1 (fine for local/dev use). For
  frictionless distribution, pass `--signAppIdentity` / `--notaryProfile` to `vpk`.
- Builds are **self-contained** (no .NET install required on the target machine).
- Keep the `--packId` (`DayZ.MaskEditor`) stable across releases — Velopack keys
  updates off it.
