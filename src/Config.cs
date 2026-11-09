using System.Text;
using System.Text.Json;

namespace CustomFancyZone;

/// <summary>Une disposition telle qu'écrite dans layouts.json.</summary>
internal sealed class LayoutDef
{
    public string? Name { get; set; }

    /// <summary>Dessin en lettres : chaque lettre est une zone, chaque caractère une case de la grille.</summary>
    public string[]? Grid { get; set; }

    /// <summary>Zones en pourcentage de l'écran : [x, y, largeur, hauteur].</summary>
    public double[][]? Zones { get; set; }

    /// <summary>Zoom (en %) à appliquer à la fenêtre placée dans chaque zone, dans l'ordre des zones (null = ne pas toucher).</summary>
    public int?[]? Zoom { get; set; }
}

/// <summary>
/// Application qui dessine son bouton Agrandir sans le déclarer à Windows (Unreal Engine…) :
/// position du bouton, en pixels à 100 %, depuis le coin haut-droit de la fenêtre.
/// </summary>
internal sealed class CustomButton
{
    public string WindowClass { get; set; } = "";
    public int Right { get; set; }
    public int Top { get; set; }
    public int Width { get; set; } = 42;
    public int Height { get; set; } = 30;
}

/// <summary>Contenu de layouts.json.</summary>
internal sealed class AppConfig
{
    public int HoverDelayMs { get; set; } = 400;
    public int HideDelayMs { get; set; } = 350;
    public int Columns { get; set; } = 4;
    public int TileWidth { get; set; } = 96;
    public int ZoneGap { get; set; }
    public bool ShowPreview { get; set; } = true;
    public bool ShowNames { get; set; } = true;
    public bool SnapAssist { get; set; } = true;
    public bool ShowZoomBar { get; set; } = true;
    public List<CustomButton> CustomButtons { get; set; } =
        [new() { WindowClass = "UnrealWindow", Right = 42, Top = 0, Width = 42, Height = 30 }];
    public bool DebugLog { get; set; }
    public List<LayoutDef> Layouts { get; set; } = [];
}

/// <summary>
/// Disposition prête à l'emploi : zones en fractions (0..1) de la zone de travail de l'écran,
/// et zoom éventuel (en %) pour la fenêtre placée dans chaque zone.
/// </summary>
internal sealed record SnapLayout(string Name, IReadOnlyList<RectangleF> Zones, IReadOnlyList<int?> Zoom)
{
    public bool CoversTile => Zones.Sum(z => z.Width * z.Height) >= 0.995f;
}

