using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pinboard;

/// <summary>Calendario piccolo: giorno della settimana in rosso, numero grande, mese e anno.</summary>
public sealed class CalendarWidget : WidgetWindow
{
    private static readonly CultureInfo Italian = new("it-IT");
    private static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily Text = new("Segoe UI Variable Text, Segoe UI");

    private readonly TextBlock _weekday;
    private readonly TextBlock _day;
    private readonly TextBlock _month;
    private DateTime _shown;

    public CalendarWidget() : base("calendar", 170, 170)
    {
        _weekday = new TextBlock
        {
            FontFamily = Text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Red,
        };
        _day = new TextBlock
        {
            FontFamily = Display,
            FontSize = 68,
            FontWeight = FontWeights.Light,
            Foreground = Theme.Primary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _month = new TextBlock
        {
            FontFamily = Text,
            FontSize = 14,
            Foreground = Theme.Secondary,
        };

        var grid = new Grid { Margin = new Thickness(16, 14, 16, 14) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_weekday, 0);
        Grid.SetRow(_day, 1);
        Grid.SetRow(_month, 2);
        grid.Children.Add(_weekday);
        grid.Children.Add(_day);
        grid.Children.Add(_month);

        Content = CreateCard(grid);
        OnTick();
    }

    public override void OnTick()
    {
        DateTime today = DateTime.Today;
        if (today == _shown) return;
        _shown = today;

        _weekday.Text = today.ToString("dddd", Italian).ToUpper(Italian);
        _day.Text = today.Day.ToString(Italian);
        _month.Text = today.ToString("MMMM yyyy", Italian);
    }
}
