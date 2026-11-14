using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>Drawing pieces shared by several widgets: progress rings and bars, labels, icons.</summary>
internal static class WidgetParts
{
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>A ring that fills clockwise from the top; <paramref name="fraction"/> is 0..1.</summary>
    public static Grid Ring(double fraction, double size, double thickness, Brush track, Brush fill, UIElement? center = null)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        double r = (size - thickness) / 2;
        var grid = new Grid { Width = size, Height = size };
        grid.Children.Add(new Ellipse
        {
            Stroke = track,
            StrokeThickness = thickness,
        });

        if (fraction >= 0.9999)
        {
            grid.Children.Add(new Ellipse { Stroke = fill, StrokeThickness = thickness });
        }
        else if (fraction > 0)
        {
            double angle = fraction * 2 * Math.PI;
            var c = new Point(size / 2, size / 2);
            var start = new Point(c.X, c.Y - r);
            var end = new Point(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
            grid.Children.Add(new Path
            {
                Data = new PathGeometry(new[] { figure }),
                Stroke = fill,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });
        }

        if (center != null) grid.Children.Add(center);
        return grid;
    }

    /// <summary>A horizontal bar with rounded ends; <paramref name="fraction"/> is 0..1.</summary>
    public static Grid Bar(double fraction, double height, Brush track, Brush fill)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var grid = new Grid { Height = height };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
        var back = new Border { CornerRadius = new CornerRadius(height / 2), Background = track };
        Grid.SetColumnSpan(back, 2);
        grid.Children.Add(back);
        if (fraction > 0)
            grid.Children.Add(new Border { CornerRadius = new CornerRadius(height / 2), Background = fill, MinWidth = height });
        return grid;
    }

    /// <summary>A faint version of a brush, for ring and bar tracks.</summary>
    public static Brush Faint(Brush brush, double opacity = 0.18)
    {
        var b = brush.CloneCurrentValue();
        b.Opacity = opacity;
        b.Freeze();
        return b;
    }

    public static TextBlock Label(string text, double size, Brush brush, FontWeight? weight = null, bool display = false) => new()
    {
        Text = text,
        FontFamily = display ? Fonts.Display : Fonts.Text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = brush,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    public static TextBlock Icon(string glyph, double size, Brush brush) => new()
    {
        Text = glyph,
        FontFamily = Icons,
        FontSize = size,
        Foreground = brush,
    };
}
