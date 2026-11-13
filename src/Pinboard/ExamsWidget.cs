using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>
/// Widget medio: a sinistra il prossimo esame in programma con i giorni che mancano,
/// a destra i task di oggi, di qualsiasi esame.
/// </summary>
public sealed class ExamsWidget : WidgetWindow
{
    private const int MaxTaskRows = 5;
    private const string CurrentDegree = "triennale";

    private static readonly CultureInfo Italian = new("it-IT");
    private static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily Text = new("Segoe UI Variable Text, Segoe UI");

    private readonly StackPanel _left = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _right = new() { VerticalAlignment = VerticalAlignment.Top };
    private string _stamp = "";

    public ExamsWidget() : base("exams", 360, 170)
    {
        var grid = new Grid { Margin = new Thickness(16, 14, 16, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var divider = new Border
        {
            Width = 1,
            Background = Theme.CardBorder,
            Margin = new Thickness(0, 2, 0, 2),
        };
        _right.Margin = new Thickness(14, 0, 0, 0);

        Grid.SetColumn(_left, 0);
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(_right, 2);
        grid.Children.Add(_left);
        grid.Children.Add(divider);
        grid.Children.Add(_right);

        Content = CreateCard(grid);
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
        _left.Children.Add(Header("PROSSIMO ESAME"));

        if (exam == null)
        {
            _left.Children.Add(Tb("Nessun esame in programma", 15, Theme.Primary, FontWeights.SemiBold, wrap: true));
            return;
        }

        var name = Tb(exam.Name, 15, Theme.Primary, FontWeights.SemiBold, wrap: true);
        name.MaxHeight = 40;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        _left.Children.Add(name);

        int days = (exam.Date - today).Days;
        var big = new TextBlock { Margin = new Thickness(0, 2, 0, 0) };
        if (days == 0)
            big.Inlines.Add(BigRun("Oggi"));
        else if (days == 1)
            big.Inlines.Add(BigRun("Domani"));
        else
        {
            big.Inlines.Add(BigRun(days.ToString(Italian)));
            big.Inlines.Add(new Run(" giorni")
            {
                FontFamily = Text,
                FontSize = 14,
                Foreground = Theme.Secondary,
            });
        }
        _left.Children.Add(big);

        string when = exam.Date.ToString("ddd d MMM", Italian);
        if (exam.StartMinutes is int m)
            when += " · " + TimeSpan.FromMinutes(m).ToString(@"hh\:mm");
        _left.Children.Add(Tb(when, 12, Theme.Secondary));
    }

    private static Run BigRun(string text) => new(text)
    {
        FontFamily = Display,
        FontSize = 38,
        FontWeight = FontWeights.Light,
        Foreground = Theme.Primary,
    };

    // --- Task di oggi -----------------------------------------------------------------------

    private void BuildRight(List<TaskItem> tasks)
    {
        _right.Children.Clear();
        _right.Children.Add(Header("OGGI"));

        if (tasks.Count == 0)
        {
            _right.Children.Add(Tb("Nessuna attivita' per oggi", 13, Theme.Secondary, wrap: true));
            return;
        }

        // Se non stanno tutti, l'ultima riga diventa "+N altri".
        int shown = tasks.Count > MaxTaskRows ? MaxTaskRows - 1 : tasks.Count;
        foreach (TaskItem t in tasks.Take(shown))
            _right.Children.Add(TaskRow(t));

        if (tasks.Count > shown)
            _right.Children.Add(Tb($"+{tasks.Count - shown} altri", 12, Theme.Secondary));
    }

    private static UIElement TaskRow(TaskItem task)
    {
        Brush color = Theme.Accent(task.ColorIndex);

        var bullet = new Ellipse
        {
            Width = 11,
            Height = 11,
            Stroke = color,
            StrokeThickness = 1.6,
            Fill = task.IsDone ? color : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var title = Tb(task.Title, 13, task.IsDone ? Theme.Secondary : Theme.Primary);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        if (task.IsDone) title.TextDecorations = TextDecorations.Strikethrough;

        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5), LastChildFill = true };
        DockPanel.SetDock(bullet, Dock.Left);
        row.Children.Add(bullet);
        row.Children.Add(title);
        return row;
    }

    // --- Elementi comuni --------------------------------------------------------------------

    private static TextBlock Header(string text)
    {
        var label = Tb(text, 11, Theme.Red, FontWeights.SemiBold);
        label.Margin = new Thickness(0, 0, 0, 5);
        return label;
    }

    private static TextBlock Tb(string text, double size, Brush color, FontWeight? weight = null, bool wrap = false) => new()
    {
        Text = text,
        FontFamily = Text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = color,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };
}
