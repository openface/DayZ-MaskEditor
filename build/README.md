# Packaging & Releases (Velopack)

DayZ-MaskEditor ships via [Velopack](https://velopack.io): one-click installers and
delta auto-updates on Windows, macOS, and Linux.

## One-time setup

```sh
dotnet tool install -g vpk
```

## Build an installer + update feed

The app calls `VelopackApp.Build().Run()` first thing in `Program.cs`, and checks
for updates on startup via `Services/UpdateService.cs`. It updates from this repo's
GitHub Releases (`GithubSource`); the repo is set there (`DefaultRepoUrl`) and can be
overridden per-machine with the `DAYZMASK_UPDATE_URL` env var.

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

Releases are published to this repo's **GitHub Releases**; the app reads them via
Velopack's `GithubSource` (`UpdateService.DefaultRepoUrl`; override per-machine with
`DAYZMASK_UPDATE_URL`). Publishing = pack **and** upload in one command with `-Publish`.

Set a `GITHUB_TOKEN` env var first — a PAT with `contents:write` on
`openface/DayZ-MaskEditor`.

1. Bump the version, pack, and publish (Windows):

   ```powershell
   $env:GITHUB_TOKEN = "<pat>"
   build\pack-windows.ps1 -Version X.Y.Z -Publish
   ```

   With `-Publish` the script first runs `vpk download github` to pull the current
   release into `releases\win-x64\` (so `vpk` can compute deltas against the prior
   `*-full.nupkg`), then `vpk pack`, then `vpk upload github --publish`. It produces
   `RELEASES`, `DayZ.MaskEditor-X.Y.Z-full.nupkg` (plus deltas),
   `DayZ.MaskEditor-win-Setup.exe`, and `DayZ.MaskEditor-win-Portable.zip`, and creates
   a `vX.Y.Z` GitHub Release with those assets attached.

   > Because deltas are pulled from GitHub each run, you don't need to keep
   > `releases\win-x64\` between releases. Omit `-Publish` for a local-only build.

2. Installed clients call `UpdateService.CheckAndApplyAsync` on launch, read the release
   `RELEASES`, download the delta, and restart. New users download from the GitHub Pages
   site (<https://openface.github.io/DayZ-MaskEditor/>), whose button points at
   `https://github.com/openface/DayZ-MaskEditor/releases/latest/download/DayZ.MaskEditor-win-Setup.exe`.

> The Setup filename follows `--packId`, so it stays stable across releases and the
> `releases/latest/download/…` link keeps working. Confirm the exact name on the first
> `vpk pack`.

## Notes

- **macOS signing/notarization** is optional for v1 (fine for local/dev use). For
  frictionless distribution, pass `--signAppIdentity` / `--notaryProfile` to `vpk`.
- Builds are **self-contained** (no .NET install required on the target machine).
- Keep the `--packId` (`DayZ.MaskEditor`) stable across releases — Velopack keys
  updates off it.
