using DayZ.MaskEditor.Core.Shapes;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

/// <summary>
/// Round-trips real shapefiles: write a geometry with NetTopologySuite, then load
/// it back through <see cref="ShapefileLoader"/> and verify the neutral model.
/// (A shapefile holds a single geometry type, so each kind is its own file.)
/// </summary>
public class ShapefileLoaderTests
{
    private static readonly GeometryFactory Gf = new();

    private static string WriteOne(Geometry geom)
    {
        var path = Path.Combine(Path.GetTempPath(), $"shp_{Guid.NewGuid():N}.shp");
        var attrs = new AttributesTable { { "id", 1 } };
        Shapefile.WriteAllFeatures(new IFeature[] { new Feature(geom, attrs) }, path);
        return path;
    }

    private static void Cleanup(string shp)
    {
        foreach (var ext in new[] { ".shp", ".shx", ".dbf", ".prj", ".cpg" })
        {
            var f = Path.ChangeExtension(shp, ext);
            if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public void LoadsPoint()
    {
        var shp = WriteOne(Gf.CreatePoint(new Coordinate(10, 20)));
        try
        {
            var layer = ShapefileLoader.Load(shp);
            var f = Assert.Single(layer.Features);
            Assert.Equal(ShapeKind.Point, f.Kind);
            var pt = f.Parts[0][0];
            Assert.Equal(10, pt.X, 6);
            Assert.Equal(20, pt.Y, 6);
            Assert.Equal(10, layer.MinX, 6);
            Assert.Equal(20, layer.MaxY, 6);
        }
        finally { Cleanup(shp); }
    }

    [Fact]
    public void LoadsLineString()
    {
        var line = Gf.CreateLineString(new[]
        {
            new Coordinate(0, 0), new Coordinate(100, 50), new Coordinate(200, 0),
        });
        var shp = WriteOne(line);
        try
        {
            var layer = ShapefileLoader.Load(shp);
            var f = Assert.Single(layer.Features);
            Assert.Equal(ShapeKind.Line, f.Kind);
            Assert.Single(f.Parts);
            Assert.Equal(3, f.Parts[0].Count);
            Assert.Equal(100, f.Parts[0][1].X, 6);
            Assert.Equal(50, f.Parts[0][1].Y, 6);
        }
        finally { Cleanup(shp); }
    }

    [Fact]
    public void CarriesDbfAttributes()
    {
        // Mirror a Terrain Builder export: __LAYER (text) + __ID (numeric).
        var attrs = new AttributesTable
        {
            { "__LAYER", "ROADS_DIRT" },
            { "__ID", 142.0 },
        };
        var path = Path.Combine(Path.GetTempPath(), $"shp_{Guid.NewGuid():N}.shp");
        Shapefile.WriteAllFeatures(
            new IFeature[] { new Feature(Gf.CreatePoint(new Coordinate(5, 6)), attrs) }, path);
        try
        {
            var layer = ShapefileLoader.Load(path);
            var f = Assert.Single(layer.Features);
            var map = f.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value);
            Assert.Equal("ROADS_DIRT", map["__LAYER"]);
            Assert.Equal("142", map["__ID"]); // integral double formatted without decimals
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void LoadsPolygon()
    {
        var ring = Gf.CreateLinearRing(new[]
        {
            new Coordinate(0, 0), new Coordinate(100, 0),
            new Coordinate(100, 100), new Coordinate(0, 100), new Coordinate(0, 0),
        });
        var shp = WriteOne(Gf.CreatePolygon(ring));
        try
        {
            var layer = ShapefileLoader.Load(shp);
            var f = Assert.Single(layer.Features);
            Assert.Equal(ShapeKind.Polygon, f.Kind);
            Assert.Single(f.Parts);              // exterior ring only
            Assert.Equal(5, f.Parts[0].Count);   // closed ring
            Assert.Equal(0, layer.MinX, 6);
            Assert.Equal(100, layer.MaxX, 6);
        }
        finally { Cleanup(shp); }
    }
}
