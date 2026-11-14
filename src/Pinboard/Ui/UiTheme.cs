using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pinboard;

/// <summary>Colori della dashboard, in chiaro o scuro come le app di Windows.</summary>
internal static class UiTheme
{
    public static bool Dark { get; private set; }
    public static Color Pin { get; private set; }
    public static Color WindowColor { get; private set; }

    private static readonly (string Key, string Light, string Dark)[] Palette =
    {
        ("Bg.Window", "#ECEEF1", "#16171A"),
        ("Bg.Surface", "#FFFFFF", "#202226"),
        ("Bg.SurfaceAlt", "#F5F6F8", "#1B1D20"),
        ("Bg.Hover", "#E3E5E9", "#2A2D32"),
        ("Bg.NavSelected", "#DEE1E6", "#272A30"),
        ("Stroke", "#E1E3E8", "#2C2F35"),
        ("Stroke.Strong", "#CDD1D8", "#3B3F46"),
        ("Ink", "#1A1C21", "#F1F2F4"),
        ("Ink.Muted", "#5E6470", "#A3A9B4"),
        ("Ink.Faint", "#8D939E", "#737985"),
        ("Accent", "#2A55D4", "#7D9CFF"),
        ("Accent.Ink", "#FFFFFF", "#0D1430"),
        ("Accent.Soft", "#E3EAFB", "#26304F"),
        ("Danger", "#C4312A", "#FF7A72"),
        ("Pin", "#D8372F", "#F0524A"),
        ("Star", "#E8A317", "#F5B83D"),
    };

    public static void Apply()
    {
        Dark = !SystemTheme.AppsUseLight();
        ResourceDictionary r = Application.Current.Resources;
        foreach (var (key, light, dark) in Palette)
            r[key] = ColorUtil.Brush(Dark ? dark : light);

        Pin = ColorUtil.Parse(Dark ? "#F0524A" : "#D8372F");
        WindowColor = ColorUtil.Parse(Dark ? "#16171A" : "#ECEEF1");
        r["Cork"] = CorkTexture.Create(Dark);
    }

    // --- Barra del titolo nello stesso colore della finestra (Windows 11) -----------------

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void ApplyChrome(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        Color c = WindowColor;
        int caption = c.R | (c.G << 8) | (c.B << 16);
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
    }
}

/// <summary>Trama di sughero generata al volo: base calda con granelli chiari e scuri.</summary>
internal static class CorkTexture
{
    public static Brush Create(bool dark)
    {
        const int n = 192;
        var px = new byte[n * n * 4];
        var rnd = new Random(11);
        (int r, int g, int b) = dark ? (0x6A, 0x56, 0x42) : (0xC6, 0xA0, 0x74);

        for (int i = 0; i < n * n; i++)
        {
            int v = rnd.Next(-7, 8);
            Put(px, i, r + v, g + v, b + v);
        }

        // Granelli: per lo piu' scuri, qualcuno chiaro, a volte allungati.
        for (int k = 0; k < n * n / 7; k++)
        {
            int x = rnd.Next(n), y = rnd.Next(n);
            bool darker = rnd.NextDouble() < 0.7;
            int d = darker ? -(16 + rnd.Next(34)) : 10 + rnd.Next(20);
            int len = rnd.NextDouble() < 0.25 ? 2 + rnd.Next(2) : 1;
            bool horizontal = rnd.Next(2) == 0;
            for (int s = 0; s < len; s++)
            {
                int xx = (x + (horizontal ? s : 0)) % n, yy = (y + (horizontal ? 0 : s)) % n;
                int i = yy * n + xx;
                Put(px, i, px[i * 4 + 2] + d, px[i * 4 + 1] + d, px[i * 4] + d);
            }
        }

        BitmapSource bmp = BitmapSource.Create(n, n, 96, 96, PixelFormats.Bgra32, null, px, n * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, n, n),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    private static void Put(byte[] px, int i, int r, int g, int b)
    {
        px[i * 4] = (byte)Math.Clamp(b, 0, 255);
        px[i * 4 + 1] = (byte)Math.Clamp(g, 0, 255);
        px[i * 4 + 2] = (byte)Math.Clamp(r, 0, 255);
        px[i * 4 + 3] = 255;
    }
}
