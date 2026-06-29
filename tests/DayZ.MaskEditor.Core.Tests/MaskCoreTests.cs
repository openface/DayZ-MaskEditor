using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Masking;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

/// <summary>Port of test_maskcore.py — counting/tally/summarize half.</summary>
public class MaskCoreTests
{
    private static readonly Rgb RED = new(255, 0, 0);
    private static readonly Rgb GRN = new(0, 255, 0);
    private static readonly Rgb BLU = new(0, 0, 255);
    private static readonly Rgb YEL = new(255, 255, 0);
    private static readonly Rgb WHT = new(255, 255, 255);

    /// <summary>Pack a list of colours into a 3-bpp byte buffer.</summary>
    private static byte[] Px(params Rgb[] colors)
    {
        var b = new byte[colors.Length * 3];
        for (int i = 0; i < colors.Length; i++)
        {
            b[i * 3] = colors[i].R;
            b[i * 3 + 1] = colors[i].G;
            b[i * 3 + 2] = colors[i].B;
        }
        return b;
    }

    [Fact]
    public void DistinctColourCounting()
    {
        Assert.Equal(1, MaskCore.CountCellColors(Px(RED, RED, RED, RED), 3));
        Assert.Equal(2, MaskCore.CountCellColors(Px(RED, GRN, RED, GRN), 3));
        Assert.Equal(4, MaskCore.CountCellColors(Px(RED, GRN, BLU, YEL), 3));
        Assert.Equal(5, MaskCore.CountCellColors(Px(RED, GRN, BLU, YEL, WHT, RED), 3));
        Assert.Equal(0, MaskCore.CountCellColors(Array.Empty<byte>(), 3));
    }

    [Fact]
    public void DistinctColourCountingRgbaIgnoresAlpha()
    {
        Assert.Equal(1, MaskCore.CountCellColors(
            new byte[] { 0xff, 0x00, 0x00, 0x10, 0xff, 0x00, 0x00, 0x80 }, 4));
        Assert.Equal(2, MaskCore.CountCellColors(
            new byte[] { 0xff, 0x00, 0x00, 0xff, 0x00, 0xff, 0x00, 0xff }, 4));
    }

    [Fact]
    public void TallyColours()
    {
        var c = new Dictionary<int, long>();
        MaskCore.TallyColors(Px(RED, GRN, RED, BLU, RED), 3, c);
        Assert.Equal(3, c[RED.Packed]);
        Assert.Equal(1, c[GRN.Packed]);
        Assert.Equal(3, c.Count);
    }

    [Fact]
    public void TallyBpp4IgnoresAlpha()
    {
        var c = new Dictionary<int, long>();
        MaskCore.TallyColors(
            new byte[] { 0xff, 0x00, 0x00, 0x10, 0xff, 0x00, 0x00, 0x80, 0x00, 0xff, 0x00, 0xff },
            4, c);
        Assert.Equal(2, c[RED.Packed]);
        Assert.Equal(1, c[GRN.Packed]);
    }

    [Fact]
    public void Summarize()
    {
        var surfaces = new List<Surface>
        {
            new() { Name = "red", Rgb = RED },
            new() { Name = "grn", Rgb = GRN },
        };
        var cnt = new Dictionary<int, long>();
        MaskCore.TallyColors(Px(RED, RED, GRN, BLU), 3, cnt); // red=2 grn=1 blu=1 stray
        var res = MaskCore.Summarize(cnt, 4, surfaces);

        Assert.Equal(4, res.Total);
        Assert.Equal(1, res.Invalid);
        Assert.Equal(1, res.DistinctStrays);
        Assert.Equal(BLU, res.Strays[0].Rgb);

        var cov = res.Coverage.ToDictionary(c => c.Name, c => c.Count);
        Assert.Equal(2, cov["red"]);
        Assert.Equal(1, cov["grn"]);
        Assert.Equal(25.0, Math.Round(res.InvalidPercent, 1));
    }

    [Fact]
    public void CleanImageNoStrays()
    {
        var surfaces = new List<Surface>
        {
            new() { Name = "red", Rgb = RED },
            new() { Name = "grn", Rgb = GRN },
        };
        var cnt = new Dictionary<int, long> { [RED.Packed] = 1, [GRN.Packed] = 1 };
        Assert.Equal(0, MaskCore.Summarize(cnt, 2, surfaces).Invalid);
    }
}
