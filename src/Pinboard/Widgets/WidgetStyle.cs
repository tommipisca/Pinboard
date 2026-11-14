using System.Windows.Media;
using Microsoft.Win32;

namespace Pinboard;

/// <summary>Tema chiaro o scuro delle app di Windows.</summary>
internal static class SystemTheme
{
    public static bool AppsUseLight()
    {
        try
        {
            object? v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is not int i || i != 0;
        }
        catch
        {
            return true;
        }
    }
}

internal static class ColorUtil
{
    public static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            color = Parse(hex);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Luminanza relativa (0 nero, 1 bianco).</summary>
    public static double Luminance(Color c)
    {
        static double Lin(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    public static SolidColorBrush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static SolidColorBrush Brush(string hex) => Brush(Parse(hex));
}

/// <summary>I colori con cui un widget si disegna, ricavati dalle sue personalizzazioni.</summary>
public sealed class WidgetStyle
{
    public static readonly Color LightCard = ColorUtil.Parse("#F6F6F8");
    public static readonly Color DarkCard = ColorUtil.Parse("#1F1F21");

    public required bool DarkBackground { get; init; }
    public required Brush Card { get; init; }
    public required Brush CardBorder { get; init; }
    public required Brush Primary { get; init; }
    public required Brush Secondary { get; init; }
    public required Brush Accent { get; init; }

    /// <summary>Testo da mettere sopra il colore d'accento.</summary>
    public required Brush OnAccent { get; init; }

    public static WidgetStyle For(WidgetInstance w, string defaultAccent)
    {
        bool dark = w.Theme switch
        {
            "light" => false,
            "dark" => true,
            _ => !SystemTheme.AppsUseLight(),
        };

        Color bg = ColorUtil.TryParse(w.Background, out Color custom) ? custom : dark ? DarkCard : LightCard;
        bool darkBg = ColorUtil.Luminance(bg) < 0.4;
        double opacity = Math.Clamp(w.Opacity, 0.2, 1);
        Color card = Color.FromArgb((byte)Math.Round(opacity * 255), bg.R, bg.G, bg.B);

        Color accent = ColorUtil.TryParse(w.Accent, out Color a) ? a : ColorUtil.Parse(defaultAccent);

        return new WidgetStyle
        {
            DarkBackground = darkBg,
            Card = ColorUtil.Brush(card),
            CardBorder = ColorUtil.Brush(darkBg ? "#24FFFFFF" : "#14000000"),
            Primary = ColorUtil.Brush(darkBg ? "#FFFFFF" : "#1C1C1E"),
            Secondary = ColorUtil.Brush(darkBg ? "#A0FFFFFF" : "#8A8A8E"),
            Accent = ColorUtil.Brush(accent),
            OnAccent = ColorUtil.Brush(ColorUtil.Luminance(accent) > 0.5 ? "#1C1C1E" : "#FFFFFF"),
        };
    }
}
