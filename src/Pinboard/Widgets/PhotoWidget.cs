using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pinboard;

/// <summary>Random order without repeats: every photo is shown once before any comes back.</summary>
internal sealed class PhotoShuffle
{
    private readonly IReadOnlyList<string> _files;
    private readonly Random _rng;
    private readonly Queue<string> _queue = new();
    private string? _last;

    public PhotoShuffle(IReadOnlyList<string> files, Random rng)
    {
        _files = files;
        _rng = rng;
    }

    public string? Next()
    {
        if (_files.Count == 0) return null;
        if (_queue.Count == 0)
        {
            var round = _files.OrderBy(_ => _rng.Next()).ToList();
            // Don't show the same photo twice in a row across two rounds.
            if (round.Count > 1 && round[0] == _last) (round[0], round[^1]) = (round[^1], round[0]);
            foreach (var f in round) _queue.Enqueue(f);
        }
        _last = _queue.Dequeue();
        return _last;
    }
}

internal static class PhotoFiles
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

    private const int MaxFiles = 5000;

    /// <summary>Photos in the folder and its subfolders; nothing when the folder is missing or unreadable.</summary>
    public static IReadOnlyList<string> Find(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            return Directory.EnumerateFiles(folder, "*", options)
                .Where(f => Extensions.Contains(Path.GetExtension(f)))
                .Take(MaxFiles)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>The photo decoded at about <paramref name="pixelWidth"/> pixels wide, or null if it can't be read.</summary>
    public static BitmapSource? TryLoad(string file, int pixelWidth)
    {
        try
        {
            // Our own stream, always closed: decoding from a Uri can leave the file locked when it fails.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = pixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException
                                       or ArgumentException or InvalidOperationException
                                       or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    public static string DefaultFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
}

public sealed class PhotoDefinition : WidgetDefinition
{
    internal static readonly (string Value, string Label)[] Intervals =
        { ("1", "1 min"), ("5", "5 min"), ("15", "15 min"), ("60", "1 hour"), ("1440", "1 day") };

    public override string Type => "photos";
    public override string Name => "Photo frame";
    public override string Description =>
        "Photos from a folder on your computer, changing every few minutes in random order. " +
        "Subfolders are included.";
    public override string Category => "Photos";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Small, Medium, Large };
    public override string DefaultAccent => "#AF52DE";
    public override IReadOnlyList<string> Features { get; } = new[] { "Folder and how often the photo changes" };

    protected override void ApplyDefaults(WidgetInstance w)
    {
        w.Options["folder"] = PhotoFiles.DefaultFolder;
        w.Options["interval"] = "15";
    }

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new PhotoView(w, style, size);

    public override string? Summary(WidgetInstance w)
    {
        string folder = Path.GetFileName(Folder(w).TrimEnd('\\', '/'));
        string every = Intervals.First(i => i.Value == Interval(w).ToString(Fonts.Culture)).Label;
        return $"{(folder.Length > 0 ? folder : Folder(w))}, every {every}";
    }

    public override FrameworkElement CreateOptionsEditor(WidgetInstance w, Action changed) => new StackPanel
    {
        Children =
        {
            UiKit.Field("Folder", UiKit.FolderPicker(Folder(w), v =>
            {
                w.Options["folder"] = v;
                changed();
            })),
            UiKit.Field("Change every", UiKit.Segmented(Intervals, Interval(w).ToString(Fonts.Culture), v =>
            {
                w.Options["interval"] = v;
                changed();
            })),
        },
    };

    internal static string Folder(WidgetInstance w)
    {
        string f = w.Option("folder");
        return f.Length > 0 ? f : PhotoFiles.DefaultFolder;
    }

    internal static int Interval(WidgetInstance w) =>
        int.TryParse(w.Option("interval"), out int m) && Intervals.Any(i => i.Value == w.Option("interval")) ? m : 15;
}

internal sealed class PhotoView : WidgetView
{
    private readonly WidgetSize _size;
    private readonly Image _image;
    private readonly FrameworkElement _empty;
    private readonly DispatcherTimer _timer;
    private PhotoShuffle? _shuffle;
    private bool _detached;

    public PhotoView(WidgetInstance w, WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _size = size;
        _image = new Image
        {
            Stretch = Stretch.UniformToFill,
            Width = size.Width,
            Height = size.Height,
            Clip = new RectangleGeometry(new Rect(0, 0, size.Width, size.Height), 21, 21),
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

        var icon = WidgetParts.Icon(char.ConvertFromUtf32(0xE91B), 28, s.Secondary);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var text = WidgetParts.Label("Choose a folder with photos", 12, s.Secondary);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextAlignment = TextAlignment.Center;
        text.Margin = new Thickness(0, 8, 0, 0);
        _empty = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18),
            Visibility = Visibility.Collapsed,
            Children = { icon, text },
        };

        Child = new Grid { Children = { _image, _empty } };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(PhotoDefinition.Interval(w)) };
        _timer.Tick += (_, _) => ShowNext();

        string folder = PhotoDefinition.Folder(w);
        Task.Run(() => PhotoFiles.Find(folder)).ContinueWith(t =>
        {
            if (_detached) return;
            _shuffle = new PhotoShuffle(t.Result, new Random());
            ShowNext();
            _timer.Start();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Loads the next readable photo in the background; skips files that can't be decoded.</summary>
    private void ShowNext()
    {
        if (_shuffle == null || _detached) return;
        var shuffle = _shuffle;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int pixels = (int)Math.Ceiling(Math.Max(_size.Width, _size.Height) * Math.Max(scale, 1) * 1.25);
        Task.Run(() =>
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (shuffle.Next() is not string file) return null;
                if (PhotoFiles.TryLoad(file, pixels) is BitmapSource image) return image;
            }
            return null;
        }).ContinueWith(t =>
        {
            if (_detached) return;
            _image.Source = t.Result;
            _empty.Visibility = t.Result == null ? Visibility.Visible : Visibility.Collapsed;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public override void Detach()
    {
        _detached = true;
        _timer.Stop();
    }
}
