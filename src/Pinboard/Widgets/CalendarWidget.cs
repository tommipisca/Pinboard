using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

public sealed class CalendarDefinition : WidgetDefinition
{
    public override string Type => "calendar";
    public override string Name => "Calendario";
    public override string Description =>
        "La data di oggi a colpo d'occhio: giorno della settimana, numero e mese. " +
        "La versione media mostra anche il mese intero.";
    public override string Category => "Calendario";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#FF3B30";
    public override IReadOnlyList<string> Features { get; } = new[] { "Mese intero nella versione media" };

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new CalendarView(style, size);
}

internal sealed class CalendarView : WidgetView
{
    private readonly bool _medium;
    private DateTime _shown;

    public CalendarView(WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _medium = size.Id == WidgetDefinition.Medium.Id;
        Tick();
    }

    public override void Tick()
    {
        DateTime today = DateTime.Today;
        if (today == _shown) return;
        _shown = today;
        Child = _medium ? BuildMedium(today) : BuildSmall(today);
    }

    private Grid BuildSmall(DateTime today)
    {
        var grid = new Grid { Margin = new Thickness(16, 14, 16, 14) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Add(grid, Weekday(today), 0);
        Add(grid, BigDay(today, 68), 1);
        Add(grid, Caption(today.ToString("MMMM yyyy", Fonts.Italian)), 2);
        return grid;
    }

    private Grid BuildMedium(DateTime today)
    {
        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition());
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Add(left, Weekday(today), 0);
        Add(left, BigDay(today, 64), 1);
        Add(left, Caption(today.ToString("MMMM yyyy", Fonts.Italian)), 2);

        var root = new Grid { Margin = new Thickness(16, 14, 14, 12) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(left);
        var month = MonthGrid(today);
        Grid.SetColumn(month, 1);
        root.Children.Add(month);
        return root;
    }

    /// <summary>Griglia del mese, settimana da lunedi'.</summary>
    private Grid MonthGrid(DateTime today)
    {
        var first = new DateTime(today.Year, today.Month, 1);
        int lead = ((int)first.DayOfWeek + 6) % 7;
        int days = DateTime.DaysInMonth(today.Year, today.Month);
        int weeks = (lead + days + 6) / 7;

        var grid = new Grid();
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        for (int r = 0; r < weeks; r++) grid.RowDefinitions.Add(new RowDefinition());

        string[] heads = { "L", "M", "M", "G", "V", "S", "D" };
        for (int c = 0; c < 7; c++)
        {
            var head = new TextBlock
            {
                Text = heads[c],
                FontFamily = Fonts.Text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = c == 6 ? Look.Accent : Look.Secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(head, c);
            grid.Children.Add(head);
        }

        for (int d = 1; d <= days; d++)
        {
            int index = lead + d - 1;
            bool isToday = d == today.Day;
            var cell = new Grid();
            if (isToday)
                cell.Children.Add(new Ellipse { Width = 21, Height = 21, Fill = Look.Accent });
            cell.Children.Add(new TextBlock
            {
                Text = d.ToString(Fonts.Italian),
                FontFamily = Fonts.Text,
                FontSize = 11.5,
                FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isToday ? Look.OnAccent : Look.Primary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetRow(cell, 1 + index / 7);
            Grid.SetColumn(cell, index % 7);
            grid.Children.Add(cell);
        }
        return grid;
    }

    private TextBlock Weekday(DateTime d) => new()
    {
        Text = d.ToString("dddd", Fonts.Italian).ToUpper(Fonts.Italian),
        FontFamily = Fonts.Text,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = Look.Accent,
    };

    private TextBlock BigDay(DateTime d, double size) => new()
    {
        Text = d.Day.ToString(Fonts.Italian),
        FontFamily = Fonts.Display,
        FontSize = size,
        FontWeight = FontWeights.Light,
        Foreground = Look.Primary,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private TextBlock Caption(string text) => new()
    {
        Text = text,
        FontFamily = Fonts.Text,
        FontSize = 14,
        Foreground = Look.Secondary,
    };

    private static void Add(Grid grid, UIElement e, int row)
    {
        Grid.SetRow(e, row);
        grid.Children.Add(e);
    }
}

internal static class Fonts
{
    public static readonly System.Globalization.CultureInfo Italian = new("it-IT");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Text = new("Segoe UI Variable Text, Segoe UI");
}
