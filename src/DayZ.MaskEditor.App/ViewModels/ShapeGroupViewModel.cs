using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayZ.MaskEditor.Core.Shapes;

namespace DayZ.MaskEditor.App.ViewModels;

/// <summary>
/// One <c>__LAYER</c> group within a shapefile (e.g. all "deep forest" polygons):
/// the feature subset, its world bounds, a distinct colour, and a visibility toggle.
/// </summary>
public sealed partial class ShapeGroupViewModel : ObservableObject
{
    public string Name { get; }
    public IReadOnlyList<ShapeFeature> Features { get; }
    public int Count => Features.Count;
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    private int _colorIndex;

    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isVisible = true;

    public IBrush Swatch => new SolidColorBrush(Color);

    /// <summary>Raised when colour or visibility changes (bubbles to the layer).</summary>
    public event Action? Changed;

    public ShapeGroupViewModel(string name, IReadOnlyList<ShapeFeature> features, int colorIndex)
    {
        Name = name;
        Features = features;
        _colorIndex = colorIndex;
        _color = ShapeGrouping.DistinctColor(colorIndex);
        (MinX, MinY, MaxX, MaxY) = ShapeGrouping.Bounds(features);
    }

    partial void OnColorChanged(Color value) { OnPropertyChanged(nameof(Swatch)); Changed?.Invoke(); }
    partial void OnIsVisibleChanged(bool value) => Changed?.Invoke();

    [RelayCommand]
    private void CycleColor()
    {
        _colorIndex += 5;
        Color = ShapeGrouping.DistinctColor(_colorIndex);
    }
}

/// <summary>Grouping helpers: split a shapefile by <c>__LAYER</c> and pick distinct colours.</summary>
public static class ShapeGrouping
{
    public const string LayerField = "__LAYER";

    public static bool HasField(IReadOnlyList<ShapeFeature> features) =>
        features.Any(f => Value(f) != null);

    public static string? Value(ShapeFeature f)
    {
        foreach (var a in f.Attributes)
            if (string.Equals(a.Key, LayerField, StringComparison.OrdinalIgnoreCase))
                return a.Value;
        return null;
    }

    public static List<ShapeGroupViewModel> Build(IReadOnlyList<ShapeFeature> features)
    {
        var byVal = new SortedDictionary<string, List<ShapeFeature>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in features)
        {
            string val = Value(f) is { Length: > 0 } v ? v : "(none)";
            if (!byVal.TryGetValue(val, out var list)) byVal[val] = list = new List<ShapeFeature>();
            list.Add(f);
        }
        var groups = new List<ShapeGroupViewModel>(byVal.Count);
        int i = 0;
        foreach (var (name, list) in byVal)
            groups.Add(new ShapeGroupViewModel(name, list, i++));
        return groups;
    }

    public static (double MinX, double MinY, double MaxX, double MaxY) Bounds(
        IReadOnlyList<ShapeFeature> features)
    {
        double minx = double.MaxValue, miny = double.MaxValue, maxx = double.MinValue, maxy = double.MinValue;
        foreach (var f in features)
            foreach (var part in f.Parts)
                foreach (var p in part)
                {
                    if (p.X < minx) minx = p.X;
                    if (p.Y < miny) miny = p.Y;
                    if (p.X > maxx) maxx = p.X;
                    if (p.Y > maxy) maxy = p.Y;
                }
        return minx == double.MaxValue ? (0, 0, 0, 0) : (minx, miny, maxx, maxy);
    }

    /// <summary>Visually distinct colour for index i (golden-angle hue spacing).</summary>
    public static Color DistinctColor(int i) => FromHsv((i * 137.508) % 360.0, 0.6, 0.95);

    private static Color FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r, g, b;
        switch (((int)Math.Floor(hp)) % 6)
        {
            case 0: (r, g, b) = (c, x, 0); break;
            case 1: (r, g, b) = (x, c, 0); break;
            case 2: (r, g, b) = (0, c, x); break;
            case 3: (r, g, b) = (0, x, c); break;
            case 4: (r, g, b) = (x, 0, c); break;
            default: (r, g, b) = (c, 0, x); break;
        }
        double m = v - c;
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
