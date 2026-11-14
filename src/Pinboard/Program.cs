using Velopack;

namespace Pinboard;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run first: during install, update and uninstall it starts Pinboard
        // with special arguments, runs these steps and exits right away.
        VelopackApp.Build()
            .OnAfterUpdateFastCallback(_ => AutoStart.Refresh())
            .OnBeforeUninstallFastCallback(_ => AutoStart.Set(false))
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
