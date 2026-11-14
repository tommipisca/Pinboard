using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Pinboard;

/// <summary>UI pieces shared by the dashboard and the customization window.</summary>
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
            star.IsChecked == true ? $"Remove {widgetName} from favorites" : $"Add {widgetName} to favorites");
        Describe();
        star.ToolTip = new ToolTip { Content = "Favorite" };
        star.Click += (_, _) =>
        {
            Describe();
            changed(star.IsChecked == true);
        };
        return star;
    }

    /// <summary>A form row: label on the left, control on the right.</summary>
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

    /// <summary>A choice between a few options, like the segmented controls in Windows.</summary>
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
    /// Color swatches: "automatic" (null), a few presets and a custom one that opens
    /// the Windows color picker.
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
            "Pick a color", !matched);
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

    /// <summary>"Automatic" swatch, split diagonally between light and dark.</summary>
    public static Brush SplitBrush(Color a, Color b)
    {
        var brush = new LinearGradientBrush(new GradientStopCollection
        {
            new(a, 0), new(a, 0.5), new(b, 0.5), new(b, 1),
        }, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>A one-line text input.</summary>
    public static TextBox Input(string text, string name, Action<string> changed, double width = 260)
    {
        var box = new TextBox { Text = text, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        box.SetResourceReference(FrameworkElement.StyleProperty, "Input");
        AutomationProperties.SetName(box, name);
        box.TextChanged += (_, _) => changed(box.Text);
        return box;
    }

    /// <summary>A list of switches, one per value; reports the set of checked values.</summary>
    public static FrameworkElement CheckList(IEnumerable<(string Value, string Label)> items, ISet<string> selected,
        Action<ISet<string>> changed)
    {
        var current = new HashSet<string>(selected);
        var panel = new StackPanel();
        foreach (var (value, label) in items)
        {
            var box = new CheckBox { Content = label, IsChecked = current.Contains(value), Margin = new Thickness(0, 0, 0, 10) };
            box.SetResourceReference(FrameworkElement.StyleProperty, "Switch");
            box.Click += (_, _) =>
            {
                if (box.IsChecked == true) current.Add(value);
                else current.Remove(value);
                changed(new HashSet<string>(current));
            };
            panel.Children.Add(box);
        }
        return panel;
    }

    /// <summary>The chosen folder and a button that opens the Windows folder picker.</summary>
    public static FrameworkElement FolderPicker(string path, Action<string> changed)
    {
        var label = Text(path, "Text.Body");
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.TextWrapping = TextWrapping.NoWrap;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ToolTip = new ToolTip { Content = path };

        var choose = Button("Choose folder…", "Button.Secondary", () =>
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Choose a folder with photos",
                UseDescriptionForTitle = true,
                SelectedPath = path,
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            path = dialog.SelectedPath;
            label.Text = path;
            label.ToolTip = new ToolTip { Content = path };
            changed(path);
        });
        choose.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(choose, Dock.Right);
        return new DockPanel { Children = { choose, label } };
    }

    /// <summary>
    /// Date (yyyy-MM-dd) and time (HH:mm) inputs. Valid values are reported right away; an invalid
    /// one is shown in the caption and ignored until it is fixed.
    /// </summary>
    public static FrameworkElement DateTimePicker(DateTime value, Action<DateTime> changed)
    {
        var caption = Text("", "Text.Caption", new Thickness(0, 6, 0, 0));
        TextBox date = null!, time = null!;

        void Update()
        {
            bool okDate = DateTime.TryParseExact(date.Text.Trim(), "yyyy-MM-dd", Fonts.Culture,
                System.Globalization.DateTimeStyles.None, out DateTime d);
            bool okTime = TimeSpan.TryParseExact(time.Text.Trim(), @"h\:mm", Fonts.Culture, out TimeSpan t)
                && t < TimeSpan.FromDays(1);
            if (!okDate || !okTime)
            {
                caption.Text = !okDate ? "Write the date as year-month-day, e.g. 2026-12-25." : "Write the time as hours:minutes, e.g. 09:30.";
                caption.SetResourceReference(TextBlock.ForegroundProperty, "Danger");
                return;
            }
            DateTime result = d.Date + t;
            caption.Text = result.ToString("dddd, MMMM d, yyyy 'at' HH:mm", Fonts.Culture);
            caption.SetResourceReference(TextBlock.ForegroundProperty, "Ink.Muted");
            changed(result);
        }

        date = Input(value.ToString("yyyy-MM-dd", Fonts.Culture), "Date", _ => Update(), 140);
        time = Input(value.ToString("HH:mm", Fonts.Culture), "Time", _ => Update(), 90);
        time.Margin = new Thickness(10, 0, 0, 0);
        Update();
        return new StackPanel
        {
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { date, time } },
                caption,
            },
        };
    }
}

/// <summary>Cork board on which a widget is shown "pinned" with a push pin.</summary>
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