internal sealed class ConfigStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string FilePath { get; } = filePath;
    public AppConfig Config { get; private set; } = new();
    public IReadOnlyList<SnapLayout> Layouts { get; private set; } = [];

    public static string DefaultJson()
    {
        using var stream = typeof(ConfigStore).Assembly.GetManifestResourceStream("layouts.default.json")
            ?? throw new InvalidOperationException("Ressource layouts.default.json absente.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void EnsureFile()
    {
        if (!File.Exists(FilePath))
            File.WriteAllText(FilePath, DefaultJson(), new UTF8Encoding(false));
    }

    /// <summary>
    /// Relit le fichier. Renvoie la liste des problèmes trouvés (vide si tout va bien).
    /// Si le JSON est illisible, la configuration précédente est conservée.
    /// </summary>
    public List<string> Load()
    {
        var problems = new List<string>();
        string? text = null;
        for (int attempt = 0; attempt < 5 && text is null; attempt++)
        {
            try { text = File.ReadAllText(FilePath); }
            catch (IOException) when (attempt < 4) { Thread.Sleep(60); }
            catch (Exception ex) { problems.Add($"Lecture impossible : {ex.Message}"); break; }
        }

        if (text is null || !TryApply(text, problems))
        {
            if (Layouts.Count == 0)
            {
                problems.Add("Dispositions par défaut utilisées.");
                TryApply(DefaultJson(), []);
            }
        }
        return problems;
    }

    private bool TryApply(string json, List<string> problems)
    {
        AppConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            problems.Add($"JSON invalide : {ex.Message}");
            return false;
        }
        if (cfg is null)
        {
            problems.Add("Fichier vide.");
            return false;
        }

        var layouts = new List<SnapLayout>();
        for (int i = 0; i < cfg.Layouts.Count; i++)
        {
            var layout = Parse(cfg.Layouts[i], i, problems);
            if (layout is not null) layouts.Add(layout);
        }
        if (layouts.Count == 0)
        {
            problems.Add("Aucune disposition valide.");
            return false;
        }

        Config = cfg;
        Layouts = layouts;
        return true;
    }

    public static SnapLayout? Parse(LayoutDef def, int index, List<string> problems)
    {
        var name = string.IsNullOrWhiteSpace(def.Name) ? $"Disposition {index + 1}" : def.Name.Trim();
        var zones = new List<RectangleF>();

        if (def.Grid is { Length: > 0 } grid)
        {
            int rows = grid.Length, cols = grid[0].Length;
            if (cols == 0 || grid.Any(r => r is null || r.Length != cols))
            {
                problems.Add($"« {name} » : toutes les lignes de \"grid\" doivent avoir la même longueur.");
                return null;
            }

            var order = new List<char>();
            var boxes = new Dictionary<char, (int L, int T, int R, int B, int Count)>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    char ch = grid[r][c];
                    if (ch is '.' or ' ') continue;
                    if (!boxes.TryGetValue(ch, out var b))
                    {
                        order.Add(ch);
                        b = (c, r, c, r, 0);
                    }
                    boxes[ch] = (Math.Min(b.L, c), Math.Min(b.T, r), Math.Max(b.R, c), Math.Max(b.B, r), b.Count + 1);
                }
            }

            foreach (var ch in order)
            {
                var b = boxes[ch];
                if ((b.R - b.L + 1) * (b.B - b.T + 1) != b.Count)
                    problems.Add($"« {name} » : la zone '{ch}' n'est pas un rectangle, son contour englobant est utilisé.");
                zones.Add(RectangleF.FromLTRB(
                    (float)b.L / cols, (float)b.T / rows, (float)(b.R + 1) / cols, (float)(b.B + 1) / rows));
            }
        }
        else if (def.Zones is { Length: > 0 })
        {
            foreach (var z in def.Zones)
            {
                if (z is not { Length: 4 })
                {
                    problems.Add($"« {name} » : chaque zone doit être [x, y, largeur, hauteur] en %.");
                    continue;
                }
                double x = Math.Clamp(z[0], 0, 100), y = Math.Clamp(z[1], 0, 100);
                double w = Math.Clamp(z[2], 0, 100 - x), h = Math.Clamp(z[3], 0, 100 - y);
                if (w < 1 || h < 1)
                {
                    problems.Add($"« {name} » : zone trop petite ou hors de l'écran ignorée.");
                    continue;
                }
                zones.Add(new RectangleF((float)(x / 100), (float)(y / 100), (float)(w / 100), (float)(h / 100)));
            }
        }
        else
        {
            problems.Add($"« {name} » : il faut \"grid\" ou \"zones\".");
        }

        if (zones.Count == 0) return null;

        var zoom = new int?[zones.Count];
        if (def.Zoom is { Length: > 0 })
        {
            if (def.Zoom.Length != zones.Count)
                problems.Add($"« {name} » : \"zoom\" a {def.Zoom.Length} valeurs pour {zones.Count} zones.");
            for (int i = 0; i < Math.Min(def.Zoom.Length, zones.Count); i++)
            {
                if (def.Zoom[i] is not int z) continue;
                if (z is < 25 or > 500)
                {
                    problems.Add($"« {name} » : zoom {z} % hors limites (25 à 500), ignoré.");
                    continue;
                }
                zoom[i] = z;
            }
        }
        return new SnapLayout(name, zones, zoom);
    }
}
