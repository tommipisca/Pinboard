using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>
/// Widget medio nello stile di ExamsTrack ("inchiostro e evidenziatore"): a sinistra il prossimo
/// esame in programma con il countdown, a destra i task di oggi, di qualsiasi esame.
/// Colori, caratteri e countdown seguono l'app (desktop/src/renderer/styles.css).
/// </summary>
public sealed class ExamsWidget : WidgetWindow
{
    private const int MaxTaskRows = 3;
    private const string CurrentDegree = "triennale";

    private static readonly CultureInfo Italian = new("it-IT");

    // Tema dell'app: sempre scuro, indipendente dal tema di Windows.
    private static readonly Brush Background = Theme.Make("#F20F1728");  // inchiostro
    private static readonly Brush Stroke = Theme.Make("#26324A");        // filetti
    private static readonly Brush TextPrimary = Theme.Make("#ECE8DD");   // carta
    private static readonly Brush TextSecondary = Theme.Make("#93A0B8"); // matita
    private static readonly Brush Success = Theme.Make("#5BD6A0");
    private static readonly Brush Warning = Theme.Make("#F6B94A");
    private static readonly Brush Danger = Theme.Make("#FF7D8E");
    private static readonly Brush Ink = Theme.Make("#0F1728");

    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily SerifFont = new("Sitka Heading, Sitka Text, Georgia");
    private static readonly FontFamily CountdownFont = new(
        "Bahnschrift SemiBold Condensed, Bahnschrift Condensed, Bahnschrift, Segoe UI Variable Display");

    private readonly StackPanel _left = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _right = new() { VerticalAlignment = VerticalAlignment.Top };
    private string _stamp = "";

