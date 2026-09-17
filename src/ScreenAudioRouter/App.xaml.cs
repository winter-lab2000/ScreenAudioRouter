using System.Windows;
using ScreenAudioRouter.Services;

namespace ScreenAudioRouter;

public partial class App : Application
{
    public static LogService Log { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Info("=== ScreenAudioRouter starting ===");
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("=== ScreenAudioRouter exiting ===");
        base.OnExit(e);
    }
}
