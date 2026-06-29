using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DayZ.MaskEditor.Core.Imaging;

/// <summary>
/// Image decode/encode via ImageSharp. Pixels are kept as exact 8-bit RGB —
/// loaded straight into a packed <see cref="PixelBuffer"/> and written back with
/// no colour-management — so the mask stays byte-exact (the gegl_io.py lesson).
/// </summary>
public static class ImageIO
{
    /// <summary>Decode a PNG/BMP/TGA/TIFF file into a packed RGB24 PixelBuffer.</summary>
    public static PixelBuffer Load(string path)
    {
        using var image = Image.Load<Rgb24>(path);
        var buffer = new PixelBuffer(image.Width, image.Height, 3);
        var data = buffer.Data;
        int stride = buffer.Stride;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                Span<byte> rowBytes = MemoryMarshal.AsBytes(row);
                rowBytes.CopyTo(data.AsSpan(y * stride, stride));
            }
        });
        return buffer;
    }

    /// <summary>Read just the pixel dimensions without decoding the whole image.</summary>
    public static (int Width, int Height) ReadSize(string path)
    {
        var info = Image.Identify(path);
        return (info.Width, info.Height);
    }

    /// <summary>Encode a packed RGB24 PixelBuffer to a lossless PNG.</summary>
    public static void SavePng(PixelBuffer buffer, string path)
    {
        if (buffer.BytesPerPixel != 3)
            throw new InvalidOperationException("Mask must be RGB (3 bpp) to save.");

        using var image = new Image<Rgb24>(buffer.Width, buffer.Height);
        var data = buffer.Data;
        int stride = buffer.Stride;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                Span<byte> rowBytes = MemoryMarshal.AsBytes(row);
                data.AsSpan(y * stride, stride).CopyTo(rowBytes);
            }
        });

        var encoder = new PngEncoder
        {
            ColorType = PngColorType.Rgb,
            BitDepth = PngBitDepth.Bit8,
            CompressionLevel = PngCompressionLevel.DefaultCompression,
        };
        image.Save(path, encoder);
    }
}
