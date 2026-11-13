using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Pinboard;

/// <summary>Gestisce i widget aperti, il loro aggiornamento e il menu nell'area di notifica.</summary>
internal static class WidgetManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Pinboard";

    private static readonly List<WidgetWindow> Widgets = new();
    private static readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private static Forms.NotifyIcon? _tray;
    private static Forms.ToolStripMenuItem? _moveItem;
    private static bool _editing;

    public static readonly uint TaskbarCreatedMessage = DesktopHost.RegisterWindowMessage("TaskbarCreated");
    public static WidgetSettings Settings { get; } = WidgetSettings.Load();

    public static void Start(params WidgetWindow[] widgets)
    {
        Widgets.AddRange(widgets);
        CreateTrayIcon();

        foreach (var w in Widgets) w.Show();

        _timer.Tick += (_, _) =>
        {
            foreach (var w in Widgets) w.OnTick();
            if (!App.Floating)
                DesktopHost.KeepVisible(Widgets.Where(w => !w.Editing).Select(w => w.Handle).ToList());
        };
        _timer.Start();
    }

    public static void Shutdown()
    {
        _timer.Stop();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }

    /// <summary>Posizione salvata, oppure una predefinita: in fila dall'angolo in alto a destra.</summary>
    public static (int x, int y) PositionFor(WidgetWindow widget)
    {
        if (Settings.Positions.TryGetValue(widget.Id, out int[]? p) && p.Length == 2 && IsOnScreen(p[0], p[1]))
            return (p[0], p[1]);

        double scale = System.Windows.Media.VisualTreeHelper.GetDpi(widget).DpiScaleX;
        int margin = (int)Math.Round(48 * scale);
        int gap = (int)Math.Round(16 * scale);
        int x = DesktopHost.GetSystemMetrics(0) - margin; // larghezza dello schermo principale

        foreach (var w in Widgets)
        {
            x -= w.SizeInPixels().w;
            if (w == widget) return (x, margin);
            x -= gap;
        }
        return (margin, margin);
    }

    private static bool IsOnScreen(int x, int y)
    {
        int left = DesktopHost.GetSystemMetrics(76), top = DesktopHost.GetSystemMetrics(77);
        int width = DesktopHost.GetSystemMetrics(78), height = DesktopHost.GetSystemMetrics(79);
        return x >= left && y >= top && x < left + width - 40 && y < top + height - 40;
    }

    public static void ReplaceAll()
    {
        foreach (var w in Widgets.Where(w => !w.Editing)) w.Place();
    }

    // --- Menu nell'area di notifica ---------------------------------------------------------

    private static void CreateTrayIcon()
    {
        _moveItem = new Forms.ToolStripMenuItem("Sposta widget", null, (_, _) => ToggleEditMode());

        var startup = new Forms.ToolStripMenuItem("Avvia con Windows")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled(),
        };
        startup.Click += (_, _) => SetStartup(startup.Checked);

        var exit = new Forms.ToolStripMenuItem("Esci", null, (_, _) => Application.Current.Shutdown());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_moveItem);
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Pinboard",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private static void ToggleEditMode()
    {
        if (!_editing)
        {
            foreach (var w in Widgets) w.BeginEdit();
            _moveItem!.Text = "Blocca posizione";
        }
        else
        {
            foreach (var w in Widgets) w.EndEdit();
            Settings.Save();
            _moveItem!.Text = "Sposta widget";
        }
        _editing = !_editing;
    }

    private static bool IsStartupEnabled()
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

    private static void SetStartup(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is string path)
                key.SetValue(RunValueName, $"\"{path}\"");
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            App.Log(ex.ToString(), force: true);
        }
    }
}
