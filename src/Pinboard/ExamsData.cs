using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Pinboard;

internal sealed record TaskItem(string Title, DateTime Due, bool IsDone, string ExamName, int ColorIndex);

internal sealed record ExamItem(
    string Id, string Name, DateTime Date, int? StartMinutes, string Degree,
    bool IsScheduled, bool Passed, bool HasGrade, int ColorIndex, List<TaskItem> Tasks);

/// <summary>Legge gli esami e i task salvati da ExamsTrack in %APPDATA%\ExamsTrack\data (sola lettura).</summary>
internal static class ExamsData
{
    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExamsTrack", "data");

    private static string ExamsFile => Path.Combine(DataDir, "exams.json");
    private static string DeletionsFile => Path.Combine(DataDir, "deletions.json");

    /// <summary>Cambia quando ExamsTrack modifica i file: serve a sapere quando rileggere.</summary>
    public static string Stamp() =>
        $"{File.GetLastWriteTimeUtc(ExamsFile).Ticks}/{File.GetLastWriteTimeUtc(DeletionsFile).Ticks}";

    public static List<ExamItem> Load()
    {
        var result = new List<ExamItem>();
        try
        {
            if (!File.Exists(ExamsFile)) return result;

            HashSet<string> deleted = ReadDeletedIds();

            using var stream = new FileStream(ExamsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (JsonElement e in doc.RootElement.EnumerateArray())
            {
                string id = Str(e, "id");
                if (deleted.Contains(id)) continue;

                string name = Str(e, "name");
                int color = Int(e, "colorIndex") ?? 0;

                var tasks = new List<TaskItem>();
                if (e.TryGetProperty("tasks", out JsonElement taskArray) && taskArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement t in taskArray.EnumerateArray())
                    {
                        string due = Str(t, "dueDate");
                        if (due.Length == 0) continue;
                        tasks.Add(new TaskItem(Str(t, "title"), ParseLocal(due), Bool(t, "isDone"), name, color));
                    }
                }

                string date = Str(e, "date");
                if (date.Length == 0) continue;

                result.Add(new ExamItem(
                    id, name, ParseLocal(date).Date, Int(e, "startTime"), Str(e, "degree"),
                    Bool(e, "isScheduled"), Bool(e, "passed"),
                    e.TryGetProperty("grade", out JsonElement g) && g.ValueKind == JsonValueKind.Number,
                    color, tasks));
            }
        }
        catch (Exception ex)
        {
            // File in scrittura o formato diverso: il widget resta con quello che ha.
            App.Log("ExamsData: " + ex.Message, force: true);
        }
        return result;
    }

    private static HashSet<string> ReadDeletedIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(DeletionsFile)) return ids;
            using var stream = new FileStream(DeletionsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty p in doc.RootElement.EnumerateObject()) ids.Add(p.Name);
        }
        catch
        {
            // Senza l'elenco delle eliminazioni si mostrano tutti gli esami.
        }
        return ids;
    }

    private static DateTime ParseLocal(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).LocalDateTime;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i)
            ? i : null;
}
