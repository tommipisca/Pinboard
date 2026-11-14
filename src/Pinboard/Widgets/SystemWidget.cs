using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Pinboard;

/// <summary>CPU, memory, disk and battery readings, shared by all System widgets and refreshed at most once a second.</summary>
internal static class SystemStats
{
    /// <summary>Cumulative CPU times from GetSystemTimes (100 ns ticks).</summary>
    public readonly struct Times(long idle, long kernel, long user)
    {
        public long Idle { get; } = idle;
        public long Kernel { get; } = kernel;
        public long User { get; } = user;
    }

    public readonly record struct Usage(double Percent, double UsedGb, double TotalGb);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    private const double Gb = 1024d * 1024 * 1024;

    private static DateTime _lastSample = DateTime.MinValue;
    private static DateTime _lastDisk = DateTime.MinValue;
    private static Times? _previous;

    public static double Cpu { get; private set; }
    public static Usage Memory { get; private set; }
    public static Usage Disk { get; private set; }
    public static (double Percent, bool Charging)? Battery { get; private set; }

    /// <summary>Busy share of the time between two samples (kernel time includes idle time).</summary>
    public static double CpuUsage(Times before, Times after)
    {
        long idle = after.Idle - before.Idle;
        long total = after.Kernel - before.Kernel + after.User - before.User;
        return total <= 0 ? 0 : Math.Clamp((total - idle) * 100.0 / total, 0, 100);
    }

    public static Usage ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0) return new Usage(0, 0, 0);
        double total = status.TotalPhys / Gb;
        double used = (status.TotalPhys - status.AvailPhys) / Gb;
        return new Usage(used * 100 / total, used, total);
    }

    private static Usage ReadDisk()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            double total = drive.TotalSize / Gb;
            double used = (drive.TotalSize - drive.TotalFreeSpace) / Gb;
            return new Usage(total > 0 ? used * 100 / total : 0, used, total);
        }
        catch (IOException)
        {
            return new Usage(0, 0, 0);
        }
    }

    private static (double, bool)? ReadBattery()
    {
        Forms.PowerStatus power = Forms.SystemInformation.PowerStatus;
        if (power.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery) ||
            power.BatteryChargeStatus == Forms.BatteryChargeStatus.Unknown)
            return null;
        return (Math.Clamp(power.BatteryLifePercent * 100, 0, 100), power.PowerLineStatus == Forms.PowerLineStatus.Online);
    }

    /// <summary>Refreshes the readings if the last sample is older than a second (disk: a minute).</summary>
    public static void Sample()
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastSample < TimeSpan.FromSeconds(1)) return;
        _lastSample = now;

        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            var times = new Times(idle, kernel, user);
            if (_previous is Times before) Cpu = CpuUsage(before, times);
            _previous = times;
        }
        Memory = ReadMemory();
        Battery = ReadBattery();
        if (now - _lastDisk >= TimeSpan.FromMinutes(1))
        {
            _lastDisk = now;
            Disk = ReadDisk();
        }
    }
}

public sealed class SystemDefinition : WidgetDefinition
{
    internal static readonly (string Value, string Label)[] Values =
        { ("cpu", "CPU"), ("ram", "Memory"), ("disk", "Disk"), ("battery", "Battery") };

    public override string Type => "system";
    public override string Name => "System";
    public override string Description =>
        "How busy your computer is: processor, memory, disk space and battery. The small size shows " +
        "them as rings, the medium size as bars with details.";
    public override string Category => "System";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium };
    public override string DefaultAccent => "#0A84FF";
    public override IReadOnlyList<string> Features { get; } = new[] { "Choose which values to show" };

    protected override void ApplyDefaults(WidgetInstance w) => w.Options["show"] = "cpu,ram,disk,battery";

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new SystemView(w, style, size);

    public override string? Summary(WidgetInstance w) =>
        string.Join(", ", Shown(w.Option("show")).Select(v => Values.First(x => x.Value == v).Label));

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed) =>
        UiKit.Field("Show", UiKit.CheckList(Values, Shown(w.Option("show")).ToHashSet(), set =>
        {
            w.Options["show"] = string.Join(",", set);
            changed();
        }), alignTop: true);

    /// <summary>The known values from the option, in the standard order; CPU when none is left.</summary>
    internal static string[] Shown(string option)
    {
        var chosen = option.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var shown = Values.Select(v => v.Value).Where(chosen.Contains).ToArray();
        return shown.Length > 0 ? shown : new[] { "cpu" };
    }
}

