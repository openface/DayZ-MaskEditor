using System.Text;
using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;

namespace DayZ.MaskEditor.Core.Masking;

/// <summary>One satellite tile that exceeds the per-tile colour budget.</summary>
public readonly record struct OverLimitTile(
    int Ix, int Iy, int X, int Y, int W, int H, int ColorCount);

/// <summary>Result of the per-tile colour-budget check.</summary>
public sealed class TileCheckResult
{
    public int TilesInRow { get; init; }
    public int TotalTiles { get; init; }
    public int MaxColors { get; init; }
    public int MaxColorsFound { get; init; }
    public IReadOnlyList<OverLimitTile> OverLimit { get; init; } = Array.Empty<OverLimitTile>();
    public string AsciiGrid { get; init; } = "";
    public bool Passed => OverLimit.Count == 0;
}

/// <summary>Result of the structural image-spec check.</summary>
public sealed class ImageSpecResult
{
    public bool IsRgb { get; init; }
    public bool Is8Bit { get; init; }
    public bool NoAlpha { get; init; }
    public bool NotIndexed { get; init; }
    public bool IsSquare { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Passed => IsRgb && Is8Bit && NoAlpha && NotIndexed && IsSquare;
}

/// <summary>
/// Read-only validation against DayZ's mask rules (v1 reports only; no auto-fix).
/// Reuses <see cref="MaskCore"/> and <see cref="TileGeometry"/> so it stays in
/// lock-step with the ported plugin logic.
/// </summary>
public static class Validation
{
    /// <summary>
    /// Check every pixel against the legend palette. Returns the Check Legend
    /// summary; stray colours (for canvas highlight) are in <c>Strays</c>.
    /// </summary>
    public static LegendSummary CheckLegend(PixelBuffer buf, IReadOnlyList<Surface> surfaces)
    {
        var counter = new Dictionary<int, long>();
        for (int y = 0; y < buf.Height; y++)
            MaskCore.TallyColors(buf.Data.AsSpan(y * buf.Stride, buf.Stride),
                buf.BytesPerPixel, counter);
        long total = (long)buf.Width * buf.Height;
        return MaskCore.Summarize(counter, total, surfaces);
    }

    /// <summary>
    /// Walk the Terrain Builder tile grid and flag tiles with more than
    /// <paramref name="maxColors"/> distinct colours.
    /// </summary>
    public static TileCheckResult CheckTiles(
        PixelBuffer buf, int tileSize, int overlap, int tilesInRow, int maxColors)
    {
        var over = new List<OverLimitTile>();
        int maxFound = 0;
        var grid = new StringBuilder();

        for (int iy = 0; iy < tilesInRow; iy++)
        {
            var (sy, sh) = TileGeometry.TileRegion(iy, tilesInRow, tileSize, overlap, buf.Height);
            for (int ix = 0; ix < tilesInRow; ix++)
            {
                var (sx, sw) = TileGeometry.TileRegion(ix, tilesInRow, tileSize, overlap, buf.Width);
                int count = CountTileColors(buf, sx, sy, sw, sh);
                if (count > maxFound) maxFound = count;
                bool bad = count > maxColors;
                if (bad) over.Add(new OverLimitTile(ix, iy, sx, sy, sw, sh, count));
                grid.Append(bad ? '#' : '.');
            }
            grid.Append('\n');
        }

        over.Sort((a, b) => b.ColorCount.CompareTo(a.ColorCount));
        return new TileCheckResult
        {
            TilesInRow = tilesInRow,
            TotalTiles = tilesInRow * tilesInRow,
            MaxColors = maxColors,
            MaxColorsFound = maxFound,
            OverLimit = over,
            AsciiGrid = grid.ToString(),
        };
    }

    private static int CountTileColors(PixelBuffer buf, int sx, int sy, int sw, int sh)
    {
        var set = new HashSet<int>();
        int bpp = buf.BytesPerPixel;
        for (int y = sy; y < sy + sh; y++)
        {
            int rowStart = y * buf.Stride + sx * bpp;
            int len = sw * bpp;
            var span = buf.Data.AsSpan(rowStart, len);
            for (int i = 0; i + 2 < span.Length; i += bpp)
                set.Add((span[i] << 16) | (span[i + 1] << 8) | span[i + 2]);
        }
        return set.Count;
    }

    /// <summary>
    /// Structural spec check. The decoded buffer is always RGB8 (ImageSharp
    /// normalises on load); <paramref name="sourceHasAlpha"/>/<paramref name="sourceIndexed"/>
    /// reflect the original file so we can warn about a non-conforming source.
    /// </summary>
    public static ImageSpecResult CheckImageSpecs(
        PixelBuffer buf, bool sourceHasAlpha, bool sourceIndexed)
    {
        return new ImageSpecResult
        {
            IsRgb = buf.BytesPerPixel >= 3,
            Is8Bit = true,
            NoAlpha = !sourceHasAlpha && buf.BytesPerPixel == 3,
            NotIndexed = !sourceIndexed,
            IsSquare = buf.IsSquare,
            Width = buf.Width,
            Height = buf.Height,
        };
    }
}
