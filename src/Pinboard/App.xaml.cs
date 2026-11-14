using System.IO;
using System.Windows;

namespace Pinboard;

public partial class App : Application
{
    private const string InstanceName = "Pinboard.SingleInstance";
    private const string ShowEventName = "Pinboard.ShowDashboard";

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private static DashboardWindow? _dashboard;

    /// <summary>--floating: finestre normali in primo piano, per provare il disegno.</summary>
    internal static bool Floating { get; private set; }

    /// <summary>--debug: scrive il log in %APPDATA%\Pinboard\pinboard.log.</summary>
    internal static bool Debug { get; private set; }

    internal static bool Exiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, InstanceName, out bool isFirst);
        if (!isFirst)
        {
            // Pinboard e' gia' aperto: gli si chiede di mostrare la dashboard.
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ShowEventName);
                signal.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Floating = e.Args.Contains("--floating");
        Debug = e.Args.Contains("--debug");

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception.ToString(), force: true);
            MessageBox.Show(args.Exception.Message, "Pinboard");
            args.Handled = true;
        };

        ListenForShowRequests();
        UiTheme.Apply();
        AutoStart.Refresh();
        WidgetManager.Start();

        if (!e.Args.Contains(AutoStart.BackgroundArg)) ShowDashboard();
    }

    /// <summary>Un secondo avvio di Pinboard segnala questo evento per riaprire la dashboard.</summary>
    private void ListenForShowRequests()
    {
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var thread = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.BeginInvoke(new Action(ShowDashboard));
        })
        { IsBackground = true, Name = "Pinboard show requests" };
        thread.Start();
    }

    internal static void Quit()
    {
        Exiting = true;
        Current.Shutdown();
    }

    internal static void ShowDashboard()
    {
        _dashboard ??= new DashboardWindow();
        if (!_dashboard.IsVisible) _dashboard.Show();
        if (_dashboard.WindowState == WindowState.Minimized) _dashboard.WindowState = WindowState.Normal;
        _dashboard.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Exiting = true;
        WidgetManager.Shutdown();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    internal static void Log(string message, bool force = false)
    {
        if (!force && !Debug) return;
        try
        {
            Directory.CreateDirectory(AppSettings.Folder);
            File.AppendAllText(Path.Combine(AppSettings.Folder, "pinboard.log"),
                $"{DateTime.Now:s} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
