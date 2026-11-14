using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Pinboard;

/// <summary>I widget sul desktop: li crea, li aggiorna, li salva. Gestisce anche l'icona nell'area di notifica.</summary>
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

    /// <summary>Widget aggiunti, tolti o modificati, o modalita' "sposta" cambiata.</summary>
    public static event Action? Changed;

    /// <summary>Il tema chiaro/scuro di Windows e' cambiato.</summary>
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
                App.Log($"Widget di tipo sconosciuto '{w.Type}' ignorato.", force: true);
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

    // --- Widget -------------------------------------------------------------------------

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

    /// <summary>Applica le personalizzazioni di <paramref name="edited"/> al widget con lo stesso id.</summary>
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
        // Show() porta la finestra in cima alle altre: va rimessa subito sopra il desktop dopo.
        host.Place();
        return host;
    }

    // --- Favoriti -----------------------------------------------------------------------

    public static bool IsFavorite(string type) => Settings.Favorites.Contains(type);

    public static void SetFavorite(string type, bool favorite)
    {
        Settings.Favorites.Remove(type);
        if (favorite) Settings.Favorites.Add(type);
        Settings.Save();
    }

    // --- Posizioni ----------------------------------------------------------------------

    /// <summary>Posizione salvata; se manca, il primo spazio libero partendo dall'alto a destra.</summary>
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

    /// <summary>Stacca i widget dal desktop per trascinarli, o li rimette al loro posto salvando le posizioni.</summary>
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
        if (_moveItem != null) _moveItem.Text = editing ? "Blocca posizione" : "Sposta widget";
        Changed?.Invoke();
    }

    // --- Area di notifica ---------------------------------------------------------------

    private static void CreateTrayIcon()
    {
        var open = new Forms.ToolStripMenuItem("Apri Pinboard", null, (_, _) => App.ShowDashboard());
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        _moveItem = new Forms.ToolStripMenuItem("Sposta widget", null, (_, _) => SetEditing(!Editing));
        var exit = new Forms.ToolStripMenuItem("Esci", null, (_, _) => App.Quit());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(open);
        menu.Items.Add(_moveItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

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

    /// <summary>L'icona dell'eseguibile (Assets\Pinboard.ico); se manca, quella predefinita.</summary>
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
            // Si usa l'icona predefinita.
        }
        return System.Drawing.SystemIcons.Application;
    }
}

/// <summary>Avvio automatico con Windows (chiave Run dell'utente).</summary>
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

    /// <summary>Se l'avvio automatico e' attivo, aggiorna percorso e argomenti (l'exe puo' essere stato spostato).</summary>
    public static void Refresh()
    {
        if (IsEnabled()) Set(true);
    }
}
