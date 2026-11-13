using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>Orologio analogico piccolo, con lancetta dei secondi arancione.</summary>
public sealed class ClockWidget : WidgetWindow
{
    private const double Size = 170;
    private const double C = Size / 2;

    private readonly RotateTransform _hour = new() { CenterX = C, CenterY = C };
    private readonly RotateTransform _minute = new() { CenterX = C, CenterY = C };
    private readonly RotateTransform _second = new() { CenterX = C, CenterY = C };

    public ClockWidget() : base("clock", Size, Size)
    {
        var canvas = new Canvas
        {
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Tacche: 60, quelle delle ore piu' lunghe e scure.
        for (int i = 0; i < 60; i++)
        {
            bool hourMark = i % 5 == 0;
            double outer = 70;
            double inner = outer - (hourMark ? 9 : 4);
            double angle = i * 6 * Math.PI / 180;
            canvas.Children.Add(new Line
            {
                X1 = C + outer * Math.Sin(angle),
                Y1 = C - outer * Math.Cos(angle),
                X2 = C + inner * Math.Sin(angle),
                Y2 = C - inner * Math.Cos(angle),
                Stroke = hourMark ? Theme.Primary : Theme.Secondary,
                StrokeThickness = hourMark ? 2.4 : 1,
            });
        }

        canvas.Children.Add(Hand(_hour, 38, 4.6, Theme.Primary, 0));
        canvas.Children.Add(Hand(_minute, 58, 3.4, Theme.Primary, 0));
        canvas.Children.Add(Hand(_second, 62, 1.5, Theme.Orange, 14));
        canvas.Children.Add(Dot(8, Theme.Orange));
        canvas.Children.Add(Dot(3, Theme.Card));

        Content = CreateCard(canvas);
        OnTick();
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
        Canvas.SetLeft(dot, C - diameter / 2);
        Canvas.SetTop(dot, C - diameter / 2);
        return dot;
    }

    public override void OnTick()
    {
        DateTime now = DateTime.Now;
        _hour.Angle = (now.Hour % 12 + now.Minute / 60.0) * 30;
        _minute.Angle = (now.Minute + now.Second / 60.0) * 6;
        _second.Angle = now.Second * 6;
    }
}
