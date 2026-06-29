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

1. Run the pack script with a higher `--packVersion` than the last release.
2. Upload the entire `releases/<runtime>/` contents to a **GitHub Release** on the
   repo named in `UpdateService.DefaultRepoUrl`.
3. Installed clients call `UpdateService.CheckAndApplyAsync` on launch, download the
   delta, and restart into the new version.

## Notes

- **macOS signing/notarization** is optional for v1 (fine for local/dev use). For
  frictionless distribution, pass `--signAppIdentity` / `--notaryProfile` to `vpk`.
- Builds are **self-contained** (no .NET install required on the target machine).
- Keep the `--packId` (`DayZ.MaskEditor`) stable across releases — Velopack keys
  updates off it.
