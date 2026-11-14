using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>Pezzi d'interfaccia condivisi da dashboard e finestra di personalizzazione.</summary>
internal static class UiKit
{
    public static TextBlock Text(string text, string style, Thickness? margin = null)
    {
        var t = new TextBlock { Text = text, Margin = margin ?? new Thickness(0) };
        t.SetResourceReference(FrameworkElement.StyleProperty, style);
        return t;
    }

    public static Button Button(string text, string style, Action onClick)
    {
        var b = new Button { Content = text };
        b.SetResourceReference(FrameworkElement.StyleProperty, style);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static ToggleButton Star(string widgetName, bool on, Action<bool> changed)
    {
        var star = new ToggleButton { IsChecked = on };
        star.SetResourceReference(FrameworkElement.StyleProperty, "StarToggle");
        void Describe() => AutomationProperties.SetName(star,
            star.IsChecked == true ? $"Togli {widgetName} dai preferiti" : $"Aggiungi {widgetName} ai preferiti");
        Describe();
        star.ToolTip = new ToolTip { Content = "Preferito" };
        star.Click += (_, _) =>
        {
            Describe();
            changed(star.IsChecked == true);
        };
        return star;
    }

    /// <summary>Riga di un modulo: etichetta a sinistra, controllo a destra.</summary>
    public static FrameworkElement Field(string label, UIElement control, bool alignTop = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var text = Text(label, "Text.Body");
        text.VerticalAlignment = alignTop ? VerticalAlignment.Top : VerticalAlignment.Center;
        if (alignTop) text.Margin = new Thickness(0, 6, 0, 0);
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>Scelta tra poche opzioni, come i segmenti dei controlli di Windows.</summary>
    public static FrameworkElement Segmented(IEnumerable<(string Value, string Label)> options, string selected,
        Action<string> changed)
    {
        string group = "seg" + Guid.NewGuid().ToString("N");
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in options)
        {
            var rb = new RadioButton { Content = label, GroupName = group, IsChecked = value == selected };
            rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
            rb.Checked += (_, _) => changed(value);
            panel.Children.Add(rb);
        }
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = panel,
        };
        border.SetResourceReference(Border.BackgroundProperty, "Bg.Hover");
        return border;
    }

    /// <summary>
    /// Campioni di colore: "automatico" (null), alcuni preimpostati e uno personalizzato che apre
    /// il selettore di Windows.
    /// </summary>
    public static FrameworkElement Swatches(Brush autoLook, string autoName,
        IReadOnlyList<(string Hex, string Name)> presets, string? selected, Action<string?> changed)
    {
        string group = "sw" + Guid.NewGuid().ToString("N");
        var panel = new WrapPanel();
        RadioButton? previous = null;

        RadioButton Make(Brush look, string name, bool isChecked)
        {
            var rb = new RadioButton { Background = look, GroupName = group, IsChecked = isChecked };
            rb.SetResourceReference(FrameworkElement.StyleProperty, "Swatch");
            rb.ToolTip = new ToolTip { Content = name };
            AutomationProperties.SetName(rb, name);
            if (isChecked) previous = rb;
            panel.Children.Add(rb);
            return rb;
        }

        var auto = Make(autoLook, autoName, selected == null);
        auto.Checked += (_, _) => { previous = auto; changed(null); };

        bool matched = selected == null;
        foreach (var (hex, name) in presets)
        {
            bool isIt = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);
            matched |= isIt;
            var rb = Make(ColorUtil.Brush(hex), name, isIt);
            rb.Checked += (_, _) => { previous = rb; changed(hex); };
        }

