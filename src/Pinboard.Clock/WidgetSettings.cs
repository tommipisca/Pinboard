using System.IO;
using System.Text.Json;

namespace Pinboard.Clock;

/// <summary>Posizione del widget in pixel dello schermo (angolo in alto a sinistra).</summary>
internal sealed class WidgetSettings
{
    public int? X { get; set; }
    public int? Y { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Pinboard", "clock.json");

    public static WidgetSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch
        {
            // File rovinato: si riparte dalla posizione predefinita.
        }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Salvataggio non critico.
        }
    }
}
