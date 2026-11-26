using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace KiyoControl;

public partial class MainWindow : Window
{
    private readonly AppModel _model;
    private readonly PreviewController _preview;
    private readonly Dictionary<string, bool> _collapsed = [];
    private PanelBuilder? _builder;
    private (Tab Tab, bool Connected, bool Razer)? _builtFor;
    private Tab _tab = Tab.Camera;
    private bool _updating;
    private bool _wasConnected;
    private string? _previewCamera;

    /// <summary>Set by the app when quitting, so closing really closes instead of hiding to the tray.</summary>
    public bool AllowClose { get; set; }

    private CameraController C => _model.Controller;

    public MainWindow(AppModel model)
    {
        InitializeComponent();
        _model = model;
        _preview = new PreviewController(PreviewImage, model.Settings);
        _preview.StateChanged += UpdatePreviewBar;
        _model.Changed += Refresh;
        IsVisibleChanged += OnVisibilityChanged;
        Refresh();
    }

    // Lifecycle: the preview runs only while the window is visible, so the camera light goes off when hidden.

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && _model.Settings.PreviewEnabled && C.Connected) await _preview.StartAsync();
        else if (!IsVisible) await _preview.StopAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            // Closing the window keeps the app in the tray, so profiles still apply when the camera is plugged in.
            e.Cancel = true;
            Hide();
            ((App)System.Windows.Application.Current).ShowTrayHintOnce();
        }
        base.OnClosing(e);
    }

    public Task ShutdownPreviewAsync() => _preview.StopAsync();

    // Refresh

    private void Refresh()
    {
        _updating = true;
        try
        {
            if (!C.Connected)
            {
                // In place of the disabled controls: the cameras plugged in and why none is connected.
                SettingsHost.Content = CameraListView.NoCameraCard(_model);
                _builtFor = null;
                _builder = null;
            }
            else
            {
                var key = (_tab, C.Connected, C.SupportsRazer);
                if (_builtFor != key)
                {
                    var offset = _builtFor?.Tab == _tab ? SettingsScroll.VerticalOffset : 0;
                    _builder = new PanelBuilder(_model, _collapsed);
                    SettingsHost.Content = _builder.Build(_tab);
                    SettingsScroll.ScrollToVerticalOffset(offset);
                    _builtFor = key;
                }
                else
                {
                    _builder?.Update();
                }
            }

            ProfileBox.ItemsSource = null;
            ProfileBox.ItemsSource = C.ProfileNames;
            ProfileBox.SelectedItem = C.ActiveProfile;
            ResetAllButton.IsEnabled = C.Connected;
            SaveButton.IsEnabled = C.IsDirty;
            SaveButton.ToolTip = C.SupportsRazer
                ? "Saves this profile (Ctrl+S). Razer settings are also stored in the camera, so they stick without the app."
                : "Saves this profile (Ctrl+S). Kiyo Control applies it again when the camera is plugged in.";
            RevertButton.IsEnabled = C.IsDirty && C.Connected;
            SummaryText.Text = Summary();
            Title = C.IsDirty ? "Kiyo Control •" : "Kiyo Control";

            if (C.LastError is string error)
            {
                ErrorText.Text = error;
                ErrorBanner.Visibility = Visibility.Visible;
            }
            else ErrorBanner.Visibility = Visibility.Collapsed;
        }
        finally { _updating = false; }

        // On connect, replug (which can change the formats offered, e.g. on a USB 3 port) or a switch to another
        // Kiyo, show the camera the settings control.
        _preview.CameraId = C.CameraId;
        if (C.Connected != _wasConnected || (C.Connected && !CameraDiscovery.SameDevice(C.CameraId, _previewCamera)))
        {
            _wasConnected = C.Connected;
            _previewCamera = C.CameraId;
            if (IsVisible && _model.Settings.PreviewEnabled)
            {
                if (C.Connected) _ = _preview.RestartAsync();
                else _ = _preview.StopAsync();
            }
        }
        UpdatePreviewBar();
    }

    private string Summary()
    {
        if (!C.Connected) return _model.DisconnectedTitle.ToUpperInvariant();
        var parts = new List<string>();
        if (_preview.Mode is VideoMode m) parts.Add(m.Label);
        parts.Add(Fmt.Zoom(C.Value(K.Zoom)));
        if (C.SupportsRazer) parts.Add($"FOV {Fmt.Fov(C.Value(K.Zoom))}");
        if (C.IsAuto(K.AutoExposure)) parts.Add("AE");
        else if (C.SupportsRazer)
        {
            if (C.Razer.Iso is int iso) parts.Add($"ISO {iso}");
            if (C.Razer.ShutterMicroseconds is int us) parts.Add($"SS 1/{1_000_000 / us}S");
        }
        else
        {
            // Models without Razer commands: the standard exposure time and gain.
            if (C.Has(K.ExposureTime)) parts.Add($"SS {Fmt.ExposureLog2(C.Value(K.ExposureTime))}");
            if (C.Has(K.Gain)) parts.Add($"GAIN {C.Value(K.Gain)}");
        }
        parts.Add(C.IsAuto(K.AutoWhiteBalance) ? "AWB" : Fmt.Kelvin(C.Value(K.WhiteBalance)));
        return string.Join("   ", parts).ToUpperInvariant();
    }

    private void UpdatePreviewBar()
    {
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            PreviewSwitch.IsChecked = _model.Settings.PreviewEnabled;
            PreviewStatus.Text = !_model.Settings.PreviewEnabled ? "Preview off"
                : !C.Connected ? "No Kiyo connected"
                : _preview.Status;
            if (!_model.Settings.PreviewEnabled || !C.Connected) PreviewImage.Source = null;

            ModeBox.ItemsSource = _preview.Modes.Select(m => m.Label).ToList();
            ModeBox.SelectedIndex = _preview.Mode is VideoMode mode ? _preview.Modes.IndexOf(mode) : -1;
            ModeBox.IsEnabled = _preview.Running && !_preview.Shared && _preview.Modes.Count > 1;
            ModeBox.Visibility = _preview.Modes.Count > 0 ? Visibility.Visible : Visibility.Hidden;
            // Only for cameras that can stream 4K: a 1080p camera loses nothing over USB 2.
            Usb2Warning.Visibility = C.Connected && C.Device.SupportsFourK && _preview.Modes.Count > 0 && _preview.Modes.All(m => m.Height < 2160)
                ? Visibility.Visible : Visibility.Collapsed;
            SummaryText.Text = Summary();
        }
        finally { _updating = wasUpdating; }
    }

    // Top bar

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        _tab = sender == ProcessingTabButton ? Tab.Processing : sender == ImageTabButton ? Tab.Image : Tab.Camera;
        if (IsLoaded) Refresh();
    }

    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || ProfileBox.SelectedItem is not string name || name == C.ActiveProfile) return;
        if (C.IsDirty && !Confirm("Discard unsaved changes?", "Your changes to this profile are not saved.", "Discard and Switch"))
        {
            Refresh();
            return;
        }
        _model.Perform(c => c.SwitchProfile(name));
    }

    private void OnMoreClicked(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = MoreButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var create = new MenuItem { Header = "New Profile…" };
        create.Click += (_, _) => NewProfile();
        menu.Items.Add(create);

        var delete = new MenuItem { Header = "Delete Profile", IsEnabled = C.ProfileNames.Count > 1 };
        delete.Click += (_, _) =>
        {
            if (Confirm($"Delete the profile \"{C.ActiveProfile}\"?", "This can't be undone.", "Delete"))
                _model.Perform(c => c.DeleteActiveProfile());
        };
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());

        var restore = new MenuItem { Header = "Restore Backup", IsEnabled = C.Backups.Count > 0 && C.Connected };
        foreach (var b in C.Backups)
        {
            var item = new MenuItem { Header = $"{b.Profile.SavedAt.ToLocalTime():g} · {b.Profile.Name}" };
            item.Click += (_, _) => _model.Perform(c => c.Restore(b));
            restore.Items.Add(item);
        }
        menu.Items.Add(restore);

        var reveal = new MenuItem { Header = "Show Backups in File Explorer" };
        reveal.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{C.Store.BackupsDir}\"") { UseShellExecute = true });
        menu.Items.Add(reveal);

        // With two or more supported cameras plugged in, switch between them.
        if (_model.SupportedCameras.Count > 1)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Camera", IsEnabled = false });
            foreach (var camera in _model.SupportedCameras)
            {
                var item = new MenuItem { Header = camera.Name, IsCheckable = true, IsChecked = CameraDiscovery.SameDevice(camera.Id, C.CameraId) };
                item.Click += (_, _) => _model.Select(camera);
                menu.Items.Add(item);
            }
        }

        if (C.Device.FirmwareUpdater is string updater)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = $"Firmware {C.FirmwareVersion ?? "unknown"}", IsEnabled = false });
            var link = new MenuItem { Header = "Razer Firmware Updater…" };
            link.Click += (_, _) => Process.Start(new ProcessStartInfo(updater) { UseShellExecute = true });
            menu.Items.Add(link);
        }

        menu.Items.Add(new Separator());
        var app = (App)System.Windows.Application.Current;
        if (app.Updater.Pending is { } pending)
        {
            var restart = new MenuItem { Header = $"Restart to Update ({pending.Version})" };
            restart.Click += (_, _) => app.RestartToUpdate();
            menu.Items.Add(restart);
        }
        menu.Items.Add(new MenuItem { Header = $"Kiyo Control {Updater.Version}", IsEnabled = false });
        var project = new MenuItem { Header = "Project Page on GitHub…" };
        project.Click += (_, _) => Process.Start(new ProcessStartInfo(Updater.RepoUrl) { UseShellExecute = true });
        menu.Items.Add(project);
        menu.IsOpen = true;
    }

    private void NewProfile()
    {
        var name = InputDialog.Ask(this, "New Profile", "Saves the current settings as a new profile.", "Create");
        if (name is null) return;
        var created = false;
        _model.Perform(c => created = c.CreateProfile(name));
        if (!created)
            MessageBox.Show(this, $"A profile named \"{name.Trim()}\" already exists, or the name is empty.", "New Profile",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        if (Confirm("Reset all settings to the camera's factory defaults?",
                "A backup of the current settings is saved first, so you can restore it.", "Reset All Settings"))
            _model.Perform(c => c.ResetAll());
    }

    // Bottom bar

    private void OnSave(object sender, RoutedEventArgs e) => _model.Perform(c => c.Save());

    private void OnSaveCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (C.IsDirty) _model.Perform(c => c.Save());
    }

    private void OnRevert(object sender, RoutedEventArgs e) => _model.Perform(c => c.Revert());

    private void OnDismissError(object sender, RoutedEventArgs e) => _model.Perform(c => c.LastError = null);

    private async void OnPreviewToggled(object sender, RoutedEventArgs e)
    {
        _model.Settings.PreviewEnabled = PreviewSwitch.IsChecked == true;
        _model.Settings.Save();
        if (_model.Settings.PreviewEnabled && C.Connected) await _preview.StartAsync();
        else await _preview.StopAsync();
        UpdatePreviewBar();
    }

    private async void OnModeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || ModeBox.SelectedIndex < 0 || ModeBox.SelectedIndex >= _preview.Modes.Count) return;
        var mode = _preview.Modes[ModeBox.SelectedIndex];
        if (mode != _preview.Mode) await _preview.SelectAsync(mode);
    }

    private bool Confirm(string title, string message, string action) =>
        MessageBox.Show(this, $"{title}\n\n{message}", action, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
}