        Brush rainbow = new LinearGradientBrush(new GradientStopCollection
        {
            new(ColorUtil.Parse("#FF3B30"), 0), new(ColorUtil.Parse("#FFCC00"), 0.25),
            new(ColorUtil.Parse("#34C759"), 0.5), new(ColorUtil.Parse("#0A84FF"), 0.75),
            new(ColorUtil.Parse("#AF52DE"), 1),
        }, 45);
        rainbow.Freeze();
        var custom = Make(!matched && ColorUtil.TryParse(selected, out Color c) ? ColorUtil.Brush(c) : rainbow,
            "Scegli un colore", !matched);
        custom.Content = new TextBlock
        {
            Text = "",
            FontSize = 11,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            Foreground = Brushes.White,
            IsHitTestVisible = false,
        };
        string? current = matched ? null : selected;
        custom.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
            if (ColorUtil.TryParse(current ?? selected, out Color start))
                dialog.Color = System.Drawing.Color.FromArgb(start.R, start.G, start.B);
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var picked = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                current = ColorUtil.ToHex(picked);
                custom.Background = ColorUtil.Brush(picked);
                previous = custom;
                changed(current);
            }
            else if (previous != null && previous != custom)
            {
                previous.IsChecked = true;
            }
            else if (current == null)
            {
                custom.IsChecked = false;
            }
        };
        return panel;
    }

    /// <summary>Campione "automatico" diviso in diagonale tra chiaro e scuro.</summary>
    public static Brush SplitBrush(Color a, Color b)
    {
        var brush = new LinearGradientBrush(new GradientStopCollection
        {
            new(a, 0), new(a, 0.5), new(b, 0.5), new(b, 1),
        }, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Pannello di sughero su cui un widget viene mostrato "appuntato" con una puntina.</summary>
internal sealed class CorkStage : Border
{
    private readonly Viewbox _box;

    public CorkStage(double padding = 18, double radius = 12)
    {
        SetResourceReference(BackgroundProperty, "Cork");
        CornerRadius = new CornerRadius(radius);
        ClipToBounds = true;

        var vignette = new Border
        {
            CornerRadius = new CornerRadius(radius),
            IsHitTestVisible = false,
            Background = new RadialGradientBrush(Colors.Transparent, Color.FromArgb(0x38, 0, 0, 0))
            {
                RadiusX = 0.75,
                RadiusY = 0.75,
                GradientStops = { new GradientStop(Colors.Transparent, 0.55) },
            },
        };
        var edge = new Border
        {
            CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        _box = new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(padding) };
        Child = new Grid { Children = { vignette, _box, edge } };
    }

    public void Show(FrameworkElement widget)
    {
        widget.Effect = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.32,
            Color = Colors.Black,
        };
        var pinned = new Grid { Margin = new Thickness(0, 9, 0, 0) };
        pinned.Children.Add(widget);
        pinned.Children.Add(CreatePin());
        _box.Child = pinned;
    }

    private static UIElement CreatePin()
    {
        Color pin = UiTheme.Pin;
        Color light = Color.FromArgb(255, (byte)Math.Min(255, pin.R + 70), (byte)Math.Min(255, pin.G + 70),
            (byte)Math.Min(255, pin.B + 70));
        Color deep = Color.FromArgb(255, (byte)(pin.R * 0.62), (byte)(pin.G * 0.62), (byte)(pin.B * 0.62));

        var canvas = new Canvas
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(0, -9),
            IsHitTestVisible = false,
        };
        var shadow = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)),
            Effect = new BlurEffect { Radius = 3 },
        };
        Canvas.SetLeft(shadow, 3);
        Canvas.SetTop(shadow, 4);
        var head = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new RadialGradientBrush(light, deep)
            {
                GradientOrigin = new Point(0.35, 0.3),
                Center = new Point(0.4, 0.38),
                RadiusX = 0.7,
                RadiusY = 0.7,
                GradientStops = { new GradientStop(pin, 0.45) },
            },
        };
        Canvas.SetLeft(head, 1);
        Canvas.SetTop(head, 1);
        canvas.Children.Add(shadow);
        canvas.Children.Add(head);
        return canvas;
    }
}
