using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pinboard;

/// <summary>Window where you choose how a widget looks, before adding it or later on.</summary>
internal sealed class CustomizeWindow : Window
{
    private static readonly (string, string)[] Backgrounds =
    {
        ("#FFFFFF", "White"), ("#2B2F36", "Slate"), ("#1D2B4F", "Ink"),
        ("#DDE5D8", "Sage"), ("#D6E6F5", "Sky"), ("#F6DCDF", "Blush"), ("#C49E73", "Cork"),
    };

    private static readonly (string, string)[] Accents =
    {
        ("#FF9F0A", "Orange"), ("#FF3B30", "Red"), ("#FF2D55", "Pink"), ("#AF52DE", "Purple"),
        ("#0A84FF", "Blue"), ("#30D158", "Green"), ("#00C7BE", "Mint"),
    };

    private readonly WidgetDefinition _def;
    private readonly WidgetInstance _working;
    private readonly CorkStage _stage = new(28);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _transparencyValue;
    private WidgetView? _preview;

    public WidgetInstance Result => _working;

    public CustomizeWindow(WidgetDefinition def, WidgetInstance start, bool isNew)
    {
        _def = def;
        _working = start.Clone();

        Title = (isNew ? "Add " : "Customize ") + def.Name.ToLower(Fonts.Culture);
        Width = 920;
        Height = 620;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Bg.Surface");
        SetResourceReference(FontFamilyProperty, "Font.Text");

        // Preview on the left.
        var left = new Border { Padding = new Thickness(20, 20, 0, 20), Child = _stage };

        // Options on the right.
        var form = new StackPanel { Margin = new Thickness(28, 26, 28, 26) };
        form.Children.Add(UiKit.Text("Appearance", "Text.Heading", new Thickness(0, 0, 0, 18)));

        if (def.Sizes.Count > 1)
            form.Children.Add(UiKit.Field("Size", UiKit.Segmented(
                def.Sizes.Select(s => (s.Id, s.Name)), def.SizeOf(_working).Id,
                v => { _working.Size = v; Refresh(); })));

        form.Children.Add(UiKit.Field("Theme", UiKit.Segmented(
            new[] { ("system", "Like Windows"), ("light", "Light"), ("dark", "Dark") }, _working.Theme,
            v => { _working.Theme = v; Refresh(); })));

        form.Children.Add(UiKit.Field("Background", UiKit.Swatches(
            UiKit.SplitBrush(WidgetStyle.LightCard, WidgetStyle.DarkCard), "Follows the theme", Backgrounds,
            _working.Background, v => { _working.Background = v; Refresh(); }), alignTop: true));

        form.Children.Add(UiKit.Field("Accent color", UiKit.Swatches(
            ColorUtil.Brush(def.DefaultAccent), "Default", Accents.Where(a => a.Item1 != def.DefaultAccent).ToList(),
            _working.Accent, v => { _working.Accent = v; Refresh(); }), alignTop: true));

        _transparencyValue = UiKit.Text("", "Text.Body", new Thickness(14, 0, 0, 0));
        _transparencyValue.Width = 44;
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 80,
            SmallChange = 5,
            LargeChange = 10,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Value = Math.Round((1 - _working.Opacity) * 100 / 5) * 5,
            Width = 240,
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, "Background transparency");
        slider.ValueChanged += (_, e) =>
        {
            _working.Opacity = 1 - e.NewValue / 100;
            ShowTransparency();
            Refresh();
        };
        var sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { slider, _transparencyValue } };
        form.Children.Add(UiKit.Field("Transparency", sliderRow));
        ShowTransparency();

        if (def.CreateOptionsEditor(_working, Refresh) is FrameworkElement editor)
        {
            form.Children.Add(UiKit.Text(def.Name, "Text.Heading", new Thickness(0, 14, 0, 18)));
            form.Children.Add(editor);
        }

        var scroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        // Buttons at the bottom.
        var cancel = UiKit.Button("Cancel", "Button.Secondary", () => DialogResult = false);
        cancel.IsCancel = true;
        var confirm = UiKit.Button(isNew ? "Add to desktop" : "Save changes", "Button.Primary",
            () => DialogResult = true);
        confirm.IsDefault = true;
        confirm.Margin = new Thickness(10, 0, 0, 0);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, confirm },
        };
        var footer = new Border { Padding = new Thickness(24, 14, 24, 14), BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons };
        footer.SetResourceReference(Border.BackgroundProperty, "Bg.SurfaceAlt");
        footer.SetResourceReference(Border.BorderBrushProperty, "Stroke");

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(scroll, 1);
        Grid.SetRow(footer, 1);
        Grid.SetColumnSpan(footer, 2);
        root.Children.Add(left);
        root.Children.Add(scroll);
        root.Children.Add(footer);
        Content = root;

        Refresh();
        _timer.Tick += (_, _) => _preview?.Tick();
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _preview?.Detach();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UiTheme.ApplyChrome(this);
    }

    private void ShowTransparency() =>
        _transparencyValue.Text = $"{Math.Round((1 - _working.Opacity) * 100)}%";

    private void Refresh()
    {
        _preview?.Detach();
        _preview = _def.CreateView(_working);
        _stage.Show(_preview);
    }
}
