using System.IO;
using System.Text.Json;

namespace Pinboard;

/// <summary>Posizioni dei widget in pixel dello schermo (angolo in alto a sinistra): id -> [x, y].</summary>
internal sealed class WidgetSettings
{
    public Dictionary<string, int[]> Positions { get; set; } = new();

    /// <summary>Corso mostrato dal widget Exams: "triennale", "magistrale" o vuoto per entrambi (come ExamsTrack).</summary>
    public string? ExamsDegree { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Pinboard", "widgets.json");

    public static WidgetSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch
        {
            // File rovinato: si riparte dalle posizioni predefinite.
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
