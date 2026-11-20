using System.Threading;
using System.Windows;

namespace KiyoControl;

/// <summary>
/// One instance per user. Runs in the tray; the window opens on start (unless started with
/// --background at login) and from the tray icon. A second launch just shows the first one's window.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\KiyoControl.Windows.SingleInstance";
    private const string ShowEventName = @"Local\KiyoControl.Windows.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private AppModel? _model;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private bool _quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, MutexName, out var first);
        if (!first)
        {
            // Already running: ask that instance to show its window.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (_quitting) return;
                Dispatcher.BeginInvoke(ShowWindow);
            }
        }) { IsBackground = true, Name = "Show listener" };
        listener.Start();

        _model = new AppModel();
        _model.Start();
        _window = new MainWindow(_model);
        _tray = new TrayIcon(_model, ShowWindow, Quit);

        if (!e.Args.Contains("--background")) ShowWindow();
    }

    public void ShowWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void ShowTrayHintOnce()
    {
        if (_model is null || _model.Settings.TrayHintShown) return;
        _model.Settings.TrayHintShown = true;
        _model.Settings.Save();
        _tray?.ShowHint("Kiyo Control is still running", "Your profile stays applied. Open it again or quit from the tray icon.");
    }

    private async void Quit()
    {
        _quitting = true;
        if (_window is not null)
        {
            await _window.ShutdownPreviewAsync();
            _window.AllowClose = true;
            _window.Close();
        }
        _model?.Stop();
        _tray?.Dispose();
        _showEvent?.Set();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _model?.Stop();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
