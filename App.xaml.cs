using System.Windows;

namespace HyperMusNotifi;

public partial class App : System.Windows.Application
{
    private MediaWatcher? _watcher;
    private NotifyIconController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _tray = new NotifyIconController();
        _watcher = new MediaWatcher();
        _watcher.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        _tray?.Dispose();
        System.Windows.Forms.Application.Exit();
        base.OnExit(e);
    }
}
