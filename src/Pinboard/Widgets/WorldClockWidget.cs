using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pinboard;

/// <summary>The cities of a World clock, stored as JSON in the "cities" option.</summary>
internal static class WorldClocks
{
    public sealed record City(
        [property: JsonPropertyName("zone")] string Zone,
        [property: JsonPropertyName("city")] string Name);

    public const int Max = 4;
    public const int Min = 2;

    private static readonly City[] Preset =
    {
        new("Europe/Rome", "Rome"), new("Europe/London", "London"),
        new("America/New_York", "New York"), new("Asia/Tokyo", "Tokyo"),
    };

    /// <summary>Rome, London, New York and Tokyo, minus any that has the same time as here.</summary>
    public static List<City> Defaults()
    {
        DateTime utc = DateTime.UtcNow;
        TimeSpan local = TimeZoneInfo.Local.GetUtcOffset(utc);
        var list = Preset.Where(c => ClockZone.Find(c.Zone) is TimeZoneInfo z && z.GetUtcOffset(utc) != local).ToList();
        return list.Count >= Min ? list : Preset.Where(c => ClockZone.Find(c.Zone) != null).ToList();
    }

    /// <summary>Valid cities (unknown time zones are skipped), at most four; broken data gives the defaults.</summary>
    public static List<City> Parse(string json)
    {
        try
        {
            var cities = JsonSerializer.Deserialize<List<City>>(json, Json) ?? new();
            return cities.Where(c => !string.IsNullOrWhiteSpace(c.Zone) && ClockZone.Find(c.Zone) != null)
                .Take(Max).ToList();
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public static string Serialize(IEnumerable<City> cities) => JsonSerializer.Serialize(cities, Json);

    private static readonly JsonSerializerOptions Json = new();


    /// <summary>"Today", "Tomorrow" or "Yesterday" for a time somewhere else compared to here.</summary>
    public static string DayLabel(DateTime there, DateTime here) => (there.Date - here.Date).Days switch
    {
        > 0 => "Tomorrow",
        < 0 => "Yesterday",
        _ => "Today",
    };

    /// <summary>"+8h", "−5h", "+5:30h".</summary>
    public static string FormatOffset(TimeSpan diff)
    {
        string sign = diff < TimeSpan.Zero ? "−" : "+";
        diff = diff.Duration();
        return diff.Minutes == 0 ? $"{sign}{(int)diff.TotalHours}h" : $"{sign}{(int)diff.TotalHours}:{diff.Minutes:00}h";
    }

    public static List<City> Of(WidgetInstance w)
    {
        string json = w.Option("cities");
        return json.Length == 0 ? Defaults() : Parse(json);
    }
}

public sealed class WorldClockDefinition : WidgetDefinition
{
    public override string Type => "worldclock";
    public override string Name => "World clock";
    public override string Description =>
        "Two to four analog clocks side by side, each with the time of a city and how far ahead or " +
        "behind it is.";
    public override string Category => "Clocks";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Medium };
    public override string DefaultAccent => "#FF9F0A";
    public override IReadOnlyList<string> Features { get; } = new[] { "Two to four cities of your choice" };

    protected override void ApplyDefaults(WidgetInstance w) =>
        w.Options["cities"] = WorldClocks.Serialize(WorldClocks.Defaults());

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new WorldClockView(w, style, size);

    public override string? Summary(WidgetInstance w) => string.Join(", ", WorldClocks.Of(w).Select(c => c.Name));

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed) =>
        new WorldClockEditor(w, changed);
}

internal sealed class WorldClockView : WidgetView
{
    private readonly List<(WorldClocks.City City, TimeZoneInfo Zone, ClockDial Dial, TextBlock Detail)> _clocks = new();

