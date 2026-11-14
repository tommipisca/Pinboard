using System.Windows.Media;
using Microsoft.Win32;

namespace Pinboard;

/// <summary>Colori dei widget, in chiaro o scuro a seconda del tema delle app di Windows.</summary>
internal static class Theme
{
    public static readonly bool IsLight = DetectLight();

    public static readonly Brush Card = Make(IsLight ? "#EBF6F6F8" : "#E61F1F21");
    public static readonly Brush CardBorder = Make(IsLight ? "#14000000" : "#24FFFFFF");
    public static readonly Brush Primary = Make(IsLight ? "#1C1C1E" : "#FFFFFF");
    public static readonly Brush Secondary = Make(IsLight ? "#8A8A8E" : "#A0FFFFFF");
    public static readonly Brush Red = Make(IsLight ? "#FF3B30" : "#FF453A");
    public static readonly Brush Orange = Make("#FF9F0A");

    private static bool DetectLight()
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

    internal static Brush Make(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
