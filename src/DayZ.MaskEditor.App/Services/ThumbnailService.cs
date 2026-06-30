using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DayZ.MaskEditor.App.Services;

/// <summary>
/// Loads a surface's texture thumbnail (96×96 PNG), mirroring the GIMP plugin:
/// look up by surface name — lower-cased, with the multi-colour " #2" suffix
/// stripped (those share one texture) — first in a per-terrain "thumbnails" folder
/// next to layers.cfg, then in the bundled set. Misses are cached so a surface
/// without art isn't probed again.
/// </summary>
public static class ThumbnailService
{
    private static readonly Dictionary<string, Bitmap?> _assetCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string surfaceName)
    {
        int hash = surfaceName.IndexOf(" #", StringComparison.Ordinal);
        string baseName = hash >= 0 ? surfaceName[..hash] : surfaceName;
        return baseName.Trim().ToLowerInvariant();
    }

    public static Bitmap? Load(string surfaceName, string? cfgPath)
    {
        string key = Key(surfaceName);
        if (string.IsNullOrEmpty(key)) return null;

        // 1. Per-terrain override: <cfg dir>/thumbnails/<key>.png (not cached — may change).
        if (!string.IsNullOrEmpty(cfgPath))
        {
            string? dir = Path.GetDirectoryName(cfgPath);
            if (dir is not null)
            {
                string p = Path.Combine(dir, "thumbnails", key + ".png");
                if (File.Exists(p))
                {
                    try { return new Bitmap(p); }
                    catch { /* fall through to the bundled set */ }
                }
            }
        }

        // 2. Bundled asset (cached, including misses).
        if (_assetCache.TryGetValue(key, out var cached)) return cached;
        Bitmap? bmp = null;
        var uri = new Uri($"avares://DayZ.MaskEditor.App/Assets/Thumbnails/{key}.png");
        try { if (AssetLoader.Exists(uri)) bmp = new Bitmap(AssetLoader.Open(uri)); }
        catch { bmp = null; }
        _assetCache[key] = bmp;
        return bmp;
    }
}