    public WorldClockView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        var cities = WorldClocks.Of(w);
        var grid = new UniformGrid { Rows = 1, Columns = Math.Max(1, cities.Count), Margin = new Thickness(10, 12, 10, 12) };
        foreach (var city in cities)
        {
            if (ClockZone.Find(city.Zone) is not TimeZoneInfo zone) continue;
            var dial = new ClockDial(s, seconds: false, label: null);
            var box = new Viewbox { Width = 84, Height = 84, Child = dial, HorizontalAlignment = HorizontalAlignment.Center };
            var name = WidgetParts.Label(city.Name, 12.5, s.Primary, FontWeights.SemiBold);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 4, 0, 0);
            var detail = WidgetParts.Label("", 11, s.Secondary);
            detail.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { box, name, detail } });
            _clocks.Add((city, zone, dial, detail));
        }
        Child = grid;
        Tick();
    }

    public override void Tick()
    {
        DateTime utc = DateTime.UtcNow;
        DateTime here = DateTime.Now;
        TimeSpan local = TimeZoneInfo.Local.GetUtcOffset(utc);
        foreach (var (_, zone, dial, detail) in _clocks)
        {
            DateTime there = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            dial.Show(there);
            detail.Text = $"{WorldClocks.DayLabel(there, here)}, {WorldClocks.FormatOffset(zone.GetUtcOffset(utc) - local)}";
        }
    }
}

/// <summary>World clock options: the chosen cities with Remove, and a search to add more.</summary>
internal sealed class WorldClockEditor : StackPanel
{
    private readonly WidgetInstance _w;
    private readonly Action _changed;
    private readonly StackPanel _chosen = new();
    private readonly ListBox _results = new() { Height = 168, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBox _search = new() { Tag = "Search for a city or time zone" };
    private readonly TextBlock _limit;
    private List<WorldClocks.City> _cities;

    public WorldClockEditor(WidgetInstance w, Action changed)
    {
        _w = w;
        _changed = changed;
        _cities = WorldClocks.Of(w);

        _search.SetResourceReference(StyleProperty, "SearchBox");
        System.Windows.Automation.AutomationProperties.SetName(_search, "Search for a city to add");
        _search.TextChanged += (_, _) => Fill();
        System.Windows.Automation.AutomationProperties.SetName(_results, "Cities and time zones");
        _results.SelectionChanged += (_, _) =>
        {
            if (_results.SelectedItem is not ListBoxItem { Tag: ZoneChoice c } || _cities.Count >= WorldClocks.Max) return;
            _cities.Add(new WorldClocks.City(c.Id, c.City));
            Save();
        };
        _limit = UiKit.Text("", "Text.Caption", new Thickness(0, 6, 0, 0));

        Children.Add(UiKit.Field("Cities", _chosen, alignTop: true));
        Children.Add(UiKit.Field("Add a city", new StackPanel { Children = { _search, _results, _limit } }, alignTop: true));
        Refresh();
        Fill();
    }

    private void Save()
    {
        _w.Options["cities"] = WorldClocks.Serialize(_cities);
        Refresh();
        _changed();
    }

    private void Refresh()
    {
        _chosen.Children.Clear();
        foreach (var city in _cities.ToList())
        {
            var remove = UiKit.Button("Remove", "Button.Ghost", () =>
            {
                _cities.Remove(city);
                Save();
            });
            remove.IsEnabled = _cities.Count > WorldClocks.Min;
            System.Windows.Automation.AutomationProperties.SetName(remove, $"Remove {city.Name}");
            DockPanel.SetDock(remove, Dock.Right);
            var name = UiKit.Text(city.Name, "Text.Label");
            name.VerticalAlignment = VerticalAlignment.Center;
            _chosen.Children.Add(new DockPanel { Margin = new Thickness(0, 0, 0, 4), Children = { remove, name } });
        }
        bool full = _cities.Count >= WorldClocks.Max;
        _search.IsEnabled = _results.IsEnabled = !full;
        _limit.Text = full
            ? "A World clock shows up to four cities. Remove one to add another."
            : $"You can add {WorldClocks.Max - _cities.Count} more.";
        _results.SelectedItem = null;
    }

    private void Fill()
    {
        _results.Items.Clear();
        foreach (ZoneChoice c in Cities.Search(_search.Text).Take(80))
        {
            var name = new TextBlock { Text = c.City };
            name.SetResourceReference(StyleProperty, "Text.Label");
            var detail = new TextBlock { Text = c.Detail, TextTrimming = TextTrimming.CharacterEllipsis };
            detail.SetResourceReference(StyleProperty, "Text.Caption");
            var item = new ListBoxItem { Tag = c, Content = new StackPanel { Children = { name, detail } } };
            System.Windows.Automation.AutomationProperties.SetName(item, $"{c.City}, {c.Detail}");
            _results.Items.Add(item);
        }
    }
}
