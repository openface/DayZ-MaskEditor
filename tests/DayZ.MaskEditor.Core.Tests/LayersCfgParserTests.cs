using DayZ.MaskEditor.Core.Config;
using Xunit;

namespace DayZ.MaskEditor.Core.Tests;

/// <summary>Port of test_parser.py — pins parser behaviour to the GIMP plugin.</summary>
public class LayersCfgParserTests
{
    private static Dictionary<string, Rgb> SurfMap(TerrainConfig c) =>
        c.Surfaces.ToDictionary(s => s.Name, s => s.Rgb);

    [Fact]
    public void SingleColourPerSurface()
    {
        const string cfg = """
        class Layers {
          class grass { texture = "g.paa"; material = "g.rvmat"; };
          class dirt  { texture = "d.paa"; material = "d.rvmat"; };
        };
        class Legend {
          picture = "legend.png";
          class Colors {
            grass[] = {{0,120,0}};
            dirt[]  = {{120, 80, 40}};
          };
        };
        """;
        var res = LayersCfgParser.Parse(cfg);
        var sm = SurfMap(res);
        Assert.Equal(2, res.Surfaces.Count);
        Assert.Equal(new Rgb(0, 120, 0), sm["grass"]);
        Assert.Equal(new Rgb(120, 80, 40), sm["dirt"]);
        Assert.Equal("legend.png", res.Picture);
        Assert.Equal("g.rvmat", res.Surfaces[0].Material);
    }

    [Fact]
    public void MultiColourSurfaceNotDropped()
    {
        const string cfg = """
        class Legend { class Colors {
          rock[] = {{10,10,10},{20,20,20},{30,30,30}};
          sand[] = {{200,180,120}};
        }; };
        """;
        var res = LayersCfgParser.Parse(cfg);
        var sm = SurfMap(res);
        Assert.Equal(4, res.Surfaces.Count);
        Assert.Equal(new Rgb(10, 10, 10), sm["rock"]);
        Assert.Equal(new Rgb(20, 20, 20), sm["rock #2"]);
        Assert.Equal(new Rgb(30, 30, 30), sm["rock #3"]);
        Assert.Equal(new Rgb(200, 180, 120), sm["sand"]);
        Assert.Contains(res.Warnings, w => w.Contains("3 legend colors"));
    }

    [Fact]
    public void NoLegendBlockScansWholeFile()
    {
        const string cfg = "foo[] = {{1,2,3}};\nbar[] = {{4,5,6}};\n";
        var res = LayersCfgParser.Parse(cfg);
        Assert.Equal(2, res.Surfaces.Count);
        Assert.Contains(res.Warnings, w => w.Contains("No 'class Legend'"));
    }

    [Fact]
    public void DuplicateNameKeptOnceDuplicateRgbWarned()
    {
        const string cfg = """
        class Legend { class Colors {
          a[] = {{5,5,5}};
          a[] = {{9,9,9}};
          b[] = {{5,5,5}};
        }; };
        """;
        var res = LayersCfgParser.Parse(cfg);
        var sm = SurfMap(res);
        Assert.Equal(new Rgb(5, 5, 5), sm["a"]);
        Assert.Contains(res.Warnings, w => w.Contains("Duplicate color entry"));
        Assert.Contains(res.Warnings, w => w.Contains("shared by surfaces"));
    }

    [Fact]
    public void TerrainNameStripsSource()
    {
        Assert.Equal("MyTerrain",
            LayersCfgParser.TerrainNameFromPath(@"p:/MyTerrain/source/layers.cfg"));
    }
}
