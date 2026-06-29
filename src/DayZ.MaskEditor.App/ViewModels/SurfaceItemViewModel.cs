using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DayZ.MaskEditor.Core.Config;

namespace DayZ.MaskEditor.App.ViewModels;

/// <summary>One palette entry in the surface browser (a single legend colour).</summary>
public sealed partial class SurfaceItemViewModel : ObservableObject
{
    public Surface Surface { get; }

    public string Name => Surface.Name;
    public Rgb Rgb => Surface.Rgb;
    public int PackedRgb => Surface.Rgb.Packed;

    public IBrush Swatch { get; }
    public string RgbText => $"{Rgb.R}, {Rgb.G}, {Rgb.B}";
    public string? Material => Surface.Material;
    public string Tooltip =>
        $"{Name}\nRGB {RgbText}" + (Material != null ? $"\nmaterial: {Material}" : "");

    [ObservableProperty] private bool _isArmed;
    [ObservableProperty] private double _coveragePercent;

    public string CoverageText => CoveragePercent > 0
        ? $"{CoveragePercent:0.0}%"
        : "";

    public SurfaceItemViewModel(Surface surface)
    {
        Surface = surface;
        Swatch = new SolidColorBrush(Color.FromRgb(surface.Rgb.R, surface.Rgb.G, surface.Rgb.B));
    }

    partial void OnCoveragePercentChanged(double value) => OnPropertyChanged(nameof(CoverageText));
}
