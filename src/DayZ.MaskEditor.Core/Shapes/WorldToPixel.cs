namespace DayZ.MaskEditor.Core.Shapes;

/// <summary>
/// Maps shapefile world coordinates (metres) onto mask image pixels. Terrain
/// Builder's world origin is bottom-left with Y up, while the image origin is
/// top-left with Y down — so Y is flipped by default.
///
/// Terrain Builder commonly exports X (easting) with a large offset (the Arma
/// "200,000" convention), and sometimes Y too, so a world-space
/// <see cref="OffsetX"/>/<see cref="OffsetY"/> (metres) is subtracted before
/// scaling. A pixel-space nudge then fine-tunes alignment against satmap landmarks.
///
/// Pure and immutable so it can be unit-tested and freely shared.
/// </summary>
public readonly struct WorldToPixel
{
    public int ImageWidth { get; }
    public int ImageHeight { get; }
    public double Metres { get; }
    public bool FlipY { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double NudgeX { get; }
    public double NudgeY { get; }

    public WorldToPixel(int imageWidth, int imageHeight, double metres,
        bool flipY = true, double offsetX = 0, double offsetY = 0,
        double nudgeX = 0, double nudgeY = 0)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Metres = metres <= 0 ? 1 : metres;
        FlipY = flipY;
        OffsetX = offsetX;
        OffsetY = offsetY;
        NudgeX = nudgeX;
        NudgeY = nudgeY;
    }

    public double ScaleX => ImageWidth / Metres;
    public double ScaleY => ImageHeight / Metres;

    /// <summary>World point → image-pixel coordinate (sub-pixel precision).</summary>
    public (double X, double Y) ToPixel(WorldPoint p)
    {
        double wx = p.X - OffsetX;
        double wy = p.Y - OffsetY;
        double px = wx * ScaleX + NudgeX;
        double py = FlipY
            ? ImageHeight - wy * ScaleY + NudgeY
            : wy * ScaleY + NudgeY;
        return (px, py);
    }
}
