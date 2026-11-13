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

    /// <summary>Prova di posizionamento: auto (sotto le icone), top (sopra a tutto), bottom.</summary>
    public static string ZMode { get; set; } = "auto";

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);

    public static void LogChildren(IntPtr parent, string label)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"Figli di {label} dall'alto verso il basso:");
        for (IntPtr h = GetWindow(parent, 5); h != IntPtr.Zero; h = GetWindow(h, 2))
        {
            var cls = new System.Text.StringBuilder(256);
            GetClassName(h, cls, 256);
            GetWindowRect(h, out RECT r);
            sb.Append($"{Environment.NewLine}   0x{h.ToInt64():X} {cls} vis={IsWindowVisible(h)} " +
                      $"rect=({r.Left},{r.Top},{r.Right},{r.Bottom})");
        }
        App.Log(sb.ToString());
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    private static string ClassOf(IntPtr h)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(h, sb, 256);
        return sb.ToString();
    }

    /// <summary>Finestra di primo livello che contiene le icone del desktop (Progman o WorkerW).</summary>
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

    /// <summary>
    /// Modalita' finestra di primo livello: la finestra resta subito sopra il desktop e sotto
    /// tutte le altre. Nessun genitore, quindi niente problemi di disegno.
    /// </summary>
    public static void PlaceAboveDesktop(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);

        IntPtr desktop = FindDesktopTopLevel();
        bool ok = SetWindowPos(hwnd, desktop, screenX, screenY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        App.Log($"PlaceAboveDesktop desktop=0x{desktop.ToInt64():X} ({ClassOf(desktop)}) ok={ok}");
    }

    private static bool _wasHidden;

    /// <summary>
    /// Da chiamare a intervalli: se "Mostra desktop" ha nascosto o minimizzato il widget lo
    /// rimostra, e lo rimette sopra il desktop quando il desktop e' in primo piano.
    /// </summary>
    public static void KeepVisible(IntPtr hwnd)
    {
        IntPtr fg = GetForegroundWindow();
        string fgClass = fg == IntPtr.Zero ? "" : ClassOf(fg);
        bool desktopFocused = fgClass is "Progman" or "WorkerW";

        bool hidden = IsIconic(hwnd) || !IsWindowVisible(hwnd);
        if (hidden != _wasHidden)
        {
            App.Log($"Widget {(hidden ? "nascosto/minimizzato" : "di nuovo visibile")} (foreground={fgClass})");
            _wasHidden = hidden;
        }
        if (hidden) ShowWindow(hwnd, 4 /* SW_SHOWNOACTIVATE */);

        if (desktopFocused || hidden)
            SetWindowPos(hwnd, FindDesktopTopLevel(), 0, 0, 0, 0, 0x0001 | 0x0002 | SWP_NOACTIVATE);
    }

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
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);
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
    public static (IntPtr parent, IntPtr insertAfter) FindDesktopLayer()
    {
        IntPtr progman = FindWindow("Progman", null);
        // Chiede a Explorer di creare il livello WorkerW dello sfondo.
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        // Windows 11 24H2: SHELLDLL_DefView (icone) e WorkerW (sfondo) sono entrambi figli di
        // Progman. Il widget va come figlio di Progman, subito sotto SHELLDLL_DefView, cioe'
        // sopra lo sfondo e sotto le icone.
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
            return (progman, defView);

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

        return (found != IntPtr.Zero ? found : progman, IntPtr.Zero);
    }

    /// <summary>Rende la finestra figlia del desktop e la posiziona (coordinate schermo, pixel).</summary>
    public static void Attach(IntPtr hwnd, int screenX, int screenY, int width, int height)
    {
        var (layer, insertAfter) = FindDesktopLayer();

        int style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_POPUP) | WS_CHILD);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);

        IntPtr oldParent = SetParent(hwnd, layer);
        App.Log($"layer=0x{layer.ToInt64():X} insertAfter=0x{insertAfter.ToInt64():X} " +
                $"SetParent->old=0x{oldParent.ToInt64():X} err={Marshal.GetLastWin32Error()}");

        // Le coordinate di una finestra figlia sono relative al genitore.
        var p = new POINT { X = screenX, Y = screenY };
        MapWindowPoints(IntPtr.Zero, layer, ref p, 1);
        // Con insertAfter la finestra va subito sotto SHELLDLL_DefView; senza, resta dov'e'.
        if (ZMode == "top") insertAfter = IntPtr.Zero;            // HWND_TOP
        else if (ZMode == "bottom") insertAfter = new IntPtr(1);  // HWND_BOTTOM
        bool keepZ = ZMode == "auto" && insertAfter == IntPtr.Zero;
        uint flags = SWP_NOACTIVATE | SWP_SHOWWINDOW | (keepZ ? SWP_NOZORDER : 0);
        bool ok = SetWindowPos(hwnd, insertAfter, p.X, p.Y, width, height, flags);
        GetWindowRect(hwnd, out RECT r);
        App.Log($"SetWindowPos ok={ok} err={Marshal.GetLastWin32Error()} client=({p.X},{p.Y}) " +
                $"screenRect=({r.Left},{r.Top},{r.Right},{r.Bottom}) visible={IsWindowVisible(hwnd)} " +
                $"parent=0x{GetParent(hwnd).ToInt64():X} style=0x{GetWindowLong(hwnd, GWL_STYLE):X} " +
                $"exstyle=0x{GetWindowLong(hwnd, GWL_EXSTYLE):X} zmode={ZMode}");
        LogChildren(layer, $"0x{layer.ToInt64():X}");
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

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr region, bool redraw);

    /// <summary>Ritaglia gli angoli della finestra (per le finestre non trasparenti).</summary>
    public static void ApplyRoundedRegion(IntPtr hwnd, int width, int height, int radius)
    {
        IntPtr rgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        SetWindowRgn(hwnd, rgn, true); // dopo la chiamata la regione appartiene al sistema
    }

    public static void ClearTopmost(IntPtr hwnd) =>
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | SWP_NOACTIVATE);
}
