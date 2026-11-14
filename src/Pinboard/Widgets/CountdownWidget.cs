using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pinboard;

/// <summary>Time math for the Countdown widget.</summary>
internal static class CountdownMath
{
    /// <summary>Time left until <paramref name="target"/>, split into days, hours and minutes.</summary>
    public static (int Days, int Hours, int Minutes, bool Past) Remaining(DateTime target, DateTime now)
    {
        TimeSpan left = target - now;
        bool past = left < TimeSpan.Zero;
        left = left.Duration();
        return ((int)left.TotalDays, left.Hours, left.Minutes, past);
    }

    /// <summary>Difference in calendar days: tomorrow at 1:00 is 1 day away even at 23:00 today.</summary>
    public static int CalendarDays(DateTime target, DateTime now) => (target.Date - now.Date).Days;

    /// <summary>"Today", "Tomorrow", "In 30 days", "1 day ago", "3 days ago".</summary>
    public static string Headline(DateTime target, DateTime now)
    {
        int days = CalendarDays(target, now);
        return days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            > 1 => $"In {days} days",
            -1 => "1 day ago",
            _ => $"{-days} days ago",
        };
    }
}

public sealed class CountdownDefinition : WidgetDefinition
{
    private static string G(int code) => char.ConvertFromUtf32(code);

    /// <summary>Icons to choose from (Segoe Fluent Icons), tinted with the accent color.</summary>
    internal static readonly (string Id, string Glyph, string Name)[] IconSet =
    {
        ("calendar", G(0xE787), "Calendar"), ("airplane", G(0xE709), "Airplane"), ("celebration", G(0xF133), "Celebration"),
        ("heart", G(0xEB51), "Heart"), ("star", G(0xE734), "Star"), ("education", G(0xE7BE), "Education"),
        ("work", G(0xE821), "Work"), ("home", G(0xE80F), "Home"),
    };

    public override string Type => "countdown";
    public override string Name => "Countdown";
    public override string Description =>
        "Counts down the days to a date that matters to you: a trip, a birthday, a deadline. " +
        "The medium size also shows hours and minutes.";
    public override string Category => "Productivity";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#FF2D55";
    public override IReadOnlyList<string> Features { get; } = new[] { "Title, date and time, icon" };

    protected override void ApplyDefaults(WidgetInstance w)
    {
        w.Options["title"] = "Vacation";
        w.Options["target"] = DateTime.Today.AddDays(30).ToString("s", CultureInfo.InvariantCulture);
        w.Options["icon"] = "calendar";
    }

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new CountdownView(w, style, size);

    public override string? Summary(WidgetInstance w) =>
        $"{Title(w)}, {CountdownMath.Headline(Target(w), DateTime.Now).ToLower(Fonts.Culture)}";

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed)
    {
        var title = UiKit.Input(Title(w), "Title", v =>
        {
            w.Options["title"] = v;
            changed();
        });
        var when = UiKit.DateTimePicker(Target(w), v =>
        {
            w.Options["target"] = v.ToString("s", CultureInfo.InvariantCulture);
            changed();
        });

        string group = "icon" + Guid.NewGuid().ToString("N");
        var icons = new WrapPanel();
        foreach (var (id, glyph, name) in IconSet)
        {
            var rb = new RadioButton
            {
                GroupName = group,
                IsChecked = id == IconId(w),
                Content = WidgetParts.Icon(glyph, 16, null!),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(0, 0, 4, 4),
                ToolTip = new ToolTip { Content = name },
            };
            ((TextBlock)rb.Content).ClearValue(TextBlock.ForegroundProperty);
            rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
            AutomationProperties.SetName(rb, name);
            rb.Checked += (_, _) =>
            {
                w.Options["icon"] = id;
                changed();
            };
            icons.Children.Add(rb);
        }

        return new StackPanel
        {
            Children =
            {
                UiKit.Field("Title", title),
                UiKit.Field("Date", when, alignTop: true),
                UiKit.Field("Icon", icons, alignTop: true),
            },
        };
    }

    internal static string Title(WidgetInstance w)
    {
        string t = w.Option("title").Trim();
        return t.Length == 0 ? "Countdown" : t;
    }

    internal static DateTime Target(WidgetInstance w) =>
        DateTime.TryParse(w.Option("target"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t)
            ? t
            : DateTime.Today.AddDays(30);

    internal static string IconId(WidgetInstance w) =>
        IconSet.Any(i => i.Id == w.Option("icon")) ? w.Option("icon") : "calendar";

    internal static string Glyph(WidgetInstance w) => IconSet.First(i => i.Id == IconId(w)).Glyph;
}

internal sealed class CountdownView : WidgetView
{
    private readonly WidgetInstance _w;
    private readonly bool _medium;
    private string _shown = "";

