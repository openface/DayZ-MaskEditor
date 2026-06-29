using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DayZ.MaskEditor.App.Models;
using DayZ.MaskEditor.App.ViewModels;
using DayZ.MaskEditor.Core.Masking;

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
    private HashSet<int>? _strayHighlights;

    // Interaction state
    private bool _painting;
    private bool _panning;
    private Point _lastPointer;
    private double _lastImgX, _lastImgY;

    public MaskCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
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

    public void SetTileHighlights(IReadOnlyList<OverLimitTile>? tiles)
    {
        _tileHighlights = tiles;
        InvalidateVisual();
    }

    public void SetStrayHighlights(IReadOnlyCollection<int>? strayRgb)
    {
        _strayHighlights = strayRgb is null ? null : new HashSet<int>(strayRgb);
        RecomposeAll();
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
        DrawTileHighlights(context);
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
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 40, 40)), 2);
        foreach (var t in _tileHighlights)
        {
            double sxp = (t.X - _originX) * _zoom;
            double syp = (t.Y - _originY) * _zoom;
            ctx.DrawRectangle(null, pen, new Rect(sxp, syp, t.W * _zoom, t.H * _zoom));
        }
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
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _painting = false;
        _panning = false;
        e.Pointer.Capture(null);
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
