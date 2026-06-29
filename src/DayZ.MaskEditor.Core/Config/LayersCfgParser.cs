using System.Text.RegularExpressions;

namespace DayZ.MaskEditor.Core.Config;

/// <summary>
/// Parser for DayZ/Arma layers.cfg. Direct port of the GIMP plugin's pure
/// layers_cfg.py module (text-only, no I/O), so its behaviour — and the unit
/// tests that pin it — stay equivalent.
/// </summary>
public static class LayersCfgParser
{
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline);
    private static readonly Regex LineComment = new(@"//[^\n]*");

    private static readonly Regex ClassInLayers =
        new(@"class\s+(\w+)\s*\{", RegexOptions.IgnoreCase);
    private static readonly Regex TextureRe =
        new(@"texture\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
    private static readonly Regex MaterialRe =
        new(@"material\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
    private static readonly Regex PictureRe =
        new(@"picture\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);

    // name[] = {{r,g,b}};  or multi-colour name[] = {{r,g,b},{r,g,b},...};
    private static readonly Regex EntryRe = new(
        @"(\w+)\s*\[\s*\]\s*=\s*\{\s*" +
        @"((?:\{\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\}\s*,?\s*)+)\}",
        RegexOptions.IgnoreCase);
    private static readonly Regex TripleRe =
        new(@"\{\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\}");

    private static string StripComments(string text)
    {
        text = BlockComment.Replace(text, "");
        text = LineComment.Replace(text, "");
        return text;
    }

    /// <summary>
    /// Return the body between the matching '{' and its balanced '}' for the
    /// first occurrence of headerRegex (which must end right before the '{').
    /// </summary>
    private static string? ExtractBlock(string text, Regex headerRegex)
    {
        var m = headerRegex.Match(text);
        if (!m.Success) return null;
        int i = text.IndexOf('{', m.Index + m.Length - 1);
        if (i < 0) return null;
        int depth = 0;
        for (int j = i; j < text.Length; j++)
        {
            char c = text[j];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(i + 1, j - (i + 1));
            }
        }
        return text.Substring(i + 1);
    }

    private static string? ExtractBlock(string text, string headerPattern) =>
        ExtractBlock(text, new Regex(headerPattern, RegexOptions.IgnoreCase));

    public static TerrainConfig Parse(string text)
    {
        text = StripComments(text);
        var warnings = new List<string>();

        // ---- Layers block: name -> texture/material -------------------------
        var layers = new Dictionary<string, (string? Texture, string? Material, string Name)>();
        var layersBody = ExtractBlock(text, @"class\s+Layers\s*");
        if (layersBody != null)
        {
            foreach (Match cm in ClassInLayers.Matches(layersBody))
            {
                string name = cm.Groups[1].Value;
                var sub = ExtractBlock(layersBody.Substring(cm.Index),
                    $@"class\s+{Regex.Escape(name)}\s*");
                string? tex = null, mat = null;
                if (sub != null)
                {
                    var tm = TextureRe.Match(sub);
                    var mm = MaterialRe.Match(sub);
                    tex = tm.Success ? tm.Groups[1].Value : null;
                    mat = mm.Success ? mm.Groups[1].Value : null;
                }
                layers[name.ToLowerInvariant()] = (tex, mat, name);
            }
        }
        else
        {
            warnings.Add("No 'class Layers' block found.");
        }

        // ---- Legend block: picture + name -> rgb ----------------------------
        string? picture = null;
        var surfaces = new List<Surface>();
        var seen = new HashSet<string>();

        var legendBody = ExtractBlock(text, @"class\s+Legend\s*");
        string searchScope = legendBody ?? text;
        if (legendBody == null)
            warnings.Add("No 'class Legend' block found; scanning whole file for colors.");

        var pm = PictureRe.Match(searchScope);
        if (pm.Success) picture = pm.Groups[1].Value;

        string colorsBody = ExtractBlock(searchScope, @"class\s+Colors\s*") ?? searchScope;

        static string Disp(string name, int i) => i == 0 ? name : $"{name} #{i + 1}";

        foreach (Match cm in EntryRe.Matches(colorsBody))
        {
            string name = cm.Groups[1].Value;
            string key = name.ToLowerInvariant();
            if (seen.Contains(key))
            {
                warnings.Add($"Duplicate color entry for {name}; keeping first.");
                continue;
            }
            seen.Add(key);
            layers.TryGetValue(key, out var lay);

            var triples = TripleRe.Matches(cm.Groups[2].Value);
            for (int i = 0; i < triples.Count; i++)
            {
                var t = triples[i];
                int r = int.Parse(t.Groups[1].Value);
                int g = int.Parse(t.Groups[2].Value);
                int b = int.Parse(t.Groups[3].Value);
                if (r > 255 || g > 255 || b > 255)
                    warnings.Add($"{name} has out-of-range color ({r}, {g}, {b})");
                surfaces.Add(new Surface
                {
                    Name = Disp(name, i),
                    Rgb = new Rgb((byte)r, (byte)g, (byte)b),
                    Texture = lay.Texture,
                    Material = lay.Material,
                });
            }
            if (triples.Count > 1)
            {
                var names = string.Join(", ",
                    Enumerable.Range(0, triples.Count).Select(i => Disp(name, i)));
                warnings.Add(
                    $"Surface '{name}' has {triples.Count} legend colors; added as {names}.");
            }
        }

        // Detect duplicate RGB values mapped to different surfaces (ambiguous!)
        var byRgb = new Dictionary<Rgb, List<string>>();
        foreach (var s in surfaces)
        {
            if (!byRgb.TryGetValue(s.Rgb, out var list))
                byRgb[s.Rgb] = list = new List<string>();
            list.Add(s.Name);
        }
        foreach (var (rgb, names) in byRgb)
        {
            if (names.Count > 1)
                warnings.Add($"Color {rgb} shared by surfaces: {string.Join(", ", names)}");
        }

        // Surfaces defined in Layers but with no legend colour
        foreach (var (key, lay) in layers)
        {
            if (!seen.Contains(key))
                warnings.Add($"Surface '{lay.Name}' has no legend color (not paintable).");
        }

        if (surfaces.Count == 0)
            warnings.Add("No colors parsed - check the file format.");

        return new TerrainConfig
        {
            Surfaces = surfaces,
            Picture = picture,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Derive a friendly terrain name from a cfg path,
    /// e.g. &lt;drive&gt;/MyTerrain/source/layers.cfg -> MyTerrain.
    /// </summary>
    public static string TerrainNameFromPath(string cfgPath)
    {
        try
        {
            var parent = Path.GetDirectoryName(cfgPath) ?? "";
            var base_ = Path.GetFileName(parent);
            if (base_.ToLowerInvariant() is "source" or "src" or "data" or "")
                base_ = Path.GetFileName(Path.GetDirectoryName(parent) ?? "");
            return string.IsNullOrEmpty(base_) ? "DayZ" : base_;
        }
        catch
        {
            return "DayZ";
        }
    }
}
