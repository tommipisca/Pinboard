using System.Runtime.InteropServices;
using System.Text;

namespace Pinboard;

/// <summary>
/// Keeps the widget windows just above the desktop and below all other windows.
/// When "Show desktop" brings the desktop to the foreground, the widgets temporarily become
/// "always on top" so they stay visible.
/// </summary>
internal static class DesktopHost
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_APPWINDOW = 0x00040000;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    private static string ClassOf(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, 256);
        return sb.ToString();
    }

    /// <summary>Top-level window that holds the desktop icons (Progman or WorkerW).</summary>
    public static IntPtr FindDesktopTopLevel()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return progman;

        IntPtr found = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                found = top;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found != IntPtr.Zero ? found : progman;
    }

    /// <summary>Puts the window just above the desktop (screen coordinates, pixels).</summary>
    public static void PlaceAboveDesktop(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);

        // Clear the topmost flag first (set during "Show desktop"): if the window were still topmost,
        // placing it after the desktop would send it below the desktop, hidden.
        SetWindowPos(hwnd, HWND_NOTOPMOST, screenX, screenY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowPos(hwnd, FindDesktopTopLevel(), screenX, screenY, width, height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
        _desktopMode = false;
    }

    /// <summary>Regular always-on-top window, for testing the design (--floating).</summary>
    public static void Detach(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~WS_EX_NOACTIVATE);
        SetWindowPos(hwnd, HWND_TOPMOST, screenX, screenY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static bool _desktopMode;
    private static string _lastForeground = "";

    /// <summary>
    /// Call periodically for all the widgets on the desktop. When the desktop comes to the
    /// foreground (Win+D) the widgets become topmost; when another window becomes active again,
    /// they go back to normal, just above the desktop.
    /// </summary>
    public static void KeepVisible(IReadOnlyCollection<IntPtr> windows)
    {
        if (windows.Count == 0) return;

        IntPtr fg = GetForegroundWindow();
        string fgClass = fg == IntPtr.Zero ? "" : ClassOf(fg);
        bool desktopFocused = fgClass is "Progman" or "WorkerW";

        if (fgClass != _lastForeground)
        {
            App.Log($"Foreground: '{fgClass}'");
            _lastForeground = fgClass;
        }

        foreach (IntPtr hwnd in windows)
            if (IsIconic(hwnd) || !IsWindowVisible(hwnd))
                ShowWindow(hwnd, 4 /* SW_SHOWNOACTIVATE */);

        const uint flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;
        if (desktopFocused && !_desktopMode)
        {
            foreach (IntPtr hwnd in windows) SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);
            _desktopMode = true;
            App.Log("Desktop in the foreground: widgets made topmost.");
        }
        else if (!desktopFocused && _desktopMode)
        {
            IntPtr desktop = FindDesktopTopLevel();
            foreach (IntPtr hwnd in windows)
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                SetWindowPos(hwnd, desktop, 0, 0, 0, 0, flags);
            }
            _desktopMode = false;
            _settleTicks = 8;
            App.Log("Desktop no longer in the foreground: widgets put back above the desktop.");
        }
        else if (!desktopFocused && _settleTicks > 0)
        {
            // When leaving "Show desktop", Windows moves the desktop back to the bottom only after a moment:
            // until then "just after the desktop" is halfway through the windows. For about two seconds
            // the widgets are placed after the desktop again, until it is back in its place.
            _settleTicks--;
            IntPtr desktop = FindDesktopTopLevel();
            foreach (IntPtr hwnd in windows) SetWindowPos(hwnd, desktop, 0, 0, 0, 0, flags);
        }
        else if (desktopFocused)
        {
            _settleTicks = 0;
        }
    }

    private static int _settleTicks;
}
