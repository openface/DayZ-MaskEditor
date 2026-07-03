using Velopack;
using Velopack.Sources;

namespace DayZ.MaskEditor.App.Services;

/// <summary>
/// Velopack auto-update against this project's GitHub Releases (packages are published
/// there with <c>vpk upload github</c>). No-ops when the app is running uninstalled
/// (e.g. a dev build), so it is safe to call on start.
/// </summary>
public static class UpdateService
{
    /// <summary>
    /// The GitHub repository whose Releases hold the Velopack packages (the <c>RELEASES</c>
    /// index and <c>.nupkg</c> assets). Override at runtime via the DAYZMASK_UPDATE_URL
    /// env var (interpreted as a repo URL).
    /// </summary>
    public const string DefaultRepoUrl = "https://github.com/openface/DayZ-MaskEditor";

    public static string RepoUrl =>
        Environment.GetEnvironmentVariable("DAYZMASK_UPDATE_URL") is { Length: > 0 } u
            ? u
            : DefaultRepoUrl;

    /// <summary>
    /// Check for, download and apply an update, restarting if one was applied.
    /// Failures are reported via <paramref name="log"/> and otherwise swallowed —
    /// an update hiccup must never block the editor.
    /// </summary>
    public static async Task CheckAndApplyAsync(Action<string> log)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
            if (!mgr.IsInstalled) return; // dev / portable run

            var info = await mgr.CheckForUpdatesAsync();
            if (info is null) return; // already current

            log($"Update available: {info.TargetFullRelease.Version}. Downloading…");
            await mgr.DownloadUpdatesAsync(info);
            log("Update downloaded — restarting to apply.");
            mgr.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            log("Update check failed: " + ex.Message);
        }
    }
}
