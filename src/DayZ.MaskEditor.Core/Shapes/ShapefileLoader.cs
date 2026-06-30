using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;

namespace DayZ.MaskEditor.Core.Shapes;

/// <summary>
/// Loads an ESRI shapefile (.shp + .shx + .dbf) into the framework-neutral
/// <see cref="ShapeLayer"/> model. NetTopologySuite stays behind this boundary so
/// the rest of the app never references it. Read-only: shapes are reference
/// geometry, never edited or written back.
/// </summary>
public static class ShapefileLoader
{
    public static ShapeLayer Load(string shpPath)
    {
        var features = new List<ShapeFeature>();
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        void Track(double x, double y)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        foreach (var feature in Shapefile.ReadAllFeatures(shpPath))
        {
            if (feature.Geometry is { } g)
                AddGeometry(g, features, Track, ExtractAttributes(feature.Attributes));
        }

        if (features.Count == 0) // no features — keep a zero box rather than inverted
        {
            minX = minY = maxX = maxY = 0;
        }

        return new ShapeLayer
        {
            Name = Path.GetFileNameWithoutExtension(shpPath),
            Features = features,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
        };
    }

    private static void AddGeometry(Geometry g, List<ShapeFeature> features,
        Action<double, double> track, IReadOnlyList<KeyValuePair<string, string>> attrs)
    {
        switch (g)
        {
            case Point pt:
                features.Add(new ShapeFeature
                {
                    Kind = ShapeKind.Point,
                    Parts = new[] { new[] { W(pt.Coordinate, track) } },
                    Attributes = attrs,
                });
                break;

            case MultiPoint mp:
            {
                var parts = new List<IReadOnlyList<WorldPoint>>(mp.NumGeometries);
                for (int i = 0; i < mp.NumGeometries; i++)
                    if (mp.GetGeometryN(i) is Point p)
                        parts.Add(new[] { W(p.Coordinate, track) });
                features.Add(new ShapeFeature { Kind = ShapeKind.Point, Parts = parts, Attributes = attrs });
                break;
            }

            case LineString ls:
                features.Add(new ShapeFeature
                {
                    Kind = ShapeKind.Line,
                    Parts = new[] { Coords(ls.Coordinates, track) },
                    Attributes = attrs,
                });
                break;

            case MultiLineString mls:
            {
                var parts = new List<IReadOnlyList<WorldPoint>>(mls.NumGeometries);
                for (int i = 0; i < mls.NumGeometries; i++)
                    if (mls.GetGeometryN(i) is LineString line)
                        parts.Add(Coords(line.Coordinates, track));
                features.Add(new ShapeFeature { Kind = ShapeKind.Line, Parts = parts, Attributes = attrs });
                break;
            }

            case Polygon poly:
                features.Add(new ShapeFeature
                {
                    Kind = ShapeKind.Polygon,
                    Parts = PolygonRings(poly, track),
                    Attributes = attrs,
                });
                break;

            case MultiPolygon mpoly:
            {
                var parts = new List<IReadOnlyList<WorldPoint>>();
                for (int i = 0; i < mpoly.NumGeometries; i++)
                    if (mpoly.GetGeometryN(i) is Polygon p)
                        parts.AddRange(PolygonRings(p, track));
                features.Add(new ShapeFeature { Kind = ShapeKind.Polygon, Parts = parts, Attributes = attrs });
                break;
            }

            case GeometryCollection gc:
                for (int i = 0; i < gc.NumGeometries; i++)
                    AddGeometry(gc.GetGeometryN(i), features, track, attrs);
                break;
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ExtractAttributes(
        NetTopologySuite.Features.IAttributesTable? table)
    {
        if (table is null) return Array.Empty<KeyValuePair<string, string>>();
        var names = table.GetNames();
        var list = new List<KeyValuePair<string, string>>(names.Length);
        foreach (var name in names)
        {
            object? value = table[name];
            list.Add(new KeyValuePair<string, string>(name, FormatValue(value)));
        }
        return list;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        double d => d == Math.Floor(d) && !double.IsInfinity(d)
            ? ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static List<IReadOnlyList<WorldPoint>> PolygonRings(Polygon poly, Action<double, double> track)
    {
        var rings = new List<IReadOnlyList<WorldPoint>>(1 + poly.NumInteriorRings)
        {
            Coords(poly.ExteriorRing.Coordinates, track),
        };
        for (int i = 0; i < poly.NumInteriorRings; i++)
            rings.Add(Coords(poly.GetInteriorRingN(i).Coordinates, track));
        return rings;
    }

    private static WorldPoint[] Coords(Coordinate[] cs, Action<double, double> track)
    {
        var pts = new WorldPoint[cs.Length];
        for (int i = 0; i < cs.Length; i++)
            pts[i] = W(cs[i], track);
        return pts;
    }

    private static WorldPoint W(Coordinate c, Action<double, double> track)
    {
        track(c.X, c.Y);
        return new WorldPoint(c.X, c.Y);
    }
}
