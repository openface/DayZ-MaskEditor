using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayZ.MaskEditor.App.Models;
using DayZ.MaskEditor.App.Services;
using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;
using DayZ.MaskEditor.Core.Masking;

namespace DayZ.MaskEditor.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public EditorDocument Document { get; } = new();

    /// <summary>Set by the window once the canvas exists.</summary>
    public ICanvasHost? Canvas { get; set; }

    /// <summary>Set by the window (needs a TopLevel for file pickers).</summary>
    public IDialogService? Dialogs { get; set; }

    public ObservableCollection<SurfaceItemViewModel> Surfaces { get; } = new();

    [ObservableProperty] private string? _cfgPath;
    [ObservableProperty] private string? _satmapPath;
    [ObservableProperty] private string? _maskPath;

    [ObservableProperty] private SurfaceItemViewModel? _armedSurface;
    [ObservableProperty] private string _armedText = "Pick a surface to paint";

    [ObservableProperty] private int _brushSize;
    [ObservableProperty] private double _overlayOpacity;
    [ObservableProperty] private bool _showTileGrid;

    [ObservableProperty] private int _tileSize;
    [ObservableProperty] private int _tileOverlap;
    [ObservableProperty] private int _tilesInRow;
    [ObservableProperty] private int _tileMaxColors;

    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _status = "Load a layers.cfg to begin.";
    [ObservableProperty] private bool _isBusy;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        _cfgPath = settings.LastCfg;
        _satmapPath = settings.LastSatmap;
        _maskPath = settings.LastMask;
        _brushSize = settings.BrushSize;
        _overlayOpacity = settings.OverlayOpacity;
        _showTileGrid = settings.ShowTileGrid;
        _tileSize = settings.TileSize;
        _tileOverlap = settings.TileOverlap;
        _tilesInRow = settings.TilesInRow;
        _tileMaxColors = settings.TileMaxColors;
        Document.BrushSize = settings.BrushSize;
    }

    // --- settings write-through ------------------------------------------- //
    partial void OnBrushSizeChanged(int value)
    {
        _settings.BrushSize = value;
        Document.BrushSize = value;
    }

    partial void OnOverlayOpacityChanged(double value)
    {
        _settings.OverlayOpacity = value;
        Canvas?.SetOverlayOpacity(value);
    }

    partial void OnShowTileGridChanged(bool value)
    {
        _settings.ShowTileGrid = value;
        Canvas?.SetTileGrid(value, TileSize, TileOverlap, TilesInRow);
    }

    partial void OnTileSizeChanged(int value) => _settings.TileSize = value;
    partial void OnTileOverlapChanged(int value) => _settings.TileOverlap = value;
    partial void OnTilesInRowChanged(int value) => _settings.TilesInRow = value;
    partial void OnTileMaxColorsChanged(int value) => _settings.TileMaxColors = value;

    // --- file pickers ----------------------------------------------------- //
    [RelayCommand]
    private async Task BrowseCfgAsync()
    {
        if (Dialogs is null) return;
        var p = await Dialogs.OpenFileAsync("Select layers.cfg",
            ("Config files", new[] { "*.cfg" }), ("All files", new[] { "*" }));
        if (p != null) { CfgPath = p; _settings.LastCfg = p; }
    }

    [RelayCommand]
    private async Task BrowseSatmapAsync()
    {
        if (Dialogs is null) return;
        var p = await Dialogs.OpenFileAsync("Select satmap image",
            ("Images", new[] { "*.png", "*.tga", "*.tif", "*.tiff", "*.bmp" }),
            ("All files", new[] { "*" }));
        if (p != null) { SatmapPath = p; _settings.LastSatmap = p; }
    }

    [RelayCommand]
    private async Task BrowseMaskAsync()
    {
        if (Dialogs is null) return;
        var p = await Dialogs.OpenFileAsync("Select surface mask image",
            ("Images", new[] { "*.png", "*.tga", "*.tif", "*.tiff", "*.bmp" }),
            ("All files", new[] { "*" }));
        if (p != null) { MaskPath = p; _settings.LastMask = p; }
    }

    // --- load ------------------------------------------------------------- //
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(CfgPath) || !File.Exists(CfgPath))
        {
            Log("No layers.cfg selected.");
            return;
        }

        IsBusy = true;
        Status = "Loading…";
        try
        {
            // Parse cfg
            var cfg = await Task.Run(() => LayersCfgParser.Parse(File.ReadAllText(CfgPath)));
            Document.Config = cfg;
            PopulateSurfaces(cfg);
            Log($"Loaded {Path.GetFileName(CfgPath)}: {cfg.Surfaces.Count} surfaces.");
            foreach (var w in cfg.Warnings) Log("  ! " + w);

            // Load images (heavy — off the UI thread)
            if (File.Exists(SatmapPath) && File.Exists(MaskPath))
            {
                var (sat, mask) = await Task.Run(() =>
                    (ImageIO.Load(SatmapPath!), ImageIO.Load(MaskPath!)));
                Document.Satmap = sat;
                Document.Mask = mask;
                Document.MaskPath = MaskPath;
                Document.MaskDirty = false;
                Log($"Satmap {sat.Width}×{sat.Height}, mask {mask.Width}×{mask.Height}.");
                if (!Document.DimensionsMatch)
                    Log("  ! Satmap and mask dimensions differ — overlay may misalign.");

                Canvas?.SetOverlayOpacity(OverlayOpacity);
                Canvas?.ReloadDocument();
                UpdateCoverage();
            }
            else if (!string.IsNullOrWhiteSpace(SatmapPath) || !string.IsNullOrWhiteSpace(MaskPath))
            {
                Log("  ! Select both a satmap and a mask to enable the editor.");
            }

            Status = "Ready.";
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            Status = "Load failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateSurfaces(TerrainConfig cfg)
    {
        Surfaces.Clear();
        foreach (var s in cfg.Surfaces)
            Surfaces.Add(new SurfaceItemViewModel(s));
        ArmedSurface = null;
        Document.ArmedRgb = -1;
        ArmedText = "Pick a surface to paint";
    }

    // --- arming ----------------------------------------------------------- //
    [RelayCommand]
    private void ArmSurface(SurfaceItemViewModel? item)
    {
        if (item is null) return;
        if (ArmedSurface != null) ArmedSurface.IsArmed = false;
        ArmedSurface = item;
        item.IsArmed = true;
        Document.ArmedRgb = item.PackedRgb;
        ArmedText = $"Painting: {item.Name}  ({item.RgbText})";
    }

    // --- save ------------------------------------------------------------- //
    [RelayCommand]
    private async Task SaveMaskAsync()
    {
        if (Document.Mask is null) { Log("Nothing to save."); return; }
        var target = Document.MaskPath ?? MaskPath;
        if (string.IsNullOrWhiteSpace(target) && Dialogs != null)
            target = await Dialogs.SaveFileAsync("Save mask as", "mask.png", "png");
        if (string.IsNullOrWhiteSpace(target)) return;

        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            await Task.Run(() => ImageIO.SavePng(mask, target!));
            Document.MaskPath = target;
            Document.MaskDirty = false;
            Log($"Saved mask → {target}");
        }
        catch (Exception ex)
        {
            Log("ERROR saving: " + ex.Message);
        }
        finally { IsBusy = false; }
    }

    // --- validation (report-only) ---------------------------------------- //
    [RelayCommand]
    private async Task CheckLegendAsync()
    {
        if (Document.Mask is null || Document.Config is null) { Log("Load a mask first."); return; }
        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            var surfaces = Document.Config.Surfaces;
            var sum = await Task.Run(() => Validation.CheckLegend(mask, surfaces));
            ReportLegend(sum);
            UpdateCoverage(sum);
            // Highlight the worst stray colours (cap mirrors the plugin's 64).
            var strays = sum.Strays.Take(64).Select(s => s.Rgb.Packed).ToHashSet();
            Canvas?.SetStrayHighlights(strays.Count > 0 ? strays : null);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckTilesAsync()
    {
        if (Document.Mask is null) { Log("Load a mask first."); return; }
        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            int ts = TileSize, ov = TileOverlap, nt = TilesInRow, mx = TileMaxColors;
            var res = await Task.Run(() => Validation.CheckTiles(mask, ts, ov, nt, mx));
            ReportTiles(res);
            Canvas?.SetTileHighlights(res.OverLimit.Count > 0 ? res.OverLimit : null);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CheckSpecs()
    {
        if (Document.Mask is null) { Log("Load a mask first."); return; }
        var r = Validation.CheckImageSpecs(Document.Mask, sourceHasAlpha: false, sourceIndexed: false);
        Log("— Image specs —");
        Log($"  {(r.IsRgb ? "OK" : "FAIL")}  RGB colour");
        Log($"  {(r.Is8Bit ? "OK" : "FAIL")}  8-bit/channel");
        Log($"  {(r.NoAlpha ? "OK" : "FAIL")}  no alpha channel");
        Log($"  {(r.NotIndexed ? "OK" : "FAIL")}  not indexed");
        Log($"  {(r.IsSquare ? "OK" : "FAIL")}  square ({r.Width}×{r.Height})");
        Log(r.Passed ? "  → all specs pass." : "  → fix the FAIL items above.");
    }

    // --- coverage --------------------------------------------------------- //
    private void UpdateCoverage(LegendSummary? sum = null)
    {
        if (Document.Mask is null || Document.Config is null) return;
        sum ??= Validation.CheckLegend(Document.Mask, Document.Config.Surfaces);
        var byName = sum.Coverage.ToDictionary(c => c.Name, c => c.Percent);
        foreach (var item in Surfaces)
            item.CoveragePercent = byName.GetValueOrDefault(item.Name);
    }

    // --- reporting -------------------------------------------------------- //
    private void ReportLegend(LegendSummary s)
    {
        Log("— Check legend colours —");
        Log($"  total pixels: {s.Total:N0}");
        Log($"  invalid (stray): {s.Invalid:N0}  ({s.InvalidPercent:0.000}%)");
        Log($"  distinct stray colours: {s.DistinctStrays}");
        if (s.DistinctStrays > 0)
        {
            Log("  top strays:");
            foreach (var (rgb, count) in s.Strays.Take(10))
                Log($"    {rgb}  ×{count:N0}");
        }
        else Log("  → every pixel is an exact legend colour.");
    }

    private void ReportTiles(TileCheckResult r)
    {
        Log("— Check colours per tile —");
        Log($"  grid: {r.TilesInRow}×{r.TilesInRow} ({r.TotalTiles} tiles), limit {r.MaxColors}");
        Log($"  max distinct colours in any tile: {r.MaxColorsFound}");
        if (r.OverLimit.Count == 0)
        {
            Log("  → all tiles within the colour budget.");
            return;
        }
        Log($"  over-limit tiles: {r.OverLimit.Count}");
        foreach (var t in r.OverLimit.Take(10))
            Log($"    tile ({t.Ix},{t.Iy}) @ {t.X},{t.Y} → {t.ColorCount} colours");
        Log("  grid (# = over limit):");
        foreach (var line in r.AsciiGrid.TrimEnd('\n').Split('\n'))
            Log("    " + line);
    }

    // --- log -------------------------------------------------------------- //
    private readonly StringBuilder _log = new();
    private void Log(string line)
    {
        _log.AppendLine(line);
        LogText = _log.ToString();
    }

    /// <summary>Thread-safe log entry point for background callers (e.g. updates).</summary>
    public void LogLine(string line)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Log(line);
        else Avalonia.Threading.Dispatcher.UIThread.Post(() => Log(line));
    }
}
