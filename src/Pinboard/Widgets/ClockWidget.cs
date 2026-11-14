using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

public sealed class ClockDefinition : WidgetDefinition
{
    public override string Type => "clock";
    public override string Name => "Orologio";
    public override string Description =>
        "Un orologio analogico con la lancetta dei secondi. Può seguire l'ora del computer " +
        "o quella di un'altra città; la versione media aggiunge l'ora in cifre e la data.";
    public override string Category => "Orologi";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#FF9F0A";
    public override IReadOnlyList<string> Features { get; } = new[]
    {
        "Ora del computer o di un'altra città",
        "Lancetta dei secondi da mostrare o nascondere",
    };

    protected override void ApplyDefaults(WidgetInstance w) => w.Options["seconds"] = "true";

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new ClockView(w, style, size);

    public override string? Summary(WidgetInstance w)
    {
        string where = ClockZone.Resolve(w).City ?? "ora del computer";
        return w.Option("seconds", "true") == "true" ? where : where + ", senza secondi";
    }

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed) =>
        new ClockOptionsEditor(w, changed);
}

/// <summary>Il fuso orario scelto per un orologio.</summary>
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

    /// <summary>"+8 ore", "−1 ora", "+5 ore 30 min", "stessa ora".</summary>
    public static string Difference(TimeZoneInfo zone)
    {
        DateTime utc = DateTime.UtcNow;
        TimeSpan diff = zone.GetUtcOffset(utc) - TimeZoneInfo.Local.GetUtcOffset(utc);
        if (diff == TimeSpan.Zero) return "stessa ora";
        string sign = diff < TimeSpan.Zero ? "−" : "+";
        diff = diff.Duration();
        string hours = diff.Hours == 1 ? "1 ora" : $"{diff.Hours} ore";
        if (diff.Minutes == 0) return sign + hours;
        return diff.Hours == 0 ? $"{sign}{diff.Minutes} min" : $"{sign}{hours} {diff.Minutes} min";
    }
}

/// <summary>Quadrante analogico di 170 x 170.</summary>
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
                Text = _city ?? "Ora locale",
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

        _time.Text = now.ToString("HH:mm", Fonts.Italian);
        string date = now.ToString("dddd d MMMM", Fonts.Italian);
        _date.Text = _zone == null ? date : $"{date}, {ClockZone.Difference(_zone)}";
    }
}

/// <summary>Una citta' o un fuso orario che l'orologio puo' seguire.</summary>
internal sealed record ZoneChoice(string Id, string City, string Detail, string SearchKey);

