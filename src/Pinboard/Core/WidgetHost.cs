using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pinboard;

/// <summary>Finestra trasparente senza bordi che tiene un widget sul desktop.</summary>
public sealed class WidgetHost : Window
{
    private IntPtr _hwnd;
    private WidgetView _view = null!;

    public WidgetHost(WidgetInstance instance, WidgetDefinition definition)
    {
        Instance = instance;
        Definition = definition;
        Title = "Pinboard " + definition.Name;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        // Niente ShowInTaskbar = false: WPF darebbe alla finestra un proprietario nascosto e una finestra
        // con proprietario resta sempre sopra di lui, quindi non si potrebbe portare giu' fino al desktop.
        // Fuori dalla barra delle applicazioni e da Alt+Tab la tiene WS_EX_TOOLWINDOW (DesktopHost).
        ShowActivated = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Rebuild();
    }

    public WidgetInstance Instance { get; }
    public WidgetDefinition Definition { get; }

    internal IntPtr Handle => _hwnd;
    internal bool Editing { get; private set; }

    /// <summary>Ridisegna il widget dopo un cambio di impostazioni o di tema.</summary>
    public void Rebuild()
    {
        WidgetSize size = Definition.SizeOf(Instance);
        Width = size.Width;
        Height = size.Height;
        _view = Definition.CreateView(Instance);
        Content = _view;
        if (_hwnd != IntPtr.Zero) Place();
    }

    public void Tick() => _view.Tick();

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
        if ((uint)msg == WidgetManager.TaskbarCreatedMessage)
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
        App.Log($"{Instance.Type}/{Instance.Id}: posizionato in {x},{y} ({w}x{h}px)");
    }

    /// <summary>Rende il widget trascinabile. Resta sul desktop, sotto le altre finestre.</summary>
    internal void BeginEdit()
    {
        if (Editing) return;
        Editing = true;
        Cursor = Cursors.SizeAll;
        MouseLeftButtonDown += OnDrag;
    }

    internal void EndEdit()
    {
        if (!Editing) return;
        MouseLeftButtonDown -= OnDrag;
        Cursor = null;
        Editing = false;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        // Finito il trascinamento: si ricorda il punto e si rimette il widget subito sopra il desktop,
        // nel caso lo spostamento l'avesse portato sopra le altre finestre.
        DesktopHost.GetWindowRect(_hwnd, out var r);
        Instance.Position = new[] { r.Left, r.Top };
        WidgetManager.Settings.Save();
        Place();
    }
}
