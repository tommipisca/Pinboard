using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pinboard;

/// <summary>Transparent borderless window that holds a widget on the desktop.</summary>
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
        // No ShowInTaskbar = false: WPF would give the window a hidden owner, and an owned window
        // always stays above its owner, so it could not be sent down to the desktop.
        // WS_EX_TOOLWINDOW (DesktopHost) keeps it out of the taskbar and Alt+Tab.
        ShowActivated = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Rebuild();
    }

    public WidgetInstance Instance { get; }
    public WidgetDefinition Definition { get; }

    internal IntPtr Handle => _hwnd;
    internal bool Editing { get; private set; }

    /// <summary>Redraws the widget after a change of settings or theme.</summary>
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
        // If Explorer restarts, the desktop changes: the widgets must be put back in place.
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
        App.Log($"{Instance.Type}/{Instance.Id}: placed at {x},{y} ({w}x{h}px)");
    }

    /// <summary>Makes the widget draggable. It stays on the desktop, below the other windows.</summary>
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
        // Drag finished: remember the spot and put the widget back just above the desktop,
        // in case moving it brought it above the other windows.
        DesktopHost.GetWindowRect(_hwnd, out var r);
        Instance.Position = new[] { r.Left, r.Top };
        WidgetManager.Settings.Save();
        Place();
    }
}
