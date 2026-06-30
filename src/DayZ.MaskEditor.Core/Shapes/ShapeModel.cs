namespace DayZ.MaskEditor.Core.Shapes;

/// <summary>A point in terrain world coordinates (metres).</summary>
public readonly record struct WorldPoint(double X, double Y);

public enum ShapeKind { Point, Line, Polygon }

/// <summary>
/// One shapefile feature, reduced to a framework-neutral form. A point feature is
/// one part with one vertex; a line/polygon feature has one vertex list per
/// path/ring.
/// </summary>
public sealed class ShapeFeature
{
    public required ShapeKind Kind { get; init; }
    public required IReadOnlyList<IReadOnlyList<WorldPoint>> Parts { get; init; }

    /// <summary>
    /// The feature's .dbf attributes in file order (e.g. Terrain Builder's
    /// <c>__LAYER</c>/<c>__ID</c>). Empty when the shapefile has no attribute table.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; init; } =
        Array.Empty<KeyValuePair<string, string>>();
}

/// <summary>A loaded shapefile: its features plus the world-space bounding box.</summary>
public sealed class ShapeLayer
{
    public required string Name { get; init; }
    public required IReadOnlyList<ShapeFeature> Features { get; init; }
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
}
