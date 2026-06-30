using DayZ.MaskEditor.App.Models;
using DayZ.MaskEditor.Core.Masking;
using DayZ.MaskEditor.Core.Shapes;

namespace DayZ.MaskEditor.App.ViewModels;

/// <summary>
/// What the view-model needs from the editing canvas. Implemented by
/// <c>MaskCanvas</c>; keeps the VM free of rendering details.
/// </summary>
public interface ICanvasHost
{
    /// <summary>(Re)build tile cache + mip pyramid from the document and fit to view.</summary>
    void ReloadDocument();

    /// <summary>Repaint (e.g. after opacity or grid change).</summary>
    void InvalidateView();

    /// <summary>Recomposite the visible mask after in-place edits (keeps zoom/pan).</summary>
    void RefreshMask();

    /// <summary>Drop paint-undo history (after a whole-mask edit like auto-fix).</summary>
    void ClearHistory();

    /// <summary>Centre and zoom so the whole image is visible.</summary>
    void FitToView();

    void SetOverlayOpacity(double opacity);

    /// <summary>Toggle eyedropper mode: left-click samples the mask instead of painting.</summary>
    void SetEyedropper(bool active);

    void SetTileGrid(bool show, int tileSize, int overlap, int tilesInRow);

    /// <summary>Highlight over-limit tiles from the per-tile check (null clears).
    /// <paramref name="maxColors"/> is the per-tile limit, shown on each tile badge.</summary>
    void SetTileHighlights(IReadOnlyList<OverLimitTile>? tiles, int maxColors);

    /// <summary>Highlight pixels matching these stray colours (null clears).</summary>
    void SetStrayHighlights(IReadOnlyCollection<int>? strayRgb);

    /// <summary>Replace the shapefile overlay layers (null/empty clears).</summary>
    void SetShapeLayers(IReadOnlyList<ShapeRenderLayer>? layers);

    /// <summary>Set the world→pixel transform used to place shapes on the mask.</summary>
    void SetWorldTransform(WorldToPixel transform);
}
