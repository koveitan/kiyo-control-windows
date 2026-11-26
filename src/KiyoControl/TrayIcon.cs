using Kiyo.Core;
using WinForms = System.Windows.Forms;

namespace KiyoControl;

/// <summary>
/// The notification-area icon (the Mac app's menu bar icon): open the window, switch profiles,
/// jump to zoom presets, launch at login, quit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly AppModel _model;
    private readonly Updater _updater;
    private readonly Action _open;
    private readonly Action _quit;
    private readonly Action _restartToUpdate;

    public TrayIcon(AppModel model, Updater updater, Action open, Action quit, Action restartToUpdate)
    {
        _model = model;
        _updater = updater;
        _open = open;
        _quit = quit;
        _restartToUpdate = restartToUpdate;
        var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _icon = new WinForms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize),
            Text = "Kiyo Control",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) _open();
        };
        BuildMenu();
    }

    public void ShowHint(string title, string text) => _icon.ShowBalloonTip(4000, title, text, WinForms.ToolTipIcon.None);

    private void BuildMenu()
    {
        var c = _model.Controller;
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();
        menu.Items.Add(new WinForms.ToolStripMenuItem("Open Kiyo Control", null, (_, _) => _open()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new WinForms.ToolStripLabel(c.Connected ? $"{c.Device.Name} connected" : _model.DisconnectedTitle) { ForeColor = System.Drawing.SystemColors.GrayText });
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // With two or more supported cameras plugged in, switch between them.
        if (_model.SupportedCameras.Count > 1)
        {
            var cameras = new WinForms.ToolStripMenuItem("Camera");
            foreach (var camera in _model.SupportedCameras)
            {
                var item = new WinForms.ToolStripMenuItem(camera.Name) { Checked = CameraDiscovery.SameDevice(camera.Id, c.CameraId) };
                item.Click += (_, _) => _model.Select(camera);
                cameras.DropDownItems.Add(item);
            }
            menu.Items.Add(cameras);
        }

        var profiles = new WinForms.ToolStripMenuItem("Profile");
        foreach (var name in c.ProfileNames)
        {
            var item = new WinForms.ToolStripMenuItem(name) { Checked = name == c.ActiveProfile };
            item.Click += (_, _) => _model.Perform(x => x.SwitchProfile(name));
            profiles.DropDownItems.Add(item);
        }
        menu.Items.Add(profiles);

        var presets = new WinForms.ToolStripMenuItem("Zoom Preset") { Enabled = c.Connected };
        for (var slot = 0; slot < ZoomPreset.Slots; slot++)
        {
            var s = slot;
            var p = c.ZoomPresets[s];
            var item = new WinForms.ToolStripMenuItem(p is null ? $"{s + 1}  (empty)" : $"{s + 1}  {Fmt.Zoom(p.Zoom)}") { Enabled = p is not null };
            item.Click += (_, _) => _model.Perform(x => x.ApplyZoomPreset(s));
            presets.DropDownItems.Add(item);
        }
        menu.Items.Add(presets);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        var login = new WinForms.ToolStripMenuItem("Launch at Login") { Checked = LaunchAtLogin.Enabled, CheckOnClick = true };
        login.Click += (_, _) =>
        {
            try { LaunchAtLogin.Enabled = login.Checked; }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException) { login.Checked = LaunchAtLogin.Enabled; }
        };
        menu.Items.Add(login);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        if (_updater.Pending is { } pending)
            menu.Items.Add(new WinForms.ToolStripMenuItem($"Restart to Update ({pending.Version})", null, (_, _) => _restartToUpdate()));
        menu.Items.Add(new WinForms.ToolStripLabel($"Version {Updater.Version}") { ForeColor = System.Drawing.SystemColors.GrayText });
        menu.Items.Add(new WinForms.ToolStripMenuItem("Quit Kiyo Control", null, (_, _) => _quit()));
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
