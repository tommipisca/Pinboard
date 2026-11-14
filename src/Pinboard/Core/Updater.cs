using System.IO;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Pinboard;

public enum UpdateStatus
{
    /// <summary>Started from Visual Studio or with dotnet run: there is nothing to update.</summary>
    Unsupported,
    Idle,
    Checking,
    UpToDate,
    Downloading,
    /// <summary>Downloaded: installs on quit or with "Restart and update".</summary>
    Ready,
    Error,
}

/// <summary>
/// Updates from GitHub releases (Velopack). Pinboard checks 15 seconds after startup and then
/// every 6 hours; if there is a new version it downloads it in the background and installs it on quit, or
/// right away with "Restart and update". Works for both the installed and the portable version.
/// </summary>
internal static class Updater
{
    public const string RepositoryUrl = "https://github.com/tommipisca/Pinboard";
    public const string ReleasesUrl = RepositoryUrl + "/releases/latest";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private static UpdateManager? _manager;
    private static VelopackAsset? _ready;
    private static bool _restartAfterUpdate;
    private static readonly DispatcherTimer Timer = new();

    public static UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    public static string? NewVersion { get; private set; }
    public static int Percent { get; private set; }
    public static string? Error { get; private set; }
    public static DateTime? LastCheck { get; private set; }
    public static bool IsPortable => _manager?.IsPortable ?? false;

    public static string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? typeof(Updater).Assembly.GetName().Version?.ToString(3)
        ?? "";

    public static event Action? Changed;

    public static void Start()
    {
        try
        {
            _manager = new UpdateManager(CreateSource());
        }
        catch (Exception ex)
        {
            App.Log("Updates not available: " + ex.Message, force: true);
        }

        if (_manager == null || !_manager.IsInstalled)
        {
            Status = UpdateStatus.Unsupported;
            return;
        }

        // An update downloaded in a previous session and not installed yet.
        if (_manager.UpdatePendingRestart is VelopackAsset pending)
        {
            _ready = pending;
            NewVersion = pending.Version.ToString();
            Status = UpdateStatus.Ready;
        }

        Timer.Tick += (_, _) =>
        {
            Timer.Interval = CheckInterval;
            _ = CheckAsync();
        };
        Schedule();
    }

    /// <summary>
    /// GitHub, or a folder or URL in PINBOARD_UPDATE_URL: used to test updates
    /// without publishing a release.
    /// </summary>
    private static IUpdateSource CreateSource()
    {
        string? custom = Environment.GetEnvironmentVariable("PINBOARD_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(custom)) return new GithubSource(RepositoryUrl, null, false);
        return Directory.Exists(custom)
            ? new SimpleFileSource(new DirectoryInfo(custom))
            : new SimpleWebSource(custom);
    }

    /// <summary>Starts or stops the automatic checks according to the setting.</summary>
    public static void Schedule()
    {
        Timer.Stop();
        if (Status == UpdateStatus.Unsupported || !WidgetManager.Settings.AutoUpdate) return;
        Timer.Interval = LastCheck == null ? FirstCheckDelay : CheckInterval;
        Timer.Start();
    }

    public static async Task CheckAsync()
    {
        if (_manager == null || Status is UpdateStatus.Unsupported or UpdateStatus.Checking
                or UpdateStatus.Downloading or UpdateStatus.Ready)
            return;

        Set(UpdateStatus.Checking);
        try
        {
            UpdateInfo? info = await _manager.CheckForUpdatesAsync();
            LastCheck = DateTime.Now;
            if (info == null)
            {
                Set(UpdateStatus.UpToDate);
                return;
            }

            NewVersion = info.TargetFullRelease.Version.ToString();
            Percent = 0;
            Set(UpdateStatus.Downloading);
            await _manager.DownloadUpdatesAsync(info, p => Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Percent = p;
                Changed?.Invoke();
            }));

            _ready = info.TargetFullRelease;
            Set(UpdateStatus.Ready);
            WidgetManager.ShowTrayTip("Update ready",
                $"Pinboard {NewVersion} will install when you quit. To install it now, use Restart and update.");
        }
        catch (Exception ex)
        {
            App.Log("Update check failed: " + ex, force: true);
            LastCheck = DateTime.Now;
            Error = Describe(ex);
            Set(UpdateStatus.Error);
        }
    }

    /// <summary>Quits Pinboard, installs the downloaded update and opens it again.</summary>
    public static void RestartAndUpdate()
    {
        if (Status != UpdateStatus.Ready) return;
        _restartAfterUpdate = true;
        App.Quit();
    }

    /// <summary>
    /// Call on exit, after closing the widgets: if an update has been downloaded,
    /// Velopack waits for Pinboard to close and installs it silently.
    /// </summary>
    public static void ApplyOnExit()
    {
        if (_manager == null || _ready == null) return;
        try
        {
            _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: _restartAfterUpdate,
                Array.Empty<string>());
        }
        catch (Exception ex)
        {
            App.Log("Could not install the update: " + ex, force: true);
        }
    }

    private static void Set(UpdateStatus status)
    {
        Status = status;
        if (status != UpdateStatus.Error) Error = null;
        Changed?.Invoke();
    }

    /// <summary>A readable message instead of the technical error.</summary>
    private static string Describe(Exception ex)
    {
        string text = ex.ToString();
        if (ex is System.Net.Http.HttpRequestException or TaskCanceledException ||
            text.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Host sconosciuto", StringComparison.OrdinalIgnoreCase))
            return "Can't reach GitHub. Try again later.";
        if (text.Contains("403") || text.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "GitHub refused the request because of too many checks. Try again in an hour.";
        return "Couldn't check for updates.";
    }
}
