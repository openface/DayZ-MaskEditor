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
(`DefaultRepoUrl`) or set the `DAYZMASK_UPDATE_URL` env var.

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

Releases are hosted on the **GitHub Releases** of
[`openface/devtwo.com`](https://github.com/openface/devtwo.com) — the same repo that
serves the public download page via GitHub Pages. `UpdateService.DefaultRepoUrl` already
points there (override per-machine with `DAYZMASK_UPDATE_URL`).

1. Bump the version and pack (Windows):

   ```powershell
   build\pack-windows.ps1 -Version X.Y.Z
   ```

   This produces, under `releases\win-x64\`: `RELEASES`, `DayZ.MaskEditor-X.Y.Z-full.nupkg`
   (plus delta `.nupkg` packages once a prior release exists), `DayZ.MaskEditor-win-Setup.exe`,
   and `DayZ.MaskEditor-win-Portable.zip`.

2. Create a **GitHub Release** on `openface/devtwo.com` tagged `vX.Y.Z` and upload the
   **entire** `releases\win-x64\` contents (the `RELEASES` index and every `.nupkg` must be
   attached, not just the installer — Velopack reads them to compute the update).

3. Installed clients call `UpdateService.CheckAndApplyAsync` on launch, download the delta,
   and restart into the new version. New users download from the public page:
   <https://devtwo.com/projects/dayz-mask-editor.html>.

> The download button on that page links to
> `https://github.com/openface/devtwo.com/releases/latest/download/DayZ.MaskEditor-win-Setup.exe`,
> so keep the Setup asset name stable across releases (it follows `--packId`).

## Notes

- **macOS signing/notarization** is optional for v1 (fine for local/dev use). For
  frictionless distribution, pass `--signAppIdentity` / `--notaryProfile` to `vpk`.
- Builds are **self-contained** (no .NET install required on the target machine).
- Keep the `--packId` (`DayZ.MaskEditor`) stable across releases — Velopack keys
  updates off it.
