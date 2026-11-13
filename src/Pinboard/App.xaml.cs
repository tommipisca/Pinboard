using System.IO;
using System.Windows;

namespace Pinboard;

public partial class App : Application
{
    private Mutex? _singleInstance;

    /// <summary>--floating: finestre normali in primo piano, per provare il disegno.</summary>
    internal static bool Floating { get; private set; }

    /// <summary>--debug: scrive il log in %APPDATA%\Pinboard\pinboard.log.</summary>
    internal static bool Debug { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "Pinboard.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
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

        WidgetManager.Start(new ClockWidget(), new CalendarWidget(), new ExamsWidget());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WidgetManager.Shutdown();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    internal static void Log(string message, bool force = false)
    {
        if (!force && !Debug) return;
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pinboard");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "pinboard.log"),
                $"{DateTime.Now:s} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