    public CountdownView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _w = w;
        _medium = size.Id == WidgetDefinition.Medium.Id;
        Tick();
    }

    public override void Tick()
    {
        DateTime now = DateTime.Now;
        string key = now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        if (key == _shown) return;
        _shown = key;
        Child = _medium ? BuildMedium(now) : BuildSmall(now);
    }

    private FrameworkElement Header()
    {
        var icon = WidgetParts.Icon(CountdownDefinition.Glyph(_w), 15, Look.Accent);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = new Thickness(0, 0, 8, 0);
        var title = WidgetParts.Label(CountdownDefinition.Title(_w), 13, Look.Primary, FontWeights.SemiBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(icon, Dock.Left);
        return new DockPanel { Children = { icon, title } };
    }

    private string DateLine(DateTime target) =>
        target.TimeOfDay == TimeSpan.Zero
            ? target.ToString("ddd, MMM d, yyyy", Fonts.Culture)
            : target.ToString("ddd, MMM d, HH:mm", Fonts.Culture);

    /// <summary>The big number and its unit for the small size.</summary>
    private (string Number, string Unit) Big(DateTime target, DateTime now)
    {
        int days = CountdownMath.CalendarDays(target, now);
        var r = CountdownMath.Remaining(target, now);
        if (days > 0) return (days.ToString(Fonts.Culture), days == 1 ? "day left" : "days left");
        if (days < 0) return ((-days).ToString(Fonts.Culture), days == -1 ? "day ago" : "days ago");
        if (r.Past || (r.Hours == 0 && r.Minutes == 0)) return ("Today", "");
        return r.Hours > 0
            ? (r.Hours.ToString(Fonts.Culture), r.Hours == 1 ? "hour left" : "hours left")
            : (r.Minutes.ToString(Fonts.Culture), r.Minutes == 1 ? "minute left" : "minutes left");
    }

    private FrameworkElement BuildSmall(DateTime now)
    {
        DateTime target = CountdownDefinition.Target(_w);
        var (number, unit) = Big(target, now);
        bool word = unit.Length == 0;

        var big = WidgetParts.Label(number, word ? 40 : number.Length > 3 ? 48 : 60, word ? Look.Accent : Look.Primary,
            FontWeights.Light, display: true);
        big.VerticalAlignment = VerticalAlignment.Center;
        var bottom = new StackPanel
        {
            Children =
            {
                WidgetParts.Label(unit, 13, Look.Primary),
                WidgetParts.Label(DateLine(target), 12, Look.Secondary),
            },
        };
        if (word) bottom.Children.RemoveAt(0);

        var grid = new Grid { Margin = new Thickness(16, 14, 16, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = Header();
        Grid.SetRow(big, 1);
        Grid.SetRow(bottom, 2);
        grid.Children.Add(header);
        grid.Children.Add(big);
        grid.Children.Add(bottom);
        return grid;
    }

    private FrameworkElement BuildMedium(DateTime now)
    {
        DateTime target = CountdownDefinition.Target(_w);
        var r = CountdownMath.Remaining(target, now);
        int days = CountdownMath.CalendarDays(target, now);

        var left = new Grid { Width = 140 };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition());
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var headline = WidgetParts.Label(CountdownMath.Headline(target, now), 13, Look.Accent, FontWeights.SemiBold);
        var date = WidgetParts.Label(DateLine(target), 12, Look.Secondary);
        date.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(headline, 1);
        headline.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetRow(date, 2);
        left.Children.Add(Header());
        left.Children.Add(headline);
        left.Children.Add(date);

        FrameworkElement right;
        if (r.Past || (days == 0 && r.Hours == 0 && r.Minutes == 0))
        {
            var word = WidgetParts.Label(days < 0 ? $"{-days}" : "Today", days < 0 ? 52 : 40,
                days < 0 ? Look.Primary : Look.Accent, FontWeights.Light, display: true);
            var unit = WidgetParts.Label(days == -1 ? "day ago" : days < 0 ? "days ago" : "", 13, Look.Secondary);
            right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { word, unit } };
        }
        else
        {
            var blocks = new UniformGrid { Columns = 3, VerticalAlignment = VerticalAlignment.Center };
            foreach (var (value, label) in new[] { (r.Days, "days"), (r.Hours, "hours"), (r.Minutes, "min") })
            {
                var n = WidgetParts.Label(value.ToString(Fonts.Culture), 44, Look.Primary, FontWeights.Light, display: true);
                n.HorizontalAlignment = HorizontalAlignment.Center;
                var l = WidgetParts.Label(label, 12, Look.Secondary);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                blocks.Children.Add(new StackPanel { Children = { n, l } });
            }
            right = blocks;
        }

        var grid = new Grid { Margin = new Thickness(18, 14, 16, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }
}
