using System.Windows;
using System.Windows.Controls;

namespace Pinboard;

public sealed record WidgetSize(string Id, string Name, double Width, double Height);

/// <summary>Il contenuto di un widget: una scheda arrotondata che si aggiorna da sola.</summary>
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

    /// <summary>Chiamato quattro volte al secondo.</summary>
    public virtual void Tick() { }
}

/// <summary>Un tipo di widget del catalogo.</summary>
public abstract class WidgetDefinition
{
    public static readonly WidgetSize Small = new("small", "Piccolo", 170, 170);
    public static readonly WidgetSize Medium = new("medium", "Medio", 360, 170);

    public abstract string Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract IReadOnlyList<WidgetSize> Sizes { get; }
    public abstract string DefaultAccent { get; }

    /// <summary>Cosa si puo' personalizzare oltre all'aspetto comune.</summary>
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

    /// <summary>Controlli per le opzioni proprie del widget; null se non ne ha.</summary>
    public virtual FrameworkElement? CreateOptionsEditor(WidgetInstance working, Action changed) => null;

    /// <summary>Riassunto delle opzioni proprie, per l'elenco dei widget sul desktop.</summary>
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