    public ExamsWidget() : base("exams", 360, 170)
    {
        var grid = new Grid { Margin = new Thickness(16, 14, 16, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var divider = new Border { Width = 1, Background = Stroke, Margin = new Thickness(0, 2, 0, 2) };
        _right.Margin = new Thickness(14, 0, 0, 0);

        Grid.SetColumn(_left, 0);
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(_right, 2);
        grid.Children.Add(_left);
        grid.Children.Add(divider);
        grid.Children.Add(_right);

        Content = CreateCard(grid, Background, Stroke, radius: 20);
        OnTick();
    }

    public override void OnTick()
    {
        // Si ricostruisce solo se ExamsTrack ha salvato qualcosa o e' cambiato il giorno.
        string stamp = ExamsData.Stamp() + "|" + DateTime.Today.Ticks;
        if (stamp == _stamp) return;
        _stamp = stamp;
        Refresh();
    }

    private void Refresh()
    {
        List<ExamItem> exams = ExamsData.Load();
        DateTime today = DateTime.Today;

        ExamItem? next = exams
            .Where(e => e.IsScheduled && e.Degree == CurrentDegree && !e.Passed && !e.HasGrade && e.Date >= today)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartMinutes ?? int.MaxValue)
            .FirstOrDefault();

        List<TaskItem> tasks = exams
            .SelectMany(e => e.Tasks)
            .Where(t => t.Due.Date == today)
            .OrderBy(t => t.IsDone)
            .ThenBy(t => t.ExamName)
            .ToList();

        BuildLeft(next, today);
        BuildRight(tasks);
    }

    // --- Esame ------------------------------------------------------------------------------

    private void BuildLeft(ExamItem? exam, DateTime today)
    {
        _left.Children.Clear();
        _left.Children.Add(SectionHeader("Prossimo esame"));

        if (exam == null)
        {
            _left.Children.Add(Tb("Nessun esame in programma", 16, TextPrimary, SerifFont, FontWeights.SemiBold, wrap: true));
            return;
        }

        var name = Tb(exam.Name, 15, TextPrimary, SerifFont, FontWeights.SemiBold, wrap: true);
        name.MaxHeight = 40;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        _left.Children.Add(name);

        // Il countdown e' l'unico elemento che alza la voce: colore dell'esame, poi ambra a 7 giorni
        // e corallo a 2.
        int days = (exam.Date - today).Days;
        Brush color = days <= 2 ? Danger : days <= 7 ? Warning : Theme.Accent(exam.ColorIndex);

        var big = new TextBlock { Margin = new Thickness(0, 1, 0, 0) };
        if (days == 0)
            big.Inlines.Add(CountdownRun("Oggi", 40, color));
        else if (days == 1)
            big.Inlines.Add(CountdownRun("Domani", 40, color));
        else
        {
            big.Inlines.Add(CountdownRun(days.ToString(Italian), 52, color));
            big.Inlines.Add(new Run(" giorni")
            {
                FontFamily = UiFont,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = color,
                BaselineAlignment = BaselineAlignment.Baseline,
            });
        }
        big.Opacity = 1;
        _left.Children.Add(big);

        string when = exam.Date.ToString("ddd d MMM", Italian);
        if (exam.StartMinutes is int m)
            when += " · " + TimeSpan.FromMinutes(m).ToString(@"hh\:mm");
        _left.Children.Add(Tb(when, 12, TextSecondary, UiFont));

        if (exam.Tasks.Count > 0)
        {
            int done = exam.Tasks.Count(t => t.IsDone);
            _left.Children.Add(Tb($"{done} di {exam.Tasks.Count} task", 11, TextSecondary, UiFont));
        }
    }

    private static Run CountdownRun(string text, double size, Brush color) => new(text)
    {
        FontFamily = CountdownFont,
        FontSize = size,
        FontWeight = FontWeights.Bold,
        Foreground = color,
    };

    // --- Task di oggi -----------------------------------------------------------------------

    private void BuildRight(List<TaskItem> tasks)
    {
        _right.Children.Clear();
        _right.Children.Add(SectionHeader("Oggi"));

        if (tasks.Count == 0)
        {
            _right.Children.Add(Tb("Nessun task per oggi", 13, TextSecondary, UiFont, wrap: true));
            return;
        }

        // Se non stanno tutti, l'ultima riga diventa "+N altri".
        int shown = tasks.Count > MaxTaskRows ? MaxTaskRows - 1 : tasks.Count;
        foreach (TaskItem t in tasks.Take(shown))
            _right.Children.Add(TaskRow(t));

        if (tasks.Count > shown)
            _right.Children.Add(Tb($"+{tasks.Count - shown} altri", 12, TextSecondary, UiFont));
    }

    private static UIElement TaskRow(TaskItem task)
    {
        Brush examColor = Theme.Accent(task.ColorIndex);

        var title = Tb(task.Title, 13, task.IsDone ? TextSecondary : TextPrimary, UiFont);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        if (task.IsDone) title.TextDecorations = TextDecorations.Strikethrough;

        var exam = Tb(task.ExamName, 11, examColor, UiFont, FontWeights.SemiBold);
        exam.TextTrimming = TextTrimming.CharacterEllipsis;

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(exam);

        var check = CheckCircle(task.IsDone, examColor);
        check.VerticalAlignment = VerticalAlignment.Center;
        check.Margin = new Thickness(0, 0, 8, 0);

        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };
        DockPanel.SetDock(check, Dock.Left);
        row.Children.Add(check);
        row.Children.Add(text);
        return row;
    }

    /// <summary>Cerchio del task come nell'app: vuoto con bordo colorato, o pieno con la spunta.</summary>
    private static FrameworkElement CheckCircle(bool isDone, Brush color)
    {
        var box = new Grid { Width = 18, Height = 18 };
        if (isDone)
        {
            box.Children.Add(new Ellipse { Fill = color });
            box.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 5.6,9.6 L 7.9,11.9 L 12.4,7"),
                Stroke = Ink,
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            });
        }
        else
        {
            box.Children.Add(new Ellipse { Stroke = color, StrokeThickness = 1.6, Margin = new Thickness(0.8) });
        }
        return box;
    }

    // --- Elementi comuni --------------------------------------------------------------------

    /// <summary>Intestazione di sezione: sentence case, semibold, color matita.</summary>
    private static TextBlock SectionHeader(string text)
    {
        var header = Tb(text, 12, TextSecondary, UiFont, FontWeights.SemiBold);
        header.Margin = new Thickness(0, 0, 0, 5);
        return header;
    }

    private static TextBlock Tb(string text, double size, Brush color, FontFamily font,
        FontWeight? weight = null, bool wrap = false) => new()
    {
        Text = text,
        FontFamily = font,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = color,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };
}
