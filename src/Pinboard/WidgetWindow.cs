using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pinboard;

/// <summary>Base dei widget: finestra trasparente senza bordi, posizionata sul desktop.</summary>
public abstract class WidgetWindow : Window
{
    private IntPtr _hwnd;

    protected WidgetWindow(string id, double width, double height)
    {
        Id = id;
        Title = "Pinboard " + id;
        Width = width;
        Height = height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
    }

    public string Id { get; }

    internal IntPtr Handle => _hwnd;
    internal bool Editing { get; private set; }

    /// <summary>Chiamato quattro volte al secondo: aggiorna il contenuto.</summary>
    public virtual void OnTick() { }

    /// <summary>Scheda con angoli arrotondati, in stile widget di macOS.</summary>
    protected static Border CreateCard(UIElement content) => new()
    {
        CornerRadius = new CornerRadius(22),
        Background = Theme.Card,
        BorderBrush = Theme.CardBorder,
        BorderThickness = new Thickness(1),
        Child = content,
    };

    internal (int w, int h) SizeInPixels()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(Width * dpi.DpiScaleX), (int)Math.Round(Height * dpi.DpiScaleY));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        Place();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Se Explorer si riavvia, il desktop cambia: i widget vanno rimessi al loro posto.
        if ((uint)msg == WidgetManager.TaskbarCreatedMessage && !Editing)
            Dispatcher.BeginInvoke(new Action(Place));
        return IntPtr.Zero;
    }

    internal void Place()
    {
        var (x, y) = WidgetManager.PositionFor(this);
        var (w, h) = SizeInPixels();
        if (App.Floating)
            DesktopHost.Detach(_hwnd, x, y, w, h);
        else
            DesktopHost.PlaceAboveDesktop(_hwnd, x, y, w, h);
        App.Log($"{Id}: posizionato in {x},{y} ({w}x{h}px)");
    }

    /// <summary>Stacca il widget dal desktop: diventa una finestra normale da trascinare.</summary>
    internal void BeginEdit()
    {
        DesktopHost.GetWindowRect(_hwnd, out var r);
        var (w, h) = SizeInPixels();
        DesktopHost.Detach(_hwnd, r.Left, r.Top, w, h);
        Editing = true;
        MouseLeftButtonDown += OnDrag;
    }

    internal void EndEdit()
    {
        MouseLeftButtonDown -= OnDrag;
        DesktopHost.GetWindowRect(_hwnd, out var r);
        WidgetManager.Settings.Positions[Id] = new[] { r.Left, r.Top };
        Editing = false;
        Place();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
