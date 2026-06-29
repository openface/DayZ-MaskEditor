using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;
using DayZ.MaskEditor.Core.Masking;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

public class ImageIoAndValidationTests
{
    private static readonly Rgb Grass = new(0, 120, 0);
    private static readonly Rgb Dirt = new(120, 80, 40);
    private static readonly Rgb Rock = new(90, 90, 90);

    /// <summary>Exact-8-bit guarantee: PNG save→load must be byte-identical.</summary>
    [Fact]
    public void PngRoundTripIsByteExact()
    {
        var buf = new PixelBuffer(64, 64, 3);
        var rng = new Random(1234);
        rng.NextBytes(buf.Data); // arbitrary exact bytes

        var path = Path.Combine(Path.GetTempPath(), $"mask_rt_{Guid.NewGuid():N}.png");
        try
        {
            ImageIO.SavePng(buf, path);
            var loaded = ImageIO.Load(path);
            Assert.Equal(buf.Width, loaded.Width);
            Assert.Equal(buf.Height, loaded.Height);
            Assert.Equal(buf.Data, loaded.Data);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CheckLegendFindsStrays()
    {
        var surfaces = new List<Surface>
        {
            new() { Name = "grass", Rgb = Grass },
            new() { Name = "dirt", Rgb = Dirt },
        };
        var buf = new PixelBuffer(4, 4, 3);
        // Fill grass, then poke one stray (Rock) and one dirt.
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                buf.SetRgb(x, y, Grass.Packed);
        buf.SetRgb(0, 0, Dirt.Packed);
        buf.SetRgb(1, 1, Rock.Packed); // stray

        var sum = Validation.CheckLegend(buf, surfaces);
        Assert.Equal(16, sum.Total);
        Assert.Equal(1, sum.Invalid);
        Assert.Equal(Rock, sum.Strays[0].Rgb);
        var cov = sum.Coverage.ToDictionary(c => c.Name, c => c.Count);
        Assert.Equal(14, cov["grass"]);
        Assert.Equal(1, cov["dirt"]);
    }

    [Fact]
    public void CheckTilesFlagsOverLimitTile()
    {
        // 4x4 image, 1 tile of size 4 (no overlap): 3 colours, limit 2 -> over.
        var buf = new PixelBuffer(4, 4, 3);
        for (int i = 0; i < 16; i++)
        {
            int x = i % 4, y = i / 4;
            var c = (i % 3) switch { 0 => Grass, 1 => Dirt, _ => Rock };
            buf.SetRgb(x, y, c.Packed);
        }
        var res = Validation.CheckTiles(buf, tileSize: 4, overlap: 0, tilesInRow: 1, maxColors: 2);
        Assert.Equal(1, res.TotalTiles);
        Assert.Equal(3, res.MaxColorsFound);
        Assert.Single(res.OverLimit);
        Assert.False(res.Passed);
    }
}
