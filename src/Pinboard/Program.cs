using Velopack;

namespace Pinboard;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack deve partire per primo: durante installazione, aggiornamento e disinstallazione
        // avvia Pinboard con argomenti speciali, esegue questi passi ed esce subito.
        VelopackApp.Build()
            .OnAfterUpdateFastCallback(_ => AutoStart.Refresh())
            .OnBeforeUninstallFastCallback(_ => AutoStart.Set(false))
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
