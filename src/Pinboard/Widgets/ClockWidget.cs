using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

public sealed class ClockDefinition : WidgetDefinition
{
    public override string Type => "clock";
    public override string Name => "Clock";
    public override string Description =>
        "An analog clock with a second hand. It can follow your computer's time or another " +
        "city's; the medium size adds the time in digits and the date.";
    public override string Category => "Clocks";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#FF9F0A";
    public override IReadOnlyList<string> Features { get; } = new[]
    {
        "Your computer's time or another city's",
        "Second hand shown or hidden",
    };

    protected override void ApplyDefaults(WidgetInstance w) => w.Options["seconds"] = "true";

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new ClockView(w, style, size);

    public override string? Summary(WidgetInstance w)
    {
        string where = ClockZone.Resolve(w).City ?? "computer time";
        return w.Option("seconds", "true") == "true" ? where : where + ", no second hand";
    }

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed) =>
        new ClockOptionsEditor(w, changed);
}

/// <summary>The time zone chosen for a clock.</summary>
internal static class ClockZone
{
    private static readonly Dictionary<string, TimeZoneInfo?> Cache = new();

    public static (TimeZoneInfo? Zone, string? City) Resolve(WidgetInstance w)
    {
        string id = w.Option("zone");
        if (id.Length == 0) return (null, null);
        TimeZoneInfo? zone = Find(id);
        return zone == null ? (null, null) : (zone, w.Option("city", zone.StandardName));
    }

    public static TimeZoneInfo? Find(string id)
    {
        if (Cache.TryGetValue(id, out var cached)) return cached;
        TimeZoneInfo? zone = null;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out string? win))
            {
                try { zone = TimeZoneInfo.FindSystemTimeZoneById(win); } catch { }
            }
        }
        Cache[id] = zone;
        return zone;
    }

    public static DateTime Now(TimeZoneInfo? zone) =>
        zone == null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);

    /// <summary>"+8 hours", "−1 hour", "+5 hours 30 min", "same time".</summary>
    public static string Difference(TimeZoneInfo zone)
    {
        DateTime utc = DateTime.UtcNow;
        TimeSpan diff = zone.GetUtcOffset(utc) - TimeZoneInfo.Local.GetUtcOffset(utc);
        if (diff == TimeSpan.Zero) return "same time";
        string sign = diff < TimeSpan.Zero ? "−" : "+";
        diff = diff.Duration();
        string hours = diff.Hours == 1 ? "1 hour" : $"{diff.Hours} hours";
        if (diff.Minutes == 0) return sign + hours;
        return diff.Hours == 0 ? $"{sign}{diff.Minutes} min" : $"{sign}{hours} {diff.Minutes} min";
    }
}

/// <summary>Analog dial, 170 x 170.</summary>
internal sealed class ClockDial : Canvas
{
    private const double Size = 170;
    private const double C = Size / 2;

    private readonly RotateTransform _hour = new() { CenterX = C, CenterY = C };
    private readonly RotateTransform _minute = new() { CenterX = C, CenterY = C };
    private readonly RotateTransform? _second;

    public ClockDial(WidgetStyle s, bool seconds, string? label)
    {
        Width = Size;
        Height = Size;

        for (int i = 0; i < 60; i++)
        {
            bool hourMark = i % 5 == 0;
            double outer = 70;
            double inner = outer - (hourMark ? 9 : 4);
            double angle = i * 6 * Math.PI / 180;
            Children.Add(new Line
            {
                X1 = C + outer * Math.Sin(angle),
                Y1 = C - outer * Math.Cos(angle),
                X2 = C + inner * Math.Sin(angle),
                Y2 = C - inner * Math.Cos(angle),
                Stroke = hourMark ? s.Primary : s.Secondary,
                StrokeThickness = hourMark ? 2.4 : 1,
            });
        }

        if (label != null)
        {
            var text = new TextBlock
            {
                Text = label,
                Width = 90,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = Fonts.Text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = s.Secondary,
            };
            SetLeft(text, C - 45);
            SetTop(text, C + 22);
            Children.Add(text);
        }

        Children.Add(Hand(_hour, 38, 4.6, s.Primary, 0));
        Children.Add(Hand(_minute, 58, 3.4, s.Primary, 0));
        if (seconds)
        {
            _second = new RotateTransform { CenterX = C, CenterY = C };
            Children.Add(Hand(_second, 62, 1.5, s.Accent, 14));
            Children.Add(Dot(8, s.Accent));
        }
        else
        {
            Children.Add(Dot(8, s.Primary));
        }
        Children.Add(Dot(3, s.Card));
    }

    public void Show(DateTime t)
    {
        _hour.Angle = (t.Hour % 12 + t.Minute / 60.0) * 30;
        _minute.Angle = (t.Minute + t.Second / 60.0) * 6;
        if (_second != null) _second.Angle = t.Second * 6;
    }

