using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Pinboard.Clock;

public partial class MainWindow : Window
{
    private static readonly CultureInfo Italian = new("it-IT");

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly WidgetSettings _settings = WidgetSettings.Load();
    private readonly uint _taskbarCreated = DesktopHost.RegisterWindowMessage("TaskbarCreated");

    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _moveItem;
    private IntPtr _hwnd;
    private bool _editing;

    internal static bool Floating { get; set; }

    /// <summary>--child: vecchio metodo (figlio di Progman). Predefinito: finestra di primo livello.</summary>
    internal static bool AsChild { get; set; }

    /// <summary>Senza finestra "layered": sfondo pieno e angoli ritagliati con una regione.</summary>
    internal static bool Opaque { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        if (Opaque)
        {
            AllowsTransparency = false;
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x1F));
            Card.CornerRadius = new CornerRadius(0);
            Card.Background = Brushes.Transparent;
            Card.BorderThickness = new Thickness(0);
        }
        _timer.Tick += (_, _) =>
        {
            UpdateText();
            if (!Floating && !AsChild && !_editing && _hwnd != IntPtr.Zero)
                DesktopHost.KeepVisible(_hwnd);
        };
        UpdateText();
        _timer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        CreateTrayIcon();

        if (Floating)
        {
            Topmost = true;
            App.Log("Modalita' --floating: nessun aggancio al desktop.");
            return;
        }

        AttachToDesktop();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tray?.Dispose();
        base.OnClosed(e);
    }

    // --- Orologio -------------------------------------------------------------------------

    private void UpdateText()
    {
        DateTime now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm", Italian);
        DateText.Text = now.ToString("dddd d MMMM", Italian);
    }

    // --- Posizione e aggancio -------------------------------------------------------------

    private (int w, int h) SizeInPixels()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(Width * dpi.DpiScaleX), (int)Math.Round(Height * dpi.DpiScaleY));
    }

    private (int x, int y) CurrentOrDefaultPosition()
    {
        if (_settings.X is int x && _settings.Y is int y) return (x, y);

        // Predefinito: angolo in alto a destra dello schermo principale, con un po' di margine.
        var (w, _) = SizeInPixels();
        int screenW = DesktopHost.GetSystemMetrics(0);
        int margin = (int)Math.Round(48 * VisualTreeHelper.GetDpi(this).DpiScaleX);
        return (screenW - w - margin, margin);
    }

    private void AttachToDesktop()
    {
        var (x, y) = CurrentOrDefaultPosition();
        var (w, h) = SizeInPixels();
        if (AsChild)
            DesktopHost.Attach(_hwnd, x, y, w, h);
        else
            DesktopHost.PlaceAboveDesktop(_hwnd, x, y, w, h);
        if (Opaque)
            DesktopHost.ApplyRoundedRegion(_hwnd, w, h,
                (int)Math.Round(26 * VisualTreeHelper.GetDpi(this).DpiScaleX));
        App.Log($"Agganciato al desktop in {x},{y} ({w}x{h}px).");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Se Explorer si riavvia, il livello del desktop cambia: bisogna riagganciarsi.
        if ((uint)msg == _taskbarCreated && !_editing)
            Dispatcher.BeginInvoke(AttachToDesktop);
        return IntPtr.Zero;
    }

    // --- Menu nell'area di notifica --------------------------------------------------------

    private void CreateTrayIcon()
    {
        _moveItem = new Forms.ToolStripMenuItem("Sposta widget", null, (_, _) => ToggleEditMode());
        var exit = new Forms.ToolStripMenuItem("Esci", null, (_, _) => Application.Current.Shutdown());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_moveItem);
        menu.Items.Add(exit);

        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Pinboard Clock",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private void ToggleEditMode()
    {
        if (!_editing)
        {
            // Stacca il widget dal desktop: diventa una finestra normale che si trascina.
            DesktopHost.GetWindowRect(_hwnd, out var r);
            var (w, h) = SizeInPixels();
            DesktopHost.Detach(_hwnd, r.Left, r.Top, w, h);
            _editing = true;
            _moveItem!.Text = "Blocca posizione";
            MouseLeftButtonDown += OnDrag;
        }
        else
        {
            MouseLeftButtonDown -= OnDrag;
            DesktopHost.GetWindowRect(_hwnd, out var r);
            _settings.X = r.Left;
            _settings.Y = r.Top;
            _settings.Save();

            DesktopHost.ClearTopmost(_hwnd);
            _editing = false;
            _moveItem!.Text = "Sposta widget";
            AttachToDesktop();
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
