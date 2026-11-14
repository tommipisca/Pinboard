using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pinboard;

/// <summary>Finestra in cui si sceglie l'aspetto di un widget, prima di aggiungerlo o in seguito.</summary>
internal sealed class CustomizeWindow : Window
{
    private static readonly (string, string)[] Backgrounds =
    {
        ("#FFFFFF", "Bianco"), ("#2B2F36", "Ardesia"), ("#1D2B4F", "Inchiostro"),
        ("#DDE5D8", "Salvia"), ("#D6E6F5", "Cielo"), ("#F6DCDF", "Cipria"), ("#C49E73", "Sughero"),
    };

    private static readonly (string, string)[] Accents =
    {
        ("#FF9F0A", "Arancione"), ("#FF3B30", "Rosso"), ("#FF2D55", "Rosa"), ("#AF52DE", "Viola"),
        ("#0A84FF", "Blu"), ("#30D158", "Verde"), ("#00C7BE", "Menta"),
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

        Title = (isNew ? "Aggiungi " : "Personalizza ") + def.Name.ToLower(Fonts.Italian);
        Width = 920;
        Height = 620;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Bg.Surface");
        SetResourceReference(FontFamilyProperty, "Font.Text");

        // Anteprima a sinistra.
        var left = new Border { Padding = new Thickness(20, 20, 0, 20), Child = _stage };

        // Opzioni a destra.
        var form = new StackPanel { Margin = new Thickness(28, 26, 28, 26) };
        form.Children.Add(UiKit.Text("Aspetto", "Text.Heading", new Thickness(0, 0, 0, 18)));

        if (def.Sizes.Count > 1)
            form.Children.Add(UiKit.Field("Taglia", UiKit.Segmented(
                def.Sizes.Select(s => (s.Id, s.Name)), def.SizeOf(_working).Id,
                v => { _working.Size = v; Refresh(); })));

        form.Children.Add(UiKit.Field("Tema", UiKit.Segmented(
            new[] { ("system", "Come Windows"), ("light", "Chiaro"), ("dark", "Scuro") }, _working.Theme,
            v => { _working.Theme = v; Refresh(); })));

        form.Children.Add(UiKit.Field("Sfondo", UiKit.Swatches(
            UiKit.SplitBrush(WidgetStyle.LightCard, WidgetStyle.DarkCard), "Segue il tema", Backgrounds,
            _working.Background, v => { _working.Background = v; Refresh(); }), alignTop: true));

        form.Children.Add(UiKit.Field("Colore d'accento", UiKit.Swatches(
            ColorUtil.Brush(def.DefaultAccent), "Predefinito", Accents.Where(a => a.Item1 != def.DefaultAccent).ToList(),
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
        System.Windows.Automation.AutomationProperties.SetName(slider, "Trasparenza dello sfondo");
        slider.ValueChanged += (_, e) =>
        {
            _working.Opacity = 1 - e.NewValue / 100;
            ShowTransparency();
            Refresh();
        };
        var sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { slider, _transparencyValue } };
        form.Children.Add(UiKit.Field("Trasparenza", sliderRow));
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

        // Pulsanti in fondo.
        var cancel = UiKit.Button("Annulla", "Button.Secondary", () => DialogResult = false);
        cancel.IsCancel = true;
        var confirm = UiKit.Button(isNew ? "Aggiungi al desktop" : "Salva modifiche", "Button.Primary",
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
        Closed += (_, _) => _timer.Stop();
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
        _preview = _def.CreateView(_working);
        _stage.Show(_preview);
    }
}