    private static Line Hand(RotateTransform rotation, double length, double thickness, Brush brush, double tail) => new()
    {
        X1 = C,
        Y1 = C + tail,
        X2 = C,
        Y2 = C - length,
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        RenderTransform = rotation,
    };

    private static Ellipse Dot(double diameter, Brush fill)
    {
        var dot = new Ellipse { Width = diameter, Height = diameter, Fill = fill };
        SetLeft(dot, C - diameter / 2);
        SetTop(dot, C - diameter / 2);
        return dot;
    }
}

internal sealed class ClockView : WidgetView
{
    private readonly ClockDial _dial;
    private readonly TimeZoneInfo? _zone;
    private readonly string? _city;
    private readonly TextBlock? _time;
    private readonly TextBlock? _date;

    public ClockView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        (_zone, _city) = ClockZone.Resolve(w);
        bool seconds = w.Option("seconds", "true") == "true";

        if (size.Id != WidgetDefinition.Medium.Id)
        {
            _dial = new ClockDial(s, seconds, _city);
            Child = _dial;
        }
        else
        {
            _dial = new ClockDial(s, seconds, null);
            var where = new TextBlock
            {
                Text = _city ?? "Local time",
                FontFamily = Fonts.Text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = s.Accent,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            _time = new TextBlock
            {
                FontFamily = Fonts.Display,
                FontSize = 50,
                FontWeight = FontWeights.Light,
                Foreground = s.Primary,
                Margin = new Thickness(-2, -4, 0, -2),
            };
            _date = new TextBlock
            {
                FontFamily = Fonts.Text,
                FontSize = 13,
                Foreground = s.Secondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var text = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0),
                Children = { where, _time, _date },
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(text, 1);
            grid.Children.Add(_dial);
            grid.Children.Add(text);
            Child = grid;
        }
        Tick();
    }

    public override void Tick()
    {
        DateTime now = ClockZone.Now(_zone);
        _dial.Show(now);
        if (_time == null || _date == null) return;

        _time.Text = now.ToString("HH:mm", Fonts.Culture);
        string date = now.ToString("dddd, MMMM d", Fonts.Culture);
        _date.Text = _zone == null ? date : $"{date}, {ClockZone.Difference(_zone)}";
    }
}

/// <summary>A city or a time zone the clock can follow.</summary>
internal sealed record ZoneChoice(string Id, string City, string Detail, string SearchKey);

