using DayZ.MaskEditor.Core.Masking;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

/// <summary>Port of test_maskcore.py — tile geometry + fragment partition half.</summary>
public class TileGeometryTests
{
    private static (int, int) Tr(int i) => TileGeometry.TileRegion(i, 22, 512, 32, 10240);

    [Fact]
    public void TileRegionMatchesMaskColorChecker()
    {
        Assert.Equal((0, 496), Tr(0));
        Assert.Equal((464, 512), Tr(1));
        Assert.Equal((4784, 512), Tr(10));
        Assert.Equal((10064, 175), Tr(21));
        Assert.Equal(32, Tr(0).Item1 + Tr(0).Item2 - Tr(1).Item1); // overlap tile0/tile1
    }

    /// <summary>True iff the enabled fragments tile the region exactly once each.</summary>
    private static bool Partitions(int ix, int iy, int sizeX, int sizeY,
        int overlap = 10, int nt = 22, int sx = 0, int sy = 0)
    {
        var frags = TileGeometry.BuildTileFragments(ix, iy, sx, sy, sizeX, sizeY, overlap, nt);
        var cover = new Dictionary<(int, int), int>();
        foreach (var f in frags)
            for (int yy = f.Y; yy < f.Y + f.H; yy++)
                for (int xx = f.X; xx < f.X + f.W; xx++)
                    cover[(xx, yy)] = cover.GetValueOrDefault((xx, yy)) + 1;

        int inside = 0;
        for (int yy = sy; yy < sy + sizeY; yy++)
            for (int xx = sx; xx < sx + sizeX; xx++)
            {
                if (cover.GetValueOrDefault((xx, yy)) != 1) return false;
                inside++;
            }
        return cover.Count == inside;
    }

    [Theory]
    [InlineData(5, 5, 100, 100)]    // interior
    [InlineData(0, 0, 100, 100)]    // top-left
    [InlineData(21, 0, 100, 100)]   // top-right
    [InlineData(5, 0, 100, 100)]    // top-middle
    [InlineData(0, 5, 100, 100)]    // mid-left
    [InlineData(21, 5, 100, 100)]   // mid-right
    [InlineData(0, 21, 100, 100)]   // bottom-left
    [InlineData(21, 21, 100, 100)]  // bottom-right
    [InlineData(5, 21, 100, 100)]   // bottom-middle
    [InlineData(21, 21, 70, 70)]    // clamped last tile
    public void FragmentsPartitionTileExactly(int ix, int iy, int sizeX, int sizeY)
    {
        Assert.True(Partitions(ix, iy, sizeX, sizeY));
    }
}