internal sealed class SystemView : WidgetView
{
    private static readonly Brush Warning = ColorUtil.Brush("#FF453A");

    private readonly string[] _shown;
    private readonly bool _medium;
    private string _drawn = "";

    public SystemView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _shown = SystemDefinition.Shown(w.Option("show"));
        _medium = size.Id == WidgetDefinition.Medium.Id;
        SystemStats.Sample();
        Tick();
    }

    public override void Tick()
    {
        SystemStats.Sample();
        var values = _shown.Select(Read).ToList();
        string key = string.Join("|", values.Select(v => $"{Math.Round(v.Percent)}{v.Detail}"));
        if (key == _drawn) return;
        _drawn = key;
        Child = _medium ? BuildMedium(values) : BuildSmall(values);
    }

    private static (string Label, double Percent, string Detail, bool Missing) Read(string value) => value switch
    {
        "cpu" => ("CPU", SystemStats.Cpu, "", false),
        "ram" => ("Memory", SystemStats.Memory.Percent, string.Create(Fonts.Culture, $"{SystemStats.Memory.UsedGb:0.0} of {SystemStats.Memory.TotalGb:0} GB"), false),
        "disk" => ("Disk", SystemStats.Disk.Percent, string.Create(Fonts.Culture, $"{SystemStats.Disk.TotalGb - SystemStats.Disk.UsedGb:0} GB free"), false),
        _ => SystemStats.Battery is var (p, charging)
            ? ("Battery", p, charging ? "Plugged in" : "On battery", false)
            : ("Battery", 0, "No battery", true),
    };

    private Brush ColorFor(string label, double percent) =>
        label == "Battery" ? (percent <= 15 ? Warning : Look.Accent) : (percent >= 90 ? Warning : Look.Accent);

    private FrameworkElement BuildSmall(List<(string Label, double Percent, string Detail, bool Missing)> values)
    {
        var shown = values.Where(v => !v.Missing).ToList();
        if (shown.Count == 0) shown = values.Take(1).ToList();
        int columns = shown.Count == 1 ? 1 : 2;
        double ring = shown.Count == 1 ? 104 : 50;
        var grid = new UniformGrid { Columns = columns, Margin = new Thickness(10, 8, 10, 8) };
        foreach (var (label, percent, _, missing) in shown)
        {
            var number = WidgetParts.Label(missing ? "—" : $"{Math.Round(percent)}%", shown.Count == 1 ? 26 : 14,
                Look.Primary, FontWeights.SemiBold, display: shown.Count == 1);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            var r = WidgetParts.Ring(percent / 100, ring, shown.Count == 1 ? 9 : 6,
                WidgetParts.Faint(Look.Primary, 0.14), ColorFor(label, percent), number);
            var name = WidgetParts.Label(label == "Memory" && shown.Count > 1 ? "RAM" : label, 10.5, Look.Secondary);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 2, 0, 0);
            grid.Children.Add(new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { r, name },
            });
        }
        return grid;
    }

    private FrameworkElement BuildMedium(List<(string Label, double Percent, string Detail, bool Missing)> values)
    {
        var rows = new Grid { Margin = new Thickness(20, 14, 20, 14) };
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        rows.ColumnDefinitions.Add(new ColumnDefinition());
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        for (int i = 0; i < values.Count; i++)
        {
            var (label, percent, detail, missing) = values[i];
            rows.RowDefinitions.Add(new RowDefinition());
            var name = WidgetParts.Label(label, 13, Look.Primary, FontWeights.SemiBold);
            name.VerticalAlignment = VerticalAlignment.Center;
            var bar = WidgetParts.Bar(missing ? 0 : percent / 100, 8, WidgetParts.Faint(Look.Primary, 0.14), ColorFor(label, percent));
            bar.VerticalAlignment = VerticalAlignment.Center;
            string text = missing ? detail : detail.Length > 0 ? $"{Math.Round(percent)}%  ·  {detail}" : $"{Math.Round(percent)}%";
            var value = WidgetParts.Label(text, 11.5, Look.Secondary);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            foreach (var (e, col) in new (FrameworkElement, int)[] { (name, 0), (bar, 1), (value, 2) })
            {
                Grid.SetRow(e, i);
                Grid.SetColumn(e, col);
                rows.Children.Add(e);
            }
        }
        return rows;
    }
}
