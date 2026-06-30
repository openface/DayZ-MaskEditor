using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayZ.MaskEditor.Core.Shapes;

namespace DayZ.MaskEditor.App.ViewModels;

/// <summary>One loaded shapefile as a stylable, toggleable overlay layer.</summary>
public sealed partial class ShapeLayerViewModel : ObservableObject
{
    /// <summary>Distinct default colours cycled as layers are added.</summary>
    public static readonly Color[] Palette =
    {
        Color.FromRgb(255, 80, 80), Color.FromRgb(80, 200, 255),
        Color.FromRgb(255, 220, 60), Color.FromRgb(120, 255, 120),
        Color.FromRgb(255, 140, 40), Color.FromRgb(220, 120, 255),
    };

    public ShapeLayer Layer { get; }
    public string Path { get; }

    public string Name => Layer.Name;
    public int FeatureCount => Layer.Features.Count;

    /// <summary>True when features carry a <c>__LAYER</c> attribute to group by.</summary>
    public bool CanGroup { get; }

    /// <summary><c>__LAYER</c> groups (built once); only used when <see cref="GroupByLayer"/>.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ShapeGroupViewModel> Groups { get; } = new();

    [ObservableProperty] private bool _groupByLayer;

    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private double _opacity = 1.0;

    /// <summary>Non-null when this layer falls outside the configured terrain extent.</summary>
    [ObservableProperty] private string? _extentProblem;

    public bool HasProblem => ExtentProblem != null;

    public IBrush Swatch => new SolidColorBrush(Color);

    /// <summary>Raised when any render-affecting property changes.</summary>
    public event Action? Changed;

    /// <summary>Raised when the user removes this layer.</summary>
    public event Action<ShapeLayerViewModel>? RemoveRequested;

    public ShapeLayerViewModel(ShapeLayer layer, string path, Color color)
    {
        Layer = layer;
        Path = path;
        _color = color;

        CanGroup = ShapeGrouping.HasField(layer.Features);
        if (CanGroup)
            foreach (var g in ShapeGrouping.Build(layer.Features))
            {
                g.Changed += () => Changed?.Invoke();
                Groups.Add(g);
            }
    }

    partial void OnGroupByLayerChanged(bool value) => Changed?.Invoke();

    partial void OnColorChanged(Color value)
    {
        OnPropertyChanged(nameof(Swatch));
        Changed?.Invoke();
    }

    partial void OnIsVisibleChanged(bool value) => Changed?.Invoke();
    partial void OnOpacityChanged(double value) => Changed?.Invoke();
    partial void OnExtentProblemChanged(string? value) => OnPropertyChanged(nameof(HasProblem));

    [RelayCommand]
    private void Remove() => RemoveRequested?.Invoke(this);

    [RelayCommand]
    private void CycleColor()
    {
        int i = Array.IndexOf(Palette, Color);
        Color = Palette[(i + 1 + Palette.Length) % Palette.Length];
    }
}
