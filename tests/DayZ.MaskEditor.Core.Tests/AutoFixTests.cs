using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;
using DayZ.MaskEditor.Core.Masking;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

public class AutoFixTests
{
    private static readonly Rgb Grass = new(0, 120, 0);
    private static readonly Rgb Dirt = new(120, 80, 40);
    private static readonly Rgb Red = new(255, 0, 0);
    private static readonly Rgb Grn = new(0, 255, 0);
    private static readonly Rgb Blu = new(0, 0, 255);

    [Fact]
    public void SnapToLegendReplacesStraysWithNearest()
    {
        var surfaces = new List<Surface>
        {
            new() { Name = "grass", Rgb = Grass },
            new() { Name = "dirt", Rgb = Dirt },
        };
        var buf = new PixelBuffer(4, 4, 3);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                buf.SetRgb(x, y, Grass.Packed);
        buf.SetRgb(1, 1, new Rgb(0, 118, 2).Packed);    // near grass
        buf.SetRgb(2, 2, new Rgb(118, 82, 38).Packed);  // near dirt

        long changed = AutoFix.SnapToLegend(buf, surfaces);

        Assert.Equal(2, changed);
        Assert.Equal(Grass.Packed, buf.GetRgb(1, 1));
        Assert.Equal(Dirt.Packed, buf.GetRgb(2, 2));
        // Every pixel is now an exact legend colour.
        Assert.Equal(0, Validation.CheckLegend(buf, surfaces).Invalid);
    }

    [Fact]
    public void ConsolidateTilesBringsTileWithinLimit()
    {
        // One 4x4 tile, no overlap: RED×8, GRN×5, BLU×3 -> 3 colours, limit 2.
        var buf = new PixelBuffer(4, 4, 3);
        var seq = new List<Rgb>();
        seq.AddRange(Enumerable.Repeat(Red, 8));
        seq.AddRange(Enumerable.Repeat(Grn, 5));
        seq.AddRange(Enumerable.Repeat(Blu, 3));
        for (int i = 0; i < 16; i++) buf.SetRgb(i % 4, i / 4, seq[i].Packed);

        int fixedTiles = AutoFix.ConsolidateTiles(buf, tileSize: 4, overlap: 0, tilesInRow: 1, maxColors: 2);

        Assert.Equal(1, fixedTiles);
        var res = Validation.CheckTiles(buf, 4, 0, 1, 2);
        Assert.True(res.Passed);
        Assert.True(res.MaxColorsFound <= 2);
    }

    [Fact]
    public void ConsolidateTilesLeavesWithinLimitTilesUntouched()
    {
        var buf = new PixelBuffer(4, 4, 3);
        for (int i = 0; i < 16; i++) buf.SetRgb(i % 4, i / 4, (i % 2 == 0 ? Red : Grn).Packed);

        int fixedTiles = AutoFix.ConsolidateTiles(buf, 4, 0, 1, maxColors: 4);

        Assert.Equal(0, fixedTiles);
    }
}
