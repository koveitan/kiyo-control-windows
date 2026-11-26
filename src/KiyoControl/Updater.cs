using System.Reflection;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace KiyoControl;

/// <summary>
/// Updates from GitHub Releases via Velopack. Only active in an installed copy (not when run from source):
/// checks shortly after start and then twice a day, downloads in the background, and installs the update
/// when the user restarts from the tray or quits (or at the next launch).
/// </summary>
public sealed class Updater
{
    public const string RepoUrl = Kiyo.Core.ProjectInfo.ProjectUrl;

    private readonly UpdateManager? _manager;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(12) };
    private bool _checking;

    /// <summary>A downloaded update waiting to be installed, or null.</summary>
    public VelopackAsset? Pending { get; private set; }

    /// <summary>Raised on the UI thread when an update has been downloaded.</summary>
    public event Action? UpdateReady;

    public Updater()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (manager.IsInstalled) _manager = manager;
        }
        catch (Exception) { }
    }

    public bool IsInstalled => _manager is not null;

    /// <summary>The app's version, e.g. "0.1.0".</summary>
    public static string Version
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var plus = v.IndexOf('+');
            return plus < 0 ? v : v[..plus];
        }
    }

    public void Start()
    {
        if (_manager is null) return;
        Pending = _manager.UpdatePendingRestart;
        // Give the camera and preview a moment first.
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        first.Tick += async (_, _) =>
        {
            first.Stop();
            await CheckAsync();
        };
        first.Start();
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
    }

    public async Task CheckAsync()
    {
        if (_manager is null || _checking) return;
        _checking = true;
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return;
            await _manager.DownloadUpdatesAsync(update);
            Pending = update.TargetFullRelease;
            UpdateReady?.Invoke();
        }
        catch (Exception)
        {
            // Offline, rate-limited or GitHub unavailable: try again at the next check.
        }
        finally { _checking = false; }
    }

    /// <summary>Installs the downloaded update and starts the new version. Exits this process.</summary>
    public void RestartToUpdate()
    {
        if (_manager is not null && Pending is not null) _manager.ApplyUpdatesAndRestart(Pending);
    }

    /// <summary>When quitting: install the downloaded update after this process exits, without restarting.</summary>
    public void ApplyOnExit()
    {
        if (_manager is null || Pending is null) return;
        try { _manager.WaitExitThenApplyUpdates(Pending, silent: true, restart: false); }
        catch (Exception) { }
    }
}
