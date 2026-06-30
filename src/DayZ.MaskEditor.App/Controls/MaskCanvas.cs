using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DayZ.MaskEditor.App.Models;
using DayZ.MaskEditor.App.ViewModels;
using DayZ.MaskEditor.Core.Masking;
using DayZ.MaskEditor.Core.Shapes;

namespace DayZ.MaskEditor.App.Controls;

/// <summary>
/// The overlay editing surface. Renders only the visible viewport — it samples
/// the satmap + mask buffers into a screen-sized bitmap each time the view
/// changes, so memory and texture cost are bounded regardless of image size
/// (15360² class works). Painting writes exact legend colours into the mask
/// buffer and recomposites just the affected screen rectangle.
/// </summary>
public sealed class MaskCanvas : Control, ICanvasHost
{
    private EditorDocument? _doc;

    private WriteableBitmap? _view;   // screen-sized composite (DIP pixels)
    private int _viewW, _viewH;

    // View transform: image coord shown at screen origin + screen px per image px.
    private double _zoom = 1.0;
    private double _originX, _originY;

    private double _opacity = 0.6;

    // Overlays
    private bool _showGrid;
    private int _gridTile, _gridOverlap, _gridNt;
    private IReadOnlyList<OverLimitTile>? _tileHighlights;
    private int _tileLimit;
    private HashSet<int>? _strayHighlights;

    // Shapefile overlays
    private IReadOnlyList<ShapeRenderLayer>? _shapeLayers;
    private WorldToPixel? _worldToPixel;

    // Hover tooltip
    private string? _hoverText;
    private Point _hoverPos;
    private const double HoverTolerancePx = 6;

    // Interaction state
    private bool _painting;
    private bool _panning;
    private Point _lastPointer;
    private double _lastImgX, _lastImgY;

    // Brush cursor (drawn overlay reflecting brush size + armed colour)
    private Point _cursorPos;
    private bool _cursorInside;

    public MaskCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        // Crosshair marks the exact centre pixel; the drawn ring shows size + colour.
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public EditorDocument? Document
    {
        get => _doc;
        set { _doc = value; ReloadDocument(); }
    }

    // ----------------------------------------------------------------- ICanvasHost
    public void ReloadDocument()
    {
        EnsureViewBitmap();
        FitToView();
    }

    public void InvalidateView() => InvalidateVisual();

    public void RefreshMask() => RecomposeAll();

    public void SetOverlayOpacity(double opacity)
    {
        _opacity = Math.Clamp(opacity, 0, 1);
        RecomposeAll();
    }

    public void SetTileGrid(bool show, int tileSize, int overlap, int tilesInRow)
    {
        _showGrid = show;
        _gridTile = tileSize;
        _gridOverlap = overlap;
        _gridNt = tilesInRow;
        InvalidateVisual();
    }

    public void SetTileHighlights(IReadOnlyList<OverLimitTile>? tiles, int maxColors)
    {
        _tileHighlights = tiles;
        _tileLimit = maxColors;
        InvalidateVisual();
    }

    public void SetStrayHighlights(IReadOnlyCollection<int>? strayRgb)
    {
        _strayHighlights = strayRgb is null ? null : new HashSet<int>(strayRgb);
        RecomposeAll();
    }

    public void SetShapeLayers(IReadOnlyList<ShapeRenderLayer>? layers)
    {
        _shapeLayers = layers;
        _hoverText = null;
        InvalidateVisual();
    }

    public void SetWorldTransform(WorldToPixel transform)
    {
        _worldToPixel = transform;
        _hoverText = null;
        InvalidateVisual();
    }

    public void FitToView()
    {
        var (w, h) = ImageSize();
        if (w == 0 || h == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            InvalidateVisual();
            return;
        }
        double zx = Bounds.Width / w;
        double zy = Bounds.Height / h;
        _zoom = Math.Min(zx, zy);
        // Centre the image.
        _originX = -((Bounds.Width / _zoom) - w) / 2.0;
        _originY = -((Bounds.Height / _zoom) - h) / 2.0;
        RecomposeAll();
    }

