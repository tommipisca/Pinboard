using System.IO;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Pinboard;

public enum UpdateStatus
{
    /// <summary>Avvio da Visual Studio o con dotnet run: non c'e' niente da aggiornare.</summary>
    Unsupported,
    Idle,
    Checking,
    UpToDate,
    Downloading,
    /// <summary>Scaricato: si installa all'uscita o con "Riavvia e aggiorna".</summary>
    Ready,
    Error,
}

/// <summary>
/// Aggiornamenti dalle release di GitHub (Velopack). Pinboard controlla 15 secondi dopo l'avvio e poi
/// ogni 6 ore; se c'e' una versione nuova la scarica in background e la installa quando si esce, o subito
/// con "Riavvia e aggiorna". Vale sia per la versione installata sia per quella portable.
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
            App.Log("Aggiornamenti non disponibili: " + ex.Message, force: true);
        }

        if (_manager == null || !_manager.IsInstalled)
        {
            Status = UpdateStatus.Unsupported;
            return;
        }

        // Un aggiornamento scaricato in una sessione precedente e non ancora installato.
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
    /// GitHub, oppure una cartella o un indirizzo in PINBOARD_UPDATE_URL: serve per provare gli
    /// aggiornamenti senza pubblicare una release.
    /// </summary>
    private static IUpdateSource CreateSource()
    {
        string? custom = Environment.GetEnvironmentVariable("PINBOARD_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(custom)) return new GithubSource(RepositoryUrl, null, false);
        return Directory.Exists(custom)
            ? new SimpleFileSource(new DirectoryInfo(custom))
            : new SimpleWebSource(custom);
    }

    /// <summary>Avvia o ferma i controlli automatici secondo l'impostazione.</summary>
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
            WidgetManager.ShowTrayTip("Aggiornamento pronto",
                $"Pinboard {NewVersion} si installerà quando esci. Per installarlo subito usa Riavvia e aggiorna.");
        }
        catch (Exception ex)
        {
            App.Log("Controllo aggiornamenti non riuscito: " + ex, force: true);
            LastCheck = DateTime.Now;
            Error = Describe(ex);
            Set(UpdateStatus.Error);
        }
    }

    /// <summary>Esce da Pinboard, installa l'aggiornamento scaricato e lo riapre.</summary>
    public static void RestartAndUpdate()
    {
        if (Status != UpdateStatus.Ready) return;
        _restartAfterUpdate = true;
        App.Quit();
    }

    /// <summary>
    /// Da chiamare all'uscita, dopo aver chiuso i widget: se c'e' un aggiornamento scaricato,
    /// Velopack aspetta che Pinboard sia chiuso e lo installa in silenzio.
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
            App.Log("Installazione dell'aggiornamento non riuscita: " + ex, force: true);
        }
    }

    private static void Set(UpdateStatus status)
    {
        Status = status;
        if (status != UpdateStatus.Error) Error = null;
        Changed?.Invoke();
    }

    /// <summary>Messaggio comprensibile al posto dell'errore tecnico.</summary>
    private static string Describe(Exception ex)
    {
        string text = ex.ToString();
        if (ex is System.Net.Http.HttpRequestException or TaskCanceledException ||
            text.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Host sconosciuto", StringComparison.OrdinalIgnoreCase))
            return "Nessuna connessione a GitHub. Riprova più tardi.";
        if (text.Contains("403") || text.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "GitHub ha rifiutato la richiesta per troppi controlli. Riprova tra un'ora.";
        return "Non è stato possibile controllare gli aggiornamenti.";
    }
}
