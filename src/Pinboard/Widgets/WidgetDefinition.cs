using System.Windows;
using System.Windows.Controls;

namespace Pinboard;

public sealed record WidgetSize(string Id, string Name, double Width, double Height);

/// <summary>The content of a widget: a rounded card that updates itself.</summary>
public abstract class WidgetView : Border
{
    protected WidgetView(WidgetStyle style, WidgetSize size)
    {
        Look = style;
        Width = size.Width;
        Height = size.Height;
        CornerRadius = new CornerRadius(22);
        Background = style.Card;
        BorderBrush = style.CardBorder;
        BorderThickness = new Thickness(1);
        SnapsToDevicePixels = true;
    }

    protected WidgetStyle Look { get; }

    /// <summary>Called four times per second.</summary>
    public virtual void Tick() { }
}

/// <summary>A widget type in the catalog.</summary>
public abstract class WidgetDefinition
{
    public static readonly WidgetSize Small = new("small", "Small", 170, 170);
    public static readonly WidgetSize Medium = new("medium", "Medium", 360, 170);

    public abstract string Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract IReadOnlyList<WidgetSize> Sizes { get; }
    public abstract string DefaultAccent { get; }

    /// <summary>What can be customized besides the common appearance.</summary>
    public virtual IReadOnlyList<string> Features => Array.Empty<string>();

    public WidgetSize SizeOf(WidgetInstance w) => Sizes.FirstOrDefault(s => s.Id == w.Size) ?? Sizes[0];

    public WidgetInstance CreateInstance(string? size = null)
    {
        var w = new WidgetInstance { Type = Type, Size = size ?? Sizes[0].Id };
        ApplyDefaults(w);
        return w;
    }

    public WidgetView CreateView(WidgetInstance w) => Build(w, WidgetStyle.For(w, DefaultAccent), SizeOf(w));

    protected abstract WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size);

    protected virtual void ApplyDefaults(WidgetInstance w) { }

    /// <summary>Controls for the widget's own options; null if it has none.</summary>
    public virtual FrameworkElement? CreateOptionsEditor(WidgetInstance working, Action changed) => null;

    /// <summary>Summary of the widget's own options, for the list of widgets on the desktop.</summary>
    public virtual string? Summary(WidgetInstance w) => null;
}

public static class WidgetCatalog
{
    public static IReadOnlyList<WidgetDefinition> All { get; } = new WidgetDefinition[]
    {
        new ClockDefinition(),
        new CalendarDefinition(),
    };

    public static WidgetDefinition? Find(string type) => All.FirstOrDefault(d => d.Type == type);

    public static IEnumerable<string> Categories => All.Select(d => d.Category).Distinct();
}