    // ----------------------------------------------------------------- sizing
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            EnsureViewBitmap();
            RecomposeAll();
        }
    }

    private void EnsureViewBitmap()
    {
        int w = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        int h = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        if (_view != null && _viewW == w && _viewH == h) return;
        _view?.Dispose();
        _view = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Premul);
        _viewW = w;
        _viewH = h;
    }

    private (int W, int H) ImageSize()
    {
        var b = _doc?.Mask ?? _doc?.Satmap;
        return b is null ? (0, 0) : (b.Width, b.Height);
    }

    // ----------------------------------------------------------------- compose
    private void RecomposeAll() => RecomposeRect(0, 0, _viewW, _viewH);

    private unsafe void RecomposeRect(int rx, int ry, int rw, int rh)
    {
        if (_view is null) return;
        EnsureViewBitmap();
        rx = Math.Clamp(rx, 0, _viewW);
        ry = Math.Clamp(ry, 0, _viewH);
        rw = Math.Clamp(rw, 0, _viewW - rx);
        rh = Math.Clamp(rh, 0, _viewH - ry);
        if (rw == 0 || rh == 0) { InvalidateVisual(); return; }

        var sat = _doc?.Satmap;
        var mask = _doc?.Mask;
        bool overlay = sat != null && mask != null &&
                       sat.Width == mask.Width && sat.Height == mask.Height;
        var strays = _strayHighlights;
        double invZoom = 1.0 / _zoom;

        using (var fb = _view.Lock())
        {
            byte* basePtr = (byte*)fb.Address;
            int rowBytes = fb.RowBytes;

            for (int sy = ry; sy < ry + rh; sy++)
            {
                double imgYf = _originY + (sy + 0.5) * invZoom;
                int iy = (int)Math.Floor(imgYf);
                byte* row = basePtr + sy * rowBytes + rx * 4;

                for (int sx = rx; sx < rx + rw; sx++, row += 4)
                {
                    double imgXf = _originX + (sx + 0.5) * invZoom;
                    int ix = (int)Math.Floor(imgXf);

                    byte r, g, b;
                    if (overlay && InBounds(ix, iy, mask!.Width, mask.Height))
                    {
                        int mrgb = mask.GetRgb(ix, iy);
                        if (strays != null && strays.Contains(mrgb))
                        {
                            r = 255; g = 0; b = 255; // magenta stray marker
                        }
                        else
                        {
                            int srgb = sat!.GetRgb(ix, iy);
                            r = Blend((byte)(srgb >> 16), (byte)(mrgb >> 16));
                            g = Blend((byte)(srgb >> 8), (byte)(mrgb >> 8));
                            b = Blend((byte)srgb, (byte)mrgb);
                        }
                    }
                    else if (mask != null && InBounds(ix, iy, mask.Width, mask.Height))
                    {
                        int mrgb = mask.GetRgb(ix, iy);
                        r = (byte)(mrgb >> 16); g = (byte)(mrgb >> 8); b = (byte)mrgb;
                    }
                    else if (sat != null && InBounds(ix, iy, sat.Width, sat.Height))
                    {
                        int srgb = sat.GetRgb(ix, iy);
                        r = (byte)(srgb >> 16); g = (byte)(srgb >> 8); b = (byte)srgb;
                    }
                    else
                    {
                        r = g = b = 32; // outside-image backdrop
                    }

                    row[0] = b; row[1] = g; row[2] = r; row[3] = 255;
                }
            }
        }
        InvalidateVisual();
    }

    private byte Blend(byte sat, byte mask) =>
        (byte)(mask * _opacity + sat * (1.0 - _opacity) + 0.5);

    private static bool InBounds(int x, int y, int w, int h) =>
        x >= 0 && y >= 0 && x < w && y < h;

    // ----------------------------------------------------------------- render
    public override void Render(DrawingContext context)
    {
        if (_view != null)
            context.DrawImage(_view, new Rect(0, 0, _viewW, _viewH));

        DrawTileGrid(context);
        DrawShapes(context);
        DrawTileDim(context);        // darken everything outside over-limit tiles
        DrawTileHighlights(context); // crisp border on top of the dimming
        DrawTileBadges(context);     // colour-count badge per failed tile
        DrawHoverTooltip(context);
        DrawBrushCursor(context);
    }

    /// <summary>
    /// Darken the whole canvas except the over-limit tiles, so failed tiles stand out
    /// as the only full-brightness areas. Uses an even-odd geometry (full-canvas rect
    /// with each tile punched out as a hole) — no per-pixel cost.
    /// </summary>
    private void DrawTileDim(DrawingContext ctx)
    {
        if (_tileHighlights is null || _tileHighlights.Count == 0) return;

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, Bounds.Width, Bounds.Height)));
        foreach (var t in _tileHighlights)
        {
            double sxp = (t.X - _originX) * _zoom;
            double syp = (t.Y - _originY) * _zoom;
            group.Children.Add(new RectangleGeometry(
                new Rect(sxp, syp, t.W * _zoom, t.H * _zoom)));
        }
        ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, group);
    }

    /// <summary>Draw a "colours/limit" badge at the visible top-left of each failed tile.</summary>
    private void DrawTileBadges(DrawingContext ctx)
    {
        if (_tileHighlights is null || _tileHighlights.Count == 0) return;

        var bg = new SolidColorBrush(Color.FromArgb(235, 200, 0, 0));
        var border = new Pen(Brushes.White, 1);
        foreach (var t in _tileHighlights)
        {
            double sxp = (t.X - _originX) * _zoom;
            double syp = (t.Y - _originY) * _zoom;
            // Clamp to the on-screen part of the tile so the badge is always visible.
            double vx = Math.Max(sxp, 0), vy = Math.Max(syp, 0);
            double vr = Math.Min(sxp + t.W * _zoom, Bounds.Width);
            double vb = Math.Min(syp + t.H * _zoom, Bounds.Height);
            if (vr <= vx || vb <= vy) continue; // tile fully off-screen

            string label = _tileLimit > 0 ? $"{t.ColorCount} / {_tileLimit}" : t.ColorCount.ToString();
            var ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), 13, Brushes.White);

            const double pad = 4, margin = 3;
            var rect = new Rect(vx + margin, vy + margin, ft.Width + pad * 2, ft.Height + pad * 2);
            ctx.DrawRectangle(bg, border, rect, 3, 3);
            ctx.DrawText(ft, new Point(vx + margin + pad, vy + margin + pad));
        }
    }

    /// <summary>
    /// Draw the brush footprint under the pointer: a translucent disc tinted with the
    /// armed colour, ringed for crispness with a dark halo so it reads on any
    /// background. Diameter is the brush size in image px scaled by the current zoom,
    /// so it matches exactly what a dab will paint. Only shown when a surface is armed
    /// and a mask is loaded (<see cref="CanPaint"/>).
    /// </summary>
    private void DrawBrushCursor(DrawingContext ctx)
    {
        if (!_cursorInside || !CanPaint()) return;

        int rgb = _doc!.ArmedRgb;
        var col = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        int size = Math.Max(1, _doc.BrushSize);
        // True footprint radius; clamp the drawn ring to a visible minimum so the
        // colour is still readable when zoomed far out (the crosshair marks centre).
        double rad = Math.Max(2.5, size * _zoom / 2.0);

        var fill = new SolidColorBrush(Color.FromArgb(64, col.R, col.G, col.B));
        var halo = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 3);
        var ring = new Pen(new SolidColorBrush(col), 1.5);

        ctx.DrawEllipse(fill, null, _cursorPos, rad, rad);
        ctx.DrawEllipse(null, halo, _cursorPos, rad, rad);
        ctx.DrawEllipse(null, ring, _cursorPos, rad, rad);
    }

    private void DrawTileGrid(DrawingContext ctx)
    {
        if (!_showGrid || _gridNt <= 0 || _gridTile <= 0) return;
        var (w, h) = ImageSize();
        if (w == 0) return;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 200, 255)), 1);
        for (int i = 0; i < _gridNt; i++)
        {
            var (cx, _) = TileGeometry.TileRegion(i, _gridNt, _gridTile, _gridOverlap, w);
            var (cy, _) = TileGeometry.TileRegion(i, _gridNt, _gridTile, _gridOverlap, h);
            double sxp = (cx - _originX) * _zoom;
            double syp = (cy - _originY) * _zoom;
            ctx.DrawLine(pen, new Point(sxp, 0), new Point(sxp, Bounds.Height));
            ctx.DrawLine(pen, new Point(0, syp), new Point(Bounds.Width, syp));
        }
    }

    private void DrawTileHighlights(DrawingContext ctx)
    {
        if (_tileHighlights is null) return;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 40, 40)), 2.5);
        foreach (var t in _tileHighlights)
        {
            double sxp = (t.X - _originX) * _zoom;
            double syp = (t.Y - _originY) * _zoom;
            ctx.DrawRectangle(null, pen, new Rect(sxp, syp, t.W * _zoom, t.H * _zoom));
        }
    }

    private void DrawShapes(DrawingContext ctx)
    {
        if (_shapeLayers is null || _shapeLayers.Count == 0 || _worldToPixel is not { } w2p)
            return;

        // Viewport in image space (with a small margin), for part-level culling.
        double vL = _originX - 2, vT = _originY - 2;
        double vR = _originX + Bounds.Width / _zoom + 2;
        double vB = _originY + Bounds.Height / _zoom + 2;

        foreach (var layer in _shapeLayers)
        {
            if (layer.Features.Count == 0) continue;

            byte a = (byte)Math.Clamp(layer.Opacity * 255, 0, 255);
            var stroke = new Pen(
                new SolidColorBrush(Color.FromArgb(a, layer.Color.R, layer.Color.G, layer.Color.B)),
                1.5);
            var fill = new SolidColorBrush(
                Color.FromArgb((byte)(a * 0.25), layer.Color.R, layer.Color.G, layer.Color.B));

            foreach (var feature in layer.Features)
            {
                foreach (var part in feature.Parts)
                {
                    if (part.Count == 0) continue;

                    if (feature.Kind == ShapeKind.Point)
                    {
                        var (ix, iy) = w2p.ToPixel(part[0]);
                        if (ix < vL || ix > vR || iy < vT || iy > vB) continue;
                        var c = new Point((ix - _originX) * _zoom, (iy - _originY) * _zoom);
                        ctx.DrawEllipse(stroke.Brush, null, c, 3, 3);
                        continue;
                    }

                    // Convert part to image space + bbox for culling.
                    double pMinX = double.MaxValue, pMinY = double.MaxValue;
                    double pMaxX = double.MinValue, pMaxY = double.MinValue;
                    var imgPts = new (double X, double Y)[part.Count];
                    for (int i = 0; i < part.Count; i++)
                    {
                        var (px, py) = w2p.ToPixel(part[i]);
                        imgPts[i] = (px, py);
                        if (px < pMinX) pMinX = px;
                        if (py < pMinY) pMinY = py;
                        if (px > pMaxX) pMaxX = px;
                        if (py > pMaxY) pMaxY = py;
                    }
                    if (pMaxX < vL || pMinX > vR || pMaxY < vT || pMinY > vB) continue;

                    bool polygon = feature.Kind == ShapeKind.Polygon;
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        g.BeginFigure(ToScreen(imgPts[0]), isFilled: polygon);
                        for (int i = 1; i < imgPts.Length; i++)
                            g.LineTo(ToScreen(imgPts[i]));
                        g.EndFigure(polygon);
                    }
                    ctx.DrawGeometry(polygon ? fill : null, stroke, geo);
                }
            }
        }
    }

    private Point ToScreen((double X, double Y) img) =>
        new((img.X - _originX) * _zoom, (img.Y - _originY) * _zoom);

    // ----------------------------------------------------------------- hover
    private void UpdateHover(Point pos)
    {
        var text = HitTestShapes(pos);
        if (text is null)
        {
            if (_hoverText != null) { _hoverText = null; InvalidateVisual(); }
            return;
        }
        _hoverText = text;
        _hoverPos = pos;
        InvalidateVisual();
    }

    private string? HitTestShapes(Point pos)
    {
        if (_shapeLayers is null || _shapeLayers.Count == 0 || _worldToPixel is not { } w2p)
            return null;

        var (cx, cy) = ScreenToImage(pos);
        double tol = HoverTolerancePx / _zoom;
        double tolSq = tol * tol;

        foreach (var layer in _shapeLayers)
        {
            // Layer bounding-box cull (world → image).
            var (a, b) = w2p.ToPixel(new WorldPoint(layer.MinX, layer.MinY));
            var (c, d) = w2p.ToPixel(new WorldPoint(layer.MaxX, layer.MaxY));
            if (cx < Math.Min(a, c) - tol || cx > Math.Max(a, c) + tol ||
                cy < Math.Min(b, d) - tol || cy > Math.Max(b, d) + tol)
                continue;

            foreach (var feature in layer.Features)
            {
                foreach (var part in feature.Parts)
                {
                    if (part.Count == 0) continue;

                    double pminx = double.MaxValue, pminy = double.MaxValue;
                    double pmaxx = double.MinValue, pmaxy = double.MinValue;
                    var pts = new (double X, double Y)[part.Count];
                    for (int i = 0; i < part.Count; i++)
                    {
                        pts[i] = w2p.ToPixel(part[i]);
                        if (pts[i].X < pminx) pminx = pts[i].X;
                        if (pts[i].Y < pminy) pminy = pts[i].Y;
                        if (pts[i].X > pmaxx) pmaxx = pts[i].X;
                        if (pts[i].Y > pmaxy) pmaxy = pts[i].Y;
                    }
                    if (cx < pminx - tol || cx > pmaxx + tol || cy < pminy - tol || cy > pmaxy + tol)
                        continue;

                    bool hit = feature.Kind switch
                    {
                        ShapeKind.Point => Dist2(cx, cy, pts[0].X, pts[0].Y) <= tolSq,
                        ShapeKind.Polygon => PointInPolygon(cx, cy, pts) || NearAnySegment(cx, cy, pts, tolSq),
                        _ => NearAnySegment(cx, cy, pts, tolSq),
                    };
                    if (hit) return BuildHoverText(layer.Name, feature);
                }
            }
        }
        return null;
    }

    private static string BuildHoverText(string layerName, ShapeFeature feature)
    {
        var sb = new StringBuilder();
        sb.Append(layerName).Append('\n').Append(feature.Kind);
        foreach (var kv in feature.Attributes)
            sb.Append('\n').Append(FriendlyAttr(kv.Key)).Append(": ").Append(kv.Value);
        return sb.ToString();
    }

    // Terrain Builder's __LAYER/__ID are TB layer metadata, NOT DayZ surfaces —
    // label them so the tooltip can't be mistaken for a surface assignment.
    private static string FriendlyAttr(string key) => key.ToUpperInvariant() switch
    {
        "__LAYER" => "TB layer",
        "__ID" => "TB id",
        _ => key,
    };

    private static double Dist2(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx, dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static bool NearAnySegment(double px, double py, (double X, double Y)[] pts, double tolSq)
    {
        for (int i = 0; i < pts.Length - 1; i++)
            if (SegDist2(px, py, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y) <= tolSq)
                return true;
        return false;
    }

    private static double SegDist2(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        double t = len2 <= 0 ? 0 : ((px - ax) * dx + (py - ay) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        double qx = ax + t * dx, qy = ay + t * dy;
        return Dist2(px, py, qx, qy);
    }

    private static bool PointInPolygon(double px, double py, (double X, double Y)[] pts)
    {
        bool inside = false;
        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
        {
            if (pts[i].Y > py != pts[j].Y > py &&
                px < (pts[j].X - pts[i].X) * (py - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
                inside = !inside;
        }
        return inside;
    }

    private void DrawHoverTooltip(DrawingContext ctx)
    {
        if (_hoverText is null) return;
        var ft = new FormattedText(_hoverText, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), 12, Brushes.White);

        const double pad = 6;
        double w = ft.Width + pad * 2, h = ft.Height + pad * 2;
        double x = _hoverPos.X + 14, y = _hoverPos.Y + 14;
        if (x + w > Bounds.Width) x = _hoverPos.X - w - 14;
        if (y + h > Bounds.Height) y = _hoverPos.Y - h - 14;
        x = Math.Max(0, x);
        y = Math.Max(0, y);

        var rect = new Rect(x, y, w, h);
        ctx.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(235, 25, 25, 25)),
            new Pen(new SolidColorBrush(Color.FromArgb(200, 150, 150, 150)), 1),
            rect, 4, 4);
        ctx.DrawText(ft, new Point(x + pad, y + pad));
    }

    // ----------------------------------------------------------------- input
    private (double X, double Y) ScreenToImage(Point p) =>
        (_originX + p.X / _zoom, _originY + p.Y / _zoom);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetCurrentPoint(this);
        _lastPointer = pt.Position;
        _hoverText = null; // hide tooltip while interacting

        if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsRightButtonPressed)
        {
            _panning = true;
            e.Pointer.Capture(this);
        }
        else if (pt.Properties.IsLeftButtonPressed && CanPaint())
        {
            _painting = true;
            e.Pointer.Capture(this);
            var (ix, iy) = ScreenToImage(pt.Position);
            PaintDab(ix, iy);
            _lastImgX = ix; _lastImgY = iy;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        _cursorPos = pos;
        _cursorInside = true;

        if (_panning)
        {
            var dx = pos.X - _lastPointer.X;
            var dy = pos.Y - _lastPointer.Y;
            _originX -= dx / _zoom;
            _originY -= dy / _zoom;
            _lastPointer = pos;
            RecomposeAll();
        }
        else if (_painting && CanPaint())
        {
            var (ix, iy) = ScreenToImage(pos);
            PaintStroke(_lastImgX, _lastImgY, ix, iy);
            _lastImgX = ix; _lastImgY = iy;
        }
        else
        {
            UpdateHover(pos);
            if (CanPaint()) InvalidateVisual(); // keep the brush ring tracking the cursor
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _painting = false;
        _panning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _cursorInside = false;
        _hoverText = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetPosition(this);
        var (imgX, imgY) = ScreenToImage(pos);
        double factor = e.Delta.Y > 0 ? 1.2 : 1.0 / 1.2;
        double newZoom = Math.Clamp(_zoom * factor, 0.02, 64.0);
        if (Math.Abs(newZoom - _zoom) < 1e-9) return;
        _zoom = newZoom;
        // Keep the point under the cursor fixed.
        _originX = imgX - pos.X / _zoom;
        _originY = imgY - pos.Y / _zoom;
        RecomposeAll();
        e.Handled = true;
    }

    // ----------------------------------------------------------------- painting
    private bool CanPaint() => _doc?.Mask != null && _doc.ArmedRgb >= 0;

    private void PaintStroke(double x0, double y0, double x1, double y1)
    {
        double dist = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        int steps = (int)Math.Ceiling(dist);
        if (steps <= 0) { PaintDab(x1, y1); return; }
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            PaintDab(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
        }
    }

    private void PaintDab(double imgX, double imgY)
    {
        var mask = _doc!.Mask!;
        int rgb = _doc.ArmedRgb;
        int size = Math.Max(1, _doc.BrushSize);
        double radius = size / 2.0;
        int cx = (int)Math.Floor(imgX);
        int cy = (int)Math.Floor(imgY);
        int r = (int)Math.Ceiling(radius);

        int minX = Math.Max(0, cx - r), maxX = Math.Min(mask.Width - 1, cx + r);
        int minY = Math.Max(0, cy - r), maxY = Math.Min(mask.Height - 1, cy + r);
        if (minX > maxX || minY > maxY) return;

        double rr = radius * radius;
        for (int y = minY; y <= maxY; y++)
        {
            double dy = (y + 0.5) - imgY;
            for (int x = minX; x <= maxX; x++)
            {
                double dx = (x + 0.5) - imgX;
                if (size <= 2 || dx * dx + dy * dy <= rr)
                    mask.SetRgb(x, y, rgb);
            }
        }
        _doc.MaskDirty = true;

        // Recompose just the touched screen rectangle.
        double s0x = (minX - _originX) * _zoom, s0y = (minY - _originY) * _zoom;
        double s1x = (maxX + 1 - _originX) * _zoom, s1y = (maxY + 1 - _originY) * _zoom;
        int px = (int)Math.Floor(s0x) - 1, py = (int)Math.Floor(s0y) - 1;
        RecomposeRect(px, py, (int)Math.Ceiling(s1x - s0x) + 2, (int)Math.Ceiling(s1y - s0y) + 2);
    }
}
