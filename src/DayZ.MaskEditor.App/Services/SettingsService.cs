using System.Text.Json;
using System.Text.Json.Serialization;

namespace DayZ.MaskEditor.App.Services;

/// <summary>
/// Persisted user settings (the standalone equivalent of the plugin's
/// dayz-satmask-settings.json). Stored in the OS app-data dir.
/// </summary>
public sealed class AppSettings
{
    public string? LastCfg { get; set; }
    public string? LastSatmap { get; set; }
    public string? LastMask { get; set; }

    public int BrushSize { get; set; } = 5;

    public int TileSize { get; set; } = 512;
    public int TileOverlap { get; set; } = 32;
    public int TilesInRow { get; set; } = 22;
    public int TileMaxColors { get; set; } = 6;
    public bool ShowTileGrid { get; set; }

    public double OverlayOpacity { get; set; } = 0.6;

    public double WinWidth { get; set; } = 1200;
    public double WinHeight { get; set; } = 800;

    [JsonIgnore]
    public string FilePath { get; set; } = SettingsService.DefaultPath;

    public void Save() => SettingsService.Save(this);
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string DefaultPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DayZ-MaskEditor");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        var path = DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts);
                if (s != null) { s.FilePath = path; return s; }
            }
        }
        catch { /* corrupt settings -> fall back to defaults */ }
        return new AppSettings { FilePath = path };
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(settings.FilePath, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch { /* best-effort persistence */ }
    }
}
