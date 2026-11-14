using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pinboard;

/// <summary>The main window: catalog, widgets on the desktop and settings.</summary>
public partial class DashboardWindow : Window
{
    private const string AllFilter = "all";
    private const string FavoritesFilter = "favorites";

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly List<WidgetView> _live = new();
    private string _filter = AllFilter;
    private string _search = "";
    private ContentControl? _cards;
    private Action? _reshow;

    public DashboardWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) =>
        {
            foreach (var v in _live) v.Tick();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _timer.Start();
            else _timer.Stop();
        };

        WidgetManager.Changed += OnWidgetsChanged;
        WidgetManager.ThemeChanged += OnThemeChanged;
        Updater.Changed += () =>
        {
            if (NavSettings.IsChecked == true) _reshow?.Invoke();
        };
        UpdateCount();

        if (WidgetManager.Widgets.Count > 0) NavDesktop.IsChecked = true;
        else NavCatalog.IsChecked = true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UiTheme.ApplyChrome(this);
    }

    /// <summary>Closing the window only hides it: the widgets stay on the desktop.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.Exiting) return;
        e.Cancel = true;
        Hide();
        if (!WidgetManager.Settings.CloseHintShown)
        {
            WidgetManager.Settings.CloseHintShown = true;
            WidgetManager.Settings.Save();
            WidgetManager.ShowTrayTip("Pinboard is still running",
                "Your widgets stay on the desktop. To open Pinboard again, click its icon next to the clock.");
        }
    }

    public void ShowCatalogPage() => NavCatalog.IsChecked = true;

    public void ShowSettingsPage() => NavSettings.IsChecked = true;

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (sender == NavCatalog) ShowCatalog();
        else if (sender == NavDesktop) ShowDesktop();
        else if (sender == NavSettings) ShowSettings();
    }

    private void OnWidgetsChanged()
    {
        UpdateCount();
        if (NavDesktop.IsChecked == true || NavSettings.IsChecked == true) _reshow?.Invoke();
    }

    private void OnThemeChanged()
    {
        UiTheme.Apply();
        UiTheme.ApplyChrome(this);
        _reshow?.Invoke();
    }

    private void UpdateCount()
    {
        int n = WidgetManager.Widgets.Count;
        CountText.Text = n.ToString(Fonts.Culture);
        CountBadge.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearLive()
    {
        foreach (var v in _live) v.Detach();
        _live.Clear();
    }

    private void SetPage(FrameworkElement content, Action reshow)
    {
        ClearLive();
        _cards = null;
        _reshow = reshow;
        Page.Content = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
    }

    private CorkStage Stage(WidgetDefinition def, WidgetInstance w, double padding, double radius = 12)
    {
        var stage = new CorkStage(padding, radius);
        WidgetView view = def.CreateView(w);
        _live.Add(view);
        stage.Show(view);
        return stage;
    }

    private static FrameworkElement Header(string title, string caption, UIElement? right = null)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 22), LastChildFill = true };
        if (right != null)
        {
            DockPanel.SetDock(right, Dock.Right);
            if (right is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Bottom;
            dock.Children.Add(right);
        }
        dock.Children.Add(new StackPanel
        {
            Children =
            {
                UiKit.Text(title, "Text.Title"),
                UiKit.Text(caption, "Text.Caption", new Thickness(0, 4, 24, 0)),
            },
        });
        return dock;
    }

    // --- Catalog ------------------------------------------------------------------------

    private void ShowCatalog()
    {
        var search = new TextBox { Tag = "Search widgets", Width = 260, Text = _search };
        search.SetResourceReference(StyleProperty, "SearchBox");
        System.Windows.Automation.AutomationProperties.SetName(search, "Search widgets");
        search.TextChanged += (_, _) =>
        {
            _search = search.Text;
            FillCards();
        };

        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 18) };
        void Chip(string value, string label)
        {
            var rb = new RadioButton { Content = label, GroupName = "filter", IsChecked = _filter == value };
            rb.SetResourceReference(StyleProperty, "Chip");
            rb.Checked += (_, _) =>
            {
                _filter = value;
                FillCards();
            };
            chips.Children.Add(rb);
        }
        Chip(AllFilter, "All");
        Chip(FavoritesFilter, "Favorites");
        foreach (string category in WidgetCatalog.Categories) Chip("cat:" + category, category);

        var page = new StackPanel { Margin = new Thickness(40, 32, 40, 32) };
        page.Children.Add(Header("Catalog", "Pick a widget and put it on your desktop.", search));
        page.Children.Add(chips);
        SetPage(page, ShowCatalog);

        _cards = new ContentControl { Focusable = false };
        page.Children.Add(_cards);
        FillCards();
    }

    private void FillCards()
    {
        if (_cards == null) return;
        ClearLive();

        string q = Cities.Normalize(_search);
        var defs = WidgetCatalog.All.Where(d =>
            (_filter == AllFilter
             || (_filter == FavoritesFilter && WidgetManager.IsFavorite(d.Type))
             || _filter == "cat:" + d.Category)
            && (q.Length == 0 || Cities.Normalize($"{d.Name} {d.Category} {d.Description}").Contains(q)))
            .ToList();

        if (defs.Count == 0)
        {
            string message = q.Length > 0
                ? $"No widgets match “{_search.Trim()}”. Try another word."
                : _filter == FavoritesFilter
                    ? "You have no favorites yet. Tap the star on a widget to find it here."
                    : "No widgets in this category.";
            _cards.Content = UiKit.Text(message, "Text.Body", new Thickness(0, 24, 0, 0));
            return;
        }

        var wrap = new WrapPanel();
        foreach (WidgetDefinition def in defs) wrap.Children.Add(Card(def));
        _cards.Content = wrap;
    }

    private FrameworkElement Card(WidgetDefinition def)
    {
        var stage = Stage(def, def.CreateInstance(), 22);
        stage.Width = 264;
        stage.Height = 184;

        var body = new StackPanel
        {
            Children =
            {
                stage,
                UiKit.Text(def.Name, "Text.Label", new Thickness(2, 12, 0, 0)),
                UiKit.Text(string.Join(" or ", def.Sizes.Select(s => s.Name.ToLower(Fonts.Culture))).Capitalize(),
                    "Text.Caption", new Thickness(2, 2, 0, 0)),
            },
        };
        var open = new Button { Content = body };
        open.SetResourceReference(StyleProperty, "Button.Card");
        System.Windows.Automation.AutomationProperties.SetName(open, $"{def.Name}, open details");
        open.Click += (_, _) => ShowDetail(def);

        var star = UiKit.Star(def.Name, WidgetManager.IsFavorite(def.Type), on =>
        {
            WidgetManager.SetFavorite(def.Type, on);
            if (_filter == FavoritesFilter) FillCards();
        });
        star.HorizontalAlignment = HorizontalAlignment.Right;
        star.VerticalAlignment = VerticalAlignment.Top;
        star.Margin = new Thickness(0, 10, 10, 0);
        star.SetResourceReference(BackgroundProperty, "Bg.Surface");

        return new Grid { Margin = new Thickness(0, 0, 28, 28), Children = { open, star } };
    }

    // --- Details ------------------------------------------------------------------------

    private void ShowDetail(WidgetDefinition def, string? size = null)
    {
        size ??= def.Sizes[0].Id;
        var preview = def.CreateInstance(size);

        var stage = Stage(def, preview, 36, 14);
        stage.Width = 460;
        stage.Height = 320;
        var left = new StackPanel { Children = { stage } };
        if (def.Sizes.Count > 1)
        {
            var sizes = UiKit.Segmented(def.Sizes.Select(s => (s.Id, s.Name)), size, v => ShowDetail(def, v));
            sizes.HorizontalAlignment = HorizontalAlignment.Center;
            sizes.Margin = new Thickness(0, 14, 0, 0);
            left.Children.Add(sizes);
        }

        var star = UiKit.Star(def.Name, WidgetManager.IsFavorite(def.Type), on => WidgetManager.SetFavorite(def.Type, on));
        star.Margin = new Thickness(8, 0, 0, 0);
        star.VerticalAlignment = VerticalAlignment.Center;
        var title = new StackPanel { Orientation = Orientation.Horizontal, Children = { UiKit.Text(def.Name, "Text.Title"), star } };

        var features = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var all = new List<string>();
        if (def.Sizes.Count > 1)
            all.Add("Size: " + string.Join(" or ", def.Sizes.Select(s => s.Name.ToLower(Fonts.Culture))));
        all.Add("Theme, background color and accent color");
        all.Add("Background transparency");
        all.AddRange(def.Features);
        foreach (string f in all)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Margin = new Thickness(2, 9, 12, 0), VerticalAlignment = VerticalAlignment.Top };
            dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Ink.Faint");
            row.Children.Add(dot);
            row.Children.Add(UiKit.Text(f, "Text.Body"));
            features.Children.Add(row);
        }

        var add = UiKit.Button("Add to desktop", "Button.Primary", () => Customize(def, def.CreateInstance(size), isNew: true));
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 28, 0, 0);
        add.Padding = new Thickness(20, 9, 20, 9);

        var right = new StackPanel
        {
            MaxWidth = 420,
            Children =
            {
                title,
                UiKit.Text(def.Category, "Text.Caption", new Thickness(0, 2, 0, 0)),
                UiKit.Text(def.Description, "Text.Body", new Thickness(0, 16, 0, 0)),
                UiKit.Text("What you can customize", "Text.Label", new Thickness(0, 26, 0, 0)),
                features,
                add,
            },
        };

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(right);

        var back = UiKit.Button("Catalog", "Button.Ghost", ShowCatalog);
        back.SetResourceReference(FontFamilyProperty, "Font.Text");
        back.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = "",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 1, 10, 0),
                },
                new TextBlock { Text = "Catalog" },
            },
        };
        System.Windows.Automation.AutomationProperties.SetName(back, "Back to the catalog");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Margin = new Thickness(-10, 0, 0, 18);

        var page = new StackPanel { Margin = new Thickness(40, 24, 40, 32), Children = { back, columns } };
        SetPage(page, () => ShowDetail(def, size));
    }

    private void Customize(WidgetDefinition def, WidgetInstance start, bool isNew)
    {
        var dialog = new CustomizeWindow(def, start, isNew) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (isNew)
        {
            WidgetManager.Add(dialog.Result);
            NavDesktop.IsChecked = true;
        }
        else
        {
            WidgetManager.Update(dialog.Result);
        }
    }

    // --- On your desktop ----------------------------------------------------------------

    private void ShowDesktop()
    {
        bool editing = WidgetManager.Editing;
        var move = UiKit.Button(editing ? "Lock position" : "Move widgets",
            editing ? "Button.Primary" : "Button.Secondary", () => WidgetManager.SetEditing(!WidgetManager.Editing));

        string caption = editing
            ? "Drag the widgets anywhere on your desktop, then press Lock position."
            : "The widgets you put on your desktop. They stay there even when you close this window.";

        var page = new StackPanel { Margin = new Thickness(40, 32, 40, 32) };
        var list = WidgetManager.Widgets.ToList();
        page.Children.Add(Header("On your desktop", caption, list.Count > 0 ? move : null));
        SetPage(page, ShowDesktop);

        if (list.Count == 0)
        {
            var open = UiKit.Button("Open the catalog", "Button.Primary", () => NavCatalog.IsChecked = true);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = new Thickness(0, 18, 0, 0);
            page.Children.Add(new StackPanel
            {
                Margin = new Thickness(0, 30, 0, 0),
                Children =
                {
                    UiKit.Text("Your desktop is still empty", "Text.Heading"),
                    UiKit.Text("Pick a widget from the catalog and add it: you'll find it here to customize it.",
                        "Text.Body", new Thickness(0, 6, 0, 0)),
                    open,
                },
            });
            return;
        }

        foreach (WidgetInstance w in list)
        {
            if (WidgetCatalog.Find(w.Type) is not WidgetDefinition def) continue;
            page.Children.Add(Row(def, w));
        }
    }

    private FrameworkElement Row(WidgetDefinition def, WidgetInstance w)
    {
        var stage = Stage(def, w, 10, 8);
        stage.Width = 150;
        stage.Height = 96;

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 0, 16, 0),
            Children =
            {
                UiKit.Text(def.Name, "Text.Label"),
                UiKit.Text(Describe(def, w), "Text.Caption", new Thickness(0, 3, 0, 0)),
            },
        };

        var customize = UiKit.Button("Customize", "Button.Secondary", () => Customize(def, w, isNew: false));
        var remove = UiKit.Button("Remove", "Button.Ghost", () => WidgetManager.Remove(w.Id));
        remove.SetResourceReference(ForegroundProperty, "Danger");
        remove.Margin = new Thickness(6, 0, 0, 0);
        System.Windows.Automation.AutomationProperties.SetName(customize, $"Customize {def.Name}");
        System.Windows.Automation.AutomationProperties.SetName(remove, $"Remove {def.Name} from the desktop");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { customize, remove },
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(stage);
        grid.Children.Add(text);
        grid.Children.Add(buttons);

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
        };
        card.SetResourceReference(Border.BackgroundProperty, "Bg.SurfaceAlt");
        card.SetResourceReference(Border.BorderBrushProperty, "Stroke");
        return card;
    }

    private static string Describe(WidgetDefinition def, WidgetInstance w)
    {
        var parts = new List<string> { def.SizeOf(w).Name };
        parts.Add(w.Theme switch { "light" => "light theme", "dark" => "dark theme", _ => "Windows theme" });
        int transparency = (int)Math.Round((1 - w.Opacity) * 100);
        parts.Add(transparency == 0 ? "solid background" : $"{transparency}% transparency");
        if (def.Summary(w) is string extra) parts.Add(extra);
        return string.Join(", ", parts);
    }

    // --- Settings -----------------------------------------------------------------------

    private void ShowSettings()
    {
        var startup = new CheckBox { IsChecked = AutoStart.IsEnabled() };
        startup.SetResourceReference(StyleProperty, "Switch");
        System.Windows.Automation.AutomationProperties.SetName(startup, "Start with Windows");
        startup.Click += (_, _) => AutoStart.Set(startup.IsChecked == true);

        var move = UiKit.Button(WidgetManager.Editing ? "Lock position" : "Move widgets",
            WidgetManager.Editing ? "Button.Primary" : "Button.Secondary",
            () => WidgetManager.SetEditing(!WidgetManager.Editing));
        move.IsEnabled = WidgetManager.Widgets.Count > 0;

        var exit = UiKit.Button("Quit Pinboard", "Button.Secondary", () => App.Quit());

        var general = new StackPanel();
        Row(general, "Start with Windows", "Your widgets appear as soon as you sign in, without opening this window.", startup);
        Row(general, "Widget position", "Make the widgets draggable to move them anywhere on your desktop, then lock them.", move);
        Row(general, "Quit", "When you close this window, Pinboard keeps running next to the clock. " +
                    "To close it completely, widgets included, quit.", exit, last: true);

        var updates = new StackPanel();
        bool supported = Updater.Status != UpdateStatus.Unsupported;
        string version = "Version " + Updater.CurrentVersion + (Updater.IsPortable ? " (portable)" : "");
        Row(updates, version, UpdateCaption(), UpdateButton(), last: !supported);
        if (supported)
        {
            var auto = new CheckBox { IsChecked = WidgetManager.Settings.AutoUpdate };
            auto.SetResourceReference(StyleProperty, "Switch");
            System.Windows.Automation.AutomationProperties.SetName(auto, "Check automatically");
            auto.Click += (_, _) =>
            {
                WidgetManager.Settings.AutoUpdate = auto.IsChecked == true;
                WidgetManager.Settings.Save();
                Updater.Schedule();
            };
            Row(updates, "Check automatically",
                "At startup and every 6 hours. New versions download by themselves and install when you quit.",
                auto, last: true);
        }

        var page = new StackPanel
        {
            Margin = new Thickness(40, 32, 40, 32),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                Header("Settings", "How Pinboard behaves on your computer."),
                Card(general),
                UiKit.Text("Updates", "Text.Heading", new Thickness(2, 28, 0, 12)),
                Card(updates),
            },
        };
        SetPage(page, ShowSettings);
    }

    private static void Row(Panel group, string title, string caption, FrameworkElement control, bool last = false)
    {
        control.VerticalAlignment = VerticalAlignment.Center;
        var dock = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };
        DockPanel.SetDock(control, Dock.Right);
        dock.Children.Add(control);
        dock.Children.Add(new StackPanel
        {
            Margin = new Thickness(0, 0, 24, 0),
            Children = { UiKit.Text(title, "Text.Label"), UiKit.Text(caption, "Text.Caption", new Thickness(0, 3, 0, 0)) },
        });
        group.Children.Add(dock);
        if (!last)
        {
            var line = new Border { Height = 1, Margin = new Thickness(20, 0, 20, 0) };
            line.SetResourceReference(Border.BackgroundProperty, "Stroke");
            group.Children.Add(line);
        }
    }

    private static Border Card(UIElement content)
    {
        var card = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = content };
        card.SetResourceReference(Border.BackgroundProperty, "Bg.SurfaceAlt");
        card.SetResourceReference(Border.BorderBrushProperty, "Stroke");
        return card;
    }

    private static string UpdateCaption()
    {
        string last = Updater.LastCheck is DateTime t
            ? $" Last checked {(t.Date == DateTime.Today ? "today" : t.ToString("MMMM d", Fonts.Culture))} at {t:HH:mm}."
            : "";
        return Updater.Status switch
        {
            UpdateStatus.Unsupported =>
                "Updates work in the installed and portable versions downloaded from the releases.",
            UpdateStatus.Checking => "Checking…",
            UpdateStatus.UpToDate => "You have the latest version." + last,
            UpdateStatus.Downloading => $"Downloading version {Updater.NewVersion} ({Updater.Percent}%).",
            UpdateStatus.Ready => $"Version {Updater.NewVersion} is ready: it installs when you quit Pinboard.",
            UpdateStatus.Error => Updater.Error + last,
            _ => "Pinboard checks for new versions by itself.",
        };
    }

    private static Button UpdateButton()
    {
        switch (Updater.Status)
        {
            case UpdateStatus.Unsupported:
                return UiKit.Button("Open releases", "Button.Secondary", () =>
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Updater.ReleasesUrl)
                    {
                        UseShellExecute = true,
                    }));
            case UpdateStatus.Ready:
                return UiKit.Button("Restart and update", "Button.Primary", Updater.RestartAndUpdate);
            default:
                var check = UiKit.Button("Check now", "Button.Secondary", () => _ = Updater.CheckAsync());
                check.IsEnabled = Updater.Status is not (UpdateStatus.Checking or UpdateStatus.Downloading);
                return check;
        }
    }
}

internal static class StringExtensions
{
    public static string Capitalize(this string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0], Fonts.Culture) + s[1..];
}