internal static class Cities
{
    // Major cities with their IANA time zone (on Windows .NET converts them by itself).
    private static readonly (string City, string Country, string Iana)[] Known =
    {
        ("Rome", "Italy", "Europe/Rome"), ("Milan", "Italy", "Europe/Rome"),
        ("London", "United Kingdom", "Europe/London"), ("Dublin", "Ireland", "Europe/Dublin"),
        ("Lisbon", "Portugal", "Europe/Lisbon"), ("Madrid", "Spain", "Europe/Madrid"),
        ("Barcelona", "Spain", "Europe/Madrid"), ("Paris", "France", "Europe/Paris"),
        ("Brussels", "Belgium", "Europe/Brussels"), ("Amsterdam", "Netherlands", "Europe/Amsterdam"),
        ("Berlin", "Germany", "Europe/Berlin"), ("Munich", "Germany", "Europe/Berlin"),
        ("Zurich", "Switzerland", "Europe/Zurich"), ("Vienna", "Austria", "Europe/Vienna"),
        ("Prague", "Czech Republic", "Europe/Prague"), ("Warsaw", "Poland", "Europe/Warsaw"),
        ("Budapest", "Hungary", "Europe/Budapest"), ("Bucharest", "Romania", "Europe/Bucharest"),
        ("Athens", "Greece", "Europe/Athens"), ("Istanbul", "Turkey", "Europe/Istanbul"),
        ("Kyiv", "Ukraine", "Europe/Kiev"), ("Moscow", "Russia", "Europe/Moscow"),
        ("Copenhagen", "Denmark", "Europe/Copenhagen"), ("Oslo", "Norway", "Europe/Oslo"),
        ("Stockholm", "Sweden", "Europe/Stockholm"), ("Helsinki", "Finland", "Europe/Helsinki"),
        ("Reykjavík", "Iceland", "Atlantic/Reykjavik"),
        ("New York", "United States", "America/New_York"), ("Washington", "United States", "America/New_York"),
        ("Boston", "United States", "America/New_York"), ("Miami", "United States", "America/New_York"),
        ("Chicago", "United States", "America/Chicago"), ("Dallas", "United States", "America/Chicago"),
        ("Denver", "United States", "America/Denver"), ("Phoenix", "United States", "America/Phoenix"),
        ("Los Angeles", "United States", "America/Los_Angeles"), ("San Francisco", "United States", "America/Los_Angeles"),
        ("Seattle", "United States", "America/Los_Angeles"), ("Las Vegas", "United States", "America/Los_Angeles"),
        ("Anchorage", "United States", "America/Anchorage"), ("Honolulu", "United States", "Pacific/Honolulu"),
        ("Toronto", "Canada", "America/Toronto"), ("Montréal", "Canada", "America/Toronto"),
        ("Vancouver", "Canada", "America/Vancouver"), ("Mexico City", "Mexico", "America/Mexico_City"),
        ("Havana", "Cuba", "America/Havana"), ("Bogotá", "Colombia", "America/Bogota"),
        ("Lima", "Peru", "America/Lima"), ("Caracas", "Venezuela", "America/Caracas"),
        ("Santiago", "Chile", "America/Santiago"), ("Buenos Aires", "Argentina", "America/Argentina/Buenos_Aires"),
        ("São Paulo", "Brazil", "America/Sao_Paulo"), ("Rio de Janeiro", "Brazil", "America/Sao_Paulo"),
        ("Casablanca", "Morocco", "Africa/Casablanca"), ("Cairo", "Egypt", "Africa/Cairo"),
        ("Lagos", "Nigeria", "Africa/Lagos"), ("Nairobi", "Kenya", "Africa/Nairobi"),
        ("Johannesburg", "South Africa", "Africa/Johannesburg"), ("Cape Town", "South Africa", "Africa/Johannesburg"),
        ("Jerusalem", "Israel", "Asia/Jerusalem"), ("Tel Aviv", "Israel", "Asia/Jerusalem"),
        ("Riyadh", "Saudi Arabia", "Asia/Riyadh"), ("Doha", "Qatar", "Asia/Qatar"),
        ("Dubai", "United Arab Emirates", "Asia/Dubai"), ("Abu Dhabi", "United Arab Emirates", "Asia/Dubai"),
        ("Tehran", "Iran", "Asia/Tehran"), ("Karachi", "Pakistan", "Asia/Karachi"),
        ("Mumbai", "India", "Asia/Kolkata"), ("New Delhi", "India", "Asia/Kolkata"),
        ("Bangalore", "India", "Asia/Kolkata"), ("Kathmandu", "Nepal", "Asia/Kathmandu"),
        ("Dhaka", "Bangladesh", "Asia/Dhaka"), ("Bangkok", "Thailand", "Asia/Bangkok"),
        ("Hanoi", "Vietnam", "Asia/Bangkok"), ("Jakarta", "Indonesia", "Asia/Jakarta"),
        ("Singapore", "Singapore", "Asia/Singapore"), ("Kuala Lumpur", "Malaysia", "Asia/Kuala_Lumpur"),
        ("Manila", "Philippines", "Asia/Manila"), ("Hong Kong", "China", "Asia/Hong_Kong"),
        ("Beijing", "China", "Asia/Shanghai"), ("Shanghai", "China", "Asia/Shanghai"),
        ("Taipei", "Taiwan", "Asia/Taipei"), ("Seoul", "South Korea", "Asia/Seoul"),
        ("Tokyo", "Japan", "Asia/Tokyo"), ("Osaka", "Japan", "Asia/Tokyo"),
        ("Perth", "Australia", "Australia/Perth"), ("Adelaide", "Australia", "Australia/Adelaide"),
        ("Brisbane", "Australia", "Australia/Brisbane"), ("Sydney", "Australia", "Australia/Sydney"),
        ("Melbourne", "Australia", "Australia/Melbourne"), ("Auckland", "New Zealand", "Pacific/Auckland"),
    };

    private static List<ZoneChoice>? _all;

    /// <summary>The known cities, then all the Windows time zones.</summary>
    public static IReadOnlyList<ZoneChoice> All => _all ??= Build();

    private static List<ZoneChoice> Build()
    {
        var list = new List<ZoneChoice>();
        foreach (var (city, country, iana) in Known)
        {
            if (ClockZone.Find(iana) is not TimeZoneInfo zone) continue;
            list.Add(new ZoneChoice(iana, city, $"{country}, {Offset(zone)}", Normalize($"{city} {country}")));
        }
        foreach (TimeZoneInfo zone in TimeZoneInfo.GetSystemTimeZones())
        {
            // "(UTC+09:00) Osaka, Sapporo, Tokyo" -> "Osaka"
            string name = zone.DisplayName;
            int close = name.IndexOf(") ", StringComparison.Ordinal);
            string places = close >= 0 ? name[(close + 2)..] : name;
            string first = places.Split(',')[0].Trim();
            list.Add(new ZoneChoice(zone.Id, first, name, Normalize(name + " " + zone.Id)));
        }
        return list;
    }

