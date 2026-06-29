using DayZ.MaskEditor.Core.Config;

namespace DayZ.MaskEditor.Core.Masking;

/// <summary>One surface's coverage in a Check Legend report.</summary>
public readonly record struct Coverage(string Name, Rgb Rgb, long Count, double Percent);

/// <summary>Result of <see cref="MaskCore.Summarize"/> — the Check Legend report.</summary>
public sealed class LegendSummary
{
    public long Total { get; init; }
    public long Invalid { get; init; }
    public double InvalidPercent { get; init; }
    public IReadOnlyList<Coverage> Coverage { get; init; } = Array.Empty<Coverage>();
    /// <summary>Stray (non-legend) colours, descending by count.</summary>
    public IReadOnlyList<(Rgb Rgb, long Count)> Strays { get; init; } =
        Array.Empty<(Rgb, long)>();
    public int DistinctStrays => Strays.Count;
}

/// <summary>
/// Pure pixel analysis — distinct-colour sets, tallies and the Check Legend
/// summary. Direct port of maskcore.py (the counting half). Colours are packed
/// 24-bit ints (0xRRGGBB) which double as dictionary/set keys, mirroring the
/// Python use of 3-byte slices.
/// </summary>
public static class MaskCore
{
    private static int Pack(ReadOnlySpan<byte> p) => (p[0] << 16) | (p[1] << 8) | p[2];

    /// <summary>Set of distinct colours in a packed pixel buffer.</summary>
    public static HashSet<int> CellColorSet(ReadOnlySpan<byte> buf, int bpp)
    {
        var set = new HashSet<int>();
        for (int i = 0; i + 2 < buf.Length; i += bpp)
            set.Add(Pack(buf.Slice(i, 3)));
        return set;
    }

    /// <summary>Number of distinct colours in a packed pixel buffer.</summary>
    public static int CountCellColors(ReadOnlySpan<byte> buf, int bpp) =>
        CellColorSet(buf, bpp).Count;

    /// <summary>Add a packed pixel buffer's exact colours into a counter (alpha dropped).</summary>
    public static void TallyColors(ReadOnlySpan<byte> buf, int bpp, Dictionary<int, long> counter)
    {
        for (int i = 0; i + 2 < buf.Length; i += bpp)
        {
            int key = Pack(buf.Slice(i, 3));
            counter.TryGetValue(key, out long c);
            counter[key] = c + 1;
        }
    }

    /// <summary>
    /// Turn a colour counter into a Check Legend report. Colours in
    /// <paramref name="surfaces"/> are legend; everything else is a stray.
    /// </summary>
    public static LegendSummary Summarize(
        Dictionary<int, long> counter, long total, IReadOnlyList<Surface> surfaces)
    {
        var legendKeys = new HashSet<int>(surfaces.Select(s => s.Rgb.Packed));

        var coverage = new List<Coverage>(surfaces.Count);
        foreach (var s in surfaces)
        {
            counter.TryGetValue(s.Rgb.Packed, out long c);
            coverage.Add(new Coverage(s.Name, s.Rgb, c, total != 0 ? 100.0 * c / total : 0.0));
        }

        var strays = new List<(Rgb, long)>();
        long invalid = 0;
        foreach (var (key, c) in counter)
        {
            if (!legendKeys.Contains(key))
            {
                strays.Add((Unpack(key), c));
                invalid += c;
            }
        }
        strays.Sort((a, b) => b.Item2.CompareTo(a.Item2));

        return new LegendSummary
        {
            Total = total,
            Invalid = invalid,
            InvalidPercent = total != 0 ? 100.0 * invalid / total : 0.0,
            Coverage = coverage,
            Strays = strays,
        };
    }

    public static Rgb Unpack(int rgb) =>
        new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
