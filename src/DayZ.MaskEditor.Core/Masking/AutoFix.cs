using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;

namespace DayZ.MaskEditor.Core.Masking;

/// <summary>
/// In-place mask repair. Ports the GIMP plugin's two auto-fixes to operate
/// directly on a <see cref="PixelBuffer"/>:
///  • <see cref="SnapToLegend"/> — replace every stray pixel with the nearest
///    legend colour (the plugin's indexed-convert snap).
///  • <see cref="ConsolidateTiles"/> — bring each over-budget Terrain Builder
///    tile down to the colour limit by replacing its rarest colours
///    (port of maskcore.plan_tile_fix + fix_fragment, fragment-by-fragment).
/// Masks are always RGB (bpp 3).
/// </summary>
public static class AutoFix
{
    /// <summary>
    /// Snap every pixel that is not an exact legend colour to the nearest legend
    /// colour (squared-RGB distance). Returns the number of pixels changed.
    /// </summary>
    public static long SnapToLegend(PixelBuffer buf, IReadOnlyList<Surface> surfaces)
    {
        if (buf.BytesPerPixel != 3) throw new InvalidOperationException("Mask must be RGB (3 bpp).");
        var legend = surfaces.Select(s => s.Rgb.Packed).Distinct().ToArray();
        if (legend.Length == 0) return 0;

        var legendSet = new HashSet<int>(legend);
        var nearest = new Dictionary<int, int>();
        var data = buf.Data;
        long changed = 0;

        for (int i = 0; i + 2 < data.Length; i += 3)
        {
            int rgb = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
            if (legendSet.Contains(rgb)) continue;
            if (!nearest.TryGetValue(rgb, out int near))
            {
                near = Nearest(rgb, legend);
                nearest[rgb] = near;
            }
            data[i] = (byte)(near >> 16);
            data[i + 1] = (byte)(near >> 8);
            data[i + 2] = (byte)near;
            changed++;
        }
        return changed;
    }

    private static int Nearest(int rgb, int[] legend)
    {
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        int best = legend[0], bestD = int.MaxValue;
        foreach (int c in legend)
        {
            int dr = r - ((c >> 16) & 0xFF), dg = g - ((c >> 8) & 0xFF), db = b - (c & 0xFF);
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>
    /// Reduce every tile with more than <paramref name="maxColors"/> distinct colours
    /// to the limit, by replacing its rarest colours with dominant acceptable ones,
    /// fragment by fragment. Tiles are processed in place and in order, so the shared
    /// overlap fragments merge with neighbours (matching the plugin). Returns the
    /// number of tiles that were over the limit and got fixed.
    /// </summary>
    public static int ConsolidateTiles(
        PixelBuffer buf, int tileSize, int overlap, int tilesInRow, int maxColors)
    {
        if (buf.BytesPerPixel != 3) throw new InvalidOperationException("Mask must be RGB (3 bpp).");
        int fixedTiles = 0;

        for (int iy = 0; iy < tilesInRow; iy++)
        {
            var (sy, sh) = TileGeometry.TileRegion(iy, tilesInRow, tileSize, overlap, buf.Height);
            for (int ix = 0; ix < tilesInRow; ix++)
            {
                var (sx, sw) = TileGeometry.TileRegion(ix, tilesInRow, tileSize, overlap, buf.Width);

                var counts = CountRect(buf, sx, sy, sw, sh);
                if (counts.Count <= maxColors) continue;

                var (replaceSet, backup) = PlanFix(counts, maxColors);
                foreach (var f in TileGeometry.BuildTileFragments(ix, iy, sx, sy, sw, sh, overlap, tilesInRow))
                    FixFragment(buf, f, replaceSet, backup);

                fixedTiles++;
            }
        }
        return fixedTiles;
    }

    /// <summary>Pick the rarest colours to remove + the most common as fallback.</summary>
    private static (HashSet<int> Replace, int Backup) PlanFix(Dictionary<int, long> counts, int maxColors)
    {
        var asc = counts.OrderBy(kv => kv.Value).ToList(); // rarest first
        int removeCount = counts.Count - maxColors;
        var replace = new HashSet<int>(asc.Take(removeCount).Select(kv => kv.Key));
        int backup = asc[^1].Key; // most common
        return (replace, backup);
    }

    /// <summary>
    /// Within one fragment, replace any to-be-removed colour with the fragment's own
    /// most common acceptable colour (falling back to the tile's backup).
    /// </summary>
    private static void FixFragment(PixelBuffer buf, Fragment f, HashSet<int> replaceSet, int backup)
    {
        var counts = CountRect(buf, f.X, f.Y, f.W, f.H);
        bool needsWork = false;
        foreach (var key in counts.Keys)
            if (replaceSet.Contains(key)) { needsWork = true; break; }
        if (!needsWork) return;

        int dominant = backup;
        foreach (var kv in counts.OrderByDescending(kv => kv.Value))
            if (!replaceSet.Contains(kv.Key)) { dominant = kv.Key; break; }

        var data = buf.Data;
        int stride = buf.Stride;
        byte dr = (byte)(dominant >> 16), dg = (byte)(dominant >> 8), db = (byte)dominant;
        for (int y = f.Y; y < f.Y + f.H; y++)
        {
            int row = y * stride + f.X * 3;
            for (int x = 0; x < f.W; x++)
            {
                int o = row + x * 3;
                int rgb = (data[o] << 16) | (data[o + 1] << 8) | data[o + 2];
                if (replaceSet.Contains(rgb)) { data[o] = dr; data[o + 1] = dg; data[o + 2] = db; }
            }
        }
    }

    private static Dictionary<int, long> CountRect(PixelBuffer buf, int sx, int sy, int sw, int sh)
    {
        var counts = new Dictionary<int, long>();
        var data = buf.Data;
        int stride = buf.Stride;
        for (int y = sy; y < sy + sh; y++)
        {
            int row = y * stride + sx * 3;
            for (int x = 0; x < sw; x++)
            {
                int o = row + x * 3;
                int rgb = (data[o] << 16) | (data[o + 1] << 8) | data[o + 2];
                counts.TryGetValue(rgb, out long c);
                counts[rgb] = c + 1;
            }
        }
        return counts;
    }
}
