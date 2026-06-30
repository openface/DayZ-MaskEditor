using DayZ.MaskEditor.Core.Shapes;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

public class WorldToPixelTests
{
    [Fact]
    public void FlipYMapsWorldOriginToBottomLeft()
    {
        // 1000 px image, 1000 m terrain -> 1 px per metre, Y flipped.
        var t = new WorldToPixel(1000, 1000, 1000, flipY: true);

        var origin = t.ToPixel(new WorldPoint(0, 0));
        Assert.Equal(0, origin.X, 6);
        Assert.Equal(1000, origin.Y, 6);   // world bottom-left -> image bottom

        var topRight = t.ToPixel(new WorldPoint(1000, 1000));
        Assert.Equal(1000, topRight.X, 6);
        Assert.Equal(0, topRight.Y, 6);    // world top-right -> image top
    }

    [Fact]
    public void NoFlipKeepsYDown()
    {
        var t = new WorldToPixel(1000, 1000, 1000, flipY: false);
        var p = t.ToPixel(new WorldPoint(0, 0));
        Assert.Equal(0, p.X, 6);
        Assert.Equal(0, p.Y, 6);
    }

    [Fact]
    public void ScaleAppliesWhenImageAndMetresDiffer()
    {
        // 2048 px image, 1024 m terrain -> 2 px per metre.
        var t = new WorldToPixel(2048, 2048, 1024, flipY: false);
        var p = t.ToPixel(new WorldPoint(512, 256));
        Assert.Equal(1024, p.X, 6);
        Assert.Equal(512, p.Y, 6);
    }

    [Fact]
    public void NudgeOffsetsPixels()
    {
        var t = new WorldToPixel(1000, 1000, 1000, flipY: false, nudgeX: 5, nudgeY: -3);
        var p = t.ToPixel(new WorldPoint(100, 100));
        Assert.Equal(105, p.X, 6);
        Assert.Equal(97, p.Y, 6);
    }

    [Fact]
    public void WorldOffsetStripsArmaEasting()
    {
        // 10240 px / 10240 m -> 1:1, X shifted by the Arma 200,000 offset.
        var t = new WorldToPixel(10240, 10240, 10240, flipY: true, offsetX: 200000);
        var p = t.ToPixel(new WorldPoint(200000, 0));   // terrain SW corner
        Assert.Equal(0, p.X, 6);
        Assert.Equal(10240, p.Y, 6);
        var q = t.ToPixel(new WorldPoint(210240, 10240)); // terrain NE corner
        Assert.Equal(10240, q.X, 6);
        Assert.Equal(0, q.Y, 6);
    }
}
