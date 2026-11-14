using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Pinboard;

/// <summary>The widgets on the desktop: creates, updates and saves them. Also handles the notification area icon.</summary>
internal static class WidgetManager
{
    private static readonly List<WidgetHost> Hosts = new();
    private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private static Forms.NotifyIcon? _tray;
    private static Forms.ToolStripMenuItem? _moveItem;
    private static bool _lightTheme;

    public static readonly uint TaskbarCreatedMessage = DesktopHost.RegisterWindowMessage("TaskbarCreated");
    public static AppSettings Settings { get; } = AppSettings.Load();

    public static bool Editing { get; private set; }

    /// <summary>Widgets added, removed or changed, or "move" mode toggled.</summary>
    public static event Action? Changed;

    /// <summary>The Windows light/dark theme changed.</summary>
    public static event Action? ThemeChanged;

    public static IReadOnlyList<WidgetInstance> Widgets => Settings.Widgets;

    public static void Start()
    {
        _lightTheme = SystemTheme.AppsUseLight();
        CreateTrayIcon();

        foreach (WidgetInstance w in Settings.Widgets.ToList())
        {
            if (WidgetCatalog.Find(w.Type) is WidgetDefinition def)
                Open(w, def);
            else
                App.Log($"Ignoring widget of unknown type '{w.Type}'.", force: true);
        }

        Timer.Tick += (_, _) =>
        {
            foreach (var h in Hosts) h.Tick();
            if (!App.Floating)
                DesktopHost.KeepVisible(Hosts.Select(h => h.Handle).ToList());
        };
        Timer.Start();

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.General) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(CheckTheme));
        };
    }

    public static void Shutdown()
    {
        Timer.Stop();
        if (Editing) SetEditing(false);
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }

    private static void CheckTheme()
    {
        bool light = SystemTheme.AppsUseLight();
        if (light == _lightTheme) return;
        _lightTheme = light;
        foreach (var h in Hosts.Where(h => h.Instance.Theme == "system")) h.Rebuild();
        ThemeChanged?.Invoke();
    }

    // --- Widgets ------------------------------------------------------------------------

    public static void Add(WidgetInstance w)
    {
        if (WidgetCatalog.Find(w.Type) is not WidgetDefinition def) return;
        w.Position = null;
        Settings.Widgets.Add(w);
        WidgetHost host = Open(w, def);
        if (Editing) host.BeginEdit();
        Settings.Save();
        Changed?.Invoke();
    }

    /// <summary>Applies the customizations of <paramref name="edited"/> to the widget with the same id.</summary>
    public static void Update(WidgetInstance edited)
    {
        WidgetHost? host = Hosts.FirstOrDefault(h => h.Instance.Id == edited.Id);
        if (host == null) return;
        WidgetInstance w = host.Instance;
        w.Size = edited.Size;
        w.Theme = edited.Theme;
        w.Background = edited.Background;
        w.Accent = edited.Accent;
        w.Opacity = edited.Opacity;
        w.Options = new Dictionary<string, string>(edited.Options);
        host.Rebuild();
        Settings.Save();
        Changed?.Invoke();
    }

    public static void Remove(string id)
    {
        WidgetHost? host = Hosts.FirstOrDefault(h => h.Instance.Id == id);
        if (host != null)
        {
            Hosts.Remove(host);
            host.Close();
        }
        Settings.Widgets.RemoveAll(w => w.Id == id);
        Settings.Save();
        Changed?.Invoke();
    }

    private static WidgetHost Open(WidgetInstance w, WidgetDefinition def)
    {
        var host = new WidgetHost(w, def);
        Hosts.Add(host);
        host.Show();
        // Show() brings the window on top of the others: put it back just above the desktop afterwards.
        host.Place();
        return host;
    }

    // --- Favorites ----------------------------------------------------------------------

    public static bool IsFavorite(string type) => Settings.Favorites.Contains(type);

    public static void SetFavorite(string type, bool favorite)
    {
        Settings.Favorites.Remove(type);
        if (favorite) Settings.Favorites.Add(type);
        Settings.Save();
    }

    // --- Positions ----------------------------------------------------------------------

    /// <summary>Saved position; if missing, the first free spot starting from the top right.</summary>
    public static (int x, int y) PositionFor(WidgetHost host)
    {
        if (host.Instance.Position is { Length: 2 } p && IsOnScreen(p[0], p[1]))
            return (p[0], p[1]);

        var (x, y) = FindFreeSpot(host);
        host.Instance.Position = new[] { x, y };
        Settings.Save();
        return (x, y);
    }

    private static (int x, int y) FindFreeSpot(WidgetHost host)
    {
        double scale = System.Windows.Media.VisualTreeHelper.GetDpi(host).DpiScaleX;
        Rect area = SystemParameters.WorkArea;
        int left = (int)(area.Left * scale), top = (int)(area.Top * scale);
        int right = (int)(area.Right * scale), bottom = (int)(area.Bottom * scale);
        int margin = (int)(40 * scale), gap = (int)(16 * scale), step = (int)Math.Max(4, 8 * scale);
        var (w, h) = host.SizeInPixels();

        var taken = Hosts
            .Where(o => o != host && o.Instance.Position is { Length: 2 })
            .Select(o =>
            {
                var (ow, oh) = o.SizeInPixels();
                return (x: o.Instance.Position![0], y: o.Instance.Position[1], w: ow, h: oh);
            })
            .ToList();

        for (int y = top + margin; y + h <= bottom - margin; y += step)
        {
            for (int x = right - margin - w; x >= left + margin; x -= step)
            {
                bool free = taken.All(t =>
                    x + w + gap <= t.x || t.x + t.w + gap <= x || y + h + gap <= t.y || t.y + t.h + gap <= y);
                if (free) return (x, y);
            }
        }
        return (right - margin - w, top + margin);
    }

    private static bool IsOnScreen(int x, int y)
    {
        int left = DesktopHost.GetSystemMetrics(76), top = DesktopHost.GetSystemMetrics(77);
        int width = DesktopHost.GetSystemMetrics(78), height = DesktopHost.GetSystemMetrics(79);
        return x >= left && y >= top && x < left + width - 40 && y < top + height - 40;
    }

    /// <summary>Makes the widgets draggable, or locks them again and saves their positions.</summary>
    public static void SetEditing(bool editing)
    {
        if (editing == Editing) return;
        Editing = editing;
        foreach (var h in Hosts)
        {
            if (editing) h.BeginEdit();
            else h.EndEdit();
        }
        if (!editing) Settings.Save();
        if (_moveItem != null) _moveItem.Text = editing ? "Lock position" : "Move widgets";
        Changed?.Invoke();
    }

    // --- Notification area --------------------------------------------------------------

    private static void CreateTrayIcon()
    {
        var open = new Forms.ToolStripMenuItem("Open Pinboard", null, (_, _) => App.ShowDashboard());
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        _moveItem = new Forms.ToolStripMenuItem("Move widgets", null, (_, _) => SetEditing(!Editing));
        var update = new Forms.ToolStripMenuItem("Check for updates", null, (_, _) =>
        {
            if (Updater.Status == UpdateStatus.Ready) Updater.RestartAndUpdate();
            else
            {
                App.ShowDashboard();
                App.ShowSettings();
                _ = Updater.CheckAsync();
            }
        });
        var exit = new Forms.ToolStripMenuItem("Quit", null, (_, _) => App.Quit());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(open);
        menu.Items.Add(_moveItem);
        menu.Items.Add(update);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);
        menu.Opening += (_, _) =>
        {
            update.Visible = Updater.Status != UpdateStatus.Unsupported;
            update.Text = Updater.Status == UpdateStatus.Ready
                ? $"Restart and update to {Updater.NewVersion}"
                : "Check for updates";
        };

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Pinboard",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) App.ShowDashboard();
        };
    }

    public static void ShowTrayTip(string title, string text) =>
        _tray?.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.None);

    /// <summary>The executable's icon (Assets\Pinboard.ico); the default one if missing.</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            if (Environment.ProcessPath is string path &&
                System.Drawing.Icon.ExtractAssociatedIcon(path) is System.Drawing.Icon icon)
                return icon;
        }
        catch
        {
            // Use the default icon.
        }
        return System.Drawing.SystemIcons.Application;
    }
}

/// <summary>Start with Windows (the user's Run key).</summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Pinboard";
    public const string BackgroundArg = "--background";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is string path)
                key.SetValue(RunValueName, $"\"{path}\" {BackgroundArg}");
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            App.Log(ex.ToString(), force: true);
        }
    }

    /// <summary>If start with Windows is on, refreshes path and arguments (the exe may have moved).</summary>
    public static void Refresh()
    {
        if (IsEnabled()) Set(true);
    }
}
