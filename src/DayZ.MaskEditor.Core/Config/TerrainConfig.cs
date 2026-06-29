namespace DayZ.MaskEditor.Core.Config;

/// <summary>An exact 8-bit RGB colour. Doubles as a dictionary/set key.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"({R}, {G}, {B})";

    /// <summary>Pack into a 3-byte array (matches Python's bytes(rgb)).</summary>
    public int Packed => (R << 16) | (G << 8) | B;
}

/// <summary>
/// One paintable surface entry, derived from the Legend &gt; Colors block.
/// Mirrors the dict produced by layers_cfg.parse_layers_cfg in the GIMP plugin.
/// Multi-colour surfaces are split so each colour is its own Surface
/// ("rock", "rock #2", ...).
/// </summary>
public sealed class Surface
{
    public required string Name { get; init; }
    public required Rgb Rgb { get; init; }
    public string? Texture { get; init; }
    public string? Material { get; init; }
}

/// <summary>Result of parsing a layers.cfg. Order follows the Legend Colors block.</summary>
public sealed class TerrainConfig
{
    public IReadOnlyList<Surface> Surfaces { get; init; } = Array.Empty<Surface>();
    public string? Picture { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
