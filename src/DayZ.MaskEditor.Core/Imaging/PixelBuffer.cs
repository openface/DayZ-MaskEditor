namespace DayZ.MaskEditor.Core.Imaging;

/// <summary>
/// A flat, full-resolution 8-bit pixel buffer. The editable mask lives here as
/// tightly packed RGB (bpp 3); a satmap may be RGB too. Pixels are literal sRGB
/// bytes — never gamma/linear transformed — to keep legend colours byte-exact
/// (the lesson baked into the plugin's gegl_io.py).
/// </summary>
public sealed class PixelBuffer
{
    public int Width { get; }
    public int Height { get; }
    public int BytesPerPixel { get; }
    public int Stride { get; }
    public byte[] Data { get; }

    public PixelBuffer(int width, int height, int bytesPerPixel = 3)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (bytesPerPixel is not (3 or 4))
            throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "Only RGB(3)/RGBA(4) supported.");
        Width = width;
        Height = height;
        BytesPerPixel = bytesPerPixel;
        Stride = width * bytesPerPixel;
        long n = (long)Stride * height;
        if (n > int.MaxValue)
            throw new ArgumentException("Image too large for a single contiguous buffer.");
        Data = new byte[(int)n];
    }

    public PixelBuffer(int width, int height, int bytesPerPixel, byte[] data)
    {
        Width = width;
        Height = height;
        BytesPerPixel = bytesPerPixel;
        Stride = width * bytesPerPixel;
        Data = data;
    }

    public bool IsSquare => Width == Height;

    public int PixelOffset(int x, int y) => y * Stride + x * BytesPerPixel;

    /// <summary>Read one pixel as packed 24-bit RGB (0xRRGGBB), alpha ignored.</summary>
    public int GetRgb(int x, int y)
    {
        int o = PixelOffset(x, y);
        return (Data[o] << 16) | (Data[o + 1] << 8) | Data[o + 2];
    }

    /// <summary>Write one pixel's RGB (alpha untouched if present).</summary>
    public void SetRgb(int x, int y, int rgb)
    {
        int o = PixelOffset(x, y);
        Data[o] = (byte)(rgb >> 16);
        Data[o + 1] = (byte)(rgb >> 8);
        Data[o + 2] = (byte)rgb;
    }
}
