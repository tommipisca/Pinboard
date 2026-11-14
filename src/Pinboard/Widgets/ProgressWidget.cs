using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pinboard;

/// <summary>How much of the day, week (Monday to Sunday), month or year has passed.</summary>
internal static class TimeProgress
{
    public static readonly string[] Periods = { "day", "week", "month", "year" };

    public static (DateTime Start, DateTime End) Bounds(string period, DateTime now)
    {
        DateTime day = now.Date;
        return period switch
        {
            "day" => (day, day.AddDays(1)),
            "week" => (day.AddDays(-(((int)day.DayOfWeek + 6) % 7)), day.AddDays(-(((int)day.DayOfWeek + 6) % 7) + 7)),
            "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            _ => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)),
        };
    }

    public static double Fraction(string period, DateTime now)
    {
        var (start, end) = Bounds(period, now);
        return (now - start).TotalSeconds / (end - start).TotalSeconds;
    }

    public static string Label(string period, DateTime now) => period switch
    {
        "day" => "Today",
        "week" => $"Week {ISOWeek.GetWeekOfYear(now)}",
        "month" => now.ToString("MMMM", Fonts.Culture),
        _ => now.Year.ToString(Fonts.Culture),
    };

    /// <summary>"9 h left", "1 day left", "47 days left".</summary>
    public static string Left(string period, DateTime now)
    {
        var (_, end) = Bounds(period, now);
        TimeSpan left = end - now;
        if (period == "day")
        {
            int hours = (int)Math.Ceiling(left.TotalHours);
            return hours <= 1 ? "Less than an hour left" : $"{hours} h left";
        }
        int days = (int)Math.Ceiling(left.TotalDays);
        return days == 1 ? "Last day" : $"{days} days left";
    }
}

public sealed class ProgressDefinition : WidgetDefinition
{
    private static readonly (string, string)[] PeriodNames =
        { ("day", "Day"), ("week", "Week"), ("month", "Month"), ("year", "Year") };

    public override string Type => "progress";
    public override string Name => "Time progress";
    public override string Description =>
        "How much of the day, the week, the month or the year has already gone by. The small size " +
        "shows one of them as a ring, the medium size all four as bars.";
    public override string Category => "Productivity";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#30D158";
    public override IReadOnlyList<string> Features { get; } = new[] { "Day, week, month or year in the small size" };

    protected override void ApplyDefaults(WidgetInstance w) => w.Options["period"] = "year";

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new ProgressView(w, style, size);

    public override string? Summary(WidgetInstance w) =>
        SizeOf(w).Id == Small.Id ? PeriodNames.First(p => p.Item1 == Period(w)).Item2.ToLower(Fonts.Culture) : null;

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed)
    {
        var periods = UiKit.Segmented(PeriodNames, Period(w), v =>
        {
            w.Options["period"] = v;
            changed();
        });
        var note = UiKit.Text("Used by the small size. The medium size shows all four.", "Text.Caption",
            new Thickness(0, 6, 0, 0));
        return UiKit.Field("Period", new StackPanel { Children = { periods, note } }, alignTop: true);
    }

    internal static string Period(WidgetInstance w)
    {
        string p = w.Option("period", "year");
        return TimeProgress.Periods.Contains(p) ? p : "year";
    }
}

internal sealed class ProgressView : WidgetView
{
    private readonly WidgetInstance _w;
    private readonly bool _medium;
    private string _shown = "";

    public ProgressView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _w = w;
        _medium = size.Id == WidgetDefinition.Medium.Id;
        Tick();
    }

    public override void Tick()
    {
        DateTime now = DateTime.Now;
        // Redraw only when a visible percentage changes (about once a minute for the day).
        string key = string.Join("|", TimeProgress.Periods.Select(p => Math.Floor(TimeProgress.Fraction(p, now) * 100)));
        if (key == _shown) return;
        _shown = key;
        Child = _medium ? BuildMedium(now) : BuildSmall(now);
    }

    private FrameworkElement BuildSmall(DateTime now)
    {
        string period = ProgressDefinition.Period(_w);
        double fraction = TimeProgress.Fraction(period, now);
        var percent = WidgetParts.Label($"{Math.Floor(fraction * 100)}%", 26, Look.Primary, FontWeights.Light, display: true);
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        percent.VerticalAlignment = VerticalAlignment.Center;
        var ring = WidgetParts.Ring(fraction, 92, 9, WidgetParts.Faint(Look.Accent), Look.Accent, percent);

        var title = WidgetParts.Label(TimeProgress.Label(period, now), 13, Look.Accent, FontWeights.SemiBold);
        var left = WidgetParts.Label(TimeProgress.Left(period, now), 12, Look.Secondary);
        left.HorizontalAlignment = HorizontalAlignment.Center;

        var grid = new Grid { Margin = new Thickness(16, 14, 16, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(ring, 1);
        Grid.SetRow(left, 2);
        grid.Children.Add(title);
        grid.Children.Add(ring);
        grid.Children.Add(left);
        return grid;
    }

    private FrameworkElement BuildMedium(DateTime now)
    {
        var rows = new Grid { Margin = new Thickness(20, 16, 20, 16) };
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        rows.ColumnDefinitions.Add(new ColumnDefinition());
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        for (int i = 0; i < TimeProgress.Periods.Length; i++)
        {
            string period = TimeProgress.Periods[i];
            double fraction = TimeProgress.Fraction(period, now);
            rows.RowDefinitions.Add(new RowDefinition());

            var name = WidgetParts.Label(TimeProgress.Label(period, now), 13, Look.Primary, FontWeights.SemiBold);
            name.VerticalAlignment = VerticalAlignment.Center;
            var bar = WidgetParts.Bar(fraction, 8, WidgetParts.Faint(Look.Accent), Look.Accent);
            bar.VerticalAlignment = VerticalAlignment.Center;
            var value = WidgetParts.Label($"{Math.Floor(fraction * 100)}%", 13, Look.Secondary);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;

            foreach (var (e, col) in new (FrameworkElement, int)[] { (name, 0), (bar, 1), (value, 2) })
            {
                Grid.SetRow(e, i);
                Grid.SetColumn(e, col);
                rows.Children.Add(e);
            }
        }
        return rows;
    }
}
