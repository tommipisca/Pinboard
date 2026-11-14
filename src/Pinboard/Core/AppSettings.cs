using System.IO;
using System.Text.Json;

namespace Pinboard;

/// <summary>Un widget messo sul desktop, con le sue personalizzazioni.</summary>
public sealed class WidgetInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

    /// <summary>Tipo di widget nel catalogo (es. "clock").</summary>
    public string Type { get; set; } = "";

    /// <summary>Id della taglia (es. "small", "medium").</summary>
    public string Size { get; set; } = "small";

    /// <summary>"system", "light" o "dark".</summary>
    public string Theme { get; set; } = "system";

    /// <summary>Colore di sfondo "#RRGGBB"; null = quello del tema.</summary>
    public string? Background { get; set; }

    /// <summary>Colore d'accento "#RRGGBB"; null = quello predefinito del widget.</summary>
    public string? Accent { get; set; }

    /// <summary>Opacita' dello sfondo, da 0.2 a 1.</summary>
    public double Opacity { get; set; } = 0.92;

    /// <summary>Opzioni proprie del tipo di widget.</summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>Angolo in alto a sinistra in pixel dello schermo; null = ancora da posizionare.</summary>
    public int[]? Position { get; set; }

    public string Option(string key, string fallback = "") =>
        Options.TryGetValue(key, out string? v) ? v : fallback;

    public WidgetInstance Clone() =>
        JsonSerializer.Deserialize<WidgetInstance>(JsonSerializer.Serialize(this))!;
}

/// <summary>Tutto quello che Pinboard ricorda: widget sul desktop e preferiti.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public List<WidgetInstance> Widgets { get; set; } = new();
    public List<string> Favorites { get; set; } = new();
    public bool CloseHintShown { get; set; }

    /// <summary>Controlla da solo se ci sono aggiornamenti (all'avvio e ogni 6 ore).</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>%APPDATA%\Pinboard, oppure la cartella in PINBOARD_HOME (per le prove).</summary>
    internal static string Folder =>
        Environment.GetEnvironmentVariable("PINBOARD_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pinboard");

    private static string FilePath => Path.Combine(Folder, "settings.json");
    private static string LegacyPath => Path.Combine(Folder, "widgets.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            if (File.Exists(LegacyPath))
                return MigrateLegacy();
        }
        catch (Exception ex)
        {
            App.Log("Impostazioni illeggibili, si riparte da zero: " + ex.Message, force: true);
        }
        return new();
    }

    /// <summary>
    /// Le versioni precedenti avevano orologio e calendario sempre attivi e salvavano solo le
    /// posizioni ("clock": [x, y]). Diventano due widget normali, nello stesso punto.
    /// </summary>
    private static AppSettings MigrateLegacy()
    {
        var settings = new AppSettings();
        using var doc = JsonDocument.Parse(File.ReadAllText(LegacyPath));
        var positions = doc.RootElement.TryGetProperty("Positions", out var p) ? p : default;

        foreach (string type in new[] { "clock", "calendar" })
        {
            var widget = new WidgetInstance { Type = type };
            if (positions.ValueKind == JsonValueKind.Object &&
                positions.TryGetProperty(type, out var xy) && xy.GetArrayLength() == 2)
                widget.Position = new[] { xy[0].GetInt32(), xy[1].GetInt32() };
            settings.Widgets.Add(widget);
        }
        settings.Save();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            App.Log("Salvataggio non riuscito: " + ex.Message, force: true);
        }
    }
}
