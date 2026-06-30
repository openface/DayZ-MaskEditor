using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayZ.MaskEditor.App.Models;
using DayZ.MaskEditor.App.Services;
using DayZ.MaskEditor.Core.Config;
using DayZ.MaskEditor.Core.Imaging;
using DayZ.MaskEditor.Core.Masking;
using DayZ.MaskEditor.Core.Shapes;

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

    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _status = "Load a layers.cfg to begin.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSnapFix))]
    [NotifyPropertyChangedFor(nameof(CanTileFix))]
    private bool _isBusy;

    // Fixes are only offered once the matching check has actually found problems.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSnapFix))]
    private bool _hasStrays;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTileFix))]
    private bool _hasOverLimitTiles;

    public bool CanSnapFix => HasStrays && !IsBusy;
    public bool CanTileFix => HasOverLimitTiles && !IsBusy;

    // --- terrain setup (mirrors the TB Mapframe; the single source of truth) ---
    public ObservableCollection<ShapeLayerViewModel> ShapeLayers { get; } = new();

    [ObservableProperty] private int _gridCells;        // grid size (cells)
    [ObservableProperty] private double _cellSize;      // cell size (m)
    [ObservableProperty] private int _satSourcePx;      // sat/mask source image (px)
    [ObservableProperty] private double _shapeOffsetX;  // Easting (m)
    [ObservableProperty] private double _shapeOffsetY;  // Northing (m)
    [ObservableProperty] private double _shapeNudgeX;
    [ObservableProperty] private double _shapeNudgeY;

    // Tile parameters (also from the Mapframe Samplers tab) feed Check-per-tile.
    [ObservableProperty] private int _tileSize;
    [ObservableProperty] private int _tileOverlap;
    [ObservableProperty] private int _tilesInRow;
    [ObservableProperty] private int _tileMaxColors;

    /// <summary>Derived (read-only): terrain size in metres = grid × cell.</summary>
    public double TerrainMeters => GridCells * CellSize;

    /// <summary>Derived (read-only): metres per pixel of the source image.</summary>
    public double Resolution =>
        TerrainMeters > 0 && SatSourcePx > 0 ? TerrainMeters / SatSourcePx : 0;

    public string TerrainMetersText => TerrainMeters > 0 ? $"{TerrainMeters:0.###} m" : "—";
    public string ResolutionText => Resolution > 0 ? $"{Resolution:0.######} m/px" : "—";

    [ObservableProperty] private string _shapeStatus = "Enter the Mapframe values to enable shape overlays.";

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
        _gridCells = settings.GridCells;
        _cellSize = settings.CellSize;
        _satSourcePx = settings.SatSourcePx;
        _shapeOffsetX = settings.ShapeOffsetX;
        _shapeOffsetY = settings.ShapeOffsetY;
        _shapeNudgeX = settings.ShapeNudgeX;
        _shapeNudgeY = settings.ShapeNudgeY;
        Document.BrushSize = settings.BrushSize;
    }

    // --- settings write-through ------------------------------------------- //
    partial void OnBrushSizeChanged(int value)
    {
        _settings.BrushSize = value;
        Document.BrushSize = value;
        Canvas?.InvalidateView(); // resize the brush ring under a stationary cursor
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
        if (value && Document.Mask is { } m && TileGridProblem(m.Width) is { } warn)
            Log($"Tile grid may be misaligned — {warn}");
    }

    partial void OnTileSizeChanged(int value) { _settings.TileSize = value; RefreshShapes(); }
    partial void OnTileOverlapChanged(int value) { _settings.TileOverlap = value; RefreshShapes(); }
    partial void OnTilesInRowChanged(int value) { _settings.TilesInRow = value; RefreshShapes(); }
    partial void OnTileMaxColorsChanged(int value) { _settings.TileMaxColors = value; RefreshShapes(); }

    partial void OnGridCellsChanged(int value)
    {
        _settings.GridCells = value;
        OnPropertyChanged(nameof(TerrainMeters));
        OnPropertyChanged(nameof(TerrainMetersText));
        OnPropertyChanged(nameof(ResolutionText));
        RefreshShapes();
    }

    partial void OnCellSizeChanged(double value)
    {
        _settings.CellSize = value;
        OnPropertyChanged(nameof(TerrainMeters));
        OnPropertyChanged(nameof(TerrainMetersText));
        OnPropertyChanged(nameof(ResolutionText));
        RefreshShapes();
    }

    partial void OnSatSourcePxChanged(int value)
    {
        _settings.SatSourcePx = value;
        OnPropertyChanged(nameof(Resolution));
        OnPropertyChanged(nameof(ResolutionText));
        RefreshShapes();
    }

    partial void OnShapeOffsetXChanged(double value) { _settings.ShapeOffsetX = value; RefreshShapes(); }
    partial void OnShapeOffsetYChanged(double value) { _settings.ShapeOffsetY = value; RefreshShapes(); }
    partial void OnShapeNudgeXChanged(double value) { _settings.ShapeNudgeX = value; RefreshShapes(); }
    partial void OnShapeNudgeYChanged(double value) { _settings.ShapeNudgeY = value; RefreshShapes(); }

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
                RefreshShapes();
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
            Surfaces.Add(new SurfaceItemViewModel(s, ThumbnailService.Load(s.Name, CfgPath)));
        ArmedSurface = null;
        Document.ArmedRgb = -1;
        ArmedText = "Pick a surface to paint";
        // A freshly loaded mask hasn't been checked yet — no fixes offered until it is.
        HasStrays = false;
        HasOverLimitTiles = false;
    }

    // --- arming ----------------------------------------------------------- //
    [ObservableProperty] private bool _isEyedropperActive;

    partial void OnIsEyedropperActiveChanged(bool value) => Canvas?.SetEyedropper(value);

    [RelayCommand]
    private void ArmSurface(SurfaceItemViewModel? item)
    {
        if (item is null) return;
        if (ReferenceEquals(ArmedSurface, item)) { Disarm(); return; } // click armed → unarm
        Arm(item);
    }

    /// <summary>Arm a surface for painting (shared by click and the eyedropper).</summary>
    private void Arm(SurfaceItemViewModel item)
    {
        if (ArmedSurface != null && !ReferenceEquals(ArmedSurface, item))
            ArmedSurface.IsArmed = false;
        ArmedSurface = item;
        item.IsArmed = true;
        Document.ArmedRgb = item.PackedRgb;
        ArmedText = $"Painting: {item.Name}  ({item.RgbText})";
        Canvas?.InvalidateView(); // recolour the brush ring immediately
    }

    /// <summary>
    /// Eyedropper callback: arm the legend surface matching the sampled mask colour.
    /// One-shot — picking returns to painting with that surface armed.
    /// </summary>
    public void PickColor(int rgb)
    {
        IsEyedropperActive = false;
        var match = Surfaces.FirstOrDefault(s => s.PackedRgb == rgb);
        if (match is null)
        {
            Log($"Eyedropper: no legend surface matches RGB {(rgb >> 16) & 255}, " +
                $"{(rgb >> 8) & 255}, {rgb & 255} (stray colour).");
            return;
        }
        Arm(match);
    }

    /// <summary>Stop the active tool — cancels the eyedropper and clears the armed surface.</summary>
    [RelayCommand]
    private void Disarm()
    {
        IsEyedropperActive = false;
        if (ArmedSurface is null) return;
        ArmedSurface.IsArmed = false;
        ArmedSurface = null;
        Document.ArmedRgb = -1;
        ArmedText = "Pick a surface to paint";
        Canvas?.InvalidateView(); // hide the brush ring
    }

    // --- terrain setup + shape overlays ---------------------------------- //
    private const double ExtentTolerance = 1.0; // metres of slack at the terrain edge

    /// <summary>
    /// Validate the terrain setup against the loaded mask. Returns null when the
    /// setup is complete and consistent, otherwise a message explaining what to fix.
    /// Nothing is inferred — every value comes from the Mapframe the user entered.
    /// </summary>
    private string? SetupProblem()
    {
        if (GridCells <= 0 || CellSize <= 0)
            return "Set Grid size and Cell size (from the Mapframe).";
        if (SatSourcePx <= 0)
            return "Set the sat/mask source size (px) from the Mapframe.";
        if (TileSize <= 0 || TilesInRow <= 0 || TileMaxColors <= 0)
            return "Set the tile size, tiles-in-row and max colours from the Samplers tab.";
        if (Document.Mask is null)
            return "Load a mask to position shapes.";
        if (Document.Mask.Width != Document.Mask.Height)
            return $"Loaded mask is {Document.Mask.Width}×{Document.Mask.Height}; it must be square.";
        if (Document.Mask.Width != SatSourcePx)
            return $"Loaded mask is {Document.Mask.Width}px but the Mapframe source is {SatSourcePx}px — wrong or stale mask.";
        return TileGridProblem(SatSourcePx);
    }

    /// <summary>
    /// Reason the entered tile (Samplers) values don't match an image of
    /// <paramref name="imgSize"/> px, or null when they tile it exactly. Keeps the
    /// tile grid honest to Terrain Builder: a typo'd tile size / count / overlap
    /// that doesn't reconcile with the satmap source is caught instead of drawing
    /// a plausible-but-wrong grid.
    /// </summary>
    private string? TileGridProblem(int imgSize)
    {
        if (TileSize <= 0 || TilesInRow <= 0) return null; // "not set" handled elsewhere
        if (TileGeometry.GridFitsImage(TileSize, TileOverlap, TilesInRow, imgSize))
            return null;
        var (min, max) = TileGeometry.FittingImageSizeRange(TileSize, TileOverlap, TilesInRow);
        return $"Tile settings don't match this {imgSize}px terrain: {TilesInRow} tiles of " +
               $"{TileSize}px (overlap {TileOverlap}) tile a {min}–{max}px source. " +
               $"Check the Samplers values on the Terrain tab — use TB's *Actual* overlap, not Desired.";
    }

    /// <summary>Reason this layer's bounding box falls outside the terrain, or null.</summary>
    private string? ExtentProblem(ShapeLayer layer)
    {
        double terr = TerrainMeters;
        double x0 = ShapeOffsetX - ExtentTolerance, x1 = ShapeOffsetX + terr + ExtentTolerance;
        double y0 = ShapeOffsetY - ExtentTolerance, y1 = ShapeOffsetY + terr + ExtentTolerance;
        if (layer.MinX < x0 || layer.MaxX > x1 || layer.MinY < y0 || layer.MaxY > y1)
            return $"outside terrain extent " +
                   $"[{ShapeOffsetX:0}–{ShapeOffsetX + terr:0}, {ShapeOffsetY:0}–{ShapeOffsetY + terr:0}]; " +
                   $"shape spans ({layer.MinX:0},{layer.MinY:0})–({layer.MaxX:0},{layer.MaxY:0})";
        return null;
    }

    /// <summary>
    /// Push the transform + visible layers to the canvas — but only when the setup
    /// verifies. Anything unverified blocks rendering and reports why (no guessing).
    /// </summary>
    private void RefreshShapes()
    {
        var problem = SetupProblem();
        if (problem != null)
        {
            ShapeStatus = problem;
            Canvas?.SetShapeLayers(null);
            return;
        }

        // Y is always flipped: TB world origin is bottom-left, image origin is top-left.
        Canvas?.SetWorldTransform(new WorldToPixel(
            Document.Mask!.Width, Document.Mask.Height, TerrainMeters, flipY: true,
            ShapeOffsetX, ShapeOffsetY, ShapeNudgeX, ShapeNudgeY));

        var render = new List<ShapeRenderLayer>();
        int blocked = 0, shownFiles = 0;
        foreach (var v in ShapeLayers)
        {
            v.ExtentProblem = ExtentProblem(v.Layer);
            if (v.ExtentProblem != null) { blocked++; continue; }
            if (!v.IsVisible) continue;
            shownFiles++;

            if (v.GroupByLayer && v.CanGroup)
            {
                foreach (var g in v.Groups)
                    if (g.IsVisible)
                        render.Add(new ShapeRenderLayer(v.Name, g.Features,
                            g.MinX, g.MinY, g.MaxX, g.MaxY, g.Color, v.Opacity));
            }
            else
            {
                render.Add(new ShapeRenderLayer(v.Name, v.Layer.Features,
                    v.Layer.MinX, v.Layer.MinY, v.Layer.MaxX, v.Layer.MaxY, v.Color, v.Opacity));
            }
        }
        Canvas?.SetShapeLayers(render);

        ShapeStatus = ShapeLayers.Count == 0
            ? $"Setup OK ({TerrainMeters:0} m, {Resolution:0.###} m/px). Add shapefile(s)."
            : blocked > 0
                ? $"{shownFiles} shown, {blocked} blocked (outside terrain extent — see log)."
                : $"{shownFiles} file(s) shown.";
    }

    private void PersistShapes()
    {
        _settings.ShapeLayers = ShapeLayers.Select(v => new ShapeLayerSetting
        {
            Path = v.Path,
            ColorArgb = v.Color.ToUInt32(),
            Visible = v.IsVisible,
            Opacity = v.Opacity,
            GroupByLayer = v.GroupByLayer,
        }).ToList();
    }

    private ShapeLayerViewModel WireLayer(ShapeLayer layer, string path, Color color,
        bool visible, double opacity, bool groupByLayer = false)
    {
        var vm = new ShapeLayerViewModel(layer, path, color)
        {
            IsVisible = visible,
            Opacity = opacity,
            GroupByLayer = groupByLayer,
        };
        vm.Changed += OnShapeLayerChanged;
        vm.RemoveRequested += RemoveShapeLayer;
        return vm;
    }

    private void OnShapeLayerChanged()
    {
        RefreshShapes();
        PersistShapes();
    }

    private void RemoveShapeLayer(ShapeLayerViewModel vm)
    {
        vm.Changed -= OnShapeLayerChanged;
        vm.RemoveRequested -= RemoveShapeLayer;
        ShapeLayers.Remove(vm);
        Log($"Removed shape layer '{vm.Name}'.");
        RefreshShapes();
        PersistShapes();
    }

    [RelayCommand]
    private async Task AddShapefilesAsync()
    {
        if (Dialogs is null) return;
        var paths = await Dialogs.OpenFilesAsync("Add shapefile(s)",
            ("Shapefiles", new[] { "*.shp" }), ("All files", new[] { "*" }));
        if (paths.Count == 0) return;

        foreach (var path in paths)
        {
            try
            {
                var layer = await Task.Run(() => ShapefileLoader.Load(path));
                var color = ShapeLayerViewModel.Palette[ShapeLayers.Count % ShapeLayerViewModel.Palette.Length];
                ShapeLayers.Add(WireLayer(layer, path, color, visible: true, opacity: 1.0));
                Log($"Added shape layer '{layer.Name}': {layer.Features.Count} features, " +
                    $"bbox ({layer.MinX:0},{layer.MinY:0})-({layer.MaxX:0},{layer.MaxY:0}).");
                var ext = ExtentProblem(layer);
                if (ext != null) Log($"  ! '{layer.Name}' {ext}");
            }
            catch (Exception ex)
            {
                Log($"ERROR loading {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        RefreshShapes();
        PersistShapes();
    }

    /// <summary>Reload shape layers saved in settings (called once after the canvas is wired).</summary>
    public async Task RestoreSavedShapesAsync()
    {
        if (_settings.ShapeLayers.Count == 0) return;
        foreach (var s in _settings.ShapeLayers)
        {
            if (string.IsNullOrWhiteSpace(s.Path) || !File.Exists(s.Path)) continue;
            try
            {
                var layer = await Task.Run(() => ShapefileLoader.Load(s.Path));
                ShapeLayers.Add(WireLayer(layer, s.Path, Color.FromUInt32(s.ColorArgb),
                    s.Visible, s.Opacity, s.GroupByLayer));
                Log($"Restored shape layer '{layer.Name}'.");
            }
            catch (Exception ex)
            {
                Log($"Could not restore {Path.GetFileName(s.Path)}: {ex.Message}");
            }
        }
        RefreshShapes();
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
            HasStrays = sum.Invalid > 0;
            // Highlight the worst stray colours (cap mirrors the plugin's 64).
            var strays = sum.Strays.Take(64).Select(s => s.Rgb.Packed).ToHashSet();
            Canvas?.SetStrayHighlights(strays.Count > 0 ? strays : null);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SnapToLegendAsync()
    {
        if (Document.Mask is null || Document.Config is null) { Log("Load a mask first."); return; }
        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            var surfaces = Document.Config.Surfaces;
            long changed = await Task.Run(() => AutoFix.SnapToLegend(mask, surfaces));
            Document.MaskDirty = true;
            Canvas?.RefreshMask();
            Canvas?.ClearHistory(); // whole-mask edit — paint undo no longer applies
            Log($"Snap to legend: {changed:N0} stray pixel(s) replaced.");

            var sum = await Task.Run(() => Validation.CheckLegend(mask, surfaces));
            ReportLegend(sum);
            UpdateCoverage(sum);
            HasStrays = sum.Invalid > 0;
            var strays = sum.Strays.Take(64).Select(s => s.Rgb.Packed).ToHashSet();
            Canvas?.SetStrayHighlights(strays.Count > 0 ? strays : null);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckTilesAsync()
    {
        if (Document.Mask is null) { Log("Load a mask first."); return; }
        if (TileSize <= 0 || TilesInRow <= 0 || TileMaxColors <= 0)
        {
            Log("Set tile size, tiles-in-row and max colours (from the Samplers tab) first.");
            return;
        }
        if (TileGridProblem(Document.Mask.Width) is { } gridProblem)
        {
            Log(gridProblem);
            return;
        }
        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            int ts = TileSize, ov = TileOverlap, nt = TilesInRow, mx = TileMaxColors;
            var res = await Task.Run(() => Validation.CheckTiles(mask, ts, ov, nt, mx));
            ReportTiles(res);
            HasOverLimitTiles = res.OverLimit.Count > 0;
            Canvas?.SetTileHighlights(res.OverLimit.Count > 0 ? res.OverLimit : null, res.MaxColors);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ConsolidateTilesAsync()
    {
        if (Document.Mask is null) { Log("Load a mask first."); return; }
        if (TileSize <= 0 || TilesInRow <= 0 || TileMaxColors <= 0)
        {
            Log("Set tile size, tiles-in-row and max colours (Terrain tab) first.");
            return;
        }
        if (TileGridProblem(Document.Mask.Width) is { } gridProblem)
        {
            Log(gridProblem);
            return;
        }
        IsBusy = true;
        try
        {
            var mask = Document.Mask;
            int ts = TileSize, ov = TileOverlap, nt = TilesInRow, mx = TileMaxColors;
            int fixedTiles = await Task.Run(() => AutoFix.ConsolidateTiles(mask, ts, ov, nt, mx));
            Document.MaskDirty = true;
            Canvas?.RefreshMask();
            Canvas?.ClearHistory(); // whole-mask edit — paint undo no longer applies
            Log($"Consolidate tiles: fixed {fixedTiles} over-limit tile(s).");

            var res = await Task.Run(() => Validation.CheckTiles(mask, ts, ov, nt, mx));
            ReportTiles(res);
            HasOverLimitTiles = res.OverLimit.Count > 0;
            Canvas?.SetTileHighlights(res.OverLimit.Count > 0 ? res.OverLimit : null, res.MaxColors);
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
