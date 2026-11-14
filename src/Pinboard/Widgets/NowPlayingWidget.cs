using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using MediaManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using MediaSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Pinboard;

/// <summary>The Windows media sessions (the ones in the volume flyout), requested once and shared.</summary>
internal static class MediaSessions
{
    private static Task<MediaManager?>? _manager;

    public static Task<MediaManager?> Manager => _manager ??= Request();

    private static async Task<MediaManager?> Request()
    {
        try
        {
            return await MediaManager.RequestAsync();
        }
        catch (Exception ex)
        {
            App.Log("Media sessions not available: " + ex.Message, force: true);
            return null;
        }
    }

    /// <summary>The album art, decoded small; null if there is none or it can't be read.</summary>
    public static async Task<BitmapSource?> LoadArt(IRandomAccessStreamReference? reference)
    {
        if (reference == null) return null;
        try
        {
            using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
            using Stream source = stream.AsStreamForRead();
            var memory = new MemoryStream();
            await source.CopyToAsync(memory);
            memory.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 256;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            App.Log("Album art not readable: " + ex.Message);
            return null;
        }
    }
}

public sealed class NowPlayingDefinition : WidgetDefinition
{
    public override string Type => "nowplaying";
    public override string Name => "Now playing";
    public override string Description =>
        "The song or video playing on your computer, from Spotify, your browser or any app that shows " +
        "up in the Windows volume panel, with previous, play/pause and next buttons.";
    public override string Category => "Media";
    public override IReadOnlyList<WidgetSize> Sizes { get; } = new[] { Medium };
    public override string DefaultAccent => "#FF2D55";
    public override IReadOnlyList<string> Features { get; } = new[] { "Previous, play/pause and next buttons" };

    protected override WidgetView Build(WidgetInstance w, WidgetStyle style, WidgetSize size) =>
        new NowPlayingView(style, size);
}

internal sealed class NowPlayingView : WidgetView
{
    private static readonly string Note = char.ConvertFromUtf32(0xE8D6);
    private static readonly string Previous = char.ConvertFromUtf32(0xE892);
    private static readonly string Next = char.ConvertFromUtf32(0xE893);
    private static readonly string Play = char.ConvertFromUtf32(0xE768);
    private static readonly string Pause = char.ConvertFromUtf32(0xE769);

    private readonly Image _art;
    private readonly TextBlock _placeholder;
    private readonly TextBlock _title;
    private readonly TextBlock _artist;
    private readonly StackPanel _buttons;
    private readonly Border _previous;
    private readonly Border _next;
    private readonly Border _play;
    private readonly TextBlock _playIcon;

    private MediaManager? _manager;
    private MediaSession? _session;
    private bool _detached;
    private int _refreshing;

    public NowPlayingView(WidgetStyle s, WidgetSize size) : base(s, size)
    {
        _placeholder = WidgetParts.Icon(Note, 40, s.Accent);
        _placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        _art = new Image { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(_art, BitmapScalingMode.HighQuality);
        var artBox = new Border
        {
            Width = 126,
            Height = 126,
            CornerRadius = new CornerRadius(14),
            Background = WidgetParts.Faint(s.Accent, 0.16),
            Clip = new RectangleGeometry(new Rect(0, 0, 126, 126), 14, 14),
            Child = new Grid { Children = { _placeholder, _art } },
        };

        _title = WidgetParts.Label("Nothing playing", 15, s.Primary, FontWeights.SemiBold);
        _title.TextWrapping = TextWrapping.Wrap;
        _title.MaxHeight = 42;
        _artist = WidgetParts.Label("Play music or a video in any app to see it here.", 12.5, s.Secondary);
        _artist.TextWrapping = TextWrapping.Wrap;
        _artist.MaxHeight = 36;
        _artist.Margin = new Thickness(0, 3, 0, 0);

        _previous = IconButton(Previous, 16, s.Primary, null, () => _ = Do(m => m.TrySkipPreviousAsync()));
        _playIcon = WidgetParts.Icon(Play, 16, s.OnAccent);
        _play = IconButton(Play, 16, s.OnAccent, s.Accent, () => _ = Do(m => m.TryTogglePlayPauseAsync()), _playIcon);
        _next = IconButton(Next, 16, s.Primary, null, () => _ = Do(m => m.TrySkipNextAsync()));
        _play.Margin = new Thickness(10, 0, 10, 0);
        _buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(-6, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            Children = { _previous, _play, _next },
        };

        var text = new Grid { Margin = new Thickness(16, 2, 0, 2) };
        text.RowDefinitions.Add(new RowDefinition());
        text.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _artist } };
        Grid.SetRow(_buttons, 1);
        text.Children.Add(titles);
        text.Children.Add(_buttons);

