namespace DayZ.MaskEditor.Core.Masking;

/// <summary>A rectangular fragment of a tile. <c>W == 0</c> means disabled.</summary>
public sealed class Fragment
{
    public int X, Y, W, H;
    public Fragment(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

    /// <summary>Grow this fragment's bounding box to also cover <paramref name="o"/>.</summary>
    public void Extend(Fragment o)
    {
        if (o.X + o.W > X + W) W += o.W;
        if (o.Y + o.H > Y + H) H += o.H;
        if (o.X < X) { W += X - o.X; X = o.X; }
        if (o.Y < Y) { H += Y - o.Y; Y = o.Y; }
    }
}

/// <summary>
/// Terrain Builder satellite/mask tile geometry. Direct port of maskcore.py's
/// tile_region + build_tile_fragments. The geometry must match TB (and the
/// MaskColorChecker tool) exactly, including the last-tile <c>-1</c> parity.
/// </summary>
public static class TileGeometry
{
    /// <summary>
    /// Pixel (start, length) for one satellite tile along one axis.
    /// spacing = tileSize - overlap; tiles offset by -overlap/2 and overlap their
    /// neighbours, first tile pinned to 0 and last clamped to the edge.
    /// </summary>
    public static (int Start, int Length) TileRegion(
        int index, int nTiles, int tileSize, int overlap, int imgSize)
    {
        int spacing = tileSize - overlap;
        int half = overlap / 2; // integer truncation of overlap / 2
        if (index == 0)
            return (0, tileSize - half);
        int pos = spacing * index - half;
        if (index == nTiles - 1)
            // Last tile clamps to the edge; the -1 is deliberate MaskColorChecker parity.
            return (pos, imgSize - pos - 1);
        return (pos, tileSize);
    }

    /// <summary>
    /// Subdivide a tile into the 9 overlap fragments, merging/disabling edge and
    /// corner fragments so the enabled ones partition the tile exactly. Returns
    /// the enabled (W&gt;0, H&gt;0) fragments.
    /// </summary>
    public static List<Fragment> BuildTileFragments(
        int ix, int iy, int sx, int sy, int sizeX, int sizeY, int overlap, int nt)
    {
        int ov = overlap;
        int inX = sizeX - 2 * ov;
        int inY = sizeY - 2 * ov;
        var NW = new Fragment(sx, sy, ov, ov);
        var N = new Fragment(sx + ov, sy, inX, ov);
        var NE = new Fragment(sx + sizeX - ov, sy, ov, ov);
        var W = new Fragment(sx, sy + ov, ov, inY);
        var M = new Fragment(sx + ov, sy + ov, inX, inY);
        var E = new Fragment(sx + sizeX - ov, sy + ov, ov, inY);
        var SW = new Fragment(sx, sy + sizeY - ov, ov, ov);
        var S = new Fragment(sx + ov, sy + sizeY - ov, inX, ov);
        var SE = new Fragment(sx + sizeX - ov, sy + sizeY - ov, ov, ov);

        static void Disable(Fragment f) => f.W = 0;

        if (iy == 0)                      // top row: no neighbour above
        {
            if (ix == 0)                  // top-left
            {
                M.Extend(NW); Disable(NW); Disable(N); Disable(W);
                S.Extend(SW); Disable(SW);
                E.Extend(NE); Disable(NE);
            }
            else if (ix == nt - 1)        // top-right
            {
                M.Extend(NE); Disable(NE); Disable(N); Disable(E);
                S.Extend(SE); Disable(SE);
                W.Extend(NW); Disable(NW);
            }
            else                          // top-middle
            {
                W.Extend(NW); Disable(NW);
                M.Extend(N); Disable(N);
                E.Extend(NE); Disable(NE);
            }
        }
        else if (iy == nt - 1)            // bottom row: no neighbour below
        {
            if (ix == 0)                  // bottom-left
            {
                M.Extend(SW); Disable(SW); Disable(W); Disable(S);
                N.Extend(NW); Disable(NW);
                E.Extend(SE); Disable(SE);
            }
            else if (ix == nt - 1)        // bottom-right
            {
                M.Extend(SE); Disable(SE); Disable(E); Disable(S);
                N.Extend(NE); Disable(NE);
                W.Extend(SW); Disable(SW);
            }
            else                          // bottom-middle
            {
                W.Extend(SW); Disable(SW);
                M.Extend(S); Disable(S);
                E.Extend(SE); Disable(SE);
            }
        }
        else                              // middle rows
        {
            if (ix == 0)                  // left column
            {
                N.Extend(NW); Disable(NW);
                M.Extend(W); Disable(W);
                S.Extend(SW); Disable(SW);
            }
            else if (ix == nt - 1)        // right column
            {
                N.Extend(NE); Disable(NE);
                M.Extend(E); Disable(E);
                S.Extend(SE); Disable(SE);
            }
        }

        var all = new[] { NW, N, NE, W, M, E, SW, S, SE };
        return all.Where(f => f.W > 0 && f.H > 0).ToList();
    }
}
