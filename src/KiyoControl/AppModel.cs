using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Kiyo.Core;
using Kiyo.Windows;
using Microsoft.Win32;

namespace KiyoControl;

/// <summary>
/// Owns the <see cref="CameraController"/> on the UI thread: polls for the camera every 2 seconds,
/// re-applies settings after the PC wakes, and tells the UI when something changed.
/// </summary>
public sealed class AppModel
{
    public CameraController Controller { get; }
    public AppSettings Settings { get; } = AppSettings.Load();

    /// <summary>Raised after every change, on the UI thread.</summary>
    public event Action? Changed;

    private readonly DispatcherTimer _poll;

    public AppModel()
    {
        Controller = new CameraController(new ProfileStore(), () => WindowsCameraBackend.Connect());
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += (_, _) => Poll();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Start()
    {
        Poll();
        _poll.Start();
    }

    public void Stop()
    {
        _poll.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Controller.Disconnect();
    }

    /// <summary>Runs a controller action and tells the UI to refresh.</summary>
    public void Perform(Action<CameraController> action)
    {
        action(Controller);
        Changed?.Invoke();
    }

    /// <summary>Refreshes the UI only when the connection changes, not on every 2-second check.</summary>
    private void Poll()
    {
        var was = Controller.Connected;
        Controller.Poll();
        if (Controller.Connected != was) Changed?.Invoke();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        // The camera can lose its settings across sleep, so write them again a moment after wake.
        var timer = new DispatcherTimer(DispatcherPriority.Normal, System.Windows.Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Poll();
            if (Controller.Connected) Perform(c => c.Reapply());
        };
        timer.Start();
    }
}

/// <summary>App preferences (not camera settings), in %APPDATA%\KiyoControl\app-settings.json.</summary>
public sealed class AppSettings
{
    public bool PreviewEnabled { get; set; } = true;
    public string? PreviewMode { get; set; }
    public bool TrayHintShown { get; set; }

    private static string FilePath => Path.Combine(ProfileStore.DefaultRoot, "app-settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ProfileStore.DefaultRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Start with Windows, via the per-user Run key (no admin rights needed).</summary>
public static class LaunchAtLogin
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KiyoControl";

    private static string Command => $"\"{Environment.ProcessPath}\" --background";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && s.Equals(Command, StringComparison.OrdinalIgnoreCase);
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

/// <summary>Display formats, like the Mac app's.</summary>
public static class Fmt
{
    public static string Zoom(int v) => $"{v / 100.0:0.0}X";
    public static string Fov(int zoom) => $"{Math.Round(FieldOfView.Degrees(zoom))}°";
    /// <summary>Windows reports pan and tilt in degrees.</summary>
    public static string Degrees(int v) => $"{v}°";
    public static string Kelvin(int v) => $"{v}K";
    /// <summary>DirectShow exposure is log2 seconds: -6 is 1/64 s.</summary>
    public static string ExposureLog2(int v) => v < 0 ? $"1/{Math.Round(Math.Pow(2, -v))}S" : $"{Math.Pow(2, v):0.#}S";

    /// <summary>Synapse's shutter steps, as 1/x seconds.</summary>
    public static readonly int[] ShutterDivisors =
        [2000, 1600, 1280, 1000, 800, 640, 500, 400, 320, 250, 200, 160, 120, 100,
         90, 75, 60, 50, 48, 40, 33, 30, 25, 24, 20, 17, 16, 15, 13, 12, 11, 10];
}
