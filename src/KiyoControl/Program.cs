using Velopack;

namespace KiyoControl;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles install / update / uninstall hooks and applies a downloaded update on start.
        VelopackApp.Build()
            // A removed app should not stay in the Run key.
            .OnBeforeUninstallFastCallback(_ =>
            {
                try { LaunchAtLogin.Enabled = false; } catch (Exception) { }
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