internal static class Cities
{
    // Citta' principali con il loro fuso IANA (su Windows .NET li converte da solo).
    private static readonly (string City, string Country, string Iana)[] Known =
    {
        ("Roma", "Italia", "Europe/Rome"), ("Milano", "Italia", "Europe/Rome"),
        ("Londra", "Regno Unito", "Europe/London"), ("Dublino", "Irlanda", "Europe/Dublin"),
        ("Lisbona", "Portogallo", "Europe/Lisbon"), ("Madrid", "Spagna", "Europe/Madrid"),
        ("Barcellona", "Spagna", "Europe/Madrid"), ("Parigi", "Francia", "Europe/Paris"),
        ("Bruxelles", "Belgio", "Europe/Brussels"), ("Amsterdam", "Paesi Bassi", "Europe/Amsterdam"),
        ("Berlino", "Germania", "Europe/Berlin"), ("Monaco di Baviera", "Germania", "Europe/Berlin"),
        ("Zurigo", "Svizzera", "Europe/Zurich"), ("Vienna", "Austria", "Europe/Vienna"),
        ("Praga", "Repubblica Ceca", "Europe/Prague"), ("Varsavia", "Polonia", "Europe/Warsaw"),
        ("Budapest", "Ungheria", "Europe/Budapest"), ("Bucarest", "Romania", "Europe/Bucharest"),
        ("Atene", "Grecia", "Europe/Athens"), ("Istanbul", "Turchia", "Europe/Istanbul"),
        ("Kiev", "Ucraina", "Europe/Kiev"), ("Mosca", "Russia", "Europe/Moscow"),
        ("Copenaghen", "Danimarca", "Europe/Copenhagen"), ("Oslo", "Norvegia", "Europe/Oslo"),
        ("Stoccolma", "Svezia", "Europe/Stockholm"), ("Helsinki", "Finlandia", "Europe/Helsinki"),
        ("Reykjavík", "Islanda", "Atlantic/Reykjavik"),
        ("New York", "Stati Uniti", "America/New_York"), ("Washington", "Stati Uniti", "America/New_York"),
        ("Boston", "Stati Uniti", "America/New_York"), ("Miami", "Stati Uniti", "America/New_York"),
        ("Chicago", "Stati Uniti", "America/Chicago"), ("Dallas", "Stati Uniti", "America/Chicago"),
        ("Denver", "Stati Uniti", "America/Denver"), ("Phoenix", "Stati Uniti", "America/Phoenix"),
        ("Los Angeles", "Stati Uniti", "America/Los_Angeles"), ("San Francisco", "Stati Uniti", "America/Los_Angeles"),
        ("Seattle", "Stati Uniti", "America/Los_Angeles"), ("Las Vegas", "Stati Uniti", "America/Los_Angeles"),
        ("Anchorage", "Stati Uniti", "America/Anchorage"), ("Honolulu", "Stati Uniti", "Pacific/Honolulu"),
        ("Toronto", "Canada", "America/Toronto"), ("Montréal", "Canada", "America/Toronto"),
        ("Vancouver", "Canada", "America/Vancouver"), ("Città del Messico", "Messico", "America/Mexico_City"),
        ("L'Avana", "Cuba", "America/Havana"), ("Bogotá", "Colombia", "America/Bogota"),
        ("Lima", "Perù", "America/Lima"), ("Caracas", "Venezuela", "America/Caracas"),
        ("Santiago", "Cile", "America/Santiago"), ("Buenos Aires", "Argentina", "America/Argentina/Buenos_Aires"),
        ("San Paolo", "Brasile", "America/Sao_Paulo"), ("Rio de Janeiro", "Brasile", "America/Sao_Paulo"),
        ("Casablanca", "Marocco", "Africa/Casablanca"), ("Il Cairo", "Egitto", "Africa/Cairo"),
        ("Lagos", "Nigeria", "Africa/Lagos"), ("Nairobi", "Kenya", "Africa/Nairobi"),
        ("Johannesburg", "Sudafrica", "Africa/Johannesburg"), ("Città del Capo", "Sudafrica", "Africa/Johannesburg"),
        ("Gerusalemme", "Israele", "Asia/Jerusalem"), ("Tel Aviv", "Israele", "Asia/Jerusalem"),
        ("Riad", "Arabia Saudita", "Asia/Riyadh"), ("Doha", "Qatar", "Asia/Qatar"),
        ("Dubai", "Emirati Arabi Uniti", "Asia/Dubai"), ("Abu Dhabi", "Emirati Arabi Uniti", "Asia/Dubai"),
        ("Teheran", "Iran", "Asia/Tehran"), ("Karachi", "Pakistan", "Asia/Karachi"),
        ("Mumbai", "India", "Asia/Kolkata"), ("Nuova Delhi", "India", "Asia/Kolkata"),
        ("Bangalore", "India", "Asia/Kolkata"), ("Kathmandu", "Nepal", "Asia/Kathmandu"),
        ("Dacca", "Bangladesh", "Asia/Dhaka"), ("Bangkok", "Thailandia", "Asia/Bangkok"),
        ("Hanoi", "Vietnam", "Asia/Bangkok"), ("Giacarta", "Indonesia", "Asia/Jakarta"),
        ("Singapore", "Singapore", "Asia/Singapore"), ("Kuala Lumpur", "Malesia", "Asia/Kuala_Lumpur"),
        ("Manila", "Filippine", "Asia/Manila"), ("Hong Kong", "Cina", "Asia/Hong_Kong"),
        ("Pechino", "Cina", "Asia/Shanghai"), ("Shanghai", "Cina", "Asia/Shanghai"),
        ("Taipei", "Taiwan", "Asia/Taipei"), ("Seul", "Corea del Sud", "Asia/Seoul"),
        ("Tokyo", "Giappone", "Asia/Tokyo"), ("Osaka", "Giappone", "Asia/Tokyo"),
        ("Perth", "Australia", "Australia/Perth"), ("Adelaide", "Australia", "Australia/Adelaide"),
        ("Brisbane", "Australia", "Australia/Brisbane"), ("Sydney", "Australia", "Australia/Sydney"),
        ("Melbourne", "Australia", "Australia/Melbourne"), ("Auckland", "Nuova Zelanda", "Pacific/Auckland"),
    };

    private static List<ZoneChoice>? _all;

    /// <summary>Le citta' note, poi tutti i fusi orari di Windows.</summary>
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
            // "(UTC+09:00) Osaka, Sapporo, Tokio" -> "Osaka"
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

/// <summary>Opzioni dell'orologio nella finestra di personalizzazione.</summary>
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
            Content = "Mostra la lancetta dei secondi",
            IsChecked = w.Option("seconds", "true") == "true",
        };
        seconds.SetResourceReference(StyleProperty, "Switch");
        seconds.Click += (_, _) =>
        {
            _w.Options["seconds"] = seconds.IsChecked == true ? "true" : "false";
            _changed();
        };

        bool other = w.Option("zone").Length > 0;
        var mode = UiKit.Segmented(new[] { ("system", "Del computer"), ("city", "Di un'altra città") },
            other ? "city" : "system", OnMode);

        var search = new TextBox { Tag = "Cerca una città o un fuso orario" };
        search.SetResourceReference(StyleProperty, "SearchBox");
        AutomationName(search, "Cerca una città o un fuso orario");
        search.TextChanged += (_, _) => Fill(search.Text);

        _results = new ListBox { Height = 188, Margin = new Thickness(0, 8, 0, 0) };
        AutomationName(_results, "Città e fusi orari");
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

        Children.Add(UiKit.Field("Secondi", seconds));
        Children.Add(UiKit.Field("Ora", new StackPanel { Children = { mode, _picker } }, alignTop: true));

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
            ? "Scegli una città dall'elenco."
            : $"Mostra l'ora di {city} ({ClockZone.Difference(zone)} rispetto a qui).";
        _chosen.Visibility = _picker.Visibility;
    }
}