        var root = new Grid { Margin = new Thickness(22, 21, 18, 21) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(text, 1);
        root.Children.Add(artBox);
        root.Children.Add(text);
        Child = root;

        _ = StartAsync();
    }

    /// <summary>
    /// A round button that acts on mouse down. Widgets never get keyboard focus, and in
    /// "Move widgets" mode the host handles the press first, so the button doesn't act while dragging.
    /// </summary>
    private static Border IconButton(string glyph, double size, Brush brush, Brush? fill, Action click, TextBlock? icon = null)
    {
        icon ??= WidgetParts.Icon(glyph, size, brush);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var hover = WidgetParts.Faint(brush, 0.12);
        var button = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = fill ?? Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = icon,
        };
        if (fill == null)
        {
            button.MouseEnter += (_, _) => button.Background = hover;
            button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        }
        else
        {
            button.MouseEnter += (_, _) => button.Opacity = 0.85;
            button.MouseLeave += (_, _) => button.Opacity = 1;
        }
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            App.Log($"Now playing: button pressed (enabled: {button.IsEnabled})");
            if (button.IsEnabled) click();
        };
        return button;
    }

    private async Task StartAsync()
    {
        _manager = await MediaSessions.Manager;
        if (_detached || _manager == null) return;
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        Use(_manager.GetCurrentSession());
    }

    private void OnCurrentSessionChanged(MediaManager sender, CurrentSessionChangedEventArgs args) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (!_detached) Use(sender.GetCurrentSession());
        });

    private void Use(MediaSession? session)
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
        }
        _session = session;
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaChanged;
            _session.PlaybackInfoChanged += OnPlaybackChanged;
        }
        _ = RefreshAsync();
    }

    private void OnMediaChanged(MediaSession sender, MediaPropertiesChangedEventArgs args) =>
        Dispatcher.BeginInvoke(() => _ = RefreshAsync());

    private void OnPlaybackChanged(MediaSession sender, PlaybackInfoChangedEventArgs args) =>
        Dispatcher.BeginInvoke(ShowPlayback);

    private async Task RefreshAsync()
    {
        if (_detached) return;
        int version = ++_refreshing;
        MediaSession? session = _session;
        if (session == null)
        {
            ShowEmpty();
            return;
        }
        try
        {
            var media = await session.TryGetMediaPropertiesAsync();
            BitmapSource? art = await MediaSessions.LoadArt(media?.Thumbnail);
            if (_detached || version != _refreshing || session != _session) return;
            string title = media?.Title ?? "";
            if (title.Length == 0)
            {
                ShowEmpty();
                return;
            }
            _title.Text = title;
            _artist.Text = string.IsNullOrWhiteSpace(media!.Artist) ? media.AlbumTitle ?? "" : media.Artist;
            _art.Source = art;
            _placeholder.Visibility = art == null ? Visibility.Visible : Visibility.Collapsed;
            _buttons.Visibility = Visibility.Visible;
            ShowPlayback();
        }
        catch (Exception ex)
        {
            App.Log("Now playing: " + ex.Message);
            if (version == _refreshing) ShowEmpty();
        }
    }

    private void ShowPlayback()
    {
        if (_detached || _session == null) return;
        try
        {
            var info = _session.GetPlaybackInfo();
            bool playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            _playIcon.Text = playing ? Pause : Play;
            SetEnabled(_previous, info.Controls.IsPreviousEnabled);
            SetEnabled(_next, info.Controls.IsNextEnabled);
            SetEnabled(_play, playing ? info.Controls.IsPauseEnabled : info.Controls.IsPlayEnabled);
        }
        catch (Exception ex)
        {
            App.Log("Now playing: " + ex.Message);
        }
    }

    private static void SetEnabled(Border button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1 : 0.35;
    }

    private void ShowEmpty()
    {
        _title.Text = "Nothing playing";
        _artist.Text = "Play music or a video in any app to see it here.";
        _art.Source = null;
        _placeholder.Visibility = Visibility.Visible;
        _buttons.Visibility = Visibility.Collapsed;
    }

    private async Task Do(Func<MediaSession, global::Windows.Foundation.IAsyncOperation<bool>> action)
    {
        if (_session == null) return;
        try
        {
            bool done = await action(_session);
            App.Log($"Now playing: command sent, accepted: {done}");
        }
        catch (Exception ex)
        {
            App.Log("Media command failed: " + ex.Message);
        }
    }

    public override void Detach()
    {
        _detached = true;
        if (_manager != null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
        }
    }
}
