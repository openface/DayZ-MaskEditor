using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;

namespace DayZ.MaskEditor.App.Models;

/// <summary>
/// The currently loaded terrain: the editable mask buffer, the display-only
/// satmap, the parsed legend, and the current paint state. Shared between the
/// view-model and the <c>MaskCanvas</c> (which reads buffers + paint state and
/// reports dirty tiles back).
/// </summary>
public sealed class EditorDocument
{
    public TerrainConfig? Config { get; set; }
    public PixelBuffer? Satmap { get; set; }
    public PixelBuffer? Mask { get; set; }

    public string? MaskPath { get; set; }
    public bool MaskDirty { get; set; }

    /// <summary>Armed surface colour packed as 0xRRGGBB, or -1 if none armed.</summary>
    public int ArmedRgb { get; set; } = -1;
    public int BrushSize { get; set; } = 5;

    public bool HasImages => Satmap != null && Mask != null;

    /// <summary>True when satmap and mask agree on dimensions (required to overlay).</summary>
    public bool DimensionsMatch =>
        Satmap != null && Mask != null &&
        Satmap.Width == Mask.Width && Satmap.Height == Mask.Height;
}