    private static string Offset(TimeZoneInfo zone)
    {
        TimeSpan o = zone.GetUtcOffset(DateTime.UtcNow);
        string sign = o < TimeSpan.Zero ? "−" : "+";
        o = o.Duration();
        return o.Minutes == 0 ? $"UTC{sign}{o.Hours}" : $"UTC{sign}{o.Hours}:{o.Minutes:00}";
    }

    public static IEnumerable<ZoneChoice> Search(string query)
    {
        string q = Normalize(query);
        return q.Length == 0 ? All : All.Where(c => c.SearchKey.Contains(q, StringComparison.Ordinal));
    }

    public static string Normalize(string s)
    {
        string d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (char ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString().Trim();
    }
}

/// <summary>Clock options in the customization window.</summary>
internal sealed class ClockOptionsEditor : StackPanel
{
    private readonly WidgetInstance _w;
    private readonly Action _changed;
    private readonly ListBox _results;
    private readonly FrameworkElement _picker;
    private readonly TextBlock _chosen;

    public ClockOptionsEditor(WidgetInstance w, Action changed)
    {
        _w = w;
        _changed = changed;

        var seconds = new CheckBox
        {
            Content = "Show the second hand",
            IsChecked = w.Option("seconds", "true") == "true",
        };
        seconds.SetResourceReference(StyleProperty, "Switch");
        seconds.Click += (_, _) =>
        {
            _w.Options["seconds"] = seconds.IsChecked == true ? "true" : "false";
            _changed();
        };

        bool other = w.Option("zone").Length > 0;
        var mode = UiKit.Segmented(new[] { ("system", "Computer's"), ("city", "Another city's") },
            other ? "city" : "system", OnMode);

        var search = new TextBox { Tag = "Search for a city or time zone" };
        search.SetResourceReference(StyleProperty, "SearchBox");
        AutomationName(search, "Search for a city or time zone");
        search.TextChanged += (_, _) => Fill(search.Text);

        _results = new ListBox { Height = 188, Margin = new Thickness(0, 8, 0, 0) };
        AutomationName(_results, "Cities and time zones");
        _results.SelectionChanged += (_, _) =>
        {
            if (_results.SelectedItem is ListBoxItem { Tag: ZoneChoice c }) Choose(c);
        };

        _chosen = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        _chosen.SetResourceReference(StyleProperty, "Text.Caption");

        _picker = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0),
            Visibility = other ? Visibility.Visible : Visibility.Collapsed,
            Children = { search, _results, _chosen },
        };

        Children.Add(UiKit.Field("Seconds", seconds));
        Children.Add(UiKit.Field("Time", new StackPanel { Children = { mode, _picker } }, alignTop: true));

        Fill("");
        UpdateChosen();
    }

    private static void AutomationName(DependencyObject o, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(o, name);

    private void OnMode(string mode)
    {
        bool other = mode == "city";
        _picker.Visibility = other ? Visibility.Visible : Visibility.Collapsed;
        if (!other)
        {
            _w.Options.Remove("zone");
            _w.Options.Remove("city");
            _results.SelectedItem = null;
        }
        else if (_results.SelectedItem is ListBoxItem { Tag: ZoneChoice c })
        {
            Choose(c);
            return;
        }
        UpdateChosen();
        _changed();
    }

    private void Fill(string query)
    {
        string current = _w.Option("zone");
        string city = _w.Option("city");
        _results.Items.Clear();
        foreach (ZoneChoice c in Cities.Search(query).Take(80))
        {
            var name = new TextBlock { Text = c.City };
            name.SetResourceReference(StyleProperty, "Text.Label");
            var detail = new TextBlock { Text = c.Detail, TextTrimming = TextTrimming.CharacterEllipsis };
            detail.SetResourceReference(StyleProperty, "Text.Caption");
            var item = new ListBoxItem { Tag = c, Content = new StackPanel { Children = { name, detail } } };
            System.Windows.Automation.AutomationProperties.SetName(item, $"{c.City}, {c.Detail}");
            _results.Items.Add(item);
            if (c.Id == current && c.City == city) _results.SelectedItem = item;
        }
    }

    private void Choose(ZoneChoice c)
    {
        if (_w.Option("zone") == c.Id && _w.Option("city") == c.City) return;
        _w.Options["zone"] = c.Id;
        _w.Options["city"] = c.City;
        UpdateChosen();
        _changed();
    }

    private void UpdateChosen()
    {
        var (zone, city) = ClockZone.Resolve(_w);
        _chosen.Text = zone == null
            ? "Pick a city from the list."
            : $"Shows the time in {city} ({ClockZone.Difference(zone)} from here).";
        _chosen.Visibility = _picker.Visibility;
    }
}
