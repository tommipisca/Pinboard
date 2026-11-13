using System.Runtime.InteropServices;

namespace Pinboard.Clock;

/// <summary>
/// Aggancia una finestra al livello del desktop (quello dello sfondo), cosi' non e' piu' una
/// finestra di primo livello: "Mostra desktop" e Win+D non la minimizzano e non compare in
/// barra delle applicazioni ne' in Alt+Tab.
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

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

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
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT pts, int count);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string name);

    /// <summary>Trova la finestra del desktop sotto le icone, che ospita lo sfondo.</summary>
    public static IntPtr FindDesktopLayer()
    {
        IntPtr progman = FindWindow("Progman", null);
        // Chiede a Explorer di creare il livello WorkerW dello sfondo.
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        // Windows 11 24H2: SHELLDLL_DefView e WorkerW sono entrambi figli di Progman.
        if (FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
        {
            IntPtr child = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (child != IntPtr.Zero) return child;
        }

        // Layout classico: WorkerW e' fratello di quello che contiene SHELLDLL_DefView.
        IntPtr found = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                found = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found != IntPtr.Zero ? found : progman;
    }

    /// <summary>Rende la finestra figlia del desktop e la posiziona (coordinate schermo, pixel).</summary>
    public static void Attach(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        IntPtr layer = FindDesktopLayer();

        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_POPUP) | WS_CHILD);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);

        SetParent(hwnd, layer);

        // Le coordinate di una finestra figlia sono relative al genitore.
        var p = new POINT { X = screenX, Y = screenY };
        MapWindowPoints(IntPtr.Zero, layer, ref p, 1);
        SetWindowPos(hwnd, IntPtr.Zero, p.X, p.Y, width, height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>Stacca la finestra dal desktop per poterla trascinare.</summary>
    public static void Detach(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        SetParent(hwnd, IntPtr.Zero);

        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~WS_EX_NOACTIVATE);

        SetWindowPos(hwnd, HWND_TOPMOST, screenX, screenY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static void ClearTopmost(IntPtr hwnd) =>
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | SWP_NOACTIVATE);
}
