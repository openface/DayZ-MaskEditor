using Avalonia.Media;
using DayZ.MaskEditor.Core.Shapes;

namespace DayZ.MaskEditor.App.Models;

/// <summary>
/// A drawable set of shape features with one style. A shapefile becomes either a
/// single render layer (flat colour) or, when grouped by <c>__LAYER</c>, one render
/// layer per group. The world bounding box is precomputed for fast viewport/hover
/// culling. Only visible groups are emitted, so there is no visibility flag here.
/// </summary>
public sealed record ShapeRenderLayer(
    string Name,
    IReadOnlyList<ShapeFeature> Features,
    double MinX, double MinY, double MaxX, double MaxY,
    Color Color, double Opacity);
