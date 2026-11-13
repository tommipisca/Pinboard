using System.IO;
using System.Windows;

namespace Pinboard.Clock;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "Pinboard.Clock.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception.ToString());
            MessageBox.Show(args.Exception.Message, "Pinboard Clock");
            args.Handled = true;
        };

        // --floating: finestra normale sopra le altre, per controllare che il disegno funzioni
        // senza l'aggancio al desktop.
        Pinboard.Clock.MainWindow.Floating = e.Args.Contains("--floating");
        new MainWindow().Show();
    }

    internal static void Log(string message)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pinboard");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "clock.log"), $"{DateTime.Now:s} {message}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
