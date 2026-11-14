using System.IO;
using System.Text.Json;

namespace Pinboard;

/// <summary>A widget placed on the desktop, with its customizations.</summary>
public sealed class WidgetInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

    /// <summary>Widget type in the catalog (e.g. "clock").</summary>
    public string Type { get; set; } = "";

    /// <summary>Size id (e.g. "small", "medium").</summary>
    public string Size { get; set; } = "small";

    /// <summary>"system", "light" or "dark".</summary>
    public string Theme { get; set; } = "system";

    /// <summary>Background color "#RRGGBB"; null = the theme's one.</summary>
    public string? Background { get; set; }

    /// <summary>Accent color "#RRGGBB"; null = the widget's default.</summary>
    public string? Accent { get; set; }

    /// <summary>Background opacity, from 0.2 to 1.</summary>
    public double Opacity { get; set; } = 0.92;

    /// <summary>Options specific to the widget type.</summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>Top-left corner in screen pixels; null = not placed yet.</summary>
    public int[]? Position { get; set; }

    public string Option(string key, string fallback = "") =>
        Options.TryGetValue(key, out string? v) ? v : fallback;

    public WidgetInstance Clone() =>
        JsonSerializer.Deserialize<WidgetInstance>(JsonSerializer.Serialize(this))!;
}

/// <summary>Everything Pinboard remembers: widgets on the desktop and favorites.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public List<WidgetInstance> Widgets { get; set; } = new();
    public List<string> Favorites { get; set; } = new();
    public bool CloseHintShown { get; set; }

    /// <summary>Check for updates automatically (at startup and every 6 hours).</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>%APPDATA%\Pinboard, or the folder in PINBOARD_HOME (for testing).</summary>
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
            App.Log("Unreadable settings, starting from scratch: " + ex.Message, force: true);
        }
        return new();
    }

    /// <summary>
    /// Earlier versions always showed the clock and the calendar and only saved their
    /// positions ("clock": [x, y]). They become two regular widgets, in the same place.
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
            App.Log("Could not save: " + ex.Message, force: true);
        }
    }
}
